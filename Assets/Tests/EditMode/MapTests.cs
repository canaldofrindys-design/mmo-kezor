using System.Collections.Generic;
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
    /// <summary>Vários mapas: portais, /warp, AOI por mapa, A* na masmorra, chefe da cripta.</summary>
    public class MapTests
    {
        private static GameServer NewServer(int seed = 42) =>
            new GameServer(new SystemRandom(seed), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999 });

        private static void Run(GameServer gs, float seconds, float dt = 0.05f)
        {
            for (float t = 0; t < seconds; t += dt) gs.Tick(dt);
        }

        [Test]
        public void MapsAreConsistent()
        {
            var maps = MapDefinition.CreateAll();
            Assert.AreEqual(4, maps.Count);
            Assert.AreEqual(80, maps[MapDefinition.EscarpId].MinLevel);
            Assert.AreEqual(20, maps[MapDefinition.SwampId].MinLevel);
            Assert.AreEqual(45, maps[MapDefinition.CryptId].MinLevel);
            foreach (var m in maps.Values)
            {
                Assert.IsFalse(m.IsBlocked(m.RespawnX, m.RespawnZ), $"{m.Name}: ponto de chegada bloqueado");
                foreach (var g in m.Spawns) Assert.IsFalse(m.IsBlocked(g.X, g.Z), $"{m.Name}: spawn de {g.Monster.Name} bloqueado");
                foreach (var p in m.Portals)
                {
                    var dest = maps[p.TargetMap];
                    Assert.IsFalse(m.IsBlocked(p.X, p.Z), $"{m.Name}: portal bloqueado");
                    Assert.IsFalse(dest.IsBlocked(p.TargetX, p.TargetZ), $"{dest.Name}: destino do portal bloqueado");
                    Assert.IsTrue(dest.Portals.All(q => MathK.Dist(q.X, q.Z, p.TargetX, p.TargetZ) > q.Radius + 1), "chegaria em cima de outro portal");
                    Assert.IsTrue(dest.Portals.Any(q => q.TargetMap == m.Id), $"{dest.Name} sem portal de volta para {m.Name}");
                }
                Assert.IsNotEmpty(m.WarpCode);
            }
            // Monstros novos de cada mapa estão na faixa de nível do mapa
            Assert.IsTrue(maps[MapDefinition.SwampId].Spawns.All(g => g.Monster.Level >= 20 && g.Monster.Level <= 45));
            Assert.IsTrue(maps[MapDefinition.CryptId].Spawns.All(g => g.Monster.Level >= 45 && g.Monster.Level <= 80));
            Assert.IsTrue(maps[MapDefinition.CryptId].Spawns.Any(g => g.Monster.Boss && g.Monster.SpecialDrops.Length > 0));
            Assert.IsTrue(maps[MapDefinition.EscarpId].Spawns.All(g => g.Monster.Level >= 80 && g.Monster.Level <= 120));
        }

        [Test]
        public void TownLayoutLeavesStreetsAndPlazasFree()
        {
            var m = MapDefinition.Get(MapDefinition.ValeId);
            var problems = new List<string>();
            // pontos (a cada 0,5 m) do corpo principal de cada prédio, já em mundo
            List<(float x, float z)> Body(TownHouse h)
            {
                var pts = new List<(float, float)>();
                var (bx, bz, hx, hz) = MapDefinition.HouseFootprint(h.Kind)[0];
                for (float u = -hx + 0.3f; u <= hx - 0.3f; u += 0.5f)
                    for (float v = -hz + 0.3f; v <= hz - 0.3f; v += 0.5f)
                        pts.Add(MapDefinition.LocalToWorld(h.X, h.Z, h.Yaw, h.Scale, bx + u, bz + v));
                return pts;
            }
            var boxes = m.Houses.Select(h => MapDefinition.HouseBoxes(h).ToList()).ToList();
            for (int i = 0; i < m.Houses.Count; i++)
            {
                var h = m.Houses[i];
                foreach (var (x, z) in Body(h))
                {
                    if (m.IsTownStreet(x, z)) { problems.Add($"{h.Kind} #{i} invade a rua em {x - m.SafeX:0.0};{z - m.SafeZ:0.0}"); break; }
                    if (m.TownDistance(x, z) > MapDefinition.LaneRadius) { problems.Add($"{h.Kind} #{i} encosta na muralha"); break; }
                }
                for (int j = 0; j < m.Houses.Count; j++)
                    if (j != i && Body(h).Any(p => boxes[j].Any(b => b.Contains(p.x, p.z, 0.1f))))
                        problems.Add($"{h.Kind} #{i} sobreposto a {m.Houses[j].Kind} #{j}");
                // a porta (frente) dá para um lugar livre: 1 m à frente do prédio não é parede nem outro prédio
                var (fx, fz) = MapDefinition.LocalToWorld(h.X, h.Z, h.Yaw, h.Scale, 0f, MapDefinition.HouseFootprint(h.Kind)[0].HZ + 1.6f);
                if (h.Kind != "Taverna4" && h.Kind != "Guilda4" && h.Kind != "Capela4" && h.Kind != "Loja4" && m.IsBlocked(fx, fz))
                    problems.Add($"{h.Kind} #{i}: porta bloqueada");
            }
            foreach (var p in m.Props)
            {
                if (p.R <= 0) continue;
                for (int j = 0; j < m.Houses.Count; j++)
                    if (boxes[j].Any(b => b.Contains(p.X, p.Z, p.R))) { problems.Add($"{p.Kind} dentro do prédio #{j} ({m.Houses[j].Kind})"); break; }
            }
            foreach (var n in m.Npcs)
                if (m.IsBlocked(n.X, n.Z)) problems.Add($"NPC {n.Name} preso num obstáculo");
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.Greater(m.Houses.Count, 40);
        }

        [Test]
        public void PortalChecksLevelAndMovesToTheOtherMap()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Viajante GUA");
            var c = gs.GetCharacter(1)!;
            var portal = gs.Map.Portals.First(p => p.TargetMap == MapDefinition.SwampId);
            Run(gs, 2.1f);                                         // espera do portal após entrar no mundo
            c.X = portal.X; c.Z = portal.Z;
            Run(gs, 0.1f);
            Assert.AreEqual(MapDefinition.ValeId, c.MapId, "nível 1 não entra no pântano");
            gs.Handle(1, "GM LEVEL 25");
            Run(gs, 2.1f);
            Assert.AreEqual(MapDefinition.SwampId, c.MapId);
            Assert.AreEqual(portal.TargetX, c.X, 0.01f);
            Assert.AreEqual(portal.TargetZ, c.Z, 0.01f);
            var outbox = gs.DrainOutbox(1)!;
            Assert.IsTrue(outbox.Any(l => l == $"@WELCOME {c.Id} {MapDefinition.SwampId}"));
        }

        [Test]
        public void WarpCostsGoldAndNeedsLevel()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Mago ARC");
            var c = gs.GetCharacter(1)!;
            StringAssert.Contains("CRIPTA", gs.Handle(1, "WARP"));
            StringAssert.StartsWith("ERR", gs.Handle(1, "WARP CRIPTA"));              // nível 1
            gs.Handle(1, "GM LEVEL 50");
            c.Gold = 1000;
            StringAssert.StartsWith("ERR", gs.Handle(1, "WARP CRIPTA"));              // sem ouro
            c.Gold = 200_000;
            StringAssert.StartsWith("OK", gs.Handle(1, "WARP CRIPTA"));
            Assert.AreEqual(MapDefinition.CryptId, c.MapId);
            Assert.AreEqual(80_000, c.Gold);
            Assert.IsFalse(gs.MapOf(c.MapId).IsBlocked(c.X, c.Z));
            StringAssert.StartsWith("OK", gs.Handle(1, "WARP VALE"));                 // voltar é grátis
            Assert.AreEqual(MapDefinition.ValeId, c.MapId);
            Assert.AreEqual(80_000, c.Gold);
            StringAssert.StartsWith("ERR", gs.Handle(1, "WARP NARNIA"));
        }

        [Test]
        public void SnapshotsOnlyShowTheSameMap()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Aqui GUA");
            gs.Handle(2, "CREATE Ali GUA");
            gs.Handle(2, "GM LEVEL 30");
            var a = gs.GetCharacter(1)!; var b = gs.GetCharacter(2)!;
            b.Gold = 1_000_000;
            StringAssert.StartsWith("OK", gs.Handle(2, "WARP PANTANO"));
            a.X = b.X; a.Z = b.Z;                                     // mesmas coordenadas, mapas diferentes
            gs.DrainOutbox(1); gs.DrainOutbox(2);
            Run(gs, 0.2f);
            var va = new ClientState(); foreach (var l in gs.DrainOutbox(1)!) va.Process(l);
            var vb = new ClientState(); foreach (var l in gs.DrainOutbox(2)!) vb.Process(l);
            Assert.IsFalse(va.Entities.ContainsKey(b.Id));
            Assert.IsFalse(vb.Entities.ContainsKey(a.Id));
            Assert.IsTrue(vb.Entities.Values.Where(e => e.Kind == EntityKind.Monster).All(e => MonsterCatalog.Get(e.TypeId)!.Level >= 20));
            Assert.AreEqual(MapDefinition.SwampId, gs.MapOf(b.MapId).Id);
            StringAssert.StartsWith("ERR", gs.Handle(1, $"ATTACK {gs.Monsters.Values.First(m => m.MapId == MapDefinition.SwampId).Id}"));
        }

        [Test]
        public void PathfindingCrossesTheCryptAndTheTown()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Explorador PAT");
            gs.Handle(1, "GM LEVEL 300");
            var c = gs.GetCharacter(1)!;
            c.Gold = 1_000_000;
            gs.Handle(1, "WARP CRIPTA");
            foreach (var m in gs.Monsters.Values) if (m.MapId == MapDefinition.CryptId) m.Hp = 0;   // sem distração
            var map = gs.MapOf(MapDefinition.CryptId);
            // Do átrio até o fundo do ninho das aranhas: precisa dobrar dois corredores
            gs.Handle(1, "MOVE 70 108");
            for (int i = 0; i < 1200 && MathK.Dist(c.X, c.Z, 70, 108) > 0.2f; i++)
            {
                gs.Tick(0.05f);
                Assert.IsFalse(map.IsBlocked(c.X, c.Z), $"atravessou parede em {c.X};{c.Z}");
            }
            Assert.AreEqual(0f, MathK.Dist(c.X, c.Z, 70, 108), 0.25f);

            // Na vila: de dentro de uma casa para trás da fonte, contornando tudo
            gs.Handle(1, "WARP VALE");
            var vale = gs.Map;
            float vc = MapDefinition.ValeCenter;
            c.X = vc; c.Z = vc - 7.5f;
            gs.Handle(1, $"MOVE {Protocol.F(vc)} {Protocol.F(vc + 7.5f)}");      // atrás da fonte (bloqueada no centro)
            for (int i = 0; i < 400 && MathK.Dist(c.X, c.Z, vc, vc + 7.5f) > 0.2f; i++) gs.Tick(0.05f);
            Assert.AreEqual(0f, MathK.Dist(c.X, c.Z, vc, vc + 7.5f), 0.25f);
        }

        [Test]
        public void DyingInTheCryptWakesUpInTheVillage()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Azarado GUA");
            gs.Handle(1, "GM LEVEL 46");
            var c = gs.GetCharacter(1)!;
            c.Gold = 1_000_000;
            gs.Handle(1, "WARP CRIPTA");
            var sentinel = gs.Monsters.Values.First(m => m.Def == MonsterCatalog.GlassSentinel);
            c.X = sentinel.X + 1; c.Z = sentinel.Z; c.Hp = 1;
            sentinel.State = MonsterState.Chase; sentinel.TargetId = c.Id;
            for (int i = 0; i < 100 && c.MapId == MapDefinition.CryptId; i++) gs.Tick(0.1f);
            Assert.AreEqual(MapDefinition.ValeId, c.MapId);
            Assert.IsTrue(gs.Map.InSafeZone(c.X, c.Z));
        }

        [Test]
        public void SovereignAlwaysDropsAnEpicOrWings()
        {
            var rng = new SystemRandom(5);
            for (int i = 0; i < 20; i++)
            {
                var drops = DropSystem.Roll(MonsterCatalog.BlackGlassSovereign, rng);
                Assert.IsTrue(drops.Any(d => d.Def.Epic || d.Def.IsWings), "sem arma épica ou asas");
            }
        }
    
        [Test]
        public void CityV4IsWalledNavigableAndCoherent()
        {
            var m = MapDefinition.Get(MapDefinition.ValeId);
            Assert.AreEqual(MapDefinition.TownWallRadius, m.WallRadius, 0.01f, "muralha quadrada de 104 m (cidade v4)");
            Assert.Greater(m.Size, 2 * (m.WallRadius * 1.42f + 40), "mapa com bosque em volta da cidade");
            Assert.GreaterOrEqual(m.Houses.Count, 40, "quarteirões de casas");
            Assert.IsTrue(m.InSafeZone(m.RespawnX, m.RespawnZ));
            Assert.IsTrue(m.InSafeZone(m.SafeX + m.WallRadius - 3, m.SafeZ + m.WallRadius - 3), "canto de dentro da muralha é cidade");
            Assert.IsFalse(m.InSafeZone(m.SafeX + m.WallRadius + 4, m.SafeZ), "fora da muralha não é cidade");
            Assert.IsFalse(m.IsBlocked(m.RespawnX, m.RespawnZ), "renascer na praça, fora da fonte");
            foreach (var kind in new[] { "Taverna4", "Loja4", "Ferraria4", "Capela4", "Guilda4", "Estabulo4" })
                Assert.IsTrue(m.Houses.Exists(h => h.Kind == kind), $"falta {kind}");
            foreach (var kind in new[] { "Barraca4R", "Poste4", "Braseiro4", "Poco4", "Estandarte4" })
                Assert.IsTrue(m.Props.Exists(p => p.Kind == kind), $"falta {kind}");
            Assert.AreEqual(12, m.Towers.Count, "4 torres de canto e 8 no meio dos trechos");
            var pf = new Pathfinder(m);
            // cada NPC é alcançável a pé da praça (ponto de conversa do lado da praça, como o cliente faz)
            foreach (var n in m.Npcs)
            {
                Assert.IsTrue(m.InSafeZone(n.X, n.Z), $"{n.Name} fora da vila");
                float dx = m.SafeX - n.X, dz = m.SafeZ - n.Z, d = (float)System.Math.Sqrt(dx * dx + dz * dz);
                float ax = n.X + dx / d * 3.2f, az = n.Z + dz / d * 3.2f;
                Assert.IsFalse(m.IsBlocked(ax, az), $"{n.Name}: lugar de falar bloqueado");
                var path = pf.Find(m.RespawnX, m.RespawnZ, ax, az);
                Assert.IsNotNull(path, $"{n.Name}: sem caminho da praça");
            }
            var ilsa = m.Npcs.Single(n => n.Role == "vault");
            Assert.LessOrEqual(MathK.Dist(ilsa.X, ilsa.Z, MapDefinition.VaultChestX, MapDefinition.VaultChestZ), GameServer.VaultRange, "baú perto da guardiã");
            Assert.IsTrue(m.IsBlocked(MapDefinition.VaultChestX, MapDefinition.VaultChestZ));
            // de fora de cada portão até a praça existe caminho; os quatro cantos da rua da muralha também se alcançam
            for (int g = 0; g < 4; g++)
            {
                double a = g * System.Math.PI / 2;
                float ox = m.SafeX + (float)System.Math.Cos(a) * (m.WallRadius + 8), oz = m.SafeZ + (float)System.Math.Sin(a) * (m.WallRadius + 8);
                Assert.IsFalse(m.IsBlocked(ox, oz));
                Assert.IsNotNull(pf.Find(ox, oz, m.RespawnX, m.RespawnZ), $"portão {g}: sem caminho até a praça");
                float lane = MapDefinition.LaneRadius + 0.3f;
                float cx = m.SafeX + (g == 0 || g == 3 ? lane : -lane), cz = m.SafeZ + (g < 2 ? lane : -lane);
                Assert.IsFalse(m.IsBlocked(cx, cz), $"canto {g} da rua da muralha bloqueado");
                Assert.IsNotNull(pf.Find(m.RespawnX, m.RespawnZ, cx, cz), $"canto {g} da rua da muralha sem caminho");
            }
            foreach (var p in m.Portals)
            {
                Assert.Greater(m.TownDistance(p.X, p.Z), m.WallRadius + p.Radius + 4f, $"{p.Label}: colado na muralha");
                Assert.IsFalse(m.IsBlocked(p.X, p.Z), $"{p.Label}: portal bloqueado");
                var dest = MapDefinition.Get(p.TargetMap);
                Assert.IsFalse(dest.IsBlocked(p.TargetX, p.TargetZ), $"{p.Label}: chegada bloqueada");
            }
            foreach (var other in MapDefinition.AllMaps)
                foreach (var p in other.Portals.Where(x => x.TargetMap == MapDefinition.ValeId))
                    Assert.IsFalse(m.IsBlocked(p.TargetX, p.TargetZ), "chegada no Vale bloqueada");
            foreach (var g in m.Spawns)
            {
                Assert.Greater(m.TownDistance(g.X, g.Z) - g.Radius, m.WallRadius + 6f, $"{g.Monster.Name} encostado na muralha");
                Assert.IsTrue(g.X - g.Radius > 10 && g.Z - g.Radius > 10 && g.X + g.Radius < m.Size - 10 && g.Z + g.Radius < m.Size - 10, $"{g.Monster.Name} na borda");
            }
        }

        [Test]
        public void HeroStaysWhereHeLoggedOut()
        {
            var vale = MapDefinition.Get(MapDefinition.ValeId);
            System.Collections.Generic.IDictionary<string, object?> Save(Character ch) =>
                (System.Collections.Generic.IDictionary<string, object?>)MiniJson.Read(MiniJson.Write(SaveData.CharacterToJson(ch, includeVault: false)))!;
            // save antigo (sem versão, ou planta v2 de 260 m): o mesmo lugar em relação à vila, deslocado pelo centro novo
            float shift = MapDefinition.ValeCenter - MapDefinition.ValeCenterV2;
            var c = new Character(1, "Andarilho", CharacterClass.Warden) { MapId = MapDefinition.ValeId, X = 91, Z = 67 };
            var j = Save(c); j.Remove("mapv");
            var back = SaveData.CharacterFromJson(j, 2);
            Assert.AreEqual(91 + shift, back.X, 0.01f); Assert.AreEqual(67 + shift, back.Z, 0.01f);
            j = Save(c); j["mapv"] = 2;
            back = SaveData.CharacterFromJson(j, 2);
            Assert.AreEqual(91 + shift, back.X, 0.01f); Assert.AreEqual(67 + shift, back.Z, 0.01f);
            // save da planta atual em lugar livre: fica exatamente onde saiu
            back = SaveData.CharacterFromJson(Save(c), 2);
            Assert.AreEqual(91, back.X, 0.01f); Assert.AreEqual(67, back.Z, 0.01f);
            c.X = vale.SafeX + 1; c.Z = vale.SafeZ - 25;
            back = SaveData.CharacterFromJson(Save(c), 3);
            Assert.AreEqual(vale.SafeX + 1, back.X, 0.01f); Assert.AreEqual(vale.SafeZ - 25, back.Z, 0.01f);
            // posição que não existe mais (dentro de uma casa): o ponto livre mais perto, não a praça
            var h = vale.Houses[10];
            c.X = h.X; c.Z = h.Z;
            back = SaveData.CharacterFromJson(Save(c), 4);
            Assert.IsFalse(vale.IsBlocked(back.X, back.Z));
            Assert.Less(MathK.Dist(back.X, back.Z, h.X, h.Z), 6f, "fica ao lado da casa");
            // fora do mapa: puxado para a borda, livre
            c.X = -5; c.Z = 400;
            back = SaveData.CharacterFromJson(Save(c), 5);
            Assert.IsTrue(back.X >= 1 && back.Z <= vale.Size - 1);
            Assert.IsFalse(vale.IsBlocked(back.X, back.Z));
        }

        [Test]
        public void LogoutAndLoginKeepExactPositionAndFacingInEveryMap()
        {
            var gs = new GameServer(new SystemRandom(8), options: new ServerOptions { RequireAccounts = true, EventFirstDelay = 99999, PasswordIterations = 2000, AutosaveSeconds = 0, AllowGm = true });
            gs.Handle(1, "REGISTER viajante segredo1");
            gs.Handle(1, "CREATE Errante PAT");
            StringAssert.StartsWith("OK", gs.Handle(1, "PLAY Errante"));
            var c = gs.GetCharacter(1)!;
            gs.Handle(1, "GM LEVEL 300"); c.Gold = 10_000_000;
            foreach (var (code, mapId, x, z) in new (string, int, float, float)[]
            {
                ("VALE", MapDefinition.ValeId, 91.3f, 67.7f),        // no bosque, fora da vila
                ("PANTANO", MapDefinition.SwampId, 24.5f, 60.25f),
                ("CRIPTA", MapDefinition.CryptId, 62f, 58f),
            })
            {
                if (c.MapId != mapId) StringAssert.StartsWith("OK", gs.Handle(1, $"WARP {code}"));
                Assert.AreEqual(mapId, c.MapId);
                c.X = x - 2; c.Z = z;
                gs.Handle(1, $"MOVE {Protocol.F(x)} {Protocol.F(z)}");       // anda 2 m para leste: olha para +X (90°)
                for (int i = 0; i < 40 && MathK.Dist(c.X, c.Z, x, z) > 0.05f; i++) gs.Tick(0.05f);
                float fx = c.X, fz = c.Z, yaw = c.Facing;
                Assert.AreEqual(90f, yaw, 1f);
                gs.Disconnect(1);                                                 // sair (queda, fechar o jogo, trocar de herói: o mesmo caminho)
                gs.Handle(1, "AUTH viajante segredo1");
                StringAssert.StartsWith("OK", gs.Handle(1, "PLAY Errante"));
                c = gs.GetCharacter(1)!;
                Assert.AreEqual(mapId, c.MapId, $"{code}: mesmo mapa");
                Assert.AreEqual(fx, c.X, 0.01f, $"{code}: mesmo X"); Assert.AreEqual(fz, c.Z, 0.01f, $"{code}: mesmo Z");
                Assert.AreEqual(yaw, c.Facing, 0.5f, $"{code}: mesma direção");
            }
        }
    }
}
