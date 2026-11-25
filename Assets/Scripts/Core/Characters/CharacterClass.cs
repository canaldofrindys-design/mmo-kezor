using System;

namespace Kezor.Core.Characters
{
    /// <summary>Classes originais de Kezor.</summary>
    public enum CharacterClass { Warden = 0, Arcanist = 1, Ranger = 2, Bladeweaver = 3, Commander = 4, Invoker = 5, EmberFist = 6 }

    [Flags]
    public enum ClassMask { None = 0, Warden = 1, Arcanist = 2, Ranger = 4, Bladeweaver = 8, Commander = 16, Invoker = 32, EmberFist = 64, All = 127 }

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

    /// <summary>Definição balanceável de uma classe. Todos os números são deste projeto.</summary>
    public sealed class ClassDefinition
    {
        public CharacterClass Class;
        public string DisplayName = "";
        public string Code = "";
        public string Lore = "";
        public Attributes BaseStats;
        public int PointsPerLevel;
        public int BaseHp, BaseMp;
        public float HpPerLevel, HpPerVitality, MpPerLevel, MpPerEnergy;
        public int StrDivMin, StrDivMax;               // dano físico = Str/div
        public int AgiDamageDivMin, AgiDamageDivMax;   // 0 = não usa (Patrulheiro usa)
        public int EneDivMin, EneDivMax;               // dano mágico (0 = sem magia)
        public int AgiDivDefense;
        public float BasicRange;                       // alcance do ataque básico (unidades do mundo)
        public float MoveSpeed = 5.5f;
        /// <summary>Intervalo mínimo entre golpes (s). O Punho de Brasa bate mais rápido.</summary>
        public float AttackCooldown = 0.6f;
        /// <summary>Resumo do papel para a tela de criação.</summary>
        public string Role = "";

        public static ClassMask MaskOf(CharacterClass c) => (ClassMask)(1 << (int)c);

        public static ClassDefinition Get(CharacterClass c) => c switch
        {
            CharacterClass.Warden => Warden,
            CharacterClass.Arcanist => Arcanist,
            CharacterClass.Bladeweaver => Bladeweaver,
            CharacterClass.Commander => Commander,
            CharacterClass.Invoker => Invoker,
            CharacterClass.EmberFist => EmberFist,
            _ => Ranger
        };

        public static readonly ClassDefinition Warden = new ClassDefinition
        {
            Class = CharacterClass.Warden, DisplayName = "Guardião Rúnico", Code = "GUA",
            Lore = "Soldados que gravam runas de juramento na própria armadura. Linha de frente, muita vida, golpes em combo.", Role = "Linha de frente",
            BaseStats = new Attributes(28, 20, 25, 10), PointsPerLevel = 5,
            BaseHp = 110, BaseMp = 20, HpPerLevel = 2f, HpPerVitality = 3f, MpPerLevel = 0.5f, MpPerEnergy = 1f,
            StrDivMin = 6, StrDivMax = 4, AgiDivDefense = 3, BasicRange = 2.3f
        };
        public static readonly ClassDefinition Arcanist = new ClassDefinition
        {
            Class = CharacterClass.Arcanist, DisplayName = "Arcanista do Véu", Code = "ARC",
            Lore = "Estudiosos que puxam poder do Véu entre os mundos. Frágeis, dano mágico em área.", Role = "Magia em área",
            BaseStats = new Attributes(18, 18, 15, 30), PointsPerLevel = 5,
            BaseHp = 60, BaseMp = 60, HpPerLevel = 1f, HpPerVitality = 2f, MpPerLevel = 2f, MpPerEnergy = 2f,
            StrDivMin = 8, StrDivMax = 4, EneDivMin = 9, EneDivMax = 4, AgiDivDefense = 4, BasicRange = 7f
        };
        public static readonly ClassDefinition Ranger = new ClassDefinition
        {
            Class = CharacterClass.Ranger, DisplayName = "Patrulheiro Selvagem", Code = "PAT",
            Lore = "Caçadores das fronteiras do Bosque Cinzento. Ágeis, atacam de longe com arco.", Role = "Ataque à distância",
            BaseStats = new Attributes(22, 25, 20, 15), PointsPerLevel = 5,
            BaseHp = 80, BaseMp = 30, HpPerLevel = 1f, HpPerVitality = 2f, MpPerLevel = 1.5f, MpPerEnergy = 1.5f,
            StrDivMin = 14, StrDivMax = 8, AgiDamageDivMin = 7, AgiDamageDivMax = 4, AgiDivDefense = 6, BasicRange = 8f, MoveSpeed = 6f
        };

