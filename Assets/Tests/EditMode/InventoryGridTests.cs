using System.Collections.Generic;
using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Inventário em grade com tamanho por item: encaixe, cheio, mover, troca, baú, equipar e migração de saves.</summary>
    public class InventoryGridTests
    {
        [Test]
        public void SizesFollowTheItemKind()
        {
            Assert.AreEqual((1, 1), ItemSize.Of(ItemDatabase.HealthPotion));
            Assert.AreEqual((2, 2), ItemSize.Of(ItemDatabase.LeatherHelm));
            Assert.AreEqual((2, 3), ItemSize.Of(ItemDatabase.WarAxe));
            Assert.AreEqual((2, 4), ItemSize.Of(ItemDatabase.NorthwindBow));
            Assert.AreEqual((3, 2), ItemSize.Of(ItemDatabase.AshWings));
            Assert.AreEqual((1, 4), ItemSize.Of(ItemDatabase.OracleStaff));
            Assert.AreEqual((2, 3), ItemSize.Of(ItemDatabase.ScaleArmor));
        }

        [Test]
        public void ItemsOccupyCellsAndDoNotOverlap()
        {
            var inv = new Inventory(64, 8);
            Assert.IsTrue(inv.TryAdd(new Item(ItemDatabase.ScaleArmor), out var a));
            Assert.AreEqual(0, a);
            foreach (var cell in new[] { 0, 1, 8, 9, 16, 17 }) Assert.AreEqual(0, inv.AnchorAt(cell), "armadura 2×3 ocupa 6 casas");
            Assert.AreEqual(-1, inv.AnchorAt(2));
            Assert.IsTrue(inv.TryAdd(new Item(ItemDatabase.LeatherHelm), out var h));
            Assert.AreEqual(2, h, "o elmo vai para a primeira casa livre onde cabe");
            Assert.IsFalse(inv.Put(9, new Item(ItemDatabase.HealthPotion)), "casa ocupada");
            Assert.IsFalse(inv.Fits(ItemDatabase.WarAxe, 7), "passa da borda direita");
            Assert.IsFalse(inv.Fits(ItemDatabase.OracleStaff, 8 * 6), "passa da borda de baixo");
            Assert.AreEqual(64 - 6 - 4, inv.FreeSlots);
        }

        [Test]
        public void FullGridRejectsBigItemsButStacksPotions()
        {
            var inv = new Inventory(64, 8);
            int helms = 0;
            while (inv.TryAdd(new Item(ItemDatabase.LeatherHelm), out _)) helms++;
            Assert.AreEqual(16, helms, "16 elmos 2×2 enchem 8×8");
            Assert.IsFalse(inv.TryAdd(new Item(ItemDatabase.HealthPotion), out _));
            var inv2 = new Inventory(64, 8);
            inv2.TryAdd(new Item(ItemDatabase.HealthPotion) { Quantity = 10 }, out var p);
            while (inv2.TryAdd(new Item(ItemDatabase.ScaleArmor), out _)) { }
            Assert.IsTrue(inv2.TryAdd(new Item(ItemDatabase.HealthPotion) { Quantity = 5 }, out var p2), "poção empilha mesmo com a grade cheia");
            Assert.AreEqual(p, p2);
            Assert.AreEqual(15, inv2.Get(p)!.Quantity);
        }

        [Test]
        public void MoveInsideTheGridChecksTheFit()
        {
            var inv = new Inventory(64, 8);
            inv.TryAdd(new Item(ItemDatabase.WarAxe), out var axe);
            inv.TryAdd(new Item(ItemDatabase.LeatherHelm), out var helm);
            Assert.IsTrue(inv.Move(axe, 8 * 5 + 6));
            Assert.AreEqual(8 * 5 + 6, inv.AnchorAt(8 * 7 + 7));
            Assert.IsNull(inv.Get(axe));
            Assert.IsFalse(inv.Move(helm, 8 * 5 + 5), "colide com o machado");
            Assert.IsTrue(inv.Move(8 * 5 + 6, 8 * 5 + 6), "mover para o mesmo lugar é ok");
            Assert.IsTrue(inv.Move(8 * 5 + 6, 8 * 5 + 5), "deslocar uma casa sobre as próprias casas");
        }

        [Test]
        public void ServerMoveCommandAndVaultCellDrop()
        {
            var gs = new GameServer(new SystemRandom(5), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999 });
            gs.Handle(1, "CREATE Iris GUA");
            var c = gs.GetCharacter(1)!;
            c.Inventory.TryAdd(new Item(ItemDatabase.ScaleArmor), out var slot);
            StringAssert.StartsWith("OK", gs.Handle(1, $"IMOVE {slot} 30"));
            Assert.IsNotNull(c.Inventory.Get(30));
            StringAssert.StartsWith("ERR", gs.Handle(1, "IMOVE 30 63"));
            // baú com casa escolhida
            var vault = MapDefinition.Get(MapDefinition.ValeId).Npcs.First(n => n.Role == "vault");
            c.X = vault.X; c.Z = vault.Z - 1.2f;
            StringAssert.StartsWith("OK", gs.Handle(1, "VAULT"));
            StringAssert.StartsWith("OK", gs.Handle(1, "VPUT 30 1 42"));
            Assert.AreEqual(ItemDatabase.ScaleArmor, c.Vault.Items.Get(42)?.Def);
            StringAssert.StartsWith("OK", gs.Handle(1, "VMOVE 42 0"));
            Assert.AreEqual(ItemDatabase.ScaleArmor, c.Vault.Items.Get(0)?.Def);
        }

        [Test]
        public void TradeChecksRealSpaceForBigItems()
        {
            var gs = new GameServer(new SystemRandom(3), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999 });
            gs.Handle(1, "CREATE Ana GUA"); gs.Handle(2, "CREATE Beto PAT");
            var a = gs.GetCharacter(1)!; var b = gs.GetCharacter(2)!;
            a.X = MapDefinition.ValeCenter; a.Z = MapDefinition.ValeCenter - 8; b.X = MapDefinition.ValeCenter + 1.5f; b.Z = MapDefinition.ValeCenter - 8;
            a.Inventory.TryAdd(new Item(ItemDatabase.NorthwindBow), out var bow);    // 2×4
            while (b.Inventory.TryAdd(new Item(ItemDatabase.LeatherHelm), out _)) { }              // Beto: 8×8 cheio de elmos
            gs.Handle(1, "TRADE Beto"); gs.Handle(2, "TRADE ACCEPT");
            gs.Handle(1, $"TRADE ADD {bow}");
            gs.Handle(1, "TRADE LOCK"); gs.Handle(2, "TRADE LOCK");
            gs.Handle(1, "TRADE CONFIRM");
            var r = gs.Handle(2, "TRADE CONFIRM");
            StringAssert.Contains("espaço", r, "o arco 2×4 não cabe nas casas soltas");
            Assert.IsNotNull(a.Inventory.Get(bow), "nada mudou");
        }

        [Test]
        public void EquipSwapNeedsRoomForTheOldItem()
        {
            var gs = new GameServer(new SystemRandom(2), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999 });
            gs.Handle(1, "CREATE Rui GUA");
            var c = gs.GetCharacter(1)!;
            c.Stats = new Attributes(200, 100, 50, 20);
            c.Equipment[EquipSlot.RightHand] = new Item(ItemDatabase.WarAxe);                      // 2×3 equipado
            for (int i = 0; i < 64; i++) c.Inventory.Remove(i);
            Assert.IsTrue(c.Inventory.Put(0, new Item(ItemDatabase.IronBlade)));                  // 1×3 na mochila
            while (c.Inventory.TryAdd(new Item(ItemDatabase.HealthPotion) { Quantity = 1 }, out var s) && c.Inventory.Get(s)!.Quantity == 1)
                c.Inventory.Get(s)!.Quantity = 50;                                                  // enche o resto com pilhas cheias
            Assert.IsFalse(c.Equip(0, out var err), "o machado 2×3 não cabe onde a espada 1×3 estava");
            StringAssert.Contains("espaço", err);
            Assert.AreEqual(ItemDatabase.WarAxe, c.Equipment[EquipSlot.RightHand].Def);
            Assert.AreEqual(ItemDatabase.IronBlade, c.Inventory.Get(0)?.Def);
        }

        [Test]
        public void OldSavesAreRepackedAndOverflowGoesToTheVault()
        {
            var c = new Character(9, "Velho", CharacterClass.Warden);
            var json = (Dictionary<string, object?>)SaveData.CharacterToJson(c, includeVault: true);
            // save antigo: 20 armaduras em slots 0..19 (cada uma ocupava 1 casa)
            var list = new List<object?>();
            for (int i = 0; i < 20; i++) { var j = SaveData.ItemToJson(new Item(ItemDatabase.ScaleArmor)); j["slot"] = i; list.Add(j); }
            json["inventory"] = list;
            var back = SaveData.CharacterFromJson((IDictionary<string, object?>)MiniJson.Read(MiniJson.Write(json))!, 9);
            int inBag = back.Inventory.Items().Count(), inVault = back.Vault.Items.Items().Count();
            Assert.AreEqual(8, inBag, "8×8 cabe 8 armaduras 2×3 (2 fileiras de 4)");
            Assert.AreEqual(12, inVault, "o resto foi para o baú");
            Assert.AreEqual(20, inBag + inVault, "nenhum item perdido");
        }
    }
}
