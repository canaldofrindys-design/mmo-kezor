using System;
using System.Collections.Generic;
using Kezor.Core.Combat;
using Kezor.Core.Items;

namespace Kezor.Core.Characters
{
    public sealed class Character : ICombatant, IShieldBearer
    {
        public const int MaxLevel = 400;
        public const int MaxRebirths = 50;
        public const int RebirthBonusPoints = 500;
        public const int MaxStatValue = 32767;
        public const int InventorySize = 64, InventoryColumns = 8;   // mochila em grade 8×8

        public int Id { get; internal set; }
        /// <summary>Conta dona do personagem (vazio = convidado, modo local/testes).</summary>
        public string AccountName { get; set; } = "";
        public string Name { get; }
        public CharacterClass Class { get; }
        public ClassDefinition Def => ClassDefinition.Get(Class);
        public int Level { get; private set; } = 1;
        public long Experience { get; private set; }
        public int Rebirths { get; private set; }
        public int FreePoints { get; private set; }
        public Attributes Stats;
        public long Gold { get; set; }
        public int Hp { get; set; }
        public int Mp { get; set; }
        /// <summary>Contador de abates de jogadores (PvP). Exibido como nome em vermelho.</summary>
        public int Infamy { get; set; }

        // Posição no mapa (plano XZ, unidades do mundo)
        public float X { get; set; }
        public float Z { get; set; }
        /// <summary>Direção para onde o herói olha (graus, 0 = +Z). Salva com a posição, para relogar virado igual.</summary>
        public float Facing { get; set; }
        public int MapId { get; set; } = 1;

        public Inventory Inventory { get; } = new Inventory(InventorySize, InventoryColumns);
        /// <summary>Baú da cidade (vai para a conta na etapa de contas).</summary>
        public Vault Vault { get; set; } = new Vault();
        public Dictionary<EquipSlot, Item> Equipment { get; } = new Dictionary<EquipSlot, Item>();
        /// <summary>Árvore da Ascensão (nível 400): permanente, sobrevive ao renascimento.</summary>
        public AscensionState Ascension { get; } = new AscensionState();

        public Character(int id, string name, CharacterClass cls)
        {
            Id = id; Name = name; Class = cls;
            Stats = Def.BaseStats;
            Hp = MaxHp; Mp = MaxMp;
            Vigor = MaxVigor;
        }

        // ----- Vigor e Escudo (HUD v4) -----
        /// <summary>Custo de Vigor de cada skill (o ataque básico não gasta).</summary>
        public const int SkillVigorCost = 6;
        /// <summary>Vigor recuperado por segundo em combate e fora dele.</summary>
        public const float VigorRegenCombat = 8f, VigorRegenRest = 25f;
        /// <summary>Segundos sem atacar nem apanhar para sair de combate.</summary>
        public const float CombatSeconds = 5f;
        /// <summary>Escudo: 20% da vida máxima; volta 10% por segundo depois de 6 s sem levar dano.</summary>
        public const int ShieldPercentOfHp = 20;
        public const float ShieldDelaySeconds = 6f, ShieldRegenPerSecond = 0.10f;

        /// <summary>Vigor: fôlego para skills. Sem vigor suficiente a skill não sai (só o ataque básico).</summary>
        public float Vigor { get; set; }
        public int MaxVigor => 100 + Math.Min(100, Stats.Agility / 10) + Ascension.Total(AscEffect.Vigor);
        /// <summary>Escudo de brasa: absorve dano antes da vida. Começa vazio e enche fora de perigo.</summary>
        public float Shield { get; set; }
        public int MaxShield => MaxHp * (ShieldPercentOfHp + Ascension.Total(AscEffect.ShieldPct)) / 100;
        /// <summary>Tempo restante em combate (atacou ou apanhou há pouco).</summary>
        public float CombatTimer { get; set; }
        /// <summary>Tempo até o Escudo voltar a carregar.</summary>
        public float ShieldDelay { get; set; }
        public bool InCombat => CombatTimer > 0f;

