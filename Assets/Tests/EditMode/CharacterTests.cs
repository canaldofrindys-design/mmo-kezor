using Kezor.Core.Characters;
using Kezor.Core.Items;
using NUnit.Framework;

namespace Kezor.Tests
{
    public class CharacterTests
    {
        [Test]
        public void ClassesHaveDistinctBaseStats()
        {
            var dk = new Character(1, "dk", CharacterClass.Warden);
            var dw = new Character(2, "dw", CharacterClass.Arcanist);
            Assert.Greater(dk.MaxHp, dw.MaxHp);
            Assert.Greater(dw.MaxMp, dk.MaxMp);
            Assert.Greater(dw.GetCombatStats().MaxMagic, 0);
            Assert.AreEqual(0, dk.GetCombatStats().MaxMagic);
        }

        [Test]
        public void LevelUpGrantsPoints()
        {
            var c = new Character(1, "a", CharacterClass.Ranger);
            int lv = c.GainExperience(Character.ExpToNext(1) + Character.ExpToNext(2));
            Assert.AreEqual(2, lv);
            Assert.AreEqual(3, c.Level);
            Assert.AreEqual(10, c.FreePoints);
        }

        [Test]
        public void AddPointsValidates()
        {
            var c = new Character(1, "a", CharacterClass.Warden);
            Assert.IsFalse(c.AddPoints(StatType.Strength, 1, out _));
            c.GainExperience(Character.ExpToNext(1));
            int hp = c.MaxHp;
            Assert.IsTrue(c.AddPoints(StatType.Vitality, 5, out _));
            Assert.AreEqual(hp + 15, c.MaxHp);
        }

        [Test]
        public void RebirthRequiresMaxLevelAndGold()
        {
            var c = new Character(1, "a", CharacterClass.Warden);
            Assert.IsFalse(c.TryRebirth(out _));
            c.GainExperience(long.MaxValue / 4);
            Assert.AreEqual(Character.MaxLevel, c.Level);
            Assert.IsFalse(c.TryRebirth(out _));
            c.Gold = Character.RebirthGoldCost(0);
            Assert.IsTrue(c.TryRebirth(out _));
            Assert.AreEqual(1, c.Level);
            Assert.AreEqual(Character.RebirthBonusPoints, c.FreePoints);
            Assert.AreEqual(ClassDefinition.Warden.BaseStats.Strength, c.Stats.Strength);
        }

        [Test]
        public void EquipChecksClassAndRequirements()
        {
            var dw = new Character(1, "a", CharacterClass.Arcanist);
            dw.Inventory.TryAdd(new Item(ItemDatabase.IronBlade), out var s);
            Assert.IsFalse(dw.Equip(s, out var err));
            StringAssert.Contains("classe", err);

            var dk = new Character(2, "b", CharacterClass.Warden);
            dk.Inventory.TryAdd(new Item(ItemDatabase.StormBlade), out s);
            Assert.IsFalse(dk.Equip(s, out err));
            StringAssert.Contains("força", err);
            dk.Inventory.TryAdd(new Item(ItemDatabase.IronBlade), out s);
            int before = dk.GetCombatStats().MaxDamage;
            Assert.IsTrue(dk.Equip(s, out _));
            Assert.AreEqual(before + 11, dk.GetCombatStats().MaxDamage);
        }
    }
}
