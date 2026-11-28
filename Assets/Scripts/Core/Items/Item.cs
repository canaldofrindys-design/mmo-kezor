using System;
using Kezor.Core.Characters;

namespace Kezor.Core.Items
{
    /// <summary>Slots de equipamento. Os novos vão sempre no fim (o número vai no protocolo).</summary>
    public enum EquipSlot { None, RightHand, LeftHand, Helm, Armor, Pants, Gloves, Boots, Wings, Mount, Pet, Amulet, Ring1, Ring2 }
    public enum ItemKind { Sword, Axe, Staff, Bow, Shield, Helm, Armor, Pants, Gloves, Boots, Rune, Potion, Wings, Mount, Pet, Material, Lance, Gauntlet, Tome, Ring, Amulet }
    /// <summary>Habilidade do mascote (slot Pet), executada pelo servidor.</summary>
    public enum PetType { None, Collector, Guardian }
    /// <summary>Conjunto de armadura: com as 5 peças (elmo, armadura, calças, luvas e botas) ganha o bônus de conjunto.</summary>
    public enum ArmorSet { None, Leather, Scale, Veil, RunePlate, ArcaneWeave, Bastion, EchoVeil, EmberHide, KnightPlate, TemplarPlate, SovereignPlate, ApprenticeRobe, SageRobe, ArchmageRobe, ArcherGarb, ScoutLeather, SentinelLeather, FencerGarb, DuelistPlate, SilverStagPlate, CrusaderPlate, MarshalPlate, BlackBannerPlate, MysticGarb, ProphetessRobe, QueenGown, DiscipleGarb, ImperialBrigandine, CrimsonSamurai }
    /// <summary>Runas de aprimoramento de Kezor.</summary>
    public enum RuneType { None, Dawn, Eclipse, Vital, Vortex }
    public enum PotionType { None, Health, Mana }
    public enum ItemRarity { Normal, Ascended }

    /// <summary>Opções especiais de itens Ascendentes (as 6 + 6 opções excelentes do MU).</summary>
    [Flags]
    public enum AscendedOption
    {
        None = 0,
        SurgeChance = 1,       // armas: +10% de chance de Golpe Ascendente (dano máx × 1,2)
        DamagePerLevel = 2,    // armas: +dano = nível/20
        LifeOnKill = 4,        // armas: recupera vida/8 ao matar
        DefenseRate = 8,       // armaduras: +10% taxa de defesa
        DamageReduction = 16,  // armaduras: -4% dano recebido
        MaxHpPlus = 32,        // armaduras: +4% HP máximo
        DamagePercent = 64,    // armas: +2% de dano
        AttackSpeed = 128,     // armas: +7 de velocidade de ataque
        ManaOnKill = 256,      // armas: recupera mana/8 ao matar
        MaxMpPlus = 512,       // armaduras: +4% mana máxima
        Reflect = 1024,        // armaduras: reflete 5% do dano recebido
        ZenBonus = 2048        // armaduras: +40% de ouro dos monstros
    }

