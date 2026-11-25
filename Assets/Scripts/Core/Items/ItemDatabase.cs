using System.Collections.Generic;
using Kezor.Core.Characters;

namespace Kezor.Core.Items
{
    /// <summary>Catálogo original de itens de Kezor.</summary>
    public static class ItemDatabase
    {
        private static readonly Dictionary<int, ItemDefinition> _byId = new Dictionary<int, ItemDefinition>();
        private static readonly List<ItemDefinition> _all = new List<ItemDefinition>();
        public static IReadOnlyList<ItemDefinition> All => _all;

        private const ClassMask W = ClassMask.Warden, A = ClassMask.Arcanist, R = ClassMask.Ranger;
        private const ClassMask L = ClassMask.Bladeweaver, C = ClassMask.Commander, I = ClassMask.Invoker, P = ClassMask.EmberFist;

        // Runas
        public static readonly ItemDefinition RuneOfDawn = Add(new ItemDefinition { Id = 1000, Name = "Runa da Aurora", Kind = ItemKind.Rune, Rune = RuneType.Dawn, Value = 9_000_000 });
        public static readonly ItemDefinition RuneOfEclipse = Add(new ItemDefinition { Id = 1001, Name = "Runa do Eclipse", Kind = ItemKind.Rune, Rune = RuneType.Eclipse, Value = 6_000_000 });
        public static readonly ItemDefinition VitalRune = Add(new ItemDefinition { Id = 1002, Name = "Runa Vital", Kind = ItemKind.Rune, Rune = RuneType.Vital, Value = 4_500_000 });
        public static readonly ItemDefinition VortexCrystal = Add(new ItemDefinition { Id = 1003, Name = "Cristal do Vórtice", Kind = ItemKind.Rune, Rune = RuneType.Vortex, Value = 800_000 });

        // Poções
        public static readonly ItemDefinition HealthPotion = Add(new ItemDefinition { Id = 2000, Name = "Poção Rubra", Kind = ItemKind.Potion, Potion = PotionType.Health, PotionAmount = 60, MaxStack = 50, ShopPrice = 40, DropLevel = 1, Value = 10 });
        public static readonly ItemDefinition GreaterHealthPotion = Add(new ItemDefinition { Id = 2001, Name = "Poção Rubra Maior", Kind = ItemKind.Potion, Potion = PotionType.Health, PotionAmount = 250, MaxStack = 50, ShopPrice = 300, DropLevel = 20, Value = 75 });
        public static readonly ItemDefinition ManaPotion = Add(new ItemDefinition { Id = 2002, Name = "Poção Cerúlea", Kind = ItemKind.Potion, Potion = PotionType.Mana, PotionAmount = 50, MaxStack = 50, ShopPrice = 50, DropLevel = 1, Value = 12 });

