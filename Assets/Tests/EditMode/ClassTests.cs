using System;
using System.Collections.Generic;
using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Classes novas (Lâmina Arcana, Comandante do Estandarte, Invocadora de Ecos, Punho de Brasa) e balanceamento.</summary>
    public class ClassTests
    {
        private static readonly CharacterClass[] NewClasses = { CharacterClass.Bladeweaver, CharacterClass.Commander, CharacterClass.Invoker, CharacterClass.EmberFist };
        private static readonly CharacterClass[] OldClasses = { CharacterClass.Warden, CharacterClass.Arcanist, CharacterClass.Ranger };

        private static GameServer NewServer(int seed = 11) =>
            new GameServer(new SystemRandom(seed), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999 });

        private static Character Hero(GameServer gs, int session, CharacterClass cls, int level = 100)
        {
            gs.Handle(session, $"CREATE H{session}x{(int)cls} {ClassDefinition.Get(cls).Code}");
            gs.Handle(session, $"GM LEVEL {level}");
            return gs.GetCharacter(session)!;
        }

        private static Monster Dummy(GameServer gs, Character c, MonsterDefinition def)
        {
            var m = gs.Monsters.Values.First(x => x.Def == def && !x.IsDead);
            m.X = c.X + 1.5f; m.Z = c.Z;
            return m;
        }

        [Test]
        public void SevenClassesWithSixOriginalSkillsEach()
        {
            Assert.AreEqual(7, ClassDefinition.All.Length);
            Assert.AreEqual(7, ClassDefinition.All.Select(c => c.Code).Distinct().Count());
            foreach (var cls in NewClasses)
            {
                var skills = SkillCatalog.All.Where(s => s.Id != 0 && (s.Classes & ClassDefinition.MaskOf(cls)) != 0).ToList();
                Assert.AreEqual(6, skills.Count, cls.ToString());
                Assert.AreEqual(6, skills.Select(s => s.Name).Distinct().Count());
                Assert.IsTrue(skills.Any(s => s.ReqLevel == 1 && s.NeedsTarget), $"{cls}: precisa de skill de dano no nível 1");
                // armas próprias, 1 épica e um conjunto de 5 peças só da classe
                var mask = ClassDefinition.MaskOf(cls);
                Assert.GreaterOrEqual(ItemDatabase.All.Count(d => d.IsWeapon && d.AllowedClasses == mask), 4, cls.ToString());
                Assert.IsTrue(ItemDatabase.All.Any(d => d.Epic && d.AllowedClasses == mask), cls.ToString());
                var set = ItemDatabase.All.Where(d => d.Set != ArmorSet.None && d.AllowedClasses == mask).ToList();
                Assert.AreEqual(5, set.Count, cls.ToString());
            }
            Assert.IsTrue(ClassDefinition.TryParse("pun", out var p) && p == CharacterClass.EmberFist);
        }

        [Test]
        public void CreateEquipsTheClassStarterWeapon()
        {
            var gs = NewServer();
            int sess = 1;
            foreach (var cls in NewClasses)
            {
                gs.Handle(sess, $"CREATE Novo{sess} {ClassDefinition.Get(cls).Code}");
                var c = gs.GetCharacter(sess)!;
                Assert.AreEqual(cls, c.Class);
                var w = c.Equipment[EquipSlot.RightHand];
                Assert.AreEqual(ClassDefinition.MaskOf(cls), w.Def.AllowedClasses, cls.ToString());
                sess++;
            }
        }

        [Test]
        public void BladeweaverSkillsAddStrengthAndEnergy()
        {
            var c = new Character(1, "Teste", CharacterClass.Bladeweaver);
            c.Stats = new Attributes(200, 30, 50, 150);
            var s = c.GetCombatStats();
            Assert.Greater(s.MaxMagic, 0);
            var dummy = new Monster(9, MonsterCatalog.BasaltGolem, 0, 0);
            var fio = SkillCatalog.Find(10)!;
            var rng = new SystemRandom(1);
            long hybrid = 0, phys = 0;
            var physOnly = new SkillDefinition { DamagePercent = fio.DamagePercent, FlatDamage = fio.FlatDamage };
            for (int i = 0; i < 200; i++)
            {
                dummy.Hp = dummy.MaxHp; hybrid += CombatResolver.Resolve(c, dummy, fio, rng).Damage;
                dummy.Hp = dummy.MaxHp; phys += CombatResolver.Resolve(c, dummy, physOnly, rng).Damage;
            }
            Assert.Greater(hybrid, phys * 12 / 10, "a energia precisa somar no golpe híbrido");
        }

        [Test]
        public void SelfBuffRaisesDamageAndHasCooldown()
        {
            var gs = NewServer();
            var c = Hero(gs, 1, CharacterClass.Bladeweaver);
            int before = c.GetCombatStats().DamageBonusPercent;
            StringAssert.StartsWith("OK", gs.Handle(1, "SKILL 12 0"));
            Assert.AreEqual(before + 15, c.GetCombatStats().DamageBonusPercent);
            for (int i = 0; i < 20; i++) gs.Tick(0.05f);
            StringAssert.Contains("recarregando", gs.Handle(1, "SKILL 12 0"));
            for (int i = 0; i < 320; i++) gs.Tick(0.05f);          // 16 s depois: acabou
            Assert.AreEqual(before, c.GetCombatStats().DamageBonusPercent);
        }

        [Test]
        public void BuffsAndCooldownsReachTheClient()
        {
            var gs = NewServer();
            var c = Hero(gs, 1, CharacterClass.Bladeweaver, 30);
            var cs = new Kezor.Core.Net.ClientState();
            gs.DrainOutbox(1);
            StringAssert.StartsWith("OK", gs.Handle(1, "SKILL 12 0"));
            gs.Tick(0.11f);
            foreach (var line in gs.DrainOutbox(1) ?? new List<string>()) cs.Process(line);
            Assert.AreEqual(1, cs.Effects.Count, "buff da Lâmina Ressonante no @ME");
            Assert.AreEqual(BuffKind.Damage, cs.Effects[0].Kind);
            Assert.AreEqual(15, cs.Effects[0].Value);
            Assert.Greater(cs.Effects[0].Expires - cs.ServerTime, 14f);
            Assert.IsTrue(cs.Cooldowns.ContainsKey(12), "recarga da skill no @ME");
            Assert.Greater(cs.CooldownLeft(12), 19f);
        }

        [Test]
        public void InvokerGearNeedsEnergyNotStrength()
        {
            var gs = NewServer();
            var inv = Hero(gs, 1, CharacterClass.Invoker, 60);
            inv.Stats = new Attributes(ClassDefinition.Invoker.BaseStats.Strength, inv.Stats.Agility, inv.Stats.Vitality, 200);
            foreach (var id in new[] { 56, 57, 58, 45, 170, 171, 172, 173, 174 })
            {
                var it = new Item(ItemDatabase.Get(id)!, 15);
                Assert.IsTrue(inv.CanEquip(it, out var err), $"{it.Def.Name} +15: {err}");
                Assert.Greater(it.Def.ReqEnergy, 0, "a exigência principal é energia");
            }
            inv.Stats = new Attributes(inv.Stats.Strength, inv.Stats.Agility, inv.Stats.Vitality, 30);
            Assert.IsFalse(inv.CanEquip(new Item(ItemDatabase.EternalSilenceBell), out _), "sem energia não usa a épica");
        }

        [Test]
        public void UtilitySkillDuringAutoAttackIsQueuedNotLost()
        {
            var gs = NewServer();
            var com = Hero(gs, 1, CharacterClass.Commander, 50);
            var golem = gs.Monsters.Values.First(m => m.Def == MonsterCatalog.BasaltGolem && !m.IsDead);
            com.X = golem.X + 1.5f; com.Z = golem.Z;
            gs.Handle(1, $"ATTACK {golem.Id}");
            gs.Tick(0.05f);                                            // primeiro golpe: começa o intervalo do ataque
            StringAssert.Contains("na fila", gs.Handle(1, "SKILL 17 0"));
            for (int i = 0; i < 20; i++) { gs.Tick(0.05f); golem.Hp = golem.MaxHp; com.Hp = com.MaxHp; }
            Assert.IsTrue(gs.Banners.Values.Any(b => b.OwnerId == com.Id), "o estandarte na fila deveria ter saído");
        }

        [Test]
        public void BannerAuraOnlyForPartyInsideRadius()
        {
            var gs = NewServer();
            var cmd = Hero(gs, 1, CharacterClass.Commander);
            var ally = Hero(gs, 2, CharacterClass.Ranger);
            var other = Hero(gs, 3, CharacterClass.Warden);
            gs.Handle(1, "PARTY INVITE H2x2"); gs.Handle(2, "PARTY ACCEPT");
            ally.X = cmd.X + 3; ally.Z = cmd.Z; other.X = cmd.X - 3; other.Z = cmd.Z;
            StringAssert.StartsWith("OK", gs.Handle(1, "SKILL 17 0"));
            gs.Tick(0.05f);
            int expected = 15 + cmd.Stats.Energy / 50;
            Assert.AreEqual(expected, cmd.AuraDamagePercent);
            Assert.AreEqual(expected, ally.AuraDamagePercent);
            Assert.AreEqual(0, other.AuraDamagePercent, "fora do grupo não ganha");
            ally.X = cmd.X + 10; gs.Tick(0.05f);
            Assert.AreEqual(0, ally.AuraDamagePercent, "longe do estandarte não ganha");
            for (int i = 0; i < 14; i++) gs.Tick(0.05f);
            StringAssert.StartsWith("OK", gs.Handle(1, "SKILL 20 0")); gs.Tick(0.05f);   // estandarte do vento
            float walk = cmd.Def.MoveSpeed;
            Assert.AreEqual(walk * 1.2f, cmd.MoveSpeed, 0.01f);
            // aparece no snapshot para quem está perto
            var view = new ClientState();
            gs.Tick(0.2f);
            foreach (var l in gs.DrainOutbox(3)!) view.Process(l);
            Assert.AreEqual(2, view.Entities.Values.Count(e => e.Kind == EntityKind.Banner));
            for (int i = 0; i < 420; i++) gs.Tick(0.05f);
            Assert.AreEqual(0, gs.Banners.Count, "estandartes expiram");
            Assert.AreEqual(0, cmd.AuraDamagePercent);
        }

        [Test]
        public void EchoesFightForTheOwnerAndAreNotTargetable()
        {
            var gs = NewServer();
            var inv = Hero(gs, 1, CharacterClass.Invoker);
            inv.X = MapDefinition.ValeCenter - 60; inv.Z = MapDefinition.ValeCenter - 6;                    // fora da vila, perto dos lodos do oeste
            StringAssert.StartsWith("OK", gs.Handle(1, "SKILL 23 0"));
            gs.Handle(1, "SKILL 23 0");
            for (int i = 0; i < 100; i++) gs.Tick(0.05f);
            gs.Handle(1, "SKILL 23 0");                                  // terceiro: o mais antigo some (máx. 2)
            var echoes = gs.Monsters.Values.Where(m => m.OwnerId == inv.Id).ToList();
            Assert.AreEqual(2, echoes.Count);
            Assert.Greater(echoes[0].MaxHp, MonsterCatalog.ShadowEcho.Hp, "a força escala com a energia");

            gs.Handle(2, "CREATE Rival GUA");
            StringAssert.StartsWith("ERR", gs.Handle(2, $"ATTACK {echoes[0].Id}"));   // ninguém ataca o eco

            var slime = gs.Monsters.Values.Where(m => m.Def == MonsterCatalog.MossSlime && m.MapId == 1 && !m.IsDead).OrderBy(m => MathK.Dist(m.X, m.Z, inv.X, inv.Z)).First();
            slime.X = inv.X + 4; slime.Z = inv.Z;
            gs.Handle(1, $"ATTACK {slime.Id}");                          // o eco ajuda no alvo do dono
            bool echoHit = false;
            for (int i = 0; i < 200 && !echoHit; i++)
            {
                gs.Tick(0.05f);
                echoHit = gs.DrainOutbox(1)?.Any(l => echoes.Any(e => l.StartsWith($"@EV HIT;{e.Id};"))) ?? false;
                inv.Hp = inv.MaxHp;
            }
            Assert.IsTrue(echoHit, "o eco deveria atacar o alvo do dono");

            gs.Handle(1, "STOP"); for (int i = 0; i < 110; i++) { gs.Tick(0.05f); inv.Hp = inv.MaxHp; }
            inv.Gold = 10_000_000; inv.X = MapDefinition.ValeCenter - 60; inv.Z = MapDefinition.ValeCenter - 6;
            StringAssert.StartsWith("OK", gs.Handle(1, "WARP PANTANO"));
            gs.Tick(0.05f);
            Assert.AreEqual(0, gs.Monsters.Values.Count(m => m.OwnerId == inv.Id), "ecos somem quando o dono muda de mapa");
        }

        [Test]
        public void CursesSlowAndDamageOverTime()
        {
            var gs = NewServer();
            var inv = Hero(gs, 1, CharacterClass.Invoker);
            inv.X = MapDefinition.ValeCenter - 6; inv.Z = MapDefinition.ValeCenter - 60;                      // lobos do portão sul
            var wolf = gs.Monsters.Values.First(m => m.Def == MonsterCatalog.AshWolf && !m.IsDead);
            wolf.X = inv.X + 4; wolf.Z = inv.Z;
            gs.Handle(1, $"SKILL 24 {wolf.Id}");
            for (int i = 0; i < 10 && wolf.SlowUntil <= gs.Time; i++) gs.Tick(0.05f);
            Assert.AreEqual(0.6f, wolf.SpeedFactor(gs.Time), 0.01f, "lento 40%");
            wolf.Hp = wolf.MaxHp;
            for (int i = 0; i < 20; i++) gs.Tick(0.05f);
            gs.Handle(1, $"SKILL 25 {wolf.Id}");
            for (int i = 0; i < 30 && wolf.DotUntil <= gs.Time; i++) gs.Tick(0.05f);   // espera o intervalo entre golpes (velocidade de ataque)
            Assert.Greater(wolf.DotPerSecond, 0);
            int hpAfterHit = wolf.Hp;
            for (int i = 0; i < 70; i++) { gs.Tick(0.05f); inv.Hp = inv.MaxHp; gs.Handle(1, "STOP"); }
            Assert.Less(wolf.Hp, hpAfterHit, "a chaga causa dano ao longo do tempo");
            Assert.IsTrue(wolf.DamageBy.ContainsKey(inv.Id));
        }

        [Test]
        public void EmberFlowTriggersEveryFifthHitAndFistsAreFaster()
        {
            var flow = new EmberFlow();
            var results = Enumerable.Range(0, 10).Select(i => flow.Register(7, i * 0.4)).ToList();
            Assert.AreEqual(new[] { false, false, false, false, true, false, false, false, false, true }, results.ToArray());
            Assert.IsFalse(flow.Register(8, 4.1), "trocar de alvo zera");
            flow.Register(8, 4.2); flow.Register(8, 4.3);
            Assert.IsFalse(flow.Register(8, 6.0), "demorar zera");
            Assert.Less(new Character(90, "a", CharacterClass.EmberFist).AttackCooldown, new Character(91, "b", CharacterClass.Warden).AttackCooldown, "AGI/9 contra AGI/15");

            var gs = NewServer();
            var pun = Hero(gs, 1, CharacterClass.EmberFist, 30);
            var golem = gs.Monsters.Values.First(m => m.Def == MonsterCatalog.BasaltGolem && !m.IsDead);
            pun.X = golem.X + 1.2f; pun.Z = golem.Z;
            int combos = 0;
            gs.Handle(1, $"SKILL 31 {golem.Id}");                        // rajada: 3 golpes
            for (int i = 0; i < 120; i++)
            {
                gs.Tick(0.05f);
                pun.Hp = pun.MaxHp; pun.Mp = pun.MaxMp; golem.Hp = golem.MaxHp;
                combos += gs.DrainOutbox(1)?.Count(l => l.StartsWith($"@EV HIT;{pun.Id};{golem.Id};{(int)HitType.Combo};")) ?? 0;
                if (i % 8 == 0) gs.Handle(1, $"SKILL 31 {golem.Id}");
            }
            Assert.Greater(combos, 0, "o Fluxo de Brasa deveria disparar");
        }

        // ---------------------------------------------------------------- balanceamento
        private static readonly Dictionary<CharacterClass, (double str, double agi, double vit, double ene, ItemDefinition weapon, int[] utility)> Builds =
            new Dictionary<CharacterClass, (double, double, double, double, ItemDefinition, int[])>
        {
            { CharacterClass.Warden, (0.6, 0.2, 0.2, 0.0, ItemDatabase.StormBlade, new int[0]) },
            { CharacterClass.Arcanist, (0.0, 0.1, 0.2, 0.7, ItemDatabase.OracleStaff, new int[0]) },
            { CharacterClass.Ranger, (0.15, 0.65, 0.2, 0.0, ItemDatabase.NorthwindBow, new int[0]) },
            { CharacterClass.Bladeweaver, (0.45, 0.0, 0.2, 0.35, ItemDatabase.PrismTideSword, new[] { 12 }) },
            { CharacterClass.Commander, (0.55, 0.0, 0.2, 0.25, ItemDatabase.BulwarkHalberd, new[] { 17 }) },
            { CharacterClass.Invoker, (0.0, 0.1, 0.2, 0.7, ItemDatabase.ThousandEchoGrimoire, new[] { 23, 26 }) },
            { CharacterClass.EmberFist, (0.4, 0.4, 0.2, 0.0, ItemDatabase.MagmaFists, new[] { 30 }) },
        };

        /// <summary>
        /// DPS sustentado no nível 100 contra um Golem de Basalto que nunca morre: usa buffs/estandartes/ecos quando podem,
        /// e a skill de dano mais forte que a mana permite (ou o ataque básico). Conta tudo o que foi creditado ao herói.
        /// </summary>
        private static double SimulateDps(CharacterClass cls, int seed)
        {
            var gs = NewServer(seed);
            var c = Hero(gs, 1, cls);
            var b = Builds[cls];
            int pts = c.FreePoints;
            c.Stats = new Attributes(c.Stats.Strength + (int)(pts * b.str), c.Stats.Agility + (int)(pts * b.agi), c.Stats.Vitality + (int)(pts * b.vit), c.Stats.Energy + (int)(pts * b.ene));
            c.Equipment[EquipSlot.RightHand] = new Item(b.weapon);
            c.Mp = c.MaxMp;
            var golem = gs.Monsters.Values.First(m => m.Def == MonsterCatalog.BasaltGolem && !m.IsDead);
            c.X = golem.X + 1.6f; c.Z = golem.Z;
            var dmgSkills = SkillCatalog.All.Where(s => s.Id != 0 && s.NeedsTarget && (s.Classes & ClassDefinition.MaskOf(cls)) != 0 && s.ReqLevel <= c.Level)
                                            .OrderByDescending(s => s.DamagePercent * Math.Max(1, s.Hits) + s.FlatDamage).ToList();
            const float seconds = 60f;
            for (float t = 0; t < seconds; t += 0.05f)
            {
                golem.Hp = golem.MaxHp; golem.X = c.X - 1.6f; golem.Z = c.Z;
                c.Hp = c.MaxHp;
                foreach (var u in b.utility)
                {
                    var us = SkillCatalog.Find(u)!;
                    if (c.Mp >= us.ManaCost && c.Vigor >= us.VigorCost && c.Level >= us.ReqLevel && gs.Handle(1, $"SKILL {u} 0").StartsWith("OK")) break;
                }
                var best = dmgSkills.FirstOrDefault(s => s.ManaCost <= c.Mp && s.VigorCost <= c.Vigor);   // sem vigor: ataque básico
                if (best != null) gs.Handle(1, $"SKILL {best.Id} {golem.Id}"); else gs.Handle(1, $"ATTACK {golem.Id}");
                gs.Tick(0.05f);
            }
            return golem.DamageBy.TryGetValue(c.Id, out var total) ? total / seconds : 0;
        }

        [Test]
        public void EveryClassDealsDamageAtLevel100()
        {
            // Com as regras do MU as classes não são equilibradas entre si (como no MU); o teste só confere que todas lutam.
            var dps = new Dictionary<CharacterClass, double>();
            foreach (var cls in OldClasses.Concat(NewClasses)) dps[cls] = (SimulateDps(cls, 21) + SimulateDps(cls, 22)) / 2;
            var report = string.Join(", ", dps.Select(kv => $"{kv.Key}={kv.Value:0}"));
            TestContext.WriteLine("DPS nível 100: " + report);
            foreach (var kv in dps) Assert.Greater(kv.Value, 0, $"{kv.Key} não causou dano ({report})");
        }
    }
}
