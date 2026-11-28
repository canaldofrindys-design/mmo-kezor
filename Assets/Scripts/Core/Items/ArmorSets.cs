using System.Collections.Generic;

namespace Kezor.Core.Items
{
    /// <summary>
    /// Bônus de conjunto: as 5 peças do mesmo conjunto (elmo, armadura, calças, luvas e botas), todas com durabilidade,
    /// ativam o bônus. Não importa o nível de runa das peças.
    /// </summary>
    public static class ArmorSets
    {
        public struct SetBonus
        {
            public int Defense, DefenseRatePercent, ReductionPercent, DamagePercent, MagicPercent;
        }

        public static readonly EquipSlot[] Pieces = { EquipSlot.Helm, EquipSlot.Armor, EquipSlot.Pants, EquipSlot.Gloves, EquipSlot.Boots };

        public static string Name(ArmorSet s) => s switch
        {
            ArmorSet.Leather => "Couro",
            ArmorSet.Scale => "Escamas",
            ArmorSet.Veil => "Véu",
            ArmorSet.RunePlate => "Placas Rúnicas",
            ArmorSet.ArcaneWeave => "Trama Arcana",
            ArmorSet.Bastion => "Bastião",
            ArmorSet.EchoVeil => "Véu dos Ecos",
            ArmorSet.EmberHide => "Couro Ígneo",
            // <kz-class-sets name>
            ArmorSet.KnightPlate => "Placas do Cavaleiro",
            ArmorSet.TemplarPlate => "Placas do Templário",
            ArmorSet.SovereignPlate => "Placas do Soberano",
            ArmorSet.ApprenticeRobe => "Túnica do Aprendiz",
            ArmorSet.SageRobe => "Vestes do Sábio",
            ArmorSet.ArchmageRobe => "Vestes do Arquimago",
            ArmorSet.ArcherGarb => "Trajes do Arqueiro",
            ArmorSet.ScoutLeather => "Couraça do Batedor",
            ArmorSet.SentinelLeather => "Couraça da Sentinela Rubra",
            ArmorSet.FencerGarb => "Couraça do Esgrimista",
            ArmorSet.DuelistPlate => "Placas do Duelista",
            ArmorSet.SilverStagPlate => "Placas do Cervo de Prata",
            ArmorSet.CrusaderPlate => "Placas do Cruzado",
            ArmorSet.MarshalPlate => "Placas do Marechal",
            ArmorSet.BlackBannerPlate => "Placas do Estandarte Negro",
            ArmorSet.MysticGarb => "Trajes da Mística",
            ArmorSet.ProphetessRobe => "Vestes da Profetisa",
            ArmorSet.QueenGown => "Trajes da Rainha dos Ecos",
            // </kz-class-sets name>
            _ => "",
        };

