using System.Collections.Generic;
using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Net;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Árvore da Ascensão: libera no 400, curva própria, 3 ramos por classe, pré-requisitos, respec, persistência.</summary>
    public class AscensionTests
    {
        private static GameServer Server() => new GameServer(new SystemRandom(9), options: new ServerOptions { EventFirstDelay = 99999, AutosaveSeconds = 0, AllowGm = true });

        [Test]
        public void EveryClassHasThreeBranchesOfFifteenNodes()
        {
            foreach (CharacterClass cls in System.Enum.GetValues(typeof(CharacterClass)))
            {
                var nodes = AscensionTree.For(cls);
                Assert.AreEqual(45, nodes.Count, $"{cls}: 3 ramos × 15 nós");
                for (int b = 0; b < 3; b++)
                {
                    var br = nodes.Where(n => n.Branch == b).ToList();
                    Assert.AreEqual(15, br.Count);
                    Assert.IsTrue(br.All(n => n.Tier == 0 ? n.Prereq == 0 : AscensionTree.Get(n.Prereq)!.Branch == b), "pré-requisito no mesmo ramo");
                    Assert.AreEqual(br.Count, br.Select(n => n.Name).Distinct().Count(), "nomes únicos no ramo");
                }
                Assert.AreEqual(1, nodes.Where(n => n.Branch == 2).Select(n => n.Classes).Distinct().Count(), "o terceiro ramo é exclusivo da classe");
                Assert.GreaterOrEqual(nodes.Sum(n => n.MaxRank), AscensionTree.MaxLevel, "dá para gastar todos os pontos");
            }
        }

        [Test]
        public void UnlocksAtLevel400AndExperienceTurnsIntoAscension()
        {
            var c = new Character(1, "Ascendido", CharacterClass.Warden);
            c.RestoreProgress(399, 0, 0, 0);
            Assert.IsFalse(c.Ascension.Unlocked);
            Assert.IsFalse(c.Ascension.Learn(c.Class, 1000, out var err));
            StringAssert.Contains("400", err);
            c.GainExperience(Character.ExpToNext(399));
            Assert.AreEqual(400, c.Level);
            Assert.IsTrue(c.Ascension.Unlocked, "chegar ao 400 libera a Ascensão");
            Assert.AreEqual(0, c.GainExperience(AscensionTree.ExpToNext(0) + AscensionTree.ExpToNext(1)), "no 400 não sobe mais de nível");
            Assert.AreEqual(2, c.Ascension.Level, "a experiência foi para a Ascensão");
            Assert.AreEqual(2, c.Ascension.Points, "1 ponto por nível");
            Assert.Greater(AscensionTree.ExpToNext(100), AscensionTree.ExpToNext(10), "curva própria crescente");
            // permanente: renascer não apaga
            c.Gold = Character.RebirthGoldCost(0);
            Assert.IsTrue(c.TryRebirth(out var why), why);
            Assert.AreEqual(1, c.Level);
            Assert.IsTrue(c.Ascension.Unlocked); Assert.AreEqual(2, c.Ascension.Level);
            // e só ganha Ascensão de novo quando voltar ao 400
            c.GainExperience(1000);
            Assert.AreEqual(2, c.Ascension.Level);
        }

        [Test]
        public void LearningNeedsPointsPrerequisitesAndTierPointsAndChangesStats()
        {
            var c = new Character(1, "Arvore", CharacterClass.Warden);
            c.RestoreProgress(400, 0, 0, 0);
            c.Ascension.Unlocked = true; c.Ascension.Level = 12;
            int hp0 = c.MaxHp; int def0 = c.GetCombatStats().DefenseBase;
            Assert.IsFalse(c.Ascension.Learn(c.Class, 1003, out var e1), "nó da linha 1 sem o de cima");
            for (int i = 0; i < 5; i++) Assert.IsTrue(c.Ascension.Learn(c.Class, 1000, out var e), e);   // Pele de Basalto 5/5
            Assert.IsFalse(c.Ascension.Learn(c.Class, 1000, out var eMax)); StringAssert.Contains("máximo", eMax);
            Assert.AreEqual(hp0 + hp0 * 5 / 100, c.MaxHp, "+5% de vida");
            Assert.IsTrue(c.Ascension.Learn(c.Class, 1003, out var e2), e2);                         // linha 1 liberada (5 pontos no ramo)
            Assert.IsFalse(c.Ascension.Learn(c.Class, 1006, out var e3), "linha 2 pede 10 pontos no ramo");
            StringAssert.Contains("10 pontos", e3);
            Assert.IsTrue(c.Ascension.Learn(c.Class, 1001, out _));                                   // Postura Firme
            Assert.AreEqual(def0 + 5, c.GetCombatStats().DefenseBase);
            Assert.IsFalse(c.Ascension.Learn(CharacterClass.Arcanist, 2000, out var e4), "ramo de outra classe");
            Assert.IsTrue(c.Ascension.Learn(c.Class, 2000, out var e5), e5);                          // ramo do Guardião
            Assert.AreEqual(12 - 8, c.Ascension.Points);
            for (int i = 0; i < 4; i++) c.Ascension.Learn(c.Class, 1100, out _);
            Assert.AreEqual(0, c.Ascension.Points);
            Assert.IsFalse(c.Ascension.Learn(c.Class, 1100, out var e6)); StringAssert.Contains("pontos", e6);
        }

        [Test]
        public void ServerProtocolRespecAndPersistence()
        {
            var gs = Server();
            gs.Handle(1, "CREATE Ascende GUA");
            var c = gs.GetCharacter(1)!;
            StringAssert.StartsWith("ERR", gs.Handle(1, "ASC LEARN 1000"), "trancada antes do 400");
            StringAssert.StartsWith("OK", gs.Handle(1, "GM LEVEL 400"));
            StringAssert.StartsWith("OK", gs.Handle(1, "GM ASC 6"));
            for (int i = 0; i < 5; i++) StringAssert.StartsWith("OK", gs.Handle(1, "ASC LEARN 1000"));
            StringAssert.StartsWith("OK", gs.Handle(1, "ASC LEARN 1100"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "ASC LEARN 1101"), "sem pontos");
            // o cliente recebe o @ASC
            var view = new ClientState();
            gs.Tick(0.2f);
            foreach (var l in gs.DrainOutbox(1)!) view.Process(l);
            Assert.IsTrue(view.AscUnlocked);
            Assert.AreEqual(6, view.AscLevel);
            Assert.AreEqual(0, view.AscPoints);
            Assert.AreEqual(5, view.AscRanks[1000]);
            // salva e carrega
            var back = SaveData.CharacterFromJson((IDictionary<string, object?>)MiniJson.Read(MiniJson.Write(SaveData.CharacterToJson(c, includeVault: false)))!, 2);
            Assert.IsTrue(back.Ascension.Unlocked);
            Assert.AreEqual(6, back.Ascension.Level);
            Assert.AreEqual(5, back.Ascension.RankOf(1000));
            Assert.AreEqual(1, back.Ascension.RankOf(1100));
            // redistribuir custa ouro e devolve os pontos
            c.Gold = 0;
            StringAssert.Contains("ouro insuficiente", gs.Handle(1, "ASC RESET"));
            c.Gold = AscensionTree.ResetCost(6);
            StringAssert.StartsWith("OK", gs.Handle(1, "ASC RESET"));
            Assert.AreEqual(0, c.Gold);
            Assert.AreEqual(6, c.Ascension.Points);
            Assert.AreEqual(0, c.Ascension.Spent);
        }
    }
}
