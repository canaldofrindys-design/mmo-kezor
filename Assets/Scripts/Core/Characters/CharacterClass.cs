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

    /// <summary>
    /// Definição de uma classe. As fórmulas seguem as do MU Online clássico (conferidas no OpenMU):
    /// vida = HpConst + HpPerLevel·Nv + HpPerVitality·VIT, mana igual com ENE, dano por divisores dos atributos etc.
    /// Ver docs/MECANICAS_MU.md.
    /// </summary>
    public sealed class ClassDefinition
    {
        public CharacterClass Class;
        public string DisplayName = "";
        public string Code = "";
        public string Lore = "";
        public Attributes BaseStats;
        public int PointsPerLevel = 5;
        /// <summary>Vida = HpConst + HpPerLevel·Nv + HpPerVitality·VIT.</summary>
        public float HpConst, HpPerLevel, HpPerVitality;
        /// <summary>Mana = MpConst + MpPerLevel·Nv + MpPerEnergy·(ENE − MpEnergyOffset).</summary>
        public float MpConst, MpPerLevel, MpPerEnergy;
        public int MpEnergyOffset;
        /// <summary>AG (Vigor) máximo = AgEne·ENE + AgVit·VIT + AgAgi·AGI + AgStr·FOR.</summary>
        public float AgEne, AgVit, AgAgi, AgStr;
        /// <summary>AG recuperado a cada tique: fração do máximo (Guardião 5%, demais 3%).</summary>
        public float AgRecovery = 0.03f;
        /// <summary>Dano físico: FOR/StrDiv + AGI/AgiDamageDiv + ENE/EnePhysDiv + VIT/VitPhysDiv (0 = não usa).</summary>
        public int StrDivMin, StrDivMax;
        public int AgiDamageDivMin, AgiDamageDivMax;
        public int EnePhysDivMin, EnePhysDivMax;
        public int VitPhysDivMin, VitPhysDivMax;
        /// <summary>Invocadora: o dano físico usa (FOR + AGI) / StrDiv.</summary>
        public bool StrAgiCombined;
        /// <summary>Dano mágico ENE/EneDiv (0 = sem magia).</summary>
        public int EneDivMin, EneDivMax;
        /// <summary>Defesa = AGI / AgiDivDefense; taxa de defesa = AGI / AgiDivDefenseRate.</summary>
        public int AgiDivDefense, AgiDivDefenseRate = 3;
        /// <summary>Taxa de ataque = ArPerLevel·Nv + ArPerAgi·AGI + FOR/ArStrDiv.</summary>
        public float ArPerLevel = 5f, ArPerAgi = 1.5f;
        public int ArStrDiv = 4;
        /// <summary>Velocidade de ataque = AGI / AttackSpeedDiv; de magia = AGI / MagicSpeedDiv.</summary>
        public int AttackSpeedDiv = 15, MagicSpeedDiv = 20;
        /// <summary>O ataque normal da classe é magia (usa a velocidade de magia).</summary>
        public bool CastsMagic;
        /// <summary>Multiplicador das skills: SkillMultiplier + SkillMultiplierPerEnergy·ENE.</summary>
        public float SkillMultiplier = 1f, SkillMultiplierPerEnergy;
        public float BasicRange;                       // alcance do ataque básico (unidades do mundo)
        public float MoveSpeed = 5.5f;
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

        // Papel no MU: Dark Knight
        public static readonly ClassDefinition Warden = new ClassDefinition
        {
            Class = CharacterClass.Warden, DisplayName = "Guardião Rúnico", Code = "GUA",
            Lore = "Soldados que gravam runas de juramento na própria armadura. Linha de frente, muita vida, golpes em combo.", Role = "Linha de frente",
            BaseStats = new Attributes(28, 20, 25, 10), PointsPerLevel = 5,
            HpConst = 35, HpPerLevel = 2f, HpPerVitality = 3f, MpConst = 10, MpPerLevel = 0.5f, MpPerEnergy = 1f,
            AgEne = 1f, AgVit = 0.3f, AgAgi = 0.2f, AgStr = 0.15f, AgRecovery = 0.05f,
            StrDivMin = 6, StrDivMax = 4, AgiDivDefense = 3, AgiDivDefenseRate = 3,
            AttackSpeedDiv = 15, MagicSpeedDiv = 20, SkillMultiplier = 2f, SkillMultiplierPerEnergy = 0.001f, BasicRange = 2.3f
        };
        // Papel no MU: Dark Wizard
        public static readonly ClassDefinition Arcanist = new ClassDefinition
        {
            Class = CharacterClass.Arcanist, DisplayName = "Arcanista do Véu", Code = "ARC",
            Lore = "Estudiosos que puxam poder do Véu entre os mundos. Frágeis, dano mágico em área.", Role = "Magia em área",
            BaseStats = new Attributes(18, 18, 15, 30), PointsPerLevel = 5,
            HpConst = 30, HpPerLevel = 1f, HpPerVitality = 2f, MpConst = 0, MpPerLevel = 2f, MpPerEnergy = 2f,
            AgEne = 0.2f, AgVit = 0.3f, AgAgi = 0.4f, AgStr = 0.2f,
            StrDivMin = 8, StrDivMax = 4, EneDivMin = 9, EneDivMax = 4, AgiDivDefense = 4, AgiDivDefenseRate = 3,
            AttackSpeedDiv = 20, MagicSpeedDiv = 10, CastsMagic = true, BasicRange = 7f
        };
        // Papel no MU: Fairy Elf
        public static readonly ClassDefinition Ranger = new ClassDefinition
        {
            Class = CharacterClass.Ranger, DisplayName = "Patrulheiro Selvagem", Code = "PAT",
            Lore = "Caçadores das fronteiras do Bosque Cinzento. Ágeis, atacam de longe com arco.", Role = "Ataque à distância",
            BaseStats = new Attributes(22, 25, 20, 15), PointsPerLevel = 5,
            HpConst = 39, HpPerLevel = 1f, HpPerVitality = 2f, MpConst = 6, MpPerLevel = 1.5f, MpPerEnergy = 1.5f,
            AgEne = 0.2f, AgVit = 0.3f, AgAgi = 0.2f, AgStr = 0.3f,
            StrDivMin = 14, StrDivMax = 8, AgiDamageDivMin = 7, AgiDamageDivMax = 4, AgiDivDefense = 10, AgiDivDefenseRate = 4,
            AttackSpeedDiv = 50, MagicSpeedDiv = 50, BasicRange = 8f, MoveSpeed = 6f
        };

        // Papel no MU: Magic Gladiator
        public static readonly ClassDefinition Bladeweaver = new ClassDefinition
        {
            Class = CharacterClass.Bladeweaver, DisplayName = "Lâmina Arcana", Code = "LAM", Role = "Híbrido: espada e magia",
            Lore = "Esgrimistas que tecem runas no fio da espada. Força e energia somam no mesmo golpe; encantam a lâmina e soltam ondas arcanas curtas.",
            BaseStats = new Attributes(26, 26, 26, 26), PointsPerLevel = 7,
            HpConst = 57, HpPerLevel = 1f, HpPerVitality = 2f, MpConst = 7, MpPerLevel = 1f, MpPerEnergy = 2f,
            AgEne = 0.15f, AgVit = 0.3f, AgAgi = 0.25f, AgStr = 0.2f,
            StrDivMin = 6, StrDivMax = 4, EnePhysDivMin = 12, EnePhysDivMax = 8, EneDivMin = 9, EneDivMax = 4,
            AgiDivDefense = 5, AgiDivDefenseRate = 3, AttackSpeedDiv = 15, MagicSpeedDiv = 20, SkillMultiplier = 2f, BasicRange = 2.4f
        };
        // Papel no MU: Dark Lord (o atributo Comando ainda não existe no Kezor)
        public static readonly ClassDefinition Commander = new ClassDefinition
        {
            Class = CharacterClass.Commander, DisplayName = "Comandante do Estandarte", Code = "COM", Role = "Suporte e controle",
            Lore = "Veteranos que lutam de lança na mão e estandarte nas costas. Fincam bandeiras que inflamam o grupo e dão ordens de ataque em área. A energia fortalece as auras.",
            BaseStats = new Attributes(26, 20, 20, 15), PointsPerLevel = 7,
            HpConst = 48.5f, HpPerLevel = 1.5f, HpPerVitality = 2f, MpConst = 38, MpPerLevel = 1f, MpPerEnergy = 1.5f, MpEnergyOffset = 15,
            AgEne = 0.15f, AgVit = 0.1f, AgAgi = 0.2f, AgStr = 0.3f,
            StrDivMin = 7, StrDivMax = 5, EnePhysDivMin = 14, EnePhysDivMax = 10, AgiDivDefense = 7, AgiDivDefenseRate = 7,
            ArPerAgi = 2.5f, ArStrDiv = 6, AttackSpeedDiv = 10, MagicSpeedDiv = 10,
            SkillMultiplier = 2f, SkillMultiplierPerEnergy = 0.0005f, BasicRange = 2.9f
        };
        // Papel no MU: Summoner
        public static readonly ClassDefinition Invoker = new ClassDefinition
        {
            Class = CharacterClass.Invoker, DisplayName = "Invocadora de Ecos", Code = "INV", Role = "Invocações e maldições",
            Lore = "Guardam ecos de vozes antigas num tomo ou num sino. Chamam espíritos que lutam por elas e rogam maldições que pesam e corroem.",
            BaseStats = new Attributes(21, 21, 18, 23), PointsPerLevel = 5,
            HpConst = 39, HpPerLevel = 1f, HpPerVitality = 2f, MpConst = 6, MpPerLevel = 1.5f, MpPerEnergy = 1.7f,
            AgEne = 0.15f, AgVit = 0.3f, AgAgi = 0.25f, AgStr = 0.2f,
            StrDivMin = 7, StrDivMax = 4, StrAgiCombined = true, EneDivMin = 9, EneDivMax = 4, AgiDivDefense = 3, AgiDivDefenseRate = 4,
            AttackSpeedDiv = 20, MagicSpeedDiv = 20, CastsMagic = true, BasicRange = 6.5f
        };
        // Papel no MU: Rage Fighter
        public static readonly ClassDefinition EmberFist = new ClassDefinition
        {
            Class = CharacterClass.EmberFist, DisplayName = "Punho de Brasa", Code = "PUN", Role = "Corpo a corpo veloz",
            Lore = "Lutadores dos fornos de Brasalume que treinam com manoplas em brasa. Socos rápidos que acumulam o Fluxo de Brasa até explodir.",
            BaseStats = new Attributes(32, 27, 25, 20), PointsPerLevel = 7,
            HpConst = 57, HpPerLevel = 1.3f, HpPerVitality = 2f, MpConst = 7, MpPerLevel = 1f, MpPerEnergy = 1.3f,
            AgEne = 1f, AgVit = 0.3f, AgAgi = 0.2f, AgStr = 0.15f,
            StrDivMin = 7, StrDivMax = 5, VitPhysDivMin = 15, VitPhysDivMax = 12, AgiDivDefense = 8, AgiDivDefenseRate = 10,
            ArPerLevel = 3f, ArPerAgi = 1.25f, ArStrDiv = 6, AttackSpeedDiv = 9, MagicSpeedDiv = 9,
            SkillMultiplier = 0.5f, SkillMultiplierPerEnergy = 0.001f, BasicRange = 1.9f, MoveSpeed = 5.8f
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
