using System;
using System.Collections.Generic;
using Kezor.Core.Combat;

namespace Kezor.Core.World
{
    public sealed class MonsterDefinition
    {
        public int Id;
        public string Name = "";
        public int Level, Hp, MinDamage, MaxDamage, Defense, AttackRate, DefenseRate;
        public long Experience;
        public float RespawnSeconds = 10f;
        public int GoldDropMax;
        public bool Aggressive = true;
        public float AggroRange = 6f;
        public float AttackRange = 1.8f;
        public float AttackInterval = 1.6f;
        public float MoveSpeed = 3.2f;
        public float Scale = 1f;
        public bool Boss;
        /// <summary>Drops garantidos de runas (bosses de evento).</summary>
        public int GuaranteedRunes;
        /// <summary>Chefes: um destes itens cai sempre (sorteado).</summary>
        public int[] SpecialDrops = Array.Empty<int>();
        /// <summary>Flutua (mariposa, espectro): o cliente anima como o Arauto.</summary>
        public bool Hover;
    }

    /// <summary>Bestiário original de Kezor.</summary>
    public static class MonsterCatalog
    {
        public static readonly MonsterDefinition MossSlime = new MonsterDefinition { Id = 1, Name = "Lodo Musgoso", Level = 2, Hp = 60, MinDamage = 4, MaxDamage = 7, Defense = 1, AttackRate = 10, DefenseRate = 3, Experience = 40, RespawnSeconds = 5, GoldDropMax = 200, Aggressive = false, MoveSpeed = 1.6f, AttackInterval = 2f };
        public static readonly MonsterDefinition AshWolf = new MonsterDefinition { Id = 2, Name = "Lobo Cinzento", Level = 8, Hp = 180, MinDamage = 11, MaxDamage = 16, Defense = 5, AttackRate = 40, DefenseRate = 9, Experience = 160, RespawnSeconds = 8, GoldDropMax = 600, MoveSpeed = 4.2f, AttackInterval = 1.3f };
        public static readonly MonsterDefinition Weaver = new MonsterDefinition { Id = 3, Name = "Aranha Tecelã", Level = 14, Hp = 330, MinDamage = 20, MaxDamage = 28, Defense = 10, AttackRate = 75, DefenseRate = 16, Experience = 380, RespawnSeconds = 9, GoldDropMax = 1100, MoveSpeed = 3.6f };
        public static readonly MonsterDefinition Wanderer = new MonsterDefinition { Id = 4, Name = "Ossada Errante", Level = 22, Hp = 620, MinDamage = 34, MaxDamage = 46, Defense = 18, AttackRate = 120, DefenseRate = 25, Experience = 900, RespawnSeconds = 10, GoldDropMax = 2000, MoveSpeed = 3f };
        public static readonly MonsterDefinition BasaltGolem = new MonsterDefinition { Id = 5, Name = "Golem de Basalto", Level = 35, Hp = 1500, MinDamage = 60, MaxDamage = 80, Defense = 35, AttackRate = 180, DefenseRate = 40, Experience = 2600, RespawnSeconds = 20, GoldDropMax = 4000, MoveSpeed = 2.2f, AttackInterval = 2.2f, AttackRange = 2.4f, Scale = 1.6f };
        // Evento: Invasão das Cinzas
        public static readonly MonsterDefinition Ashling = new MonsterDefinition { Id = 50, Name = "Cria das Cinzas", Level = 18, Hp = 400, MinDamage = 25, MaxDamage = 34, Defense = 12, AttackRate = 90, DefenseRate = 18, Experience = 520, RespawnSeconds = -1, GoldDropMax = 1500, MoveSpeed = 4f, AggroRange = 10f };
        public static readonly MonsterDefinition AshHerald = new MonsterDefinition { Id = 51, Name = "Arauto das Cinzas", Level = 30, Hp = 6000, MinDamage = 45, MaxDamage = 70, Defense = 25, AttackRate = 160, DefenseRate = 30, Experience = 9000, RespawnSeconds = -1, GoldDropMax = 30000, MoveSpeed = 2.8f, AggroRange = 12f, AttackRange = 2.6f, Scale = 1.9f, Boss = true, GuaranteedRunes = 3 };

