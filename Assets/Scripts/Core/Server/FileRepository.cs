using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kezor.Core.Characters;
using Kezor.Core.Items;

namespace Kezor.Core.Server
{
    /// <summary>Converte personagem/conta em JSON (MiniJson) e de volta. Formato versionado ("v").</summary>
    public static class SaveData
    {
        public const int Version = 1;

        public static Dictionary<string, object?> ItemToJson(Item it) => new Dictionary<string, object?>
        {
            ["id"] = it.Def.Id, ["lvl"] = it.Level, ["qty"] = it.Quantity, ["luck"] = it.Luck, ["skill"] = it.Skill,
            ["opt"] = it.AdditionalOption, ["asc"] = (int)it.Ascended, ["dur"] = it.Durability,
        };

        public static Item? ItemFromJson(IDictionary<string, object?> o)
        {
            var def = ItemDatabase.Get(o.I("id"));
            if (def == null) return null;   // item removido do jogo: ignora em vez de quebrar o save
            return Item.Restore(def, o.I("lvl"), Math.Max(1, o.I("qty", 1)), o.B("luck"), o.B("skill"), o.I("opt"), (AscendedOption)o.I("asc"), o.I("dur"));
        }

        private static List<object?> SlotsToJson(Inventory inv)
        {
            var list = new List<object?>();
            for (int i = 0; i < inv.Capacity; i++)
            {
                var it = inv.Get(i);
                if (it == null) continue;
                var j = ItemToJson(it); j["slot"] = i;
                list.Add(j);
            }
            return list;
        }

        /// <summary>
        /// Carrega os itens nos slots salvos; o que não couber no lugar (saves de antes da grade por tamanho) é
        /// reempacotado no primeiro espaço livre. Retorna o que não coube de jeito nenhum.
        /// </summary>
        private static List<Item> SlotsFromJson(Inventory inv, List<object?> list)
        {
            var later = new List<Item>();
            foreach (var e in list.OfType<IDictionary<string, object?>>())
            {
                int slot = e.I("slot", -1);
                var it = ItemFromJson(e);
                if (it == null) continue;
                if (!inv.Put(slot, it)) later.Add(it);
            }
            var lost = new List<Item>();
            foreach (var it in later.OrderByDescending(i => ItemSize.Of(i.Def).w * ItemSize.Of(i.Def).h))
                if (!inv.TryAdd(it, out _)) lost.Add(it);
            return lost;
        }

        public static Dictionary<string, object?> VaultToJson(Vault v) => new Dictionary<string, object?> { ["gold"] = v.Gold, ["items"] = SlotsToJson(v.Items) };

        public static Vault VaultFromJson(IDictionary<string, object?> o)
        {
            var v = new Vault();
            v.RestoreGold(o.L("gold"));
            SlotsFromJson(v.Items, o.A("items"));
            return v;
        }

        /// <summary>
        /// O herói volta exatamente onde saiu. Só se essa posição não existir mais (fora do mapa ou, depois de uma mudança
        /// de planta, dentro de uma casa/parede/poça) ele é posto no ponto livre mais próximo — nunca levado para longe.
        /// </summary>
        public static void FixPosition(Character c, int savedLayout)
        {
            var map = Kezor.Core.World.MapDefinition.Get(c.MapId);
            if (map.Id != c.MapId) { c.MapId = map.Id; c.X = map.RespawnX; c.Z = map.RespawnZ; return; }   // mapa que não existe mais
            // Vale v2 (mapa de 260 m) → v3 (340 m): o mesmo lugar em relação à vila fica deslocado pelo centro novo
            if (map.Id == Kezor.Core.World.MapDefinition.ValeId && savedLayout > 0 && savedLayout < 3)
            {
                float shift = Kezor.Core.World.MapDefinition.ValeCenter - Kezor.Core.World.MapDefinition.ValeCenterV2;
                c.X += shift; c.Z += shift;
            }
            c.X = map.ClampCoord(c.X); c.Z = map.ClampCoord(c.Z);
            if (!map.IsBlocked(c.X, c.Z)) return;
            for (float r = 0.5f; r <= 30f; r += 0.5f)                        // anel a anel, o lugar livre mais perto
                for (int k = 0; k < 16; k++)
                {
                    double a = k * Math.PI / 8;
                    float x = map.ClampCoord(c.X + (float)Math.Cos(a) * r), z = map.ClampCoord(c.Z + (float)Math.Sin(a) * r);
                    if (!map.IsBlocked(x, z)) { c.X = x; c.Z = z; return; }
                }
            c.X = map.RespawnX; c.Z = map.RespawnZ;
        }

