using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.World;
using static Kezor.Core.Server.Protocol;

namespace Kezor.Core.Server
{
    public sealed partial class GameServer
    {
        private string Stats(Character c)
        {
            var cs = c.GetCombatStats();
            return $"OK {c.Name} [{c.Def.DisplayName}] Nv {c.Level} Renasc. {c.Rebirths} Exp {c.Experience}/{Character.ExpToNext(c.Level)} " +
                   $"HP {c.Hp}/{c.MaxHp} MP {c.Mp}/{c.MaxMp} Ouro {c.Gold} | FOR {c.Stats.Strength} AGI {c.Stats.Agility} VIT {c.Stats.Vitality} ENE {c.Stats.Energy} Livres {c.FreePoints} | " +
                   $"Dano {cs.MinDamage}-{cs.MaxDamage} Magia {cs.MinMagic}-{cs.MaxMagic} Def {cs.Defense} TA {cs.AttackRate} TD {cs.DefenseRate} | Pos {F(c.X)},{F(c.Z)}";
        }

        private static string AddPoint(Character c, string[] p)
        {
            StatType t = p[1].ToUpperInvariant() switch
            {
                "STR" or "FOR" => StatType.Strength, "AGI" => StatType.Agility,
                "VIT" => StatType.Vitality, "ENE" => StatType.Energy,
                _ => throw new FormatException()
            };
            return c.AddPoints(t, Int(p, 2), out var err) ? $"OK {t} = {c.Stats.Get(t)}" : "ERR " + err;
        }

        private string Look(Character c)
        {
            var sb = new StringBuilder("OK");
            foreach (var m in _monsters.Values.Where(m => !m.IsDead).OrderBy(m => MathK.Dist(c.X, c.Z, m.X, m.Z)).Take(10))
                sb.Append($" | #{m.Id} {m.Name} Nv{m.Level} HP {m.Hp}/{m.MaxHp} dist {F(MathK.Dist(c.X, c.Z, m.X, m.Z))}");
            foreach (var g in _ground.Values.OrderBy(g => MathK.Dist(c.X, c.Z, g.X, g.Z)).Take(5))
                sb.Append($" | item #{g.Id} {g.Item.DisplayName} dist {F(MathK.Dist(c.X, c.Z, g.X, g.Z))}");
            return sb.ToString();
        }

        private static string Inv(Character c)
        {
            var sb = new StringBuilder($"OK Ouro {c.Gold}");
            foreach (var kv in c.Equipment) sb.Append($" | [{kv.Key}] {kv.Value.DisplayName}");
            for (int i = 0; i < c.Inventory.Capacity; i++)
            {
                var it = c.Inventory.Get(i);
                if (it != null) sb.Append($" | {i}: {it.DisplayName}");
            }
            return sb.ToString();
        }

        private string Use(Session s, Character c, string[] p)
        {
            int slot = Int(p, 1);
            var it = c.Inventory.Get(slot);
            if (it == null) return "ERR slot vazio";
            if (!it.Def.IsPotion) return "ERR item não é consumível";
            if (_time - s.LastPotionTime < 0.4) return "ERR aguarde";
            s.LastPotionTime = _time;
            if (it.Def.Potion == PotionType.Health) c.Hp = Math.Min(c.MaxHp, c.Hp + it.Def.PotionAmount);
            else c.Mp = Math.Min(c.MaxMp, c.Mp + it.Def.PotionAmount);
            c.Inventory.ConsumeOne(slot);
            return "OK";
        }

        /// <summary>QUICK &lt;0-3&gt; &lt;itemId|0&gt;: põe um consumível (ou nada) no atalho Q/W/E/R. Só aceita poções.</summary>
        private string Quick(Character c, string[] p)
        {
            int slot = Int(p, 1), id = p.Length > 2 ? Int(p, 2) : 0;
            if (slot < 0 || slot >= Character.QuickSlotCount) return "ERR atalho inválido (0-3)";
            if (id != 0)
            {
                var def = ItemDatabase.Get(id);
                if (def == null || !def.IsPotion) return "ERR só consumíveis vão nos atalhos";
            }
            for (int i = 0; i < Character.QuickSlotCount; i++) if (id != 0 && c.QuickSlots[i] == id) c.QuickSlots[i] = 0;   // sem repetir
            c.QuickSlots[slot] = id;
            return "OK atalho";
        }