        // Armas — Guardião
        public static readonly ItemDefinition IronBlade = Add(new ItemDefinition { Id = 1, Name = "Lâmina de Ferro", Kind = ItemKind.Sword, Slot = EquipSlot.RightHand, MinDamage = 6, MaxDamage = 11, ReqStrength = 22, BaseDurability = 22, AllowedClasses = W, DropLevel = 1, ShopPrice = 800 });
        public static readonly ItemDefinition WarAxe = Add(new ItemDefinition { Id = 3, Name = "Machado de Guerra", Kind = ItemKind.Axe, Slot = EquipSlot.RightHand, MinDamage = 15, MaxDamage = 23, ReqStrength = 50, BaseDurability = 32, AllowedClasses = W, DropLevel = 15 });
        public static readonly ItemDefinition StormBlade = Add(new ItemDefinition { Id = 2, Name = "Montante da Tempestade", Kind = ItemKind.Sword, Slot = EquipSlot.RightHand, MinDamage = 28, MaxDamage = 40, ReqStrength = 90, BaseDurability = 40, AllowedClasses = W, DropLevel = 30 });
        // Armas — Arcanista
        public static readonly ItemDefinition ApprenticeStaff = Add(new ItemDefinition { Id = 10, Name = "Cajado do Aprendiz", Kind = ItemKind.Staff, Slot = EquipSlot.RightHand, MinDamage = 3, MaxDamage = 6, MagicPowerPercent = 8, ReqStrength = 12, ReqEnergy = 20, BaseDurability = 20, AllowedClasses = A, DropLevel = 1, ShopPrice = 800 });
        public static readonly ItemDefinition AmberWand = Add(new ItemDefinition { Id = 12, Name = "Varinha de Âmbar", Kind = ItemKind.Staff, Slot = EquipSlot.RightHand, MinDamage = 6, MaxDamage = 10, MagicPowerPercent = 18, ReqStrength = 15, ReqEnergy = 50, BaseDurability = 28, AllowedClasses = A, DropLevel = 15 });
        public static readonly ItemDefinition OracleStaff = Add(new ItemDefinition { Id = 11, Name = "Cajado do Oráculo", Kind = ItemKind.Staff, Slot = EquipSlot.RightHand, MinDamage = 10, MaxDamage = 16, MagicPowerPercent = 30, ReqStrength = 20, ReqEnergy = 80, BaseDurability = 36, AllowedClasses = A, DropLevel = 30 });
        // Armas — Patrulheiro
        public static readonly ItemDefinition HuntingBow = Add(new ItemDefinition { Id = 20, Name = "Arco de Caça", Kind = ItemKind.Bow, Slot = EquipSlot.RightHand, MinDamage = 5, MaxDamage = 10, ReqAgility = 22, BaseDurability = 20, AllowedClasses = R, DropLevel = 1, ShopPrice = 800 });
        public static readonly ItemDefinition CompositeBow = Add(new ItemDefinition { Id = 21, Name = "Arco Composto", Kind = ItemKind.Bow, Slot = EquipSlot.RightHand, MinDamage = 12, MaxDamage = 19, ReqAgility = 55, BaseDurability = 30, AllowedClasses = R, DropLevel = 15 });
        public static readonly ItemDefinition NorthwindBow = Add(new ItemDefinition { Id = 22, Name = "Arco do Vento Norte", Kind = ItemKind.Bow, Slot = EquipSlot.RightHand, MinDamage = 24, MaxDamage = 35, ReqAgility = 95, BaseDurability = 38, AllowedClasses = R, DropLevel = 30 });
        // Arma especial (Forja)
        public static readonly ItemDefinition VortexBlade = Add(new ItemDefinition { Id = 30, Name = "Lâmina do Vórtice", Kind = ItemKind.Sword, Slot = EquipSlot.RightHand, MinDamage = 45, MaxDamage = 62, ReqStrength = 110, BaseDurability = 60, AllowedClasses = ClassMask.All });

        // Escudos
        public static readonly ItemDefinition BronzeShield = Add(new ItemDefinition { Id = 120, Name = "Escudo de Bronze", Kind = ItemKind.Shield, Slot = EquipSlot.LeftHand, Defense = 6, ReqStrength = 30, BaseDurability = 30, AllowedClasses = W | A, DropLevel = 5 });
        public static readonly ItemDefinition WardenShield = Add(new ItemDefinition { Id = 121, Name = "Pavês do Juramento", Kind = ItemKind.Shield, Slot = EquipSlot.LeftHand, Defense = 16, ReqStrength = 70, BaseDurability = 40, AllowedClasses = W, DropLevel = 25 });

