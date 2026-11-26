using System.Globalization;
using System.Linq;
using System.Threading;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    public class ServerTests
    {
        private static GameServer NewServer(int seed = 42, bool gm = true) =>
            new GameServer(new SystemRandom(seed), options: new ServerOptions { AllowGm = gm, EventFirstDelay = 99999 });

        private static void Run(GameServer gs, float seconds, float dt = 0.05f)
        {
            for (float t = 0; t < seconds; t += dt) gs.Tick(dt);
        }

        private static Monster Nearest(GameServer gs, Character c, MonsterDefinition def) =>
            gs.Monsters.Values.Where(m => m.Def == def && !m.IsDead).OrderBy(m => MathK.Dist(c.X, c.Z, m.X, m.Z)).First();

        [Test]
        public void CreateRejectsDuplicatesAndBadClass()
        {
            var gs = NewServer();
            StringAssert.StartsWith("ERR", gs.Handle(1, "STATS"));
            StringAssert.StartsWith("OK", gs.Handle(1, "CREATE Hero GUA"));
            StringAssert.StartsWith("ERR", gs.Handle(2, "CREATE Hero ARC"));
            StringAssert.StartsWith("ERR", gs.Handle(3, "CREATE Other XYZ"));
            var c = gs.GetCharacter(1)!;
            Assert.IsTrue(c.Equipment.ContainsKey(EquipSlot.RightHand));
            Assert.IsTrue(gs.Map.InSafeZone(c.X, c.Z));
        }

        [Test]
        public void NewCharacterWithAccountSpawnsInTownSquare()
        {   // criado com conta e salvo antes do PLAY: não pode nascer no canto (0, 0) do mapa
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kz_spawn_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            try
            {
                var gs = new GameServer(new SystemRandom(7), new JsonFileRepository(dir),
                                        new ServerOptions { RequireAccounts = true, PasswordIterations = 10, EventFirstDelay = 99999 });
                gs.Connect(1);
                StringAssert.StartsWith("OK", gs.Handle(1, "REGISTER conta1 senha123"));
                StringAssert.StartsWith("OK", gs.Handle(1, "CREATE Novato ARC"));
                StringAssert.StartsWith("OK", gs.Handle(1, "PLAY Novato"));
                var c = gs.GetCharacter(1)!;
                Assert.IsTrue(gs.Map.InSafeZone(c.X, c.Z));
                Assert.AreEqual(gs.Map.RespawnX, c.X, 3f);
                Assert.AreEqual(gs.Map.RespawnZ, c.Z, 3f);
            }
            finally { System.IO.Directory.Delete(dir, true); }
        }

        [Test]
        public void MovementIsServerSideAndSpeedLimited()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Walker PAT");
            var c = gs.GetCharacter(1)!;
            c.X = gs.Map.SafeX + 4; c.Z = gs.Map.SafeZ;   // na linha do portão leste (a muralha bloqueia o resto)
            float x0 = c.X;
            gs.Handle(1, $"MOVE {Protocol.F(x0 + 50)} {Protocol.F(c.Z)}");
            Run(gs, 1f);
            Assert.AreEqual(x0 + GameServer.TownWalkSpeed, c.X, 0.4f);   // na vila caminha: só "velocidade × tempo", não teleporta
            Run(gs, 40f);
            Assert.AreEqual(x0 + 50, c.X, 0.1f);
            StringAssert.StartsWith("ERR", gs.Handle(1, "MOVE abc 1"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "MOVE NaN 1"));
        }

        [Test]
        public void WallBlocksAndGatesRoute()
        {
            var gs = NewServer();
            var map = gs.Map;
            // Muralha (16 m × escala da vila): bloqueia fora dos portões, livre no vão entre as torres; casas e fonte bloqueiam.
            float d45 = map.WallRadius * 0.7071f;
            Assert.IsTrue(map.IsBlocked(map.SafeX + d45, map.SafeZ + d45));       // muralha a 45°
            Assert.IsFalse(map.IsBlocked(map.SafeX + map.WallRadius, map.SafeZ)); // portão leste
            Assert.IsTrue(map.IsBlocked(map.SafeX, map.SafeZ));                   // fonte
            foreach (var h in map.Houses) Assert.IsTrue(map.IsBlocked(h.X, h.Z));
            // De dentro para fora pela diagonal: chega dando a volta pelo portão, sem cruzar a muralha.
            gs.Handle(1, "CREATE Mason GUA");
            var c = gs.GetCharacter(1)!;
            c.X = map.SafeX + 8; c.Z = map.SafeZ + 8;
            float tx = map.SafeX + d45 + 4, tz = map.SafeZ + d45 + 4;
            gs.Handle(1, $"MOVE {Protocol.F(tx)} {Protocol.F(tz)}");
            for (int i = 0; i < 1200 && MathK.Dist(c.X, c.Z, tx, tz) > 0.2f; i++)
            {
                gs.Tick(0.05f);
                Assert.IsFalse(map.IsBlocked(c.X, c.Z), $"atravessou algo em {c.X};{c.Z}");
            }
            Assert.AreEqual(0f, MathK.Dist(c.X, c.Z, tx, tz), 0.2f);
        }

        [Test]
        public void EveryGateHasAClearRoadToTheSquare()
        {
            var gs = NewServer();
            var map = gs.Map;
            gs.Handle(1, "CREATE Runner PAT");
            var c = gs.GetCharacter(1)!;
            for (int g = 0; g < 4; g++)
            {
                double a = g * System.Math.PI / 2;
                float ux = (float)System.Math.Cos(a), uz = (float)System.Math.Sin(a);
                // Estrada reta do lado de fora do portão até a borda da praça: nenhum obstáculo a menos de 1,2 m do eixo.
                for (float r = MapDefinition.FountainRadius + 0.6f; r <= map.WallRadius + 6f; r += 0.25f)
                    for (float side = -1.2f; side <= 1.2f; side += 0.6f)
                        Assert.IsFalse(map.IsBlocked(map.SafeX + ux * r - uz * side, map.SafeZ + uz * r + ux * side), $"portão {g}: bloqueado a {r} m");
                // E o herói anda de fora do portão até a praça em linha reta, no tempo da distância.
                c.X = map.SafeX + ux * (map.WallRadius + 6f); c.Z = map.SafeZ + uz * (map.WallRadius + 6f);
                float tx = map.SafeX + ux * (MapDefinition.FountainRadius + 1.5f), tz = map.SafeZ + uz * (MapDefinition.FountainRadius + 1.5f);
                gs.Handle(1, $"MOVE {Protocol.F(tx)} {Protocol.F(tz)}");
                Run(gs, (map.WallRadius + 2f) / GameServer.TownWalkSpeed + 0.5f);   // dentro da vila caminha
                Assert.AreEqual(0f, MathK.Dist(c.X, c.Z, tx, tz), 0.2f, $"portão {g}");
            }
        }

        [Test]
        public void AutoAttackApproachesKillsAndLevels()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Knight GUA");
            var c = gs.GetCharacter(1)!;
            for (int i = 0; i < 60 && c.Level == 1; i++)
            {
                var m = Nearest(gs, c, MonsterCatalog.MossSlime);
                StringAssert.StartsWith("OK", gs.Handle(1, $"ATTACK {m.Id}"));
                Run(gs, 3f);
                c.Hp = c.MaxHp;
            }
            Assert.Greater(c.Level, 1);
            Assert.Greater(c.Gold, GameServer.StartingGold);
            StringAssert.StartsWith("OK", gs.Handle(1, "ADD STR 5"));
        }

        [Test]
        public void AggressiveMonstersChaseButNeverEnterTown()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Bait GUA");
            var c = gs.GetCharacter(1)!;
            var wolf = Nearest(gs, c, MonsterCatalog.AshWolf);
            c.X = wolf.X + 3; c.Z = wolf.Z;
            Run(gs, 0.2f);
            Assert.AreEqual(MonsterState.Chase, wolf.State);
            Run(gs, 3f);
            Assert.Less(c.Hp, c.MaxHp);   // foi atacado
            c.X = gs.Map.SafeX; c.Z = gs.Map.SafeZ; c.Hp = c.MaxHp;
            Run(gs, 10f);
            Assert.IsFalse(gs.Map.InSafeZone(wolf.X, wolf.Z));
            Assert.AreNotEqual(MonsterState.Chase, wolf.State);
        }

        [Test]
        public void ServerRejectsCheats()
        {
            var gs = NewServer(1);
            gs.Handle(1, "CREATE Mage ARC");
            StringAssert.StartsWith("ERR", gs.Handle(1, "ADD ENE 999"));
            int id = gs.Monsters.Keys.First();
            StringAssert.Contains("classe", gs.Handle(1, $"SKILL 1 {id}"));    // skill do Guardião
            StringAssert.Contains("nível", gs.Handle(1, $"SKILL 4 {id}"));     // nível insuficiente
            StringAssert.StartsWith("ERR", gs.Handle(1, "FORGE 0,0"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "ATTACK abc"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "ATTACK 99999999999"));
        }

        [Test]
        public void CommandsRequireTown()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Buyer PAT");
            var c = gs.GetCharacter(1)!;
            StringAssert.StartsWith("OK", gs.Handle(1, $"BUY {ItemDatabase.HealthPotion.Id} 5"));
            c.X = 10; c.Z = 10;
            StringAssert.Contains("vila", gs.Handle(1, $"BUY {ItemDatabase.HealthPotion.Id} 5"));
            StringAssert.StartsWith("ERR", gs.Handle(1, $"BUY {ItemDatabase.RuneOfDawn.Id}"));
        }

        [Test]
        public void PotionsStackAndHeal()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Drinker GUA");
            var c = gs.GetCharacter(1)!;
            int slot = Enumerable.Range(0, c.Inventory.Capacity).First(i => c.Inventory.Get(i)?.Def == ItemDatabase.HealthPotion);
            int qty = c.Inventory.Get(slot)!.Quantity;
            gs.Handle(1, $"BUY {ItemDatabase.HealthPotion.Id} 5");
            Assert.AreEqual(qty + 5, c.Inventory.Get(slot)!.Quantity);
            c.Hp = 1;
            StringAssert.StartsWith("OK", gs.Handle(1, $"USE {slot}"));
            Assert.AreEqual(1 + ItemDatabase.HealthPotion.PotionAmount, c.Hp);
        }

        [Test]
        public void RuneAndForgeThroughProtocol()
        {
            var gs = new GameServer(new FixedRandom(0), options: new ServerOptions { EventFirstDelay = 99999 });
            gs.Handle(1, "CREATE Smith GUA");
            var c = gs.GetCharacter(1)!;
            c.Gold = 100_000_000;
            c.Inventory.TryAdd(new Item(ItemDatabase.StormBlade, 9), out var w);
            c.Inventory.TryAdd(new Item(ItemDatabase.RuneOfDawn), out var b);
            c.Inventory.TryAdd(new Item(ItemDatabase.RuneOfEclipse), out var s);
            c.Inventory.TryAdd(new Item(ItemDatabase.VortexCrystal), out var ch);
            StringAssert.Contains("60%", gs.Handle(1, $"FORGEPREVIEW {w},{b},{s},{ch}"));
            StringAssert.StartsWith("OK", gs.Handle(1, $"FORGE {w},{b},{s},{ch}"));
            Assert.AreEqual(10, c.Inventory.Get(w)!.Level);
            Assert.IsNull(c.Inventory.Get(b));
            Assert.AreEqual(100_000_000 - 2_000_000, c.Gold);

            c.Inventory.TryAdd(new Item(ItemDatabase.LeatherHelm), out var helm);
            c.Inventory.TryAdd(new Item(ItemDatabase.RuneOfDawn), out var dawn);
            StringAssert.StartsWith("OK", gs.Handle(1, $"RUNE {dawn} {helm}"));
            Assert.AreEqual(1, c.Inventory.Get(helm)!.Level);
        }

        [Test]
        public void DropsAreReservedForOwner()
        {
            var gs = NewServer(7);
            gs.Handle(1, "CREATE Owner GUA");
            gs.Handle(2, "CREATE Thief GUA");
            var owner = gs.GetCharacter(1)!; var thief = gs.GetCharacter(2)!;
            gs.Handle(1, "GM LEVEL 30");
            GroundItem? drop = null;
            for (int i = 0; i < 400 && drop == null; i++)
            {
                var m = Nearest(gs, owner, MonsterCatalog.MossSlime);
                gs.Handle(1, $"ATTACK {m.Id}");
                Run(gs, 2f);
                owner.Hp = owner.MaxHp;
                drop = gs.GroundItems.Values.FirstOrDefault(g => g.OwnerId == owner.Id);
            }
            Assert.IsNotNull(drop, "nenhum drop em 400 abates");
            thief.X = drop!.X; thief.Z = drop.Z;
            gs.Handle(2, $"PICK {drop.Id}");
            Run(gs, 0.2f);
            Assert.IsTrue(gs.GroundItems.ContainsKey(drop.Id));
            gs.Handle(1, $"PICK {drop.Id}");
            Run(gs, 30f);
            Assert.IsFalse(gs.GroundItems.ContainsKey(drop.Id));
            Assert.IsTrue(Enumerable.Range(0, 32).Any(i => owner.Inventory.Get(i)?.Def == drop.Item.Def));
        }

        [Test]
        public void PartySharesExperience()
        {
            var gs = NewServer(3);
            gs.Handle(1, "CREATE Leader GUA");
            gs.Handle(2, "CREATE Healer ARC");
            StringAssert.StartsWith("OK", gs.Handle(1, "PARTY INVITE healer"));
            StringAssert.StartsWith("OK", gs.Handle(2, "PARTY ACCEPT"));
            var a = gs.GetCharacter(1)!; var b = gs.GetCharacter(2)!;
            var slime = Nearest(gs, a, MonsterCatalog.MossSlime);
            b.X = slime.X + 1; b.Z = slime.Z;
            for (int i = 0; i < 30 && a.Experience == 0 && a.Level == 1; i++) { gs.Handle(1, $"ATTACK {Nearest(gs, a, MonsterCatalog.MossSlime).Id}"); Run(gs, 2f); a.Hp = a.MaxHp; b.Hp = b.MaxHp; }
            Assert.Greater(b.Experience + b.Level - 1, 0, "membro do grupo perto deve ganhar exp");
            StringAssert.StartsWith("OK", gs.Handle(2, "PARTY LEAVE"));
        }

        [Test]
        public void PvpBlockedInTownAndForParty()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Duelist GUA");
            gs.Handle(2, "CREATE Target GUA");
            var a = gs.GetCharacter(1)!; var b = gs.GetCharacter(2)!;
            gs.Handle(1, "GM LEVEL 100"); gs.Handle(1, "ADD STR 490"); gs.Handle(2, "GM LEVEL 10");
            StringAssert.Contains("vila", gs.Handle(1, $"ATTACK {b.Id}"));
            a.X = 30; a.Z = 30; b.X = 31; b.Z = 30;
            StringAssert.StartsWith("OK", gs.Handle(1, $"ATTACK {b.Id}"));
            for (int i = 0; i < 400 && a.Infamy == 0; i++) gs.Tick(0.05f);
            Assert.AreEqual(1, a.Infamy);
            Assert.IsTrue(gs.Map.InSafeZone(b.X, b.Z));   // derrotado renasce na vila
        }

        [Test]
        public void InvasionEventSpawnsBossAndEnds()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Hero GUA");
            gs.Handle(1, "GM EVENT");
            Run(gs, 0.1f);
            Assert.IsTrue(gs.EventActive);
            var boss = gs.Monsters.Values.Single(m => m.Def == MonsterCatalog.AshHerald);
            foreach (var m in gs.Monsters.Values.Where(m => m.Temporary).ToList()) m.Hp = 1;
            var c = gs.GetCharacter(1)!;
            gs.Handle(1, "GM LEVEL 200");
            gs.Handle(1, "ADD STR 900");
            for (int i = 0; i < 200 && gs.EventActive; i++)
            {
                var t = gs.Monsters.Values.FirstOrDefault(m => m.Temporary && !m.IsDead);
                if (t == null) break;
                gs.Handle(1, $"ATTACK {t.Id}"); Run(gs, 1f); c.Hp = c.MaxHp;
            }
            Run(gs, 0.2f);
            Assert.IsFalse(gs.EventActive);
            Assert.GreaterOrEqual(gs.GroundItems.Values.Count(g => g.Item.Def.IsRune), MonsterCatalog.AshHerald.GuaranteedRunes);
        }

        [Test]
        public void MonstersRespawn()
        {
            var gs = NewServer(3);
            var m = gs.Monsters.Values.First();
            m.Hp = 0; m.RespawnTimer = 2;
            gs.Tick(0.5f); gs.Tick(0.5f); Assert.IsTrue(m.IsDead);
            gs.Tick(0.5f); gs.Tick(0.5f); gs.Tick(0.1f); Assert.IsFalse(m.IsDead);
        }

        [Test]
        public void GmCommandsDisabledByDefault()
        {
            var gs = NewServer(gm: false);
            gs.Handle(1, "CREATE Plain GUA");
            StringAssert.StartsWith("ERR", gs.Handle(1, "GM LEVEL 400"));
        }
    }

    public class ProtocolTests
    {
        [Test]
        public void ClientStateMirrorsServerEvenWithCommaLocale()
        {
            var prev = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-BR");
            try
            {
                var gs = new GameServer(new SystemRandom(5), options: new ServerOptions { EventFirstDelay = 99999, ViewRange = 200f });   // a vila v3 (raio 72) é maior que a AOI de 60 m
                var conn = new LoopbackConnection(gs);
                var st = new ClientState();
                conn.Send("CREATE Viewer ARC");
                for (int i = 0; i < 10; i++) conn.Update(0.05f);
                while (conn.TryReceive(out var l)) st.Process(l);
                Assert.IsTrue(st.InWorld);
                var me = gs.GetCharacter(1)!;
                Assert.AreEqual(me.X, st.Me.X, 0.01f);
                Assert.AreEqual(CharacterClass.Arcanist, st.Me.Class);
                Assert.IsTrue(st.Entities.ContainsKey(me.Id));
                Assert.IsTrue(st.Entities.Values.Any(e => e.Kind == EntityKind.Monster));
                Assert.IsTrue(st.Inventory.Any(i => i?.Def == ItemDatabase.HealthPotion));
                Assert.IsTrue(st.Equipment.ContainsKey(EquipSlot.RightHand));
                conn.Send($"MOVE {Protocol.F(MapDefinition.ValeCenter + 1.5f)} {Protocol.F(MapDefinition.ValeCenter - 9.75f)}");
                for (int i = 0; i < 100; i++) conn.Update(0.05f);
                while (conn.TryReceive(out var l)) st.Process(l);
                Assert.AreEqual(MapDefinition.ValeCenter + 1.5f, st.Me.X, 0.06f);
                conn.Send("SAY olá; mundo|teste");
                while (conn.TryReceive(out var l)) st.Process(l);
                Assert.IsTrue(st.Log.Any(x => x.Channel == "say" && x.Text.Contains("olá, mundo/teste")));
            }
            finally { Thread.CurrentThread.CurrentCulture = prev; }
        }

        [Test]
        public void TcpEndToEnd()
        {
            var gs = new GameServer(new SystemRandom(9), options: new ServerOptions { EventFirstDelay = 99999, ViewRange = 200f });   // a vila v3 (raio 72) é maior que a AOI de 60 m
            using var host = new TcpServerHost(gs, 0);
            host.Start();
            using var conn = new TcpClientConnection("127.0.0.1", host.Port);
            var st = new ClientState();
            for (int i = 0; i < 200 && !conn.Connected; i++) Thread.Sleep(10);
            Assert.IsTrue(conn.Connected, conn.Error);
            conn.Send("CREATE Netter PAT");
            for (int i = 0; i < 100 && st.Entities.Count == 0; i++)
            {
                host.Pump(); gs.Tick(0.05f); host.Flush(); Thread.Sleep(10);
                while (conn.TryReceive(out var l)) st.Process(l);
            }
            Assert.IsTrue(st.InWorld);
            Assert.Greater(st.Entities.Count, 1);
        }
    }
}
