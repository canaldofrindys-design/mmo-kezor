using System;
using System.Collections.Generic;
using MuProto.Core.Combat;
using MuProto.Core.Items;

namespace MuProto.Core.Characters
{
    public sealed class Character : ICombatant
    {
        public const int MaxLevel = 400;
        public const int MaxResets = 50;
        public const int ResetBonusPoints = 500;
        public const int MaxStatValue = 32767;

        public int Id { get; }
        public string Name { get; }
        public CharacterClass Class { get; }
        public ClassDefinition Def => ClassDefinition.Get(Class);
        public int Level { get; private set; } = 1;
        public long Experience { get; private set; }
        public int Resets { get; private set; }
        public int FreePoints { get; private set; }
        public Attributes Stats;
        public long Zen { get; set; }
        public int Hp { get; set; }
        public int Mp { get; set; }
        public int X { get; set; } = 130;
        public int Y { get; set; } = 130;
        public Inventory Inventory { get; } = new Inventory(32);
        public Dictionary<EquipSlot, Item> Equipment { get; } = new Dictionary<EquipSlot, Item>();

        public Character(int id, string name, CharacterClass cls)
        {
            Id = id; Name = name; Class = cls;
            Stats = Def.BaseStats;
            Hp = MaxHp; Mp = MaxMp;
        }

        public bool IsDead => Hp <= 0;
        public int MaxHp => (int)(Def.BaseHp + Def.HpPerLevel * (Level - 1) + Def.HpPerVitality * (Stats.Vitality - Def.BaseStats.Vitality));
        public int MaxMp => (int)(Def.BaseMp + Def.MpPerLevel * (Level - 1) + Def.MpPerEnergy * (Stats.Energy - Def.BaseStats.Energy));

        /// <summary>Curva de experiência original: cresce ~cúbica.</summary>
        public static long ExpToNext(int level) => (long)level * level * (level + 10) * 5 + 100;

        /// <summary>Concede experiência e retorna quantos níveis foram ganhos.</summary>
        public int GainExperience(long amount)
        {
            if (amount <= 0 || Level >= MaxLevel) return 0;
            Experience += amount;
            int gained = 0;
            while (Level < MaxLevel && Experience >= ExpToNext(Level))
            {
                Experience -= ExpToNext(Level);
                Level++; gained++;
                FreePoints += Def.PointsPerLevel;
            }
            if (Level >= MaxLevel) Experience = 0;
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

        public static long ResetZenCost(int resets) => 1_000_000L * (resets + 1);

        public bool TryReset(out string error)
        {
            error = "";
            if (Level < MaxLevel) { error = $"requer nível {MaxLevel}"; return false; }
            if (Resets >= MaxResets) { error = "limite de resets"; return false; }
            long cost = ResetZenCost(Resets);
            if (Zen < cost) { error = $"zen insuficiente ({cost})"; return false; }
            Zen -= cost;
            Resets++;
            Level = 1; Experience = 0;
            Stats = Def.BaseStats;
            FreePoints = Resets * ResetBonusPoints;
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
            Inventory.Remove(inventorySlot);
            if (Equipment.TryGetValue(item.Def.Slot, out var old)) Inventory.TryAdd(old, out _);
            Equipment[item.Def.Slot] = item;
            return true;
        }

        public bool Unequip(EquipSlot slot, out string error)
        {
            error = "";
            if (!Equipment.TryGetValue(slot, out var item)) { error = "nada equipado"; return false; }
            if (!Inventory.TryAdd(item, out _)) { error = "inventário cheio"; return false; }
            Equipment.Remove(slot);
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
                CriticalChancePercent = 0,
                ExcellentChancePercent = 0,
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
            foreach (var kv in Equipment)
            {
                var it = kv.Value;
                if (!Usable(it)) continue;
                s.MinDamage += it.MinDamage; s.MaxDamage += it.MaxDamage;
                s.Defense += it.Defense;
                if (s.MaxMagic > 0 && it.MagicPowerPercent > 0)
                {
                    s.MinMagic += s.MinMagic * it.MagicPowerPercent / 100;
                    s.MaxMagic += s.MaxMagic * it.MagicPowerPercent / 100;
                }
                if (it.Luck) s.CriticalChancePercent += 5;
                if ((it.Excellent & ExcellentOption.ExcellentDamageChance) != 0) s.ExcellentChancePercent += 10;
                if ((it.Excellent & ExcellentOption.DefenseSuccessRate) != 0) s.DefenseRate += s.DefenseRate / 10;
                if ((it.Excellent & ExcellentOption.DamageReduction) != 0) s.DamageReductionPercent += 4;
            }
            if (s.MaxDamage < s.MinDamage) s.MaxDamage = s.MinDamage;
            return s;
        }

        /// <summary>Desgaste ao atacar/ser atingido.</summary>
        public void WearEquipment(bool attacking, IRandom rng)
        {
            foreach (var kv in Equipment)
            {
                bool isWeapon = kv.Key == EquipSlot.RightHand;
                if (isWeapon != attacking) continue;
                if (rng.Roll(attacking ? 4 : 2)) kv.Value.Durability = Math.Max(0, kv.Value.Durability - 1);
            }
        }
    }
}
