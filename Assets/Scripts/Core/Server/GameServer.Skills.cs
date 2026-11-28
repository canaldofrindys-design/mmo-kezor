using System;
using System.Collections.Generic;
using System.Linq;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.World;
using static Kezor.Core.Server.Protocol;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Skills sem alvo e efeitos das classes novas: buffs em si mesmo (Lâmina Arcana, Punho de Brasa), estandartes com auras
    /// de grupo (Comandante), ecos invocados com IA própria usando o A* (Invocadora), lentidão e dano ao longo do tempo.
    /// </summary>
    public sealed partial class GameServer
    {
        public const float SummonLeash = 12f, SummonAssistRange = 8f;

        public sealed class Banner
        {
            public int Id, OwnerId, MapId;
            public string OwnerName = "";
            public float X, Z, Radius;
            public BuffKind Kind;
            public int Value;
            public double Expires;
        }

        private readonly Dictionary<int, Banner> _banners = new Dictionary<int, Banner>();
        public IReadOnlyDictionary<int, Banner> Banners => _banners;

        /// <summary>Executa uma skill de buff, estandarte ou invocação (não precisa de alvo).</summary>
        private string UseUtilitySkill(Session s, Character c, SkillDefinition sk)
        {
            if (s.SkillReadyAt.TryGetValue(sk.Id, out var ready) && _time < ready) return $"ERR {sk.Name} recarregando ({ready - _time:0.0} s)";
            if (c.Mp < sk.ManaCost) return "ERR mana insuficiente";
            if (c.Vigor < sk.VigorCost) return "ERR vigor insuficiente";
            if (_time - s.LastAttackTime < c.AttackCooldown) { s.PendingUtility = sk.Id; return $"OK {sk.Name} (na fila)"; }
            switch (sk.Kind)
            {
                case SkillKind.SelfBuff:
                    c.AddBuff(sk.Buff, sk.BuffValue, _time + sk.Duration);
                    if (sk.ExtraDamageBuff > 0) c.AddBuff(BuffKind.Damage, sk.ExtraDamageBuff, _time + sk.Duration);
                    break;
                case SkillKind.Banner:
                {
                    foreach (var old in _banners.Values.Where(b => b.OwnerId == c.Id && b.Kind == sk.Buff).ToList()) _banners.Remove(old.Id);
                    int value = sk.BuffValue + (sk.Buff == BuffKind.Speed ? 0 : c.Stats.Energy / 50);   // a energia fortalece a aura
                    var b = new Banner { Id = _nextEntityId++, OwnerId = c.Id, OwnerName = c.Name, MapId = c.MapId, X = c.X, Z = c.Z, Radius = sk.AuraRadius, Kind = sk.Buff, Value = value, Expires = _time + sk.Duration };
                    _banners[b.Id] = b;
                    break;
                }
                case SkillKind.Summon:
                {
                    var mine = _monsters.Values.Where(m => m.OwnerId == c.Id && !m.IsDead).OrderBy(m => m.ExpiresAt).ToList();
                    if (mine.Count >= sk.MaxSummons) _monsters.Remove(mine[0].Id);       // o eco mais antigo dá lugar ao novo
                    var def = MonsterCatalog.Get(sk.SummonId);
                    if (def == null) return "ERR eco desconhecido";
                    var map = MapOf(c.MapId);
                    float sx = c.X + 1.2f, sz = c.Z;
                    if (map.IsBlocked(sx, sz)) { sx = c.X; sz = c.Z; }
                    var e = Spawn(def, c.MapId, sx, sz);
                    e.OwnerId = c.Id;
                    e.ExpiresAt = _time + sk.Duration;
                    e.Power = 1f + c.Stats.Energy / 110f + c.Level / 100f;
                    e.Hp = e.MaxHp;
                    e.State = MonsterState.Idle;
                    break;
                }
                default: return "ERR skill precisa de alvo";
            }
            c.Mp -= sk.ManaCost;
            c.SpendVigor(sk.VigorCost);
            s.LastAttackTime = _time;
            if (sk.Cooldown > 0) s.SkillReadyAt[sk.Id] = _time + sk.Cooldown;
            BroadcastNear(c.MapId, c.X, c.Z, $"@EV CAST;{c.Id};{sk.Id};{F(c.X)};{F(c.Z)}");
            return $"OK {sk.Name}";
        }

        /// <summary>Lentidão e dano ao longo do tempo de uma skill de dano no monstro atingido.</summary>
        private void ApplyCurses(Character c, Monster m, SkillDefinition sk)
        {
            if (m.IsDead) return;
            if (sk.SlowPercent > 0) { m.SlowPercent = Math.Max(sk.SlowPercent, _time < m.SlowUntil ? m.SlowPercent : 0); m.SlowUntil = _time + sk.SlowSeconds; }
            if (sk.DotPercent > 0)
            {
                var cs = c.GetCombatStats();
                int basis = sk.Magic ? (cs.MinMagic + cs.MaxMagic) / 2 : (cs.MinDamage + cs.MaxDamage) / 2;
                m.DotPerSecond = Math.Max(1, basis * sk.DotPercent / 100);
                m.DotOwner = c.Id;
                m.DotUntil = _time + sk.DotSeconds;
                m.DotNext = _time + 1;
            }
        }

        /// <summary>Dano da chaga a cada segundo (ignora defesa). Retorna true se matou.</summary>
        private bool TickDot(Monster m)
        {
            if (m.DotPerSecond <= 0 || _time >= m.DotUntil + 0.01 || _time < m.DotNext) return false;
            m.DotNext += 1;
            int dmg = Math.Min(m.Hp, m.DotPerSecond);
            m.Hp -= dmg;
            m.DamageBy[m.DotOwner] = (m.DamageBy.TryGetValue(m.DotOwner, out var d) ? d : 0) + dmg;
            BroadcastNear(m.MapId, m.X, m.Z, $"@EV HIT;{m.DotOwner};{m.Id};{(int)HitType.Normal};{dmg};25");
            if (m.Hp > 0) return false;
            var owner = CharById(m.DotOwner);
            if (owner != null) OnMonsterKilled(m, owner);
            else { BroadcastNear(m.MapId, m.X, m.Z, $"@EV DIE;{m.Id}"); if (m.Temporary) _monsters.Remove(m.Id); else m.RespawnTimer = m.Def.RespawnSeconds; }
            return true;
        }

        /// <summary>
        /// IA do eco: some quando o tempo acaba ou o dono sai do mapa; ataca o alvo do dono (ou o inimigo mais perto dele) e
        /// volta para perto do dono quando não há luta. O dano conta como do dono (exp e itens).
        /// </summary>
        private void TickSummon(Monster e, float dt)
        {
            var owner = CharById(e.OwnerId);
            if (owner == null || owner.IsDead || owner.MapId != e.MapId || _time >= e.ExpiresAt) { RemoveSummon(e); return; }
            var map = MapOf(e.MapId);
            e.AttackTimer -= dt;
            if (MathK.Dist(e.X, e.Z, owner.X, owner.Z) > 25f) { e.X = owner.X + 1; e.Z = owner.Z; e.Path = null; }   // ficou muito para trás: alcança

            Monster? target = e.TargetId != 0 && _monsters.TryGetValue(e.TargetId, out var tm) && !tm.IsDead && !tm.IsSummon ? tm : null;
            if (target != null && MathK.Dist(target.X, target.Z, owner.X, owner.Z) > SummonLeash) target = null;
            if (target == null)
            {
                var os = SessionOf(owner.Id);
                if (os != null && os.AutoTargetId != 0 && _monsters.TryGetValue(os.AutoTargetId, out var ot) && !ot.IsDead && !ot.IsSummon && ot.MapId == e.MapId) target = ot;
                else target = _monsters.Values.Where(m => !m.IsDead && !m.IsSummon && m.MapId == e.MapId && !map.InSafeZone(m.X, m.Z)
                                                          && MathK.Dist(m.X, m.Z, owner.X, owner.Z) <= SummonAssistRange
                                                          && (m.State == MonsterState.Chase && m.TargetId == owner.Id || m.DamageBy.ContainsKey(owner.Id)))
                                              .OrderBy(m => MathK.Dist(m.X, m.Z, e.X, e.Z)).FirstOrDefault();
                e.TargetId = target?.Id ?? 0;
            }
            if (target == null)
            {
                if (MathK.Dist(e.X, e.Z, owner.X, owner.Z) > 2.5f) StepMonster(e, map, owner.X, owner.Z, e.Def.MoveSpeed, dt, 1.8f);
                return;
            }
            float d = MathK.Dist(e.X, e.Z, target.X, target.Z);
            if (d > e.Def.AttackRange) { StepMonster(e, map, target.X, target.Z, e.Def.MoveSpeed, dt, e.Def.AttackRange * 0.85f); return; }
            if (e.AttackTimer > 0) return;
            e.AttackTimer = e.Def.AttackInterval;
            var h = CombatResolver.Resolve(e, target, SkillCatalog.BasicAttack, _rng);
            BroadcastNear(e.MapId, target.X, target.Z, $"@EV HIT;{e.Id};{target.Id};{(int)h.Type};{h.Damage};0");
            if (h.Damage > 0) target.DamageBy[owner.Id] = (target.DamageBy.TryGetValue(owner.Id, out var dd) ? dd : 0) + h.Damage;
            if (target.State != MonsterState.Chase && !target.IsDead) { target.State = MonsterState.Chase; target.TargetId = owner.Id; BroadcastAggro(target); }
            if (h.Killed) OnMonsterKilled(target, owner);
        }

        private void RemoveSummon(Monster e)
        {
            _monsters.Remove(e.Id);
            BroadcastNear(e.MapId, e.X, e.Z, $"@EV DIE;{e.Id}");
        }

        /// <summary>Remove estandartes vencidos e recalcula as auras: o maior valor de cada tipo, para o dono e o grupo dele por perto.</summary>
        private void TickBanners()
        {
            foreach (var b in _banners.Values.Where(b => b.Expires <= _time || CharById(b.OwnerId) == null).ToList()) _banners.Remove(b.Id);
            foreach (var s in Online)
            {
                var c = s.Character!;
                c.ExpireBuffs(_time);
                int dmg = 0, def = 0, spd = 0;
                int party = _partyOf.TryGetValue(c.Id, out var pid) ? pid : 0;
                foreach (var b in _banners.Values)
                {
                    if (b.MapId != c.MapId || MathK.Dist(b.X, b.Z, c.X, c.Z) > b.Radius) continue;
                    bool ally = b.OwnerId == c.Id || (party != 0 && _partyOf.TryGetValue(b.OwnerId, out var op) && op == party);
                    if (!ally) continue;
                    switch (b.Kind)
                    {
                        case BuffKind.Damage: dmg = Math.Max(dmg, b.Value); break;
                        case BuffKind.Defense: def = Math.Max(def, b.Value); break;
                        case BuffKind.Speed: spd = Math.Max(spd, b.Value); break;
                    }
                }
                c.AuraDamagePercent = dmg; c.AuraDefense = def; c.AuraSpeedPercent = spd;
            }
        }
    }
}
