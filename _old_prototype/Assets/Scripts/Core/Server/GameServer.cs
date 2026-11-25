using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MuProto.Core.Characters;
using MuProto.Core.Combat;
using MuProto.Core.Items;
using MuProto.Core.World;

namespace MuProto.Core.Server
{
    /// <summary>
    /// Servidor autoritativo. Toda regra roda aqui; o cliente apenas envia intenções (comandos de texto).
    /// Thread-safety: chamar Handle/Tick sempre da mesma thread (o host de rede enfileira mensagens).
    /// </summary>
    public sealed class GameServer
    {
        public const int StartingZen = 50_000;
        public const float AttackCooldownSeconds = 0.25f;

        private readonly IRandom _rng;
        private readonly ICharacterRepository _repo;
        private readonly ChaosMachine _chaos;
        private readonly Dictionary<int, Session> _sessions = new Dictionary<int, Session>();
        private readonly Dictionary<int, Monster> _monsters = new Dictionary<int, Monster>();
        private int _nextCharId = 1, _nextMonsterId = 10_000;
        private double _time;

        public IReadOnlyDictionary<int, Monster> Monsters => _monsters;

        public sealed class Session
        {
            public int Id;
            public Character? Character;
            public double LastAttackTime = -999;
            public readonly List<string> Outbox = new List<string>();
        }

        public GameServer(IRandom? rng = null, ICharacterRepository? repo = null)
        {
            _rng = rng ?? new SystemRandom();
            _repo = repo ?? new InMemoryCharacterRepository();
            _chaos = new ChaosMachine(_rng);
            SpawnDefaultMap();
        }

        private void SpawnDefaultMap()
        {
            // "Campos de Lorien" — mapa inicial original do protótipo
            for (int i = 0; i < 5; i++) Spawn(MonsterCatalog.Slime, 132 + i, 128);
            for (int i = 0; i < 3; i++) Spawn(MonsterCatalog.Wolf, 150 + i * 2, 140);
            Spawn(MonsterCatalog.Golem, 170, 170);
        }

        public Monster Spawn(MonsterDefinition def, int x, int y)
        {
            var m = new Monster(_nextMonsterId++, def, x, y);
            _monsters[m.Id] = m;
            return m;
        }

        public Session Connect(int sessionId)
        {
            var s = new Session { Id = sessionId };
            _sessions[sessionId] = s;
            return s;
        }

