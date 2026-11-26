using System.Collections.Generic;
using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Joias: amuleto e dois anéis (só atributos), encaixe nos dedos, efeito nos status, drop, loja e salvamento.</summary>
    public class JewelryTests
    {
        private static Character Hero()
        {
            var c = new Character(1, "Joalheiro", CharacterClass.Warden);
            c.RestoreProgress(60, 0, 0, 0);
            return c;
        }

        private static int Bag(Character c, ItemDefinition d)
        {
            Assert.IsTrue(c.Inventory.TryAdd(new Item(d), out var slot));
            return slot;
        }

        [Test]
        public void RingsFillBothFingersThenSwapTheFirst()
        {
            var c = Hero();
            Assert.IsTrue(c.Equip(Bag(c, ItemDatabase.BasaltRing), out var e1), e1);
            Assert.IsTrue(c.Equip(Bag(c, ItemDatabase.HeartbeatRing), out var e2), e2);
            Assert.AreEqual(ItemDatabase.BasaltRing, c.Equipment[EquipSlot.Ring1].Def);
            Assert.AreEqual(ItemDatabase.HeartbeatRing, c.Equipment[EquipSlot.Ring2].Def);
            Assert.IsTrue(c.Equip(Bag(c, ItemDatabase.TideRing), out var e3), e3);   // os dois ocupados: troca o primeiro
            Assert.AreEqual(ItemDatabase.TideRing, c.Equipment[EquipSlot.Ring1].Def);
            Assert.IsTrue(c.Inventory.Items().Any(i => i.item.Def == ItemDatabase.BasaltRing), "o anel trocado volta para a mochila");
            Assert.IsTrue(c.Equip(Bag(c, ItemDatabase.LumeAmulet), out var e4), e4);
            Assert.AreEqual(ItemDatabase.LumeAmulet, c.Equipment[EquipSlot.Amulet].Def);
            Assert.AreEqual((1, 1), ItemSize.Of(ItemDatabase.LumeAmulet));
            Assert.AreEqual((1, 1), ItemSize.Of(ItemDatabase.KeenEmberRing));
            Assert.IsFalse(ItemDatabase.BasaltRing.IsUpgradable, "joias não aceitam runas");
        }

        [Test]
        public void JewelsChangeLifeManaResistanceAndDamage()
        {
            var c = Hero();
            int hp0 = c.MaxHp, mp0 = c.MaxMp;
            var s0 = c.GetCombatStats();
            c.Equip(Bag(c, ItemDatabase.LumeAmulet), out _);      // +120 HP, +3% resist
            c.Equip(Bag(c, ItemDatabase.TideRing), out _);        // +50 MP
            c.Equip(Bag(c, ItemDatabase.KeenEmberRing), out _);   // +5% dano
            var s1 = c.GetCombatStats();
            Assert.AreEqual(hp0 + 120, c.MaxHp);
            Assert.AreEqual(mp0 + 50, c.MaxMp);
            Assert.AreEqual(s0.DamageReductionPercent + 3, s1.DamageReductionPercent);
            Assert.AreEqual(s0.DamageBonusPercent + 5, s1.DamageBonusPercent);
            c.Hp = c.MaxHp; c.Mp = c.MaxMp;
            Assert.IsTrue(c.Unequip(EquipSlot.Amulet, out _));
            Assert.AreEqual(hp0, c.MaxHp);
            Assert.LessOrEqual(c.Hp, c.MaxHp, "a vida extra some ao tirar o amuleto");
            // joias não se desgastam
            int dur = c.Equipment[EquipSlot.Ring1].Durability;
            var wr = new SystemRandom(3);
            for (int i = 0; i < 400; i++) { c.WearEquipment(true, wr); c.WearEquipment(false, wr); }
            Assert.AreEqual(dur, c.Equipment[EquipSlot.Ring1].Durability);
        }

        [Test]
        public void JewelsDropAreSoldAndPersistThroughServerAndClient()
        {
            // drop: um chefe solta joia com frequência; monstros fracos só as de nível baixo
            var boss = MonsterCatalog.BlackGlassSovereign;
            var rng = new SystemRandom(7);
            int jewels = 0;
            for (int i = 0; i < 200; i++) jewels += DropSystem.Roll(boss, rng).Count(d => d.Def.IsJewel);
            Assert.Greater(jewels, 20);
            var slime = MonsterCatalog.MossSlime;
            for (int i = 0; i < 4000; i++)
                foreach (var d in DropSystem.Roll(slime, rng).Where(d => d.Def.IsJewel))
                    Assert.LessOrEqual(d.Def.DropLevel, slime.Level + 5);
            // loja: anéis comuns e o Amuleto do Lume à venda; os de nível 2 só caem
            Assert.Greater(ItemDatabase.BasaltRing.ShopPrice, 0);
            Assert.Greater(ItemDatabase.LumeAmulet.ShopPrice, 0);
            Assert.AreEqual(0, ItemDatabase.RunicAshAmulet.ShopPrice);
            Assert.AreEqual(5, ItemDatabase.All.Count(d => d.Kind == ItemKind.Ring));
            Assert.AreEqual(3, ItemDatabase.All.Count(d => d.Kind == ItemKind.Amulet));

            // salvar e carregar mantém os dois dedos e o amuleto
            var c = Hero();
            c.Equip(Bag(c, ItemDatabase.BasaltRing), out _);
            c.Equip(Bag(c, ItemDatabase.HeartbeatRing), out _);
            c.Equip(Bag(c, ItemDatabase.RunicAshAmulet), out _);
            var back = SaveData.CharacterFromJson((IDictionary<string, object?>)MiniJson.Read(MiniJson.Write(SaveData.CharacterToJson(c, includeVault: false)))!, 2);
            Assert.AreEqual(ItemDatabase.BasaltRing, back.Equipment[EquipSlot.Ring1].Def);
            Assert.AreEqual(ItemDatabase.HeartbeatRing, back.Equipment[EquipSlot.Ring2].Def);
            Assert.AreEqual(ItemDatabase.RunicAshAmulet, back.Equipment[EquipSlot.Amulet].Def);

            // o cliente recebe os slots novos e o servidor valida o EQUIP
            var gs = new GameServer(new SystemRandom(1), options: new ServerOptions { EventFirstDelay = 99999, AutosaveSeconds = 0 });
            gs.Handle(1, "CREATE Anelado GUA");
            var hero = gs.GetCharacter(1)!;
            hero.RestoreProgress(60, 0, 0, 0);
            hero.Inventory.TryAdd(new Item(ItemDatabase.BasaltRing), out var r1);
            hero.Inventory.TryAdd(new Item(ItemDatabase.TideRing), out var r2);
            StringAssert.StartsWith("OK", gs.Handle(1, $"EQUIP {r1}"));
            StringAssert.StartsWith("OK", gs.Handle(1, $"EQUIP {r2}"));
            var view = new ClientState();
            gs.Tick(0.2f);
            foreach (var l in gs.DrainOutbox(1)!) view.Process(l);
            Assert.AreEqual(ItemDatabase.BasaltRing, view.Equipment[EquipSlot.Ring1].Def);
            Assert.AreEqual(ItemDatabase.TideRing, view.Equipment[EquipSlot.Ring2].Def);
            StringAssert.StartsWith("OK", gs.Handle(1, "UNEQUIP Ring2"));
            Assert.IsFalse(hero.Equipment.ContainsKey(EquipSlot.Ring2));
        }
    }
}