        // Conjunto de Couro (todas as classes)
        public static readonly ItemDefinition LeatherHelm = Add(new ItemDefinition { Id = 100, Set = ArmorSet.Leather, Name = "Elmo de Couro", Kind = ItemKind.Helm, Slot = EquipSlot.Helm, Defense = 4, ReqStrength = 20, BaseDurability = 25, DropLevel = 1, ShopPrice = 300 });
        public static readonly ItemDefinition LeatherArmor = Add(new ItemDefinition { Id = 101, Set = ArmorSet.Leather, Name = "Armadura de Couro", Kind = ItemKind.Armor, Slot = EquipSlot.Armor, Defense = 8, ReqStrength = 20, BaseDurability = 30, DropLevel = 1, ShopPrice = 500 });
        public static readonly ItemDefinition LeatherPants = Add(new ItemDefinition { Id = 102, Set = ArmorSet.Leather, Name = "Calças de Couro", Kind = ItemKind.Pants, Slot = EquipSlot.Pants, Defense = 6, ReqStrength = 20, BaseDurability = 28, DropLevel = 1, ShopPrice = 400 });
        public static readonly ItemDefinition LeatherGloves = Add(new ItemDefinition { Id = 103, Set = ArmorSet.Leather, Name = "Luvas de Couro", Kind = ItemKind.Gloves, Slot = EquipSlot.Gloves, Defense = 2, ReqStrength = 18, BaseDurability = 20, DropLevel = 1, ShopPrice = 250 });
        public static readonly ItemDefinition LeatherBoots = Add(new ItemDefinition { Id = 104, Set = ArmorSet.Leather, Name = "Botas de Couro", Kind = ItemKind.Boots, Slot = EquipSlot.Boots, Defense = 3, ReqStrength = 18, BaseDurability = 22, DropLevel = 1, ShopPrice = 250 });
        // Conjunto de Escamas (Guardião, Patrulheiro)
        public static readonly ItemDefinition ScaleHelm = Add(new ItemDefinition { Id = 110, Set = ArmorSet.Scale, Name = "Elmo de Escamas", Kind = ItemKind.Helm, Slot = EquipSlot.Helm, Defense = 9, ReqStrength = 45, BaseDurability = 32, AllowedClasses = W | R, DropLevel = 12 });
        public static readonly ItemDefinition ScaleArmor = Add(new ItemDefinition { Id = 111, Set = ArmorSet.Scale, Name = "Cota de Escamas", Kind = ItemKind.Armor, Slot = EquipSlot.Armor, Defense = 16, ReqStrength = 50, BaseDurability = 38, AllowedClasses = W | R, DropLevel = 12 });
        public static readonly ItemDefinition ScalePants = Add(new ItemDefinition { Id = 112, Set = ArmorSet.Scale, Name = "Grevas de Escamas", Kind = ItemKind.Pants, Slot = EquipSlot.Pants, Defense = 12, ReqStrength = 45, BaseDurability = 34, AllowedClasses = W | R, DropLevel = 12 });
        public static readonly ItemDefinition ScaleGloves = Add(new ItemDefinition { Id = 113, Set = ArmorSet.Scale, Name = "Manoplas de Escamas", Kind = ItemKind.Gloves, Slot = EquipSlot.Gloves, Defense = 6, ReqStrength = 40, BaseDurability = 28, AllowedClasses = W | R, DropLevel = 12 });
        public static readonly ItemDefinition ScaleBoots = Add(new ItemDefinition { Id = 114, Set = ArmorSet.Scale, Name = "Botas de Escamas", Kind = ItemKind.Boots, Slot = EquipSlot.Boots, Defense = 7, ReqStrength = 40, BaseDurability = 30, AllowedClasses = W | R, DropLevel = 12 });
        // Conjunto do Véu (Arcanista)
        public static readonly ItemDefinition VeilHood = Add(new ItemDefinition { Id = 130, Set = ArmorSet.Veil, Name = "Capuz do Véu", Kind = ItemKind.Helm, Slot = EquipSlot.Helm, Defense = 6, ReqStrength = 18, ReqEnergy = 45, BaseDurability = 28, AllowedClasses = A, DropLevel = 10 });
        public static readonly ItemDefinition VeilRobe = Add(new ItemDefinition { Id = 131, Set = ArmorSet.Veil, Name = "Túnica do Véu", Kind = ItemKind.Armor, Slot = EquipSlot.Armor, Defense = 11, ReqStrength = 20, ReqEnergy = 50, BaseDurability = 34, AllowedClasses = A, DropLevel = 10 });
        public static readonly ItemDefinition VeilPants = Add(new ItemDefinition { Id = 132, Set = ArmorSet.Veil, Name = "Calças do Véu", Kind = ItemKind.Pants, Slot = EquipSlot.Pants, Defense = 8, ReqStrength = 18, ReqEnergy = 45, BaseDurability = 30, AllowedClasses = A, DropLevel = 10 });
        public static readonly ItemDefinition VeilGloves = Add(new ItemDefinition { Id = 133, Set = ArmorSet.Veil, Name = "Luvas do Véu", Kind = ItemKind.Gloves, Slot = EquipSlot.Gloves, Defense = 4, ReqStrength = 16, ReqEnergy = 40, BaseDurability = 24, AllowedClasses = A, DropLevel = 10 });
        public static readonly ItemDefinition VeilBoots = Add(new ItemDefinition { Id = 134, Set = ArmorSet.Veil, Name = "Sandálias do Véu", Kind = ItemKind.Boots, Slot = EquipSlot.Boots, Defense = 5, ReqStrength = 18, ReqEnergy = 40, BaseDurability = 26, AllowedClasses = A, DropLevel = 10 });
        // Placas Rúnicas (Guardião)
        public static readonly ItemDefinition RunePlateHelm = Add(new ItemDefinition { Id = 140, Set = ArmorSet.RunePlate, Name = "Elmo de Placas Rúnicas", Kind = ItemKind.Helm, Slot = EquipSlot.Helm, Defense = 16, ReqStrength = 85, BaseDurability = 42, AllowedClasses = W, DropLevel = 28 });
        public static readonly ItemDefinition RunePlateArmor = Add(new ItemDefinition { Id = 141, Set = ArmorSet.RunePlate, Name = "Peitoral de Placas Rúnicas", Kind = ItemKind.Armor, Slot = EquipSlot.Armor, Defense = 26, ReqStrength = 95, BaseDurability = 48, AllowedClasses = W, DropLevel = 28 });
        public static readonly ItemDefinition RunePlatePants = Add(new ItemDefinition { Id = 142, Set = ArmorSet.RunePlate, Name = "Grevas de Placas Rúnicas", Kind = ItemKind.Pants, Slot = EquipSlot.Pants, Defense = 22, ReqStrength = 90, BaseDurability = 46, AllowedClasses = W, DropLevel = 28 });
        public static readonly ItemDefinition RunePlateGloves = Add(new ItemDefinition { Id = 143, Set = ArmorSet.RunePlate, Name = "Manoplas de Placas Rúnicas", Kind = ItemKind.Gloves, Slot = EquipSlot.Gloves, Defense = 11, ReqStrength = 80, BaseDurability = 40, AllowedClasses = W, DropLevel = 28 });
        public static readonly ItemDefinition RunePlateBoots = Add(new ItemDefinition { Id = 144, Set = ArmorSet.RunePlate, Name = "Botas de Placas Rúnicas", Kind = ItemKind.Boots, Slot = EquipSlot.Boots, Defense = 13, ReqStrength = 80, BaseDurability = 42, AllowedClasses = W, DropLevel = 28 });

