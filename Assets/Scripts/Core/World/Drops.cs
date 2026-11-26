using System;
using System.Collections.Generic;
using Kezor.Core.Items;

namespace Kezor.Core.World
{
    /// <summary>Item caído no chão. Fica reservado ao dono (e à party) por alguns segundos.</summary>
    public sealed class GroundItem
    {
        public int Id;
        public Item Item = null!;
        public float X, Z;
        public int MapId = 1;
        public int OwnerId;
        public int OwnerPartyId;
        public double ReservedUntil, ExpiresAt;
    }

    /// <summary>Tabela de drop: chances em "por mil".</summary>
    public static class DropSystem
    {
        public const int ItemChancePerMil = 120;
        public const int RuneChancePerMil = 25;
        public const int PotionChancePerMil = 90;
        public const int AscendedChancePerMil = 10;
        public const int FeatherChancePerMil = 40;
        public const int FeatherMinLevel = 14;
        public const int JewelChancePerMil = 12;

        private static readonly ItemDefinition[] RunePool =
        { ItemDatabase.VortexCrystal, ItemDatabase.VortexCrystal, ItemDatabase.VitalRune, ItemDatabase.RuneOfEclipse, ItemDatabase.RuneOfDawn };

        public static Item RandomRune(IRandom rng) => new Item(RunePool[rng.Next(0, RunePool.Length)]);

        public static List<Item> Roll(MonsterDefinition m, IRandom rng)
        {
            var drops = new List<Item>();
            for (int i = 0; i < m.GuaranteedRunes; i++) drops.Add(RandomRune(rng));
            if (rng.RollPerMil(RuneChancePerMil)) drops.Add(RandomRune(rng));
            if (rng.RollPerMil(PotionChancePerMil))
            {
                var p = m.Level >= 70 && rng.Roll(50) ? (rng.Roll(65) ? ItemDatabase.SupremeHealthPotion : ItemDatabase.GreaterManaPotion)
                      : m.Level >= 20 && rng.Roll(50) ? ItemDatabase.GreaterHealthPotion : (rng.Roll(60) ? ItemDatabase.HealthPotion : ItemDatabase.ManaPotion);
                drops.Add(new Item(p) { Quantity = rng.Next(1, 4) });
            }
            int itemChance = m.Boss ? 1000 : ItemChancePerMil;
            if (rng.RollPerMil(itemChance))
            {
                var pool = new List<ItemDefinition>();
                foreach (var d in ItemDatabase.All) if (d.IsUpgradable && d.DropLevel <= m.Level + 5) pool.Add(d);
                if (pool.Count > 0)
                {
                    var def = pool[rng.Next(0, pool.Count)];
                    int maxLvl = Math.Min(m.Level / 10, 6);
                    var it = new Item(def, rng.Next(0, maxLvl + 1))
                    {
                        Luck = rng.Roll(10),
                        Skill = def.IsWeapon && rng.Roll(30)
                    };
                    if (rng.Roll(25)) it.AdditionalOption = rng.Next(1, 3);
                    if (rng.RollPerMil(m.Boss ? 200 : AscendedChancePerMil))
                    {
                        it.Ascended = def.IsWeapon
                            ? (rng.Roll(50) ? AscendedOption.SurgeChance : (rng.Roll(50) ? AscendedOption.DamagePerLevel : AscendedOption.LifeOnKill))
                            : (rng.Roll(34) ? AscendedOption.DefenseRate : (rng.Roll(50) ? AscendedOption.DamageReduction : AscendedOption.MaxHpPlus));
                    }
                    drops.Add(it);
                }
            }
            // Chefe com drop especial (arma épica ou asas): um sempre cai.
            if (m.SpecialDrops.Length > 0)
            {
                var sd = ItemDatabase.Get(m.SpecialDrops[rng.Next(0, m.SpecialDrops.Length)]);
                if (sd != null) drops.Add(new Item(sd) { Skill = sd.IsWeapon, Luck = rng.Roll(30) });
            }
            // Pena de Brasa (forja de asas): só monstros nível 14+, para não mexer no ritmo do começo.
            if (m.Boss) drops.Add(new Item(ItemDatabase.EmberFeather) { Quantity = 3 });
            else if (m.Level >= FeatherMinLevel && rng.RollPerMil(FeatherChancePerMil)) drops.Add(new Item(ItemDatabase.EmberFeather));
            // Joias: raras (1,2%; chefe 25%), do nível do monstro.
            if (rng.RollPerMil(m.Boss ? 250 : JewelChancePerMil))
            {
                var jewels = new List<ItemDefinition>();
                foreach (var d in ItemDatabase.All) if (d.IsJewel && d.DropLevel <= m.Level + 5) jewels.Add(d);
                if (jewels.Count > 0) drops.Add(new Item(jewels[rng.Next(0, jewels.Count)]));
            }
            return drops;
        }
    }
}
