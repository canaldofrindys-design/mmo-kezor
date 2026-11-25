using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MuProto.Core.Server
{
    /// <summary>
    /// Host TCP com protocolo de linhas UTF-8 ("COMANDO args\n" -> "OK|ERR|FAIL ...\n").
    /// Threads de rede só enfileiram; a lógica roda em Pump() na thread principal (Unity Update ou loop headless).
    /// </summary>
    public sealed class TcpServerHost : IDisposable
    {
        private readonly GameServer _game;
        private readonly TcpListener _listener;
        private readonly ConcurrentQueue<(int session, string line)> _inbox = new ConcurrentQueue<(int, string)>();
        private readonly ConcurrentDictionary<int, StreamWriter> _writers = new ConcurrentDictionary<int, StreamWriter>();
        private readonly ConcurrentQueue<int> _disconnects = new ConcurrentQueue<int>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _nextSession;
        public const int MaxLineLength = 512;

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public TcpServerHost(GameServer game, int port)
        {
            _game = game;
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            _listener.Start();
            _ = AcceptLoop();
        }

        private async Task AcceptLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(); } catch { return; }
                int id = Interlocked.Increment(ref _nextSession);
                _ = ClientLoop(id, client);
            }
        }

        private async Task ClientLoop(int id, TcpClient client)
        {
            using (client)
            {
                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.UTF8);
                var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                _writers[id] = writer;
                _inbox.Enqueue((id, "\u0001CONNECT"));
                try
                {
                    string? line;
                    while (!_cts.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Length > MaxLineLength) break; // proteção simples contra abuso
                        _inbox.Enqueue((id, line));
                    }
                }
                catch { /* conexão caiu */ }
                _writers.TryRemove(id, out _);
                _disconnects.Enqueue(id);
            }
        }

        /// <summary>Processa mensagens pendentes. Chamar uma vez por frame/tick na thread do jogo.</summary>
        public void Pump(int maxMessages = 1000)
        {
            while (maxMessages-- > 0 && _inbox.TryDequeue(out var msg))
            {
                string reply = msg.line == "\u0001CONNECT"
                    ? (_game.Connect(msg.session) != null ? "OK conectado ao MuProto. Digite HELP" : "")
                    : _game.Handle(msg.session, msg.line);
                if (_writers.TryGetValue(msg.session, out var w))
                {
                    try { w.WriteLine(reply); } catch { }
                }
            }
            while (_disconnects.TryDequeue(out var id)) _game.Disconnect(id);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
        }
    }
}