        // Armas épicas (1 por classe): caem do Golem de Basalto e do Arauto das Cinzas; rastro e partículas próprios.
        public static readonly ItemDefinition CrimsonCleaver = Add(new ItemDefinition { Id = 40, Name = "Fendente Rubro", Kind = ItemKind.Sword, Slot = EquipSlot.RightHand, MinDamage = 52, MaxDamage = 70, ReqStrength = 120, BaseDurability = 64, AllowedClasses = W, DropLevel = 34, Tier = 2, Epic = true });
        public static readonly ItemDefinition VeiledDawnScepter = Add(new ItemDefinition { Id = 41, Name = "Cetro da Aurora Velada", Kind = ItemKind.Staff, Slot = EquipSlot.RightHand, MinDamage = 16, MaxDamage = 24, MagicPowerPercent = 45, ReqStrength = 25, ReqEnergy = 120, BaseDurability = 56, AllowedClasses = A, DropLevel = 34, Tier = 2, Epic = true });
        public static readonly ItemDefinition EmberWhisperBow = Add(new ItemDefinition { Id = 42, Name = "Arco Sussurro de Brasa", Kind = ItemKind.Bow, Slot = EquipSlot.RightHand, MinDamage = 36, MaxDamage = 50, ReqAgility = 125, BaseDurability = 56, AllowedClasses = R, DropLevel = 34, Tier = 2, Epic = true });

        // Material da forja de asas (cai de monstros nível 14+; o Arauto deixa 3)
        public static readonly ItemDefinition EmberFeather = Add(new ItemDefinition { Id = 1010, Name = "Pena de Brasa", Kind = ItemKind.Material, MaxStack = 50, Value = 50_000 });

        // Asas (criadas na Forja Arcana): bônus de dano e absorção; aceitam runas como os equipamentos.
        public static readonly ItemDefinition AshWings = Add(new ItemDefinition { Id = 300, Name = "Asas de Cinza", Kind = ItemKind.Wings, Slot = EquipSlot.Wings, Defense = 8, WingDamagePercent = 12, WingAbsorbPercent = 10, ReqLevel = 40, BaseDurability = 200, Tier = 1 });
        public static readonly ItemDefinition DuskWings = Add(new ItemDefinition { Id = 301, Name = "Asas do Crepúsculo", Kind = ItemKind.Wings, Slot = EquipSlot.Wings, Defense = 16, WingDamagePercent = 20, WingAbsorbPercent = 18, ReqLevel = 80, BaseDurability = 220, Tier = 2 });
        public static readonly ItemDefinition BrasalumeWings = Add(new ItemDefinition { Id = 302, Name = "Asas de Brasalume", Kind = ItemKind.Wings, Slot = EquipSlot.Wings, Defense = 28, WingDamagePercent = 32, WingAbsorbPercent = 26, ReqLevel = 150, BaseDurability = 250, Tier = 3 });

        // Montarias (loja da Mercadora Tália; tecla X monta/desmonta)
        public static readonly ItemDefinition BasaltLizard = Add(new ItemDefinition { Id = 400, Name = "Lagarto de Basalto", Kind = ItemKind.Mount, Slot = EquipSlot.Mount, MountSpeedPercent = 35, ReqLevel = 20, BaseDurability = 100, ShopPrice = 1_500_000, Value = 1_500_000, Tier = 1 });
        public static readonly ItemDefinition EmberWolf = Add(new ItemDefinition { Id = 401, Name = "Lobo de Brasas", Kind = ItemKind.Mount, Slot = EquipSlot.Mount, MountSpeedPercent = 60, ReqLevel = 50, BaseDurability = 100, ShopPrice = 6_000_000, Value = 6_000_000, Tier = 2 });

