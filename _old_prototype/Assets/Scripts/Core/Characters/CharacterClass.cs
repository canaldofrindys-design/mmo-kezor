using System;

namespace MuProto.Core.Characters
{
    public enum CharacterClass { DarkKnight = 0, DarkWizard = 1, FairyElf = 2 }

    [Flags]
    public enum ClassMask { None = 0, DarkKnight = 1, DarkWizard = 2, FairyElf = 4, All = 7 }

    public enum StatType { Strength, Agility, Vitality, Energy }

    public struct Attributes
    {
        public int Strength, Agility, Vitality, Energy;
        public Attributes(int str, int agi, int vit, int ene) { Strength = str; Agility = agi; Vitality = vit; Energy = ene; }
        public int Get(StatType t) => t switch
        {
            StatType.Strength => Strength, StatType.Agility => Agility,
            StatType.Vitality => Vitality, _ => Energy
        };
        public void Add(StatType t, int v)
        {
            switch (t)
            {
                case StatType.Strength: Strength += v; break;
                case StatType.Agility: Agility += v; break;
                case StatType.Vitality: Vitality += v; break;
                default: Energy += v; break;
            }
        }
    }

    /// <summary>Definição balanceável de uma classe. Todos os números são originais deste protótipo.</summary>
    public sealed class ClassDefinition
    {
        public CharacterClass Class;
        public string DisplayName = "";
        public Attributes BaseStats;
        public int PointsPerLevel;
        public int BaseHp, BaseMp;
        public float HpPerLevel, HpPerVitality, MpPerLevel, MpPerEnergy;
        public int StrDivMin, StrDivMax;     // dano físico = Str/div
        public int AgiDamageDivMin, AgiDamageDivMax; // 0 = não usa (Elf usa)
        public int EneDivMin, EneDivMax;     // dano mágico (0 = sem magia)
        public int AgiDivDefense;

        public static ClassMask MaskOf(CharacterClass c) => (ClassMask)(1 << (int)c);

        public static ClassDefinition Get(CharacterClass c) => c switch
        {
            CharacterClass.DarkKnight => DarkKnight,
            CharacterClass.DarkWizard => DarkWizard,
            _ => FairyElf
        };

        public static readonly ClassDefinition DarkKnight = new ClassDefinition
        {
            Class = CharacterClass.DarkKnight, DisplayName = "Dark Knight",
            BaseStats = new Attributes(28, 20, 25, 10), PointsPerLevel = 5,
            BaseHp = 110, BaseMp = 20, HpPerLevel = 2f, HpPerVitality = 3f, MpPerLevel = 0.5f, MpPerEnergy = 1f,
            StrDivMin = 6, StrDivMax = 4, EneDivMin = 0, EneDivMax = 0, AgiDivDefense = 3
        };
        public static readonly ClassDefinition DarkWizard = new ClassDefinition
        {
            Class = CharacterClass.DarkWizard, DisplayName = "Dark Wizard",
            BaseStats = new Attributes(18, 18, 15, 30), PointsPerLevel = 5,
            BaseHp = 60, BaseMp = 60, HpPerLevel = 1f, HpPerVitality = 2f, MpPerLevel = 2f, MpPerEnergy = 2f,
            StrDivMin = 8, StrDivMax = 4, EneDivMin = 9, EneDivMax = 4, AgiDivDefense = 4
        };
        public static readonly ClassDefinition FairyElf = new ClassDefinition
        {
            Class = CharacterClass.FairyElf, DisplayName = "Fairy Elf",
            BaseStats = new Attributes(22, 25, 20, 15), PointsPerLevel = 5,
            BaseHp = 80, BaseMp = 30, HpPerLevel = 1f, HpPerVitality = 2f, MpPerLevel = 1.5f, MpPerEnergy = 1.5f,
            StrDivMin = 14, StrDivMax = 8, AgiDamageDivMin = 7, AgiDamageDivMax = 4, EneDivMin = 0, EneDivMax = 0, AgiDivDefense = 6
        };
    }
}