        /// <summary>USEQ &lt;0-3&gt;: usa uma unidade do consumível do atalho (a pilha mais vazia primeiro).</summary>
        private string UseQuick(Session s, Character c, string[] p)
        {
            int slot = Int(p, 1);
            if (slot < 0 || slot >= Character.QuickSlotCount) return "ERR atalho inválido (0-3)";
            int id = c.QuickSlots[slot];
            if (id == 0) return "ERR atalho vazio";
            int best = -1, bestQty = int.MaxValue;
            foreach (var (i, it) in c.Inventory.Items())
                if (it.Def.Id == id && it.Quantity < bestQty) { best = i; bestQty = it.Quantity; }
            if (best < 0) return "ERR sem " + (ItemDatabase.Get(id)?.Name ?? "item") + " na mochila";
            return Use(s, c, new[] { "USE", best.ToString() });
        }

        // ------------------------------------------------------------ Árvore da Ascensão
        /// <summary>ASC INFO | ASC LEARN &lt;nó&gt; | ASC RESET (custa ouro; devolve todos os pontos).</summary>
        private string AscensionCmd(Session s, Character c, string[] p)
        {
            var a = c.Ascension;
            string sub = p.Length > 1 ? p[1].ToUpperInvariant() : "INFO";
            switch (sub)
            {
                case "INFO":
                    s.SentAscVersion = int.MinValue;
                    return a.Unlocked ? $"OK Ascensão {a.Level} ({a.Points} pontos livres)" : $"OK a Ascensão libera no nível {AscensionTree.UnlockLevel}";
                case "LEARN":
                {
                    int id = Int(p, 2);
                    if (!a.Learn(c.Class, id, out var err)) return "ERR " + err;
                    var n = AscensionTree.Get(id)!;
                    c.Hp = Math.Min(c.Hp, c.MaxHp); c.Mp = Math.Min(c.Mp, c.MaxMp);
                    return $"OK {n.Name} {a.RankOf(id)}/{n.MaxRank}";
                }
                case "RESET":
                {
                    int spent = a.Spent;
                    if (spent == 0) return "ERR nenhum ponto gasto";
                    long cost = AscensionTree.ResetCost(spent);
                    if (c.Gold < cost) return $"ERR ouro insuficiente ({cost:N0})";
                    c.Gold -= cost;
                    a.ResetNodes();
                    c.Hp = Math.Min(c.Hp, c.MaxHp); c.Mp = Math.Min(c.Mp, c.MaxMp);
                    return $"OK pontos devolvidos por {cost} ouro";
                }
                default: return "ERR ASC INFO | LEARN <nó> | RESET";
            }
        }

        /// <summary>@ASC liberado;nível;exp;próximo;pontos;nó:rank,nó:rank</summary>
        private void SendAscension(Session s, Character c)
        {
            var a = c.Ascension;
            s.SentAscVersion = a.Version;
            var nodes = string.Join(",", a.Ranks.Where(kv => kv.Value > 0).Select(kv => kv.Key + ":" + kv.Value));
            s.Outbox.Add($"@ASC {(a.Unlocked ? 1 : 0)};{a.Level};{a.Exp};{AscensionTree.ExpToNext(a.Level)};{a.Points};{nodes}");
        }

