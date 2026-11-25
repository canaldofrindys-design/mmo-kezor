using System;
using MuProto.Core.Characters;

namespace MuProto.Core.Items
{
    public enum EquipSlot { None, RightHand, LeftHand, Helm, Armor, Pants, Gloves, Boots }
    public enum ItemKind { Sword, Staff, Bow, Shield, Helm, Armor, Pants, Gloves, Boots, Jewel }
    public enum JewelType { None, Bless, Soul, Life, Chaos }
    public enum ItemRarity { Normal, Excellent, Ancient }

    [Flags]
    public enum ExcellentOption
    {
        None = 0,
        ExcellentDamageChance = 1,   // armas: +10% chance de dano excelente
        DamagePlusLevel = 2,         // armas: +dano = nível/20
        LifeOnKill = 4,              // armas: +HP ao matar
        DefenseSuccessRate = 8,      // armaduras: +10% defense rate
        DamageReduction = 16,        // armaduras: -4% dano recebido
        MaxHpPlus = 32               // armaduras: +4% HP
    }

    public sealed class ItemDefinition
    {
        public int Id;
        public string Name = "";
        public ItemKind Kind;
        public EquipSlot Slot;
        public JewelType Jewel;
        public int MinDamage, MaxDamage, Defense, MagicPowerPercent;
        public int ReqStrength, ReqAgility, ReqEnergy, ReqLevel;
        public int BaseDurability;
        public ClassMask AllowedClasses = ClassMask.All;
        public int MaxStack = 1;
        public int DropLevel;
        public int Value = 100;
        public bool IsJewel => Jewel != JewelType.None;
        public bool IsWeapon => Kind == ItemKind.Sword || Kind == ItemKind.Staff || Kind == ItemKind.Bow;
        public bool IsUpgradable => Slot != EquipSlot.None;
    }

    public sealed class Item
    {
        public const int MaxLevel = 15;
        public const int MaxAdditionalOption = 7; // cada ponto = +4 (dano ou defesa), máx +28

        private static long _nextUid = 1;

        public long Uid { get; }
        public ItemDefinition Def { get; }
        public int Level { get; internal set; }
        public bool Luck { get; set; }
        public bool Skill { get; set; }
        public int AdditionalOption { get; internal set; }
        public ExcellentOption Excellent { get; set; }
        public int Durability { get; set; }
        public int Quantity { get; set; } = 1;

        public Item(ItemDefinition def, int level = 0)
        {
            Uid = System.Threading.Interlocked.Increment(ref _nextUid);
            Def = def;
            Level = Math.Clamp(level, 0, MaxLevel);
            Durability = MaxDurability;
        }

        public ItemRarity Rarity => Excellent != ExcellentOption.None ? ItemRarity.Excellent : ItemRarity.Normal;

        /// <summary>Bônus por nível: +3 por nível até +9; de +10 a +15 o ganho cresce (+4,+5,...,+9).</summary>
        public static int LevelBonus(int level)
        {
            int b = 3 * Math.Min(level, 9);
            for (int l = 10; l <= level; l++) b += l - 6;
            return b;
        }

        public int MaxDurability => Def.BaseDurability == 0 ? 0 : Def.BaseDurability + Math.Min(Level, 9) + Math.Max(0, Level - 9) * 2;

        private int ExcBonusPercent => Rarity == ItemRarity.Excellent ? 25 : 0;
        private int AddOptionValue => AdditionalOption * 4;
        private bool HalfStats => Def.BaseDurability > 0 && Durability > 0 && Durability * 5 < MaxDurability; // <20% durab = 50% eficiência

        public int MinDamage => Def.IsWeapon && Def.MaxDamage > 0 ? Scale(Def.MinDamage + Def.MinDamage * ExcBonusPercent / 100 + LevelBonus(Level) + AddOptionValue) : 0;
        public int MaxDamage => Def.IsWeapon && Def.MaxDamage > 0 ? Scale(Def.MaxDamage + Def.MaxDamage * ExcBonusPercent / 100 + LevelBonus(Level) + AddOptionValue) : 0;
        public int Defense => Def.Defense > 0 ? Scale(Def.Defense + Def.Defense * ExcBonusPercent / 100 + LevelBonus(Level) + AddOptionValue) : 0;
        public int MagicPowerPercent => Def.MagicPowerPercent > 0 ? Scale(Def.MagicPowerPercent + Level * 3 + AddOptionValue) : 0;
        private int Scale(int v) => HalfStats ? v / 2 : v;

        public int RequiredStrength => Def.ReqStrength == 0 ? 0 : Def.ReqStrength + Def.ReqStrength * Level * 3 / 100;
        public int RequiredAgility => Def.ReqAgility == 0 ? 0 : Def.ReqAgility + Def.ReqAgility * Level * 3 / 100;

        public string DisplayName
        {
            get
            {
                if (Def.IsJewel) return Quantity > 1 ? $"{Def.Name} x{Quantity}" : Def.Name;
                var s = (Rarity == ItemRarity.Excellent ? "Excelente " : "") + Def.Name + (Level > 0 ? $" +{Level}" : "");
                if (Skill) s += " [Skill]";
                if (Luck) s += " [Luck]";
                if (AdditionalOption > 0) s += $" [+{AddOptionValue}]";
                if (Def.BaseDurability > 0) s += $" ({Durability}/{MaxDurability})";
                return s;
            }
        }

        public override string ToString() => DisplayName;
    }
}
