using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kezor.Core;
using Kezor.Core.Net;
using Kezor.Core.Server;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Login com lista de servidores: parser do servers.json, comando STATUS e ping de status por TCP.</summary>
    public class ServerListTests
    {
        [Test]
        public void ParsesServersAndChannelsAndSkipsBadEntries()
        {
            const string json = @"{
              ""servers"": [
                { ""id"": ""brasalume"", ""name"": ""Brasalume"", ""host"": ""jogo.exemplo.com"", ""region"": ""BR"",
                  ""channels"": [ { ""name"": ""Canal 1"", ""port"": 55901 }, { ""port"": 55902 }, { ""name"": ""ruim"", ""port"": 70000 } ] },
                { ""name"": ""Cinzas"", ""host"": ""10.0.0.5"", ""port"": 6000 },
                { ""name"": ""Sem porta"", ""host"": ""10.0.0.6"" },
                { ""name"": """", ""host"": ""x"" },
                { ""name"": ""Sem host"" },
                { ""name"": ""Espaço"", ""host"": ""a b"" },
                { ""id"": ""brasalume"", ""name"": ""Repetido"", ""host"": ""y"" },
                { ""name"": ""Porta ruim"", ""host"": ""z"", ""port"": 0 }
              ]
            }";
            var list = ServerList.Parse(json, out var err);
            Assert.AreEqual("", err);
            CollectionAssert.AreEqual(new[] { "Brasalume", "Cinzas", "Sem porta" }, list.Select(s => s.Name).ToArray());
            var b = list[0];
            Assert.AreEqual("jogo.exemplo.com", b.Host); Assert.AreEqual("BR", b.Region); Assert.AreEqual(ServerKind.Remote, b.Kind);
            Assert.AreEqual(2, b.Channels.Count, "canal com porta inválida fica de fora");
            Assert.AreEqual("Canal 2", b.Channels[1].Name, "canal sem nome ganha nome padrão");
            Assert.AreEqual(55902, b.Channels[1].Port);
            Assert.AreEqual(6000, list[1].Channels.Single().Port, "sem canais: a porta vira canal único");
            Assert.AreEqual(Protocol.DefaultPort, list[2].Channels.Single().Port, "sem porta: a padrão");
            Assert.AreEqual("cinzas", list[1].Id, "id gerado do nome");

            var all = ServerList.WithBuiltins(list);
            Assert.AreEqual(ServerKind.Local, all[0].Kind);
            Assert.AreEqual(ServerKind.Lan, all[1].Kind);
            Assert.AreEqual(5, all.Count);

            Assert.AreEqual(0, ServerList.Parse("{ isto não é json", out var bad).Count);
            StringAssert.Contains("inválido", bad);
            Assert.AreEqual(0, ServerList.Parse("[1,2]", out var notObj).Count);
            Assert.AreNotEqual("", notObj);
        }

        [Test]
        public void StatusWorksWithoutLoginAndCountsPlayers()
        {
            var gs = new GameServer(new SystemRandom(3), options: new ServerOptions { EventFirstDelay = 99999, AutosaveSeconds = 0, ServerName = "Cinzas; Norte", Channel = 2, MaxPlayers = 2 });
            var st = ServerStatus.ParseReply(gs.Handle(7, "STATUS"));
            Assert.IsNotNull(st);
            Assert.AreEqual("Cinzas, Norte", st!.Name, "separador do protocolo removido do nome");
            Assert.AreEqual(2, st.Channel); Assert.AreEqual(0, st.Players); Assert.AreEqual(2, st.MaxPlayers);
            Assert.AreEqual(Protocol.GameVersion, st.Version);
            gs.Handle(1, "CREATE Primo GUA");
            gs.Handle(2, "CREATE Dois ARC");
            st = ServerStatus.ParseReply(gs.Handle(7, "STATUS"))!;
            Assert.AreEqual(2, st.Players);
            Assert.AreEqual("lotado", st.Load);
            Assert.IsNull(ServerStatus.ParseReply("ERR qualquer"));
            Assert.IsNull(ServerStatus.ParseReply("OK status só;um"));
        }

        [Test]
        public void FullServerRefusesPlayButStatusStillAnswers()
        {
            var gs = new GameServer(new SystemRandom(4), options: new ServerOptions { EventFirstDelay = 99999, AutosaveSeconds = 0, RequireAccounts = true, PasswordIterations = 2000, MaxPlayers = 1 });
            gs.Handle(1, "REGISTER primeira segredo1"); gs.Handle(1, "CREATE Ana GUA"); StringAssert.StartsWith("OK", gs.Handle(1, "PLAY Ana"));
            gs.Handle(2, "REGISTER segunda segredo1"); gs.Handle(2, "CREATE Bia ARC");
            StringAssert.Contains("lotado", gs.Handle(2, "PLAY Bia"));
            Assert.AreEqual(1, ServerStatus.ParseReply(gs.Handle(3, "STATUS"))!.Players);
        }

        [Test]
        public void TcpStatusProbeMeasuresLatencyAndReportsOffline()
        {
            var gs = new GameServer(new SystemRandom(5), options: new ServerOptions { EventFirstDelay = 99999, AutosaveSeconds = 0, ServerName = "Teste" });
            using var host = new TcpServerHost(gs, 0);
            host.Start();
            var probe = Task.Run(() => ServerStatusProbe.Probe("127.0.0.1", host.Port, 3000));
            for (int i = 0; i < 300 && !probe.IsCompleted; i++) { host.Pump(); gs.Tick(0.02f); host.Flush(); Thread.Sleep(10); }
            var st = probe.Result;
            Assert.IsTrue(st.Online, st.Error);
            Assert.AreEqual("Teste", st.Name);
            Assert.GreaterOrEqual(st.LatencyMs, 0);
            int port = host.Port;
            host.Dispose();
            var off = ServerStatusProbe.Probe("127.0.0.1", port, 600);
            Assert.IsFalse(off.Online);
            Assert.AreEqual("offline", off.Load);
        }
    }
}
