using System;
using System.Collections.Generic;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.Server;
using static Kezor.Core.Server.Protocol;

namespace Kezor.Core.Net
{
    public enum EntityKind { Player, Monster, Drop, Banner }

    public sealed class EntityState
    {
        /// <summary>Direção salva do jogador (graus), para a vista nascer virada como ele saiu.</summary>
        public float Facing;
        public EntityKind Kind;
        public int Id;
        public string Name = "";
        public float X, Z;
        public int Hp, MaxHp, Level;
        /// <summary>Player: classe. Monster: id da definição. Drop: id do item.</summary>
        public int TypeId;
        public int PartyId, Infamy, WeaponId;
        /// <summary>Player: aparência (equipamento visível, montaria, mascote) como veio no snapshot.</summary>
        public string Look = "";
        /// <summary>Player: nome da guilda (aparece acima do herói).</summary>
        public string Guild = "";
        /// <summary>Player: nome da loja pessoal aberta (placa acima da barraca); vazio = sem loja.</summary>
        public string Shop = "";
        /// <summary>Monster: id do dono quando é um eco invocado (0 = inimigo). Banner: TypeId = tipo da aura, Radius = raio.</summary>
        public int OwnerId;
        public float Radius;
        public Appearance Appearance = new Appearance();
        public int ItemLevel; public ItemRarity Rarity;
        public double LastSeen;
    }

    public sealed class GameEvent
    {
        public string Type = "";
        public string[] F = Array.Empty<string>();
        public int I(int i) => i < F.Length ? PI(F[i]) : 0;
        public float Fl(int i) => i < F.Length ? PF(F[i]) : 0;
    }

    public sealed class ActiveEffect
    {
        public Combat.BuffKind Kind; public int Value; public float Expires; public bool Aura;
    }

    public sealed class PlayerInfo
    {
        public int Id; public string Name = ""; public CharacterClass Class;
        public int Level; public long Exp, ExpNext; public int Hp, MaxHp, Mp, MaxMp; public long Gold; public int FreePoints;
        public int Str, Agi, Vit, Ene, Rebirths, MinDmg, MaxDmg, MinMag, MaxMag, Defense, AttackRate, DefenseRate, PartyId, Infamy;
        /// <summary>Vigor e Escudo (HUD v4) e os atalhos de consumível Q, W, E, R (id do item; 0 = vazio).</summary>
        public int Vigor, MaxVigor, Shield, MaxShield;
        public readonly int[] Quick = new int[4];
        public float X, Z;
    }

    public sealed class LogLine { public string Channel = ""; public string Text = ""; public double Time; }