        public static Dictionary<string, object?> CharacterToJson(Character c, bool includeVault)
        {
            var eq = new List<object?>();
            foreach (var kv in c.Equipment) { var j = ItemToJson(kv.Value); j["slot"] = kv.Key.ToString(); eq.Add(j); }
            var d = new Dictionary<string, object?>
            {
                ["v"] = Version, ["name"] = c.Name, ["class"] = c.Class.ToString(), ["level"] = c.Level, ["exp"] = c.Experience,
                ["rebirths"] = c.Rebirths, ["points"] = c.FreePoints, ["gold"] = c.Gold, ["infamy"] = c.Infamy, ["pksec"] = c.PkSeconds,
                ["str"] = c.Stats.Strength, ["agi"] = c.Stats.Agility, ["vit"] = c.Stats.Vitality, ["ene"] = c.Stats.Energy,
                ["account"] = c.AccountName, ["hp"] = c.Hp, ["mp"] = c.Mp, ["map"] = c.MapId, ["x"] = c.X, ["z"] = c.Z,
                ["mapv"] = Kezor.Core.World.MapDefinition.Get(c.MapId).LayoutVersion, ["yaw"] = c.Facing,
                ["inventory"] = SlotsToJson(c.Inventory), ["equipment"] = eq,
                ["quick"] = c.QuickSlots.Select(q => (object?)(long)q).ToList(),
                ["asc"] = new Dictionary<string, object?>
                {
                    ["unlocked"] = c.Ascension.Unlocked, ["level"] = c.Ascension.Level, ["exp"] = c.Ascension.Exp,
                    ["nodes"] = c.Ascension.Ranks.Select(kv => (object?)new List<object?> { (long)kv.Key, (long)kv.Value }).ToList(),
                },
            };
            if (includeVault) d["vault"] = VaultToJson(c.Vault);
            return d;
        }

        public static Character CharacterFromJson(IDictionary<string, object?> o, int id)
        {
            if (!Enum.TryParse<CharacterClass>(o.S("class"), out var cls)) cls = CharacterClass.Warden;
            var c = new Character(id, o.S("name"), cls);
            c.RestoreProgress(o.I("level", 1), o.L("exp"), o.I("rebirths"), o.I("points"));
            c.Stats = new Attributes { Strength = o.I("str", c.Stats.Strength), Agility = o.I("agi", c.Stats.Agility), Vitality = o.I("vit", c.Stats.Vitality), Energy = o.I("ene", c.Stats.Energy) };
            c.Gold = o.L("gold"); c.Infamy = o.I("infamy"); c.PkSeconds = o.I("pksec", c.Infamy > 0 ? Character.PkStageSeconds : 0); c.AccountName = o.S("account");
            c.MapId = o.I("map", 1); c.X = o.F("x"); c.Z = o.F("z");
            c.Facing = o.F("yaw");
            // (0, 0) = herói que ainda não entrou: nasce na praça. (1, 1) = herói novo que o erro antigo pôs no canto do mapa
            if (c.X == 1f && c.Z == 1f) { c.X = 0; c.Z = 0; }
            if (c.X != 0 || c.Z != 0) FixPosition(c, o.I("mapv", 1));
            var overflow = SlotsFromJson(c.Inventory, o.A("inventory"));
            foreach (var e in o.A("equipment").OfType<IDictionary<string, object?>>())
            {
                var it = ItemFromJson(e);
                if (it != null && Enum.TryParse<EquipSlot>(e.S("slot"), out var slot) && slot != EquipSlot.None) c.Equipment[slot] = it;
            }
            if (o.ContainsKey("vault")) c.Vault = VaultFromJson(o.O("vault"));
            foreach (var it in overflow) c.Vault.Items.TryAdd(it, out _);   // migração: o que não coube na mochila vai para o baú
            c.Hp = Math.Max(1, Math.Min(c.MaxHp, o.I("hp", c.MaxHp)));
            c.Mp = Math.Max(0, Math.Min(c.MaxMp, o.I("mp", c.MaxMp)));
            if (o.ContainsKey("asc"))
            {   // Árvore da Ascensão (nós de outra classe ou inexistentes são ignorados)
                var a = o.O("asc");
                c.Ascension.Unlocked = a.TryGetValue("unlocked", out var u) && u is bool ub && ub;
                c.Ascension.Level = Math.Clamp(a.I("level"), 0, AscensionTree.MaxLevel);
                c.Ascension.Exp = a.L("exp");
                var mask = ClassDefinition.MaskOf(c.Class);
                foreach (var e in a.A("nodes").OfType<List<object?>>())
                {
                    if (e.Count < 2 || !(e[0] is long nid) || !(e[1] is long rank)) continue;
                    var n = AscensionTree.Get((int)nid);
                    if (n != null && (n.Classes & mask) != 0 && rank > 0) c.Ascension.Ranks[n.Id] = (int)Math.Min(rank, n.MaxRank);
                }
                while (c.Ascension.Spent > c.Ascension.Level && c.Ascension.Ranks.Count > 0) c.Ascension.Ranks.Remove(c.Ascension.Ranks.Keys.Last());
            }
            c.Hp = Math.Max(1, Math.Min(c.MaxHp, o.I("hp", c.MaxHp)));
            c.Mp = Math.Max(0, Math.Min(c.MaxMp, o.I("mp", c.MaxMp)));
            c.Vigor = c.MaxVigor;
            if (o.ContainsKey("quick"))
            {   // atalhos Q/W/E/R (só consumíveis que existem)
                var q = o.A("quick");
                for (int i = 0; i < Character.QuickSlotCount; i++)
                {
                    int qid = i < q.Count && q[i] is long l ? (int)l : 0;
                    c.QuickSlots[i] = qid != 0 && ItemDatabase.Get(qid)?.IsPotion == true ? qid : 0;
                }
            }
            return c;
        }

