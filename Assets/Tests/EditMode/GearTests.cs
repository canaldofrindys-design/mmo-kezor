using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.Server;
using Kezor.Core.World;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>Equipamento visível, asas, montarias, mascotes e armas épicas.</summary>
    public class GearTests
    {
        private static GameServer NewServer(int seed = 42) =>
            new GameServer(new SystemRandom(seed), options: new ServerOptions { AllowGm = true, EventFirstDelay = 99999 });

        private static void Run(GameServer gs, float seconds, float dt = 0.05f)
        {
            for (float t = 0; t < seconds; t += dt) gs.Tick(dt);
        }

        private static Item Equip(Character c, ItemDefinition def, int level = 0)
        {
            var it = new Item(def, level);
            Assert.IsTrue(c.Inventory.TryAdd(it, out var slot));
            Assert.IsTrue(c.Equip(slot, out var err), err);
            return it;
        }

        private static Character Hero(GameServer gs, string cls = "GUA", int level = 200)
        {
            gs.Handle(1, $"CREATE Hero {cls}");
            gs.Handle(1, $"GM LEVEL {level}");
            var c = gs.GetCharacter(1)!;
            gs.Handle(1, $"ADD STR {c.FreePoints / 2}");
            gs.Handle(1, $"ADD AGI {c.FreePoints / 2}");
            return c;
        }

        [Test]
        public void AppearanceRoundTripsThroughSnapshot()
        {
            var gs = NewServer();
            var c = Hero(gs);
            Equip(c, ItemDatabase.StormBlade, 11);
            Equip(c, ItemDatabase.WardenShield, 7);
            Equip(c, ItemDatabase.RunePlateHelm, 9).Ascended = AscendedOption.MaxHpPlus;
            Equip(c, ItemDatabase.AshWings, 3);
            Equip(c, ItemDatabase.EmberWolf);
            Equip(c, ItemDatabase.AshImp);
            StringAssert.StartsWith("OK", gs.Handle(1, "MOUNT"));

            var code = Appearance.Encode(c);
            StringAssert.DoesNotContain(";", code);
            StringAssert.DoesNotContain("|", code);
            var back = Appearance.Parse(code);
            Assert.AreEqual(ItemDatabase.StormBlade.Id, back[EquipSlot.RightHand].Id);
            Assert.AreEqual(11, back[EquipSlot.RightHand].Level);
            Assert.IsTrue(back[EquipSlot.Helm].Ascended);
            Assert.AreEqual(ItemDatabase.AshWings.Id, back[EquipSlot.Wings].Id);
            Assert.AreEqual(ItemDatabase.AshImp.Id, back[EquipSlot.Pet].Id);
            Assert.IsTrue(back.Mounted);
            Assert.IsTrue(back[EquipSlot.Gloves].Empty);
            Assert.AreEqual(code, back.Encode());

            // Outro jogador vê a aparência pelo @S
            gs.Handle(2, "CREATE Watcher ARC");
            var watcher = gs.GetCharacter(2)!; watcher.X = c.X + 1; watcher.Z = c.Z;
            var view = new ClientState();
            Run(gs, 0.2f);
            foreach (var line in gs.DrainOutbox(2) ?? new System.Collections.Generic.List<string>()) view.Process(line);
            Assert.IsTrue(view.Entities.TryGetValue(c.Id, out var e));
            Assert.AreEqual(ItemDatabase.WardenShield.Id, e!.Appearance[EquipSlot.LeftHand].Id);
            Assert.AreEqual(7, e.Appearance[EquipSlot.LeftHand].Level);
            Assert.IsTrue(e.Appearance.Mounted);

            // Lixo ou texto antigo não quebra o cliente
            Assert.IsTrue(Appearance.Parse("").Pieces.All(p => p.Empty));
            Assert.IsTrue(Appearance.Parse("99999.1.0,x/zz")[EquipSlot.RightHand].Empty);
        }

        [Test]
        public void WingsAddDamageAndAbsorbAndScaleWithRunes()
        {
            var gs = NewServer();
            var c = Hero(gs);
            var before = c.GetCombatStats();
            Assert.AreEqual(0, before.DamageBonusPercent);
            var w = Equip(c, ItemDatabase.DuskWings, 4);
            var s = c.GetCombatStats();
            Assert.AreEqual(ItemDatabase.DuskWings.WingDamagePercent + 4, s.DamageBonusPercent);
            Assert.AreEqual(before.DamageReductionPercent + ItemDatabase.DuskWings.WingAbsorbPercent + 2, s.DamageReductionPercent);
            Assert.Greater(s.Defense, before.Defense);

            // Dano maior com asas (mesma semente → mesmo rolamento)
            var dummy = new Monster(9999, MonsterCatalog.BasaltGolem, 0, 0);
            var h1 = CombatResolver.Resolve(c, dummy, SkillCatalog.BasicAttack, new FixedRandom());
            dummy.Hp = dummy.MaxHp;
            Assert.IsTrue(c.Unequip(EquipSlot.Wings, out _));
            var h2 = CombatResolver.Resolve(c, dummy, SkillCatalog.BasicAttack, new FixedRandom());
            Assert.Greater(h1.Damage, h2.Damage);

            // Requisito de nível
            var low = new Character(77, "Novato", CharacterClass.Warden);
            low.Inventory.TryAdd(new Item(ItemDatabase.BrasalumeWings), out var slot);
            Assert.IsFalse(low.Equip(slot, out var err));
            StringAssert.Contains("nível", err);
        }

        [Test]
        public void WingsAreForgedFromFeathers()
        {
            var forge = new ArcaneForge(new FixedRandom(0));   // rolagem 0 → sempre sucesso
            var sword = new Item(ItemDatabase.StormBlade, 9);
            var feathers = new Item(ItemDatabase.EmberFeather) { Quantity = 8 };
            var dawn = new Item(ItemDatabase.RuneOfDawn);
            var mix = new System.Collections.Generic.List<Item> { sword, feathers, dawn };
            var prev = forge.Preview(mix);
            Assert.IsTrue(prev.Success, prev.Message);
            Assert.AreEqual(60, prev.RatePercent);     // 50% + 2 níveis acima de +7
            long gold = 2_000_000;
            var r = forge.Mix(mix, ref gold);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(1_000_000, gold);
            Assert.AreEqual(ItemDatabase.AshWings, r.Outputs.Single().Def);
            Assert.AreEqual(3, feathers.Quantity);      // gastou só 5 penas
            Assert.IsFalse(r.Consumed.Contains(feathers));
            Assert.IsTrue(r.Consumed.Contains(sword) && r.Consumed.Contains(dawn));

            // Penas de menos ou equipamento abaixo de +7: não casa
            Assert.IsFalse(forge.Preview(new[] { new Item(ItemDatabase.StormBlade, 9), new Item(ItemDatabase.EmberFeather) { Quantity = 4 }, dawn }).Success);
            Assert.IsFalse(forge.Preview(new[] { new Item(ItemDatabase.StormBlade, 6), new Item(ItemDatabase.EmberFeather) { Quantity = 5 }, dawn }).Success);

            // Tier 2: falha devolve as asas
            var failing = new ArcaneForge(new FixedRandom(99));
            var ash = new Item(ItemDatabase.AshWings, 2);
            var t2 = new System.Collections.Generic.List<Item> { ash, new Item(ItemDatabase.EmberFeather) { Quantity = 10 }, new Item(ItemDatabase.RuneOfEclipse), new Item(ItemDatabase.VortexCrystal) };
            Assert.AreEqual(46, failing.Preview(t2).RatePercent);
            gold = 10_000_000;
            var f = failing.Mix(t2, ref gold);
            Assert.IsFalse(f.Success);
            Assert.IsFalse(f.Consumed.Contains(ash));
            Assert.AreEqual(3, f.Consumed.Count);

            // Tier 3 precisa das asas do tier 2 em +7
            var dusk = new Item(ItemDatabase.DuskWings, 7);
            var t3 = new System.Collections.Generic.List<Item> { dusk, new Item(ItemDatabase.EmberFeather) { Quantity = 20 }, new Item(ItemDatabase.VortexCrystal), new Item(ItemDatabase.VortexCrystal), new Item(ItemDatabase.RuneOfDawn), new Item(ItemDatabase.RuneOfEclipse) };
            Assert.AreEqual("Asas: Asas de Brasalume", forge.Preview(t3).RecipeName);
        }

        [Test]
        public void MountRaisesSpeedOnlyWhileMountedAndServerValidates()
        {
            var gs = NewServer();
            var c = Hero(gs, "PAT", 60);
            StringAssert.StartsWith("ERR", gs.Handle(1, "MOUNT"));       // sem montaria
            Equip(c, ItemDatabase.EmberWolf);
            float walk = c.MoveSpeed;
            StringAssert.StartsWith("OK", gs.Handle(1, "MOUNT"));
            Assert.IsTrue(c.Mounted);
            Assert.AreEqual(walk * 1.6f, c.MoveSpeed, 0.001f);

            // O servidor anda exatamente velocidade × tempo, mesmo pedindo um destino longe
            c.X = gs.Map.SafeX + 4; c.Z = gs.Map.SafeZ;
            float x0 = c.X;
            gs.Handle(1, $"MOVE {Protocol.F(x0 + 40)} {Protocol.F(c.Z)}");
            Run(gs, 1f);
            Assert.AreEqual(x0 + walk * 1.6f, c.X, 0.4f);

            // Atacar desmonta; tirar a montaria também
            var slime = gs.Monsters.Values.First(m => !m.IsDead);
            gs.Handle(1, $"ATTACK {slime.Id}");
            Assert.IsFalse(c.Mounted);
            gs.Handle(1, "MOUNT");
            Assert.IsTrue(c.Unequip(EquipSlot.Mount, out _));
            Assert.IsFalse(c.Mounted);
            Assert.AreEqual(walk, c.MoveSpeed, 0.001f);
        }

        [Test]
        public void CollectorPetPicksUpOwnItemsAndGuardianAddsDefense()
        {
            var gs = NewServer();
            var c = Hero(gs);
            var baseDef = c.GetCombatStats();
            Equip(c, ItemDatabase.GuardianWisp);
            var g = c.GetCombatStats();
            Assert.AreEqual(baseDef.DefenseBase + Item.GuardianPetDefense, g.DefenseBase);
            Assert.AreEqual(g.DefenseBase / 2, g.Defense, "defesa final do MU: metade");
            Assert.AreEqual(baseDef.DamageReductionPercent + Item.GuardianPetReduction, g.DamageReductionPercent);
            Assert.IsTrue(c.Unequip(EquipSlot.Pet, out _));

            c.X = MapDefinition.ValeCenter - 60; c.Z = MapDefinition.ValeCenter;                  // fora da vila, perto dos lodos (sem mascote ainda)
            for (int i = 0; i < 300 && !gs.GroundItems.Values.Any(x => x.OwnerId == c.Id); i++)
            {
                var m = gs.Monsters.Values.Where(x => x.Def == MonsterCatalog.MossSlime && !x.IsDead).OrderBy(x => MathK.Dist(c.X, c.Z, x.X, x.Z)).First();
                gs.Handle(1, $"ATTACK {m.Id}");
                Run(gs, 1f);
                c.Hp = c.MaxHp;
                if (gs.GroundItems.Values.Any(x => x.OwnerId == c.Id && MathK.Dist(c.X, c.Z, x.X, x.Z) <= GameServer.PetCollectRange)) break;
            }
            gs.Handle(1, "STOP");
            var drop = gs.GroundItems.Values.FirstOrDefault(x => x.OwnerId == c.Id && MathK.Dist(c.X, c.Z, x.X, x.Z) <= GameServer.PetCollectRange);
            Assert.IsNotNull(drop, "nenhum drop perto do herói");
            Equip(c, ItemDatabase.AshImp);                       // o diabrete chega e recolhe sem o herói andar
            float hx = c.X, hz = c.Z;
            Run(gs, 0.6f);
            Assert.AreEqual(hx, c.X, 0.01f); Assert.AreEqual(hz, c.Z, 0.01f);
            Assert.IsFalse(gs.GroundItems.ContainsKey(drop!.Id), "o diabrete deveria ter recolhido");
            Assert.IsTrue(c.Inventory.Contains(drop.Item) || Enumerable.Range(0, Character.InventorySize).Any(i => c.Inventory.Get(i)?.Def == drop.Item.Def));
        }

        [Test]
        public void EpicWeaponsExistForEveryClassAndDropFromStrongMonsters()
        {
            foreach (var mask in new[] { ClassMask.Warden, ClassMask.Arcanist, ClassMask.Ranger })
                Assert.IsTrue(ItemDatabase.All.Any(d => d.Epic && d.IsWeapon && (d.AllowedClasses & mask) != 0), mask.ToString());
            var epics = ItemDatabase.All.Where(d => d.Epic).ToList();
            Assert.IsTrue(epics.All(d => d.DropLevel <= MonsterCatalog.BasaltGolem.Level + 5));
            Assert.IsTrue(epics.All(d => d.DropLevel > MonsterCatalog.Wanderer.Level + 5));   // não cai cedo demais
            // Mascotes e montarias não aceitam runas; asas aceitam
            Assert.IsFalse(ItemDatabase.EmberWolf.IsUpgradable);
            Assert.IsFalse(ItemDatabase.AshImp.IsUpgradable);
            Assert.IsTrue(ItemDatabase.AshWings.IsUpgradable);
        }

        /// <summary>RNG fixo: Next devolve sempre min + valor (limitado ao intervalo).</summary>
        private sealed class FixedRandom : IRandom
        {
            private readonly int _v;
            public FixedRandom(int v = 50) { _v = v; }
            public int Next(int min, int max) => System.Math.Min(System.Math.Max(min, min + _v), max - 1);
        }
    }
}
