using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>IA dos monstros com A*: contorna quinas da masmorra, respeita o leash e volta ao ponto de origem.</summary>
    public class MonsterAiTests
    {
        private static GameServer NewServer() =>
            new GameServer(new SystemRandom(7), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999 });

        private static (GameServer gs, Character c, Monster m) CryptCorner()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Isca GUA");
            gs.Handle(1, "GM LEVEL 400");
            var c = gs.GetCharacter(1)!;
            c.Gold = 1_000_000;
            gs.Handle(1, "WARP CRIPTA");
            foreach (var other in gs.Monsters.Values) if (other.MapId == MapDefinition.CryptId) other.Hp = 0;   // só o nosso
            // Monstro no canto sudeste do átrio; herói no corredor leste: a linha reta corta a quina da parede.
            var m = gs.Spawn(MonsterCatalog.GlassSentinel, MapDefinition.CryptId, 26, 52);
            c.X = 36; c.Z = 60; c.Hp = c.MaxHp;
            m.State = MonsterState.Chase; m.TargetId = c.Id;
            return (gs, c, m);
        }

        [Test]
        public void ChaseGoesAroundTheCornerWithoutTouchingWalls()
        {
            var (gs, c, m) = CryptCorner();
            var map = gs.MapOf(MapDefinition.CryptId);
            Assert.IsFalse(map.LineClear(m.X, m.Z, c.X, c.Z), "o teste precisa de uma quina no caminho");
            bool reached = false;
            for (int i = 0; i < 200 && !reached; i++)
            {
                gs.Tick(0.05f);
                c.Hp = c.MaxHp;
                Assert.IsFalse(map.IsBlocked(m.X, m.Z), $"o monstro entrou na parede em {m.X};{m.Z}");
                reached = MathK.Dist(m.X, m.Z, c.X, c.Z) <= m.Def.AttackRange + 0.05f;
            }
            Assert.IsTrue(reached, $"o monstro travou em {m.X};{m.Z}");
        }

        [Test]
        public void LeashSendsItBackToSpawnAlongThePath()
        {
            var (gs, c, m) = CryptCorner();
            var map = gs.MapOf(MapDefinition.CryptId);
            // herói foge para longe (além do leash), virando a quina
            c.X = 48; c.Z = 60;
            bool home = false, wasReturning = false;
            for (int i = 0; i < 400 && !home; i++)
            {
                gs.Tick(0.05f);
                c.Hp = c.MaxHp;
                if (m.State == MonsterState.Chase && MathK.Dist(m.X, m.Z, m.SpawnX, m.SpawnZ) > Monster.LeashRange * 0.5f) { c.X = 70; c.Z = 60; }
                if (m.State == MonsterState.Return) wasReturning = true;
                Assert.IsFalse(map.IsBlocked(m.X, m.Z), $"o monstro entrou na parede em {m.X};{m.Z}");
                home = wasReturning && m.State == MonsterState.Idle && MathK.Dist(m.X, m.Z, m.SpawnX, m.SpawnZ) < 0.5f;
            }
            Assert.IsTrue(wasReturning, "o leash deveria mandar o monstro voltar");
            Assert.IsTrue(home, $"não voltou para o ponto de origem (parou em {m.X};{m.Z})");
            Assert.AreEqual(m.MaxHp, m.Hp, "ao voltar recupera a vida");
        }

        [Test]
        public void MonstersNeverWalkIntoTheVillage()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Abrigo PAT");
            var c = gs.GetCharacter(1)!;
            var wolf = gs.Monsters.Values.First(x => x.Def == MonsterCatalog.AshWolf && x.MapId == MapDefinition.ValeId);
            c.X = gs.Map.SafeX; c.Z = gs.Map.SafeZ - 12;        // dentro da vila, perto do portão sul
            wolf.X = gs.Map.SafeX; wolf.Z = gs.Map.SafeZ - gs.Map.SafeRadius - 7;
            wolf.State = MonsterState.Chase; wolf.TargetId = c.Id;
            for (int i = 0; i < 200; i++) { gs.Tick(0.05f); Assert.IsFalse(gs.Map.InSafeZone(wolf.X, wolf.Z)); }
        }
    }
}
