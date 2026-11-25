using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Kezor.Core.Server;

namespace Kezor.Core.Net
{
    /// <summary>Host = servidor neste PC (o jogo abre se ainda não houver um rodando); Direct = digitar IP e porta de um
    /// amigo; Remote = servidor do servers.json.</summary>
    public enum ServerKind { Host, Direct, Remote }

    public sealed class ChannelEntry
    {
        public string Name = "";
        public int Port;
    }

    /// <summary>Servidor da lista de login: nome, endereço, região e canais (cada canal é uma porta).</summary>
    public sealed class ServerEntry
    {
        public string Id = "", Name = "", Host = "", Region = "";
        public ServerKind Kind = ServerKind.Remote;
        public readonly List<ChannelEntry> Channels = new List<ChannelEntry>();
    }

    /// <summary>
    /// Lista de servidores do login (servers.json). Formato:
    /// { "servers": [ { "id": "brasalume", "name": "Brasalume", "host": "jogo.exemplo.com", "region": "BR",
    ///                  "channels": [ { "name": "Canal 1", "port": 55901 }, { "name": "Canal 2", "port": 55902 } ] } ] }
    /// Sem "channels", usa "port" (ou a porta padrão) como canal único. Entradas sem nome/endereço ou com porta inválida
    /// são ignoradas. A entrada "Servidor deste PC" entra sempre no topo. Não existe modo solo: todo jogador entra num servidor
    /// com conta e senha, e todos os jogadores do canal dividem o mesmo mundo.
    /// </summary>
    public static class ServerList
    {
        public static List<ServerEntry> Parse(string json, out string error)
        {
            error = "";
            var list = new List<ServerEntry>();
            object? root;
            try { root = MiniJson.Read(json); }
            catch (Exception e) { error = "servers.json inválido: " + e.Message; return list; }
            if (!(root is IDictionary<string, object?> o)) { error = "servers.json: esperado um objeto"; return list; }
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in o.A("servers").OfType<IDictionary<string, object?>>())
            {
                var s = new ServerEntry { Name = e.S("name").Trim(), Host = e.S("host").Trim(), Region = e.S("region").Trim(), Kind = ServerKind.Remote };
                if (s.Name.Length == 0 || s.Host.Length == 0 || s.Host.Any(char.IsWhiteSpace)) continue;
                s.Id = e.S("id").Trim();
                if (s.Id.Length == 0) s.Id = s.Name.ToLowerInvariant().Replace(' ', '-');
                if (!used.Add(s.Id)) continue;
                foreach (var c in e.A("channels").OfType<IDictionary<string, object?>>())
                {
                    int port = (int)c.L("port");
                    if (port < 1 || port > 65535) continue;
                    var name = c.S("name").Trim();
                    s.Channels.Add(new ChannelEntry { Name = name.Length > 0 ? name : $"Canal {s.Channels.Count + 1}", Port = port });
                }
                if (s.Channels.Count == 0)
                {
                    int port = e.ContainsKey("port") ? (int)e.L("port") : Protocol.DefaultPort;
                    if (port < 1 || port > 65535) continue;
                    s.Channels.Add(new ChannelEntry { Name = "Canal 1", Port = port });
                }
                list.Add(s);
            }
            return list;
        }

        public const string HostId = "este-pc", DirectId = "por-ip";

        /// <summary>"Servidor deste PC" (127.0.0.1 na porta dada) e "Entrar por IP" no topo, depois os servidores do arquivo.</summary>
        public static List<ServerEntry> WithBuiltins(IEnumerable<ServerEntry> remote, int hostPort = Protocol.DefaultPort)
        {
            var all = new List<ServerEntry>
            {
                new ServerEntry { Id = HostId, Name = "Servidor deste PC", Host = "127.0.0.1", Region = "você e sua rede", Kind = ServerKind.Host, Channels = { new ChannelEntry { Name = "Canal 1", Port = hostPort } } },
                new ServerEntry { Id = DirectId, Name = "Entrar por IP", Host = "", Region = "servidor de um amigo", Kind = ServerKind.Direct, Channels = { new ChannelEntry { Name = "Canal 1", Port = Protocol.DefaultPort } } },
            };
            all.AddRange(remote);
            return all;
        }

        /// <summary>Lê o arquivo (vazio se não existir).</summary>
        public static List<ServerEntry> Load(string path, out string error)
        {
            error = "";
            if (!File.Exists(path)) return new List<ServerEntry>();
            return Parse(File.ReadAllText(path), out error);
        }
    }

    /// <summary>Resposta do STATUS: nome, canal, população e latência medida pelo cliente.</summary>
    public sealed class ServerStatus
    {
        public bool Online;
        public string Name = "", Version = "", Error = "";
        public int Channel, Players, MaxPlayers;
        public int LatencyMs = -1;

        /// <summary>"OK status nome;canal;online;máximo;versão" → campos.</summary>
        public static ServerStatus? ParseReply(string reply)
        {
            const string head = "OK status ";
            if (!reply.StartsWith(head)) return null;
            var f = reply.Substring(head.Length).Split(Protocol.Sep);
            if (f.Length < 4) return null;
            if (!int.TryParse(f[1], out var ch) || !int.TryParse(f[2], out var on) || !int.TryParse(f[3], out var max)) return null;
            return new ServerStatus { Online = true, Name = f[0], Channel = ch, Players = on, MaxPlayers = max, Version = f.Length > 4 ? f[4] : "" };
        }

        /// <summary>Lotação para a interface: livre, cheio ou lotado.</summary>
        public string Load => !Online ? "offline" : MaxPlayers <= 0 ? "livre" : Players * 10 >= MaxPlayers * 9 ? "lotado" : Players * 2 >= MaxPlayers ? "cheio" : "livre";
    }

    /// <summary>
    /// Ping de status por TCP: conecta, manda STATUS, espera a resposta e mede o tempo. Bloqueia (rode numa tarefa);
    /// nunca lança: em falha devolve Online = false com o motivo.
    /// </summary>
    public static class ServerStatusProbe
    {
        public static ServerStatus Probe(string host, int port, int timeoutMs = 1500)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var tcp = new TcpClient { NoDelay = true };
                var connect = tcp.ConnectAsync(host, port);
                if (!connect.Wait(timeoutMs) || !tcp.Connected) return new ServerStatus { Error = "sem resposta" };
                var stream = tcp.GetStream();
                stream.ReadTimeout = Math.Max(100, timeoutMs - (int)sw.ElapsedMilliseconds);
                var w = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                var r = new StreamReader(stream, Encoding.UTF8);
                var sent = Stopwatch.StartNew();
                w.WriteLine("STATUS");
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    var line = r.ReadLine();
                    if (line == null) break;
                    if (line.StartsWith("@")) continue;                     // mensagens empurradas antes da resposta
                    var st = ServerStatus.ParseReply(line);
                    if (st == null) return new ServerStatus { Error = "resposta inesperada" };
                    st.LatencyMs = (int)sent.ElapsedMilliseconds;
                    return st;
                }
                return new ServerStatus { Error = "sem resposta" };
            }
            catch (Exception e) { return new ServerStatus { Error = e is SocketException ? "offline" : e.Message }; }
        }
    }
}
