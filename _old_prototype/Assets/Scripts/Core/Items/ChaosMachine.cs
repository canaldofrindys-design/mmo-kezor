using System;
using System.Collections.Generic;
using System.Linq;

namespace MuProto.Core.Items
{
    public enum ChaosFailurePolicy { DestroyItem, ResetToZero }

    public sealed class ChaosResult
    {
        public bool Success;
        public string Message = "";
        public int RatePercent;
        public long ZenCost;
        public string RecipeName = "";
        /// <summary>Itens que saem da máquina (vão para o inventário).</summary>
        public List<Item> Outputs = new List<Item>();
        /// <summary>Itens que foram consumidos (removidos do inventário).</summary>
        public List<Item> Consumed = new List<Item>();
        public static ChaosResult Fail(string m) => new ChaosResult { Success = false, Message = m };
    }

    public interface IChaosRecipe
    {
        string Name { get; }
        bool Matches(IReadOnlyList<Item> items);
        int SuccessRate(IReadOnlyList<Item> items);
        long ZenCost(IReadOnlyList<Item> items);
        void Execute(IReadOnlyList<Item> items, bool success, ChaosResult result);
    }

    /// <summary>
    /// Refino +9 -> +15. Receita para alvo +N (N=10..15): item em +(N-1), (N-9) Bless, (N-9) Soul, 1 Chaos.
    /// </summary>
    public sealed class ItemUpgradeRecipe : IChaosRecipe
    {
        public static readonly Dictionary<int, int> BaseRates = new Dictionary<int, int>
        { {10, 60}, {11, 55}, {12, 50}, {13, 45}, {14, 40}, {15, 35} };
        public const int LuckBonus = 20;
        public const int MaxRate = 75;
        public ChaosFailurePolicy FailurePolicy = ChaosFailurePolicy.DestroyItem;

        public string Name => "Refino de Item (+10~+15)";

        private static Item? Target(IReadOnlyList<Item> items)
        {
            var eq = items.Where(i => i.Def.IsUpgradable).ToList();
            return eq.Count == 1 ? eq[0] : null;
        }
        public static int RequiredJewels(int targetLevel) => targetLevel - 9;
        public static long ZenFor(int targetLevel) => 2_000_000L * (targetLevel - 9);

        public bool Matches(IReadOnlyList<Item> items)
        {
            var t = Target(items);
            if (t == null || t.Level < 9 || t.Level >= Item.MaxLevel) return false;
            int n = RequiredJewels(t.Level + 1);
            int bless = items.Count(i => i.Def.Jewel == JewelType.Bless);
            int soul = items.Count(i => i.Def.Jewel == JewelType.Soul);
            int chaos = items.Count(i => i.Def.Jewel == JewelType.Chaos);
            return bless == n && soul == n && chaos == 1 && items.Count == 1 + 2 * n + 1;
        }

        public int SuccessRate(IReadOnlyList<Item> items)
        {
            var t = Target(items)!;
            int r = BaseRates[t.Level + 1] + (t.Luck ? LuckBonus : 0);
            return Math.Min(r, MaxRate);
        }

        public long ZenCost(IReadOnlyList<Item> items) => ZenFor(Target(items)!.Level + 1);

        public void Execute(IReadOnlyList<Item> items, bool success, ChaosResult result)
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
            else if (FailurePolicy == ChaosFailurePolicy.ResetToZero)
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
    /// Arma do Caos: 1+ itens equipáveis entre +4 e +8 (+9 em diante é reservado ao refino, evitando consumo acidental) (com qualquer opção) + 1 Jewel of Chaos (+ Bless/Soul opcionais p/ aumentar taxa).
    /// Taxa = soma por item: nível*4 + opção*3 + luck 5 ; Bless +8, Soul +5 cada ; máx 80%.
    /// </summary>
    public sealed class ChaosWeaponRecipe : IChaosRecipe
    {
        public const int MaxRate = 80;
        public string Name => "Arma do Caos";

        public bool Matches(IReadOnlyList<Item> items)
        {
            if (items.Count(i => i.Def.Jewel == JewelType.Chaos) != 1) return false;
            var eq = items.Where(i => i.Def.IsUpgradable).ToList();
            if (eq.Count < 1 || eq.Any(i => i.Level < 4 || i.Level > 8)) return false;
            return items.All(i => i.Def.IsUpgradable || i.Def.Jewel == JewelType.Chaos || i.Def.Jewel == JewelType.Bless || i.Def.Jewel == JewelType.Soul);
        }

        public int SuccessRate(IReadOnlyList<Item> items)
        {
            int r = 0;
            foreach (var i in items)
            {
                if (i.Def.IsUpgradable) r += i.Level * 4 + i.AdditionalOption * 3 + (i.Luck ? 5 : 0);
                else if (i.Def.Jewel == JewelType.Bless) r += 8;
                else if (i.Def.Jewel == JewelType.Soul) r += 5;
            }
            return Math.Min(r, MaxRate);
        }

        public long ZenCost(IReadOnlyList<Item> items) => 10_000L * SuccessRate(items);

        public void Execute(IReadOnlyList<Item> items, bool success, ChaosResult result)
        {
            result.Consumed.AddRange(items);
            if (success)
            {
                var w = new Item(ItemDatabase.ChaosRuneBlade) { Skill = true };
                result.Outputs.Add(w);
                result.Message = $"sucesso! criou {w.DisplayName}";
            }
            else result.Message = "falhou: materiais perdidos";
        }
    }

    public sealed class ChaosMachine
    {
        private readonly List<IChaosRecipe> _recipes;
        private readonly IRandom _rng;

        public ChaosMachine(IRandom rng, IEnumerable<IChaosRecipe>? recipes = null)
        {
            _rng = rng;
            _recipes = recipes?.ToList() ?? new List<IChaosRecipe> { new ItemUpgradeRecipe(), new ChaosWeaponRecipe() };
        }

        public IChaosRecipe? FindRecipe(IReadOnlyList<Item> items) => _recipes.FirstOrDefault(r => r.Matches(items));

        /// <summary>Pré-visualização (taxa e custo) sem consumir nada.</summary>
        public ChaosResult Preview(IReadOnlyList<Item> items)
        {
            var r = FindRecipe(items);
            if (r == null) return ChaosResult.Fail("combinação inválida");
            return new ChaosResult { Success = true, RecipeName = r.Name, RatePercent = r.SuccessRate(items), ZenCost = r.ZenCost(items), Message = "ok" };
        }

        /// <summary>Executa a mistura. Cobra o zen via callback; não altera nada se zen insuficiente.</summary>
        public ChaosResult Mix(IReadOnlyList<Item> items, ref long zen)
        {
            if (items.Count == 0 || items.Distinct().Count() != items.Count) return ChaosResult.Fail("itens inválidos");
            var r = FindRecipe(items);
            if (r == null) return ChaosResult.Fail("combinação inválida");
            long cost = r.ZenCost(items);
            if (zen < cost) return ChaosResult.Fail($"zen insuficiente ({cost})");
            zen -= cost;
            int rate = r.SuccessRate(items);
            bool ok = _rng.Roll(rate);
            var res = new ChaosResult { Success = ok, RatePercent = rate, ZenCost = cost, RecipeName = r.Name };
            r.Execute(items, ok, res);
            return res;
        }
    }
}
