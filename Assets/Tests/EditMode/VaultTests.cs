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
    /// <summary>Baú da cidade: guardar/retirar itens e ouro, só perto da guardiã, sem duplicar.</summary>
    public class VaultTests
    {
        private static (GameServer gs, Character c) AtVault()
        {
            var gs = new GameServer(new SystemRandom(42), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999 });
            gs.Handle(1, "CREATE Guarda GUA");
            var c = gs.GetCharacter(1)!;
            var ilsa = gs.Map.Npcs.Single(n => n.Role == "vault");
            c.X = ilsa.X - 1.5f; c.Z = ilsa.Z;
            return (gs, c);
        }

        private static int SlotOf(Character c, ItemDefinition d) => Enumerable.Range(0, Character.InventorySize).First(i => c.Inventory.Get(i)?.Def == d);

        [Test]
        public void StoreAndTakeItemsAndStacks()
        {
            var (gs, c) = AtVault();
            StringAssert.StartsWith("OK", gs.Handle(1, "VAULT"));
            int pot = SlotOf(c, ItemDatabase.HealthPotion);           // 15 poções
            StringAssert.StartsWith("OK", gs.Handle(1, $"VPUT {pot} 10"));
            Assert.AreEqual(5, c.Inventory.Get(pot)!.Quantity);
            Assert.AreEqual(10, c.Vault.Items.Get(0)!.Quantity);
            StringAssert.StartsWith("OK", gs.Handle(1, $"VPUT {pot}"));  // o resto empilha no mesmo espaço
            Assert.IsNull(c.Inventory.Get(pot));
            Assert.AreEqual(15, c.Vault.Items.Get(0)!.Quantity);
            var armor = c.Inventory.TryAdd(new Item(ItemDatabase.ScaleArmor, 5), out var aslot);
            StringAssert.StartsWith("OK", gs.Handle(1, $"VPUT {aslot}"));
            Assert.AreEqual(5, c.Vault.Items.Get(1)!.Level);

            StringAssert.StartsWith("OK", gs.Handle(1, "VTAKE 0 4"));
            Assert.AreEqual(11, c.Vault.Items.Get(0)!.Quantity);
            Assert.AreEqual(4, c.Inventory.Get(SlotOf(c, ItemDatabase.HealthPotion))!.Quantity);
            StringAssert.StartsWith("OK", gs.Handle(1, "VTAKE 1"));
            Assert.IsNull(c.Vault.Items.Get(1));
            StringAssert.StartsWith("ERR", gs.Handle(1, "VTAKE 0 99"));

            // O cliente recebe o @VAULT com o conteúdo
            var view = new ClientState();
            gs.Tick(0.2f);
            foreach (var l in gs.DrainOutbox(1)!) view.Process(l);
            Assert.IsTrue(view.VaultOpen);
            Assert.AreEqual(11, view.Vault[0]!.Quantity);
        }

        [Test]
        public void RepeatedRequestsNeverDuplicate()
        {
            var (gs, c) = AtVault();
            c.Inventory.TryAdd(new Item(ItemDatabase.StormBlade, 9) { Luck = true }, out var slot);
            StringAssert.StartsWith("OK", gs.Handle(1, $"VPUT {slot}"));
            StringAssert.StartsWith("ERR", gs.Handle(1, $"VPUT {slot}"));   // o slot já está vazio
            StringAssert.StartsWith("OK", gs.Handle(1, "VTAKE 0"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "VTAKE 0"));
            int blades = Enumerable.Range(0, Character.InventorySize).Count(i => c.Inventory.Get(i)?.Def == ItemDatabase.StormBlade)
                       + Enumerable.Range(0, Vault.Capacity).Count(i => c.Vault.Items.Get(i)?.Def == ItemDatabase.StormBlade);
            Assert.AreEqual(1, blades);
            StringAssert.StartsWith("ERR", gs.Handle(1, $"VPUT {SlotOf(c, ItemDatabase.HealthPotion)} 0"));
            StringAssert.StartsWith("ERR", gs.Handle(1, $"VPUT {SlotOf(c, ItemDatabase.HealthPotion)} -3"));
        }

        [Test]
        public void GoldMovesBothWaysWithLimits()
        {
            var (gs, c) = AtVault();
            long start = c.Gold;
            StringAssert.StartsWith("OK", gs.Handle(1, "VGOLDIN 5000"));
            Assert.AreEqual(start - 5000, c.Gold);
            Assert.AreEqual(5000, c.Vault.Gold);
            StringAssert.StartsWith("ERR", gs.Handle(1, $"VGOLDIN {c.Gold + 1}"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "VGOLDOUT 6000"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "VGOLDIN abc"));
            StringAssert.StartsWith("OK", gs.Handle(1, "VGOLDOUT 2000"));
            Assert.AreEqual(3000, c.Vault.Gold);
            Assert.AreEqual(start - 3000, c.Gold);
            c.Gold = Vault.MaxGold;
            StringAssert.StartsWith("ERR", gs.Handle(1, $"VGOLDIN {Vault.MaxGold}"));   // passaria do limite
        }

        [Test]
        public void FullVaultAndOutsideTheCityAreRejected()
        {
            var (gs, c) = AtVault();
            while (c.Vault.Items.TryAdd(new Item(ItemDatabase.LeatherHelm), out _)) { }
            c.Inventory.TryAdd(new Item(ItemDatabase.LeatherBoots), out var slot);
            StringAssert.Contains("cheio", gs.Handle(1, $"VPUT {slot}"));
            Assert.IsNotNull(c.Inventory.Get(slot));

            c.X = gs.Map.SafeX - 6; c.Z = gs.Map.SafeZ;                // na vila, mas longe da Ilsa
            StringAssert.StartsWith("ERR", gs.Handle(1, "VAULT"));
            c.X = 40; c.Z = 40;                                          // fora da vila
            StringAssert.Contains("cidade", gs.Handle(1, "VTAKE 0"));

            // Item vinculado nunca entra no baú
            var bound = new ItemDefinition { Id = 9999, Name = "Selo do Juramento", Kind = ItemKind.Material, Bound = true };
            var (gs2, c2) = AtVault();
            c2.Inventory.TryAdd(new Item(bound), out var bslot);
            StringAssert.Contains("vinculado", gs2.Handle(1, $"VPUT {bslot}"));
        }

        [Test]
        public void VaultClosesWhenWalkingAway()
        {
            var (gs, c) = AtVault();
            gs.Handle(1, "VAULT");
            c.X = gs.Map.SafeX - 6;
            gs.Tick(0.2f);
            var view = new ClientState();
            view.Process("@VAULT 10");
            foreach (var l in gs.DrainOutbox(1)!) view.Process(l);
            Assert.IsFalse(view.VaultOpen);
        }
    }
}
