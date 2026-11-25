using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using static Kezor.Core.Server.Protocol;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Troca segura entre dois jogadores próximos: cada lado oferece itens da mochila e ouro, trava (passo 1) e confirma
    /// (passo 2). Qualquer mudança destrava os dois lados. Na confirmação dupla o servidor revalida tudo (itens nos mesmos
    /// espaços, ouro, espaço na mochila de quem recebe) e troca de uma vez; se algo falhar, nada muda.
    /// </summary>
    public sealed partial class GameServer
    {
        public const float TradeRange = 4f, TradeBreakRange = 6f;

        public sealed class TradeSide
        {
            public int SessionId;
            public readonly List<(int slot, Item item)> Items = new List<(int, Item)>();
            /// <summary>Como cada item estava quando foi oferecido (quantidade, nível...). Se mudar, a troca destrava.</summary>
            public readonly List<string> Seen = new List<string>();
            public long Gold;
            public bool Locked, Confirmed;
        }

        public sealed class Trade
        {
            public readonly TradeSide[] Sides = new TradeSide[2];
            public int Version;
            public TradeSide Of(int sessionId) => Sides[0].SessionId == sessionId ? Sides[0] : Sides[1];
            public TradeSide Other(int sessionId) => Sides[0].SessionId == sessionId ? Sides[1] : Sides[0];
            public void Unlock() { foreach (var s in Sides) { s.Locked = false; s.Confirmed = false; } Version++; }
        }

        private readonly Dictionary<int, Trade> _tradeOf = new Dictionary<int, Trade>();   // sessionId -> troca
        private readonly Dictionary<int, int> _tradeInvite = new Dictionary<int, int>();   // convidado -> quem convidou
        public Trade? TradeOfSession(int sessionId) => _tradeOf.TryGetValue(sessionId, out var t) ? t : null;

        private string TradeCmd(Session s, Character c, string[] p)
        {
            if (p.Length < 2) return "ERR use TRADE <nome>|ACCEPT|DECLINE|ADD <slot>|REMOVE <slot>|GOLD <n>|LOCK|CONFIRM|CANCEL";
            string sub = p[1].ToUpperInvariant();
            switch (sub)
            {
                case "ACCEPT": return TradeAccept(s, c);
                case "DECLINE": _tradeInvite.Remove(s.Id); return "OK convite recusado";
                case "CANCEL":
                    if (!_tradeOf.ContainsKey(s.Id)) return "ERR nenhuma troca aberta";
                    EndTrade(_tradeOf[s.Id], $"{c.Name} cancelou a troca.");
                    return "OK troca cancelada";
                case "ADD": return TradeAdd(s, c, p);
                case "REMOVE": return TradeRemove(s, c, p);
                case "GOLD": return TradeGold(s, c, p);
                case "LOCK": return TradeLock(s);
                case "CONFIRM": return TradeConfirm(s);
                default: return TradeRequest(s, c, p[1]);
            }
        }

        private string TradeRequest(Session s, Character c, string name)
        {
            if (_tradeOf.ContainsKey(s.Id)) return "ERR você já está numa troca";
            var other = CharByName(name);
            if (other == null || other == c) return "ERR jogador não encontrado";
            var os = SessionOf(other.Id)!;
            if (_tradeOf.ContainsKey(os.Id)) return "ERR o jogador já está numa troca";
            if (other.MapId != c.MapId || MathK.Dist(c.X, c.Z, other.X, other.Z) > TradeRange) return "ERR chegue mais perto para trocar";
            if (c.IsDead || other.IsDead) return "ERR ninguém caído pode trocar";
            _tradeInvite[os.Id] = s.Id;
            Push(os, $"@EV TRADEREQ;{c.Name}");
            return $"OK pedido de troca enviado para {other.Name}";
        }

        private string TradeAccept(Session s, Character c)
        {
            if (!_tradeInvite.TryGetValue(s.Id, out var fromId)) return "ERR nenhum pedido de troca";
            _tradeInvite.Remove(s.Id);
            if (!_sessions.TryGetValue(fromId, out var fs) || fs.Character == null) return "ERR o jogador saiu";
            var a = fs.Character;
            if (_tradeOf.ContainsKey(s.Id) || _tradeOf.ContainsKey(fs.Id)) return "ERR alguém já está numa troca";
            if (a.MapId != c.MapId || MathK.Dist(c.X, c.Z, a.X, a.Z) > TradeRange) return "ERR chegue mais perto para trocar";
            var t = new Trade();
            t.Sides[0] = new TradeSide { SessionId = fs.Id };
            t.Sides[1] = new TradeSide { SessionId = s.Id };
            _tradeOf[fs.Id] = t; _tradeOf[s.Id] = t;
            t.Version++;
            SendTrade(t);
            return $"OK troca aberta com {a.Name}";
        }

        private Trade? Open(Session s, out string err)
        {
            err = "";
            if (_tradeOf.TryGetValue(s.Id, out var t)) return t;
            err = "ERR nenhuma troca aberta";
            return null;
        }

        private string TradeAdd(Session s, Character c, string[] p)
        {
            var t = Open(s, out var err); if (t == null) return err;
            int slot = Int(p, 2);
            var it = c.Inventory.Get(slot);
            if (it == null) return "ERR slot vazio";
            if (it.Def.Bound) return "ERR item vinculado não pode ser trocado";
            var me = t.Of(s.Id);
            if (me.Items.Any(e => e.slot == slot)) return "ERR item já está na troca";
            if (me.Items.Count >= 12) return "ERR no máximo 12 itens por troca";
            me.Items.Add((slot, it));
            me.Seen.Add(ItemFields(it));
            t.Unlock();
            SendTrade(t);
            return $"OK ofereceu {it.DisplayName}";
        }

        private string TradeRemove(Session s, Character c, string[] p)
        {
            var t = Open(s, out var err); if (t == null) return err;
            int slot = Int(p, 2);
            var me = t.Of(s.Id);
            int idx = me.Items.FindIndex(e => e.slot == slot);
            if (idx < 0) return "ERR item não está na troca";
            me.Items.RemoveAt(idx); me.Seen.RemoveAt(idx);
            t.Unlock();
            SendTrade(t);
            return "OK";
        }

        private string TradeGold(Session s, Character c, string[] p)
        {
            var t = Open(s, out var err); if (t == null) return err;
            if (!TryPL(p[2], out var gold) || gold < 0) return "ERR quantia inválida";
            if (gold > c.Gold) return "ERR ouro insuficiente";
            t.Of(s.Id).Gold = gold;
            t.Unlock();
            SendTrade(t);
            return "OK";
        }

        private string TradeLock(Session s)
        {
            var t = Open(s, out var err); if (t == null) return err;
            t.Of(s.Id).Locked = true;
            t.Version++;
            SendTrade(t);
            return "OK oferta travada";
        }

        private string TradeConfirm(Session s)
        {
            var t = Open(s, out var err); if (t == null) return err;
            if (!t.Sides[0].Locked || !t.Sides[1].Locked) return "ERR os dois precisam travar antes de confirmar";
            t.Of(s.Id).Confirmed = true;
            t.Version++;
            if (t.Sides[0].Confirmed && t.Sides[1].Confirmed) return ExecuteTrade(t);
            SendTrade(t);
            return "OK confirmado, esperando o outro jogador";
        }

        /// <summary>Executa a troca de uma vez, depois de revalidar tudo. Se algo não bater, cancela sem mexer em nada.</summary>
        private string ExecuteTrade(Trade t)
        {
            var sa = _sessions[t.Sides[0].SessionId]; var sb = _sessions[t.Sides[1].SessionId];
            var a = sa.Character!; var b = sb.Character!;
            string? Check(TradeSide side, Character owner)
            {
                foreach (var (slot, item) in side.Items)
                    if (!ReferenceEquals(owner.Inventory.Get(slot), item)) return $"os itens de {owner.Name} mudaram";
                if (side.Gold > owner.Gold) return $"{owner.Name} não tem o ouro oferecido";
                return null;
            }
            var why = Check(t.Sides[0], a) ?? Check(t.Sides[1], b);
            // Espaço: cada um recebe os itens do outro nos espaços livres + os que ele mesmo libera
            if (why == null && !b.Inventory.CanReceive(t.Sides[1].Items.Select(x => x.slot), t.Sides[0].Items.Select(x => x.item))) why = $"a mochila de {b.Name} não tem espaço";
            if (why == null && !a.Inventory.CanReceive(t.Sides[0].Items.Select(x => x.slot), t.Sides[1].Items.Select(x => x.item))) why = $"a mochila de {a.Name} não tem espaço";
            if (why != null) { EndTrade(t, "Troca cancelada: " + why + "."); return "ERR " + why; }

            foreach (var (slot, _) in t.Sides[0].Items) a.Inventory.Remove(slot);
            foreach (var (slot, _) in t.Sides[1].Items) b.Inventory.Remove(slot);
            foreach (var (_, item) in t.Sides[0].Items) b.Inventory.TryAdd(item, out _);
            foreach (var (_, item) in t.Sides[1].Items) a.Inventory.TryAdd(item, out _);
            a.Gold += t.Sides[1].Gold - t.Sides[0].Gold;
            b.Gold += t.Sides[0].Gold - t.Sides[1].Gold;
            a.Inventory.Touch(); b.Inventory.Touch();
            LogLine($"troca: {a.Name} deu [{Describe(t.Sides[0].Items.Select(x => x.item), t.Sides[0].Gold)}] e {b.Name} deu [{Describe(t.Sides[1].Items.Select(x => x.item), t.Sides[1].Gold)}]");
            EndTrade(t, "Troca concluída!");
            SaveCharacter(sa); SaveCharacter(sb);
            return "OK troca concluída";
        }

        private void EndTrade(Trade t, string reason)
        {
            foreach (var side in t.Sides)
            {
                _tradeOf.Remove(side.SessionId);
                if (_sessions.TryGetValue(side.SessionId, out var ss)) { Push(ss, "@TRADE CLOSE"); Sys(ss, reason); }
            }
        }

        /// <summary>Cancela trocas com jogadores longe, em outro mapa, caídos ou desconectados (chamado no Tick).</summary>
        private void TickTrades()
        {
            foreach (var t in _tradeOf.Values.Distinct().ToList())
            {
                _sessions.TryGetValue(t.Sides[0].SessionId, out var sa);
                _sessions.TryGetValue(t.Sides[1].SessionId, out var sb);
                var a = sa?.Character; var b = sb?.Character;
                if (a == null || b == null || a.IsDead || b.IsDead || a.MapId != b.MapId || MathK.Dist(a.X, a.Z, b.X, b.Z) > TradeBreakRange)
                { EndTrade(t, "Troca cancelada: vocês se afastaram."); continue; }
                // Item oferecido mudou (usou uma poção da pilha, refinou, vendeu...): destrava e mostra de novo
                bool changed = false;
                foreach (var (side, owner) in new[] { (t.Sides[0], a), (t.Sides[1], b) })
                    for (int i = side.Items.Count - 1; i >= 0; i--)
                    {
                        var (slot, item) = side.Items[i];
                        if (!ReferenceEquals(owner.Inventory.Get(slot), item)) { side.Items.RemoveAt(i); side.Seen.RemoveAt(i); changed = true; continue; }
                        var f = ItemFields(item);
                        if (f != side.Seen[i]) { side.Seen[i] = f; changed = true; }
                    }
                if (changed) { t.Unlock(); SendTrade(t); }
            }
        }

        /// <summary>
        /// @TRADE parceiro|meuOuro|travei|confirmei|ouroDele|eleTravou|eleConfirmou|M;slot;item...|T;item...
        /// (M = meus itens com o espaço da mochila, T = itens dele).
        /// </summary>
        private void SendTrade(Trade t)
        {
            for (int i = 0; i < 2; i++)
            {
                var me = t.Sides[i]; var them = t.Sides[1 - i];
                if (!_sessions.TryGetValue(me.SessionId, out var ss)) continue;
                var partner = _sessions.TryGetValue(them.SessionId, out var ts) ? ts.Character?.Name ?? "?" : "?";
                var sb = new StringBuilder("@TRADE ").Append(Clean(partner))
                    .Append(ListSep).Append(me.Gold).Append(ListSep).Append(me.Locked ? 1 : 0).Append(ListSep).Append(me.Confirmed ? 1 : 0)
                    .Append(ListSep).Append(them.Gold).Append(ListSep).Append(them.Locked ? 1 : 0).Append(ListSep).Append(them.Confirmed ? 1 : 0);
                foreach (var (slot, item) in me.Items) sb.Append(ListSep).Append("M;").Append(slot).Append(Sep).Append(ItemFields(item));
                foreach (var (_, item) in them.Items) sb.Append(ListSep).Append("T;0").Append(Sep).Append(ItemFields(item));
                ss.Outbox.Add(sb.ToString());
            }
        }
    }
}
