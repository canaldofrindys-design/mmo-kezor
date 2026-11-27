using System.Linq;
using Kezor.Core;
using Kezor.Core.Net;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Evento AGGRO: o servidor avisa quando um monstro parte para cima, para o cliente tocar o grito de alerta.</summary>
    public class CombatSoundEventTests
    {
        [Test]
        public void AggressiveMonsterAnnouncesAggroOnce()
        {
            var gs = new GameServer(new SystemRandom(7), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999 });
            gs.Handle(1, "CREATE Isca GUA");
            var c = gs.GetCharacter(1)!;
            var wolf = gs.Monsters.Values.First(x => x.Def == MonsterCatalog.AshWolf && x.MapId == MapDefinition.ValeId && !x.IsDead);
            gs.DrainOutbox(1);
            c.X = wolf.X + 3; c.Z = wolf.Z;
            var lines = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 20; i++) { gs.Tick(0.05f); lines.AddRange(gs.DrainOutbox(1) ?? new System.Collections.Generic.List<string>()); }
            Assert.AreEqual(MonsterState.Chase, wolf.State);
            Assert.AreEqual(1, lines.Count(l => l == $"@EV AGGRO;{wolf.Id};{c.Id}"), "um alerta só, quando o lobo começa a perseguir");

            var view = new ClientState();
            foreach (var l in lines) view.Process(l);
            Assert.IsTrue(view.Events.Any(e => e.Type == "AGGRO" && e.I(0) == wolf.Id && e.I(1) == c.Id), "o cliente recebe o evento");
        }
    }
}
