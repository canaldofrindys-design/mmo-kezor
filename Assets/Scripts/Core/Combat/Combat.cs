using System;
using Kezor.Core.Characters;

namespace Kezor.Core.Combat
{
    public sealed class CombatStats
    {
        public int Level;
        public int MinDamage, MaxDamage, MinMagic, MaxMagic;
        public int Defense, AttackRate, DefenseRate;
        /// <summary>Herói: soma da defesa (AGI + itens + bônus) antes de dividir por 2 (a Defense final do MU é a metade).</summary>
        public int DefenseBase;
        public int CriticalChancePercent, SurgeChancePercent, DamageReductionPercent;
        /// <summary>Dano a mais em % (asas).</summary>
        public int DamageBonusPercent;
        /// <summary>Opções excelentes: recupera vida/mana ao matar (máx/8 por opção), reflete % do dano, +% de zen.</summary>
        public int LifeOnKill, ManaOnKill, ReflectPercent, ZenBonusPercent;
        /// <summary>Velocidade de ataque (MU) e multiplicador das skills da classe.</summary>
        public int AttackSpeed;
        public float SkillMultiplier = 1f;
        /// <summary>Herói (PvP usa o Escudo; o fator 0,3 de "taxa de defesa maior" só vale contra monstros).</summary>
        public bool IsPlayer;
    }

    /// <summary>Quem tem Escudo (heróis): em golpes de jogadores, 90% vai no escudo e o resto na vida (SD do MU).</summary>
    public interface IShieldBearer
    {
        /// <summary>Absorve a parte do escudo e devolve o que vai para a vida.</summary>
        int AbsorbShield(int damage, bool fromPlayer);
    }

    public interface ICombatant
    {
        int Id { get; }
        string Name { get; }
        int Level { get; }
        int Hp { get; set; }
        int MaxHp { get; }
        bool IsDead { get; }
        float X { get; }
        float Z { get; }
        /// <summary>Mapa onde está (1 = Vale de Brasalume).</summary>
        int MapId { get; }
        CombatStats GetCombatStats();
    }

    /// <summary>Miss, Normal, Critical (dano máximo, via Sorte), Surge (dano máximo +20%, via opção Ascendente), Combo.</summary>
    public enum HitType { Miss, Normal, Critical, Surge, Combo }

    public struct HitResult
    {
        public HitType Type;
        public int Damage;
        public bool Killed;
        public override string ToString() => Type == HitType.Miss ? "MISS" : $"{Type}:{Damage}{(Killed ? ":KILL" : "")}";
    }

    /// <summary>Tipo de skill: dano no alvo, buff em si mesmo, estandarte (aura de grupo no chão) ou invocação.</summary>
    public enum SkillKind { Damage, SelfBuff, Banner, Summon }

    /// <summary>Efeito de buff/aura: % de dano, defesa (pontos) + taxa de defesa (%), ou % de velocidade.</summary>
    public enum BuffKind { None, Damage, Defense, Speed }

    public sealed class SkillDefinition
    {
        public int Id;
        public string Name = "";
        public string Description = "";
        public ClassMask Classes = ClassMask.All;
        public int ManaCost;
        public int FlatDamage;
        /// <summary>Multiplicador final do golpe (%): 100 em todas, menos nas de vários golpes (cada golpe leva uma parte).</summary>
        public int DamagePercent = 100;
        public bool Magic;
        /// <summary>Alcance em unidades do mundo. 0 = usa o alcance básico da classe.</summary>
        public float Range;
        /// <summary>Raio de área em volta do alvo (0 = alvo único).</summary>
        public float AreaRadius;
        public int ReqLevel = 1;
        public int ReqEnergy;
        /// <summary>Conta para a sequência de combo do Guardião.</summary>
        public bool ComboStarter;
        /// <summary>Lâmina Arcana: o dano soma o físico e o mágico.</summary>
        public bool Hybrid;
        public SkillKind Kind = SkillKind.Damage;
        public BuffKind Buff;
        /// <summary>Valor do buff/aura (% ou pontos) e duração (s). Estandartes: raio = AuraRadius.</summary>
        public int BuffValue;
        public float Duration;
        public float AuraRadius = 6f;
        /// <summary>Recarga própria da skill (s), além do intervalo entre golpes.</summary>
        public float Cooldown;
        /// <summary>Maldições: lentidão (%) e dano ao longo do tempo (% do dano mágico por segundo).</summary>
        public int SlowPercent;
        public float SlowSeconds;
        public int DotPercent;
        public float DotSeconds;
        /// <summary>Golpes por uso (Rajada de Punhos).</summary>
        public int Hits = 1;
        /// <summary>Invocação: id do MonsterDefinition do eco e quantos podem existir ao mesmo tempo.</summary>
        public int SummonId;
        public int MaxSummons = 2;
        /// <summary>Buff de dano extra junto do buff principal (Passo da Chama: velocidade + dano).</summary>
        public int ExtraDamageBuff;
        public bool NeedsTarget => Kind == SkillKind.Damage;
        /// <summary>AG (Vigor) gasto: as skills iniciais (nível 1) não gastam, como no MU; as demais gastam <see cref="Character.SkillVigorCost"/>.</summary>
        public int VigorCost => Id == 0 || ReqLevel <= 1 ? 0 : Character.SkillVigorCost;
    }