        public static SetBonus Bonus(ArmorSet s) => s switch
        {
            ArmorSet.Leather => new SetBonus { Defense = 5, DefenseRatePercent = 5 },
            ArmorSet.Scale => new SetBonus { Defense = 12, DefenseRatePercent = 10 },
            ArmorSet.Veil => new SetBonus { Defense = 6, MagicPercent = 15 },
            ArmorSet.RunePlate => new SetBonus { Defense = 20, ReductionPercent = 5, DamagePercent = 5 },
            ArmorSet.ArcaneWeave => new SetBonus { Defense = 10, DamagePercent = 6, MagicPercent = 8 },
            ArmorSet.Bastion => new SetBonus { Defense = 18, ReductionPercent = 4, DefenseRatePercent = 5 },
            ArmorSet.EchoVeil => new SetBonus { Defense = 8, MagicPercent = 12, ReductionPercent = 2 },
            ArmorSet.EmberHide => new SetBonus { Defense = 8, DamagePercent = 10 },
            // <kz-class-sets bonus>
            ArmorSet.KnightPlate => new SetBonus { Defense = 12, ReductionPercent = 3 },
            ArmorSet.TemplarPlate => new SetBonus { Defense = 30, ReductionPercent = 7, DamagePercent = 6 },
            ArmorSet.SovereignPlate => new SetBonus { Defense = 42, ReductionPercent = 10, DamagePercent = 8, DefenseRatePercent = 5 },
            ArmorSet.ApprenticeRobe => new SetBonus { Defense = 4, MagicPercent = 8 },
            ArmorSet.SageRobe => new SetBonus { Defense = 12, MagicPercent = 20, ReductionPercent = 3 },
            ArmorSet.ArchmageRobe => new SetBonus { Defense = 18, MagicPercent = 28, ReductionPercent = 5, DamagePercent = 4 },
            ArmorSet.ArcherGarb => new SetBonus { Defense = 6, DefenseRatePercent = 6 },
            ArmorSet.ScoutLeather => new SetBonus { Defense = 20, DefenseRatePercent = 14, DamagePercent = 5 },
            ArmorSet.SentinelLeather => new SetBonus { Defense = 28, DefenseRatePercent = 18, DamagePercent = 8, ReductionPercent = 4 },
            ArmorSet.FencerGarb => new SetBonus { Defense = 6, DamagePercent = 4, MagicPercent = 4 },
            ArmorSet.DuelistPlate => new SetBonus { Defense = 22, DamagePercent = 10, MagicPercent = 12 },
            ArmorSet.SilverStagPlate => new SetBonus { Defense = 32, DamagePercent = 14, MagicPercent = 16, ReductionPercent = 4 },
            ArmorSet.CrusaderPlate => new SetBonus { Defense = 10, ReductionPercent = 3, DefenseRatePercent = 3 },
            ArmorSet.MarshalPlate => new SetBonus { Defense = 28, ReductionPercent = 7, DefenseRatePercent = 8, DamagePercent = 4 },
            ArmorSet.BlackBannerPlate => new SetBonus { Defense = 40, ReductionPercent = 9, DefenseRatePercent = 10, DamagePercent = 7 },
            ArmorSet.MysticGarb => new SetBonus { Defense = 4, MagicPercent = 8 },
            ArmorSet.ProphetessRobe => new SetBonus { Defense = 12, MagicPercent = 18, ReductionPercent = 3 },
            ArmorSet.QueenGown => new SetBonus { Defense = 18, MagicPercent = 26, ReductionPercent = 5, DamagePercent = 4 },
            // </kz-class-sets bonus>
            _ => default,
        };

        public static string Describe(ArmorSet s)
        {
            var b = Bonus(s);
            var parts = new List<string>();
            if (b.Defense > 0) parts.Add($"defesa +{b.Defense}");
            if (b.DefenseRatePercent > 0) parts.Add($"taxa de defesa +{b.DefenseRatePercent}%");
            if (b.ReductionPercent > 0) parts.Add($"-{b.ReductionPercent}% de dano recebido");
            if (b.DamagePercent > 0) parts.Add($"dano +{b.DamagePercent}%");
            if (b.MagicPercent > 0) parts.Add($"poder mágico +{b.MagicPercent}%");
            return $"Conjunto {Name(s)} (5 peças): " + string.Join(", ", parts);
        }

        /// <summary>Quantas peças do conjunto estão equipadas (e com durabilidade).</summary>
        public static int Count(IReadOnlyDictionary<EquipSlot, Item> eq, ArmorSet set)
        {
            int n = 0;
            foreach (var slot in Pieces)
                if (eq.TryGetValue(slot, out var it) && it.Def.Set == set && it.Durability > 0) n++;
            return n;
        }

        public static ArmorSet Complete(IReadOnlyDictionary<EquipSlot, Item> eq)
        {
            if (!eq.TryGetValue(EquipSlot.Armor, out var armor) || armor.Def.Set == ArmorSet.None) return ArmorSet.None;
            return Count(eq, armor.Def.Set) == Pieces.Length ? armor.Def.Set : ArmorSet.None;
        }
    }
}