        public void Disconnect(int sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var s) && s.Character != null) _repo.Save(s.Character);
            _sessions.Remove(sessionId);
        }

        public Character? GetCharacter(int sessionId) => _sessions.TryGetValue(sessionId, out var s) ? s.Character : null;

        public void Tick(float dt)
        {
            _time += dt;
            foreach (var m in _monsters.Values)
            {
                if (!m.IsDead) continue;
                m.RespawnTimer -= dt;
                if (m.RespawnTimer <= 0) m.Respawn();
            }
        }

        /// <summary>Processa um comando e retorna a resposta (uma ou mais linhas).</summary>
        public string Handle(int sessionId, string line)
        {
            if (!_sessions.TryGetValue(sessionId, out var s)) s = Connect(sessionId);
            var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "ERR comando vazio";
            string cmd = parts[0].ToUpperInvariant();
            try
            {
                if (cmd == "CREATE") return Create(s, parts);
                if (cmd == "LOGIN") return Login(s, parts);
                if (cmd == "HELP") return "OK CREATE <nome> <DK|DW|ELF> | LOGIN <nome> | STATS | ADD <STR|AGI|VIT|ENE> <n> | LOOK | ATTACK <monsterId> [skillId] | INV | EQUIP <slot> | JEWEL <jóiaSlot> <alvoSlot> | MIXPREVIEW <s1,s2,..> | MIX <s1,s2,..> | RESET | REPAIR";
                var c = s.Character;
                if (c == null) return "ERR faça LOGIN ou CREATE primeiro";
                switch (cmd)
                {
                    case "STATS": return Stats(c);
                    case "ADD": return AddPoint(c, parts);
                    case "LOOK": return Look(c);
                    case "ATTACK": return Attack(s, c, parts);
                    case "INV": return Inv(c);
                    case "EQUIP": return c.Equip(Int(parts, 1), out var e1) ? "OK equipado" : "ERR " + e1;
                    case "JEWEL": return Jewel(c, parts);
                    case "MIXPREVIEW": return MixPreview(c, parts);
                    case "MIX": return Mix(c, parts);
                    case "RESET": return c.TryReset(out var e2) ? $"OK reset #{c.Resets}, pontos livres {c.FreePoints}" : "ERR " + e2;
                    case "REPAIR": return Repair(c);
                    default: return "ERR comando desconhecido (HELP)";
                }
            }
            catch (FormatException) { return "ERR argumento inválido"; }
            catch (IndexOutOfRangeException) { return "ERR argumentos faltando"; }
        }

        private static int Int(string[] p, int i) => int.Parse(p[i]);

        private string Create(Session s, string[] p)
        {
            string name = p[1];
            if (name.Length < 3 || name.Length > 10 || !name.All(char.IsLetterOrDigit)) return "ERR nome deve ter 3-10 letras/números";
            if (_repo.Load(name) != null) return "ERR nome em uso";
            CharacterClass cls = p[2].ToUpperInvariant() switch
            {
                "DK" => CharacterClass.DarkKnight,
                "DW" => CharacterClass.DarkWizard,
                "ELF" => CharacterClass.FairyElf,
                _ => throw new FormatException()
            };
            var c = new Character(_nextCharId++, name, cls) { Zen = StartingZen };
            var starter = cls switch
            {
                CharacterClass.DarkKnight => ItemDatabase.IronBlade,
                CharacterClass.DarkWizard => ItemDatabase.ApprenticeStaff,
                _ => ItemDatabase.HuntingBow
            };
            c.Inventory.TryAdd(new Item(starter), out var slot);
            c.Equip(slot, out _);
            c.Inventory.TryAdd(new Item(ItemDatabase.LeatherArmor), out _);
            _repo.Save(c);
            s.Character = c;
            return $"OK criado {c.Name} ({c.Def.DisplayName})";
        }

        private string Login(Session s, string[] p)
        {
            var c = _repo.Load(p[1]);
            if (c == null) return "ERR personagem não existe";
            if (_sessions.Values.Any(o => o != s && o.Character == c)) return "ERR já conectado";
            s.Character = c;
            return $"OK bem-vindo {c.Name}";
        }

        private static string Stats(Character c)
        {
            var cs = c.GetCombatStats();
            return $"OK {c.Name} [{c.Def.DisplayName}] Lv {c.Level} Resets {c.Resets} Exp {c.Experience}/{Character.ExpToNext(c.Level)} " +
                   $"HP {c.Hp}/{c.MaxHp} MP {c.Mp}/{c.MaxMp} Zen {c.Zen} | STR {c.Stats.Strength} AGI {c.Stats.Agility} VIT {c.Stats.Vitality} ENE {c.Stats.Energy} Livres {c.FreePoints} | " +
                   $"Dano {cs.MinDamage}-{cs.MaxDamage} Magia {cs.MinMagic}-{cs.MaxMagic} Def {cs.Defense} AR {cs.AttackRate} DR {cs.DefenseRate}";
        }

        private static string AddPoint(Character c, string[] p)
        {
            StatType t = p[1].ToUpperInvariant() switch
            {
                "STR" => StatType.Strength, "AGI" => StatType.Agility,
                "VIT" => StatType.Vitality, "ENE" => StatType.Energy,
                _ => throw new FormatException()
            };
            return c.AddPoints(t, Int(p, 2), out var err) ? $"OK {t} = {c.Stats.Get(t)}" : "ERR " + err;
        }

        private string Look(Character c)
        {
            var sb = new StringBuilder("OK");
            foreach (var m in _monsters.Values.Where(m => !m.IsDead).OrderBy(m => Dist(c, m)))
                sb.Append($" | #{m.Id} {m.Name} Lv{m.Level} HP {m.Hp}/{m.MaxHp} dist {Dist(c, m)}");
            return sb.ToString();
        }

        private static int Dist(Character c, Monster m) => Math.Max(Math.Abs(c.X - m.X), Math.Abs(c.Y - m.Y));

        private string Attack(Session s, Character c, string[] p)
        {
            if (c.IsDead) return "ERR você está morto";
            if (!_monsters.TryGetValue(Int(p, 1), out var m) || m.IsDead) return "ERR alvo inválido";
            var skill = p.Length > 2 ? SkillCatalog.Find(Int(p, 2)) : SkillCatalog.BasicAttack;
            if (skill == null) return "ERR skill inexistente";
            if ((skill.Classes & ClassDefinition.MaskOf(c.Class)) == 0) return "ERR sua classe não usa essa skill";
            if (c.Level < skill.ReqLevel || c.Stats.Energy < skill.ReqEnergy) return "ERR requisitos da skill não atendidos";
            if (_time - s.LastAttackTime < AttackCooldownSeconds) return "ERR ataque muito rápido";
            if (c.Mp < skill.ManaCost) return "ERR mana insuficiente";

            // Protótipo: o personagem "se aproxima" do alvo se fora de alcance (movimento simplificado).
            if (Dist(c, m) > skill.Range) { c.X = m.X - Math.Sign(m.X - c.X) * 1; c.Y = m.Y; }

            s.LastAttackTime = _time;
            c.Mp -= skill.ManaCost;
            var hit = CombatResolver.Resolve(c, m, skill, _rng);
            c.WearEquipment(true, _rng);
            if (hit.Damage > 0) m.DamageBy[c.Id] = (m.DamageBy.TryGetValue(c.Id, out var d) ? d : 0) + hit.Damage;

            var sb = new StringBuilder($"OK {skill.Name} -> {m.Name}: {hit}");
            if (hit.Killed)
            {
                m.RespawnTimer = m.Def.RespawnSeconds;
                long exp = ExpFor(c.Level, m.Def);
                int lv = c.GainExperience(exp);
                long zen = _rng.Next(m.Def.ZenDropMax / 4, m.Def.ZenDropMax + 1);
                c.Zen += zen;
                sb.Append($" | +{exp} exp +{zen} zen");
                if (lv > 0) sb.Append($" | LEVEL UP! Lv {c.Level} (+{lv * c.Def.PointsPerLevel} pontos)");
                foreach (var it in DropSystem.Roll(m.Def, _rng))
                    sb.Append(c.Inventory.TryAdd(it, out var slot) ? $" | drop: {it.DisplayName} (slot {slot})" : $" | drop perdido (inventário cheio): {it.DisplayName}");
            }
            else
            {
                var counter = CombatResolver.Resolve(m, c, SkillCatalog.BasicAttack, _rng);
                c.WearEquipment(false, _rng);
                sb.Append($" | {m.Name} contra-ataca: {counter} (HP {c.Hp}/{c.MaxHp})");
                if (counter.Killed)
                {
                    c.Hp = c.MaxHp; c.Mp = c.MaxMp; c.X = 130; c.Y = 130;
                    sb.Append(" | você morreu e renasceu na cidade");
                }
            }
            return sb.ToString();
        }

        /// <summary>Exp de monstro ajustada pela diferença de nível (penalidade se monstro muito fraco).</summary>
        public static long ExpFor(int playerLevel, MonsterDefinition m)
        {
            int diff = playerLevel - m.Level;
            if (diff <= 10) return m.Experience;
            return Math.Max(1, m.Experience * 10 / diff);
        }

        private static string Inv(Character c)
        {
            var sb = new StringBuilder($"OK Zen {c.Zen}");
            foreach (var kv in c.Equipment) sb.Append($" | [{kv.Key}] {kv.Value.DisplayName}");
            for (int i = 0; i < c.Inventory.Capacity; i++)
            {
                var it = c.Inventory.Get(i);
                if (it != null) sb.Append($" | {i}: {it.DisplayName}");
            }
            return sb.ToString();
        }

        private string Jewel(Character c, string[] p)
        {
            int js = Int(p, 1), ts = Int(p, 2);
            var jewel = c.Inventory.Get(js); var target = c.Inventory.Get(ts);
            if (jewel == null || target == null || js == ts) return "ERR slots inválidos";
            var r = JewelService.Apply(jewel, target, _rng);
            if (!r.Applied) return "ERR " + r.Message;
            c.Inventory.Remove(js);
            return (r.Success ? "OK " : "FAIL ") + r.Message + $" -> {target.DisplayName}";
        }

        private static List<Item>? CollectSlots(Character c, string[] p, out string err)
        {
            err = "";
            var slots = p[1].Split(',').Select(int.Parse).ToList();
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

        private string MixPreview(Character c, string[] p)
        {
            var items = CollectSlots(c, p, out var err);
            if (items == null) return "ERR " + err;
            var r = _chaos.Preview(items);
            return r.Success ? $"OK {r.RecipeName}: taxa {r.RatePercent}% custo {r.ZenCost} zen" : "ERR " + r.Message;
        }

        private string Mix(Character c, string[] p)
        {
            var items = CollectSlots(c, p, out var err);
            if (items == null) return "ERR " + err;
            long zen = c.Zen;
            var r = _chaos.Mix(items, ref zen);
            if (r.RecipeName == "") return "ERR " + r.Message;
            c.Zen = zen;
            foreach (var it in r.Consumed) c.Inventory.RemoveItem(it);
            foreach (var it in r.Outputs)
                if (!c.Inventory.Contains(it)) c.Inventory.TryAdd(it, out _);
            return (r.Success ? "OK " : "FAIL ") + $"{r.RecipeName} ({r.RatePercent}%): {r.Message}";
        }

        private static string Repair(Character c)
        {
            long cost = 0;
            foreach (var it in c.Equipment.Values) cost += (it.MaxDurability - it.Durability) * (50L + it.Level * 20);
            if (c.Zen < cost) return $"ERR zen insuficiente ({cost})";
            c.Zen -= cost;
            foreach (var it in c.Equipment.Values) it.Durability = it.MaxDurability;
            return $"OK reparado por {cost} zen";
        }
    }
}
