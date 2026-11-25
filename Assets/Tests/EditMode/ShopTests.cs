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
    /// <summary>Loja pessoal: abrir na vila, anunciar, comprar com validação (sem duplicar, preço trocado, mochila cheia, dono saiu).</summary>
    public class ShopTests
    {
        private static (GameServer gs, Character seller, Character buyer, int sword) Market()
        {
            var gs = new GameServer(new SystemRandom(5), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999 });
            gs.Handle(1, "CREATE Vendedor GUA");
            gs.Handle(2, "CREATE Freguesa ARC");
            var s = gs.GetCharacter(1)!; var b = gs.GetCharacter(2)!;
            s.X = MapDefinition.ValeCenter - 2; s.Z = MapDefinition.ValeCenter - 7; b.X = MapDefinition.ValeCenter - 0.5f; b.Z = MapDefinition.ValeCenter - 7;
            b.Gold = 1_000_000;
            s.Inventory.TryAdd(new Item(ItemDatabase.StormBlade, 7), out var sword);
            return (gs, s, b, sword);
        }

        private static int Count(Character c, ItemDefinition d) => Enumerable.Range(0, Character.InventorySize).Count(i => c.Inventory.Get(i)?.Def == d);

        [Test]
        public void OpenSellAndBuyOnce()
        {
            var (gs, s, b, sword) = Market();
            StringAssert.StartsWith("ERR", gs.Handle(1, "SHOP OPEN Vazia"));             // sem itens
            StringAssert.StartsWith("OK", gs.Handle(1, $"SHOP ADD {sword} 250000"));
            StringAssert.StartsWith("OK", gs.Handle(1, "SHOP OPEN Forja do Vendedor"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "MOVE 70 70"));                  // parado enquanto vende
            gs.Tick(0.2f);
            var view = new ClientState();
            foreach (var l in gs.DrainOutbox(2)!) view.Process(l);
            Assert.AreEqual("Forja do Vendedor", view.Entities[s.Id].Shop, "a placa aparece para os outros");

            StringAssert.StartsWith("OK", gs.Handle(2, "SHOP VIEW Vendedor"));
            foreach (var l in gs.DrainOutbox(2)!) view.Process(l);
            Assert.AreEqual(250000, view.ShopListings.Single().price);
            Assert.AreEqual(ItemDatabase.StormBlade, view.ShopListings.Single().item.Def);

            long sg = s.Gold;
            StringAssert.StartsWith("OK", gs.Handle(2, $"SHOP BUY Vendedor {sword} 250000"));
            StringAssert.StartsWith("ERR", gs.Handle(2, $"SHOP BUY Vendedor {sword} 250000"));   // repetir não duplica
            Assert.AreEqual(1, Count(b, ItemDatabase.StormBlade));
            Assert.AreEqual(0, Count(s, ItemDatabase.StormBlade));
            Assert.AreEqual(750_000, b.Gold);
            Assert.AreEqual(sg + 250_000, s.Gold);
            Assert.IsFalse(gs.ShopOfSession(1)!.Open, "vendeu tudo: fechou");
        }

        [Test]
        public void PriceChangeItemMovedAndFullBagAreRejected()
        {
            var (gs, s, b, sword) = Market();
            gs.Handle(1, $"SHOP ADD {sword} 1000");
            s.Inventory.TryAdd(new Item(ItemDatabase.WardenShield, 3), out var shield);
            gs.Handle(1, $"SHOP ADD {shield} 5000");
            gs.Handle(1, "SHOP OPEN Bancada");
            // o vendedor tira e põe de novo com preço maior: o comprador que viu 1000 não paga 900000
            gs.Handle(1, $"SHOP REMOVE {sword}");
            gs.Handle(1, $"SHOP ADD {sword} 900000");
            StringAssert.Contains("preço", gs.Handle(2, $"SHOP BUY Vendedor {sword} 1000"));
            Assert.AreEqual(1_000_000, b.Gold);

            // item saiu da mochila do vendedor (guardou no baú): anúncio cai, nada é duplicado
            var moved = s.Inventory.Remove(shield)!;
            s.Vault.Items.TryAdd(moved, out _);
            StringAssert.StartsWith("ERR", gs.Handle(2, $"SHOP BUY Vendedor {shield} 5000"));
            Assert.AreEqual(0, Count(b, ItemDatabase.WardenShield));

            // mochila do comprador cheia
            while (b.Inventory.TryAdd(new Item(ItemDatabase.LeatherHelm), out _)) { }
            StringAssert.Contains("cheia", gs.Handle(2, $"SHOP BUY Vendedor {sword} 900000"));
            Assert.AreEqual(1, Count(s, ItemDatabase.StormBlade));
        }

        [Test]
        public void ShopOnlyInTownAndClosesWhenOwnerLeaves()
        {
            var (gs, s, b, sword) = Market();
            gs.Handle(1, $"SHOP ADD {sword} 1000");
            s.X = 40; s.Z = 40;
            StringAssert.Contains("vila", gs.Handle(1, "SHOP OPEN Longe"));
            s.X = MapDefinition.ValeCenter - 2; s.Z = MapDefinition.ValeCenter - 7;
            gs.Handle(1, "SHOP OPEN Perto");
            b.X = MapDefinition.ValeCenter + 10;
            StringAssert.StartsWith("ERR", gs.Handle(2, "SHOP VIEW Vendedor"));          // comprador longe
            b.X = MapDefinition.ValeCenter - 0.5f;
            gs.Disconnect(1);
            gs.Tick(0.05f);
            StringAssert.StartsWith("ERR", gs.Handle(2, $"SHOP BUY Vendedor {sword} 1000"));
            Assert.AreEqual(1_000_000, b.Gold);
        }
    }
}
