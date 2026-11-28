using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    public class CombatTests
    {
        [Test]
        public void HitChanceClamped()
        {
            // MU: 1 − TD/TA quando a taxa de ataque é maior; senão 3%
            Assert.AreEqual(999, CombatResolver.HitChancePerMil(1000, 1));
            Assert.AreEqual(750, CombatResolver.HitChancePerMil(400, 100));
            Assert.AreEqual(30, CombatResolver.HitChancePerMil(1, 1000));
            Assert.AreEqual(30, CombatResolver.HitChancePerMil(10, 10), "taxas iguais: 3%");
        }

        [Test]
        public void DamageSubtractsDefenseWithFloor()
        {
            var w = new Character(1, "a", CharacterClass.Warden);
            var m = new Monster(1, MonsterCatalog.BasaltGolem, 0, 0);
            var r = CombatResolver.Resolve(w, m, SkillCatalog.BasicAttack, new FixedRandom(0));
            Assert.AreEqual(HitType.Normal, r.Type);
            Assert.AreEqual(1, r.Damage);
        }

        [Test]
        public void MissDealsNoDamage()
        {
            var w = new Character(1, "a", CharacterClass.Warden);
            var m = new Monster(1, MonsterCatalog.MossSlime, 0, 0);
            var r = CombatResolver.Resolve(w, m, SkillCatalog.BasicAttack, new FixedRandom(999));
            Assert.AreEqual(HitType.Miss, r.Type);
            Assert.AreEqual(m.MaxHp, m.Hp);
        }

        [Test]
        public void ArcanistSkillUsesMagic()
        {
            var a = new Character(1, "a", CharacterClass.Arcanist);
            var m = new Monster(1, MonsterCatalog.MossSlime, 0, 0);
            var skill = SkillCatalog.Find(3)!;
            var r = CombatResolver.Resolve(a, m, skill, new FixedRandom(0, 99, 99, 0));
            var cs = a.GetCombatStats();
            Assert.AreEqual(cs.MinMagic + skill.FlatDamage - MonsterCatalog.MossSlime.Defense, r.Damage);
        }

        [Test]
        public void UpgradedWeaponHitsHarder()
        {
            var a = new Item(ItemDatabase.IronBlade, 0);
            var b = new Item(ItemDatabase.IronBlade, 15);
            Assert.AreEqual(a.MaxDamage + 66, b.MaxDamage);
        }

        [Test]
        public void ComboNeedsThreeHitsWithTwoDistinctSkills()
        {
            var c = new ComboTracker();
            Assert.IsFalse(c.Register(1, 0));
            Assert.IsFalse(c.Register(1, 0.5));
            Assert.IsFalse(c.Register(1, 1.0));     // 1,1,1 não fecha
            Assert.IsTrue(c.Register(2, 1.5));      // 1,1,2 fecha
            Assert.IsFalse(c.Register(1, 2));
            Assert.IsFalse(c.Register(2, 2.5));
            Assert.IsFalse(c.Register(1, 9));       // janela expirou
        }

        [Test]
        public void AscendedOptionsApply()
        {
            var w = new Character(1, "a", CharacterClass.Warden);
            int hp = w.MaxHp;
            var armor = new Item(ItemDatabase.LeatherArmor) { Ascended = AscendedOption.MaxHpPlus | AscendedOption.DamageReduction };
            w.Inventory.TryAdd(armor, out var s);
            Assert.IsTrue(w.Equip(s, out _));
            Assert.AreEqual(hp + hp * 4 / 100, w.MaxHp);
            Assert.AreEqual(4, w.GetCombatStats().DamageReductionPercent);
            Assert.Greater(armor.Defense, ItemDatabase.LeatherArmor.Defense);
        }
    }
}
