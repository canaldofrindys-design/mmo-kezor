using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kezor.Core;
using Kezor.Core.Net;
using Kezor.Core.Server;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>
    /// Multiplayer de verdade, pela rede (TCP em 127.0.0.1): vários jogadores com conta entram no mesmo servidor, se veem,
    /// conversam, formam grupo, sussurram, trocam de personagem e caem por inatividade. É o mesmo caminho que o jogo usa.
    /// </summary>
    public class MultiplayerTests
    {
        private sealed class Player : IDisposable
        {
            public readonly TcpClientConnection Conn;
            public readonly ClientState State = new ClientState();
            public readonly List<string> Replies = new List<string>();
            public readonly List<string> All = new List<string>();
            public Player(int port) { Conn = new TcpClientConnection("127.0.0.1", port); }
            public void Drain()
            {
                while (Conn.TryReceive(out var l))
                {
                    All.Add(l);
                    State.Process(l);
                    if (l.Length > 0 && l[0] != '@') Replies.Add(l);
                }
            }
            public void Dispose() => Conn.Dispose();
        }

        private sealed class World : IDisposable
        {
            public readonly GameServer Game;
            public readonly TcpServerHost Host;
            public readonly List<Player> Players = new List<Player>();
            public World(ServerOptions? opt = null)
            {
                var o = opt ?? new ServerOptions();
                o.RequireAccounts = true; o.EventFirstDelay = 99999; o.PasswordIterations = 1000; o.AutosaveSeconds = 0; o.ViewRange = 200f;
                Game = new GameServer(new SystemRandom(3), options: o);
                Host = new TcpServerHost(Game, 0);
                Host.Start();
            }
            public Player Join()
            {
                var p = new Player(Host.Port);
                Players.Add(p);
                Step(() => p.Conn.Connected);
                Assert.IsTrue(p.Conn.Connected, p.Conn.Error);
                return p;
            }
            /// <summary>Roda o servidor (como o loop do jogo) até a condição valer ou o tempo acabar.</summary>
            public bool Step(Func<bool> until, int maxMs = 3000)
            {
                var end = DateTime.UtcNow.AddMilliseconds(maxMs);
                while (DateTime.UtcNow < end)
                {
                    Host.Pump(); Game.Tick(0.05f); Host.Flush();
                    Thread.Sleep(5);
                    foreach (var p in Players) p.Drain();
                    if (until()) return true;
                }
                return false;
            }
            /// <summary>Manda um comando e espera a resposta (OK/ERR/FAIL).</summary>
            public string Ask(Player p, string cmd)
            {
                int before = p.Replies.Count;
                p.Conn.Send(cmd);
                Assert.IsTrue(Step(() => p.Replies.Count > before), $"sem resposta para {cmd}");
                return p.Replies[before];
            }
            /// <summary>Conta nova + herói + entra no mundo.</summary>
            public Player Enter(string account, string hero, string cls = "GUA")
            {
                var p = Join();
                StringAssert.StartsWith("OK", Ask(p, $"REGISTER {account} segredo123"));
                StringAssert.StartsWith("OK", Ask(p, $"CREATE {hero} {cls}"));
                StringAssert.StartsWith("OK", Ask(p, $"PLAY {hero}"));
                Assert.IsTrue(Step(() => p.State.InWorld), "não entrou no mundo");
                return p;
            }
            public void Dispose() { foreach (var p in Players) p.Dispose(); Host.Dispose(); }
        }

        private static bool Sees(Player p, string name) => p.State.Entities.Values.Any(e => e.Kind == EntityKind.Player && e.Name == name);

        [Test]
        public void TwoPlayersSeeEachOtherAndTalk()
        {
            using var w = new World();
            var a = w.Enter("contaana", "Ana");
            var b = w.Enter("contabeto", "Beto", "ARC");
            Assert.IsTrue(w.Step(() => Sees(a, "Beto") && Sees(b, "Ana")), "os dois devem se ver no mesmo mapa");
            Assert.AreNotEqual(a.State.MyId, b.State.MyId);
            Assert.IsTrue(a.All.Any(l => l.Contains("Beto entrou no mundo")), "aviso de entrada para quem já estava");

            StringAssert.StartsWith("OK", w.Ask(a, "SAY oi pessoal"));
            Assert.IsTrue(w.Step(() => b.State.Log.Any(l => l.Channel == "say" && l.Text.Contains("Ana: oi pessoal"))), "chat local chega no outro");

            // o outro anda e a posição chega pela rede
            var me = b.State.Entities[b.State.MyId];
            float tx = me.X + 4f, tz = me.Z;
            w.Ask(b, $"MOVE {Protocol.F(tx)} {Protocol.F(tz)}");
            Assert.IsTrue(w.Step(() => a.State.Entities.TryGetValue(b.State.MyId, out var eb) && Math.Abs(eb.X - tx) < 0.8f), "Ana vê o Beto andar");
        }

        [Test]
        public void WhisperPartyAndOnlineList()
        {
            using var w = new World();
            var a = w.Enter("contaclara", "Clara");
            var b = w.Enter("contadavi", "Davi", "PAT");

            StringAssert.StartsWith("OK", w.Ask(a, "W Davi só pra você"));
            Assert.IsTrue(w.Step(() => b.State.Log.Any(l => l.Channel == "whisper" && l.Text.Contains("de Clara") && l.Text.Contains("só pra você"))));
            Assert.IsTrue(a.State.Log.Any(l => l.Channel == "whisper" && l.Text.Contains("para Davi")), "quem manda vê a própria mensagem");
            StringAssert.StartsWith("ERR", w.Ask(a, "W Ninguem oi"));

            var online = w.Ask(a, "ONLINE");
            StringAssert.StartsWith("OK online 2:", online);
            StringAssert.Contains("Clara", online); StringAssert.Contains("Davi", online);

            StringAssert.StartsWith("OK", w.Ask(a, "PARTY INVITE Davi"));
            Assert.IsTrue(w.Step(() => b.All.Any(l => l.StartsWith("@EV INVITE;Clara"))), "convite de grupo chega");
            StringAssert.StartsWith("OK", w.Ask(b, "PARTY ACCEPT"));
            StringAssert.StartsWith("OK", w.Ask(b, "SAY /p vamos"));
            Assert.IsTrue(w.Step(() => a.State.Log.Any(l => l.Channel == "party" && l.Text.Contains("Davi: vamos"))), "chat do grupo");
        }

        [Test]
        public void SelectGoesBackToCharactersAndOthersStopSeeing()
        {
            using var w = new World();
            var a = w.Enter("contaeva", "Eva");
            var b = w.Enter("contafabio", "Fabio");
            Assert.IsTrue(w.Step(() => Sees(a, "Fabio")));
            w.Ask(a, "PARTY INVITE Fabio"); w.Step(() => b.All.Any(l => l.StartsWith("@EV INVITE"))); w.Ask(b, "PARTY ACCEPT");

            var r = w.Ask(b, "SELECT");
            StringAssert.StartsWith("OK chars Fabio:", r);
            Assert.IsTrue(w.Step(() => !Sees(a, "Fabio")), "quem saiu some da tela dos outros");
            Assert.IsTrue(a.All.Any(l => l.Contains("Fabio saiu do mundo")));
            StringAssert.StartsWith("ERR", w.Ask(b, "SAY ainda aqui?"), "sem herói no mundo, nada de comandos de jogo");

            // mesma conexão: escolhe outro herói e volta
            StringAssert.StartsWith("OK", w.Ask(b, "CREATE Fabinho PUN"));
            StringAssert.StartsWith("OK", w.Ask(b, "PLAY Fabinho"));
            Assert.IsTrue(w.Step(() => Sees(a, "Fabinho")), "o novo herói aparece para os outros");
        }

        [Test]
        public void SameAccountCannotPlayTwiceAndIsFreedAfterDisconnect()
        {
            using var w = new World();
            var a = w.Enter("contagil", "Gil");
            var dup = w.Join();
            StringAssert.Contains("já está conectada", w.Ask(dup, "AUTH contagil segredo123"));
            a.Dispose();
            w.Players.Remove(a);
            Assert.IsTrue(w.Step(() => w.Game.OnlineCount == 0), "a queda tira o herói do mundo");
            var r = w.Ask(dup, "AUTH contagil segredo123");
            StringAssert.StartsWith("OK chars Gil:", r);
        }

        [Test]
        public void SilentConnectionIsDroppedButPingKeepsItAlive()
        {
            using var w = new World();
            w.Host.IdleTimeoutSeconds = 0.6f;
            var quiet = w.Enter("contamudo", "Mudo");
            var talker = w.Enter("contafala", "Fala");
            var end = DateTime.UtcNow.AddSeconds(2.5);
            while (DateTime.UtcNow < end)
            {
                talker.Conn.Send("PING");
                w.Step(() => false, 200);
            }
            Assert.IsTrue(w.Step(() => !quiet.Conn.Connected), "conexão muda cai");
            Assert.IsTrue(talker.Conn.Connected, "quem manda PING continua");
            Assert.AreEqual(1, w.Game.OnlineCount);
            Assert.IsTrue(talker.Replies.Contains("OK pong"));
        }

        [Test]
        public void GmOnlyForListedAccounts()
        {
            using var w = new World();
            var dono = w.Enter("donopc", "Dono");
            var visita = w.Enter("visita", "Visita");
            StringAssert.StartsWith("ERR", w.Ask(dono, "GM LEVEL 20"), "ninguém é GM por padrão");
            w.Game.GrantGm("donopc");
            StringAssert.StartsWith("OK nível 20", w.Ask(dono, "GM LEVEL 20"));
            StringAssert.StartsWith("ERR", w.Ask(visita, "GM LEVEL 20"), "os outros jogadores continuam sem GM");
        }

        [Test]
        public void GmKicksBansAndUnbansOverTheNetwork()
        {
            using var w = new World();
            w.Game.GrantGm("contagm");
            var gm = w.Enter("contagm", "Mestre");
            var bad = w.Enter("contaruim", "Bagunca");
            StringAssert.StartsWith("ERR", w.Ask(bad, "GM KICK Mestre"), "jogador comum não expulsa ninguém");

            StringAssert.StartsWith("OK", w.Ask(gm, "GM KICK Bagunca flood no chat"));
            Assert.IsTrue(w.Step(() => !bad.Conn.Connected), "o expulso cai");
            Assert.IsTrue(bad.All.Any(l => l.Contains("desconectado por um GM: flood no chat")), "e vê o motivo");
            Assert.IsTrue(w.Step(() => w.Game.OnlineCount == 1));

            // volta, leva ban de 3 dias: cai e não consegue mais entrar
            var again = w.Join();
            StringAssert.StartsWith("OK", w.Ask(again, "AUTH contaruim segredo123"));
            StringAssert.StartsWith("OK", w.Ask(again, "PLAY Bagunca"));
            StringAssert.StartsWith("OK conta contaruim banida até", w.Ask(gm, "GM BAN Bagunca 3 trapaça"));
            Assert.IsTrue(w.Step(() => !again.Conn.Connected), "banido cai na hora");
            var third = w.Join();
            var r = w.Ask(third, "AUTH contaruim segredo123");
            StringAssert.StartsWith("ERR esta conta está banida", r);
            StringAssert.Contains("trapaça", r);
            StringAssert.Contains("BANIDA", w.Ask(gm, "GM INFO contaruim"));

            StringAssert.StartsWith("OK", w.Ask(gm, "GM UNBAN contaruim"));
            StringAssert.StartsWith("OK chars Bagunca", w.Ask(third, "AUTH contaruim segredo123"));

            // para sempre
            StringAssert.Contains("para sempre", w.Ask(gm, "GM BAN contaruim perm"));
            Assert.IsTrue(w.Step(() => !third.Conn.Connected));
            var fourth = w.Join();
            StringAssert.Contains("para sempre", w.Ask(fourth, "AUTH contaruim segredo123"));
        }

        [Test]
        public void MutedPlayerCannotTalkUntilReleased()
        {
            using var w = new World();
            w.Game.GrantGm("contagm2");
            var gm = w.Enter("contagm2", "Juiz");
            var loud = w.Enter("contagrito", "Grito");
            StringAssert.StartsWith("OK", w.Ask(gm, "GM MUTE Grito 10"));
            Assert.IsTrue(w.Step(() => loud.All.Any(l => l.Contains("chat foi bloqueado"))));
            StringAssert.StartsWith("ERR seu chat está bloqueado", w.Ask(loud, "SAY oi"));
            StringAssert.StartsWith("ERR seu chat está bloqueado", w.Ask(loud, "W Juiz oi"));
            StringAssert.StartsWith("ERR seu chat está bloqueado", w.Ask(loud, "SAY /p oi"));
            StringAssert.StartsWith("OK", w.Ask(loud, "ONLINE"), "o resto do jogo continua");
            StringAssert.StartsWith("OK", w.Ask(gm, "GM UNMUTE Grito"));
            StringAssert.StartsWith("OK", w.Ask(loud, "SAY voltei"));
        }

        [Test]
        public void GmGoesToPlayerAndBringsPlayer()
        {
            using var w = new World();
            w.Game.GrantGm("contagm3");
            var gm = w.Enter("contagm3", "Guia");
            var lost = w.Enter("contaperdido", "Perdido");
            var lc = w.Game.OnlineCharacter("Perdido")!;
            while (lc.Level < 30) lc.GainExperience(Kezor.Core.Characters.Character.ExpToNext(lc.Level) - lc.Experience);
            lc.Gold = 1_000_000;
            StringAssert.StartsWith("OK", w.Ask(lost, "WARP PANTANO"));
            StringAssert.StartsWith("OK", w.Ask(gm, "GM GOTO Perdido"));
            var gc = w.Game.OnlineCharacter("Guia")!;
            Assert.AreEqual(lc.MapId, gc.MapId, "o GM vai para o mapa do jogador");
            Assert.Less(Kezor.Core.MathK.Dist(gc.X, gc.Z, lc.X, lc.Z), 3f);
            StringAssert.StartsWith("OK", w.Ask(gm, "WARP VALE"));
            StringAssert.StartsWith("OK", w.Ask(gm, "GM BRING Perdido"));
            Assert.AreEqual(gc.MapId, lc.MapId, "e traz o jogador até ele");
        }

        [Test]
        public void ServerRequiresAccountsForEveryone()
        {
            using var w = new World();
            var p = w.Join();
            StringAssert.StartsWith("ERR", w.Ask(p, "CREATE Semconta GUA"));
            StringAssert.StartsWith("ERR", w.Ask(p, "LOGIN Qualquer"));
            StringAssert.StartsWith("OK status", w.Ask(p, "STATUS"), "a lista de servidores funciona sem login");
        }
    }
}