        /// <summary>Atalhos de consumíveis Q, W, E, R (id do item; 0 = vazio). Salvos com o personagem.</summary>
        public readonly int[] QuickSlots = { 2000, 2002, 2001, 0 };
        public const int QuickSlotCount = 4;

        public int AbsorbShield(int damage)
        {
            CombatTimer = CombatSeconds;
            ShieldDelay = ShieldDelaySeconds;
            if (Shield <= 0f || damage <= 0) return damage;
            float take = Math.Min(Shield, damage);
            Shield -= take;
            return damage - (int)take;
        }

        /// <summary>Marca combate (quando o herói ataca).</summary>
        public void EnterCombat() => CombatTimer = CombatSeconds;

        /// <summary>Gasta o vigor de uma skill (false se não houver).</summary>
        public bool SpendVigor(int amount)
        {
            if (Vigor < amount) return false;
            Vigor -= amount;
            return true;
        }

        /// <summary>Passa o tempo: vigor volta (mais rápido fora de combate) e o escudo recarrega depois do atraso.</summary>
        public void TickVitals(float dt)
        {
            if (dt <= 0f) return;
            if (CombatTimer > 0f) CombatTimer = Math.Max(0f, CombatTimer - dt);
            Vigor = Math.Min(MaxVigor, Vigor + (InCombat ? VigorRegenCombat : VigorRegenRest) * dt);
            if (IsDead) { Shield = 0f; return; }
            if (ShieldDelay > 0f) { ShieldDelay = Math.Max(0f, ShieldDelay - dt); return; }
            Shield = Math.Min(MaxShield, Shield + MaxShield * ShieldRegenPerSecond * dt);
        }

        /// <summary>Restaura o progresso salvo (persistência).</summary>
        public void RestoreProgress(int level, long experience, int rebirths, int freePoints)
        {
            Level = Math.Max(1, Math.Min(MaxLevel, level));
            Experience = Math.Max(0, experience);
            Rebirths = Math.Max(0, Math.Min(MaxRebirths, rebirths));
            FreePoints = Math.Max(0, freePoints);
        }

        public bool IsDead => Hp <= 0;
        public int MaxHp
        {
            get
            {
                int hp = (int)(Def.BaseHp + Def.HpPerLevel * (Level - 1) + Def.HpPerVitality * (Stats.Vitality - Def.BaseStats.Vitality));
                foreach (var it in Equipment.Values)
                    if (it.Durability > 0 && (it.Ascended & AscendedOption.MaxHpPlus) != 0) hp += hp * 4 / 100;
                hp += hp * Ascension.Total(AscEffect.HpPct) / 100;
                foreach (var it in Equipment.Values) if (it.Def.IsJewel) hp += it.Def.JewelHp;
                return hp;
            }
        }
        public int MaxMp
        {
            get
            {
                int mp = (int)(Def.BaseMp + Def.MpPerLevel * (Level - 1) + Def.MpPerEnergy * (Stats.Energy - Def.BaseStats.Energy));
                mp += mp * Ascension.Total(AscEffect.MpPct) / 100;
                foreach (var it in Equipment.Values) if (it.Def.IsJewel) mp += it.Def.JewelMp;
                return mp;
            }
        }

        /// <summary>Slot onde um item entra: anéis vão no primeiro dedo livre (ou trocam o Ring1 se os dois estiverem ocupados).</summary>
        public EquipSlot TargetSlot(ItemDefinition d)
        {
            if (d.Kind != ItemKind.Ring) return d.Slot;
            if (!Equipment.ContainsKey(EquipSlot.Ring1)) return EquipSlot.Ring1;
            if (!Equipment.ContainsKey(EquipSlot.Ring2)) return EquipSlot.Ring2;
            return EquipSlot.Ring1;
        }
        /// <summary>Montado na montaria equipada (validado pelo servidor; desmonta ao atacar ou tirar a montaria).</summary>
        public bool Mounted { get; private set; }
        public Item? MountItem => Equipment.TryGetValue(EquipSlot.Mount, out var m) ? m : null;
        /// <summary>Conjunto completo equipado (bônus ativo), ou None.</summary>
        public ArmorSet ActiveSet => ArmorSets.Complete(Equipment);
        public PetType ActivePet => Equipment.TryGetValue(EquipSlot.Pet, out var p) ? p.Def.Pet : PetType.None;
        public float MoveSpeed => Def.MoveSpeed * (Mounted && MountItem != null ? 1f + MountItem.Def.MountSpeedPercent / 100f : 1f)
                                  * (1f + (BuffSpeedPercent + AuraSpeedPercent + Ascension.Total(AscEffect.SpeedPct)) / 100f);

