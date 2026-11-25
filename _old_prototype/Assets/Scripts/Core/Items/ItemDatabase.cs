using System.Collections.Generic;
using MuProto.Core.Characters;

namespace MuProto.Core.Items
{
    /// <summary>Catálogo original de itens do protótipo (nomes e valores próprios).</summary>
    public static class ItemDatabase
    {
        private static readonly Dictionary<int, ItemDefinition> _byId = new Dictionary<int, ItemDefinition>();
        public static IReadOnlyCollection<ItemDefinition> All => _byId.Values;

        // Jóias
        public static readonly ItemDefinition JewelOfBless = Add(new ItemDefinition { Id = 1000, Name = "Jewel of Bless", Kind = ItemKind.Jewel, Jewel = JewelType.Bless, MaxStack = 1, Value = 9_000_000 });
        public static readonly ItemDefinition JewelOfSoul = Add(new ItemDefinition { Id = 1001, Name = "Jewel of Soul", Kind = ItemKind.Jewel, Jewel = JewelType.Soul, MaxStack = 1, Value = 6_000_000 });
        public static readonly ItemDefinition JewelOfLife = Add(new ItemDefinition { Id = 1002, Name = "Jewel of Life", Kind = ItemKind.Jewel, Jewel = JewelType.Life, MaxStack = 1, Value = 4_500_000 });
        public static readonly ItemDefinition JewelOfChaos = Add(new ItemDefinition { Id = 1003, Name = "Jewel of Chaos", Kind = ItemKind.Jewel, Jewel = JewelType.Chaos, MaxStack = 1, Value = 800_000 });

        // Armas
        public static readonly ItemDefinition IronBlade = Add(new ItemDefinition { Id = 1, Name = "Lâmina de Ferro", Kind = ItemKind.Sword, Slot = EquipSlot.RightHand, MinDamage = 6, MaxDamage = 11, ReqStrength = 22, BaseDurability = 22, AllowedClasses = ClassMask.DarkKnight, DropLevel = 1 });
        public static readonly ItemDefinition StormBlade = Add(new ItemDefinition { Id = 2, Name = "Montante da Tempestade", Kind = ItemKind.Sword, Slot = EquipSlot.RightHand, MinDamage = 28, MaxDamage = 40, ReqStrength = 90, BaseDurability = 40, AllowedClasses = ClassMask.DarkKnight, DropLevel = 40 });
        public static readonly ItemDefinition ApprenticeStaff = Add(new ItemDefinition { Id = 10, Name = "Cajado do Aprendiz", Kind = ItemKind.Staff, Slot = EquipSlot.RightHand, MinDamage = 3, MaxDamage = 6, MagicPowerPercent = 8, ReqStrength = 12, ReqEnergy = 20, BaseDurability = 20, AllowedClasses = ClassMask.DarkWizard, DropLevel = 1 });
        public static readonly ItemDefinition OracleStaff = Add(new ItemDefinition { Id = 11, Name = "Cajado do Oráculo", Kind = ItemKind.Staff, Slot = EquipSlot.RightHand, MinDamage = 10, MaxDamage = 16, MagicPowerPercent = 30, ReqStrength = 20, ReqEnergy = 80, BaseDurability = 36, AllowedClasses = ClassMask.DarkWizard, DropLevel = 40 });
        public static readonly ItemDefinition HuntingBow = Add(new ItemDefinition { Id = 20, Name = "Arco de Caça", Kind = ItemKind.Bow, Slot = EquipSlot.RightHand, MinDamage = 5, MaxDamage = 10, ReqAgility = 22, BaseDurability = 20, AllowedClasses = ClassMask.FairyElf, DropLevel = 1 });
        public static readonly ItemDefinition ChaosRuneBlade = Add(new ItemDefinition { Id = 30, Name = "Lâmina Rúnica do Caos", Kind = ItemKind.Sword, Slot = EquipSlot.RightHand, MinDamage = 45, MaxDamage = 62, ReqStrength = 110, BaseDurability = 60, AllowedClasses = ClassMask.All, DropLevel = 999 });

        // Armaduras
        public static readonly ItemDefinition LeatherHelm = Add(new ItemDefinition { Id = 100, Name = "Elmo de Couro", Kind = ItemKind.Helm, Slot = EquipSlot.Helm, Defense = 4, ReqStrength = 20, BaseDurability = 25, DropLevel = 1 });
        public static readonly ItemDefinition LeatherArmor = Add(new ItemDefinition { Id = 101, Name = "Armadura de Couro", Kind = ItemKind.Armor, Slot = EquipSlot.Armor, Defense = 8, ReqStrength = 20, BaseDurability = 30, DropLevel = 1 });
        public static readonly ItemDefinition LeatherBoots = Add(new ItemDefinition { Id = 104, Name = "Botas de Couro", Kind = ItemKind.Boots, Slot = EquipSlot.Boots, Defense = 3, ReqStrength = 18, BaseDurability = 22, DropLevel = 1 });
        public static readonly ItemDefinition BronzeShield = Add(new ItemDefinition { Id = 120, Name = "Escudo de Bronze", Kind = ItemKind.Shield, Slot = EquipSlot.LeftHand, Defense = 6, ReqStrength = 30, BaseDurability = 30, AllowedClasses = ClassMask.DarkKnight | ClassMask.DarkWizard, DropLevel = 5 });

        private static ItemDefinition Add(ItemDefinition d) { _byId[d.Id] = d; return d; }
        public static ItemDefinition? Get(int id) => _byId.TryGetValue(id, out var d) ? d : null;
    }
}
