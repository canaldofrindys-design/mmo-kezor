using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kezor.Core.Characters;
using static Kezor.Core.Server.Protocol;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Guildas: criar com o Hadrik na vila (nível 50, 500 mil de ouro), convidar/aceitar, sair, expulsar, promover/rebaixar,
    /// dissolver, chat de guilda (GSAY). O nome da guilda vai no snapshot para aparecer acima do herói. Salvas no repositório.
    /// </summary>
    public sealed partial class GameServer
    {
        private readonly Dictionary<string, Guild> _guilds = new Dictionary<string, Guild>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _guildInvite = new Dictionary<int, string>();   // sessão convidada -> guilda
        private IGuildRepository _guildRepo = new InMemoryGuildRepository();
        public IReadOnlyDictionary<string, Guild> Guilds => _guilds;

        private void InitGuilds(IGuildRepository? repo)
        {
            _guildRepo = repo ?? new InMemoryGuildRepository();
            foreach (var g in _guildRepo.LoadAllGuilds()) if (g.Members.Count > 0) _guilds[g.Name] = g;
        }

        public Guild? GuildOf(string charName) => _guilds.Values.FirstOrDefault(g => g.Members.ContainsKey(charName));

        private void SaveGuild(Guild g) { try { _guildRepo.SaveGuild(g); } catch (Exception e) { Console.Error.WriteLine($"[Kezor] falha ao salvar guilda {g.Name}: {e.Message}"); } }

        private string NearGuildMaster(Character c)
        {
            var map = MapOf(c.MapId);
            if (!map.InSafeZone(c.X, c.Z)) return "ERR só é possível na vila";
            foreach (var n in map.Npcs) if (n.Role == "guild" && MathK.Dist(c.X, c.Z, n.X, n.Z) <= 6.5f) return "";
            return "ERR fale com o Hadrik, o Arauto das Guildas";
        }

        private string GuildCmd(Session s, Character c, string[] p)
        {
            if (p.Length < 2) return "ERR use GUILD CREATE <nome>|INVITE <nome>|ACCEPT|DECLINE|LEAVE|KICK <nome>|PROMOTE <nome>|DEMOTE <nome>|DISBAND|INFO";
            var g = GuildOf(c.Name);
            switch (p[1].ToUpperInvariant())
            {
                case "CREATE":
                {
                    if (g != null) return "ERR você já está numa guilda";
                    var near = NearGuildMaster(c); if (near != "") return near;
                    if (p.Length < 3 || !Guild.ValidName(p[2])) return "ERR o nome da guilda deve ter 3-14 letras/números";
                    if (_guilds.ContainsKey(p[2])) return "ERR já existe uma guilda com esse nome";
                    if (c.Level < Guild.CreateMinLevel) return $"ERR requer nível {Guild.CreateMinLevel}";
                    if (c.Gold < Guild.CreateCost) return $"ERR custa {Guild.CreateCost:N0} de ouro";
                    c.Gold -= Guild.CreateCost; c.Inventory.Touch();
                    var ng = new Guild { Name = p[2] };
                    ng.Members[c.Name] = GuildRank.Leader;
                    _guilds[ng.Name] = ng;
                    SaveGuild(ng);
                    SendGuild(ng);
                    SysAll($"A guilda {ng.Name} foi fundada por {c.Name}!");
                    return $"OK guilda {ng.Name} criada";
                }
                case "INVITE":
                {
                    if (g == null) return "ERR você não está numa guilda";
                    if (g.RankOf(c.Name) < GuildRank.Officer) return "ERR só líder e oficiais convidam";
                    if (g.Members.Count >= Guild.MaxMembers) return $"ERR a guilda está cheia ({Guild.MaxMembers})";
                    var t = p.Length > 2 ? CharByName(p[2]) : null;
                    if (t == null) return "ERR jogador não encontrado";
                    if (GuildOf(t.Name) != null) return "ERR o jogador já está numa guilda";
                    var ts = SessionOf(t.Id)!;
                    _guildInvite[ts.Id] = g.Name;
                    Push(ts, $"@EV GINVITE;{g.Name};{c.Name}");
                    return $"OK convite enviado para {t.Name}";
                }
                case "ACCEPT":
                {
                    if (g != null) return "ERR você já está numa guilda";
                    if (!_guildInvite.TryGetValue(s.Id, out var gname) || !_guilds.TryGetValue(gname, out var ig)) return "ERR nenhum convite";
                    _guildInvite.Remove(s.Id);
                    if (ig.Members.Count >= Guild.MaxMembers) return "ERR a guilda está cheia";
                    ig.Members[c.Name] = GuildRank.Member;
                    SaveGuild(ig);
                    GuildSys(ig, $"{c.Name} entrou na guilda.");
                    SendGuild(ig);
                    return $"OK você entrou na guilda {ig.Name}";
                }
                case "DECLINE": _guildInvite.Remove(s.Id); return "OK";
                case "LEAVE":
                {
                    if (g == null) return "ERR você não está numa guilda";
                    bool leader = g.RankOf(c.Name) == GuildRank.Leader;
                    g.Members.Remove(c.Name);
                    if (g.Members.Count == 0) { Disband(g); return "OK você saiu e a guilda foi dissolvida"; }
                    if (leader)
                    {   // passa a liderança para o oficial mais antigo na lista (ou para qualquer membro)
                        var heir = g.Members.FirstOrDefault(kv => kv.Value == GuildRank.Officer).Key ?? g.Members.Keys.First();
                        g.Members[heir] = GuildRank.Leader;
                        GuildSys(g, $"{heir} agora lidera a guilda.");
                    }
                    SaveGuild(g);
                    GuildSys(g, $"{c.Name} saiu da guilda.");
                    SendGuild(g);
                    Push(s, "@GUILD NONE");
                    return "OK você saiu da guilda";
                }
                case "KICK":
                {
                    if (g == null) return "ERR você não está numa guilda";
                    var name = p.Length > 2 ? g.Members.Keys.FirstOrDefault(k => string.Equals(k, p[2], StringComparison.OrdinalIgnoreCase)) : null;
                    if (name == null) return "ERR não é membro";
                    var mine = g.RankOf(c.Name); var theirs = g.RankOf(name);
                    if (name == c.Name) return "ERR use LEAVE para sair";
                    if (mine < GuildRank.Officer || theirs >= mine) return "ERR você não pode expulsar este membro";
                    g.Members.Remove(name);
                    SaveGuild(g);
                    GuildSys(g, $"{name} foi expulso da guilda.");
                    SendGuild(g);
                    var kc = CharByName(name);
                    if (kc != null) { var ks = SessionOf(kc.Id)!; Push(ks, "@GUILD NONE"); Sys(ks, $"Você foi expulso da guilda {g.Name}."); }
                    return "OK";
                }
                case "PROMOTE":
                case "DEMOTE":
                {
                    if (g == null) return "ERR você não está numa guilda";
                    if (g.RankOf(c.Name) != GuildRank.Leader) return "ERR só o líder muda cargos";
                    var name = p.Length > 2 ? g.Members.Keys.FirstOrDefault(k => string.Equals(k, p[2], StringComparison.OrdinalIgnoreCase)) : null;
                    if (name == null || name == c.Name) return "ERR membro inválido";
                    g.Members[name] = p[1].ToUpperInvariant() == "PROMOTE" ? GuildRank.Officer : GuildRank.Member;
                    SaveGuild(g);
                    GuildSys(g, $"{name} agora é {Guild.RankName(g.Members[name])}.");
                    SendGuild(g);
                    return "OK";
                }
                case "DISBAND":
                {
                    if (g == null) return "ERR você não está numa guilda";
                    if (g.RankOf(c.Name) != GuildRank.Leader) return "ERR só o líder dissolve a guilda";
                    GuildSys(g, $"A guilda {g.Name} foi dissolvida.");
                    Disband(g);
                    return "OK guilda dissolvida";
                }
                case "WAR":
                case "WARACCEPT":
                case "WARDECLINE":
                case "WARINFO":
                    return WarCmd(c, g, p[1].ToUpperInvariant(), p);
                case "INFO":
                    if (g == null) { Push(s, "@GUILD NONE"); return "OK sem guilda"; }
                    SendGuild(g);
                    return $"OK {g.Name}: {g.Members.Count} membros, líder {g.Leader}";
                default: return "ERR subcomando desconhecido";
            }
        }

        private void Disband(Guild g)
        {
            var members = g.Members.Keys.ToList();
            g.Members.Clear();
            _guilds.Remove(g.Name);
            try { _guildRepo.DeleteGuild(g.Name); } catch { }
            foreach (var n in members) { var mc = CharByName(n); if (mc != null) Push(mc, "@GUILD NONE"); }
        }

        private string GuildSay(Character c, string text)
        {
            var g = GuildOf(c.Name);
            if (g == null) return "ERR você não está numa guilda";
            text = Clean(text);
            if (text.Length == 0) return "ERR mensagem vazia";
            if (text.Length > 120) text = text.Substring(0, 120);
            foreach (var n in g.Members.Keys) { var mc = CharByName(n); if (mc != null) Push(mc, $"@EV CHAT;guild;{c.Name};{text}"); }
            return "OK";
        }

        private void GuildSys(Guild g, string text)
        {
            foreach (var n in g.Members.Keys) { var mc = CharByName(n); if (mc != null) Push(mc, "@EV CHAT;guild;Guilda;" + Clean(text)); }
        }

        /// <summary>@GUILD nome|meuCargo|membro;cargo;online|... para os membros online (cada um com o seu cargo).</summary>
        private void SendGuild(Guild g)
        {
            foreach (var n in g.Members.Keys)
            {
                var mc = CharByName(n);
                if (mc == null) continue;
                var sb = new StringBuilder("@GUILD ").Append(g.Name).Append(ListSep).Append((int)g.RankOf(n));
                foreach (var kv in g.Members.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key))
                    sb.Append(ListSep).Append(kv.Key).Append(Sep).Append((int)kv.Value).Append(Sep).Append(CharByName(kv.Key) != null ? 1 : 0);
                Push(mc, sb.ToString());
            }
        }
    }
}