        // ----- Buffs (skills em si mesmo) e auras (estandartes do Comandante) -----
        /// <summary>Buffs ativos: tipo, valor e quando expiram (tempo do servidor). O servidor remove os vencidos.</summary>
        public readonly List<(Kezor.Core.Combat.BuffKind kind, int value, double expires)> Buffs = new List<(Kezor.Core.Combat.BuffKind, int, double)>();
        /// <summary>Bônus de estandarte recalculados pelo servidor a cada tick (o maior de cada tipo por perto).</summary>
        public int AuraDamagePercent, AuraDefense, AuraSpeedPercent;
        private int BuffSum(Kezor.Core.Combat.BuffKind k) { int s = 0; foreach (var b in Buffs) if (b.kind == k) s += b.value; return s; }
        public int BuffDamagePercent => BuffSum(Kezor.Core.Combat.BuffKind.Damage);
        public int BuffSpeedPercent => BuffSum(Kezor.Core.Combat.BuffKind.Speed);
        public int BuffDefense => BuffSum(Kezor.Core.Combat.BuffKind.Defense);

        public void AddBuff(Kezor.Core.Combat.BuffKind kind, int value, double expires)
        {
            Buffs.RemoveAll(b => b.kind == kind);   // reaplicar renova (não acumula)
            Buffs.Add((kind, value, expires));
        }

        public void ExpireBuffs(double now) => Buffs.RemoveAll(b => b.expires <= now);

        public bool Mount(out string error)
        {
            error = "";
            if (IsDead) { error = "você está caído"; return false; }
            if (MountItem == null) { error = "nenhuma montaria equipada"; return false; }
            Mounted = true;
            return true;
        }

        public void Dismount() => Mounted = false;

        /// <summary>Curva de experiência: cresce ~cúbica.</summary>
        public static long ExpToNext(int level) => (long)level * level * (level + 10) * 5 + 100;

        /// <summary>Concede experiência e retorna quantos níveis foram ganhos.</summary>
        public int GainExperience(long amount)
        {
            if (amount <= 0) return 0;
            if (Level >= MaxLevel) { Ascension.Unlocked = true; Ascension.Gain(amount); return 0; }   // no 400 a experiência vira Ascensão
            Experience += amount;
            int gained = 0;
            while (Level < MaxLevel && Experience >= ExpToNext(Level))
            {
                Experience -= ExpToNext(Level);
                Level++; gained++;
                FreePoints += Def.PointsPerLevel;
            }
            if (Level >= MaxLevel) { Experience = 0; if (!Ascension.Unlocked) { Ascension.Unlocked = true; Ascension.Touch(); } }
            if (gained > 0) { Hp = MaxHp; Mp = MaxMp; }
            return gained;
        }

        public bool AddPoints(StatType stat, int amount, out string error)
        {
            error = "";
            if (amount <= 0) { error = "quantidade inválida"; return false; }
            if (amount > FreePoints) { error = "pontos insuficientes"; return false; }
            if (Stats.Get(stat) + amount > MaxStatValue) { error = "atributo no limite"; return false; }
            Stats.Add(stat, amount);
            FreePoints -= amount;
            return true;
        }

        public static long RebirthGoldCost(int rebirths) => 1_000_000L * (rebirths + 1);

