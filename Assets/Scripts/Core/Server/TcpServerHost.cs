using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Host TCP com protocolo de linhas UTF-8. Threads de rede só enfileiram; a lógica roda em Pump()/Tick()
    /// na thread do jogo (Unity Update ou loop headless). Flush() envia os Outbox das sessões.
    /// </summary>
    public sealed class TcpServerHost : IDisposable
    {
        private readonly GameServer _game;
        private readonly TcpListener _listener;
        private readonly ConcurrentQueue<(int session, string line)> _inbox = new ConcurrentQueue<(int, string)>();
        private readonly ConcurrentDictionary<int, StreamWriter> _writers = new ConcurrentDictionary<int, StreamWriter>();
        private readonly ConcurrentDictionary<int, TcpClient> _clients = new ConcurrentDictionary<int, TcpClient>();
        private readonly ConcurrentDictionary<int, long> _lastSeen = new ConcurrentDictionary<int, long>();   // ticks UTC da última linha recebida
        private readonly ConcurrentQueue<int> _connects = new ConcurrentQueue<int>();
        private readonly ConcurrentQueue<int> _disconnects = new ConcurrentQueue<int>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _nextSession = 1000; // sessões TCP começam em 1001
        /// <summary>Conexão sem mandar nada por tanto tempo é derrubada (o cliente manda PING a cada poucos segundos). 0 = nunca.</summary>
        public float IdleTimeoutSeconds = 60f;
        public const int MaxLineLength = 512;
        public const int MaxMessagesPerSecond = 60;

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public int ConnectionCount => _writers.Count;

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
                try { client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false); } catch { return; }
                client.NoDelay = true;
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
                var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = false, NewLine = "\n" };
                _writers[id] = writer;
                _clients[id] = client;
                _lastSeen[id] = DateTime.UtcNow.Ticks;
                _connects.Enqueue(id);
                int count = 0; var window = DateTime.UtcNow;
                try
                {
                    string? line;
                    while (!_cts.IsCancellationRequested && (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        if (line.Length > MaxLineLength) break; // proteção contra abuso
                        _lastSeen[id] = DateTime.UtcNow.Ticks;
                        if ((DateTime.UtcNow - window).TotalSeconds >= 1) { window = DateTime.UtcNow; count = 0; }
                        if (++count > MaxMessagesPerSecond) continue; // flood: descarta
                        _inbox.Enqueue((id, line));
                    }
                }
                catch { /* conexão caiu */ }
                _writers.TryRemove(id, out _);
                _clients.TryRemove(id, out _);
                _lastSeen.TryRemove(id, out _);
                _disconnects.Enqueue(id);
            }
        }

        /// <summary>Processa mensagens pendentes. Chamar uma vez por frame/tick na thread do jogo.</summary>
        public void Pump(int maxMessages = 2000)
        {
            while (_connects.TryDequeue(out var cid))
            {
                _game.Connect(cid);
                Write(cid, "@EV SYS;Conectado ao servidor Kezor.");
            }
            while (maxMessages-- > 0 && _inbox.TryDequeue(out var msg))
                Write(msg.session, _game.Handle(msg.session, msg.line));
            while (_disconnects.TryDequeue(out var id)) _game.Disconnect(id);
            DropIdle();
            Flush();
            DropKicked();
        }

        /// <summary>Expulsos ou banidos por um GM: a mensagem já foi enviada no Flush; agora a conexão é fechada.</summary>
        private void DropKicked()
        {
            while (_game.TryTakeKick(out var id))
            {
                _lastSeen.TryRemove(id, out _);
                if (_clients.TryGetValue(id, out var c)) { try { c.Close(); } catch { } }   // o ClientLoop enfileira a desconexão
                else _game.Disconnect(id);
            }
        }

        /// <summary>Envia as mensagens empurradas (snapshots, eventos) de todas as sessões.</summary>
        public void Flush()
        {
            foreach (var kv in _writers)
            {
                var lines = _game.DrainOutbox(kv.Key);
                if (lines != null) foreach (var l in lines) Write(kv.Key, l, false);
                try { kv.Value.Flush(); } catch { }
            }
        }

        /// <summary>Derruba conexões mudas (cabo puxado, jogo travado): o herói é salvo e a conta fica livre para entrar de novo.</summary>
        private void DropIdle()
        {
            if (IdleTimeoutSeconds <= 0) return;
            long limit = DateTime.UtcNow.Ticks - (long)(IdleTimeoutSeconds * TimeSpan.TicksPerSecond);
            foreach (var kv in _lastSeen)
                if (kv.Value < limit && _clients.TryGetValue(kv.Key, out var c))
                {
                    _lastSeen.TryRemove(kv.Key, out _);
                    Write(kv.Key, "@EV SYS;Desconectado por inatividade.");
                    try { c.Close(); } catch { }   // o ClientLoop percebe e enfileira a desconexão
                }
        }

        private void Write(int session, string line, bool flush = true)
        {
            if (!_writers.TryGetValue(session, out var w)) return;
            try { w.WriteLine(line); if (flush) w.Flush(); } catch { }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            foreach (var c in _clients.Values) { try { c.Close(); } catch { } }   // derruba os jogadores (o servidor já salvou)
            _clients.Clear();
        }
    }
}