    /// <summary>Skills originais de Kezor.</summary>
    public static class SkillCatalog
    {
        public static readonly SkillDefinition BasicAttack = new SkillDefinition { Id = 0, Name = "Ataque", Description = "Ataque básico com a arma equipada." };
        public static readonly SkillDefinition[] All =
        {
            BasicAttack,
            new SkillDefinition { Id = 1, Name = "Corte Rúnico", Description = "Golpe que inflama as runas da lâmina.", Classes = ClassMask.Warden, ManaCost = 8, FlatDamage = 15, Range = 2.6f, ComboStarter = true },
            new SkillDefinition { Id = 2, Name = "Ciclone de Aço", Description = "Giro que atinge todos à volta do alvo.", Classes = ClassMask.Warden, ManaCost = 18, FlatDamage = 30, Range = 2.6f, AreaRadius = 3f, ReqLevel = 20, ComboStarter = true },
            new SkillDefinition { Id = 3, Name = "Dardo Etéreo", Description = "Projétil de energia do Véu.", Classes = ClassMask.Arcanist, ManaCost = 3, FlatDamage = 8, Magic = true, Range = 7.5f },
            new SkillDefinition { Id = 4, Name = "Chuva de Brasas", Description = "Brasas caem numa área em volta do alvo.", Classes = ClassMask.Arcanist, ManaCost = 16, FlatDamage = 30, Magic = true, Range = 8f, AreaRadius = 3.5f, ReqLevel = 15, ReqEnergy = 50 },
            new SkillDefinition { Id = 5, Name = "Disparo Perfurante", Description = "Flecha pesada que atravessa armaduras.", Classes = ClassMask.Ranger, ManaCost = 5, FlatDamage = 10, Range = 8.5f },
            new SkillDefinition { Id = 6, Name = "Saraivada", Description = "Uma chuva de flechas numa área.", Classes = ClassMask.Ranger, ManaCost = 14, FlatDamage = 20, Range = 8.5f, AreaRadius = 3f, ReqLevel = 15 },

            // Lâmina Arcana (dano híbrido: força + energia)
            new SkillDefinition { Id = 10, Name = "Fio Encantado", Description = "Corte com a lâmina tomada de runas.", Classes = ClassMask.Bladeweaver, ManaCost = 6, FlatDamage = 12, Range = 2.6f, Hybrid = true },
            new SkillDefinition { Id = 11, Name = "Onda Arcana", Description = "Onda curta de energia que atinge tudo em volta do alvo.", Classes = ClassMask.Bladeweaver, ManaCost = 12, FlatDamage = 20, Range = 4f, AreaRadius = 2.5f, Hybrid = true },
            new SkillDefinition { Id = 12, Name = "Lâmina Ressonante", Description = "Encanta a espada: +15% de dano por 15 s.", Classes = ClassMask.Bladeweaver, ManaCost = 20, Kind = SkillKind.SelfBuff, Buff = BuffKind.Damage, BuffValue = 15, Duration = 15f, Cooldown = 20f, ReqLevel = 10 },
            new SkillDefinition { Id = 13, Name = "Corte Estelar", Description = "Golpe pesado que rasga com luz das estrelas.", Classes = ClassMask.Bladeweaver, ManaCost = 18, FlatDamage = 25, Range = 2.6f, Hybrid = true, ReqLevel = 20 },
            new SkillDefinition { Id = 14, Name = "Maré de Lâminas", Description = "Redemoinho de lâminas arcanas em área.", Classes = ClassMask.Bladeweaver, ManaCost = 28, FlatDamage = 40, Range = 3f, AreaRadius = 3.5f, Hybrid = true, ReqLevel = 40 },
            new SkillDefinition { Id = 15, Name = "Eclipse da Lâmina", Description = "Corte devastador que deixa o alvo lento.", Classes = ClassMask.Bladeweaver, ManaCost = 40, FlatDamage = 50, Range = 2.6f, Hybrid = true, SlowPercent = 30, SlowSeconds = 3f, ReqLevel = 80 },

            // Comandante do Estandarte (estandartes: auras de grupo; ordens: ataques em área)
            new SkillDefinition { Id = 16, Name = "Estocada de Lança", Description = "Estocada longa com a lança.", Classes = ClassMask.Commander, ManaCost = 5, FlatDamage = 12, Range = 3.1f },
            new SkillDefinition { Id = 17, Name = "Estandarte da Fúria", Description = "Finca um estandarte: o grupo perto dele causa +15% de dano (+1% a cada 50 de energia).", Classes = ClassMask.Commander, ManaCost = 25, Kind = SkillKind.Banner, Buff = BuffKind.Damage, BuffValue = 15, Duration = 20f, Cooldown = 6f },
            new SkillDefinition { Id = 18, Name = "Ordem: Investida!", Description = "Ordem de ataque: golpe em área em volta do alvo.", Classes = ClassMask.Commander, ManaCost = 14, FlatDamage = 20, Range = 3.1f, AreaRadius = 3f, ReqLevel = 10 },
            new SkillDefinition { Id = 19, Name = "Estandarte da Muralha", Description = "Finca um estandarte: o grupo perto dele ganha +20 de defesa e +10% de taxa de defesa.", Classes = ClassMask.Commander, ManaCost = 25, Kind = SkillKind.Banner, Buff = BuffKind.Defense, BuffValue = 20, Duration = 20f, Cooldown = 6f, ReqLevel = 20 },
            new SkillDefinition { Id = 20, Name = "Estandarte do Vento", Description = "Finca um estandarte: o grupo perto dele anda 20% mais rápido.", Classes = ClassMask.Commander, ManaCost = 25, Kind = SkillKind.Banner, Buff = BuffKind.Speed, BuffValue = 20, Duration = 20f, Cooldown = 6f, ReqLevel = 40 },
            new SkillDefinition { Id = 21, Name = "Ordem: Cerco de Ferro", Description = "Ordem de cerco: dano em área grande e lentidão.", Classes = ClassMask.Commander, ManaCost = 35, FlatDamage = 60, Range = 3.1f, AreaRadius = 4f, SlowPercent = 25, SlowSeconds = 3f, ReqLevel = 80 },

            // Invocadora de Ecos (ecos aliados e maldições)
            new SkillDefinition { Id = 22, Name = "Sussurro do Eco", Description = "Um sussurro cortante de longe.", Classes = ClassMask.Invoker, ManaCost = 4, FlatDamage = 8, Magic = true, Range = 7f },
            new SkillDefinition { Id = 23, Name = "Eco Sombrio", Description = "Chama um eco que luta por você por 30 s (até 2).", Classes = ClassMask.Invoker, ManaCost = 30, Kind = SkillKind.Summon, SummonId = 60, Duration = 30f, Cooldown = 4f, MaxSummons = 2 },
            new SkillDefinition { Id = 24, Name = "Maldição do Peso", Description = "Deixa o alvo 40% mais lento por 5 s.", Classes = ClassMask.Invoker, ManaCost = 10, FlatDamage = 10, Magic = true, Range = 7f, SlowPercent = 40, SlowSeconds = 5f, ReqLevel = 10 },
            new SkillDefinition { Id = 25, Name = "Chaga Ecoante", Description = "Ferida que corrói: dano agora e a cada segundo por 6 s.", Classes = ClassMask.Invoker, ManaCost = 14, FlatDamage = 10, Magic = true, Range = 7f, DotPercent = 30, DotSeconds = 6f, ReqLevel = 20 },
            new SkillDefinition { Id = 26, Name = "Eco Luminoso", Description = "Chama um eco guardião mais resistente por 30 s (até 2).", Classes = ClassMask.Invoker, ManaCost = 40, Kind = SkillKind.Summon, SummonId = 61, Duration = 30f, Cooldown = 4f, MaxSummons = 2, ReqLevel = 40 },
            new SkillDefinition { Id = 27, Name = "Coro dos Ecos", Description = "Coro de vozes em área que deixa uma chaga.", Classes = ClassMask.Invoker, ManaCost = 38, FlatDamage = 50, Magic = true, Range = 7f, AreaRadius = 4f, DotPercent = 20, DotSeconds = 4f, ReqLevel = 80 },

            // Punho de Brasa (golpes rápidos que alimentam o Fluxo de Brasa)
            new SkillDefinition { Id = 28, Name = "Jab de Brasa", Description = "Soco curto e rápido.", Classes = ClassMask.EmberFist, ManaCost = 3, FlatDamage = 6, Range = 2f },
            new SkillDefinition { Id = 29, Name = "Gancho Ígneo", Description = "Gancho que levanta faíscas.", Classes = ClassMask.EmberFist, ManaCost = 7, FlatDamage = 14, Range = 2f },
            new SkillDefinition { Id = 30, Name = "Passo da Chama", Description = "+15% de dano e +25% de velocidade por 10 s.", Classes = ClassMask.EmberFist, ManaCost = 15, Kind = SkillKind.SelfBuff, Buff = BuffKind.Speed, BuffValue = 25, ExtraDamageBuff = 15, Duration = 10f, Cooldown = 15f, ReqLevel = 10 },
            new SkillDefinition { Id = 31, Name = "Rajada de Punhos", Description = "Três socos seguidos (cada um conta para o Fluxo).", Classes = ClassMask.EmberFist, ManaCost = 14, FlatDamage = 4, DamagePercent = 50, Range = 2f, Hits = 3, ReqLevel = 20 },
            new SkillDefinition { Id = 32, Name = "Coluna de Cinzas", Description = "Golpe no chão que ergue cinzas em volta do alvo.", Classes = ClassMask.EmberFist, ManaCost = 22, FlatDamage = 40, Range = 2f, AreaRadius = 2.5f, ReqLevel = 40 },
            new SkillDefinition { Id = 33, Name = "Fúria do Núcleo", Description = "Um soco com todo o calor do núcleo.", Classes = ClassMask.EmberFist, ManaCost = 34, FlatDamage = 50, Range = 2f, ReqLevel = 80 },
        };
        public static SkillDefinition? Find(int id) => Array.Find(All, s => s.Id == id);
        public static float RangeOf(SkillDefinition s, CharacterClass c) => s.Range > 0 ? s.Range : ClassDefinition.Get(c).BasicRange;
    }