        // Pântano de Turfa Negra (nível 20-45)
        public static readonly MonsterDefinition PeatCrawler = new MonsterDefinition { Id = 6, Name = "Rastejante da Turfa", Level = 24, Hp = 750, MinDamage = 38, MaxDamage = 50, Defense = 20, AttackRate = 130, DefenseRate = 27, Experience = 1100, RespawnSeconds = 9, GoldDropMax = 2300, MoveSpeed = 2.6f };
        public static readonly MonsterDefinition ReedHag = new MonsterDefinition { Id = 7, Name = "Bruxa do Junco", Level = 30, Hp = 980, MinDamage = 48, MaxDamage = 62, Defense = 24, AttackRate = 155, DefenseRate = 32, Experience = 1600, RespawnSeconds = 10, GoldDropMax = 2800, MoveSpeed = 3.0f, AttackRange = 2.4f };
        public static readonly MonsterDefinition DrownedGhoul = new MonsterDefinition { Id = 8, Name = "Carniçal Afogado", Level = 36, Hp = 1300, MinDamage = 58, MaxDamage = 74, Defense = 30, AttackRate = 180, DefenseRate = 38, Experience = 2200, RespawnSeconds = 10, GoldDropMax = 3400, MoveSpeed = 3.4f };
        public static readonly MonsterDefinition WispMoth = new MonsterDefinition { Id = 9, Name = "Mariposa Fátua", Level = 42, Hp = 1500, MinDamage = 66, MaxDamage = 84, Defense = 32, AttackRate = 200, DefenseRate = 48, Experience = 2800, RespawnSeconds = 11, GoldDropMax = 3800, MoveSpeed = 4.4f, Hover = true };
        // Cripta de Vidro Negro (nível 45-80)
        public static readonly MonsterDefinition GlassSentinel = new MonsterDefinition { Id = 10, Name = "Sentinela de Vidro", Level = 50, Hp = 2400, MinDamage = 85, MaxDamage = 105, Defense = 45, AttackRate = 240, DefenseRate = 55, Experience = 4200, RespawnSeconds = 12, GoldDropMax = 5200, MoveSpeed = 2.6f, AttackRange = 2.2f, Scale = 1.2f };
        public static readonly MonsterDefinition ObsidianSpider = new MonsterDefinition { Id = 11, Name = "Aranha de Obsidiana", Level = 58, Hp = 2900, MinDamage = 100, MaxDamage = 124, Defense = 52, AttackRate = 280, DefenseRate = 66, Experience = 5600, RespawnSeconds = 12, GoldDropMax = 6000, MoveSpeed = 4.0f, Scale = 1.3f };
        public static readonly MonsterDefinition GlassWraith = new MonsterDefinition { Id = 12, Name = "Espectro Vítreo", Level = 66, Hp = 3300, MinDamage = 115, MaxDamage = 140, Defense = 58, AttackRate = 320, DefenseRate = 80, Experience = 7000, RespawnSeconds = 13, GoldDropMax = 7000, MoveSpeed = 3.8f, Hover = true };
        public static readonly MonsterDefinition BlackGlassSovereign = new MonsterDefinition { Id = 52, Name = "Soberano de Vidro Negro", Level = 78, Hp = 30000, MinDamage = 160, MaxDamage = 210, Defense = 75, AttackRate = 420, DefenseRate = 95, Experience = 60000, RespawnSeconds = 240, GoldDropMax = 80000, MoveSpeed = 2.6f, AggroRange = 9f, AttackRange = 3f, AttackInterval = 1.8f, Scale = 2.0f, Boss = true, GuaranteedRunes = 2,
            SpecialDrops = new[] { 40, 41, 42, 300, 301 } };

        // Ecos da Invocadora (aliados, temporários). Os números são a base; a força escala com a energia de quem invocou.
        public static readonly MonsterDefinition ShadowEcho = new MonsterDefinition { Id = 60, Name = "Eco Sombrio", Level = 1, Hp = 300, MinDamage = 10, MaxDamage = 16, Defense = 8, AttackRate = 60, DefenseRate = 15, RespawnSeconds = -1, MoveSpeed = 5.5f, AttackInterval = 1.2f, AttackRange = 2f, Hover = true, Aggressive = false };
        public static readonly MonsterDefinition LuminousEcho = new MonsterDefinition { Id = 61, Name = "Eco Luminoso", Level = 1, Hp = 700, MinDamage = 8, MaxDamage = 13, Defense = 20, AttackRate = 60, DefenseRate = 25, RespawnSeconds = -1, MoveSpeed = 5f, AttackInterval = 1.4f, AttackRange = 2.2f, Hover = true, Aggressive = false, Scale = 1.15f };

