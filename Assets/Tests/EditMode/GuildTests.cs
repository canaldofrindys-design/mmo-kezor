using System.IO;
using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Net;
using Kezor.Core.Server;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Guildas: criar no NPC, convidar/aceitar, cargos, expulsar, chat, nome no snapshot e persistência.</summary>
    public class GuildTests
    {
        private static GameServer NewServer(JsonFileRepository? repo = null) =>
            new GameServer(new SystemRandom(4), repo, new ServerOptions { AllowGm = true, EventFirstDelay = 99999, AutosaveSeconds = 0, PasswordIterations = 2000 });

        private static Character Join(GameServer gs, int session, string name, int level = 60)
        {
            gs.Handle(session, $"CREATE {name} GUA");
            gs.Handle(session, $"GM LEVEL {level}");
            var c = gs.GetCharacter(session)!;
            var hadrik = gs.Map.Npcs.Single(n => n.Role == "guild");
            c.X = hadrik.X + 1.5f; c.Z = hadrik.Z;
            c.Gold = 2_000_000;
            return c;
        }

        [Test]
        public void CreateNeedsNpcLevelAndGold()
        {
            var gs = NewServer();
            var low = Join(gs, 1, "Novato", 10);
            StringAssert.Contains("nível", gs.Handle(1, "GUILD CREATE Brasas"));
            var lead = Join(gs, 2, "Chefe");
            lead.Gold = 100;
            StringAssert.Contains("ouro", gs.Handle(2, "GUILD CREATE Brasas"));
            lead.Gold = 2_000_000;
            lead.X = 40; lead.Z = 40;
            StringAssert.StartsWith("ERR", gs.Handle(2, "GUILD CREATE Brasas"));      // longe do Hadrik
            var hadrik = gs.Map.Npcs.Single(n => n.Role == "guild");
            lead.X = hadrik.X + 1; lead.Z = hadrik.Z;
            StringAssert.StartsWith("ERR", gs.Handle(2, "GUILD CREATE x"));           // nome curto
            StringAssert.StartsWith("OK", gs.Handle(2, "GUILD CREATE Brasas"));
            Assert.AreEqual(2_000_000 - Guild.CreateCost, lead.Gold);
            Assert.AreEqual(GuildRank.Leader, gs.GuildOf("Chefe")!.RankOf("Chefe"));
            StringAssert.StartsWith("ERR", gs.Handle(2, "GUILD CREATE Outra"));       // já tem guilda
        }

        [Test]
        public void InviteRanksKickChatAndSnapshotName()
        {
            var gs = NewServer();
            var lead = Join(gs, 1, "Chefe");
            var off = Join(gs, 2, "Braco");
            var mem = Join(gs, 3, "Recruta");
            gs.Handle(1, "GUILD CREATE Brasas");
            StringAssert.StartsWith("OK", gs.Handle(1, "GUILD INVITE Braco"));
            StringAssert.StartsWith("OK", gs.Handle(2, "GUILD ACCEPT"));
            StringAssert.StartsWith("ERR", gs.Handle(2, "GUILD INVITE Recruta"));    // membro comum não convida
            StringAssert.StartsWith("OK", gs.Handle(1, "GUILD PROMOTE Braco"));
            StringAssert.StartsWith("OK", gs.Handle(2, "GUILD INVITE Recruta"));     // oficial convida
            StringAssert.StartsWith("OK", gs.Handle(3, "GUILD ACCEPT"));
            var g = gs.GuildOf("Recruta")!;
            Assert.AreEqual(3, g.Members.Count);
            StringAssert.StartsWith("ERR", gs.Handle(2, "GUILD KICK Chefe"));        // oficial não expulsa o líder
            StringAssert.StartsWith("ERR", gs.Handle(3, "GUILD PROMOTE Recruta"));

            // Chat de guilda chega só aos membros; o nome aparece no snapshot de quem vê
            gs.Handle(4, "CREATE Estranho ARC");
            var stranger = gs.GetCharacter(4)!; stranger.X = mem.X; stranger.Z = mem.Z;
            foreach (var id in new[] { 1, 2, 3, 4 }) gs.DrainOutbox(id);
            StringAssert.StartsWith("OK", gs.Handle(3, "GSAY reunião na fonte"));
            Assert.IsTrue(gs.DrainOutbox(1)!.Any(l => l.Contains("CHAT;guild;Recruta;reunião na fonte")));
            Assert.IsFalse((gs.DrainOutbox(4) ?? new System.Collections.Generic.List<string>()).Any(l => l.Contains("reunião")));
            gs.Tick(0.2f);
            var view = new ClientState();
            foreach (var l in gs.DrainOutbox(4)!) view.Process(l);
            Assert.AreEqual("Brasas", view.Entities[mem.Id].Guild);
            Assert.AreEqual("", view.Entities[stranger.Id].Guild);

            // Membro vê a lista e o próprio cargo
            var mview = new ClientState();
            gs.Handle(3, "GUILD INFO");
            foreach (var l in gs.DrainOutbox(3)!) mview.Process(l);
            Assert.AreEqual("Brasas", mview.GuildName);
            Assert.AreEqual((int)GuildRank.Member, mview.GuildRank);
            Assert.AreEqual(3, mview.GuildMembers.Count);

            StringAssert.StartsWith("OK", gs.Handle(2, "GUILD KICK Recruta"));
            Assert.IsNull(gs.GuildOf("Recruta"));
            // Líder sai: a liderança passa para o oficial
            StringAssert.StartsWith("OK", gs.Handle(1, "GUILD LEAVE"));
            Assert.AreEqual("Braco", gs.Guilds["Brasas"].Leader);
            StringAssert.StartsWith("OK", gs.Handle(2, "GUILD DISBAND"));
            Assert.IsFalse(gs.Guilds.ContainsKey("Brasas"));
        }

        [Test]
        public void GuildsAreSavedAndLoaded()
        {
            var dir = Path.Combine(Path.GetTempPath(), "kezor-guild-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var gs = NewServer(new JsonFileRepository(dir));
                Join(gs, 1, "Fundador");
                Join(gs, 2, "Amigo");
                gs.Handle(1, "GUILD CREATE Lume");
                gs.Handle(1, "GUILD INVITE Amigo"); gs.Handle(2, "GUILD ACCEPT");
                gs.Handle(1, "GUILD PROMOTE Amigo");
                var gs2 = NewServer(new JsonFileRepository(dir));
                var g = gs2.Guilds["Lume"];
                Assert.AreEqual("Fundador", g.Leader);
                Assert.AreEqual(GuildRank.Officer, g.RankOf("Amigo"));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