    public sealed class ItemDefinition
    {
        public int Id;
        public string Name = "";
        public ItemKind Kind;
        public EquipSlot Slot;
        public RuneType Rune;
        public PotionType Potion;
        public int PotionAmount;
        /// <summary>Tamanho da poção no MU (1 pequena, 2 média, 3 grande): recupera 10%·tamanho do máximo + (tamanho+1)·50 − nível.</summary>
        public int PotionTier;
        /// <summary>Quanto a poção recupera para um herói com este máximo e nível (regra do MU; sem tamanho = valor fixo).</summary>
        public int PotionRecover(int maximum, int level) => PotionTier <= 0 ? PotionAmount
            : maximum * PotionTier * 10 / 100 + Math.Max(0, (PotionTier + 1) * 50 - level);
        public string PotionDescription => PotionTier <= 0 ? $"{PotionAmount} de {(Potion == PotionType.Health ? "vida" : "mana")}"
            : $"{PotionTier * 10}% da {(Potion == PotionType.Health ? "vida" : "mana")} máxima + {(PotionTier + 1) * 50} − nível";
        public int MinDamage, MaxDamage, Defense, MagicPowerPercent;
        public int ReqStrength, ReqAgility, ReqEnergy, ReqLevel;
        public int BaseDurability;
        public ClassMask AllowedClasses = ClassMask.All;
        public int MaxStack = 1;
        /// <summary>Nível mínimo de monstro para dropar (999 = não dropa).</summary>
        public int DropLevel = 999;
        /// <summary>Preço na loja (0 = não vendido).</summary>
        public int ShopPrice;
        public int Value = 100;
        /// <summary>Faixa visual/poder: 0 comum, 1 raro, 2 épico. Nas asas é o tier (1..3).</summary>
        public int Tier;
        /// <summary>Arma épica: rastro e partículas próprios no cliente.</summary>
        public bool Epic;
        /// <summary>Asas: % de dano a mais e % de dano absorvido (cada nível de runa soma +1% / +0,5%).</summary>
        public int WingDamagePercent, WingAbsorbPercent;
        /// <summary>Montaria: % de velocidade a mais quando montado.</summary>
        public int MountSpeedPercent;
        public PetType Pet;
        public ArmorSet Set;
        /// <summary>Joias (amuleto e anéis): só atributos, sem modelo. Vida e mana a mais, % de resistência e % de dano.</summary>
        public int JewelHp, JewelMp, JewelResistPercent, JewelDamagePercent;
        /// <summary>Vinculado ao personagem: não vai para o baú nem pode ser vendido a outros (itens de missão).</summary>
        public bool Bound;
        public bool IsWings => Slot == EquipSlot.Wings;
        public bool IsMount => Slot == EquipSlot.Mount;
        public bool IsPet => Slot == EquipSlot.Pet;
        /// <summary>Anel (vai no Ring1 ou Ring2) ou amuleto.</summary>
        public bool IsJewel => Kind == ItemKind.Ring || Kind == ItemKind.Amulet;
        public bool IsRune => Rune != RuneType.None;
        public bool IsPotion => Potion != PotionType.None;
        public bool IsWeapon => Kind == ItemKind.Sword || Kind == ItemKind.Axe || Kind == ItemKind.Staff || Kind == ItemKind.Bow
                                || Kind == ItemKind.Lance || Kind == ItemKind.Gauntlet || Kind == ItemKind.Tome;
        /// <summary>Aceita runas e forja: armas, armaduras e asas (montarias e mascotes não).</summary>
        public bool IsUpgradable => Slot != EquipSlot.None && Slot != EquipSlot.Mount && Slot != EquipSlot.Pet && !IsJewel;
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
        public AscendedOption Ascended { get; set; }
        public int Durability { get; set; }
        public int Quantity { get; set; } = 1;

        public Item(ItemDefinition def, int level = 0)
        {
            Uid = System.Threading.Interlocked.Increment(ref _nextUid);
            Def = def;
            Level = Math.Clamp(level, 0, MaxLevel);
            Durability = MaxDurability;
        }

        /// <summary>Reconstrói um item a partir dos campos do protocolo (usado pelo cliente).</summary>
        public static Item Restore(ItemDefinition def, int level, int qty, bool luck, bool skill, int option, AscendedOption asc, int durability)
            => new Item(def, level) { Quantity = qty, Luck = luck, Skill = skill, AdditionalOption = Math.Clamp(option, 0, MaxAdditionalOption), Ascended = asc, Durability = durability };

        public ItemRarity Rarity => Ascended != AscendedOption.None ? ItemRarity.Ascended : ItemRarity.Normal;

        /// <summary>Bônus por nível: +3 por nível até +9; de +10 a +15 o ganho cresce (+4,+5,...,+9).</summary>
        public static int LevelBonus(int level)
        {
            int b = 3 * Math.Min(level, 9);
            for (int l = 10; l <= level; l++) b += l - 6;
            return b;
        }

        public int MaxDurability => Def.BaseDurability == 0 ? 0 : Def.BaseDurability + Math.Min(Level, 9) + Math.Max(0, Level - 9) * 2;

        private int AscBonusPercent => Rarity == ItemRarity.Ascended ? 25 : 0;
        private int AddOptionValue => AdditionalOption * 4;
        private bool HalfStats => Def.BaseDurability > 0 && Durability > 0 && Durability * 5 < MaxDurability; // <20% durab = 50% eficiência

        public int MinDamage => Def.IsWeapon && Def.MaxDamage > 0 ? Scale(Def.MinDamage + Def.MinDamage * AscBonusPercent / 100 + LevelBonus(Level) + AddOptionValue) : 0;
        public int MaxDamage => Def.IsWeapon && Def.MaxDamage > 0 ? Scale(Def.MaxDamage + Def.MaxDamage * AscBonusPercent / 100 + LevelBonus(Level) + AddOptionValue) : 0;
        public int Defense => Def.Defense > 0 ? Scale(Def.Defense + Def.Defense * AscBonusPercent / 100 + LevelBonus(Level) + AddOptionValue) : 0;
        public int MagicPowerPercent => Def.MagicPowerPercent > 0 ? Scale(Def.MagicPowerPercent + Level * 3 + AddOptionValue) : 0;
        private int Scale(int v) => HalfStats ? v / 2 : v;

        public int RequiredStrength => Def.ReqStrength == 0 ? 0 : Def.ReqStrength + Def.ReqStrength * Level * 3 / 100;
        public int RequiredAgility => Def.ReqAgility == 0 ? 0 : Def.ReqAgility + Def.ReqAgility * Level * 3 / 100;