        /// <summary>DROP &lt;slot&gt;: larga o item da mochila no chão, aos pés do herói (fica reservado ao dono por um tempo).</summary>
        private string DropToGround(Character c, string[] p)
        {
            int slot = Int(p, 1);
            var it = c.Inventory.Get(slot);
            if (it == null) return "ERR slot vazio";
            if (it.Def.Bound) return "ERR este item não pode ser largado";
            if (c.IsDead) return "ERR você está caído";
            c.Inventory.Remove(slot);
            var map = MapOf(c.MapId);
            var g = new GroundItem
            {
                Id = _nextEntityId++, Item = it, MapId = c.MapId,
                X = map.ClampCoord(c.X + _rng.Range(-0.6f, 0.6f)), Z = map.ClampCoord(c.Z + _rng.Range(-0.6f, 0.6f)),
                OwnerId = c.Id, OwnerPartyId = 0, ReservedUntil = _time + ReservedSeconds, ExpiresAt = _time + GroundItemSeconds
            };
            _ground[g.Id] = g;
            return $"OK {it.DisplayName} no chão";
        }

        private string Rune(Character c, string[] p)
        {
            int js = Int(p, 1), ts = Int(p, 2);
            var rune = c.Inventory.Get(js); var target = c.Inventory.Get(ts);
            if (rune == null || target == null || js == ts) return "ERR slots inválidos";
            var r = RuneService.Apply(rune, target, _rng);
            if (!r.Applied) return "ERR " + r.Message;
            c.Inventory.ConsumeOne(js);
            c.Inventory.Touch();
            return (r.Success ? "OK " : "FAIL ") + r.Message + $" -> {target.DisplayName}";
        }

        private static List<Item>? CollectSlots(Character c, string[] p, out string err)
        {
            err = "";
            var slots = p[1].Split(',').Select(PI).ToList();
            if (slots.Distinct().Count() != slots.Count) { err = "slots repetidos"; return null; }
            var items = new List<Item>();
            foreach (var sl in slots)
            {
                var it = c.Inventory.Get(sl);
                if (it == null) { err = $"slot {sl} vazio"; return null; }
                items.Add(it);
            }
            return items;
        }

        private string RequireTown(Character c) => c.MapId == Map.Id && Map.InSafeZone(c.X, c.Z) ? "" : "ERR só é possível na vila de Brasalume";

        // ------------------------------------------------------------ baú da cidade
        public const float VaultRange = 6.5f;

        /// <summary>"" se o personagem está na zona segura de uma cidade e perto de uma guardiã de baú.</summary>
        private string NearVault(Character c)
        {
            var map = MapOf(c.MapId);
            if (!map.InSafeZone(c.X, c.Z)) return "ERR o baú só abre dentro da cidade";
            foreach (var n in map.Npcs)
                if (n.Role == "vault" && MathK.Dist(c.X, c.Z, n.X, n.Z) <= VaultRange) return "";
            return "ERR chegue perto da guardiã do baú";
        }

        private string VaultOpen(Session s, Character c)
        {
            var why = NearVault(c); if (why != "") return why;
            s.VaultOpen = true;
            SendVault(s);
            return $"OK baú aberto ({Vault.Capacity} espaços)";
        }

        private string VaultPut(Session s, Character c, string[] p)
        {
            var why = NearVault(c); if (why != "") return why;
            int slot = Int(p, 1);
            var it = c.Inventory.Get(slot);
            if (it == null) return "ERR slot vazio";
            if (!c.Vault.CanStore(it, out var err)) return "ERR " + err;
            int qty = p.Length > 2 ? Int(p, 2) : it.Quantity;
            if (qty <= 0 || qty > it.Quantity) return "ERR quantidade inválida";
            Item moving;
            if (qty == it.Quantity) moving = it;
            else moving = Item.Restore(it.Def, it.Level, qty, it.Luck, it.Skill, it.AdditionalOption, it.Ascended, it.Durability);
            int cellV = p.Length > 3 ? Int(p, 3) : -1;
            if (!(cellV >= 0 && moving == it && c.Vault.Items.Put(cellV, moving)) && !c.Vault.Items.TryAdd(moving, out _)) return "ERR baú cheio";
            if (moving == it) c.Inventory.Remove(slot);
            else { it.Quantity -= qty; c.Inventory.Touch(); }
            s.VaultOpen = true;
            return $"OK guardou {moving.DisplayName}";
        }

