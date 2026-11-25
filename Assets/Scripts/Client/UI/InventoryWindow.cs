using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Kezor.Core.Characters;
using Kezor.Core.Items;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Inventário v4: painel alto preso à direita. Em cima, o bloco de equipamento com cada slot no formato do que
    /// recebe (arma e escudo altos, asas largas, joias pequenas) e uma silhueta-guia quando vazio; embaixo, a mochila 8×8
    /// com números grandes de pilha e fundo na cor da raridade; no rodapé, ouro e botões (fechar, reparar, forja,
    /// expandir/compactar). Clique seleciona, duplo clique equipa/usa, arrastar move entre janelas.
    /// </summary>
    public sealed class InventoryWindow : GameWindow
    {
        public const float Width = 384f;
        private const float C1 = GridView.Cell, C2 = GridView.Cell * 2 + GridView.Gap, C3 = GridView.Cell * 3 + GridView.Gap * 2;

        /// <summary>Slot, nome, guia, posição e tamanho no bloco de equipamento (364 × 302).</summary>
        private static readonly (EquipSlot slot, string name, string guide, float x, float y, float w, float h)[] Layout =
        {   // alto: mascote · amuleto · elmo · asas | meio: arma · montaria · armadura · (livre) · escudo | baixo: luvas · anel · calças · anel · botas
            (EquipSlot.Pet,       "Mascote",  "s_pet",     0,   0, C2, C2),
            (EquipSlot.Amulet,    "Amuleto",  "s_amulet", 92,  42, C1, C1),
            (EquipSlot.Helm,      "Elmo",     "s_helm",  141,   0, C2, C2),
            (EquipSlot.Wings,     "Asas",     "s_wings", 236,   0, C3, C2),
            (EquipSlot.RightHand, "Arma",     "s_weapon",  0,  90, C2, C3),
            (EquipSlot.Mount,     "Montaria", "s_mount",  92, 132, C1, C1),
            (EquipSlot.Armor,     "Armadura", "s_armor", 141,  90, C2, C3),
            (EquipSlot.LeftHand,  "Escudo",   "s_shield",278,  90, C2, C3),
            (EquipSlot.Gloves,    "Luvas",    "s_gloves",  0, 222, C2, C2),
            (EquipSlot.Ring1,     "Anel",     "s_ring",   92, 243, C1, C1),
            (EquipSlot.Pants,     "Calças",   "s_pants", 141, 222, C2, C2),
            (EquipSlot.Ring2,     "Anel",     "s_ring",  232, 243, C1, C1),
            (EquipSlot.Boots,     "Botas",    "s_boots", 278, 222, C2, C2),
        };

        private readonly Dictionary<EquipSlot, KzSlot> _equip = new Dictionary<EquipSlot, KzSlot>();
        private readonly Dictionary<EquipSlot, VisualElement> _guide = new Dictionary<EquipSlot, VisualElement>();
        private readonly VisualElement _equipBlock;
        private readonly GridView _bag;
        private readonly Label _gold, _hint, _setInfo, _free;
        private readonly Button _equipBtn, _useBtn, _runeBtn, _sellBtn, _cancelBtn, _expandBtn;
        private bool _compact;

        public InventoryWindow(UiRoot ui) : base(ui, "inv5", "Equipamento", Width, new Vector2(1600 - Width - 4, 2))
        {
            AddToClassList("inv4");
            // ---------------- equipamento
            _equipBlock = Kz.El("inv4-equip", Body, pick: true);
            _equipBlock.userData = new DropTag(DragZone.Equip);
            foreach (var (slot, name, guide, x, y, w, h) in Layout)
            {
                var sl = slot;
                var s = ItemSlot(_equipBlock, () => S.Equipment.TryGetValue(sl, out var it) ? it : null, false, "inv4-slot");
                s.style.position = Position.Absolute; s.style.left = x; s.style.top = y; s.style.width = w; s.style.height = h;
                s.Key.text = "";
                var g = Kz.Icon(Kz.Art("g_" + guide), "inv4-slot__guide", s);
                g.SendToBack();
                s.tooltip = "";
                if (w > C1) Kz.Text(name, "inv4-slot__name", s);   // nos slots pequenos basta a silhueta
                s.userData = new DropTag(DragZone.Equip, -1, sl);
                ui.Drag.Bind(s, new DropTag(DragZone.Equip, -1, sl), () => S.Equipment.TryGetValue(sl, out var it) ? it : null);
                s.RegisterCallback<ClickEvent>(e => { if (e.clickCount >= 2 && S.Equipment.ContainsKey(sl)) G.Send($"UNEQUIP {sl}"); });
                _equip[slot] = s; _guide[slot] = g;
            }
            _setInfo = Kz.Text("", "kz-text-3 inv4-setinfo", _equipBlock);

            // ---------------- mochila
            var head = Row(Body, "inv4-head");
            _hint = Kz.Text("", "kz-text-2 inv4-hint", head);
            _free = Kz.Text("", "kz-text-3 inv4-free", head);
            _bag = Body.Put(new GridView(ui, DragZone.Bag, Character.InventoryColumns, Character.InventorySize / Character.InventoryColumns, () => S.Inventory));
            _bag.AddToClassList("inv4-bag");
            _bag.OnClick = (slot, it, clicks) => G.ClickSlot(slot, it, clicks >= 2);
            _bag.Selected = i => i == G._selSlot || i == G._runeMode;
            _bag.Mark = i => G._forgeSel.Contains(i) ? new Color(1f, 0.55f, 0.1f) : (Color?)null;

            var actions = Row(Body, "inv4-actions");
            _equipBtn = Kz.Btn("Equipar", () => { G.Send($"EQUIP {G._selSlot}"); G._selSlot = -1; }, "kz-btn kz-btn--small", actions);
            _useBtn = Kz.Btn("Usar", () => G.Send($"USE {G._selSlot}"), "kz-btn kz-btn--small", actions);
            _runeBtn = Kz.Btn("Aplicar runa", () => G._runeMode = G._selSlot, "kz-btn kz-btn--small kz-btn--primary", actions);
            _sellBtn = Kz.Btn("Vender", () => { G.Send($"SELL {G._selSlot}"); G._selSlot = -1; }, "kz-btn kz-btn--small", actions);
            _cancelBtn = Kz.Btn("Cancelar", () => { G._runeMode = -1; G._selSlot = -1; }, "kz-btn kz-btn--small", actions);

            // ---------------- rodapé: caixa de ouro e botões quadrados (fechar à esquerda; reparar, forja e expandir à direita)
            var goldBox = Kz.El("inv4-goldbox", Body);
            Kz.Icon(Kz.Art("g_coin"), "inv4-goldbox__coin", goldBox);
            _gold = Kz.Text("", "inv4-goldbox__value kz-num", goldBox);
            var foot = Row(Body, "inv4-foot");
            IconBtn(foot, "g_close", "Fechar", Close);
            Kz.El("inv4-foot__gap", foot);
            IconBtn(foot, "g_options", "Reparar o equipamento (na vila)", () => G.Send("REPAIR"));
            IconBtn(foot, "g_a_mag", "Forja Arcana", () => G._showForge = !G._showForge);
            _expandBtn = IconBtn(foot, "", "Compactar / expandir", () => { _compact = !_compact; }, "+");
            _expandBtn.AddToClassList("inv4-ibtn--ember");
            userData = new DropTag(DragZone.Bag);
        }

        private static Button IconBtn(VisualElement parent, string glyph, string tip, System.Action onClick, string text = "")
        {
            var b = Kz.Btn(text, onClick, "inv4-ibtn", parent);
            b.tooltip = tip;
            if (glyph != "") Kz.Icon(Kz.Art(glyph), "inv4-ibtn__glyph", b);
            return b;
        }

        public override bool Wanted => G._showInv;
        public override void Close() { G._showInv = false; G._runeMode = -1; G._selSlot = -1; }

        public override void Refresh()
        {
            _equipBlock.Show(!_compact);
            _expandBtn.text = _compact ? "+" : "−";
            foreach (var kv in _equip)
            {
                S.Equipment.TryGetValue(kv.Key, out var it);
                ItemIcons.Fill(kv.Value, it, G);
                _guide[kv.Key].Show(it == null);
                kv.Value.Q<Label>(className: "inv4-slot__name")?.Show(it == null);
                var rc = it != null ? ItemIcons.RarityColor(it) : null;
                kv.Value.style.backgroundColor = it != null ? new Color(0.12f, 0.095f, 0.075f, 0.95f) : new StyleColor(StyleKeyword.Null);   // slot ocupado: carvão (paleta do Kezor; raridade só na borda)
                var bc = rc ?? new Color(0.54f, 0.42f, 0.24f);
                kv.Value.style.borderTopColor = kv.Value.style.borderBottomColor = kv.Value.style.borderLeftColor = kv.Value.style.borderRightColor = it != null ? bc : new StyleColor(StyleKeyword.Null);
            }
            _bag.Refresh();
            int used = 0;
            foreach (var x in S.Inventory) if (x != null) { var (w, h) = ItemSize.Of(x.Def); used += w * h; }
            _free.Set($"{Character.InventorySize - used} casas livres");
            _gold.Set($"{S.Me.Gold:N0}");   // valor grande na caixa do rodapé
            _hint.Set(G._runeMode >= 0 ? "<color=#ffb347>Escolha o item que vai receber a runa.</color>"
                    : G._showForge ? "<color=#ffb347>Clique nos itens para a forja.</color>"
                    : S.VaultOpen ? "Arraste para o baú ou dê duplo clique."
                    : S.TradeOpen ? "Arraste para a troca ou dê duplo clique."
                    : G._showMyShop ? "Arraste para a sua loja."
                    : "Mochila");
            var sel = G._selSlot >= 0 ? S.Inventory[G._selSlot] : null;
            _equipBtn.Show(sel != null && sel.Def.Slot != EquipSlot.None);
            _useBtn.Show(sel != null && sel.Def.IsPotion);
            _runeBtn.Show(sel != null && sel.Def.IsRune && sel.Def.Rune != RuneType.Vortex && G._runeMode < 0);
            _sellBtn.Show(sel != null && G._showShop);
            if (sel != null && G._showShop) _sellBtn.text = $"Vender ({Kezor.Core.Server.GameServer.SellPrice(sel):N0})";
            _cancelBtn.Show(G._runeMode >= 0 || sel != null);
            var set = ArmorSets.Complete(S.Equipment);
            _setInfo.Set(set != ArmorSet.None ? $"<color=#ffd36a>Conjunto {ArmorSets.Name(set)} ativo</color>" : "");
        }
    }
}
