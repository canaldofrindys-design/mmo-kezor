using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Kezor.Core.Server;

namespace Kezor.Core.Net
{
    /// <summary>Conexão do cliente com um servidor (local ou remoto). Mensagens são linhas de texto.</summary>
    public interface IClientConnection : IDisposable
    {
        bool Connected { get; }
        string Error { get; }
        void Send(string line);
        bool TryReceive(out string line);
        /// <summary>Chamado todo frame pelo cliente.</summary>
        void Update(float dt);
    }

    /// <summary>
    /// Conexão em memória com um GameServer, sem socket. Só para testes automáticos: o jogo não tem modo solo e sempre
    /// entra pela rede (<see cref="TcpClientConnection"/>), inclusive no servidor deste PC.
    /// </summary>
    public sealed class LoopbackConnection : IClientConnection
    {
        private const int SessionId = 1;
        private readonly Queue<string> _in = new Queue<string>();
        public GameServer Server { get; }
        public bool Connected => true;
        public string Error => "";

        public LoopbackConnection(GameServer server)
        {
            Server = server;
            Server.Connect(SessionId);
            _in.Enqueue("@EV SYS;Servidor local iniciado.");
        }

        public void Send(string line)
        {
            _in.Enqueue(Server.Handle(SessionId, line));
            Drain();
        }

        public void Update(float dt) { Server.Tick(dt); Drain(); }

        private void Drain()
        {
            var l = Server.DrainOutbox(SessionId);
            if (l != null) foreach (var s in l) _in.Enqueue(s);
        }

        public bool TryReceive(out string line)
        {
            if (_in.Count > 0) { line = _in.Dequeue(); return true; }
            line = ""; return false;
        }

        public void Dispose() => Server.Disconnect(SessionId);
    }

    /// <summary>Cliente TCP: thread de leitura enfileira linhas; envio síncrono com lock.</summary>
    public sealed class TcpClientConnection : IClientConnection
    {
        private readonly TcpClient _tcp = new TcpClient();
        private readonly ConcurrentQueue<string> _in = new ConcurrentQueue<string>();
        private StreamWriter? _w;
        private readonly object _lock = new object();
        private volatile bool _connected;
        private volatile string _error = "";
        public bool Connected => _connected;
        public string Error => _error;

        public TcpClientConnection(string host, int port)
        {
            var t = new Thread(() =>
            {
                try
                {
                    _tcp.NoDelay = true;
                    _tcp.Connect(host, port);
                    var s = _tcp.GetStream();
                    var r = new StreamReader(s, Encoding.UTF8);
                    lock (_lock) _w = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                    _connected = true;
                    string? line;
                    while ((line = r.ReadLine()) != null) _in.Enqueue(line);
                    _error = "conexão encerrada pelo servidor";
                }
                catch (Exception e) { _error = e.Message; }
                _connected = false;
            }) { IsBackground = true, Name = "Kezor-Net" };
            t.Start();
        }

        public void Send(string line)
        {
            lock (_lock)
            {
                if (_w == null) return;
                try { _w.WriteLine(line); } catch (Exception e) { _error = e.Message; _connected = false; }
            }
        }

        public bool TryReceive(out string line)
        {
            if (_in.TryDequeue(out var l)) { line = l; return true; }
            line = ""; return false;
        }

        public void Update(float dt) { }
        public void Dispose() { try { _tcp.Close(); } catch { } }
    }
}
