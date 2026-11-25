using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.Server;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>
    /// "Clique para pegar, clique para soltar": o cliente só segura o item no cursor; cada soltura vira um comando que o
    /// servidor valida (mover, trocar com item do mesmo tamanho, largar no chão). Devolver não manda nada.
    /// </summary>
    public class ItemHandTests
    {
        private static (GameServer gs, Character c) Hero()
        {
            var gs = new GameServer(new SystemRandom(3), options: new ServerOptions { EventFirstDelay = 99999, AutosaveSeconds = 0 });
            gs.Handle(1, "CREATE Mochileiro GUA");
            var c = gs.GetCharacter(1)!;
            while (c.Inventory.Items().Any()) c.Inventory.Remove(c.Inventory.Items().First().slot);
            return (gs, c);
        }

        [Test]
        public void PlaceMovesToTheClickedCell()
        {
            var (gs, c) = Hero();
            c.Inventory.Put(0, new Item(ItemDatabase.LeatherHelm));
            StringAssert.StartsWith("OK", gs.Handle(1, "IMOVE 0 20"));
            Assert.IsNull(c.Inventory.Get(0));
            Assert.AreEqual(ItemDatabase.LeatherHelm, c.Inventory.Get(20)!.Def);
            Assert.AreEqual(20, c.Inventory.AnchorAt(29), "2×2 ocupa as casas certas");
        }

        [Test]
        public void SameSizeTargetSwapsAndReportsWhereTheOldOneWent()
        {
            var (gs, c) = Hero();
            c.Inventory.Put(0, new Item(ItemDatabase.LeatherHelm));
            c.Inventory.Put(4, new Item(ItemDatabase.LeatherBoots));   // 2×2 também
            var r = gs.Handle(1, "IMOVE 0 13");                         // clica numa casa do meio das botas
            Assert.AreEqual("OK troca 0", r, "o item antigo foi para a origem (e vai para o cursor no cliente)");
            Assert.AreEqual(ItemDatabase.LeatherHelm, c.Inventory.Get(4)!.Def);
            Assert.AreEqual(ItemDatabase.LeatherBoots, c.Inventory.Get(0)!.Def);
        }

        [Test]
        public void DifferentSizeTargetIsRefusedAndNothingMoves()
        {
            var (gs, c) = Hero();
            c.Inventory.Put(0, new Item(ItemDatabase.LeatherHelm));
            c.Inventory.Put(4, new Item(ItemDatabase.ScaleArmor));     // 2×3
            StringAssert.StartsWith("ERR", gs.Handle(1, "IMOVE 0 4"));
            Assert.AreEqual(ItemDatabase.LeatherHelm, c.Inventory.Get(0)!.Def, "continua na origem (o cliente mantém na mão)");
            Assert.AreEqual(ItemDatabase.ScaleArmor, c.Inventory.Get(4)!.Def);
            // poções 1×1 também trocam entre si
            c.Inventory.Put(40, new Item(ItemDatabase.HealthPotion) { Quantity = 5 });
            c.Inventory.Put(41, new Item(ItemDatabase.ManaPotion) { Quantity = 3 });
            Assert.AreEqual("OK troca 40", gs.Handle(1, "IMOVE 40 41"));
            Assert.AreEqual(ItemDatabase.ManaPotion, c.Inventory.Get(40)!.Def);
        }

        [Test]
        public void DropOnGroundAndPickBack()
        {
            var (gs, c) = Hero();
            c.Inventory.Put(0, new Item(ItemDatabase.LeatherHelm, 3));
            StringAssert.StartsWith("OK", gs.Handle(1, "DROP 0"));
            Assert.IsNull(c.Inventory.Get(0));
            var view = new ClientState();
            gs.Tick(0.2f);
            foreach (var l in gs.DrainOutbox(1)!) view.Process(l);
            var drop = view.Entities.Values.Single(e => e.Kind == EntityKind.Drop);
            StringAssert.StartsWith("OK", gs.Handle(1, $"PICK {drop.Id}"));
            for (int i = 0; i < 40 && !c.Inventory.Items().Any(); i++) gs.Tick(0.05f);
            Assert.AreEqual(3, c.Inventory.Items().Single().item.Level, "voltou o mesmo item");
            StringAssert.StartsWith("ERR", gs.Handle(1, "DROP 30"), "slot vazio");
        }

        [Test]
        public void VaultSwapsToo()
        {
            var inv = new Inventory(Vault.Capacity, Vault.Columns);
            inv.Put(0, new Item(ItemDatabase.LeatherHelm));
            inv.Put(2, new Item(ItemDatabase.LeatherBoots));
            Assert.IsTrue(inv.MoveOrSwap(0, 3, out var back));
            Assert.AreEqual(0, back);
            Assert.AreEqual(ItemDatabase.LeatherHelm, inv.Get(2)!.Def);
            Assert.AreEqual(ItemDatabase.LeatherBoots, inv.Get(0)!.Def);
            Assert.IsTrue(inv.MoveOrSwap(0, 20, out var none));
            Assert.AreEqual(-1, none, "casa livre: só move");
        }
    
        [Test]
        public void EquippingDoesNotMoveStopOrTurnTheHero()
        {
            var (gs, c) = Hero();
            c.Inventory.Put(0, new Item(ItemDatabase.LeatherHelm));
            float x0 = c.X, z0 = c.Z;
            // parado: equipar e desequipar várias vezes não mexe na posição
            for (int k = 0; k < 3; k++)
            {
                StringAssert.StartsWith("OK", gs.Handle(1, "EQUIP 0"));
                gs.Tick(0.05f);
                Assert.AreEqual(x0, c.X, 0.0001f); Assert.AreEqual(z0, c.Z, 0.0001f);
                StringAssert.StartsWith("OK", gs.Handle(1, "UNEQUIP Helm"));
                gs.Tick(0.05f);
                Assert.AreEqual(x0, c.X, 0.0001f); Assert.AreEqual(z0, c.Z, 0.0001f);
                int slot = c.Inventory.Items().First(x => x.item.Def == ItemDatabase.LeatherHelm).slot;
                if (slot != 0) gs.Handle(1, $"IMOVE {slot} 0");
            }
            // andando: trocar item no meio do caminho não teleporta nem cancela o movimento
            gs.Handle(1, $"MOVE {Protocol.F(c.X + 6)} {Protocol.F(c.Z)}");
            for (int i = 0; i < 6; i++) gs.Tick(0.05f);
            float xm = c.X;
            Assert.Greater(xm, x0);
            StringAssert.StartsWith("OK", gs.Handle(1, "EQUIP 0"));
            Assert.AreEqual(xm, c.X, 0.0001f, "equipar não move o herói");
            gs.Tick(0.05f);
            Assert.Greater(c.X, xm, "segue andando para o mesmo destino");
        }
    }
}
