using MuProto.Core;
using MuProto.Core.Characters;
using MuProto.Core.Combat;
using MuProto.Core.Items;
using MuProto.Core.Server;
using MuProto.Core.World;
using NUnit.Framework;

namespace MuProto.Tests
{
    public class CombatTests
    {
        [Test]
        public void HitChanceClamped()
        {
            Assert.AreEqual(95, CombatResolver.HitChancePercent(1000, 1));
            Assert.AreEqual(5, CombatResolver.HitChancePercent(1, 1000));
            Assert.AreEqual(50, CombatResolver.HitChancePercent(10, 10));
        }

        [Test]
        public void DamageSubtractsDefenseWithFloor()
        {
            var dk = new Character(1, "a", CharacterClass.DarkKnight);
            var m = new Monster(1, MonsterCatalog.Golem, 0, 0);
            var r = CombatResolver.Resolve(dk, m, SkillCatalog.BasicAttack, new FixedRandom(0));
            Assert.AreEqual(HitType.Normal, r.Type);
            Assert.AreEqual(1, r.Damage); // dano baixo vs defesa alta => piso
        }

        [Test]
        public void MissDealsNoDamage()
        {
            var dk = new Character(1, "a", CharacterClass.DarkKnight);
            var m = new Monster(1, MonsterCatalog.Slime, 0, 0);
            var r = CombatResolver.Resolve(dk, m, SkillCatalog.BasicAttack, new FixedRandom(99));
            Assert.AreEqual(HitType.Miss, r.Type);
            Assert.AreEqual(m.MaxHp, m.Hp);
        }

        [Test]
        public void WizardSkillUsesMagic()
        {
            var dw = new Character(1, "a", CharacterClass.DarkWizard);
            var m = new Monster(1, MonsterCatalog.Slime, 0, 0);
            var skill = SkillCatalog.Find(3)!;
            var r = CombatResolver.Resolve(dw, m, skill, new FixedRandom(0, 99, 99, 0));
            var cs = dw.GetCombatStats();
            Assert.AreEqual(cs.MinMagic + skill.FlatDamage - MonsterCatalog.Slime.Defense, r.Damage);
        }

        [Test]
        public void UpgradedWeaponHitsHarder()
        {
            var a = new Item(ItemDatabase.IronBlade, 0);
            var b = new Item(ItemDatabase.IronBlade, 15);
            Assert.AreEqual(a.MaxDamage + 66, b.MaxDamage);
        }
    }

    public class ServerTests
    {
        [Test]
        public void FullFlowCreateFightLevel()
        {
            var gs = new GameServer(new SystemRandom(42));
            StringAssert.StartsWith("ERR", gs.Handle(1, "STATS"));
            StringAssert.StartsWith("OK", gs.Handle(1, "CREATE Hero DK"));
            StringAssert.StartsWith("ERR", gs.Handle(2, "CREATE Hero DW"));
            var c = gs.GetCharacter(1)!;
            Assert.IsTrue(c.Equipment.ContainsKey(EquipSlot.RightHand));

            int slime = 0;
            foreach (var m in gs.Monsters.Values) if (m.Def == MonsterCatalog.Slime) { slime = m.Id; break; }
            for (int i = 0; i < 200 && c.Level == 1; i++)
            {
                gs.Tick(1f);
                var resp = gs.Handle(1, $"ATTACK {slime} 1");
                if (resp.Contains("mana")) { c.Mp = c.MaxMp; }
            }
            Assert.Greater(c.Level, 1);
            StringAssert.StartsWith("OK", gs.Handle(1, "ADD STR 5"));
        }

        [Test]
        public void ServerRejectsCheats()
        {
            var gs = new GameServer(new SystemRandom(1));
            gs.Handle(1, "CREATE Mage DW");
            StringAssert.StartsWith("ERR", gs.Handle(1, "ADD ENE 999"));
            int id = 0; foreach (var m in gs.Monsters.Values) { id = m.Id; break; }
            StringAssert.Contains("classe", gs.Handle(1, $"ATTACK {id} 1"));     // skill de DK
            gs.Handle(1, $"ATTACK {id} 3");
            StringAssert.Contains("rápido", gs.Handle(1, $"ATTACK {id} 3"));  // cooldown
            StringAssert.StartsWith("ERR", gs.Handle(1, "MIX 0,0"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "ATTACK abc"));
        }

        [Test]
        public void JewelAndMixThroughProtocol()
        {
            var gs = new GameServer(new FixedRandom(0));
            gs.Handle(1, "CREATE Smith DK");
            var c = gs.GetCharacter(1)!;
            c.Zen = 100_000_000;
            c.Inventory.TryAdd(new Item(ItemDatabase.StormBlade, 9), out var w);
            c.Inventory.TryAdd(new Item(ItemDatabase.JewelOfBless), out var b);
            c.Inventory.TryAdd(new Item(ItemDatabase.JewelOfSoul), out var s);
            c.Inventory.TryAdd(new Item(ItemDatabase.JewelOfChaos), out var ch);
            StringAssert.Contains("60%", gs.Handle(1, $"MIXPREVIEW {w},{b},{s},{ch}"));
            StringAssert.StartsWith("OK", gs.Handle(1, $"MIX {w},{b},{s},{ch}"));
            Assert.AreEqual(10, c.Inventory.Get(w)!.Level);
            Assert.IsNull(c.Inventory.Get(b));
            Assert.AreEqual(100_000_000 - 2_000_000, c.Zen);
        }

        [Test]
        public void MonstersRespawn()
        {
            var gs = new GameServer(new SystemRandom(3));
            Monster m = null!; foreach (var x in gs.Monsters.Values) { m = x; break; }
            m.Hp = 0; m.RespawnTimer = 2;
            gs.Tick(1); Assert.IsTrue(m.IsDead);
            gs.Tick(1.5f); Assert.IsFalse(m.IsDead);
        }
    }
}
