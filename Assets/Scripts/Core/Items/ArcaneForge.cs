using System;
using System.Collections.Generic;
using System.Linq;

namespace Kezor.Core.Items
{
    public enum ForgeFailurePolicy { DestroyItem, ResetToZero }

    public sealed class ForgeResult
    {
        public bool Success;
        public string Message = "";
        public int RatePercent;
        public long GoldCost;
        public string RecipeName = "";
        /// <summary>Itens que saem da máquina (vão para o inventário).</summary>
        public List<Item> Outputs = new List<Item>();
        /// <summary>Itens que foram consumidos (removidos do inventário).</summary>
        public List<Item> Consumed = new List<Item>();
        public static ForgeResult Fail(string m) => new ForgeResult { Success = false, Message = m };
    }

    public interface IForgeRecipe
    {
        string Name { get; }
        bool Matches(IReadOnlyList<Item> items);
        int SuccessRate(IReadOnlyList<Item> items);
        long GoldCost(IReadOnlyList<Item> items);
        void Execute(IReadOnlyList<Item> items, bool success, ForgeResult result);
    }

    /// <summary>
    /// Refino +9 -> +15. Receita para alvo +N (N=10..15): item em +(N-1), (N-9) Runas da Aurora, (N-9) Runas do Eclipse, 1 Cristal do Vórtice.
    /// </summary>
    public sealed class SuperiorRefineRecipe : IForgeRecipe
    {
        public static readonly Dictionary<int, int> BaseRates = new Dictionary<int, int>
        { {10, 60}, {11, 55}, {12, 50}, {13, 45}, {14, 40}, {15, 35} };
        public const int LuckBonus = 25;   // MU: Sorte +25%, máx. 75%
        public const int MaxRate = 75;
        public ForgeFailurePolicy FailurePolicy = ForgeFailurePolicy.DestroyItem;

        public string Name => "Refino Superior (+10~+15)";

        private static Item? Target(IReadOnlyList<Item> items)
        {
            var eq = items.Where(i => i.Def.IsUpgradable).ToList();
            return eq.Count == 1 ? eq[0] : null;
        }
        public static int RequiredRunes(int targetLevel) => targetLevel - 9;
        public static long GoldFor(int targetLevel) => 2_000_000L * (targetLevel - 9);

        public bool Matches(IReadOnlyList<Item> items)
        {
            var t = Target(items);
            if (t == null || t.Level < 9 || t.Level >= Item.MaxLevel) return false;
            int n = RequiredRunes(t.Level + 1);
            int dawn = items.Count(i => i.Def.Rune == RuneType.Dawn);
            int eclipse = items.Count(i => i.Def.Rune == RuneType.Eclipse);
            int vortex = items.Count(i => i.Def.Rune == RuneType.Vortex);
            return dawn == n && eclipse == n && vortex == 1 && items.Count == 1 + 2 * n + 1;
        }

        public int SuccessRate(IReadOnlyList<Item> items)
        {
            var t = Target(items)!;
            int r = BaseRates[t.Level + 1] + (t.Luck ? LuckBonus : 0);
            return Math.Min(r, MaxRate);
        }

        public long GoldCost(IReadOnlyList<Item> items) => GoldFor(Target(items)!.Level + 1);

        public void Execute(IReadOnlyList<Item> items, bool success, ForgeResult result)
        {
            var t = Target(items)!;
            result.Consumed.AddRange(items.Where(i => i != t));
            if (success)
            {
                t.Level++;
                t.Durability = t.MaxDurability;
                result.Outputs.Add(t);
                result.Message = $"sucesso! {t.DisplayName}";
            }
            else if (FailurePolicy == ForgeFailurePolicy.ResetToZero)
            {
                t.Level = 0;
                t.Durability = Math.Min(t.Durability, t.MaxDurability);
                result.Outputs.Add(t);
                result.Message = "falhou: item voltou a +0";
            }
            else
            {
                result.Consumed.Add(t);
                result.Message = "falhou: o item foi destruído";
            }
        }
    }

    /// <summary>
    /// Arma do Vórtice: 1+ equipamentos entre +4 e +8 (+9 em diante é reservado ao refino) + 1 Cristal do Vórtice (+ Aurora/Eclipse opcionais p/ aumentar a taxa).
    /// Taxa = soma por item: nível*4 + opção*3 + Sorte 5 ; Aurora +8, Eclipse +5 cada ; máx 80%.
    /// </summary>
    public sealed class VortexWeaponRecipe : IForgeRecipe
    {
        public const int MaxRate = 80;
        public string Name => "Arma do Vórtice";