    /// <summary>
    /// Combo do Guardião Rúnico: 3 skills de combo seguidas, com pelo menos 2 diferentes, em até 3 s, disparam
    /// um golpe extra de (FOR + AGI + ENE) / 2 (bônus de combo do MU). Disponível a partir do nível 10.
    /// </summary>
    public sealed class ComboTracker
    {
        public const int MinLevel = 10;
        public const double Window = 3.0;
        private readonly System.Collections.Generic.List<(int id, double t)> _seq = new System.Collections.Generic.List<(int, double)>();

        /// <summary>Registra o uso de uma skill de combo; retorna true se fechou um combo.</summary>
        public bool Register(int skillId, double time)
        {
            _seq.Add((skillId, time));
            _seq.RemoveAll(e => time - e.t > Window);
            if (_seq.Count >= 3)
            {
                int n = _seq.Count;
                var a = _seq[n - 3].id; var b = _seq[n - 2].id; var c = _seq[n - 1].id;
                if (!(a == b && b == c)) { _seq.Clear(); return true; }
                _seq.RemoveAt(0);
            }
            return false;
        }
        public void Reset() => _seq.Clear();
        public static int ComboDamage(Attributes s) => (s.Strength + s.Agility + s.Energy) / 2;
    }

    /// <summary>
    /// Fluxo de Brasa (Punho de Brasa, nível 10+): cada acerto no mesmo alvo em até 1,5 s do anterior soma uma brasa;
    /// na 5ª brasa sai o Golpe do Fluxo, um golpe extra de (FOR + AGI) × 0,8, e o contador zera. Trocar de alvo ou demorar zera.
    /// </summary>
    public sealed class EmberFlow
    {
        public const int MinLevel = 10, Needed = 5;
        public const double Gap = 1.5;
        private int _target, _count;
        private double _last = -99;
        public int Embers => _count;