        // Mascotes (loja da Mercadora Tália; seguem o herói)
        public static readonly ItemDefinition AshImp = Add(new ItemDefinition { Id = 500, Name = "Diabrete de Cinza", Kind = ItemKind.Pet, Slot = EquipSlot.Pet, Pet = PetType.Collector, ReqLevel = 10, BaseDurability = 100, ShopPrice = 300_000, Value = 300_000, Tier = 1 });
        public static readonly ItemDefinition GuardianWisp = Add(new ItemDefinition { Id = 501, Name = "Lume Guardião", Kind = ItemKind.Pet, Slot = EquipSlot.Pet, Pet = PetType.Guardian, ReqLevel = 25, BaseDurability = 100, ShopPrice = 800_000, Value = 800_000, Tier = 2 });

        // Joias (amuleto e dois anéis): só atributos, 1×1, não se desgastam nem aceitam runas. Anéis vão no Ring1/Ring2.
        public static readonly ItemDefinition BasaltRing = Add(new ItemDefinition { Id = 600, Name = "Anel de Basalto", Kind = ItemKind.Ring, Slot = EquipSlot.Ring1, JewelResistPercent = 4, ReqLevel = 15, BaseDurability = 100, DropLevel = 12, ShopPrice = 120_000, Value = 120_000, Tier = 1 });
        public static readonly ItemDefinition HeartbeatRing = Add(new ItemDefinition { Id = 601, Name = "Anel do Pulso Vivo", Kind = ItemKind.Ring, Slot = EquipSlot.Ring1, JewelHp = 60, ReqLevel = 10, BaseDurability = 100, DropLevel = 8, ShopPrice = 90_000, Value = 90_000, Tier = 1 });
        public static readonly ItemDefinition TideRing = Add(new ItemDefinition { Id = 602, Name = "Anel da Maré Arcana", Kind = ItemKind.Ring, Slot = EquipSlot.Ring1, JewelMp = 50, ReqLevel = 10, BaseDurability = 100, DropLevel = 8, ShopPrice = 90_000, Value = 90_000, Tier = 1 });
        public static readonly ItemDefinition KeenEmberRing = Add(new ItemDefinition { Id = 603, Name = "Anel da Brasa Afiada", Kind = ItemKind.Ring, Slot = EquipSlot.Ring1, JewelDamagePercent = 5, ReqLevel = 30, BaseDurability = 100, DropLevel = 28, Value = 400_000, Tier = 2 });
        public static readonly ItemDefinition LumeAmulet = Add(new ItemDefinition { Id = 604, Name = "Amuleto do Lume", Kind = ItemKind.Amulet, Slot = EquipSlot.Amulet, JewelHp = 120, JewelResistPercent = 3, ReqLevel = 20, BaseDurability = 100, DropLevel = 18, ShopPrice = 350_000, Value = 350_000, Tier = 1 });
        public static readonly ItemDefinition RunicAshAmulet = Add(new ItemDefinition { Id = 605, Name = "Amuleto da Cinza Rúnica", Kind = ItemKind.Amulet, Slot = EquipSlot.Amulet, JewelMp = 80, JewelDamagePercent = 6, ReqLevel = 40, BaseDurability = 100, DropLevel = 36, Value = 900_000, Tier = 2 });

