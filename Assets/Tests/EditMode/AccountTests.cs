using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Contas (PBKDF2), seleção de até 3 personagens, baú da conta e persistência em JSON.</summary>
    public class AccountTests
    {
        private string _dir = "";

        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "kezor-test-" + System.Guid.NewGuid().ToString("N")); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static ServerOptions Opts(bool require = true) => new ServerOptions { RequireAccounts = require, EventFirstDelay = 99999, PasswordIterations = 2000, AutosaveSeconds = 0 };

        [Test]
        public void PasswordHashIsSaltedAndVerifies()
        {
            var a = PasswordHasher.Hash("dragão-123", 2000);
            var b = PasswordHasher.Hash("dragão-123", 2000);
            Assert.AreNotEqual(a, b, "sal aleatório");
            StringAssert.StartsWith("pbkdf2-sha256$2000$", a);
            Assert.IsTrue(PasswordHasher.Verify("dragão-123", a));
            Assert.IsFalse(PasswordHasher.Verify("dragao-123", a));
            Assert.IsFalse(PasswordHasher.Verify("dragão-123", a.Substring(0, a.Length - 4) + "AAAA"));
            Assert.IsFalse(PasswordHasher.Verify("x", "lixo"));
            StringAssert.DoesNotContain("dragão", a);
        }

        [Test]
        public void AccountFlowWithThreeCharactersAndSharedVault()
        {
            var gs = new GameServer(new SystemRandom(1), options: Opts());
            StringAssert.StartsWith("ERR", gs.Handle(1, "CREATE Solto GUA"));          // sem conta
            StringAssert.StartsWith("ERR", gs.Handle(1, "LOGIN Solto"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "REGISTER ab 123456"));        // nome curto
            StringAssert.StartsWith("ERR", gs.Handle(1, "REGISTER Aurelio 123"));      // senha curta
            StringAssert.StartsWith("OK", gs.Handle(1, "REGISTER Aurelio senha123"));
            StringAssert.StartsWith("OK", gs.Handle(1, "CREATE Primo GUA"));
            StringAssert.StartsWith("OK", gs.Handle(1, "CREATE Secundo ARC"));
            StringAssert.StartsWith("OK", gs.Handle(1, "CREATE Tercio PAT"));
            Assert.IsNull(gs.GetCharacter(1), "com conta, criar não entra no mundo");
            var list = gs.Handle(1, "CHARS");
            StringAssert.Contains("Primo:0:1", list);
            StringAssert.Contains("Tercio:2:1", list);

            StringAssert.StartsWith("ERR", gs.Handle(2, "AUTH Aurelio errada"));
            StringAssert.StartsWith("ERR", gs.Handle(2, "AUTH Aurelio senha123"));   // a conta já está conectada
            StringAssert.StartsWith("ERR", gs.Handle(1, "PLAY Alheio"));
            StringAssert.StartsWith("OK", gs.Handle(1, "PLAY Primo"));
            var primo = gs.GetCharacter(1)!;
            Assert.AreEqual("Aurelio", primo.AccountName);

            // Guarda no baú com o Primo...
            var ilsa = gs.Map.Npcs.Single(n => n.Role == "vault");
            primo.X = ilsa.X - 1.5f; primo.Z = ilsa.Z;
            primo.Inventory.TryAdd(new Item(ItemDatabase.StormBlade, 7), out var slot);
            StringAssert.StartsWith("OK", gs.Handle(1, $"VPUT {slot}"));
            StringAssert.StartsWith("OK", gs.Handle(1, "VGOLDIN 1000"));
            gs.Disconnect(1);

            // ...e retira com o Tércio, da mesma conta
            StringAssert.StartsWith("OK", gs.Handle(3, "AUTH aurelio senha123"));
            StringAssert.StartsWith("OK", gs.Handle(3, "PLAY Tercio"));
            var tercio = gs.GetCharacter(3)!;
            Assert.AreEqual(ItemDatabase.StormBlade, tercio.Vault.Items.Get(0)!.Def);
            Assert.AreEqual(1000, tercio.Vault.Gold);
            Assert.AreSame(tercio.Vault, primo.Vault);
        }

        [Test]
        public void JsonRoundTripKeepsEverything()
        {
            var c = new Character(5, "Salvo", CharacterClass.Ranger) { Gold = 123_456, Infamy = 2, MapId = MapDefinition.SwampId, X = 24.5f, Z = 60.25f };   // chão livre do pântano
            c.RestoreProgress(87, 4_000_000, 1, 15);
            c.Stats = new Attributes(200, 300, 120, 40);
            var bow = new Item(ItemDatabase.EmberWhisperBow, 11) { Luck = true, Skill = true, Ascended = AscendedOption.SurgeChance };
            c.Equipment[EquipSlot.RightHand] = bow;
            c.Equipment[EquipSlot.Wings] = new Item(ItemDatabase.AshWings, 3);
            c.Inventory.Put(7, new Item(ItemDatabase.HealthPotion) { Quantity = 33 });
            c.Vault.Items.Put(0, new Item(ItemDatabase.RuneOfDawn));
            c.Vault.AddGold(999, out _);
            var json = MiniJson.Write(SaveData.CharacterToJson(c, includeVault: true));
            var back = SaveData.CharacterFromJson((IDictionary<string, object?>)MiniJson.Read(json)!, 9);
            Assert.AreEqual("Salvo", back.Name);
            Assert.AreEqual(CharacterClass.Ranger, back.Class);
            Assert.AreEqual(87, back.Level);
            Assert.AreEqual(4_000_000, back.Experience);
            Assert.AreEqual(1, back.Rebirths);
            Assert.AreEqual(15, back.FreePoints);
            Assert.AreEqual(300, back.Stats.Agility);
            Assert.AreEqual(123_456, back.Gold);
            Assert.AreEqual(MapDefinition.SwampId, back.MapId);
            Assert.AreEqual(60.25f, back.Z, 0.001f);
            var b2 = back.Equipment[EquipSlot.RightHand];
            Assert.AreEqual(11, b2.Level); Assert.IsTrue(b2.Luck && b2.Skill); Assert.AreEqual(AscendedOption.SurgeChance, b2.Ascended);
            Assert.AreEqual(3, back.Equipment[EquipSlot.Wings].Level);
            Assert.AreEqual(33, back.Inventory.Get(7)!.Quantity);
            Assert.AreEqual(999, back.Vault.Gold);
            Assert.AreEqual(ItemDatabase.RuneOfDawn, back.Vault.Items.Get(0)!.Def);

            // Texto com aspas e acentos sobrevive
            var o = (IDictionary<string, object?>)MiniJson.Read(MiniJson.Write(new Dictionary<string, object?> { ["t"] = "Ilsa diz: \"olá\"\nnova linha" }))!;
            Assert.AreEqual("Ilsa diz: \"olá\"\nnova linha", o.S("t"));
        }

        [Test]
        public void ProgressSurvivesServerRestart()
        {
            var repo = new JsonFileRepository(_dir);
            var gs = new GameServer(new SystemRandom(1), repo, Opts());
            gs.Handle(1, "REGISTER Persistente segredo1");
            gs.Handle(1, "CREATE Duravel GUA");
            gs.Handle(1, "PLAY Duravel");
            var c = gs.GetCharacter(1)!;
            c.Gold = 777_777;
            c.Inventory.TryAdd(new Item(ItemDatabase.WardenShield, 6), out _);
            c.X = MapDefinition.ValeCenter - 1; c.Z = MapDefinition.ValeCenter - 8;
            gs.Disconnect(1);
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "characters", "duravel.json")));
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "accounts", "persistente.json")));
            StringAssert.DoesNotContain("segredo1", File.ReadAllText(Path.Combine(_dir, "accounts", "persistente.json")));

            // Servidor novo, mesma pasta
            var gs2 = new GameServer(new SystemRandom(2), new JsonFileRepository(_dir), Opts());
            StringAssert.StartsWith("ERR", gs2.Handle(1, "AUTH Persistente outra"));
            StringAssert.Contains("Duravel:0:1", gs2.Handle(1, "AUTH Persistente segredo1"));
            StringAssert.StartsWith("OK", gs2.Handle(1, "PLAY Duravel"));
            var c2 = gs2.GetCharacter(1)!;
            Assert.AreEqual(777_777, c2.Gold);
            Assert.IsTrue(Enumerable.Range(0, Character.InventorySize).Any(i => c2.Inventory.Get(i)?.Def == ItemDatabase.WardenShield && c2.Inventory.Get(i)!.Level == 6));
            Assert.AreEqual(MapDefinition.ValeCenter - 1, c2.X, 0.01f); Assert.AreEqual(MapDefinition.ValeCenter - 8, c2.Z, 0.01f);   // volta onde parou
            StringAssert.StartsWith("ERR", gs2.Handle(1, "REGISTER Persistente x123456"));
        }

        [Test]
        public void AutosaveWritesWhileOnline()
        {
            var opts = Opts(); opts.AutosaveSeconds = 1f;
            var gs = new GameServer(new SystemRandom(1), new JsonFileRepository(_dir), opts);
            gs.Handle(1, "REGISTER Vigia senha123");
            gs.Handle(1, "CREATE Atento PAT");
            gs.Handle(1, "PLAY Atento");
            gs.GetCharacter(1)!.Gold = 4242;
            for (int i = 0; i < 25; i++) gs.Tick(0.05f);
            var saved = (IDictionary<string, object?>)MiniJson.Read(File.ReadAllText(Path.Combine(_dir, "characters", "atento.json")))!;
            Assert.AreEqual(4242, saved.L("gold"));
        }

        [Test]
        public void LocalModeStillPlaysAsGuest()
        {
            var gs = new GameServer(new SystemRandom(1), options: Opts(require: false));
            StringAssert.StartsWith("OK", gs.Handle(1, "CREATE Visitante ARC"));
            Assert.IsNotNull(gs.GetCharacter(1));
            Assert.AreEqual("", gs.GetCharacter(1)!.AccountName);
        }
    }
}