        /// <summary>Renascimento: volta ao nível 1 com atributos base e pontos bônus acumulados.</summary>
        public bool TryRebirth(out string error)
        {
            error = "";
            if (Level < MaxLevel) { error = $"requer nível {MaxLevel}"; return false; }
            if (Rebirths >= MaxRebirths) { error = "limite de renascimentos"; return false; }
            long cost = RebirthGoldCost(Rebirths);
            if (Gold < cost) { error = $"ouro insuficiente ({cost})"; return false; }
            Gold -= cost;
            Rebirths++;
            Level = 1; Experience = 0;
            Stats = Def.BaseStats;
            FreePoints = Rebirths * RebirthBonusPoints;
            Hp = MaxHp; Mp = MaxMp;
            return true;
        }

        // ----- Equipamento -----
        public bool CanEquip(Item item, out string error)
        {
            error = "";
            var d = item.Def;
            if (d.Slot == EquipSlot.None) { error = "item não equipável"; return false; }
            if ((d.AllowedClasses & ClassDefinition.MaskOf(Class)) == 0) { error = "classe não pode usar"; return false; }
            if (Stats.Strength < item.RequiredStrength) { error = $"força requerida {item.RequiredStrength}"; return false; }
            if (Stats.Agility < item.RequiredAgility) { error = $"agilidade requerida {item.RequiredAgility}"; return false; }
            if (Stats.Energy < d.ReqEnergy) { error = $"energia requerida {d.ReqEnergy}"; return false; }
            if (Level < d.ReqLevel) { error = $"nível requerido {d.ReqLevel}"; return false; }
            return true;
        }

        public bool Equip(int inventorySlot, out string error)
        {
            var item = Inventory.Get(inventorySlot);
            if (item == null) { error = "slot vazio"; return false; }
            if (!CanEquip(item, out error)) return false;
            var target = TargetSlot(item.Def);
            Inventory.Remove(inventorySlot);
            if (Equipment.TryGetValue(target, out var old) && !Inventory.Put(inventorySlot, old) && !Inventory.TryAdd(old, out _))
            {   // o item que sai não cabe na mochila (tamanhos diferentes): desfaz
                Inventory.Put(inventorySlot, item);
                error = "sem espaço na mochila para o item trocado";
                return false;
            }
            Equipment[target] = item;
            Hp = Math.Min(Hp, MaxHp); Mp = Math.Min(Mp, MaxMp);
            return true;
        }

        public bool Unequip(EquipSlot slot, out string error)
        {
            error = "";
            if (!Equipment.TryGetValue(slot, out var item)) { error = "nada equipado"; return false; }
            if (!Inventory.TryAdd(item, out _)) { error = "inventário cheio"; return false; }
            Equipment.Remove(slot);
            if (slot == EquipSlot.Mount) Mounted = false;
            Hp = Math.Min(Hp, MaxHp); Mp = Math.Min(Mp, MaxMp);
            return true;
        }

        private static bool Usable(Item i) => i.Durability > 0;