        public bool Matches(IReadOnlyList<Item> items)
        {
            if (items.Count(i => i.Def.Rune == RuneType.Vortex) != 1) return false;
            var eq = items.Where(i => i.Def.IsUpgradable).ToList();
            if (eq.Count < 1 || eq.Any(i => i.Level < 4 || i.Level > 8)) return false;
            return items.All(i => i.Def.IsUpgradable || i.Def.Rune == RuneType.Vortex || i.Def.Rune == RuneType.Dawn || i.Def.Rune == RuneType.Eclipse);
        }

        public int SuccessRate(IReadOnlyList<Item> items)
        {
            int r = 0;
            foreach (var i in items)
            {
                if (i.Def.IsUpgradable) r += i.Level * 4 + i.AdditionalOption * 3 + (i.Luck ? 5 : 0);
                else if (i.Def.Rune == RuneType.Dawn) r += 8;
                else if (i.Def.Rune == RuneType.Eclipse) r += 5;
            }
            return Math.Min(r, MaxRate);
        }

        public long GoldCost(IReadOnlyList<Item> items) => 10_000L * SuccessRate(items);

        public void Execute(IReadOnlyList<Item> items, bool success, ForgeResult result)
        {
            result.Consumed.AddRange(items);
            if (success)
            {
                var w = new Item(ItemDatabase.VortexBlade) { Skill = true };
                result.Outputs.Add(w);
                result.Message = $"sucesso! criou {w.DisplayName}";
            }
            else result.Message = "falhou: materiais perdidos";
        }
    }

    /// <summary>
    /// Asas na Forja Arcana (material: Pena de Brasa).
    /// Tier 1 (Asas de Cinza): 1 arma/armadura +7 ou mais + 5 Penas + 1 Runa da Aurora. Taxa 50% + 5% por nível acima de +7 (+10% Sorte), máx 75%. O equipamento é consumido.
    /// Tier 2 (Asas do Crepúsculo): Asas de Cinza + 10 Penas + 1 Runa do Eclipse + 1 Cristal do Vórtice. Taxa 40% + 3% por nível das asas, máx 70%.
    /// Tier 3 (Asas de Brasalume): Asas do Crepúsculo +7 ou mais + 20 Penas + 2 Cristais do Vórtice + 1 Aurora + 1 Eclipse. Taxa 30% + 4% por nível acima de +7, máx 60%.
    /// Nos tiers 2 e 3, se falhar, as asas de entrada voltam intactas (só os materiais se perdem).
    /// </summary>
    public sealed class WingsRecipe : IForgeRecipe
    {
        public readonly int Tier;
        public WingsRecipe(int tier) { Tier = Math.Clamp(tier, 1, 3); }

        public static readonly int[] Feathers = { 0, 5, 10, 20 };
        public static readonly long[] Gold = { 0, 1_000_000, 5_000_000, 20_000_000 };
        public static ItemDefinition Output(int tier) => tier switch { 1 => ItemDatabase.AshWings, 2 => ItemDatabase.DuskWings, _ => ItemDatabase.BrasalumeWings };

        public string Name => $"Asas: {Output(Tier).Name}";

        private static int Count(IReadOnlyList<Item> items, RuneType r) => items.Count(i => i.Def.Rune == r);
        private static int FeatherQty(IReadOnlyList<Item> items) => items.Where(i => i.Def == ItemDatabase.EmberFeather).Sum(i => i.Quantity);

        /// <summary>Item principal: equipamento (tier 1) ou as asas do tier anterior (tiers 2 e 3).</summary>
        private Item? Main(IReadOnlyList<Item> items)
        {
            var eq = items.Where(i => i.Def.IsUpgradable).ToList();
            if (eq.Count != 1) return null;
            var m = eq[0];
            return Tier switch
            {
                1 => !m.Def.IsWings && m.Level >= 7 ? m : null,
                2 => m.Def == ItemDatabase.AshWings ? m : null,
                _ => m.Def == ItemDatabase.DuskWings && m.Level >= 7 ? m : null,
            };
        }

