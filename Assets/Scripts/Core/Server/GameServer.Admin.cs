using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using static Kezor.Core.Server.Protocol;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Moderação: expulsar, banir (por dias ou para sempre), silenciar o chat, ver onde alguém está e ir até ele.
    /// Usado pelos comandos GM dentro do jogo (contas da lista de GMs) e pelo console do servidor dedicado.
    /// Tudo fica no registro do servidor (LogLine), junto com trocas, compras em lojas e itens raros pegos do chão.
    /// </summary>
    public sealed partial class GameServer
    {
        private readonly Queue<int> _kicks = new Queue<int>();

        /// <summary>Conexões que o servidor quer derrubar (o host TCP fecha depois de mandar a última mensagem).</summary>
        public bool TryTakeKick(out int sessionId)
        {
            if (_kicks.Count > 0) { sessionId = _kicks.Dequeue(); return true; }
            sessionId = 0; return false;
        }

        /// <summary>Herói online com este nome (console, testes).</summary>
        public Character? OnlineCharacter(string name) => CharByName(name);

        /// <summary>Sessão de quem está conectado com este herói ou esta conta.</summary>
        private Session? FindSession(string who) => _sessions.Values.FirstOrDefault(o =>
            (o.Character != null && string.Equals(o.Character.Name, who, StringComparison.OrdinalIgnoreCase)) ||
            (o.Account != null && string.Equals(o.Account.Name, who, StringComparison.OrdinalIgnoreCase)));

        /// <summary>A conta pelo nome da conta ou do herói (online ou não). Se estiver online, devolve o mesmo objeto da sessão.</summary>
        private Account? FindAccount(string who, out Session? online)
        {
            online = FindSession(who);
            if (online?.Account != null) return online.Account;
            var a = _accounts.Load(who);
            if (a != null) return a;
            var c = _repo.Load(who);
            return c != null && c.AccountName != "" ? _accounts.Load(c.AccountName) : null;
        }

        private static string Until(DateTime? t) => t == null || t.Value.Year >= Account.PermanentBan.Year ? "para sempre"
            : "até " + t.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        private void Kick(Session s, string message)
        {
            Sys(s, message);
            if (!_kicks.Contains(s.Id)) _kicks.Enqueue(s.Id);
        }

        /// <summary>Expulsa quem está online (herói ou conta). O herói é salvo normalmente ao cair.</summary>
        public string AdminKick(string who, string reason, string by)
        {
            var s = FindSession(who);
            if (s == null) return $"ERR {Clean(who)} não está conectado";
            var name = s.Character?.Name ?? s.Account?.Name ?? who;
            Kick(s, "Você foi desconectado por um GM" + (reason.Length > 0 ? ": " + Clean(reason) : "."));
            LogLine($"GM {by}: expulsou {name}" + (reason.Length > 0 ? $" ({reason})" : ""));
            return $"OK {name} expulso";
        }

        /// <summary>Bane a conta por <paramref name="days"/> dias (0 ou menos = para sempre) e derruba se estiver online.</summary>
        public string AdminBan(string who, int days, string reason, string by)
        {
            var a = FindAccount(who, out var online);
            if (a == null) return $"ERR conta ou herói {Clean(who)} não encontrado";
            if (_opt.GmAccounts.Contains(a.Name) && by != "console") return "ERR não dá para banir outro GM pelo jogo (use o console do servidor)";
            a.BannedUntil = days <= 0 ? Account.PermanentBan : DateTime.UtcNow.AddDays(days);
            a.BanReason = Clean(reason);
            _accounts.Save(a);
            if (online != null) Kick(online, $"Sua conta foi banida {Until(a.BannedUntil)}" + (reason.Length > 0 ? ": " + Clean(reason) : "."));
            LogLine($"GM {by}: baniu a conta {a.Name} {Until(a.BannedUntil)}" + (reason.Length > 0 ? $" ({reason})" : ""));
            return $"OK conta {a.Name} banida {Until(a.BannedUntil)}";
        }

        public string AdminUnban(string who, string by)
        {
            var a = FindAccount(who, out _);
            if (a == null) return $"ERR conta ou herói {Clean(who)} não encontrado";
            a.BannedUntil = null; a.BanReason = "";
            _accounts.Save(a);
            LogLine($"GM {by}: tirou o banimento da conta {a.Name}");
            return $"OK conta {a.Name} liberada";
        }

        /// <summary>Silencia o chat (dizer, sussurro, grupo, guilda) por alguns minutos (0 ou menos = 1 dia).</summary>
        public string AdminMute(string who, int minutes, string by)
        {
            var a = FindAccount(who, out var online);
            if (a == null) return $"ERR conta ou herói {Clean(who)} não encontrado";
            if (minutes <= 0) minutes = 24 * 60;
            a.MutedUntil = DateTime.UtcNow.AddMinutes(minutes);
            _accounts.Save(a);
            if (online != null) Sys(online, $"Seu chat foi bloqueado por um GM por {minutes} minuto(s).");
            LogLine($"GM {by}: silenciou {a.Name} por {minutes} min");
            return $"OK {a.Name} silenciado por {minutes} min";
        }

        public string AdminUnmute(string who, string by)
        {
            var a = FindAccount(who, out var online);
            if (a == null) return $"ERR conta ou herói {Clean(who)} não encontrado";
            a.MutedUntil = null;
            _accounts.Save(a);
            if (online != null) Sys(online, "Seu chat foi liberado.");
            LogLine($"GM {by}: liberou o chat de {a.Name}");
            return $"OK chat de {a.Name} liberado";
        }

        /// <summary>Ficha rápida: conta, herói, nível, mapa, posição, ouro, banimento e silêncio.</summary>
        public string AdminInfo(string who)
        {
            var a = FindAccount(who, out var online);
            if (a == null) return $"ERR conta ou herói {Clean(who)} não encontrado";
            var now = DateTime.UtcNow;
            var c = online?.Character;
            var where = c != null ? $"{c.Name} nv{c.Level} {MapOf(c.MapId).Name} ({c.X:0},{c.Z:0}) ouro {c.Gold:N0}" : "fora do jogo";
            return $"OK conta {a.Name} · heróis {string.Join(", ", a.Characters)} · {where}" +
                   (a.IsBanned(now) ? $" · BANIDA {Until(a.BannedUntil)} ({a.BanReason})" : "") +
                   (a.IsMuted(now) ? $" · silenciada {Until(a.MutedUntil)}" : "");
        }

        /// <summary>Chat bloqueado para esta sessão? Devolve a mensagem de erro ou null.</summary>
        private string? MutedError(Session s)
        {
            var a = s.Account;
            if (a == null || !a.IsMuted(DateTime.UtcNow)) return null;
            return $"ERR seu chat está bloqueado {Until(a.MutedUntil)}";
        }

        /// <summary>GM KICK|BAN|UNBAN|MUTE|UNMUTE|INFO|GOTO|BRING|AVISO: moderação pelo jogo.</summary>
        private string? GmModeration(Session s, Character c, string[] p)
        {
            string by = s.Account?.Name ?? c.Name;
            string Rest(int from) => p.Length > from ? string.Join(" ", p.Skip(from)) : "";
            switch (p[1].ToUpperInvariant())
            {
                case "KICK": return p.Length < 3 ? "ERR use GM KICK <nome> [motivo]" : AdminKick(p[2], Rest(3), by);
                case "BAN":
                {
                    if (p.Length < 4) return "ERR use GM BAN <conta ou herói> <dias|perm> [motivo]";
                    int days = p[3].Equals("perm", StringComparison.OrdinalIgnoreCase) ? 0 : Int(p, 3);
                    if (days < 0) return "ERR dias inválidos";
                    return AdminBan(p[2], days, Rest(4), by);
                }
                case "UNBAN": return p.Length < 3 ? "ERR use GM UNBAN <conta>" : AdminUnban(p[2], by);
                case "MUTE": return p.Length < 3 ? "ERR use GM MUTE <nome> [minutos]" : AdminMute(p[2], p.Length > 3 ? Int(p, 3) : 30, by);
                case "UNMUTE": return p.Length < 3 ? "ERR use GM UNMUTE <nome>" : AdminUnmute(p[2], by);
                case "INFO": return p.Length < 3 ? "ERR use GM INFO <nome>" : AdminInfo(p[2]);
                case "AVISO": case "ANNOUNCE":
                {
                    var r = Announce(Rest(2));
                    LogLine($"GM {by}: aviso");
                    return r.StartsWith("aviso enviado") ? "OK " + r : "ERR " + r;
                }
                case "GOTO": case "BRING":
                {
                    if (p.Length < 3) return $"ERR use GM {p[1].ToUpperInvariant()} <nome>";
                    var other = CharByName(p[2]);
                    if (other == null) return $"ERR {Clean(p[2])} não está online";
                    if (other == c) return "ERR é você mesmo";
                    bool go = p[1].Equals("GOTO", StringComparison.OrdinalIgnoreCase);
                    var (mover, dest) = go ? (c, other) : (other, c);
                    var ms = SessionOf(mover.Id);
                    if (ms == null) return "ERR sessão não encontrada";
                    ms.ClearIntent();
                    float x = MapOf(dest.MapId).ClampCoord(dest.X + 1.5f), z = dest.Z;
                    if (MapOf(dest.MapId).IsBlocked(x, z)) { x = dest.X; z = dest.Z; }
                    if (mover.MapId != dest.MapId) Transfer(ms, mover, MapOf(dest.MapId), x, z);
                    else { mover.X = x; mover.Z = z; }
                    LogLine($"GM {by}: {(go ? "foi até" : "trouxe")} {other.Name}");
                    return $"OK {(go ? "você foi até" : "trouxe")} {other.Name}";
                }
                default: return null;
            }
        }

        // ------------------------------------------------------------ registros (economia e GM)

        /// <summary>Itens que merecem registro quando trocam de mão ou saem do chão: runas, joias, épicos, asas, nível 3+ e itens +7.</summary>
        private static bool Notable(Item it) => it.Def.Kind == ItemKind.Rune || it.Def.IsJewel || it.Def.Epic || it.Def.IsWings || it.Def.Tier >= 3 || it.Level >= 7;

        private static string Describe(IEnumerable<Item> items, long gold)
        {
            var list = items.Select(i => i.Quantity > 1 ? $"{i.Quantity}x {i.DisplayName}" : i.DisplayName).ToList();
            if (gold > 0) list.Add($"{gold:N0} de ouro");
            return list.Count == 0 ? "nada" : string.Join(", ", list);
        }
    }
}