        private string VaultTake(Session s, Character c, string[] p)
        {
            var why = NearVault(c); if (why != "") return why;
            int slot = Int(p, 1);
            var it = c.Vault.Items.Get(slot);
            if (it == null) return "ERR espaço do baú vazio";
            int qty = p.Length > 2 ? Int(p, 2) : it.Quantity;
            if (qty <= 0 || qty > it.Quantity) return "ERR quantidade inválida";
            Item moving = qty == it.Quantity ? it : Item.Restore(it.Def, it.Level, qty, it.Luck, it.Skill, it.AdditionalOption, it.Ascended, it.Durability);
            int cellI = p.Length > 3 ? Int(p, 3) : -1;
            if (!(cellI >= 0 && moving == it && c.Inventory.Put(cellI, moving)) && !c.Inventory.TryAdd(moving, out _)) return "ERR inventário cheio";
            if (moving == it) c.Vault.Items.Remove(slot);
            else { it.Quantity -= qty; c.Vault.Items.Touch(); }
            s.VaultOpen = true;
            return $"OK retirou {moving.DisplayName}";
        }

        private string VaultGold(Session s, Character c, string[] p, bool deposit)
        {
            var why = NearVault(c); if (why != "") return why;
            if (!TryPL(p[1], out var amount) || amount <= 0) return "ERR quantia inválida";
            string err;
            if (deposit)
            {
                if (amount > c.Gold) return "ERR ouro insuficiente";
                if (!c.Vault.AddGold(amount, out err)) return "ERR " + err;
                c.Gold -= amount;
            }
            else
            {
                if (!c.Vault.TakeGold(amount, out err)) return "ERR " + err;
                c.Gold += amount;
            }
            c.Inventory.Touch();
            s.VaultOpen = true;
            return deposit ? $"OK guardou {amount:N0} de ouro" : $"OK retirou {amount:N0} de ouro";
        }

        /// <summary>WARP sem argumento lista os destinos; WARP &lt;código|id&gt; cobra ouro e exige o nível do mapa.</summary>
        private string Warp(Session s, Character c, string[] p)
        {
            if (p.Length < 2)
                return "OK destinos: " + string.Join(" | ", _maps.Values.OrderBy(m => m.Id).Select(m => $"{m.WarpCode} ({m.Name}, nível {m.MinLevel}, {m.WarpCost} ouro)"));
            var arg = p[1].ToUpperInvariant();
            var dest = _maps.Values.FirstOrDefault(m => m.WarpCode == arg || m.Id.ToString() == arg);
            if (dest == null) return "ERR destino desconhecido (WARP para listar)";
            if (c.IsDead) return "ERR você está caído";
            if (c.Level < dest.MinLevel) return $"ERR {dest.Name} exige nível {dest.MinLevel}";
            if (c.Gold < dest.WarpCost) return $"ERR ouro insuficiente ({dest.WarpCost})";
            if (_time - s.LastAttackTime < 5) return "ERR espere 5 s depois de lutar";
            c.Gold -= dest.WarpCost;
            Transfer(s, c, dest, dest.RespawnX + _rng.Range(-1.5f, 1.5f), dest.RespawnZ + _rng.Range(-1.5f, 1.5f));
            return $"OK teleportado para {dest.Name}" + (dest.WarpCost > 0 ? $" (-{dest.WarpCost} ouro)" : "");
        }

        private string ForgePreview(Character c, string[] p)
        {
            var items = CollectSlots(c, p, out var err);
            if (items == null) return "ERR " + err;
            var r = _forge.Preview(items);
            return r.Success ? $"OK {r.RecipeName}: taxa {r.RatePercent}% custo {r.GoldCost} ouro" : "ERR " + r.Message;
        }