        public bool Matches(IReadOnlyList<Item> items)
        {
            if (Main(items) == null || FeatherQty(items) < Feathers[Tier]) return false;
            int dawn = Count(items, RuneType.Dawn), eclipse = Count(items, RuneType.Eclipse), vortex = Count(items, RuneType.Vortex);
            int feathersStacks = items.Count(i => i.Def == ItemDatabase.EmberFeather);
            int expected = Tier switch { 1 => dawn == 1 && eclipse == 0 && vortex == 0 ? 2 : -1,
                                         2 => dawn == 0 && eclipse == 1 && vortex == 1 ? 3 : -1,
                                         _ => dawn == 1 && eclipse == 1 && vortex == 2 ? 5 : -1 };
            return expected > 0 && items.Count == expected + feathersStacks;
        }

        public int SuccessRate(IReadOnlyList<Item> items)
        {
            var m = Main(items)!;
            return Tier switch
            {
                1 => Math.Min(75, 50 + (m.Level - 7) * 5 + (m.Luck ? 10 : 0)),
                2 => Math.Min(70, 40 + m.Level * 3),
                _ => Math.Min(60, 30 + (m.Level - 7) * 4),
            };
        }

        public long GoldCost(IReadOnlyList<Item> items) => Gold[Tier];

        public void Execute(IReadOnlyList<Item> items, bool success, ForgeResult result)
        {
            var main = Main(items)!;
            // Penas: gasta só a quantidade da receita (o resto da pilha fica no inventário).
            int need = Feathers[Tier];
            foreach (var f in items.Where(i => i.Def == ItemDatabase.EmberFeather))
            {
                int take = Math.Min(need, f.Quantity);
                f.Quantity -= take; need -= take;
                if (f.Quantity <= 0) result.Consumed.Add(f);
            }
            result.Consumed.AddRange(items.Where(i => i.Def.IsRune));
            if (success)
            {
                result.Consumed.Add(main);
                var w = new Item(Output(Tier));
                result.Outputs.Add(w);
                result.Message = $"sucesso! criou {w.DisplayName}";
            }
            else if (Tier == 1)
            {
                result.Consumed.Add(main);
                result.Message = "falhou: materiais e equipamento perdidos";
            }
            else result.Message = "falhou: materiais perdidos, as asas voltaram intactas";
        }
    }

    public sealed class ArcaneForge
    {
        private readonly List<IForgeRecipe> _recipes;
        private readonly IRandom _rng;

        public ArcaneForge(IRandom rng, IEnumerable<IForgeRecipe>? recipes = null)
        {
            _rng = rng;
            _recipes = recipes?.ToList() ?? new List<IForgeRecipe> { new SuperiorRefineRecipe(), new VortexWeaponRecipe(), new WingsRecipe(1), new WingsRecipe(2), new WingsRecipe(3) };
        }

        public IForgeRecipe? FindRecipe(IReadOnlyList<Item> items) => _recipes.FirstOrDefault(r => r.Matches(items));

        /// <summary>Pré-visualização (taxa e custo) sem consumir nada.</summary>
        public ForgeResult Preview(IReadOnlyList<Item> items)
        {
            var r = FindRecipe(items);
            if (r == null) return ForgeResult.Fail("combinação inválida");
            return new ForgeResult { Success = true, RecipeName = r.Name, RatePercent = r.SuccessRate(items), GoldCost = r.GoldCost(items), Message = "ok" };
        }

        /// <summary>Executa a mistura. Cobra o ouro; não altera nada se ouro insuficiente.</summary>
        public ForgeResult Mix(IReadOnlyList<Item> items, ref long gold)
        {
            if (items.Count == 0 || items.Distinct().Count() != items.Count) return ForgeResult.Fail("itens inválidos");
            var r = FindRecipe(items);
            if (r == null) return ForgeResult.Fail("combinação inválida");
            long cost = r.GoldCost(items);
            if (gold < cost) return ForgeResult.Fail($"ouro insuficiente ({cost})");
            gold -= cost;
            int rate = r.SuccessRate(items);
            bool ok = _rng.Roll(rate);
            var res = new ForgeResult { Success = ok, RatePercent = rate, GoldCost = cost, RecipeName = r.Name };
            r.Execute(items, ok, res);
            return res;
        }
    }
}