        // ---------------------------------------------------------------- classes novas
        // Lâmina Arcana: espadas rúnicas (força + energia)
        public static readonly ItemDefinition RuneShortblade = Add(new ItemDefinition { Id = 50, Name = "Lâmina Rúnica Curta", Kind = ItemKind.Sword, Slot = EquipSlot.RightHand, MinDamage = 6, MaxDamage = 10, MagicPowerPercent = 4, ReqStrength = 20, ReqEnergy = 15, BaseDurability = 22, AllowedClasses = L, DropLevel = 1, ShopPrice = 800 });
        public static readonly ItemDefinition DuskweaveSaber = Add(new ItemDefinition { Id = 51, Name = "Sabre do Crepúsculo Arcano", Kind = ItemKind.Sword, Slot = EquipSlot.RightHand, MinDamage = 13, MaxDamage = 20, MagicPowerPercent = 10, ReqStrength = 45, ReqEnergy = 30, BaseDurability = 32, AllowedClasses = L, DropLevel = 15, Tier = 1 });
        public static readonly ItemDefinition PrismTideSword = Add(new ItemDefinition { Id = 52, Name = "Espada da Maré Prismática", Kind = ItemKind.Sword, Slot = EquipSlot.RightHand, MinDamage = 25, MaxDamage = 36, MagicPowerPercent = 18, ReqStrength = 80, ReqEnergy = 50, BaseDurability = 40, AllowedClasses = L, DropLevel = 30, Tier = 2 });
        public static readonly ItemDefinition SunderedAstralBlade = Add(new ItemDefinition { Id = 43, Name = "Lâmina Astral Partida", Kind = ItemKind.Sword, Slot = EquipSlot.RightHand, MinDamage = 46, MaxDamage = 62, MagicPowerPercent = 28, ReqStrength = 105, ReqEnergy = 80, BaseDurability = 60, AllowedClasses = L, DropLevel = 34, Tier = 2, Epic = true });
        // Comandante do Estandarte: lanças
        public static readonly ItemDefinition RecruitSpear = Add(new ItemDefinition { Id = 53, Name = "Lança do Recruta", Kind = ItemKind.Lance, Slot = EquipSlot.RightHand, MinDamage = 6, MaxDamage = 11, ReqStrength = 22, BaseDurability = 24, AllowedClasses = C, DropLevel = 1, ShopPrice = 800 });
        public static readonly ItemDefinition VanguardSpear = Add(new ItemDefinition { Id = 54, Name = "Lança de Vanguarda", Kind = ItemKind.Lance, Slot = EquipSlot.RightHand, MinDamage = 14, MaxDamage = 22, ReqStrength = 50, BaseDurability = 34, AllowedClasses = C, DropLevel = 15, Tier = 1 });
        public static readonly ItemDefinition BulwarkHalberd = Add(new ItemDefinition { Id = 55, Name = "Alabarda do Bastião", Kind = ItemKind.Lance, Slot = EquipSlot.RightHand, MinDamage = 26, MaxDamage = 38, ReqStrength = 88, BaseDurability = 42, AllowedClasses = C, DropLevel = 30, Tier = 2 });
        public static readonly ItemDefinition SolarOathPike = Add(new ItemDefinition { Id = 44, Name = "Haste do Juramento Solar", Kind = ItemKind.Lance, Slot = EquipSlot.RightHand, MinDamage = 48, MaxDamage = 66, ReqStrength = 115, BaseDurability = 62, AllowedClasses = C, DropLevel = 34, Tier = 2, Epic = true });
        // Invocadora de Ecos: tomos e sinos
        public static readonly ItemDefinition LesserEchoTome = Add(new ItemDefinition { Id = 56, Name = "Tomo do Eco Menor", Kind = ItemKind.Tome, Slot = EquipSlot.RightHand, MinDamage = 3, MaxDamage = 6, MagicPowerPercent = 9, ReqStrength = 0, ReqEnergy = 20, BaseDurability = 20, AllowedClasses = I, DropLevel = 1, ShopPrice = 800 });
        public static readonly ItemDefinition VeiledSilverBell = Add(new ItemDefinition { Id = 57, Name = "Sino de Prata Velada", Kind = ItemKind.Tome, Slot = EquipSlot.RightHand, MinDamage = 6, MaxDamage = 10, MagicPowerPercent = 19, ReqStrength = 0, ReqEnergy = 50, BaseDurability = 28, AllowedClasses = I, DropLevel = 15, Tier = 1 });
        public static readonly ItemDefinition ThousandEchoGrimoire = Add(new ItemDefinition { Id = 58, Name = "Grimório dos Mil Ecos", Kind = ItemKind.Tome, Slot = EquipSlot.RightHand, MinDamage = 10, MaxDamage = 16, MagicPowerPercent = 31, ReqStrength = 8, ReqEnergy = 80, BaseDurability = 36, AllowedClasses = I, DropLevel = 30, Tier = 2 });
        public static readonly ItemDefinition EternalSilenceBell = Add(new ItemDefinition { Id = 45, Name = "Sino do Silêncio Eterno", Kind = ItemKind.Tome, Slot = EquipSlot.RightHand, MinDamage = 16, MaxDamage = 24, MagicPowerPercent = 46, ReqStrength = 10, ReqEnergy = 120, BaseDurability = 56, AllowedClasses = I, DropLevel = 34, Tier = 2, Epic = true });
        // Punho de Brasa: manoplas (as duas mãos; o slot é o da arma)
        public static readonly ItemDefinition RawhideFists = Add(new ItemDefinition { Id = 59, Name = "Manoplas de Couro Cru", Kind = ItemKind.Gauntlet, Slot = EquipSlot.RightHand, MinDamage = 5, MaxDamage = 9, ReqStrength = 18, ReqAgility = 18, BaseDurability = 24, AllowedClasses = P, DropLevel = 1, ShopPrice = 800 });
        public static readonly ItemDefinition CastIronFists = Add(new ItemDefinition { Id = 60, Name = "Manoplas de Ferro Fundido", Kind = ItemKind.Gauntlet, Slot = EquipSlot.RightHand, MinDamage = 11, MaxDamage = 17, ReqStrength = 40, ReqAgility = 40, BaseDurability = 34, AllowedClasses = P, DropLevel = 15, Tier = 1 });
        public static readonly ItemDefinition MagmaFists = Add(new ItemDefinition { Id = 61, Name = "Punhos de Magma", Kind = ItemKind.Gauntlet, Slot = EquipSlot.RightHand, MinDamage = 13, MaxDamage = 20, ReqStrength = 70, ReqAgility = 70, BaseDurability = 42, AllowedClasses = P, DropLevel = 30, Tier = 2 });
        public static readonly ItemDefinition LivingEmberCore = Add(new ItemDefinition { Id = 46, Name = "Núcleo de Brasa Viva", Kind = ItemKind.Gauntlet, Slot = EquipSlot.RightHand, MinDamage = 36, MaxDamage = 50, ReqStrength = 95, ReqAgility = 95, BaseDurability = 60, AllowedClasses = P, DropLevel = 34, Tier = 2, Epic = true });

