using System.Collections.Generic;
using Kezor.Core;
using Kezor.Core.Items;
using NUnit.Framework;

namespace Kezor.Tests
{
    public class ItemTests
    {
        [Test]
        public void LevelBonusCurve()
        {
            Assert.AreEqual(0, Item.LevelBonus(0));
            Assert.AreEqual(27, Item.LevelBonus(9));
            Assert.AreEqual(31, Item.LevelBonus(10));
            Assert.AreEqual(66, Item.LevelBonus(15));
            for (int l = 1; l <= 15; l++) Assert.Greater(Item.LevelBonus(l), Item.LevelBonus(l - 1));
        }

        [Test]
        public void BlessUpgradesUpToSix()
        {
            var it = new Item(ItemDatabase.IronBlade);
            var rng = new FixedRandom(0);
            for (int i = 0; i < 6; i++) Assert.IsTrue(RuneService.Apply(new Item(ItemDatabase.RuneOfDawn), it, rng).Success);
            Assert.AreEqual(6, it.Level);
            var r = RuneService.Apply(new Item(ItemDatabase.RuneOfDawn), it, rng);
            Assert.IsFalse(r.Applied);
        }

        [Test]
        public void SoulSuccessAndFailure()
        {
            var it = new Item(ItemDatabase.IronBlade, 6);
            Assert.IsTrue(RuneService.Apply(new Item(ItemDatabase.RuneOfEclipse), it, new FixedRandom(49)).Success);
            Assert.AreEqual(7, it.Level);
            var fail = RuneService.Apply(new Item(ItemDatabase.RuneOfEclipse), it, new FixedRandom(50));
            Assert.IsTrue(fail.Applied); Assert.IsFalse(fail.Success);
            Assert.AreEqual(0, it.Level);
        }

        [Test]
        public void SoulLuckRaisesRate()
        {
            var it = new Item(ItemDatabase.IronBlade, 6) { Luck = true };
            Assert.IsTrue(RuneService.Apply(new Item(ItemDatabase.RuneOfEclipse), it, new FixedRandom(74)).Success);
        }

        [Test]
        public void SoulRejectsNineAndAbove()
        {
            var it = new Item(ItemDatabase.IronBlade, 9);
            Assert.IsFalse(RuneService.Apply(new Item(ItemDatabase.RuneOfEclipse), it, new FixedRandom(0)).Applied);
        }

        [Test]
        public void LifeAddsOptionAndCaps()
        {
            var it = new Item(ItemDatabase.LeatherArmor);
            for (int i = 0; i < 7; i++) RuneService.Apply(new Item(ItemDatabase.VitalRune), it, new FixedRandom(0));
            Assert.AreEqual(7, it.AdditionalOption);
            Assert.AreEqual(8 + 28, it.Defense);
            Assert.IsFalse(RuneService.Apply(new Item(ItemDatabase.VitalRune), it, new FixedRandom(0)).Applied);
        }

        [Test]
        public void LowDurabilityHalvesStats()
        {
            var it = new Item(ItemDatabase.IronBlade);
            int full = it.MaxDamage;
            it.Durability = 1;
            Assert.AreEqual(full / 2, it.MaxDamage);
        }

        private static List<Item> UpgradeSet(Item target)
        {
            int n = SuperiorRefineRecipe.RequiredRunes(target.Level + 1);
            var l = new List<Item> { target, new Item(ItemDatabase.VortexCrystal) };
            for (int i = 0; i < n; i++) { l.Add(new Item(ItemDatabase.RuneOfDawn)); l.Add(new Item(ItemDatabase.RuneOfEclipse)); }
            return l;
        }

        [Test]
        public void ForgeUpgradeSuccess()
        {
            var it = new Item(ItemDatabase.IronBlade, 9);
            var cm = new ArcaneForge(new FixedRandom(0));
            long gold = 10_000_000;
            var r = cm.Mix(UpgradeSet(it), ref gold);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(10, it.Level);
            Assert.AreEqual(60, r.RatePercent);
            Assert.AreEqual(10_000_000 - 2_000_000, gold);
            Assert.AreEqual(3, r.Consumed.Count);
            CollectionAssert.Contains(r.Outputs, it);
        }

        [Test]
        public void ForgeUpgradeFailureDestroysItem()
        {
            var it = new Item(ItemDatabase.IronBlade, 12);
            var cm = new ArcaneForge(new FixedRandom(99));
            long gold = 100_000_000;
            var set = UpgradeSet(it);
            var r = cm.Mix(set, ref gold);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(set.Count, r.Consumed.Count);
            Assert.IsEmpty(r.Outputs);
        }

        [Test]
        public void ForgeRatesAndLuckCap()
        {
            var cm = new ArcaneForge(new FixedRandom(0));
            Assert.AreEqual(35, cm.Preview(UpgradeSet(new Item(ItemDatabase.IronBlade, 14))).RatePercent);
            Assert.AreEqual(75, cm.Preview(UpgradeSet(new Item(ItemDatabase.IronBlade, 9) { Luck = true })).RatePercent);
        }

        [Test]
        public void ForgeRejectsWrongJewelCount()
        {
            var it = new Item(ItemDatabase.IronBlade, 11);
            var wrong = new List<Item> { it, new Item(ItemDatabase.VortexCrystal), new Item(ItemDatabase.RuneOfDawn), new Item(ItemDatabase.RuneOfEclipse) };
            long gold = long.MaxValue;
            var r = new ArcaneForge(new FixedRandom(0)).Mix(wrong, ref gold);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(11, it.Level);
        }

        [Test]
        public void ForgeInsufficientGoldChangesNothing()
        {
            var it = new Item(ItemDatabase.IronBlade, 9);
            long gold = 10;
            var r = new ArcaneForge(new FixedRandom(0)).Mix(UpgradeSet(it), ref gold);
            Assert.IsFalse(r.Success); Assert.AreEqual(9, it.Level); Assert.AreEqual(10, gold);
        }

        [Test]
        public void VortexWeaponRecipeCreatesBlade()
        {
            var items = new List<Item> { new Item(ItemDatabase.IronBlade, 6) { Luck = true }, new Item(ItemDatabase.VortexCrystal), new Item(ItemDatabase.RuneOfDawn) };
            var cm = new ArcaneForge(new FixedRandom(0));
            Assert.AreEqual(24 + 5 + 8, cm.Preview(items).RatePercent);
            long gold = 1_000_000;
            var r = cm.Mix(items, ref gold);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(ItemDatabase.VortexBlade, r.Outputs[0].Def);
        }

        [Test]
        public void StatisticalUpgradeRateMatchesTable()
        {
            var cm = new ArcaneForge(new SystemRandom(1234));
            int ok = 0, n = 20000;
            for (int i = 0; i < n; i++)
            {
                long gold = long.MaxValue;
                if (cm.Mix(UpgradeSet(new Item(ItemDatabase.IronBlade, 11)), ref gold).Success) ok++;
            }
            Assert.AreEqual(0.50, (double)ok / n, 0.02);
        }
    }
}