        public const int GuardianPetDefense = 15, GuardianPetReduction = 5;
        /// <summary>Opções excelentes possíveis para armas e para armaduras (sorteio do drop).</summary>
        public static readonly AscendedOption[] WeaponExcellent = { AscendedOption.SurgeChance, AscendedOption.DamagePerLevel, AscendedOption.DamagePercent, AscendedOption.AttackSpeed, AscendedOption.LifeOnKill, AscendedOption.ManaOnKill };
        public static readonly AscendedOption[] ArmorExcellent = { AscendedOption.MaxHpPlus, AscendedOption.MaxMpPlus, AscendedOption.DamageReduction, AscendedOption.Reflect, AscendedOption.DefenseRate, AscendedOption.ZenBonus };

        /// <summary>Asas: bônus de dano e absorção com o nível de runa.</summary>
        public int WingDamage => Def.IsWings ? Def.WingDamagePercent + Level : 0;
        public int WingAbsorb => Def.IsWings ? Def.WingAbsorbPercent + Level / 2 : 0;

        public string DisplayName
        {
            get
            {
                if (!Def.IsUpgradable) return Quantity > 1 ? $"{Def.Name} x{Quantity}" : Def.Name;
                var s = (Rarity == ItemRarity.Ascended ? "Ascendente " : "") + Def.Name + (Level > 0 ? $" +{Level}" : "");
                if (Skill) s += " [Skill]";
                if (Luck) s += " [Sorte]";
                if (AdditionalOption > 0) s += $" [+{AddOptionValue}]";
                return s;
            }
        }

        /// <summary>Descrição detalhada (tooltip).</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder(DisplayName);
            if (Def.IsWeapon) sb.Append($"\nDano: {MinDamage}~{MaxDamage}");
            if (MagicPowerPercent > 0) sb.Append($"\nPoder mágico: +{MagicPowerPercent}%");
            if (Defense > 0) sb.Append($"\nDefesa: {Defense}");
            if (Def.BaseDurability > 0) sb.Append($"\nDurabilidade: {Durability}/{MaxDurability}");
            if (RequiredStrength > 0) sb.Append($"\nForça req.: {RequiredStrength}");
            if (RequiredAgility > 0) sb.Append($"\nAgilidade req.: {RequiredAgility}");
            if (Def.ReqEnergy > 0) sb.Append($"\nEnergia req.: {Def.ReqEnergy}");
            if (Luck) sb.Append("\nSorte: +5% crítico, +25% sucesso com Runa do Eclipse");
            foreach (AscendedOption o in Enum.GetValues(typeof(AscendedOption)))
                if (o != AscendedOption.None && (Ascended & o) != 0) sb.Append("\n* ").Append(AscendedText(o));
            if (Def.IsPotion) sb.Append($"\nRecupera {Def.PotionDescription}");
            if (Def.IsRune) sb.Append("\n").Append(RuneService.Describe(Def.Rune));
            if (Def.IsWings) sb.Append($"\nDano +{WingDamage}%  ·  Absorve {WingAbsorb}%");
            if (Def.IsMount) sb.Append($"\nVelocidade montado: +{Def.MountSpeedPercent}% (tecla X)");
            if (Def.Pet == PetType.Collector) sb.Append("\nRecolhe seus itens num raio de 6 m e dá +10% de ouro");
            if (Def.Pet == PetType.Guardian) sb.Append($"\nDefesa +{GuardianPetDefense} e -{GuardianPetReduction}% de dano recebido");
            if (Def.Epic) sb.Append("\nArma épica");
            if (Def.Set != ArmorSet.None) sb.Append("\n").Append(ArmorSets.Describe(Def.Set));
            return sb.ToString();
        }

        public static string AscendedText(AscendedOption o) => o switch
        {
            AscendedOption.SurgeChance => "+10% chance de Golpe Ascendente",
            AscendedOption.DamagePerLevel => "Dano + nível/20",
            AscendedOption.LifeOnKill => "Recupera HP ao abater",
            AscendedOption.DefenseRate => "+10% taxa de defesa",
            AscendedOption.DamageReduction => "-4% dano recebido",
            AscendedOption.MaxHpPlus => "+4% HP máximo",
            AscendedOption.DamagePercent => "Dano +2%",
            AscendedOption.AttackSpeed => "Velocidade de ataque +7",
            AscendedOption.ManaOnKill => "Recupera mana ao abater",
            AscendedOption.MaxMpPlus => "+4% mana máxima",
            AscendedOption.Reflect => "Reflete 5% do dano",
            AscendedOption.ZenBonus => "+40% de ouro dos monstros",
            _ => o.ToString()
        };

        public override string ToString() => DisplayName;
    }
}