        public static Dictionary<string, object?> AccountToJson(Account a)
        {
            var d = new Dictionary<string, object?>
            {
                ["v"] = Version, ["name"] = a.Name, ["password"] = a.PasswordHash, ["created"] = a.Created.ToString("o"),
                ["characters"] = a.Characters.Cast<object?>().ToList(), ["vault"] = VaultToJson(a.Vault),
            };
            if (a.BannedUntil.HasValue) { d["bannedUntil"] = a.BannedUntil.Value.ToString("o"); d["banReason"] = a.BanReason; }
            if (a.MutedUntil.HasValue) d["mutedUntil"] = a.MutedUntil.Value.ToString("o");
            return d;
        }

        private static DateTime? Utc(string s) =>
            DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : (DateTime?)null;

        public static Account AccountFromJson(IDictionary<string, object?> o)
        {
            var a = new Account { Name = o.S("name"), PasswordHash = o.S("password"), Vault = VaultFromJson(o.O("vault")) };
            if (DateTime.TryParse(o.S("created"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)) a.Created = dt;
            foreach (var n in o.A("characters")) if (n is string s) a.Characters.Add(s);
            if (o.ContainsKey("bannedUntil")) { a.BannedUntil = Utc(o.S("bannedUntil")); a.BanReason = o.S("banReason"); }
            if (o.ContainsKey("mutedUntil")) a.MutedUntil = Utc(o.S("mutedUntil"));
            return a;
        }
    }

    /// <summary>
    /// Persistência em arquivos JSON numa pasta (servidor dedicado): accounts/&lt;nome&gt;.json e characters/&lt;nome&gt;.json.
    /// Grava num .tmp e troca, para não corromper o save se o processo cair no meio. Nomes são só letras/números.
    /// </summary>
    public sealed partial class JsonFileRepository : ICharacterRepository, IAccountRepository
    {
        private readonly string _chars, _accounts;
        private int _nextId = 1_000_000;   // ids de execução dos personagens carregados (o servidor reatribui ao entrar)

        public JsonFileRepository(string folder)
        {
            _chars = Path.Combine(folder, "characters");
            _accounts = Path.Combine(folder, "accounts");
            Directory.CreateDirectory(_chars);
            Directory.CreateDirectory(_accounts);
        }

        private static string Safe(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.All(char.IsLetterOrDigit)) throw new ArgumentException("nome inválido");
            return name.ToLowerInvariant() + ".json";
        }

        private static void WriteAtomic(string path, string text)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new System.Text.UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        private static IDictionary<string, object?>? ReadJson(string path)
        {
            if (!File.Exists(path)) return null;
            return MiniJson.Read(File.ReadAllText(path)) as IDictionary<string, object?>;
        }

        public Character? Load(string name)
        {
            if (!name.All(char.IsLetterOrDigit)) return null;
            var o = ReadJson(Path.Combine(_chars, Safe(name)));
            return o == null ? null : SaveData.CharacterFromJson(o, _nextId++);
        }

        public void Save(Character c) => WriteAtomic(Path.Combine(_chars, Safe(c.Name)), MiniJson.Write(SaveData.CharacterToJson(c, includeVault: c.AccountName == "")));
        public void Delete(string name) { var p = Path.Combine(_chars, Safe(name)); if (File.Exists(p)) File.Delete(p); }

        Account? IAccountRepository.Load(string name)
        {
            if (!name.All(char.IsLetterOrDigit)) return null;
            var o = ReadJson(Path.Combine(_accounts, Safe(name)));
            return o == null ? null : SaveData.AccountFromJson(o);
        }

        public void Save(Account a) => WriteAtomic(Path.Combine(_accounts, Safe(a.Name)), MiniJson.Write(SaveData.AccountToJson(a)));
    }
}