        public bool Register(int targetId, double time)
        {
            if (targetId != _target || time - _last > Gap) _count = 0;
            _target = targetId; _last = time;
            if (++_count < Needed) return false;
            _count = 0;
            return true;
        }

        public void Reset() { _count = 0; _target = 0; }
        public static int FlowDamage(Attributes s) => (s.Strength + s.Agility) * 4 / 5;
    }

    /// <summary>
    /// Cálculo de golpe do MU Online (conferido no OpenMU, AttackableExtensions.CalculateDamageAsync):
    /// 1) acerto: se TA > TD, chance = 1 − TD/TA; senão 3%;
    /// 2) dano base = atributos + arma; a skill soma de "dano" a "dano × 1,5";
    /// 3) excelente = máximo × 1,2; crítico = máximo; senão sorteio entre mínimo e máximo;
    /// 4) tira a defesa do alvo; contra monstro com TD maior que a TA do atacante, × 0,3;
    /// 5) piso de máx(1, nível/10); asas e buffs (+%), absorção do alvo (−%);
    /// 6) skill: × multiplicador da classe (Guardião 200% + ENE/10%, ...).
    /// </summary>
    public static class CombatResolver
    {
        /// <summary>Chance de acerto quando a taxa de defesa alcança a de ataque.</summary>
        public const int MinHitPerMil = 30;