    /// <summary>
    /// Estado do mundo visto pelo cliente, montado a partir das mensagens do servidor.
    /// Não depende do Unity: a camada visual só lê daqui.
    /// </summary>
    public sealed class ClientState
    {
        public int MyId;
        public bool InWorld => MyId != 0;
        public readonly PlayerInfo Me = new PlayerInfo();
        public readonly Dictionary<int, EntityState> Entities = new Dictionary<int, EntityState>();
        public readonly Item?[] Inventory = new Item?[Character.InventorySize];
        public readonly Dictionary<EquipSlot, Item> Equipment = new Dictionary<EquipSlot, Item>();
        public readonly Queue<GameEvent> Events = new Queue<GameEvent>();
        public readonly List<LogLine> Log = new List<LogLine>();
        public readonly List<(int id, string name)> PartyMembers = new List<(int, string)>();
        public int PartyLeaderId;
        public string PendingInvite = "";
        public string LastReply = "";
        public double ServerTime, LocalTime;
        public int InventoryVersion;
        /// <summary>Baú da cidade (só chega com o baú aberto perto da guardiã).</summary>
        public readonly Item?[] Vault = new Item?[Items.Vault.Capacity];
        public long VaultGold;
        public bool VaultOpen;
        public int VaultVersion;
        /// <summary>Troca aberta (@TRADE): parceiro, ouro, travas e itens dos dois lados.</summary>
        public bool TradeOpen;
        public string TradePartner = "", TradeInviteFrom = "";
        public long MyTradeGold, TheirTradeGold;
        public bool MyTradeLocked, MyTradeConfirmed, TheirTradeLocked, TheirTradeConfirmed;
        public readonly List<(int slot, Item item)> MyTradeItems = new List<(int, Item)>();
        public readonly List<Item> TheirTradeItems = new List<Item>();
        public int TradeVersion;
        /// <summary>Guilda do próprio personagem (@GUILD): nome, cargo (0 membro, 1 oficial, 2 líder) e membros.</summary>
        public string GuildName = "", GuildInviteFrom = "", GuildInviteName = "";
        public int GuildRank;
        /// <summary>Vitrine aberta (@PSHOP): de quem é, título, se está aberta e os anúncios (espaço, preço, item).</summary>
        public string ShopOwner = "", ShopTitle = "";
        /// <summary>Último ranking recebido (@RANK): tipo (GERAL, GUA, ARC, PAT, GUILDAS) e as linhas já separadas.</summary>
        public string RankKind = "";
        public readonly List<string[]> RankRows = new List<string[]>();
        public int RankVersion;
        /// <summary>Guerra de guildas em andamento (@EV WAR): guildas, placar e segundos restantes (contados localmente).</summary>
        public string WarA = "", WarB = "", WarInviteFrom = "";
        public int WarScoreA, WarScoreB;
        public double WarEndsLocal;
        public bool AtWar => WarA != "";
        public bool ShopIsOpen, ShopVisible;
        public readonly List<(int slot, long price, Item item)> ShopListings = new List<(int, long, Item)>();
        public readonly List<(string name, int rank, bool online)> GuildMembers = new List<(string, int, bool)>();
        /// <summary>Mapa atual (vem no @WELCOME e no @ME).</summary>
        public int MapId = 1;
        /// <summary>Muda quando o mapa muda (o cliente reconstrói o mundo).</summary>
        public int MapVersion;
        public bool EventRunning; public string EventName = "";

        public void AddLog(string channel, string text)
        {
            Log.Add(new LogLine { Channel = channel, Text = text, Time = LocalTime });
            if (Log.Count > 200) Log.RemoveAt(0);
        }

        public void Process(string line)
        {
            if (line.Length == 0) return;
            if (line[0] != '@')
            {
                LastReply = line;
                if (line.StartsWith("ERR ")) AddLog("err", line.Substring(4));
                else if (line.StartsWith("FAIL ")) AddLog("fail", line.Substring(5));
                else if (line.StartsWith("OK ") && line.Length > 3 && !line.StartsWith("OK chars")) AddLog("ok", line.Substring(3)); // a lista de personagens vai para a tela de seleção
                return;
            }
            int sp = line.IndexOf(' ');
            string tag = sp < 0 ? line : line.Substring(0, sp);
            string body = sp < 0 ? "" : line.Substring(sp + 1);
            switch (tag)
            {
                case "@WELCOME":
                {
                    var w = body.Split(' ');
                    MyId = PI(w[0]);
                    int map = w.Length > 1 ? PI(w[1]) : 1;
                    if (map != MapId) { MapId = map; Entities.Clear(); VaultOpen = false; MapVersion++; }
                    break;
                }
                case "@VAULT": ParseVault(body); break;
                case "@TRADE": ParseTrade(body); break;
                case "@GUILD": ParseGuild(body); break;
                case "@PSHOP": ParseShop(body); break;
                case "@RANK":
                {
                    var rp = body.Split(ListSep);
                    RankKind = rp[0]; RankRows.Clear();
                    for (int i = 1; i < rp.Length; i++) RankRows.Add(rp[i].Split(Sep));
                    RankVersion++;
                    break;
                }
                case "@S": ParseSnapshot(body); break;
                case "@ME": ParseMe(body); break;
                case "@ASC": ParseAscension(body); break;
                case "@INV": ParseInventory(body); break;
                case "@EV": ParseEvent(body); break;
            }
        }

