using System;
using System.Collections.Generic;
using MuProto.Core.Combat;
using MuProto.Core.Items;

namespace MuProto.Core.World
{
    public sealed class MonsterDefinition
    {
        public int Id;
        public string Name = "";
        public int Level, Hp, MinDamage, MaxDamage, Defense, AttackRate, DefenseRate;
        public long Experience;
        public float RespawnSeconds = 10f;
        public int ZenDropMax;
    }

    public static class MonsterCatalog
    {
        public static readonly MonsterDefinition Slime = new MonsterDefinition { Id = 1, Name = "Gosma Verde", Level = 2, Hp = 60, MinDamage = 4, MaxDamage = 7, Defense = 1, AttackRate = 10, DefenseRate = 3, Experience = 40, RespawnSeconds = 5, ZenDropMax = 200 };
        public static readonly MonsterDefinition Wolf = new MonsterDefinition { Id = 2, Name = "Lobo Sombrio", Level = 12, Hp = 260, MinDamage = 16, MaxDamage = 24, Defense = 8, AttackRate = 60, DefenseRate = 12, Experience = 280, RespawnSeconds = 8, ZenDropMax = 900 };
        public static readonly MonsterDefinition Golem = new MonsterDefinition { Id = 3, Name = "Golem de Basalto", Level = 35, Hp = 1500, MinDamage = 60, MaxDamage = 80, Defense = 35, AttackRate = 180, DefenseRate = 40, Experience = 2600, RespawnSeconds = 15, ZenDropMax = 4000 };
    }

    public sealed class Monster : ICombatant
    {
        public int Id { get; }
        public MonsterDefinition Def { get; }
        public string Name => Def.Name;
        public int Level => Def.Level;
        public int Hp { get; set; }
        public int MaxHp => Def.Hp;
        public bool IsDead => Hp <= 0;
        public int X, Y, SpawnX, SpawnY;
        public float RespawnTimer;
        /// <summary>Contribuição de dano por jogador (para exp/loot).</summary>
        public readonly Dictionary<int, int> DamageBy = new Dictionary<int, int>();

        public Monster(int id, MonsterDefinition def, int x, int y)
        {
            Id = id; Def = def; X = SpawnX = x; Y = SpawnY = y; Hp = def.Hp;
        }

        public CombatStats GetCombatStats() => new CombatStats
        {
            Level = Def.Level, MinDamage = Def.MinDamage, MaxDamage = Def.MaxDamage,
            Defense = Def.Defense, AttackRate = Def.AttackRate, DefenseRate = Def.DefenseRate
        };

        public void Respawn() { Hp = Def.Hp; X = SpawnX; Y = SpawnY; DamageBy.Clear(); }
    }

    /// <summary>Tabela de drop: chances em "por mil" para granularidade fina.</summary>
    public static class DropSystem
    {
        public const int ItemChancePerMil = 120;
        public const int JewelChancePerMil = 25;   // dividido entre as 4 jóias
        public const int ExcellentChancePerMil = 10;

        public static List<Item> Roll(MonsterDefinition m, IRandom rng)
        {
            var drops = new List<Item>();
            if (rng.Next(0, 1000) < JewelChancePerMil)
            {
                var jewels = new[] { ItemDatabase.JewelOfChaos, ItemDatabase.JewelOfChaos, ItemDatabase.JewelOfLife, ItemDatabase.JewelOfSoul, ItemDatabase.JewelOfBless };
                drops.Add(new Item(jewels[rng.Next(0, jewels.Length)]));
            }
            if (rng.Next(0, 1000) < ItemChancePerMil)
            {
                var pool = new List<ItemDefinition>();
                foreach (var d in ItemDatabase.All) if (d.IsUpgradable && d.DropLevel <= m.Level + 5) pool.Add(d);
                if (pool.Count > 0)
                {
                    var def = pool[rng.Next(0, pool.Count)];
                    int maxLvl = Math.Min(m.Level / 10, 6);
                    var it = new Item(def, rng.Next(0, maxLvl + 1))
                    {
                        Luck = rng.Roll(10),
                        Skill = def.IsWeapon && rng.Roll(30)
                    };
                    if (rng.Roll(25)) it.AdditionalOption = rng.Next(1, 3);
                    if (rng.Next(0, 1000) < ExcellentChancePerMil)
                    {
                        it.Excellent = def.IsWeapon
                            ? (rng.Roll(50) ? ExcellentOption.ExcellentDamageChance : ExcellentOption.DamagePlusLevel)
                            : (rng.Roll(50) ? ExcellentOption.DefenseSuccessRate : ExcellentOption.DamageReduction);
                    }
                    drops.Add(it);
                }
            }
            return drops;
        }
    }
}
