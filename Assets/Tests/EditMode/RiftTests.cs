using System.Linq;
using Kezor.Core;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Escarpa da Geada Rubra (mapa 4) e o evento da Fenda: ondas, Colosso e prêmio por dano.</summary>
    internal static class RiftTestExt
    {
        public static int Runes(this Kezor.Core.Characters.Character c) =>
            Enumerable.Range(0, c.Inventory.Capacity).Select(c.Inventory.Get).Where(it => it != null && it.Def.Kind == ItemKind.Rune).Sum(it => it!.Quantity);
    }

    public class RiftTests
    {
        private static GameServer NewServer(float firstRift = 99999) =>
            new GameServer(new SystemRandom(11), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999, RiftFirstDelay = firstRift });

        private static bool IsRift(Monster m) => m.MapId == MapDefinition.EscarpId &&
            (m.Def == MonsterCatalog.RiftShard || m.Def == MonsterCatalog.CrimsonFrostColossus);

        /// <summary>Encosta no monstro e bate até ele cair (os heróis não morrem durante o teste).</summary>
        private static void KillWith(GameServer gs, int session, Monster m)
        {
            var c = gs.GetCharacter(session)!;
            string last = "";
            for (int i = 0; i < 400 && !m.IsDead && gs.Monsters.ContainsKey(m.Id); i++)
            {
                foreach (var o in new[] { gs.GetCharacter(1), gs.GetCharacter(2) }) if (o != null) { o.Hp = o.MaxHp; o.Mp = o.MaxMp; }
                c.X = m.X + 1; c.Z = m.Z;
                if (m.Hp > 50) m.Hp = 50;
                last = gs.Handle(session, $"ATTACK {m.Id}");
                gs.Tick(0.1f);
            }
            Assert.IsTrue(m.IsDead || !gs.Monsters.ContainsKey(m.Id), $"{m.Name} não caiu ({c.Name}: {last}, hp {m.Hp}) ");
        }

        [Test]
        public void EscarpmentNeedsLevel80AndHasItsMonsters()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Nevada GUA");
            var c = gs.GetCharacter(1)!;
            c.Gold = 10_000_000;
            gs.Handle(1, "GM LEVEL 60");
            StringAssert.StartsWith("ERR", gs.Handle(1, "WARP ESCARPA"));
            gs.Handle(1, "GM LEVEL 85");
            StringAssert.StartsWith("OK", gs.Handle(1, "WARP ESCARPA"));
            Assert.AreEqual(MapDefinition.EscarpId, c.MapId);
            var here = gs.Monsters.Values.Where(m => m.MapId == MapDefinition.EscarpId).Select(m => m.Def).Distinct().ToList();
            CollectionAssert.IsSupersetOf(here, new[] { MonsterCatalog.FrostLynx, MonsterCatalog.RimeWarden, MonsterCatalog.FrozenRevenant, MonsterCatalog.RimeSerpent });
            Assert.IsFalse(gs.Monsters.Values.Any(IsRift), "sem evento, nada da fenda no mapa");
        }

        [Test]
        public void ColossusDropsTheNewWeaponsAndHighPotionsAppear()
        {
            var rng = new SystemRandom(2);
            int weapons = 0;
            for (int i = 0; i < 60; i++) weapons += DropSystem.Roll(MonsterCatalog.CrimsonFrostColossus, rng).Count(d => d.Def.Id >= 70 && d.Def.Id <= 76 || d.Def.Id == 607);
            Assert.Greater(weapons, 0, "o Colosso solta as armas da Geada Rubra");
            bool supreme = false;
            for (int i = 0; i < 3000 && !supreme; i++) supreme = DropSystem.Roll(MonsterCatalog.FrozenRevenant, rng).Any(d => d.Def == ItemDatabase.SupremeHealthPotion);
            Assert.IsTrue(supreme, "monstros de nível alto soltam a poção suprema");
            Assert.IsTrue(Enumerable.Range(70, 7).All(id => ItemDatabase.Get(id) != null), "uma arma nova por classe");
        }

        [Test]
        public void RiftOpensOnTimerWithWavesThenColossusAndRewardsTopDamage()
        {
            var gs = NewServer(firstRift: 40);
            gs.Handle(1, "CREATE Rubra GUA");
            gs.Handle(2, "CREATE Ajuda ARC");
            var a = gs.GetCharacter(1)!; var b = gs.GetCharacter(2)!;
            a.Gold = b.Gold = 10_000_000;
            gs.Handle(1, "GM LEVEL 300"); gs.Handle(2, "GM LEVEL 300");
            StringAssert.StartsWith("OK", gs.Handle(1, "WARP ESCARPA")); StringAssert.StartsWith("OK", gs.Handle(2, "WARP ESCARPA"));
            foreach (var m in gs.Monsters.Values) if (m.MapId == MapDefinition.EscarpId) m.Hp = 0;   // sem distração
            var sa = gs.GetSession(1)!;

            for (int i = 0; i < 300 && !gs.RiftActive; i++) { a.Hp = a.MaxHp; b.Hp = b.MaxHp; gs.Tick(0.2f); }
            Assert.IsTrue(gs.RiftActive, "a fenda abre sozinha pelo relógio");
            Assert.IsTrue(sa.Outbox.Any(l => l.Contains("se abre em 30 segundos")), "aviso antes");
            Assert.IsTrue(sa.Outbox.Any(l => l == "@EV EVENT;1;" + GameServer.RiftName));
            Assert.AreEqual(GameServer.RiftWaveSize, gs.Monsters.Values.Count(m => IsRift(m) && !m.IsDead));

            // onda 1 e 2: Rubra mata quase todos, Ajuda só o último de cada onda
            for (int wave = 1; wave <= 2; wave++)
            {
                Assert.AreEqual(wave, gs.RiftWave);
                var shards = gs.Monsters.Values.Where(m => IsRift(m) && !m.IsDead).ToList();
                for (int k = 0; k < shards.Count; k++) KillWith(gs, k == shards.Count - 1 ? 2 : 1, shards[k]);
                gs.Tick(0.1f);
            }
            Assert.AreEqual(3, gs.RiftWave);
            var colossus = gs.Monsters.Values.Single(m => IsRift(m) && !m.IsDead);
            Assert.AreEqual(MonsterCatalog.CrimsonFrostColossus, colossus.Def);
            Assert.Greater(gs.RiftContribution(a.Id), gs.RiftContribution(b.Id));
            Assert.Greater(gs.RiftContribution(b.Id), 0);

            int runesA = a.Runes(), runesB = b.Runes();
            long goldA = a.Gold, goldB = b.Gold;
            KillWith(gs, 2, colossus);   // o último golpe é da Ajuda, mas o 1º lugar continua da Rubra
            gs.Tick(0.1f);
            Assert.IsFalse(gs.RiftActive, "o Colosso caído fecha a fenda");
            Assert.AreEqual(3, a.Runes() - runesA, "1º lugar: 3 runas");
            Assert.AreEqual(2, b.Runes() - runesB, "2º lugar: 2 runas");
            Assert.GreaterOrEqual(a.Gold - goldA, 3_000_000);
            Assert.IsTrue(sa.Outbox.Any(l => l == "@EV EVENT;0;" + GameServer.RiftName));
            Assert.IsFalse(gs.Monsters.Values.Any(m => IsRift(m) && !m.IsDead));
        }

        [Test]
        public void GmCanOpenRiftAndItClosesAloneWhenTimeRunsOut()
        {
            var gs = NewServer();
            gs.Handle(1, "CREATE Mestre GUA");
            StringAssert.StartsWith("OK", gs.Handle(1, "GM RIFT"));
            Assert.IsTrue(gs.RiftActive);
            StringAssert.StartsWith("ERR", gs.Handle(1, "GM RIFT"));
            for (int i = 0; i < 1400 && gs.RiftActive; i++) gs.Tick(0.5f);
            Assert.IsFalse(gs.RiftActive, "fecha sozinha depois do tempo limite");
            Assert.IsFalse(gs.Monsters.Values.Any(IsRift), "os monstros da fenda somem");
        }
    }
}
