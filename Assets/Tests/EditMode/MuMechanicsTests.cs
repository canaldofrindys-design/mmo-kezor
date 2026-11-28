using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Regras e fórmulas do MU Online clássico (ver docs/MECANICAS_MU.md; números conferidos no OpenMU).</summary>
    public class MuMechanicsTests
    {
        private static GameServer NewServer(int seed = 11, float expRate = 1f) =>
            new GameServer(new SystemRandom(seed), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999, AutosaveSeconds = 0, ExperienceRate = expRate });

        [TestCase(CharacterClass.Warden, 28, 20, 25, 10, 5)]
        [TestCase(CharacterClass.Arcanist, 18, 18, 15, 30, 5)]
        [TestCase(CharacterClass.Ranger, 22, 25, 20, 15, 5)]
        [TestCase(CharacterClass.Bladeweaver, 26, 26, 26, 26, 7)]
        [TestCase(CharacterClass.Commander, 26, 20, 20, 15, 7)]
        [TestCase(CharacterClass.Invoker, 21, 21, 18, 23, 5)]
        [TestCase(CharacterClass.EmberFist, 32, 27, 25, 20, 7)]
        public void BaseStatsAndPointsPerLevelMatchMu(CharacterClass cls, int str, int agi, int vit, int ene, int points)
        {
            var d = ClassDefinition.Get(cls);
            Assert.AreEqual(new[] { str, agi, vit, ene }, new[] { d.BaseStats.Strength, d.BaseStats.Agility, d.BaseStats.Vitality, d.BaseStats.Energy });
            var c = new Character(1, "a", cls);
            c.GainExperience(Character.ExpToNext(1));
            Assert.AreEqual(2, c.Level);
            Assert.AreEqual(points, c.FreePoints);
        }

        // vida e mana no nível 1 com os atributos iniciais (fórmula linear do MU)
        [TestCase(CharacterClass.Warden, 112, 20)]       // 35 + 2 + 3·25 · 10 + 0,5 + 10
        [TestCase(CharacterClass.Arcanist, 61, 62)]      // 30 + 1 + 2·15 · 2 + 2·30
        [TestCase(CharacterClass.Ranger, 80, 30)]        // 39 + 1 + 2·20 · 6 + 1,5 + 1,5·15
        [TestCase(CharacterClass.Bladeweaver, 110, 60)]  // 57 + 1 + 2·26 · 7 + 1 + 2·26
        [TestCase(CharacterClass.Commander, 90, 39)]     // 48,5 + 1,5 + 2·20 · 38 + 1 + 1,5·(15−15)
        [TestCase(CharacterClass.Invoker, 76, 46)]       // 39 + 1 + 2·18 · 6 + 1,5 + 1,7·23
        [TestCase(CharacterClass.EmberFist, 108, 34)]    // 57 + 1,3 + 2·25 · 7 + 1 + 1,3·20
        public void LifeAndManaFollowMuFormulas(CharacterClass cls, int hp, int mp)
        {
            var c = new Character(1, "a", cls);
            Assert.AreEqual(hp, c.MaxHp);
            Assert.AreEqual(mp, c.MaxMp);
        }

        [Test]
        public void DamageDefenseAndRatesFollowMuFormulas()
        {
            var dk = new Character(1, "dk", CharacterClass.Warden).GetCombatStats();
            Assert.AreEqual((28 / 6, 28 / 4), (dk.MinDamage, dk.MaxDamage));
            Assert.AreEqual(20 / 3 / 2, dk.Defense, "defesa final = (AGI/3) / 2");
            Assert.AreEqual(5 * 1 + 30 + 28 / 4, dk.AttackRate);
            Assert.AreEqual(20 / 3, dk.DefenseRate);
            Assert.AreEqual(2.01f, dk.SkillMultiplier, 0.0001f, "200% + ENE/10%");

            var mg = new Character(2, "mg", CharacterClass.Bladeweaver).GetCombatStats();
            Assert.AreEqual((26 / 6 + 26 / 12, 26 / 4 + 26 / 8), (mg.MinDamage, mg.MaxDamage));
            Assert.AreEqual((26 / 9, 26 / 4), (mg.MinMagic, mg.MaxMagic));

            var dl = new Character(3, "dl", CharacterClass.Commander).GetCombatStats();
            Assert.AreEqual((26 / 7 + 15 / 14, 26 / 5 + 15 / 10), (dl.MinDamage, dl.MaxDamage));
            Assert.AreEqual((int)(5 + 2.5f * 20) + 26 / 6, dl.AttackRate);
            Assert.AreEqual(20 / 7, dl.DefenseRate);

            var sum = new Character(4, "su", CharacterClass.Invoker).GetCombatStats();
            Assert.AreEqual((42 / 7, 42 / 4), (sum.MinDamage, sum.MaxDamage), "(FOR+AGI)/7 ~ /4");

            var rf = new Character(5, "rf", CharacterClass.EmberFist).GetCombatStats();
            Assert.AreEqual((32 / 7 + 25 / 15, 32 / 5 + 25 / 12), (rf.MinDamage, rf.MaxDamage));
            Assert.AreEqual((int)(3 + 1.25f * 27) + 32 / 6, rf.AttackRate);
            Assert.AreEqual(27 / 10, rf.DefenseRate);

            var elf = new Character(6, "elf", CharacterClass.Ranger).GetCombatStats();
            Assert.AreEqual(25 / 10 / 2, elf.Defense);
            Assert.AreEqual(25 / 4, elf.DefenseRate);
        }

        [Test]
        public void AttackSpeedComesFromAgility()
        {
            var dk = new Character(1, "dk", CharacterClass.Warden);
            dk.Stats.Agility = 300;
            Assert.AreEqual(300 / 15, dk.AttackSpeed);
            Assert.AreEqual(Character.AttackIntervalFor(20), dk.AttackCooldown);
            dk.Equipment[EquipSlot.RightHand] = new Item(ItemDatabase.IronBlade) { Ascended = AscendedOption.AttackSpeed };
            Assert.AreEqual(300 / 15 + 7, dk.AttackSpeed, "opção excelente +7");
            var dw = new Character(2, "dw", CharacterClass.Arcanist);
            dw.Stats.Agility = 300;
            Assert.AreEqual(300 / 10, dw.AttackSpeed, "magia: AGI/10");
            Assert.Greater(Character.AttackIntervalFor(0), Character.AttackIntervalFor(50));
            Assert.AreEqual(0.2f, Character.AttackIntervalFor(10_000), "limite mínimo");
        }

        [Test]
        public void ExperienceTableIsTheMuOne()
        {
            Assert.AreEqual(0, Character.TotalExpForLevel(1));
            Assert.AreEqual(100, Character.TotalExpForLevel(2));
            Assert.AreEqual(10L * 18 * 81, Character.TotalExpForLevel(10));
            Assert.AreEqual(10L * 308 * 299 * 299 + 1000L * 53 * 44 * 44, Character.TotalExpForLevel(300), "do 256 em diante soma a parte extra");
            Assert.AreEqual(Character.TotalExpForLevel(51) - Character.TotalExpForLevel(50), Character.ExpToNext(50));
        }

        [Test]
        public void MonsterExperienceUsesTheMuFormula()
        {
            Assert.AreEqual(18, GameServer.BaseExpFor(1, 2), 0.001);                     // (2+25)·2/3
            Assert.AreEqual(550 * 40 / 50.0, GameServer.BaseExpFor(50, 30), 0.001);      // herói 10+ níveis acima
            Assert.AreEqual(95 * 70 / 3.0 + 6 * 17, GameServer.BaseExpFor(1, 70), 0.001); // monstro 65+

            var gs = NewServer();
            gs.Handle(1, "CREATE Cacador GUA");
            var c = gs.GetCharacter(1)!;
            var slime = gs.Monsters.Values.First(m => m.Def == MonsterCatalog.MossSlime);
            slime.Hp = 1;
            c.X = slime.X + 1f; c.Z = slime.Z;
            c.Stats.Strength = 500; c.Stats.Agility = 500;
            gs.Handle(1, $"ATTACK {slime.Id}");
            for (int i = 0; i < 100 && !slime.IsDead; i++) gs.Tick(0.05f);
            Assert.IsTrue(slime.IsDead);
            double baseExp = GameServer.BaseExpFor(1, MonsterCatalog.MossSlime.Level);
            long got = c.Level > 1 ? Character.ExpToNext(1) + c.Experience : c.Experience;
            Assert.That(got, Is.InRange((long)baseExp, (long)(baseExp * 1.5) + 1), "sorteio de 100% a 150%");
        }

        [Test]
        public void ServerExperienceRateMultiplies()
        {
            var gs = NewServer(expRate: 100f);
            gs.Handle(1, "CREATE Rapido GUA");
            var c = gs.GetCharacter(1)!;
            var slime = gs.Monsters.Values.First(m => m.Def == MonsterCatalog.MossSlime);
            slime.Hp = 1; c.X = slime.X + 1f; c.Z = slime.Z; c.Stats.Strength = 500; c.Stats.Agility = 500;
            gs.Handle(1, $"ATTACK {slime.Id}");
            for (int i = 0; i < 100 && !slime.IsDead; i++) gs.Tick(0.05f);
            Assert.Greater(c.Level, 1, "100x de experiência");
        }

        [Test]
        public void PartyBonusIsTheClassicTable()
        {
            Assert.AreEqual(100, Party.ExpPercent(1, false));
            Assert.AreEqual(new[] { 160, 180, 200, 220 }, Enumerable.Range(2, 4).Select(n => Party.ExpPercent(n, false)).ToArray());
            Assert.AreEqual(new[] { 230, 270, 300 }, Enumerable.Range(3, 3).Select(n => Party.ExpPercent(n, true)).ToArray());
            Assert.AreEqual(160, Party.ExpPercent(2, true), "grupo de classes diferentes só conta com 3 ou mais");
        }

        [Test]
        public void SkillDamageIsAddedThenMultipliedByTheClass()
        {
            // golpe fixo: sorteio no mínimo, sem crítico; acerto garantido (TA alta)
            var dk = new Character(1, "dk", CharacterClass.Warden);
            dk.Stats.Strength = 200;   // nível 1: o piso de nível/10 não interfere
            var slime = new Monster(1, MonsterCatalog.MossSlime, 0, 0) { Hp = 100000 };
            var skill = SkillCatalog.Find(1)!;   // Corte Rúnico, dano 15
            var a = dk.GetCombatStats();
            var r = CombatResolver.Resolve(dk, slime, skill, new FixedRandom(0));
            int expected = (int)((a.MinDamage + skill.FlatDamage - MonsterCatalog.MossSlime.Defense) * a.SkillMultiplier);
            Assert.AreEqual(HitType.Normal, r.Type);
            Assert.AreEqual(expected, r.Damage);
            var basic = CombatResolver.Resolve(dk, new Monster(2, MonsterCatalog.MossSlime, 0, 0) { Hp = 100000 }, SkillCatalog.BasicAttack, new FixedRandom(0));
            Assert.AreEqual(a.MinDamage - MonsterCatalog.MossSlime.Defense, basic.Damage, "ataque normal não usa o multiplicador");
        }

        [Test]
        public void HigherDefenseRateCutsMonsterDamageTo30Percent()
        {
            var weak = new Character(1, "fraco", CharacterClass.Warden);   // TA 42 contra TD 95 do Soberano
            var boss = new Monster(1, MonsterCatalog.BlackGlassSovereign, 0, 0);
            var best = ItemDatabase.All.Where(d => d.IsWeapon).OrderByDescending(d => d.MinDamage).First();
            weak.Equipment[EquipSlot.RightHand] = new Item(best, 15);
            Assert.Less(weak.GetCombatStats().AttackRate, MonsterCatalog.BlackGlassSovereign.DefenseRate);
            var a = weak.GetCombatStats();
            var r = CombatResolver.Resolve(weak, boss, SkillCatalog.BasicAttack, new FixedRandom(0));
            Assert.AreEqual(HitType.Normal, r.Type, "FixedRandom(0) acerta nos 3%");
            Assert.AreEqual((a.MinDamage - MonsterCatalog.BlackGlassSovereign.Defense) * 3 / 10, r.Damage);
        }

        [Test]
        public void SoulRuneWorksFromZeroAndFailsLikeMu()
        {
            var soul = new Item(ItemDatabase.RuneOfEclipse);
            var it = new Item(ItemDatabase.IronBlade, 2);
            Assert.IsTrue(RuneService.Apply(soul, it, new FixedRandom(0)).Success, "+2 aceita a runa do Eclipse");
            Assert.AreEqual(3, it.Level);
            RuneService.Apply(soul, it, new FixedRandom(99));
            Assert.AreEqual(2, it.Level, "abaixo de +7 a falha tira 1 nível");
            var seven = new Item(ItemDatabase.IronBlade, 7);
            RuneService.Apply(soul, seven, new FixedRandom(99));
            Assert.AreEqual(0, seven.Level, "+7 e +8 voltam a +0");
            var zero = new Item(ItemDatabase.IronBlade, 0);
            RuneService.Apply(soul, zero, new FixedRandom(99));
            Assert.AreEqual(0, zero.Level);
            Assert.IsFalse(RuneService.Apply(soul, new Item(ItemDatabase.IronBlade, 9), new FixedRandom(0)).Applied, "+9 só na Forja");
            Assert.AreEqual(25, SuperiorRefineRecipe.LuckBonus);
        }

        [Test]
        public void PotionsHealPercentPlusLevelBonus()
        {
            var small = ItemDatabase.HealthPotion; var large = ItemDatabase.SupremeHealthPotion;
            Assert.AreEqual(1000 * 10 / 100 + (100 - 20), small.PotionRecover(1000, 20));
            Assert.AreEqual(1000 * 30 / 100 + (200 - 20), large.PotionRecover(1000, 20));
            Assert.AreEqual(1000 * 10 / 100, small.PotionRecover(1000, 150), "o bônus some nos níveis altos");
            Assert.AreEqual(1000 * 20 / 100 + (150 - 50), ItemDatabase.GreaterManaPotion.PotionRecover(1000, 50));
        }

        [Test]
        public void ExcellentOptionsComeFromTheRightPool()
        {
            var rng = new SystemRandom(3);
            for (int i = 0; i < 200; i++)
            {
                var w = DropSystem.RollExcellent(ItemDatabase.IronBlade, rng);
                Assert.IsTrue(Item.WeaponExcellent.Any(o => (w & o) != 0));
                Assert.IsFalse(Item.ArmorExcellent.Any(o => (w & o) != 0), "arma não recebe opção de armadura");
            }
            var c = new Character(1, "a", CharacterClass.Warden);
            int mp0 = c.MaxMp;
            var armor = ItemDatabase.All.First(d => d.Slot == EquipSlot.Armor && (d.AllowedClasses & ClassMask.Warden) != 0);
            c.Equipment[EquipSlot.Armor] = new Item(armor) { Ascended = AscendedOption.MaxMpPlus | AscendedOption.Reflect | AscendedOption.ZenBonus };
            Assert.AreEqual(mp0 + mp0 * 4 / 100, c.MaxMp);
            var s = c.GetCombatStats();
            Assert.AreEqual(5, s.ReflectPercent);
            Assert.AreEqual(40, s.ZenBonusPercent);
        }

        [Test]
        public void PkStagesLastThreeHoursEach()
        {
            var c = new Character(1, "pk", CharacterClass.Warden);
            c.AddPlayerKill();
            Assert.AreEqual(1, c.PkLevel, "1 morte: aviso");
            Assert.IsFalse(c.IsMurderer);
            c.AddPlayerKill();
            Assert.AreEqual(2, c.PkLevel);
            Assert.IsTrue(c.IsMurderer);
            c.AddPlayerKill(); c.AddPlayerKill();
            Assert.AreEqual(3, c.PkLevel, "no máximo 2º grau");
            Assert.AreEqual(2 * Character.PkStageSeconds, c.PkSeconds, "no 2º grau as mortes somam tempo");
            c.TickPk(2 * Character.PkStageSeconds);
            Assert.AreEqual(2, c.PkLevel, "acabou o tempo: cai um grau");
            c.TickPk(Character.PkStageSeconds); c.TickPk(Character.PkStageSeconds);
            Assert.AreEqual(0, c.PkLevel);
            Assert.AreEqual(0, c.PkSeconds);
        }

        [Test]
        public void SelfDefenseAndKillingMurderersGiveNoPk()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Vitima GUA"); gs.Handle(2, "CREATE Agressor GUA");
            var a = gs.GetCharacter(1)!; var b = gs.GetCharacter(2)!;
            gs.Handle(1, "GM LEVEL 100"); gs.Handle(1, "ADD STR 490");
            gs.Handle(2, "GM LEVEL 10");
            a.X = 30; a.Z = 30; b.X = 31; b.Z = 30;
            // B ataca A primeiro; A revida e mata: legítima defesa
            StringAssert.StartsWith("OK", gs.Handle(2, $"ATTACK {a.Id}"));
            for (int i = 0; i < 40; i++) { gs.Tick(0.05f); a.Hp = a.MaxHp; }
            gs.Handle(2, "STOP");
            StringAssert.StartsWith("OK", gs.Handle(1, $"ATTACK {b.Id}"));
            for (int i = 0; i < 400 && !b.IsDead && b.MapId == a.MapId && gs.Map.InSafeZone(b.X, b.Z) == false; i++) gs.Tick(0.05f);
            Assert.AreEqual(0, a.Infamy, "legítima defesa não dá PK");

            // matar um assassino também não
            var gs2 = NewServer();
            gs2.Handle(1, "CREATE Cacador GUA"); gs2.Handle(2, "CREATE Assassino GUA");
            var h = gs2.GetCharacter(1)!; var m = gs2.GetCharacter(2)!;
            gs2.Handle(1, "GM LEVEL 100"); gs2.Handle(1, "ADD STR 490"); gs2.Handle(2, "GM LEVEL 10");
            m.Infamy = 2; m.PkSeconds = Character.PkStageSeconds;
            h.X = 30; h.Z = 30; m.X = 31; m.Z = 30;
            gs2.Handle(1, $"ATTACK {m.Id}");
            for (int i = 0; i < 400 && !gs2.Map.InSafeZone(m.X, m.Z); i++) gs2.Tick(0.05f);
            Assert.IsTrue(gs2.Map.InSafeZone(m.X, m.Z), "o assassino caiu e renasceu na vila");
            Assert.AreEqual(0, h.Infamy);
        }
    }
}