        public static readonly ClassDefinition Bladeweaver = new ClassDefinition
        {
            Class = CharacterClass.Bladeweaver, DisplayName = "Lâmina Arcana", Code = "LAM", Role = "Híbrido: espada e magia",
            Lore = "Esgrimistas que tecem runas no fio da espada. Força e energia somam no mesmo golpe; encantam a lâmina e soltam ondas arcanas curtas.",
            BaseStats = new Attributes(24, 20, 20, 22), PointsPerLevel = 5,
            BaseHp = 85, BaseMp = 40, HpPerLevel = 1.5f, HpPerVitality = 2.5f, MpPerLevel = 1.5f, MpPerEnergy = 1.5f,
            StrDivMin = 7, StrDivMax = 5, EneDivMin = 15, EneDivMax = 9, AgiDivDefense = 4, BasicRange = 2.4f
        };
        public static readonly ClassDefinition Commander = new ClassDefinition
        {
            Class = CharacterClass.Commander, DisplayName = "Comandante do Estandarte", Code = "COM", Role = "Suporte e controle",
            Lore = "Veteranos que lutam de lança na mão e estandarte nas costas. Fincam bandeiras que inflamam o grupo e dão ordens de ataque em área. A energia fortalece as auras.",
            BaseStats = new Attributes(24, 18, 26, 20), PointsPerLevel = 5,
            BaseHp = 100, BaseMp = 35, HpPerLevel = 1.8f, HpPerVitality = 2.8f, MpPerLevel = 1.2f, MpPerEnergy = 1.5f,
            StrDivMin = 7, StrDivMax = 5, AgiDivDefense = 3, BasicRange = 2.9f
        };
        public static readonly ClassDefinition Invoker = new ClassDefinition
        {
            Class = CharacterClass.Invoker, DisplayName = "Invocadora de Ecos", Code = "INV", Role = "Invocações e maldições",
            Lore = "Guardam ecos de vozes antigas num tomo ou num sino. Chamam espíritos que lutam por elas e rogam maldições que pesam e corroem.",
            BaseStats = new Attributes(15, 18, 18, 32), PointsPerLevel = 5,
            BaseHp = 60, BaseMp = 65, HpPerLevel = 1f, HpPerVitality = 2f, MpPerLevel = 2f, MpPerEnergy = 2f,
            StrDivMin = 9, StrDivMax = 5, EneDivMin = 10, EneDivMax = 5, AgiDivDefense = 4, BasicRange = 6.5f
        };
        public static readonly ClassDefinition EmberFist = new ClassDefinition
        {
            Class = CharacterClass.EmberFist, DisplayName = "Punho de Brasa", Code = "PUN", Role = "Corpo a corpo veloz",
            Lore = "Lutadores dos fornos de Brasalume que treinam com manoplas em brasa. Socos rápidos que acumulam o Fluxo de Brasa até explodir.",
            BaseStats = new Attributes(26, 27, 22, 10), PointsPerLevel = 5,
            BaseHp = 95, BaseMp = 25, HpPerLevel = 1.6f, HpPerVitality = 2.6f, MpPerLevel = 0.8f, MpPerEnergy = 1f,
            StrDivMin = 12, StrDivMax = 9, AgiDamageDivMin = 18, AgiDamageDivMax = 12, AgiDivDefense = 4, BasicRange = 1.9f,
            MoveSpeed = 5.8f, AttackCooldown = 0.5f
        };

        public static readonly ClassDefinition[] All = { Warden, Arcanist, Ranger, Bladeweaver, Commander, Invoker, EmberFist };

        public static bool TryParse(string code, out CharacterClass cls)
        {
            foreach (var d in All)
                if (string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase)) { cls = d.Class; return true; }
            cls = CharacterClass.Warden; return false;
        }
    }
}
