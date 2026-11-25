using System;
using System.Collections.Generic;
using System.Linq;
using Kezor.Core.Characters;
using static Kezor.Core.Server.Protocol;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Guerra de guildas: o líder declara (GUILD WAR alvo), o líder do outro lado aceita (GUILD WARACCEPT). Durante a guerra,
    /// cada abate entre membros das duas guildas fora da vila vale 1 ponto (e não dá infâmia). No fim do tempo o servidor
    /// anuncia o vencedor e grava vitória/derrota/empate nas guildas (que pesa no ranking de guildas).
    /// </summary>
    public sealed partial class GameServer
    {
        public const float WarInviteSeconds = 120f;

        public sealed class GuildWar
        {
            public string A = "", B = "";
            public int ScoreA, ScoreB;
            public double EndsAt;
            public bool Involves(string g) => string.Equals(A, g, StringComparison.OrdinalIgnoreCase) || string.Equals(B, g, StringComparison.OrdinalIgnoreCase);
            public string Enemy(string g) => string.Equals(A, g, StringComparison.OrdinalIgnoreCase) ? B : A;
        }

        private readonly List<GuildWar> _wars = new List<GuildWar>();
        private readonly Dictionary<string, (string from, double expires)> _warInvites = new Dictionary<string, (string, double)>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<GuildWar> Wars => _wars;
        public float WarDuration = 600f;

        public GuildWar? WarOf(string guild) => _wars.FirstOrDefault(w => w.Involves(guild));

        /// <summary>True se os dois personagens são de guildas em guerra entre si.</summary>
        private GuildWar? WarBetween(Character a, Character b)
        {
            var ga = GuildOf(a.Name); var gb = GuildOf(b.Name);
            if (ga == null || gb == null || ga == gb) return null;
            var w = WarOf(ga.Name);
            return w != null && w.Involves(gb.Name) ? w : null;
        }

        private string WarCmd(Character c, Guild? g, string sub, string[] p)
        {
            if (g == null) return "ERR você não está numa guilda";
            switch (sub)
            {
                case "WAR":
                {
                    if (g.RankOf(c.Name) != GuildRank.Leader) return "ERR só o líder declara guerra";
                    if (p.Length < 3 || !_guilds.TryGetValue(p[2], out var target)) return "ERR guilda não encontrada";
                    if (target == g) return "ERR escolha outra guilda";
                    if (WarOf(g.Name) != null || WarOf(target.Name) != null) return "ERR uma das guildas já está em guerra";
                    _warInvites[target.Name] = (g.Name, _time + WarInviteSeconds);
                    GuildSys(target, $"A guilda {g.Name} declarou guerra! O líder aceita com GUILD WARACCEPT (2 min).");
                    foreach (var n in target.Members.Keys) { var mc = CharByName(n); if (mc != null) Push(mc, $"@EV WARINVITE;{g.Name}"); }
                    GuildSys(g, $"Declaração de guerra enviada para {target.Name}.");
                    return $"OK guerra declarada a {target.Name}; esperando aceitar";
                }
                case "WARACCEPT":
                {
                    if (g.RankOf(c.Name) != GuildRank.Leader) return "ERR só o líder aceita a guerra";
                    if (!_warInvites.TryGetValue(g.Name, out var inv) || inv.expires < _time || !_guilds.TryGetValue(inv.from, out var attacker))
                        return "ERR nenhuma declaração de guerra válida";
                    _warInvites.Remove(g.Name);
                    if (WarOf(g.Name) != null || WarOf(attacker.Name) != null) return "ERR uma das guildas já está em guerra";
                    var w = new GuildWar { A = attacker.Name, B = g.Name, EndsAt = _time + WarDuration };
                    _wars.Add(w);
                    SysAll($"Guerra de guildas: {w.A} x {w.B} por {(WarDuration >= 60 ? $"{WarDuration / 60:0} minutos" : $"{WarDuration:0} segundos")}! Abates fora da vila valem pontos.");
                    SendWar(w);
                    return "OK guerra iniciada";
                }
                case "WARDECLINE":
                    if (!_warInvites.Remove(g.Name)) return "ERR nenhuma declaração";
                    return "OK guerra recusada";
                case "WARINFO":
                {
                    var w = WarOf(g.Name);
                    if (w == null) return $"OK sem guerra — histórico: {g.Wins} vitórias, {g.Losses} derrotas, {g.Draws} empates";
                    SendWar(w);
                    return $"OK {w.A} {w.ScoreA} x {w.ScoreB} {w.B} — faltam {Math.Max(0, w.EndsAt - _time):0} s";
                }
                default: return "ERR subcomando desconhecido";
            }
        }

        /// <summary>Abate entre guildas em guerra: ponto para a guilda de quem matou. Retorna true se contou (sem infâmia).</summary>
        private bool CountWarKill(Character killer, Character victim)
        {
            var w = WarBetween(killer, victim);
            if (w == null) return false;
            var kg = GuildOf(killer.Name)!;
            if (string.Equals(kg.Name, w.A, StringComparison.OrdinalIgnoreCase)) w.ScoreA++; else w.ScoreB++;
            SendWar(w);
            foreach (var g in new[] { w.A, w.B })
                if (_guilds.TryGetValue(g, out var gg)) GuildSys(gg, $"{killer.Name} abateu {victim.Name}! Placar: {w.A} {w.ScoreA} x {w.ScoreB} {w.B}");
            return true;
        }

        private void TickWars()
        {
            foreach (var w in _wars.ToList())
            {
                if (_time < w.EndsAt && _guilds.ContainsKey(w.A) && _guilds.ContainsKey(w.B)) continue;
                _wars.Remove(w);
                _guilds.TryGetValue(w.A, out var a); _guilds.TryGetValue(w.B, out var b);
                string result;
                if (a == null || b == null) result = $"A guerra {w.A} x {w.B} terminou sem vencedor (guilda dissolvida).";
                else if (w.ScoreA == w.ScoreB) { a.Draws++; b.Draws++; result = $"Guerra {w.A} x {w.B} terminou empatada em {w.ScoreA} x {w.ScoreB}."; }
                else
                {
                    var (win, lose) = w.ScoreA > w.ScoreB ? (a, b) : (b, a);
                    win.Wins++; lose.Losses++;
                    result = $"A guilda {win.Name} venceu a guerra contra {lose.Name} ({Math.Max(w.ScoreA, w.ScoreB)} x {Math.Min(w.ScoreA, w.ScoreB)})!";
                }
                if (a != null) SaveGuild(a);
                if (b != null) SaveGuild(b);
                SysAll(result);
                foreach (var g in new[] { a, b })
                    if (g != null) foreach (var n in g.Members.Keys) { var mc = CharByName(n); if (mc != null) Push(mc, "@EV WAREND"); }
            }
            foreach (var k in _warInvites.Where(kv => kv.Value.expires < _time).Select(kv => kv.Key).ToList()) _warInvites.Remove(k);
        }

        /// <summary>@EV WAR;A;B;pontosA;pontosB;segundosRestantes para os membros das duas guildas.</summary>
        private void SendWar(GuildWar w)
        {
            var msg = $"@EV WAR;{w.A};{w.B};{w.ScoreA};{w.ScoreB};{Math.Max(0, w.EndsAt - _time):0}";
            foreach (var gname in new[] { w.A, w.B })
                if (_guilds.TryGetValue(gname, out var g))
                    foreach (var n in g.Members.Keys) { var mc = CharByName(n); if (mc != null) Push(mc, msg); }
        }
    }
}