        private void ParseSnapshot(string body)
        {
            var parts = body.Split(ListSep);
            ServerTime = PF(parts[0]); _serverTimeAtLocal = LocalTime;
            for (int i = 1; i < parts.Length; i++)
            {
                var f = parts[i].Split(Sep);
                int id = PI(f[1]);
                if (!Entities.TryGetValue(id, out var e)) { e = new EntityState { Id = id }; Entities[id] = e; }
                e.LastSeen = ServerTime;
                switch (f[0])
                {
                    case "P":
                        e.Kind = EntityKind.Player; e.Name = f[2]; e.X = PF(f[3]); e.Z = PF(f[4]); e.Hp = PI(f[5]); e.MaxHp = PI(f[6]);
                        e.Level = PI(f[7]); e.TypeId = PI(f[8]); e.PartyId = PI(f[9]); e.Infamy = PI(f[10]); e.WeaponId = PI(f[11]);
                        var look = f.Length > 12 ? f[12] : "";
                        if (look != e.Look) { e.Look = look; e.Appearance = Appearance.Parse(look); }
                        e.Guild = f.Length > 13 ? f[13] : "";
                        e.Shop = f.Length > 14 ? f[14] : "";
                        e.Facing = f.Length > 15 && int.TryParse(f[15], out var fy) ? fy : 0;
                        break;
                    case "M":
                        e.Kind = EntityKind.Monster; e.TypeId = PI(f[2]); e.X = PF(f[3]); e.Z = PF(f[4]); e.Hp = PI(f[5]); e.MaxHp = PI(f[6]);
                        e.OwnerId = f.Length > 7 ? PI(f[7]) : 0;
                        var md = World.MonsterCatalog.Get(e.TypeId);
                        e.Name = md?.Name ?? "?"; e.Level = md?.Level ?? 0;
                        break;
                    case "B":
                        e.Kind = EntityKind.Banner; e.TypeId = PI(f[2]); e.X = PF(f[3]); e.Z = PF(f[4]); e.Name = f[5];
                        e.Radius = f.Length > 6 ? PF(f[6]) : 6f; e.Level = f.Length > 7 ? PI(f[7]) : 0;
                        break;
                    case "D":
                        e.Kind = EntityKind.Drop; e.TypeId = PI(f[2]); e.X = PF(f[3]); e.Z = PF(f[4]); e.ItemLevel = PI(f[5]);
                        e.Rarity = (ItemRarity)PI(f[6]); e.Name = f[7];
                        break;
                }
            }
            // remove o que saiu do snapshot
            List<int>? gone = null;
            foreach (var e in Entities.Values) if (e.LastSeen < ServerTime) (gone ??= new List<int>()).Add(e.Id);
            if (gone != null) foreach (var id in gone) Entities.Remove(id);
        }

        private void ParseMe(string body)
        {
            var f = body.Split(Sep); int k = 0;
            var m = Me;
            m.Id = PI(f[k++]); m.Name = f[k++]; m.Class = (CharacterClass)PI(f[k++]); m.Level = PI(f[k++]); m.Exp = PL(f[k++]); m.ExpNext = PL(f[k++]);
            m.Hp = PI(f[k++]); m.MaxHp = PI(f[k++]); m.Mp = PI(f[k++]); m.MaxMp = PI(f[k++]); m.Gold = PL(f[k++]); m.FreePoints = PI(f[k++]);
            m.Str = PI(f[k++]); m.Agi = PI(f[k++]); m.Vit = PI(f[k++]); m.Ene = PI(f[k++]); m.Rebirths = PI(f[k++]);
            m.MinDmg = PI(f[k++]); m.MaxDmg = PI(f[k++]); m.MinMag = PI(f[k++]); m.MaxMag = PI(f[k++]); m.Defense = PI(f[k++]);
            m.AttackRate = PI(f[k++]); m.DefenseRate = PI(f[k++]); m.PartyId = PI(f[k++]); m.Infamy = PI(f[k++]); m.X = PF(f[k++]); m.Z = PF(f[k++]);
            if (k < f.Length) { int map = PI(f[k++]); if (map != MapId) { MapId = map; Entities.Clear(); MapVersion++; } }
            Effects.Clear(); Cooldowns.Clear();
            if (k < f.Length && f[k].Length > 0)
                foreach (var e in f[k].Split(','))
                {
                    var p = e.Substring(1).Split(':');
                    switch (e[0])
                    {
                        case 'B': Effects.Add(new ActiveEffect { Kind = (Combat.BuffKind)PI(p[0]), Value = PI(p[1]), Expires = PF(p[2]) }); break;
                        case 'A': Effects.Add(new ActiveEffect { Kind = (Combat.BuffKind)PI(p[0]), Value = PI(p[1]), Aura = true }); break;
                        case 'C': Cooldowns[PI(p[0])] = PF(p[1]); break;
                    }
                }
            k++;
            // HUD v4: vigor, escudo e atalhos Q/W/E/R (servidores antigos não mandam)
            if (k + 3 < f.Length) { m.Vigor = PI(f[k++]); m.MaxVigor = PI(f[k++]); m.Shield = PI(f[k++]); m.MaxShield = PI(f[k++]); }
            if (k < f.Length)
            {
                var q = f[k++].Split(',');
                for (int i = 0; i < m.Quick.Length; i++) m.Quick[i] = i < q.Length && q[i].Length > 0 ? PI(q[i]) : 0;
            }
        }

