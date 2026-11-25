using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.Server;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Troca segura: pedir/aceitar, travar e confirmar, destravar a cada mudança, troca atômica sem duplicação.</summary>
    public class TradeTests
    {
        private static (GameServer gs, Character a, Character b) Pair()
        {
            var gs = new GameServer(new SystemRandom(3), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999 });
            gs.Handle(1, "CREATE Ana GUA");
            gs.Handle(2, "CREATE Beto PAT");
            var a = gs.GetCharacter(1)!; var b = gs.GetCharacter(2)!;
            a.X = 80; a.Z = 72; b.X = 81.5f; b.Z = 72;
            return (gs, a, b);
        }

        private static int Count(Character c, ItemDefinition d) => Enumerable.Range(0, Character.InventorySize).Count(i => c.Inventory.Get(i)?.Def == d);

        private static void Open(GameServer gs)
        {
            StringAssert.StartsWith("OK", gs.Handle(1, "TRADE Beto"));
            StringAssert.StartsWith("OK", gs.Handle(2, "TRADE ACCEPT"));
        }

        [Test]
        public void FullTradeSwapsItemsAndGoldExactlyOnce()
        {
            var (gs, a, b) = Pair();
            a.Inventory.TryAdd(new Item(ItemDatabase.StormBlade, 9) { Luck = true }, out var sword);
            b.Inventory.TryAdd(new Item(ItemDatabase.ScaleArmor, 4), out var armor);
            long ga = a.Gold, gb = b.Gold;
            Open(gs);
            StringAssert.StartsWith("OK", gs.Handle(1, $"TRADE ADD {sword}"));
            StringAssert.StartsWith("OK", gs.Handle(1, "TRADE GOLD 5000"));
            StringAssert.StartsWith("OK", gs.Handle(2, $"TRADE ADD {armor}"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "TRADE CONFIRM"));          // ninguém travou ainda
            gs.Handle(1, "TRADE LOCK"); gs.Handle(2, "TRADE LOCK");
            StringAssert.StartsWith("OK", gs.Handle(1, "TRADE CONFIRM"));
            Assert.AreEqual(ItemDatabase.StormBlade, a.Inventory.Get(sword)!.Def, "ainda não trocou com um só confirmando");

            // O cliente vê os dois lados
            var view = new ClientState();
            foreach (var l in gs.DrainOutbox(2)!) view.Process(l);
            Assert.IsTrue(view.TradeOpen);
            Assert.AreEqual("Ana", view.TradePartner);
            Assert.AreEqual(5000, view.TheirTradeGold);
            Assert.AreEqual(ItemDatabase.StormBlade, view.TheirTradeItems.Single().Def);
            Assert.IsTrue(view.TheirTradeConfirmed);

            StringAssert.StartsWith("OK", gs.Handle(2, "TRADE CONFIRM"));
            Assert.AreEqual(0, Count(a, ItemDatabase.StormBlade)); Assert.AreEqual(1, Count(b, ItemDatabase.StormBlade));
            Assert.AreEqual(1, Count(a, ItemDatabase.ScaleArmor)); Assert.AreEqual(0, Count(b, ItemDatabase.ScaleArmor));
            Assert.AreEqual(ga - 5000, a.Gold); Assert.AreEqual(gb + 5000, b.Gold);
            Assert.IsNull(gs.TradeOfSession(1));
            StringAssert.StartsWith("ERR", gs.Handle(2, "TRADE CONFIRM"));          // confirmar de novo não faz nada
            Assert.AreEqual(1, Count(b, ItemDatabase.StormBlade));
        }

        [Test]
        public void AnyChangeUnlocksBothSides()
        {
            var (gs, a, b) = Pair();
            Open(gs);
            gs.Handle(1, "TRADE GOLD 100"); gs.Handle(1, "TRADE LOCK"); gs.Handle(2, "TRADE LOCK");
            var t = gs.TradeOfSession(1)!;
            Assert.IsTrue(t.Sides.All(s => s.Locked));
            gs.Handle(2, "TRADE GOLD 50");                                            // mudou a oferta
            Assert.IsTrue(t.Sides.All(s => !s.Locked && !s.Confirmed));

            // Usar parte da pilha oferecida também destrava (o servidor percebe no tick)
            int pot = Enumerable.Range(0, Character.InventorySize).First(i => a.Inventory.Get(i)?.Def == ItemDatabase.HealthPotion);
            gs.Handle(1, $"TRADE ADD {pot}");
            gs.Handle(1, "TRADE LOCK"); gs.Handle(2, "TRADE LOCK");
            a.Inventory.Get(pot)!.Quantity -= 3;
            gs.Tick(0.05f);
            Assert.IsFalse(t.Sides[0].Locked || t.Sides[1].Locked);
        }

        [Test]
        public void ItemSoldOrMovedDuringTradeIsNotDuplicated()
        {
            var (gs, a, b) = Pair();
            a.Inventory.TryAdd(new Item(ItemDatabase.RunePlateHelm, 7), out var helm);
            Open(gs);
            gs.Handle(1, $"TRADE ADD {helm}");
            gs.Handle(1, "TRADE LOCK"); gs.Handle(2, "TRADE LOCK");
            gs.Handle(1, "TRADE CONFIRM");
            // Ana tira o elmo da mochila (por exemplo, guardando no baú) antes do Beto confirmar
            var moved = a.Inventory.Remove(helm)!;
            a.Vault.Items.TryAdd(moved, out _);
            var r = gs.Handle(2, "TRADE CONFIRM");
            StringAssert.StartsWith("ERR", r);
            Assert.AreEqual(0, Count(b, ItemDatabase.RunePlateHelm), "o Beto não pode receber uma cópia");
            Assert.IsNull(gs.TradeOfSession(1), "a troca foi cancelada");
        }

        [Test]
        public void FullInventoryCancelsWithoutChanges()
        {
            var (gs, a, b) = Pair();
            a.Inventory.TryAdd(new Item(ItemDatabase.WarAxe), out var axe1);
            a.Inventory.TryAdd(new Item(ItemDatabase.WarAxe), out var axe2);
            while (b.Inventory.TryAdd(new Item(ItemDatabase.LeatherHelm), out _)) { }
            Open(gs);
            gs.Handle(1, $"TRADE ADD {axe1}"); gs.Handle(1, $"TRADE ADD {axe2}");
            gs.Handle(1, "TRADE LOCK"); gs.Handle(2, "TRADE LOCK");
            gs.Handle(1, "TRADE CONFIRM");
            StringAssert.Contains("espaço", gs.Handle(2, "TRADE CONFIRM"));
            Assert.AreEqual(2, Count(a, ItemDatabase.WarAxe));
            Assert.AreEqual(0, Count(b, ItemDatabase.WarAxe));
        }

        [Test]
        public void DistanceAndRulesAreChecked()
        {
            var (gs, a, b) = Pair();
            b.X = 90;                                                                  // longe
            StringAssert.StartsWith("ERR", gs.Handle(1, "TRADE Beto"));
            b.X = 81.5f;
            Open(gs);
            StringAssert.StartsWith("ERR", gs.Handle(1, $"TRADE GOLD {a.Gold + 1}"));
            var bound = new ItemDefinition { Id = 9998, Name = "Carta Selada", Kind = ItemKind.Material, Bound = true };
            a.Inventory.TryAdd(new Item(bound), out var bslot);
            StringAssert.Contains("vinculado", gs.Handle(1, $"TRADE ADD {bslot}"));
            b.X = 95;                                                                  // se afastou com a troca aberta
            gs.Tick(0.05f);
            Assert.IsNull(gs.TradeOfSession(1));
            Assert.IsNull(gs.TradeOfSession(2));
        }
    }
}
