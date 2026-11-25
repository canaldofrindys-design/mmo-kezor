using System.IO;
using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Net;
using Kezor.Core.Server;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Ranking (geral, por classe, guildas) e guerra de guildas (declarar, aceitar, placar, resultado).</summary>
    public class RankingWarTests
    {
        private static GameServer NewServer(JsonFileRepository? repo = null) =>
            new GameServer(new SystemRandom(6), repo, new ServerOptions { AllowGm = true, EventFirstDelay = 99999, AutosaveSeconds = 0, PasswordIterations = 2000 });

        private static Character Make(GameServer gs, int session, string name, string cls, int level)
        {
            gs.Handle(session, $"CREATE {name} {cls}");
            gs.Handle(session, $"GM LEVEL {level}");
            return gs.GetCharacter(session)!;
        }

        [Test]
        public void RankingOrdersByRebirthThenLevelAndByClass()
        {
            var gs = NewServer();
            Make(gs, 1, "Alto", "GUA", 120);
            Make(gs, 2, "Medio", "ARC", 80);
            var reborn = Make(gs, 3, "Renato", "PAT", 30);
            reborn.RestoreProgress(30, 0, 1, 0);                           // 1 renascimento vale mais que nível 120
            gs.Handle(4, "CREATE Baixo GUA");
            var reply = gs.Handle(2, "RANKING");
            StringAssert.Contains("3º", reply);
            var view = new ClientState();
            foreach (var l in gs.DrainOutbox(2)!) view.Process(l);
            Assert.AreEqual("GERAL", view.RankKind);
            Assert.AreEqual(new[] { "Renato", "Alto", "Medio", "Baixo" }, view.RankRows.Select(r => r[1]).ToArray());

            gs.Handle(1, "RANKING GUA");
            var v2 = new ClientState();
            foreach (var l in gs.DrainOutbox(1)!) v2.Process(l);
            Assert.AreEqual(new[] { "Alto", "Baixo" }, v2.RankRows.Select(r => r[1]).ToArray());
            StringAssert.StartsWith("ERR", gs.Handle(1, "RANKING XYZ"));
        }

        [Test]
        public void RankingIsSavedAndRebuilt()
        {
            var dir = Path.Combine(Path.GetTempPath(), "kezor-rank-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var gs = NewServer(new JsonFileRepository(dir));
                Make(gs, 1, "Lenda", "GUA", 200);
                Make(gs, 2, "Aprendiz", "ARC", 10);
                gs.SaveAll();
                Assert.IsTrue(File.Exists(Path.Combine(dir, "ranking.json")));
                var gs2 = NewServer(new JsonFileRepository(dir));
                var top = gs2.Ranking.Top();
                Assert.AreEqual("Lenda", top[0].Name);
                Assert.AreEqual(200, top[0].Level);
                // sem ranking.json o servidor reconstrói a partir dos personagens salvos
                File.Delete(Path.Combine(dir, "ranking.json"));
                var gs3 = NewServer(new JsonFileRepository(dir));
                Assert.AreEqual(2, gs3.Ranking.Top().Count);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        private static (GameServer gs, Character a1, Character a2, Character b1) TwoGuilds()
        {
            var gs = NewServer();
            var a1 = Make(gs, 1, "Aldo", "GUA", 120); var a2 = Make(gs, 2, "Aria", "ARC", 120); var b1 = Make(gs, 3, "Bruno", "PAT", 120);
            var hadrik = gs.Map.Npcs.Single(n => n.Role == "guild");
            foreach (var c in new[] { a1, b1 }) { c.X = hadrik.X + 1; c.Z = hadrik.Z; c.Gold = 1_000_000; }
            gs.Handle(1, "GUILD CREATE Alvorada");
            gs.Handle(1, "GUILD INVITE Aria"); gs.Handle(2, "GUILD ACCEPT");
            gs.Handle(3, "GUILD CREATE Breu");
            return (gs, a1, a2, b1);
        }

        [Test]
        public void WarCountsKillsWithoutInfamyAndRecordsTheWinner()
        {
            var (gs, a1, a2, b1) = TwoGuilds();
            gs.WarDuration = 30f;
            StringAssert.StartsWith("ERR", gs.Handle(2, "GUILD WAR Breu"));          // só o líder declara
            StringAssert.StartsWith("OK", gs.Handle(1, "GUILD WAR Breu"));
            StringAssert.StartsWith("OK", gs.Handle(3, "GUILD WARACCEPT"));
            Assert.IsNotNull(gs.WarOf("Alvorada"));

            // Abates fora da vila entre as duas guildas viram pontos e não dão infâmia
            foreach (var c in new[] { a1, a2, b1 }) { c.X = 60; c.Z = 60; }
            for (int i = 0; i < 2; i++)
            {
                b1.Hp = 1;
                gs.Handle(1, $"ATTACK {b1.Id}");
                for (int k = 0; k < 40 && b1.Hp > 0 && b1.MapId == 1 && b1.X == 60; k++) gs.Tick(0.05f);
                b1.X = 60; b1.Z = 60; b1.Hp = b1.MaxHp;
            }
            var w = gs.WarOf("Alvorada")!;
            Assert.AreEqual(2, w.ScoreA);
            Assert.AreEqual(0, a1.Infamy, "abate de guerra não dá infâmia");

            // O membro vê o placar
            var view = new ClientState();
            foreach (var l in gs.DrainOutbox(2)!) view.Process(l);
            Assert.IsTrue(view.AtWar);
            Assert.AreEqual(2, view.WarScoreA);

            for (int i = 0; i < 700; i++) gs.Tick(0.05f);                               // acaba o tempo
            Assert.IsNull(gs.WarOf("Alvorada"));
            Assert.AreEqual(1, gs.Guilds["Alvorada"].Wins);
            Assert.AreEqual(1, gs.Guilds["Breu"].Losses);
            // A vitória pesa no ranking de guildas
            gs.Handle(1, "RANKING GUILDAS");
            var rv = new ClientState();
            foreach (var l in gs.DrainOutbox(1)!) rv.Process(l);
            Assert.AreEqual("Alvorada", rv.RankRows[0][1]);
            Assert.AreEqual("1", rv.RankRows[0][4]);
        }

        [Test]
        public void WarNeedsAcceptanceAndOnlyOneAtATime()
        {
            var (gs, a1, a2, b1) = TwoGuilds();
            StringAssert.StartsWith("ERR", gs.Handle(3, "GUILD WARACCEPT"));          // ninguém declarou
            gs.Handle(1, "GUILD WAR Breu");
            gs.Handle(3, "GUILD WARDECLINE");
            Assert.IsNull(gs.WarOf("Breu"));
            gs.Handle(1, "GUILD WAR Breu"); gs.Handle(3, "GUILD WARACCEPT");
            StringAssert.StartsWith("ERR", gs.Handle(1, "GUILD WAR Breu"));          // já em guerra
            // Matar alguém sem guerra continua dando infâmia
            gs.Handle(4, "CREATE Solitario GUA"); gs.Handle(4, "GM LEVEL 50");
            var lone = gs.GetCharacter(4)!;
            a1.X = lone.X = 60; a1.Z = lone.Z = 60; lone.Hp = 1;
            gs.Handle(1, $"ATTACK {lone.Id}");
            for (int k = 0; k < 40 && lone.X == 60; k++) gs.Tick(0.05f);
            Assert.AreEqual(1, a1.Infamy);
        }
    }
}