        private string Forge(Character c, string[] p)
        {
            var town = RequireTown(c); if (town != "") return town;
            var items = CollectSlots(c, p, out var err);
            if (items == null) return "ERR " + err;
            long gold = c.Gold;
            var r = _forge.Mix(items, ref gold);
            if (r.RecipeName == "") return "ERR " + r.Message;
            c.Gold = gold;
            foreach (var it in r.Consumed) c.Inventory.RemoveItem(it);
            foreach (var it in r.Outputs)
                if (!c.Inventory.Contains(it)) c.Inventory.TryAdd(it, out _);
            c.Inventory.Touch();
            return (r.Success ? "OK " : "FAIL ") + $"{r.RecipeName} ({r.RatePercent}%): {r.Message}";
        }

        private string Buy(Character c, string[] p)
        {
            var town = RequireTown(c); if (town != "") return town;
            var def = ItemDatabase.Get(Int(p, 1));
            if (def == null || def.ShopPrice <= 0) return "ERR item não vendido na loja";
            int qty = p.Length > 2 ? Math.Clamp(Int(p, 2), 1, Math.Max(1, def.MaxStack)) : 1;
            if (!def.IsPotion) qty = 1;
            long cost = (long)def.ShopPrice * qty;
            if (c.Gold < cost) return $"ERR ouro insuficiente ({cost})";
            if (!c.Inventory.TryAdd(new Item(def) { Quantity = qty }, out _)) return "ERR inventário cheio";
            c.Gold -= cost;
            return $"OK comprou {def.Name} x{qty} por {cost} ouro";
        }

        public static long SellPrice(Item it)
        {
            if (it.Def.ShopPrice > 0 && it.Level == 0) return Math.Max(1, (long)it.Def.ShopPrice * it.Quantity / 4);
            if (it.Def.IsRune) return 50_000;
            if (it.Def.IsPotion) return 5L * it.Quantity;
            return 200 + it.Level * it.Level * 150L + (it.Rarity == ItemRarity.Ascended ? 5_000 : 0);
        }

        private string Sell(Character c, string[] p)
        {
            var town = RequireTown(c); if (town != "") return town;
            int slot = Int(p, 1);
            var it = c.Inventory.Get(slot);
            if (it == null) return "ERR slot vazio";
            long price = SellPrice(it);
            c.Inventory.Remove(slot);
            c.Gold += price;
            return $"OK vendeu {it.DisplayName} por {price} ouro";
        }

        private string Repair(Character c)
        {
            var town = RequireTown(c); if (town != "") return town;
            long cost = 0;
            foreach (var it in c.Equipment.Values) cost += (it.MaxDurability - it.Durability) * (20L + it.Level * 15);
            if (c.Gold < cost) return $"ERR ouro insuficiente ({cost})";
            c.Gold -= cost;
            foreach (var it in c.Equipment.Values) it.Durability = it.MaxDurability;
            c.Inventory.Touch();
            return $"OK reparado por {cost} ouro";
        }

        private string Say(Session s, Character c, string text)
        {
            text = Clean(text);
            if (text.Length == 0) return "ERR mensagem vazia";
            if (text.Length > 120) text = text.Substring(0, 120);
            if (_time - s.LastChatTime < 0.7) return "ERR aguarde para falar de novo";
            s.LastChatTime = _time;
            if (text.StartsWith("/p ") || text.StartsWith("/g "))
            {
                if (!_partyOf.TryGetValue(c.Id, out var pid)) return "ERR você não está em grupo";
                foreach (var mid in _parties[pid].Members) { var m = CharById(mid); if (m != null) Push(m, $"@EV CHAT;party;{c.Name};{text.Substring(3)}"); }
                return "OK";
            }
            BroadcastNear(c.MapId, c.X, c.Z, $"@EV CHAT;say;{c.Name};{text}");
            return "OK";
        }

