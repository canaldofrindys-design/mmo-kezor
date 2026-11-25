using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Kezor.Core.Items;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Grade de inventário em UI Toolkit: desenha as casas e põe cada item sobre as w × h casas que ele ocupa (ícone no
    /// formato da grade). Clique, duplo clique e tooltip pelo item; arrastar mostra onde ele cabe (verde) ou não (vermelho).
    /// Os dados vêm de um array de "cantos" (Item? por casa, como o @INV/@VAULT mandam).
    /// </summary>
    public sealed class GridView : VisualElement
    {
        public const float Cell = 40f, Gap = 2f;
        public readonly int Columns, Rows;
        public readonly DragZone Zone;
        private readonly VisualElement _cells, _items, _preview;
        private readonly Dictionary<int, GridItem> _shown = new Dictionary<int, GridItem>();
        private readonly Func<Item?[]> _data;
        private readonly UiRoot _ui;
        public Action<int, Item, int>? OnClick;     // canto, item, cliques
        public Func<int, bool>? Selected;
        public Func<int, Color?>? Mark;              // destaque extra (forja, runa)
        public bool Compare = true;

        private sealed class GridItem
        {
            public VisualElement Root = null!, Icon = null!, Rarity = null!;
            public Label Caption = null!, Lvl = null!, Count = null!;
            public Item? Item; public Sprite? Sprite;
        }

        public GridView(UiRoot ui, DragZone zone, int columns, int rows, Func<Item?[]> data)
        {
            _ui = ui; Zone = zone; Columns = columns; Rows = rows; _data = data;
            AddToClassList("kz-grid");
            pickingMode = PickingMode.Position;
            style.width = columns * (Cell + Gap) + Gap;
            style.height = rows * (Cell + Gap) + Gap;
            _cells = Kz.El("kz-grid__layer", this);
            for (int i = 0; i < columns * rows; i++)
            {
                var c = Kz.El("kz-grid__cell", _cells);
                c.style.left = Gap + (i % columns) * (Cell + Gap);
                c.style.top = Gap + (i / columns) * (Cell + Gap);
            }
            _preview = Kz.El("kz-grid__preview", this);
            _preview.Show(false);
            _items = Kz.El("kz-grid__layer", this);
            userData = new DropTag(zone) { Grid = this };
        }

        public static Vector2 CellPx(int w, int h) => new Vector2(w * Cell + (w - 1) * Gap, h * Cell + (h - 1) * Gap);

        /// <summary>Casa sob o ponto do painel, já descontando o deslocamento de onde o item foi pego.</summary>
        public int CellAt(Vector2 panelPos, Vector2 grabOffset)
        {
            var local = this.WorldToLocal(panelPos - grabOffset + new Vector2(Cell * 0.5f, Cell * 0.5f));
            int x = Mathf.FloorToInt((local.x - Gap) / (Cell + Gap)), y = Mathf.FloorToInt((local.y - Gap) / (Cell + Gap));
            if (x < 0 || y < 0 || x >= Columns || y >= Rows) return -1;
            return y * Columns + x;
        }

        public void ShowPreview(int cell, ItemDefinition d, bool ok)
        {
            if (cell < 0) { _preview.Show(false); return; }
            var (w, h) = ItemSize.Of(d);
            var px = CellPx(w, h);
            _preview.Show(true);
            _preview.style.left = Gap + (cell % Columns) * (Cell + Gap);
            _preview.style.top = Gap + (cell / Columns) * (Cell + Gap);
            _preview.style.width = px.x; _preview.style.height = px.y;
            _preview.SetClass("kz-grid__preview--bad", !ok);
        }

        public void HidePreview() => _preview.Show(false);

        /// <summary>Canto do item que ocupa a casa (ou -1), pelos dados da grade.</summary>
        public int AnchorAt(int cell)
        {
            var data = _data();
            int cx = cell % Columns, cy = cell / Columns;
            for (int i = 0; i < data.Length; i++)
            {
                var it = data[i];
                if (it == null) continue;
                var (w, h) = ItemSize.Of(it.Def);
                int ix = i % Columns, iy = i / Columns;
                if (cx >= ix && cx < ix + w && cy >= iy && cy < iy + h) return i;
            }
            return -1;
        }

        public Item? ItemAt(int anchor) { var d = _data(); return anchor >= 0 && anchor < d.Length ? d[anchor] : null; }

        /// <summary>Soltar em cima de um item do mesmo tamanho troca os dois (o servidor confere).</summary>
        public bool CanSwap(ItemDefinition d, int cell, int ignoreAnchor)
        {
            if (cell < 0) return false;
            int other = AnchorAt(cell);
            var ot = ItemAt(other);
            return ot != null && other != ignoreAnchor && ItemSize.Of(ot.Def) == ItemSize.Of(d);
        }

        /// <summary>Cabe com o canto em <paramref name="cell"/> (ignorando o próprio item, se mover dentro da grade)?</summary>
        public bool Fits(ItemDefinition d, int cell, int ignoreAnchor)
        {
            var data = _data();
            var (w, h) = ItemSize.Of(d);
            int x0 = cell % Columns, y0 = cell / Columns;
            if (cell < 0 || x0 + w > Columns || y0 + h > Rows) return false;
            for (int i = 0; i < data.Length; i++)
            {
                var it = data[i];
                if (it == null || i == ignoreAnchor) continue;
                var (iw, ih) = ItemSize.Of(it.Def);
                int ix = i % Columns, iy = i / Columns;
                if (ix < x0 + w && x0 < ix + iw && iy < y0 + h && y0 < iy + ih) return false;
            }
            return true;
        }

        public void Refresh()
        {
            var data = _data();
            var g = _ui.Game;
            var seen = new HashSet<int>();
            for (int i = 0; i < data.Length && i < Columns * Rows; i++)
            {
                var it = data[i];
                if (it == null) continue;
                seen.Add(i);
                if (!_shown.TryGetValue(i, out var gi)) { gi = Make(i); _shown[i] = gi; }
                if (!ReferenceEquals(gi.Item, it) || gi.Item?.Def != it.Def) Layout(gi, i, it);
                var sp = ItemIcons.Item(it.Def.Id);
                if (sp != gi.Sprite) { gi.Sprite = sp; gi.Icon.style.backgroundImage = sp != null ? new StyleBackground(sp) : new StyleBackground(StyleKeyword.None); }
                gi.Caption.Set(sp == null ? $"<color={g.ItemColor(it)}>{KezorGame.Short(it).Split('\n')[0]}</color>" : "");
                gi.Lvl.Set(it.Level > 0 ? $"+{it.Level}" : "");
                gi.Count.Set(it.Quantity > 1 ? it.Quantity.ToString() : "");
                var rc = ItemIcons.RarityColor(it);
                var col = rc ?? new Color(0, 0, 0, 0);
                gi.Rarity.style.borderLeftColor = gi.Rarity.style.borderRightColor = gi.Rarity.style.borderTopColor = gi.Rarity.style.borderBottomColor = col;
                gi.Rarity.style.backgroundColor = rc.HasValue ? new Color(col.r, col.g, col.b, 0.17f) : new Color(0, 0, 0, 0);   // fundo na cor da raridade
                bool sel = Selected?.Invoke(i) ?? false;
                gi.Root.SetClass("kz-grid__item--sel", sel);
                var mark = Mark?.Invoke(i);
                gi.Root.SetClass("kz-grid__item--mark", mark.HasValue);
                gi.Root.SetClass("kz-grid__item--broken", it.Def.BaseDurability > 0 && it.Durability * 5 < it.MaxDurability);
            }
            var gone = new List<int>();
            foreach (var kv in _shown) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var k in gone) { _shown[k].Root.RemoveFromHierarchy(); _shown.Remove(k); }
        }

        private void Layout(GridItem gi, int anchor, Item it)
        {
            gi.Item = it;
            var (w, h) = ItemSize.Of(it.Def);
            var px = CellPx(w, h);
            gi.Root.style.left = Gap + (anchor % Columns) * (Cell + Gap);
            gi.Root.style.top = Gap + (anchor / Columns) * (Cell + Gap);
            gi.Root.style.width = px.x; gi.Root.style.height = px.y;
        }

        private GridItem Make(int anchor)
        {
            var gi = new GridItem();
            gi.Root = Kz.El("kz-grid__item", _items, pick: true);
            gi.Rarity = Kz.El("kz-grid__rarity", gi.Root);
            gi.Icon = Kz.El("kz-grid__icon", gi.Root);
            gi.Caption = Kz.Text("", "kz-slot__label", gi.Root);
            gi.Lvl = Kz.Text("", "kz-slot__lvl", gi.Root);
            gi.Count = Kz.Text("", "kz-slot__count", gi.Root);
            int a = anchor;
            gi.Root.userData = new DropTag(Zone, a) { Grid = this };
            gi.Root.RegisterCallback<ClickEvent>(e => { var it = _data()[a]; if (it != null) OnClick?.Invoke(a, it, e.clickCount); });
            gi.Root.RegisterCallback<PointerEnterEvent>(e => { var it = _data()[a]; if (it != null && !_ui.Drag.Dragging) _ui.ShowItemTip(it, e.position, Compare); });
            gi.Root.RegisterCallback<PointerMoveEvent>(e => { var it = _data()[a]; if (it != null && !_ui.Drag.Dragging) _ui.ShowItemTip(it, e.position, Compare); });
            gi.Root.RegisterCallback<PointerLeaveEvent>(_ => _ui.HideItemTip());
            _ui.Drag.BindGrid(gi.Root, this, a, () => _data()[a]);
            return gi;
        }
    }
}