        // ----- Árvore da Ascensão (@ASC liberado;nível;exp;próximo;pontos;nó:rank,...) -----
        public bool AscUnlocked; public int AscLevel, AscPoints; public long AscExp, AscNext;
        public readonly Dictionary<int, int> AscRanks = new Dictionary<int, int>();
        public int AscVersion;

        private void ParseAscension(string body)
        {
            var f = body.Split(Sep);
            if (f.Length < 5) return;
            AscUnlocked = f[0] == "1"; AscLevel = PI(f[1]); AscExp = PL(f[2]); AscNext = PL(f[3]); AscPoints = PI(f[4]);
            AscRanks.Clear();
            if (f.Length > 5 && f[5].Length > 0)
                foreach (var e in f[5].Split(','))
                {
                    var kv = e.Split(':');
                    if (kv.Length == 2) AscRanks[PI(kv[0])] = PI(kv[1]);
                }
            AscVersion++;
        }

        /// <summary>Buffs e auras ativos no meu herói (Expires em tempo do servidor; aura dura enquanto estiver no raio).</summary>
        public readonly List<ActiveEffect> Effects = new List<ActiveEffect>();
        /// <summary>Skill → horário do servidor em que fica pronta.</summary>
        public readonly Dictionary<int, float> Cooldowns = new Dictionary<int, float>();

        /// <summary>Quanto falta (s) para a skill sair da recarga, no relógio local estimado do servidor.</summary>
        public float CooldownLeft(int skillId) => Cooldowns.TryGetValue(skillId, out var t) ? System.Math.Max(0f, t - ServerTimeNow) : 0f;

        /// <summary>Tempo do servidor estimado agora (último snapshot + tempo local decorrido desde ele).</summary>
        public float ServerTimeNow => (float)(ServerTime + (LocalTime - _serverTimeAtLocal));
        private double _serverTimeAtLocal;

        public static Item? ParseItem(string[] f, int start)
        {
            var def = ItemDatabase.Get(PI(f[start]));
            if (def == null) return null;
            return Item.Restore(def, PI(f[start + 1]), PI(f[start + 2]), f[start + 3] == "1", f[start + 4] == "1", PI(f[start + 5]), (AscendedOption)PI(f[start + 6]), PI(f[start + 7]));
        }

        private void ParseInventory(string body)
        {
            var parts = body.Split(ListSep);
            Me.Gold = PL(parts[0]);
            Array.Clear(Inventory, 0, Inventory.Length);
            Equipment.Clear();
            for (int i = 1; i < parts.Length; i++)
            {
                var f = parts[i].Split(Sep);
                var it = ParseItem(f, 2);
                if (it == null) continue;
                if (f[0] == "I") { int slot = PI(f[1]); if (slot >= 0 && slot < Inventory.Length) Inventory[slot] = it; }
                else Equipment[(EquipSlot)PI(f[1])] = it;
            }
            InventoryVersion++;
        }

        private void ParseVault(string body)
        {
            if (body == "CLOSE") { VaultOpen = false; VaultVersion++; return; }
            var parts = body.Split(ListSep);
            VaultGold = PL(parts[0]);
            Array.Clear(Vault, 0, Vault.Length);
            for (int i = 1; i < parts.Length; i++)
            {
                var f = parts[i].Split(Sep);
                var it = ParseItem(f, 2);
                if (it == null) continue;
                int slot = PI(f[1]);
                if (slot >= 0 && slot < Vault.Length) Vault[slot] = it;
            }
            VaultOpen = true;
            VaultVersion++;
        }

