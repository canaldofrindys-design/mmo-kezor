using System.Collections.Generic;
using MuProto.Core;
using MuProto.Core.Items;
using NUnit.Framework;

namespace MuProto.Tests
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
            for (int i = 0; i < 6; i++) Assert.IsTrue(JewelService.Apply(new Item(ItemDatabase.JewelOfBless), it, rng).Success);
            Assert.AreEqual(6, it.Level);
            var r = JewelService.Apply(new Item(ItemDatabase.JewelOfBless), it, rng);
            Assert.IsFalse(r.Applied);
        }

        [Test]
        public void SoulSuccessAndFailure()
        {
            var it = new Item(ItemDatabase.IronBlade, 6);
            Assert.IsTrue(JewelService.Apply(new Item(ItemDatabase.JewelOfSoul), it, new FixedRandom(49)).Success);
            Assert.AreEqual(7, it.Level);
            var fail = JewelService.Apply(new Item(ItemDatabase.JewelOfSoul), it, new FixedRandom(50));
            Assert.IsTrue(fail.Applied); Assert.IsFalse(fail.Success);
            Assert.AreEqual(0, it.Level);
        }

        [Test]
        public void SoulLuckRaisesRate()
        {
            var it = new Item(ItemDatabase.IronBlade, 6) { Luck = true };
            Assert.IsTrue(JewelService.Apply(new Item(ItemDatabase.JewelOfSoul), it, new FixedRandom(74)).Success);
        }

        [Test]
        public void SoulRejectsNineAndAbove()
        {
            var it = new Item(ItemDatabase.IronBlade, 9);
            Assert.IsFalse(JewelService.Apply(new Item(ItemDatabase.JewelOfSoul), it, new FixedRandom(0)).Applied);
        }

        [Test]
        public void LifeAddsOptionAndCaps()
        {
            var it = new Item(ItemDatabase.LeatherArmor);
            for (int i = 0; i < 7; i++) JewelService.Apply(new Item(ItemDatabase.JewelOfLife), it, new FixedRandom(0));
            Assert.AreEqual(7, it.AdditionalOption);
            Assert.AreEqual(8 + 28, it.Defense);
            Assert.IsFalse(JewelService.Apply(new Item(ItemDatabase.JewelOfLife), it, new FixedRandom(0)).Applied);
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
            int n = ItemUpgradeRecipe.RequiredJewels(target.Level + 1);
            var l = new List<Item> { target, new Item(ItemDatabase.JewelOfChaos) };
            for (int i = 0; i < n; i++) { l.Add(new Item(ItemDatabase.JewelOfBless)); l.Add(new Item(ItemDatabase.JewelOfSoul)); }
            return l;
        }

        [Test]
        public void ChaosUpgradeSuccess()
        {
            var it = new Item(ItemDatabase.IronBlade, 9);
            var cm = new ChaosMachine(new FixedRandom(0));
            long zen = 10_000_000;
            var r = cm.Mix(UpgradeSet(it), ref zen);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(10, it.Level);
            Assert.AreEqual(60, r.RatePercent);
            Assert.AreEqual(10_000_000 - 2_000_000, zen);
            Assert.AreEqual(3, r.Consumed.Count);
            CollectionAssert.Contains(r.Outputs, it);
        }

        [Test]
        public void ChaosUpgradeFailureDestroysItem()
        {
            var it = new Item(ItemDatabase.IronBlade, 12);
            var cm = new ChaosMachine(new FixedRandom(99));
            long zen = 100_000_000;
            var set = UpgradeSet(it);
            var r = cm.Mix(set, ref zen);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(set.Count, r.Consumed.Count);
            Assert.IsEmpty(r.Outputs);
        }

        [Test]
        public void ChaosRatesAndLuckCap()
        {
            var cm = new ChaosMachine(new FixedRandom(0));
            Assert.AreEqual(35, cm.Preview(UpgradeSet(new Item(ItemDatabase.IronBlade, 14))).RatePercent);
            Assert.AreEqual(75, cm.Preview(UpgradeSet(new Item(ItemDatabase.IronBlade, 9) { Luck = true })).RatePercent);
        }

        [Test]
        public void ChaosRejectsWrongJewelCount()
        {
            var it = new Item(ItemDatabase.IronBlade, 11);
            var wrong = new List<Item> { it, new Item(ItemDatabase.JewelOfChaos), new Item(ItemDatabase.JewelOfBless), new Item(ItemDatabase.JewelOfSoul) };
            long zen = long.MaxValue;
            var r = new ChaosMachine(new FixedRandom(0)).Mix(wrong, ref zen);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(11, it.Level);
        }

        [Test]
        public void ChaosInsufficientZenChangesNothing()
        {
            var it = new Item(ItemDatabase.IronBlade, 9);
            long zen = 10;
            var r = new ChaosMachine(new FixedRandom(0)).Mix(UpgradeSet(it), ref zen);
            Assert.IsFalse(r.Success); Assert.AreEqual(9, it.Level); Assert.AreEqual(10, zen);
        }

        [Test]
        public void ChaosWeaponRecipe()
        {
            var items = new List<Item> { new Item(ItemDatabase.IronBlade, 6) { Luck = true }, new Item(ItemDatabase.JewelOfChaos), new Item(ItemDatabase.JewelOfBless) };
            var cm = new ChaosMachine(new FixedRandom(0));
            Assert.AreEqual(24 + 5 + 8, cm.Preview(items).RatePercent);
            long zen = 1_000_000;
            var r = cm.Mix(items, ref zen);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(ItemDatabase.ChaosRuneBlade, r.Outputs[0].Def);
        }

        [Test]
        public void StatisticalUpgradeRateMatchesTable()
        {
            var cm = new ChaosMachine(new SystemRandom(1234));
            int ok = 0, n = 20000;
            for (int i = 0; i < n; i++)
            {
                long zen = long.MaxValue;
                if (cm.Mix(UpgradeSet(new Item(ItemDatabase.IronBlade, 11)), ref zen).Success) ok++;
            }
            Assert.AreEqual(0.50, (double)ok / n, 0.02);
        }
    }
}
