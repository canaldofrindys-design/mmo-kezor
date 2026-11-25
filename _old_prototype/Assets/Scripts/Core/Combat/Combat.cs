using System;
using MuProto.Core.Characters;

namespace MuProto.Core.Combat
{
    public sealed class CombatStats
    {
        public int Level;
        public int MinDamage, MaxDamage, MinMagic, MaxMagic;
        public int Defense, AttackRate, DefenseRate;
        public int CriticalChancePercent, ExcellentChancePercent, DamageReductionPercent;
    }

    public interface ICombatant
    {
        int Id { get; }
        string Name { get; }
        int Level { get; }
        int Hp { get; set; }
        int MaxHp { get; }
        bool IsDead { get; }
        CombatStats GetCombatStats();
    }

    public enum HitType { Miss, Normal, Critical, Excellent }

    public struct HitResult
    {
        public HitType Type;
        public int Damage;
        public bool Killed;
        public override string ToString() => Type == HitType.Miss ? "MISS" : $"{Type}:{Damage}{(Killed ? ":KILL" : "")}";
    }

    public sealed class SkillDefinition
    {
        public int Id;
        public string Name = "";
        public ClassMask Classes = ClassMask.All;
        public int ManaCost;
        public int FlatDamage;
        public int DamagePercent = 100;
        public bool Magic;
        public int Range = 2;
        public int ReqLevel = 1;
        public int ReqEnergy;
    }

    /// <summary>Skills originais deste protótipo (nomes/valores próprios).</summary>
    public static class SkillCatalog
    {
        public static readonly SkillDefinition BasicAttack = new SkillDefinition { Id = 0, Name = "Ataque Básico", Range = 2 };
        public static readonly SkillDefinition[] All =
        {
            BasicAttack,
            new SkillDefinition { Id = 1, Name = "Lâmina Giratória", Classes = ClassMask.DarkKnight, ManaCost = 9, FlatDamage = 15, DamagePercent = 130, Range = 2, ReqLevel = 1 },
            new SkillDefinition { Id = 2, Name = "Investida Sísmica", Classes = ClassMask.DarkKnight, ManaCost = 20, FlatDamage = 40, DamagePercent = 160, Range = 3, ReqLevel = 30 },
            new SkillDefinition { Id = 3, Name = "Orbe Arcano", Classes = ClassMask.DarkWizard, ManaCost = 3, FlatDamage = 8, Magic = true, Range = 6 },
            new SkillDefinition { Id = 4, Name = "Lança de Chamas", Classes = ClassMask.DarkWizard, ManaCost = 15, FlatDamage = 35, DamagePercent = 120, Magic = true, Range = 6, ReqLevel = 20, ReqEnergy = 60 },
            new SkillDefinition { Id = 5, Name = "Flecha Tripla", Classes = ClassMask.FairyElf, ManaCost = 6, FlatDamage = 10, DamagePercent = 115, Range = 7 },
        };
        public static SkillDefinition? Find(int id) => Array.Find(All, s => s.Id == id);
    }

    public static class CombatResolver
    {
        /// <summary>Chance de acerto = AR/(AR+DR), limitada entre 5% e 95%.</summary>
        public static int HitChancePercent(int attackRate, int defenseRate)
        {
            if (attackRate + defenseRate <= 0) return 95;
            int c = (int)(100L * attackRate / (attackRate + defenseRate));
            return Math.Clamp(c, 5, 95);
        }

        public static HitResult Resolve(ICombatant attacker, ICombatant defender, SkillDefinition skill, IRandom rng)
        {
            var a = attacker.GetCombatStats();
            var d = defender.GetCombatStats();
            var res = new HitResult();

            if (!rng.Roll(HitChancePercent(a.AttackRate, d.DefenseRate)))
            {
                res.Type = HitType.Miss;
                return res;
            }

            int min = skill.Magic ? a.MinMagic : a.MinDamage;
            int max = skill.Magic ? a.MaxMagic : a.MaxDamage;
            if (max < min) max = min;

            int dmg;
            if (rng.Roll(a.ExcellentChancePercent)) { res.Type = HitType.Excellent; dmg = max * 120 / 100; }
            else if (rng.Roll(a.CriticalChancePercent)) { res.Type = HitType.Critical; dmg = max; }
            else { res.Type = HitType.Normal; dmg = rng.Next(min, max + 1); }

            dmg = dmg * skill.DamagePercent / 100 + skill.FlatDamage;
            dmg -= d.Defense;
            dmg -= dmg * d.DamageReductionPercent / 100;
            int floor = Math.Max(1, a.Level / 10);
            if (dmg < floor) dmg = floor;

            res.Damage = dmg;
            defender.Hp = Math.Max(0, defender.Hp - dmg);
            res.Killed = defender.Hp == 0;
            return res;
        }
    }
}
