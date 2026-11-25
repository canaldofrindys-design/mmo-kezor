using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.Server;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Tela de seleção: até 5 personagens por conta, lista com dados da placa e aparência, apagar com confirmação.</summary>
    public class CharacterSelectTests
    {
        private static GameServer Server() => new GameServer(new SystemRandom(1), options: new ServerOptions
        { RequireAccounts = true, EventFirstDelay = 99999, PasswordIterations = 2000, AutosaveSeconds = 0, AllowGm = true });

        [Test]
        public void FiveCharactersPerAccount()
        {
            var gs = Server();
            StringAssert.StartsWith("OK", gs.Handle(1, "REGISTER selecao segredo1"));
            string[] codes = { "GUA", "ARC", "PAT", "LAM", "COM" };
            for (int i = 0; i < 5; i++) StringAssert.StartsWith("OK", gs.Handle(1, $"CREATE Heroi{i} {codes[i]}"), $"herói {i + 1}");
            StringAssert.Contains("limite de 5", gs.Handle(1, "CREATE Sexto PUN"));
        }

        [Test]
        public void CharListCarriesPlateDataAndAppearance()
        {
            var gs = Server();
            gs.Handle(1, "REGISTER placas segredo1");
            gs.Handle(1, "CREATE Aldo GUA");
            gs.Handle(1, "CREATE Bina INV");
            var r = gs.Handle(1, "CHARS");
            StringAssert.StartsWith("OK chars ", r);
            var entries = r.Substring("OK chars ".Length).Split('|');
            Assert.AreEqual(2, entries.Length);
            var f = entries[0].Split(':');
            Assert.AreEqual("Aldo", f[0]);
            Assert.AreEqual(((int)CharacterClass.Warden).ToString(), f[1]);
            Assert.AreEqual("1", f[2]);           // nível
            Assert.AreEqual("0", f[3]);           // renascimentos
            Assert.AreEqual("", f[4]);            // sem guilda
            Assert.AreEqual("-1", f[5]);          // sem cargo
            var look = Appearance.Parse(f[6]);
            Assert.AreEqual(ItemDatabase.IronBlade.Id, look[EquipSlot.RightHand].Id, "a aparência traz a arma inicial para montar o modelo");
            Assert.AreEqual(ItemDatabase.LeatherArmor.Id, look[EquipSlot.Armor].Id);
        }

        [Test]
        public void DeleteNeedsTheExactNameAndFreesTheSlot()
        {
            var gs = Server();
            gs.Handle(1, "REGISTER apagar segredo1");
            gs.Handle(1, "CREATE Ciro PAT");
            StringAssert.Contains("confirmação", gs.Handle(1, "DELCHAR Ciro ciro"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "DELCHAR Ciro"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "DELCHAR Outro Outro"));
            StringAssert.StartsWith("OK", gs.Handle(1, "DELCHAR Ciro Ciro"));
            Assert.AreEqual("OK chars ", gs.Handle(1, "CHARS"));
            StringAssert.StartsWith("OK", gs.Handle(1, "CREATE Ciro ARC"), "o nome fica livre de novo");
            // dentro do mundo não apaga
            gs.Handle(1, "PLAY Ciro");
            StringAssert.StartsWith("ERR", gs.Handle(1, "DELCHAR Ciro Ciro"));
        }
    }
}