        /// <summary>Chance de acerto (por mil): 1 − TD/TA se TA > TD, senão 3%.</summary>
        public static int HitChancePerMil(int attackRate, int defenseRate)
        {
            if (defenseRate < attackRate && attackRate > 0) return (int)(1000L - 1000L * defenseRate / attackRate);
            return MinHitPerMil;
        }

        /// <summary>Chance de acerto em % (arredondada para baixo, para a tela).</summary>
        public static int HitChancePercent(int attackRate, int defenseRate) => HitChancePerMil(attackRate, defenseRate) / 10;

        public static HitResult Resolve(ICombatant attacker, ICombatant defender, SkillDefinition skill, IRandom rng)
        {
            var a = attacker.GetCombatStats();
            var d = defender.GetCombatStats();
            var res = new HitResult();

            if (!rng.RollPerMil(HitChancePerMil(a.AttackRate, d.DefenseRate)))
            {
                res.Type = HitType.Miss;
                return res;
            }

            bool magic = skill.Magic;
            int min = skill.Hybrid ? a.MinDamage + a.MinMagic : magic ? a.MinMagic : a.MinDamage;
            int max = skill.Hybrid ? a.MaxDamage + a.MaxMagic : magic ? a.MaxMagic : a.MaxDamage;
            bool isSkill = skill.Id != 0;
            if (isSkill) { min += skill.FlatDamage; max += skill.FlatDamage + skill.FlatDamage / 2; }
            if (max < min) max = min;

            int dmg;
            if (rng.Roll(a.SurgeChancePercent)) { res.Type = HitType.Surge; dmg = max * 120 / 100; }
            else if (rng.Roll(a.CriticalChancePercent)) { res.Type = HitType.Critical; dmg = max; }
            else { res.Type = HitType.Normal; dmg = rng.Next(min, max + 1); }

            dmg -= d.Defense;
            bool pvp = a.IsPlayer && d.IsPlayer;
            if (!pvp && d.DefenseRate > a.AttackRate) dmg = dmg * 3 / 10;
            res.Damage = Apply(a, d, defender, dmg, out res.Killed, isSkill ? a.SkillMultiplier * skill.DamagePercent / 100f : skill.DamagePercent / 100f, defenseApplied: true);
            return res;
        }

        /// <summary>
        /// Aplica dano bruto: tira a defesa (se ainda não tirou), piso de máx(1, nível/10), +% de dano do atacante,
        /// −% de absorção do alvo (máx. 80%), multiplicador de skill e, contra heróis, o Escudo nos golpes de jogadores.
        /// </summary>
        public static int Apply(CombatStats a, CombatStats d, ICombatant defender, int raw, out bool killed, float multiplier = 1f, bool defenseApplied = false)
        {
            int dmg = defenseApplied ? raw : raw - d.Defense;
            int floor = Math.Max(1, a.Level / 10);
            if (dmg < floor) dmg = floor;
            if (a.DamageBonusPercent > 0) dmg = dmg * (100 + a.DamageBonusPercent) / 100;
            if (dmg > 1) dmg -= dmg * Math.Min(d.DamageReductionPercent, 80) / 100; // absorção máxima de 80%
            if (multiplier != 1f) dmg = Math.Max(1, (int)(dmg * multiplier));
            int toHp = defender is IShieldBearer sb ? sb.AbsorbShield(dmg, a.IsPlayer) : dmg;   // SD só em PvP
            defender.Hp = Math.Max(0, defender.Hp - toHp);
            killed = defender.Hp == 0;
            return dmg;
        }
    }
}