        // ----- Status de combate derivados -----
        public CombatStats GetCombatStats()
        {
            var d = Def;
            var s = new CombatStats
            {
                MinDamage = Stats.Strength / d.StrDivMin,
                MaxDamage = Stats.Strength / d.StrDivMax,
                Defense = Stats.Agility / d.AgiDivDefense,
                AttackRate = Level * 5 + Stats.Agility * 3 / 2 + Stats.Strength / 4,
                DefenseRate = Stats.Agility / 3,
                Level = Level
            };
            if (d.AgiDamageDivMin > 0)
            {
                s.MinDamage += Stats.Agility / d.AgiDamageDivMin;
                s.MaxDamage += Stats.Agility / d.AgiDamageDivMax;
            }
            if (d.EneDivMin > 0)
            {
                s.MinMagic = Stats.Energy / d.EneDivMin;
                s.MaxMagic = Stats.Energy / d.EneDivMax;
            }
            int magicPct = 0;
            foreach (var kv in Equipment)
            {
                var it = kv.Value;
                if (!Usable(it)) continue;
                s.MinDamage += it.MinDamage; s.MaxDamage += it.MaxDamage;
                s.Defense += it.Defense;
                magicPct += it.MagicPowerPercent;
                if (it.Luck) s.CriticalChancePercent += 5;
                if ((it.Ascended & AscendedOption.SurgeChance) != 0) s.SurgeChancePercent += 10;
                if ((it.Ascended & AscendedOption.DamagePerLevel) != 0) { s.MinDamage += Level / 20; s.MaxDamage += Level / 20; }
                if ((it.Ascended & AscendedOption.DefenseRate) != 0) s.DefenseRate += s.DefenseRate / 10;
                if ((it.Ascended & AscendedOption.DamageReduction) != 0) s.DamageReductionPercent += 4;
                if ((it.Ascended & AscendedOption.LifeOnKill) != 0) s.LifeOnKill += 1;
                if (it.Def.IsWings) { s.DamageBonusPercent += it.WingDamage; s.DamageReductionPercent += it.WingAbsorb; }
                if (it.Def.Pet == PetType.Guardian) { s.Defense += Item.GuardianPetDefense; s.DamageReductionPercent += Item.GuardianPetReduction; }
                if (it.Def.IsJewel) { s.DamageReductionPercent += it.Def.JewelResistPercent; s.DamageBonusPercent += it.Def.JewelDamagePercent; }
            }
            // Árvore da Ascensão
            var asc = Ascension;
            if (asc.Ranks.Count > 0)
            {
                s.Defense += asc.Total(AscEffect.Defense);
                s.DamageReductionPercent += asc.Total(AscEffect.ReductionPct);
                s.DamageBonusPercent += asc.Total(AscEffect.DamagePct);
                s.CriticalChancePercent += asc.Total(AscEffect.CritPct);
                s.AttackRate += s.AttackRate * asc.Total(AscEffect.AttackRatePct) / 100;
                s.DefenseRate += s.DefenseRate * asc.Total(AscEffect.DefenseRatePct) / 100;
                s.LifeOnKill += asc.Total(AscEffect.LifeOnKill);
                magicPct += asc.Total(AscEffect.MagicPct);
            }
            // buffs e auras
            s.DamageBonusPercent += BuffDamagePercent + AuraDamagePercent;
            int auraDef = BuffDefense + AuraDefense;
            if (auraDef > 0) { s.Defense += auraDef; s.DefenseRate += s.DefenseRate * auraDef / 200; }
            var set = ArmorSets.Complete(Equipment);
            if (set != ArmorSet.None)
            {
                var b = ArmorSets.Bonus(set);
                s.Defense += b.Defense;
                s.DefenseRate += s.DefenseRate * b.DefenseRatePercent / 100;
                s.DamageReductionPercent += b.ReductionPercent;
                s.DamageBonusPercent += b.DamagePercent;
                magicPct += b.MagicPercent;
            }
            if (s.MaxMagic > 0 && magicPct > 0)
            {
                s.MinMagic += s.MinMagic * magicPct / 100;
                s.MaxMagic += s.MaxMagic * magicPct / 100;
            }
            if (s.MaxDamage < s.MinDamage) s.MaxDamage = s.MinDamage;
            return s;
        }

        /// <summary>Desgaste ao atacar/ser atingido.</summary>
        public void WearEquipment(bool attacking, IRandom rng)
        {
            foreach (var kv in Equipment)
            {
                if (kv.Key == EquipSlot.Wings || kv.Key == EquipSlot.Mount || kv.Key == EquipSlot.Pet || kv.Value.Def.IsJewel) continue; // não se desgastam
                bool isWeapon = kv.Key == EquipSlot.RightHand;
                if (isWeapon != attacking) continue;
                if (kv.Value.Durability > 0 && rng.Roll(attacking ? 4 : 2)) { kv.Value.Durability--; Inventory.Touch(); }
            }
        }
    }
}