        // ------------------------------------------------------------ party
        private string PartyCmd(Character c, string[] p)
        {
            string sub = p[1].ToUpperInvariant();
            if (sub == "INVITE")
            {
                var t = CharByName(p[2]);
                if (t == null || t == c) return "ERR jogador não encontrado";
                if (_partyOf.ContainsKey(t.Id)) return "ERR jogador já está em um grupo";
                if (_partyOf.TryGetValue(c.Id, out var pid) && (_parties[pid].LeaderId != c.Id || _parties[pid].IsFull)) return "ERR só o líder convida, e o grupo tem no máximo 5";
                var ts = SessionOf(t.Id)!;
                ts.PendingInviteFrom = c.Id;
                Push(ts, $"@EV INVITE;{c.Name}");
                return $"OK convite enviado para {t.Name}";
            }
            var s = SessionOf(c.Id)!;
            if (sub == "ACCEPT")
            {
                var inviter = CharById(s.PendingInviteFrom);
                s.PendingInviteFrom = 0;
                if (inviter == null) return "ERR convite expirado";
                if (_partyOf.ContainsKey(c.Id)) return "ERR você já está em um grupo";
                if (!_partyOf.TryGetValue(inviter.Id, out var pid))
                {
                    var np = new Party { Id = _nextPartyId++, LeaderId = inviter.Id };
                    np.Members.Add(inviter.Id);
                    _parties[np.Id] = np; _partyOf[inviter.Id] = np.Id; pid = np.Id;
                }
                var party = _parties[pid];
                if (party.IsFull) return "ERR grupo cheio";
                party.Members.Add(c.Id); _partyOf[c.Id] = pid;
                SendParty(party);
                return "OK entrou no grupo";
            }
            if (sub == "LEAVE") { return LeaveParty(c) ? "OK saiu do grupo" : "ERR você não está em grupo"; }
            return "ERR use PARTY INVITE <nome> | ACCEPT | LEAVE";
        }

        private bool LeaveParty(Character c)
        {
            if (!_partyOf.TryGetValue(c.Id, out var pid)) return false;
            var party = _parties[pid];
            party.Members.Remove(c.Id); _partyOf.Remove(c.Id);
            Push(c, "@EV PARTY;0;");
            if (party.Members.Count <= 1)
            {
                foreach (var m in party.Members) { _partyOf.Remove(m); var mc = CharById(m); if (mc != null) Push(mc, "@EV PARTY;0;"); }
                _parties.Remove(pid);
            }
            else
            {
                if (party.LeaderId == c.Id) party.LeaderId = party.Members[0];
                SendParty(party);
            }
            return true;
        }

        private void SendParty(Party party)
        {
            var names = string.Join(",", party.Members.Select(CharById).Where(x => x != null).Select(x => x!.Id + ":" + x.Name));
            foreach (var mid in party.Members) { var m = CharById(mid); if (m != null) Push(m, $"@EV PARTY;{party.Id};{party.LeaderId};{names}"); }
        }

