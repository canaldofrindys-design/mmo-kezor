using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Kezor.Core.Characters;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Árvore da Ascensão (tecla A): janela de tela cheia com os três ramos da classe lado a lado (Bastião, Fúria e o
    /// ramo próprio). Cada ramo tem 5 linhas × 3 nós ligados ao de cima; aprendido brilha dourado, disponível pulsa em
    /// brasa, trancado fica apagado. Clique aprende (o servidor valida); "Redistribuir" devolve os pontos por ouro.
    /// </summary>
    public sealed class AscensionWindow : GameWindow
    {
        private const float Node = 48f, CardH = 56f, CellW = 112f, RowGap = 34f;   // carta do emblema + caixinha do rank ao lado
        private static readonly Color[] BranchTint = { KezorLook.Hex("#7fb2e8"), KezorLook.Hex("#ff8a4a"), KezorLook.Hex("#c9a8ff") };

        private readonly Label _level, _points, _lockMsg, _resetCost, _cls;
        private readonly KzBar _exp;
        private readonly VisualElement _branches;
        private readonly Button _reset;
        private CharacterClass _builtFor = (CharacterClass)(-1);
        private readonly Dictionary<int, (VisualElement root, VisualElement icon, Label rank)> _nodes = new Dictionary<int, (VisualElement, VisualElement, Label)>();
        private readonly Dictionary<int, VisualElement> _links = new Dictionary<int, VisualElement>();
        private readonly Label[] _branchSpent = new Label[3];
        private readonly Label[] _branchTitle = new Label[3];

        public AscensionWindow(UiRoot ui) : base(ui, "asc2", "Árvore da Ascensão", 1500, new Vector2(50, 4))
        {
            AddToClassList("asc");
            // cabeçalho em caixas: classe · nível de Ascensão · pontos · experiência · redistribuir
            var head = Row(Body, "asc-head");
            _cls = Kz.Text("", "asc-hbox asc-hbox--cls", head);
            _level = Kz.Text("", "asc-hbox", head);
            _points = Kz.Text("", "asc-hbox", head);
            var expBox = Kz.El("asc-hbox asc-hbox--exp", head);
            _exp = expBox.Put(new KzBar("kz-bar--exp", true));
            _exp.AddToClassList("asc-exp");
            var right = Kz.El("asc-head__right", head, pick: true);
            _resetCost = Kz.Text("", "kz-text-3", right);
            _reset = Kz.Btn("Redistribuir", () => G.Send("ASC RESET"), "kz-btn kz-btn--small", right);
            _lockMsg = Kz.Text("", "asc-lock", Body);
            _branches = Kz.El("asc-branches", Body, pick: true);
            Kz.Text("Clique num nó para aprender um rank · cada linha pede pontos gastos no ramo · os bônus valem sempre, mesmo depois de renascer",
                    "kz-text-3 asc-foot", Body);
        }

        public override bool Wanted => G._showAscension;
        public override void Close() => G._showAscension = false;

        private void Build(CharacterClass cls)
        {
            _builtFor = cls;
            _branches.Clear(); _nodes.Clear(); _links.Clear();
            var all = AscensionTree.For(cls);
            for (int b = 0; b < 3; b++)
            {
                var col = Kz.El("kz-panel-dark asc-branch", _branches, pick: true);
                _branchTitle[b] = Kz.Text(AscensionTree.BranchName(b, cls), "kz-h2 asc-branch__title", col);
                _branchTitle[b].style.color = BranchTint[b];
                _branchSpent[b] = Kz.Text("", "kz-text-3 asc-branch__spent", col);
                _branchSpent[b].Show(false);
                var grid = Kz.El("asc-grid", col, pick: true);
                grid.style.width = AscensionTree.Cols * CellW - (CellW - Node - 40);
                grid.style.height = AscensionTree.Tiers * CardH + (AscensionTree.Tiers - 1) * RowGap;
                foreach (var n in all.Where(x => x.Branch == b))
                {
                    float x = n.Col * CellW, y = n.Tier * (CardH + RowGap);
                    if (n.Prereq != 0)
                    {   // seta do nó de cima para este
                        var link = Kz.El("asc-link", grid);
                        link.style.left = x + Node / 2 - 2; link.style.top = y - RowGap + 2; link.style.height = RowGap - 9;
                        Kz.El("asc-link__head", link);
                        _links[n.Id] = link;
                    }
                    var root = Kz.El("asc-node", grid, pick: true);
                    root.style.left = x; root.style.top = y;
                    var icon = Kz.Icon(Kz.Art("g_" + Glyph(n.Effect)), "asc-node__icon", root);
                    icon.style.unityBackgroundImageTintColor = BranchTint[b];
                    var rank = Kz.Text("", "asc-node__rank", grid);
                    rank.style.left = x + Node + 4; rank.style.top = y + CardH - 24;
                    int id = n.Id;
                    root.RegisterCallback<ClickEvent>(_ => G.Send($"ASC LEARN {id}"));
                    Ui.Hud.Tip(root, () => NodeTip(id));
                    _nodes[n.Id] = (root, icon, rank);
                }
            }
        }

        private static string Glyph(AscEffect e) => e switch
        {
            AscEffect.HpPct => "a_hp", AscEffect.MpPct => "a_mp", AscEffect.Defense => "a_def", AscEffect.ReductionPct => "a_red",
            AscEffect.DamagePct => "a_dmg", AscEffect.CritPct => "a_crit", AscEffect.AttackRatePct => "a_ar", AscEffect.DefenseRatePct => "a_dr",
            AscEffect.Vigor => "a_vig", AscEffect.ShieldPct => "a_sh", AscEffect.SpeedPct => "a_spd", AscEffect.LifeOnKill => "a_lok",
            _ => "a_mag",
        };

        private int Rank(int id) => S.AscRanks.TryGetValue(id, out var r) ? r : 0;
        private int SpentIn(int branch) => S.AscRanks.Where(kv => AscensionTree.Get(kv.Key)?.Branch == branch).Sum(kv => kv.Value);

        private bool Available(AscNode n) =>
            S.AscUnlocked && S.AscPoints > 0 && Rank(n.Id) < n.MaxRank && (n.Prereq == 0 || Rank(n.Prereq) > 0) && SpentIn(n.Branch) >= n.TierPoints;

        private string NodeTip(int id)
        {
            var n = AscensionTree.Get(id);
            if (n == null) return "";
            int r = Rank(id);
            var lines = new List<string>
            {
                $"<b><color=#ffd36a>{n.Name}</color></b>   <color=#b9ad97>{r}/{n.MaxRank}</color>",
                $"<color=#b9ad97>{AscensionTree.BranchName(n.Branch, S.Me.Class)} · linha {n.Tier + 1}</color>",
            };
            lines.Add(r > 0 ? $"Agora: <b>{n.Describe(r)}</b>" : "<color=#7d7366>Ainda não aprendido</color>");
            if (r < n.MaxRank) lines.Add($"Próximo rank: <color=#8fe08f>{n.Describe(r + 1)}</color>");
            var req = new List<string>();
            if (n.Prereq != 0 && Rank(n.Prereq) == 0) req.Add($"<color=#ff6a5a>aprenda {AscensionTree.Get(n.Prereq)?.Name}</color>");
            if (SpentIn(n.Branch) < n.TierPoints) req.Add($"<color=#ff6a5a>{n.TierPoints} pontos no ramo ({SpentIn(n.Branch)})</color>");
            if (!S.AscUnlocked) req.Add($"<color=#ff6a5a>nível {AscensionTree.UnlockLevel}</color>");
            if (req.Count > 0) lines.Add("Requer: " + string.Join(" · ", req));
            else if (r < n.MaxRank) lines.Add(S.AscPoints > 0 ? "<color=#ffb347>Clique para aprender</color>" : "<color=#7d7366>Sem pontos livres</color>");
            return string.Join("\n", lines);
        }

        public override void Refresh()
        {
            var cls = S.Me.Class;
            if (cls != _builtFor) Build(cls);
            int spent = S.AscRanks.Values.Sum();
            _cls.Set(ClassDefinition.Get(cls).DisplayName);
            _level.Set(S.AscUnlocked ? $"◆  Nível de Ascensão: {S.AscLevel}" : "◆  Ascensão trancada");
            float frac = S.AscNext > 0 ? Mathf.Clamp01(S.AscExp / (float)S.AscNext) : 0f;
            _exp.Set(frac, S.AscUnlocked ? (S.AscLevel >= AscensionTree.MaxLevel ? "Experiência: máximo" : $"Experiência: {frac * 100f:0.00}%") : "");
            _points.Set($"◆  Pontos de Ascensão: {S.AscPoints}");
            _lockMsg.Show(!S.AscUnlocked);
            _lockMsg.Set($"A Ascensão libera no nível {AscensionTree.UnlockLevel} (você está no {S.Me.Level}). No 400 toda a experiência vira Ascensão: 1 ponto por nível, até {AscensionTree.MaxLevel}.");
            _reset.SetEnabled(spent > 0);
            _resetCost.Set(spent > 0 ? $"custa {AscensionTree.ResetCost(spent):N0} de ouro" : "");
            for (int b = 0; b < 3; b++) _branchTitle[b].Set($"{AscensionTree.BranchName(b, cls)}: {SpentIn(b)}");
            foreach (var kv in _nodes)
            {
                var n = AscensionTree.Get(kv.Key)!;
                int r = Rank(kv.Key);
                kv.Value.rank.Set(r.ToString());
                kv.Value.rank.SetClass("asc-node__rank--max", r >= n.MaxRank);
                kv.Value.root.SetClass("asc-node--learned", r > 0);
                kv.Value.root.SetClass("asc-node--max", r >= n.MaxRank);
                kv.Value.root.SetClass("asc-node--available", Available(n));
                kv.Value.root.SetClass("asc-node--locked", r == 0 && !Available(n));
                if (_links.TryGetValue(kv.Key, out var link)) link.SetClass("asc-link--on", r > 0);
            }
        }
    }
}
