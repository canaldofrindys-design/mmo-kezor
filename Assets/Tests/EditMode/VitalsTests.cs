using System.Collections.Generic;
using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>HUD v4: Vigor (fôlego das skills), Escudo (absorve antes da vida) e atalhos de consumível Q/W/E/R.</summary>
    public class VitalsTests
    {
        private static GameServer Server() => new GameServer(new SystemRandom(5), options: new ServerOptions { EventFirstDelay = 99999, AutosaveSeconds = 0, AllowGm = true });

        [Test]
        public void SkillsSpendVigorAndBasicAttacksDoNot()
        {
            var gs = Server();
            gs.Handle(1, "CREATE Folego ARC");
            var c = gs.GetCharacter(1)!;
            c.RestoreProgress(60, 0, 0, 0);
            c.Mp = c.MaxMp;
            Assert.AreEqual(c.MaxVigor, (int)c.Vigor, "começa com o vigor cheio");
            Assert.AreEqual(100 + System.Math.Min(100, c.Stats.Agility / 10), c.MaxVigor);
            var slime = gs.Monsters.Values.First(m => m.Def == MonsterCatalog.MossSlime);
            c.X = slime.X + 1.5f; c.Z = slime.Z;
            var skill = SkillCatalog.All.First(s => s.Id != 0 && s.NeedsTarget && (s.Classes & ClassMask.Arcanist) != 0 && s.ReqLevel <= c.Level);
            float v0 = c.Vigor;
            StringAssert.StartsWith("OK", gs.Handle(1, $"SKILL {skill.Id} {slime.Id}"));
            gs.Tick(0.05f);
            Assert.AreEqual(v0 - Character.SkillVigorCost, c.Vigor, 1.0f, "a skill gastou vigor");
            Assert.IsTrue(c.InCombat);
            // sem vigor a skill não sai, mas o ataque básico sim
            c.Vigor = Character.SkillVigorCost - 1;
            StringAssert.Contains("vigor insuficiente", gs.Handle(1, $"SKILL {skill.Id} {slime.Id}"));
            float before = c.Vigor;
            slime.Hp = slime.MaxHp;
            StringAssert.StartsWith("OK", gs.Handle(1, $"ATTACK {slime.Id}"));
            for (int i = 0; i < 4; i++) gs.Tick(0.05f);
            Assert.GreaterOrEqual(c.Vigor, before, "ataque básico não gasta vigor");
        }

        [Test]
        public void VigorRegeneratesFasterOutOfCombat()
        {
            var c = new Character(1, "Pausa", CharacterClass.Ranger);
            c.Vigor = 0; c.EnterCombat();
            c.TickVitals(1f);
            Assert.AreEqual(Character.VigorRegenCombat, c.Vigor, 0.01f);
            c.TickVitals(Character.CombatSeconds);       // sai de combate
            c.Vigor = 0;
            c.TickVitals(1f);
            Assert.AreEqual(Character.VigorRegenRest, c.Vigor, 0.01f);
            c.TickVitals(60f);
            Assert.AreEqual(c.MaxVigor, c.Vigor, 0.01f, "não passa do máximo");
        }

        [Test]
        public void ShieldAbsorbsBeforeLifeAndRechargesAfterADelay()
        {
            var c = new Character(1, "Muralha", CharacterClass.Warden);
            c.RestoreProgress(50, 0, 0, 0);
            c.Hp = c.MaxHp;
            Assert.AreEqual(0f, c.Shield, "começa vazio");
            Assert.AreEqual(c.MaxHp * Character.ShieldPercentOfHp / 100, c.MaxShield);
            for (int i = 0; i < 20; i++) c.TickVitals(1f);
            Assert.AreEqual(c.MaxShield, c.Shield, 0.01f, "enche fora de perigo");

            // um golpe: o escudo segura primeiro e só o resto sai da vida
            var att = new CombatStats { Level = 50 };
            var def = new CombatStats();
            int raw = c.MaxShield + 30;
            int shown = CombatResolver.Apply(att, def, c, raw, out var killed);
            Assert.AreEqual(raw, shown, "o número mostrado é o dano inteiro");
            Assert.AreEqual(0f, c.Shield, 0.01f);
            Assert.AreEqual(c.MaxHp - 30, c.Hp, "só o que passou do escudo tirou vida");
            Assert.IsFalse(killed);

            // apanhar segura a recarga por ShieldDelaySeconds
            c.TickVitals(Character.ShieldDelaySeconds - 1f);
            Assert.AreEqual(0f, c.Shield, 0.01f, "ainda no atraso");
            c.TickVitals(1f); c.TickVitals(1f);
            Assert.AreEqual(c.MaxShield * Character.ShieldRegenPerSecond, c.Shield, 1f, "recarrega 10% por segundo");
            c.Hp = 0; c.TickVitals(1f);
            Assert.AreEqual(0f, c.Shield, "caído perde o escudo");
        }

        [Test]
        public void QuickSlotsAreValidatedUsedAndSaved()
        {
            var gs = Server();
            gs.Handle(1, "CREATE Atalho GUA");
            var c = gs.GetCharacter(1)!;
            CollectionAssert.AreEqual(new[] { 2000, 2002, 2001, 0 }, c.QuickSlots, "padrão: rubra, cerúlea, rubra maior");
            StringAssert.StartsWith("ERR", gs.Handle(1, $"QUICK 3 {ItemDatabase.IronBlade.Id}"), "arma não vai no atalho");
            StringAssert.StartsWith("ERR", gs.Handle(1, "QUICK 4 2000"));
            StringAssert.StartsWith("OK", gs.Handle(1, "QUICK 3 2000"));
            Assert.AreEqual(0, c.QuickSlots[0], "o mesmo item não fica em dois atalhos");
            Assert.AreEqual(2000, c.QuickSlots[3]);

            // USEQ usa da mochila, somando as pilhas
            int total = c.Inventory.Items().Where(x => x.item.Def.Id == 2000).Sum(x => x.item.Quantity);
            Assert.Greater(total, 0, "o herói novo traz poções rubras");
            c.Hp = 1;
            StringAssert.StartsWith("OK", gs.Handle(1, "USEQ 3"));
            Assert.Greater(c.Hp, 1);
            Assert.AreEqual(total - 1, c.Inventory.Items().Where(x => x.item.Def.Id == 2000).Sum(x => x.item.Quantity));
            StringAssert.StartsWith("ERR", gs.Handle(1, "USEQ 0"), "atalho vazio");

            // o @ME leva vigor, escudo e atalhos para o cliente
            var view = new ClientState();
            gs.Tick(0.2f);
            foreach (var l in gs.DrainOutbox(1)!) view.Process(l);
            Assert.AreEqual(c.MaxVigor, view.Me.MaxVigor);
            Assert.AreEqual(c.MaxShield, view.Me.MaxShield);
            CollectionAssert.AreEqual(c.QuickSlots, view.Me.Quick);

            // e os atalhos ficam salvos com o personagem
            var back = SaveData.CharacterFromJson((IDictionary<string, object?>)MiniJson.Read(MiniJson.Write(SaveData.CharacterToJson(c, includeVault: false)))!, 2);
            CollectionAssert.AreEqual(c.QuickSlots, back.QuickSlots);
        }
    }
}