        // Conjuntos das classes novas (5 peças cada)
        public static readonly ItemDefinition WeaveHelm = Add(new ItemDefinition { Id = 150, Set = ArmorSet.ArcaneWeave, Name = "Elmo da Trama Arcana", Kind = ItemKind.Helm, Slot = EquipSlot.Helm, Defense = 8, ReqStrength = 40, ReqEnergy = 25, BaseDurability = 32, AllowedClasses = L, DropLevel = 14 });
        public static readonly ItemDefinition WeaveArmor = Add(new ItemDefinition { Id = 151, Set = ArmorSet.ArcaneWeave, Name = "Gibão da Trama Arcana", Kind = ItemKind.Armor, Slot = EquipSlot.Armor, Defense = 15, ReqStrength = 45, ReqEnergy = 30, BaseDurability = 38, AllowedClasses = L, DropLevel = 14 });
        public static readonly ItemDefinition WeavePants = Add(new ItemDefinition { Id = 152, Set = ArmorSet.ArcaneWeave, Name = "Calças da Trama Arcana", Kind = ItemKind.Pants, Slot = EquipSlot.Pants, Defense = 11, ReqStrength = 40, ReqEnergy = 25, BaseDurability = 34, AllowedClasses = L, DropLevel = 14 });
        public static readonly ItemDefinition WeaveGloves = Add(new ItemDefinition { Id = 153, Set = ArmorSet.ArcaneWeave, Name = "Luvas da Trama Arcana", Kind = ItemKind.Gloves, Slot = EquipSlot.Gloves, Defense = 6, ReqStrength = 36, ReqEnergy = 20, BaseDurability = 28, AllowedClasses = L, DropLevel = 14 });
        public static readonly ItemDefinition WeaveBoots = Add(new ItemDefinition { Id = 154, Set = ArmorSet.ArcaneWeave, Name = "Botas da Trama Arcana", Kind = ItemKind.Boots, Slot = EquipSlot.Boots, Defense = 7, ReqStrength = 36, ReqEnergy = 20, BaseDurability = 30, AllowedClasses = L, DropLevel = 14 });
        public static readonly ItemDefinition BastionHelm = Add(new ItemDefinition { Id = 160, Set = ArmorSet.Bastion, Name = "Elmo do Bastião", Kind = ItemKind.Helm, Slot = EquipSlot.Helm, Defense = 11, ReqStrength = 50, BaseDurability = 36, AllowedClasses = C, DropLevel = 16 });
        public static readonly ItemDefinition BastionArmor = Add(new ItemDefinition { Id = 161, Set = ArmorSet.Bastion, Name = "Couraça do Bastião", Kind = ItemKind.Armor, Slot = EquipSlot.Armor, Defense = 19, ReqStrength = 58, BaseDurability = 42, AllowedClasses = C, DropLevel = 16 });
        public static readonly ItemDefinition BastionPants = Add(new ItemDefinition { Id = 162, Set = ArmorSet.Bastion, Name = "Grevas do Bastião", Kind = ItemKind.Pants, Slot = EquipSlot.Pants, Defense = 14, ReqStrength = 50, BaseDurability = 38, AllowedClasses = C, DropLevel = 16 });
        public static readonly ItemDefinition BastionGloves = Add(new ItemDefinition { Id = 163, Set = ArmorSet.Bastion, Name = "Manoplas do Bastião", Kind = ItemKind.Gloves, Slot = EquipSlot.Gloves, Defense = 7, ReqStrength = 45, BaseDurability = 30, AllowedClasses = C, DropLevel = 16 });
        public static readonly ItemDefinition BastionBoots = Add(new ItemDefinition { Id = 164, Set = ArmorSet.Bastion, Name = "Botas do Bastião", Kind = ItemKind.Boots, Slot = EquipSlot.Boots, Defense = 9, ReqStrength = 45, BaseDurability = 32, AllowedClasses = C, DropLevel = 16 });
        public static readonly ItemDefinition EchoHood = Add(new ItemDefinition { Id = 170, Set = ArmorSet.EchoVeil, Name = "Capuz dos Ecos", Kind = ItemKind.Helm, Slot = EquipSlot.Helm, Defense = 6, ReqStrength = 10, ReqEnergy = 45, BaseDurability = 28, AllowedClasses = I, DropLevel = 12 });
        public static readonly ItemDefinition EchoRobe = Add(new ItemDefinition { Id = 171, Set = ArmorSet.EchoVeil, Name = "Manto dos Ecos", Kind = ItemKind.Armor, Slot = EquipSlot.Armor, Defense = 11, ReqStrength = 10, ReqEnergy = 50, BaseDurability = 34, AllowedClasses = I, DropLevel = 12 });
        public static readonly ItemDefinition EchoPants = Add(new ItemDefinition { Id = 172, Set = ArmorSet.EchoVeil, Name = "Saias dos Ecos", Kind = ItemKind.Pants, Slot = EquipSlot.Pants, Defense = 8, ReqStrength = 10, ReqEnergy = 45, BaseDurability = 30, AllowedClasses = I, DropLevel = 12 });
        public static readonly ItemDefinition EchoGloves = Add(new ItemDefinition { Id = 173, Set = ArmorSet.EchoVeil, Name = "Luvas dos Ecos", Kind = ItemKind.Gloves, Slot = EquipSlot.Gloves, Defense = 4, ReqStrength = 10, ReqEnergy = 40, BaseDurability = 24, AllowedClasses = I, DropLevel = 12 });
        public static readonly ItemDefinition EchoBoots = Add(new ItemDefinition { Id = 174, Set = ArmorSet.EchoVeil, Name = "Sapatilhas dos Ecos", Kind = ItemKind.Boots, Slot = EquipSlot.Boots, Defense = 5, ReqStrength = 10, ReqEnergy = 40, BaseDurability = 26, AllowedClasses = I, DropLevel = 12 });
        public static readonly ItemDefinition EmberHideHelm = Add(new ItemDefinition { Id = 180, Set = ArmorSet.EmberHide, Name = "Bandana de Couro Ígneo", Kind = ItemKind.Helm, Slot = EquipSlot.Helm, Defense = 7, ReqStrength = 35, ReqAgility = 35, BaseDurability = 30, AllowedClasses = P, DropLevel = 14 });
        public static readonly ItemDefinition EmberHideArmor = Add(new ItemDefinition { Id = 181, Set = ArmorSet.EmberHide, Name = "Colete de Couro Ígneo", Kind = ItemKind.Armor, Slot = EquipSlot.Armor, Defense = 14, ReqStrength = 40, ReqAgility = 40, BaseDurability = 36, AllowedClasses = P, DropLevel = 14 });
        public static readonly ItemDefinition EmberHidePants = Add(new ItemDefinition { Id = 182, Set = ArmorSet.EmberHide, Name = "Calças de Couro Ígneo", Kind = ItemKind.Pants, Slot = EquipSlot.Pants, Defense = 10, ReqStrength = 35, ReqAgility = 35, BaseDurability = 32, AllowedClasses = P, DropLevel = 14 });
        public static readonly ItemDefinition EmberHideGloves = Add(new ItemDefinition { Id = 183, Set = ArmorSet.EmberHide, Name = "Faixas de Couro Ígneo", Kind = ItemKind.Gloves, Slot = EquipSlot.Gloves, Defense = 5, ReqStrength = 30, ReqAgility = 30, BaseDurability = 26, AllowedClasses = P, DropLevel = 14 });
        public static readonly ItemDefinition EmberHideBoots = Add(new ItemDefinition { Id = 184, Set = ArmorSet.EmberHide, Name = "Botas de Couro Ígneo", Kind = ItemKind.Boots, Slot = EquipSlot.Boots, Defense = 6, ReqStrength = 30, ReqAgility = 30, BaseDurability = 28, AllowedClasses = P, DropLevel = 14 });

        private static ItemDefinition Add(ItemDefinition d) { _byId[d.Id] = d; _all.Add(d); return d; }
        public static ItemDefinition? Get(int id) => _byId.TryGetValue(id, out var d) ? d : null;
    }
}