        // ------------------------------------------------------------ GM (testes)
        private string Gm(Character c, string[] p)
        {
            switch (p[1].ToUpperInvariant())
            {
                case "LEVEL":
                    int target = Math.Clamp(Int(p, 2), 1, Character.MaxLevel);
                    while (c.Level < target) c.GainExperience(Character.ExpToNext(c.Level) - c.Experience);
                    return $"OK nível {c.Level}";
                case "GOLD": c.Gold += PL(p[2]); return $"OK ouro {c.Gold}";
                case "RUNES":
                    foreach (var d in new[] { ItemDatabase.RuneOfDawn, ItemDatabase.RuneOfDawn, ItemDatabase.RuneOfDawn, ItemDatabase.RuneOfEclipse, ItemDatabase.RuneOfEclipse, ItemDatabase.VitalRune, ItemDatabase.VortexCrystal })
                        c.Inventory.TryAdd(new Item(d), out _);
                    return "OK runas adicionadas";
                case "EVENT": _eventTimer = 0.01f; return "OK evento iniciando";
                case "RIFT": if (_riftActive) return "ERR a fenda já está aberta"; StartRift(); return "OK fenda aberta na Escarpa";
                case "HEAL": c.Hp = c.MaxHp; c.Mp = c.MaxMp; return "OK";
                case "ASC":
                {   // GM ASC <nível>: libera e define o nível de Ascensão (testes e capturas)
                    c.Ascension.Unlocked = true;
                    c.Ascension.Level = Math.Clamp(Int(p, 2), 0, AscensionTree.MaxLevel);
                    c.Ascension.Touch();
                    return $"OK Ascensão {c.Ascension.Level}";
                }
                case "TP":
                {   // teleporte no mesmo mapa (capturas e testes); não para dentro de parede
                    var map = MapOf(c.MapId);
                    if (p.Length < 4 || !TryPF(p[2], out var tx) || !TryPF(p[3], out var tz)) return "ERR GM TP x z";
                    float x = map.ClampCoord(tx), z = map.ClampCoord(tz);
                    if (map.IsBlocked(x, z)) return "ERR lugar bloqueado";
                    c.X = x; c.Z = z;
                    return $"OK {Protocol.F(x)} {Protocol.F(z)}";
                }
                case "ITEM":
                {
                    var def = ItemDatabase.Get(Int(p, 2));
                    if (def == null) return "ERR item inexistente";
                    var it = new Item(def, p.Length > 3 ? Int(p, 3) : 0);
                    if (def.MaxStack > 1) it.Quantity = def.MaxStack;
                    return c.Inventory.TryAdd(it, out _) ? $"OK {it.DisplayName}" : "ERR inventário cheio";
                }
                default: return "ERR GM LEVEL|GOLD|RUNES|EVENT|RIFT|HEAL|ITEM|TP|ASC";
            }
        }

        // ------------------------------------------------------------ evento: Invasão das Cinzas
        private float _eventTimer;
        private bool _eventActive, _eventWarned;
        private double _eventEnds;
        private readonly List<int> _eventMonsters = new List<int>();
        public bool EventActive => _eventActive;

        private void TickEvent(float dt)
        {
            if (_eventActive)
            {
                _eventMonsters.RemoveAll(id => !_monsters.ContainsKey(id));
                if (_eventMonsters.Count == 0) { _eventActive = false; _eventTimer = _opt.EventInterval; return; }
                if (_time >= _eventEnds)
                {
                    foreach (var id in _eventMonsters) _monsters.Remove(id);
                    _eventMonsters.Clear(); _eventActive = false; _eventTimer = _opt.EventInterval;
                    SysAll("As cinzas se dispersaram... a Invasão terminou sem vencedor.");
                }
                return;
            }
            _eventTimer -= dt;
            if (!_eventWarned && _eventTimer <= 30f && _eventTimer > 0.5f)
            {
                _eventWarned = true;
                SysAll("[Evento] O céu escurece ao sul de Brasalume... a Invasão das Cinzas começa em 30 segundos!");
            }
            if (_eventTimer > 0) return;
            StartInvasion();
        }

        private void StartInvasion()
        {
            _eventActive = true; _eventWarned = false;
            _eventEnds = _time + _opt.EventDuration;
            float cx = Map.SafeX, cz = Map.SafeZ - Map.SafeRadius - 10;
            var boss = Spawn(MonsterCatalog.AshHerald, cx, cz);
            boss.WanderRadius = 3;
            _eventMonsters.Add(boss.Id);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 0.785f;
                var m = Spawn(MonsterCatalog.Ashling, cx + (float)Math.Cos(a) * 5, cz + (float)Math.Sin(a) * 5);
                _eventMonsters.Add(m.Id);
            }
            SysAll("[Evento] A Invasão das Cinzas chegou ao portão sul! Derrote o Arauto das Cinzas para ganhar runas.");
            Broadcast("@EV EVENT;1;Invasão das Cinzas");
        }

        private void OnBossKilled(Monster m, Character owner)
        {
            if (m.Def == MonsterCatalog.CrimsonFrostColossus) return;   // a Fenda avisa por conta própria
            SysAll($"[Evento] {owner.Name} derrotou o {m.Name}! As runas caíram no chão.");
            if (m.Def == MonsterCatalog.AshHerald) Broadcast("@EV EVENT;0;Invasão das Cinzas");
        }
    }
}