        public static readonly MonsterDefinition[] All = { MossSlime, AshWolf, Weaver, Wanderer, BasaltGolem, Ashling, AshHerald,
            PeatCrawler, ReedHag, DrownedGhoul, WispMoth, GlassSentinel, ObsidianSpider, GlassWraith, BlackGlassSovereign, ShadowEcho, LuminousEcho };
        public static MonsterDefinition? Get(int id) => Array.Find(All, m => m.Id == id);
    }

    public enum MonsterState { Idle, Wander, Chase, Return }

    public sealed class Monster : ICombatant
    {
        public const float LeashRange = 16f;

        public int Id { get; }
        public MonsterDefinition Def { get; }
        public string Name => Def.Name;
        public int Level => Def.Level;
        public int Hp { get; set; }
        public int MaxHp => (int)(Def.Hp * Power);
        public bool IsDead => Hp <= 0;
        public float X { get; set; }
        public float Z { get; set; }
        public int MapId { get; set; } = 1;
        public float SpawnX, SpawnZ, WanderRadius = 5f;
        public float RespawnTimer;
        public MonsterState State = MonsterState.Idle;
        public int TargetId;              // id do personagem perseguido
        public float WanderX, WanderZ, StateTimer, AttackTimer;
        /// <summary>Contribuição de dano por personagem (para exp/loot).</summary>
        public readonly Dictionary<int, int> DamageBy = new Dictionary<int, int>();
        /// <summary>Monstros de evento não renascem: são removidos ao morrer.</summary>
        public bool Temporary => Def.RespawnSeconds < 0;
        /// <summary>Caminho do A* na perseguição/retorno (null = linha reta) e o destino para o qual foi calculado.</summary>
        public List<(float x, float z)>? Path;
        public float PathTx = float.NaN, PathTz = float.NaN;
        public double PathAt;
        /// <summary>Eco invocado: id do personagem dono (0 = monstro comum), quando some e multiplicador de força/vida.</summary>
        public int OwnerId;
        public double ExpiresAt;
        public float Power = 1f;
        public bool IsSummon => OwnerId != 0;
        /// <summary>Maldições: lentidão até SlowUntil e dano por segundo até DotUntil (crédito para DotOwner).</summary>
        public int SlowPercent; public double SlowUntil;
        public int DotPerSecond, DotOwner; public double DotUntil, DotNext;
        public float SpeedFactor(double now) => now < SlowUntil ? 1f - SlowPercent / 100f : 1f;

        public Monster(int id, MonsterDefinition def, float x, float z)
        {
            Id = id; Def = def; X = SpawnX = WanderX = x; Z = SpawnZ = WanderZ = z; Hp = def.Hp;
        }

        public CombatStats GetCombatStats() => new CombatStats
        {
            Level = Def.Level, MinDamage = (int)(Def.MinDamage * Power), MaxDamage = (int)(Def.MaxDamage * Power),
            Defense = (int)(Def.Defense * Power), AttackRate = (int)(Def.AttackRate * Power), DefenseRate = (int)(Def.DefenseRate * Power)
        };

        public void Respawn()
        {
            Hp = Def.Hp; X = SpawnX; Z = SpawnZ; DamageBy.Clear();
            State = MonsterState.Idle; TargetId = 0; StateTimer = 1f; Path = null;
        }

        /// <summary>Move em direção a (tx,tz) por no máximo speed*dt. Retorna true se chegou.</summary>
        public bool StepTowards(float tx, float tz, float speed, float dt, float stopAt = 0.05f)
        {
            float dx = tx - X, dz = tz - Z;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d <= stopAt) return true;
            float step = Math.Min(speed * dt, d - stopAt);
            X += dx / d * step; Z += dz / d * step;
            return d - step <= stopAt + 0.001f;
        }

        public int TopDamager()
        {
            int best = 0, id = 0;
            foreach (var kv in DamageBy) if (kv.Value > best) { best = kv.Value; id = kv.Key; }
            return id;
        }
    }
}
