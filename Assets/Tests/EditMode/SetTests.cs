using System.Linq;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Conjuntos de armadura: 5 peças por conjunto e bônus de conjunto completo.</summary>
    public class SetTests
    {
        [Test]
        public void EverySetHasFivePiecesForEachClassThatCanWearIt()
        {
            foreach (ArmorSet set in System.Enum.GetValues(typeof(ArmorSet)))
            {
                if (set == ArmorSet.None) continue;
                var pieces = ItemDatabase.All.Where(d => d.Set == set).ToList();
                CollectionAssert.AreEquivalent(ArmorSets.Pieces, pieces.Select(p => p.Slot), set.ToString());
                var mask = pieces[0].AllowedClasses;
                Assert.IsTrue(pieces.All(p => p.AllowedClasses == mask), $"{set}: todas as peças servem para as mesmas classes");
            }
            // Cada classe tem pelo menos 2 conjuntos completos
            foreach (var cls in new[] { ClassMask.Warden, ClassMask.Arcanist, ClassMask.Ranger })
                Assert.GreaterOrEqual(ItemDatabase.All.Where(d => d.Set != ArmorSet.None && (d.AllowedClasses & cls) != 0).Select(d => d.Set).Distinct().Count(), 2, cls.ToString());
        }

        private static Character Wearing(CharacterClass cls, params ItemDefinition[] defs)
        {
            var c = new Character(1, "Teste", cls);
            foreach (var d in defs) c.Equipment[d.Slot] = new Item(d);
            return c;
        }

        [Test]
        public void FullSetGivesBonusAndFourPiecesDoNot()
        {
            var rune = ItemDatabase.All.Where(d => d.Set == ArmorSet.RunePlate).ToArray();
            var four = Wearing(CharacterClass.Warden, rune.Take(4).ToArray());
            var five = Wearing(CharacterClass.Warden, rune);
            Assert.AreEqual(ArmorSet.None, four.ActiveSet);
            Assert.AreEqual(ArmorSet.RunePlate, five.ActiveSet);
            var s4 = four.GetCombatStats();
            var s5 = five.GetCombatStats();
            var bonus = ArmorSets.Bonus(ArmorSet.RunePlate);
            int lastPieceDef = new Item(rune[4]).Defense;
            Assert.AreEqual(s4.Defense + lastPieceDef + bonus.Defense, s5.Defense);
            Assert.AreEqual(s4.DamageReductionPercent + bonus.ReductionPercent, s5.DamageReductionPercent);
            Assert.AreEqual(bonus.DamagePercent, s5.DamageBonusPercent);

            // Peça quebrada (durabilidade 0) desliga o bônus; misturar conjuntos também
            five.Equipment[EquipSlot.Boots].Durability = 0;
            Assert.AreEqual(ArmorSet.None, five.ActiveSet);
            var mixed = Wearing(CharacterClass.Warden, ItemDatabase.RunePlateHelm, ItemDatabase.RunePlateArmor, ItemDatabase.RunePlatePants, ItemDatabase.RunePlateGloves, ItemDatabase.ScaleBoots);
            Assert.AreEqual(ArmorSet.None, mixed.ActiveSet);
        }

        [Test]
        public void VeilSetBoostsMagic()
        {
            var veil = ItemDatabase.All.Where(d => d.Set == ArmorSet.Veil).ToArray();
            var four = Wearing(CharacterClass.Arcanist, veil.Where(d => d.Slot != EquipSlot.Gloves).Append(ItemDatabase.LeatherGloves).ToArray());
            var five = Wearing(CharacterClass.Arcanist, veil);
            Assert.AreEqual(ArmorSet.Veil, five.ActiveSet);
            Assert.Greater(five.GetCombatStats().MaxMagic, four.GetCombatStats().MaxMagic);
            StringAssert.Contains("poder mágico +15%", new Item(veil[0]).Describe());
        }
    }
}
