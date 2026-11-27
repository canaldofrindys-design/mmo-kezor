using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Host TCP com protocolo de linhas UTF-8. As threads de rede só leem e escrevem; toda a lógica roda em Pump()/Tick()
    /// na thread do jogo (Unity Update ou loop do servidor dedicado).
    ///
    /// Para aguentar muita gente e rede ruim:
    ///  • cada conexão tem a sua fila de envio e uma tarefa que escreve no socket: um jogador com internet lenta nunca
    ///    trava o servidor; se a fila dele passar do limite, só ele cai ("conexão lenta demais");
    ///  • limite de conexões no total e por endereço IP; linhas grandes e flood de mensagens são barrados;
    ///  • conexão muda (cabo puxado, jogo travado) cai depois de IdleTimeoutSeconds; keep-alive do TCP ligado;
    ///  • antes de fechar, o servidor manda "@BYE motivo" (expulso, banido, servidor desligando...), assim o cliente
    ///    sabe que não deve tentar reconectar sozinho.
    /// </summary>
    public sealed class TcpServerHost : IDisposable
    {
        private sealed class Client
        {
            public int Id;
            public TcpClient Tcp = null!;
            public string Address = "";
            public readonly ConcurrentQueue<string> Out = new ConcurrentQueue<string>();
            public readonly SemaphoreSlim Signal = new SemaphoreSlim(0);
            public int Queued;
            public long LastSeen;
            public volatile bool Closing;
        }

        private readonly GameServer _game;
        private readonly TcpListener _listener;
        private readonly ConcurrentQueue<(int session, string line)> _inbox = new ConcurrentQueue<(int, string)>();
        private readonly ConcurrentDictionary<int, Client> _clients = new ConcurrentDictionary<int, Client>();
        private readonly ConcurrentQueue<Client> _connects = new ConcurrentQueue<Client>();
        private readonly ConcurrentQueue<int> _disconnects = new ConcurrentQueue<int>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _nextSession = 1000; // sessões TCP começam em 1001
        private const string CloseMarker = "\u0000close";

        /// <summary>Conexão sem mandar nada por tanto tempo é derrubada (o cliente manda PING a cada poucos segundos). 0 = nunca.</summary>
        public float IdleTimeoutSeconds = 60f;
        /// <summary>Conexões ao mesmo tempo (todas) e vindas do mesmo IP. 0 = sem limite.</summary>
        public int MaxConnections = 1000, MaxConnectionsPerAddress = 10;
        /// <summary>Linhas esperando para sair numa conexão; passou disso, a conexão é derrubada por estar lenta demais.</summary>
        public int MaxQueuedLines = 4000;
        public const int MaxLineLength = 512;
        public const int MaxMessagesPerSecond = 60;

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public int ConnectionCount => _clients.Count;

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
                TcpClient tcp;
                try { tcp = await _listener.AcceptTcpClientAsync().ConfigureAwait(false); } catch { if (_cts.IsCancellationRequested) return; continue; }
                try
                {
                    tcp.NoDelay = true;
                    tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    tcp.SendTimeout = 10000;
                }
                catch { }
                string addr = (tcp.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "?";
                string? refuse = null;
                if (MaxConnections > 0 && _clients.Count >= MaxConnections) refuse = "servidor cheio, tente mais tarde";
                else if (MaxConnectionsPerAddress > 0 && _clients.Values.Count(c => c.Address == addr) >= MaxConnectionsPerAddress)
                    refuse = "muitas conexões deste endereço";
                if (refuse != null) { _ = Refuse(tcp, refuse); continue; }
                var client = new Client { Id = Interlocked.Increment(ref _nextSession), Tcp = tcp, Address = addr, LastSeen = DateTime.UtcNow.Ticks };
                _clients[client.Id] = client;
                _connects.Enqueue(client);
                _ = ReadLoop(client);
                _ = WriteLoop(client);
            }
        }

        private static async Task Refuse(TcpClient tcp, string reason)
        {
            try
            {
                var bytes = new UTF8Encoding(false).GetBytes("@BYE " + reason + "\n");
                await tcp.GetStream().WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
            catch { }
            try { tcp.Close(); } catch { }
        }

        private async Task ReadLoop(Client c)
        {
            int count = 0; var window = DateTime.UtcNow;
            try
            {
                var reader = new StreamReader(c.Tcp.GetStream(), Encoding.UTF8);
                string? line;
                while (!_cts.IsCancellationRequested && (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    if (line.Length > MaxLineLength) { Close(c, "mensagem grande demais"); break; }   // proteção contra abuso
                    c.LastSeen = DateTime.UtcNow.Ticks;
                    if ((DateTime.UtcNow - window).TotalSeconds >= 1) { window = DateTime.UtcNow; count = 0; }
                    if (++count > MaxMessagesPerSecond) continue; // flood: descarta
                    _inbox.Enqueue((c.Id, line));
                }
            }
            catch { /* conexão caiu */ }
            Drop(c);
        }

        /// <summary>Escreve a fila da conexão no socket, fora da thread do jogo.</summary>
        private async Task WriteLoop(Client c)
        {
            try
            {
                var w = new StreamWriter(c.Tcp.GetStream(), new UTF8Encoding(false)) { AutoFlush = false, NewLine = "\n" };
                while (!_cts.IsCancellationRequested)
                {
                    await c.Signal.WaitAsync(_cts.Token).ConfigureAwait(false);
                    bool close = false;
                    while (c.Out.TryDequeue(out var line))
                    {
                        Interlocked.Decrement(ref c.Queued);
                        if (line == CloseMarker) { close = true; break; }
                        await w.WriteLineAsync(line).ConfigureAwait(false);
                    }
                    await w.FlushAsync().ConfigureAwait(false);
                    if (close) break;
                }
            }
            catch { /* conexão caiu ou servidor parando */ }
            try { c.Tcp.Close(); } catch { }   // o ReadLoop percebe e enfileira a desconexão
        }

        private void Drop(Client c)
        {
            if (_clients.TryRemove(c.Id, out _)) _disconnects.Enqueue(c.Id);
            try { c.Tcp.Close(); } catch { }
            c.Closing = true;
            try { c.Signal.Release(); } catch { }
        }

        /// <summary>Põe uma linha na fila de envio da sessão (não bloqueia).</summary>
        private void Write(int session, string line)
        {
            if (!_clients.TryGetValue(session, out var c) || c.Closing) return;
            if (Interlocked.Increment(ref c.Queued) > MaxQueuedLines)
            {   // o jogador não está conseguindo receber: derruba só ele, sem travar os outros
                Interlocked.Decrement(ref c.Queued);
                Close(c, "conexão lenta demais");
                return;
            }
            c.Out.Enqueue(line);
        }

        /// <summary>Manda "@BYE motivo" e fecha a conexão depois que tudo o que estava na fila sair.</summary>
        private void Close(Client c, string reason)
        {
            if (c.Closing) return;
            c.Closing = true;
            c.Out.Enqueue("@BYE " + reason);
            Interlocked.Increment(ref c.Queued);
            c.Out.Enqueue(CloseMarker);
            Interlocked.Increment(ref c.Queued);
            try { c.Signal.Release(); } catch { }
        }

        /// <summary>Endereço IP de uma sessão TCP (registro, limites).</summary>
        public string AddressOf(int session) => _clients.TryGetValue(session, out var c) ? c.Address : "";

        /// <summary>Processa mensagens pendentes. Chamar uma vez por frame/tick na thread do jogo.</summary>
        public void Pump(int maxMessages = 2000)
        {
            while (_connects.TryDequeue(out var nc))
            {
                _game.Connect(nc.Id, nc.Address);
                Write(nc.Id, "@EV SYS;Conectado ao servidor Kezor.");
            }
            while (maxMessages-- > 0 && _inbox.TryDequeue(out var msg))
                Write(msg.session, _game.Handle(msg.session, msg.line));
            while (_disconnects.TryDequeue(out var id)) _game.Disconnect(id);
            DropIdle();
            Flush();
            DropKicked();
        }

        /// <summary>Expulsos, banidos, conta aberta em outro lugar: a mensagem já está na fila; depois dela a conexão fecha.</summary>
        private void DropKicked()
        {
            while (_game.TryTakeKick(out var id, out var reason))
            {
                if (_clients.TryGetValue(id, out var c)) Close(c, reason);
                else _game.Disconnect(id);
            }
        }

        /// <summary>Passa as mensagens empurradas (snapshots, eventos) de todas as sessões para as filas de envio.</summary>
        public void Flush()
        {
            foreach (var c in _clients.Values)
            {
                var lines = _game.DrainOutbox(c.Id);
                if (lines != null) foreach (var l in lines) Write(c.Id, l);
                if (!c.Out.IsEmpty) { try { c.Signal.Release(); } catch { } }
            }
        }

        /// <summary>Derruba conexões mudas (cabo puxado, jogo travado): o herói é salvo e a conta fica livre para entrar de novo.</summary>
        private void DropIdle()
        {
            if (IdleTimeoutSeconds <= 0) return;
            long limit = DateTime.UtcNow.Ticks - (long)(IdleTimeoutSeconds * TimeSpan.TicksPerSecond);
            foreach (var c in _clients.Values)
                if (!c.Closing && c.LastSeen < limit)
                {
                    Write(c.Id, "@EV SYS;Desconectado por inatividade.");
                    Close(c, "sem resposta do jogo (inatividade)");
                }
        }

        /// <summary>Avisa todos ("servidor desligando"), espera um instante as filas saírem e fecha tudo.</summary>
        public void Dispose()
        {
            foreach (var c in _clients.Values) Close(c, "servidor desligando");
            var end = DateTime.UtcNow.AddMilliseconds(800);
            while (DateTime.UtcNow < end && _clients.Values.Any(c => c.Queued > 0)) Thread.Sleep(10);
            _cts.Cancel();
            try { _listener.Stop(); } catch { }
            foreach (var c in _clients.Values) { try { c.Tcp.Close(); } catch { } }
            _clients.Clear();
        }
    }
}