        private void ParseShop(string body)
        {
            var p = body.Split(ListSep);
            ShopOwner = p[0]; ShopTitle = p.Length > 1 ? p[1] : ""; ShopIsOpen = p.Length > 2 && p[2] == "1";
            ShopListings.Clear();
            for (int i = 3; i < p.Length; i++)
            {
                var f = p[i].Split(Sep);
                var it = ParseItem(f, 2);
                if (it != null) ShopListings.Add((PI(f[0]), PL(f[1]), it));
            }
            ShopVisible = true;
        }

        private void ParseGuild(string body)
        {
            GuildMembers.Clear();
            if (body == "NONE") { GuildName = ""; GuildRank = 0; return; }
            var p = body.Split(ListSep);
            GuildName = p[0];
            GuildRank = PI(p[1]);
            for (int i = 2; i < p.Length; i++)
            {
                var f = p[i].Split(Sep);
                if (f.Length >= 3) GuildMembers.Add((f[0], PI(f[1]), f[2] == "1"));
            }
            GuildInviteName = "";
        }

        private void ParseTrade(string body)
        {
            TradeVersion++;
            if (body == "CLOSE") { TradeOpen = false; MyTradeItems.Clear(); TheirTradeItems.Clear(); return; }
            var p = body.Split(ListSep);
            TradePartner = p[0];
            MyTradeGold = PL(p[1]); MyTradeLocked = p[2] == "1"; MyTradeConfirmed = p[3] == "1";
            TheirTradeGold = PL(p[4]); TheirTradeLocked = p[5] == "1"; TheirTradeConfirmed = p[6] == "1";
            MyTradeItems.Clear(); TheirTradeItems.Clear();
            for (int i = 7; i < p.Length; i++)
            {
                var f = p[i].Split(Sep);
                var it = ParseItem(f, 2);
                if (it == null) continue;
                if (f[0] == "M") MyTradeItems.Add((PI(f[1]), it)); else TheirTradeItems.Add(it);
            }
            TradeOpen = true;
            TradeInviteFrom = "";
        }

        private void ParseEvent(string body)
        {
            int semi = body.IndexOf(Sep);
            string type = semi < 0 ? body : body.Substring(0, semi);
            string rest = semi < 0 ? "" : body.Substring(semi + 1);
            string[] f = type == "CHAT" ? rest.Split(new[] { Sep }, 3) : (type == "SYS" ? new[] { rest } : rest.Split(Sep));
            var ev = new GameEvent { Type = type, F = f };
            switch (type)
            {
                case "SYS": AddLog("sys", rest); break;
                case "CHAT": AddLog(f[0], $"{f[1]}: {(f.Length > 2 ? f[2] : "")}"); break;
                case "INVITE": PendingInvite = f[0]; AddLog("sys", $"{f[0]} convidou você para um grupo (tecla P para aceitar)."); break;
                case "PARTY":
                    PartyMembers.Clear();
                    PartyLeaderId = f.Length > 1 && f[1] != "" ? PI(f[1]) : 0;
                    if (f.Length > 2 && f[2] != "")
                        foreach (var m in f[2].Split(','))
                        {
                            var kv = m.Split(':');
                            if (kv.Length == 2) PartyMembers.Add((PI(kv[0]), kv[1]));
                        }
                    break;
                case "EVENT": EventRunning = f[0] == "1"; EventName = f.Length > 1 ? f[1] : ""; break;
                case "TRADEREQ": TradeInviteFrom = f[0]; AddLog("sys", $"{f[0]} quer trocar com você."); break;
                case "WAR":
                    WarA = f[0]; WarB = f[1]; WarScoreA = PI(f[2]); WarScoreB = PI(f[3]); WarEndsLocal = LocalTime + PF(f[4]); WarInviteFrom = "";
                    break;
                case "WAREND": WarA = WarB = ""; break;
                case "WARINVITE": WarInviteFrom = f[0]; break;
                case "GINVITE": GuildInviteName = f[0]; GuildInviteFrom = f.Length > 1 ? f[1] : ""; AddLog("sys", $"{GuildInviteFrom} convidou você para a guilda {f[0]} (tecla G)."); break;
            }
            Events.Enqueue(ev);
            if (Events.Count > 500) Events.Dequeue();
        }
    }
}
