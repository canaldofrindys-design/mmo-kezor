using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.Net;

namespace Kezor.Client.UI
{
    /// <summary>Ícones dos atlas gerados por Kezor ▸ Gerar ícones (Resources/UI/Icons/items e skills); null se faltar.</summary>
    public static class ItemIcons
    {
        private static Dictionary<string, Sprite>? _all;

        private static Dictionary<string, Sprite> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new Dictionary<string, Sprite>();
                foreach (var atlas in new[] { "UI/Icons/items", "UI/Icons/skills" })
                {   // atlas + mapa "nome x y w h" gerados por Kezor ▸ Gerar ícones
                    var tex = Resources.Load<Texture2D>(atlas);
                    var map = Resources.Load<TextAsset>(atlas + "_map");
                    if (tex == null || map == null) continue;
                    foreach (var line in map.text.Split((char)10))
                    {
                        var f = line.Trim().Split(' ');
                        if (f.Length < 5) continue;
                        var rect = new Rect(int.Parse(f[1]), int.Parse(f[2]), int.Parse(f[3]), int.Parse(f[4]));
                        var sp = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                        sp.name = f[0];
                        _all[f[0]] = sp;
                    }
                }
                return _all;
            }
        }

        public static Sprite? Item(int id) => All.TryGetValue("item_" + id, out var s) ? s : null;
        public static Sprite? Skill(int id) => All.TryGetValue("skill_" + id, out var s) ? s : null;

        /// <summary>Põe o ícone do item no slot (ou o nome curto, se não houver ícone) com nível, quantidade e raridade.</summary>
        public static void Fill(KzSlot slot, Item? it, KezorGame g)
        {
            if (it == null) { slot.SetSprite(null); slot.Caption.Set(""); slot.Lvl.Set(""); slot.Count.Set(""); slot.SetRarity(null); return; }
            var sp = Item(it.Def.Id);
            slot.SetSprite(sp);
            slot.Caption.Set(sp == null ? $"<color={g.ItemColor(it)}>{KezorGame.Short(it).Split('\n')[0]}</color>" : "");
            slot.Lvl.Set(it.Level > 0 ? $"+{it.Level}" : "");
            slot.Count.Set(it.Quantity > 1 ? it.Quantity.ToString() : "");
            slot.SetRarity(RarityColor(it));
            slot.SetClass("kz-slot--broken", it.Def.BaseDurability > 0 && it.Durability * 5 < it.MaxDurability);
        }

        public static Color? RarityColor(Item it)
        {
            if (it.Rarity == ItemRarity.Ascended) return KezorLook.Hex("#6dff9e");
            if (it.Def.Epic) return KezorLook.Hex("#ff8a3a");
            if (it.Def.IsRune) return Models.RuneColor(it.Def.Rune);
            if (it.Level >= 7) return KezorLook.Hex("#ffd36a");
            if (it.Def.IsJewel || it.Def.Tier >= 1) return KezorLook.Hex("#6aa0ff");   // raro
            return null;
        }

    }

    /// <summary>
    /// Tooltip rico de item: nome na cor da raridade, tipo, atributos, requisitos (vermelho se faltar), opções Ascendentes,
    /// bônus do conjunto com quantas peças você usa e comparação com o equipado no mesmo slot.
    /// </summary>
    public static class ItemTooltip
    {
        public static string KindName(ItemDefinition d) => d.Kind switch
        {
            ItemKind.Sword => "Espada", ItemKind.Axe => "Machado", ItemKind.Staff => "Cajado", ItemKind.Bow => "Arco",
            ItemKind.Lance => "Lança", ItemKind.Gauntlet => "Manoplas", ItemKind.Tome => "Tomo / sino",
            ItemKind.Shield => "Escudo", ItemKind.Helm => "Elmo", ItemKind.Armor => "Armadura", ItemKind.Pants => "Calças",
            ItemKind.Gloves => "Luvas", ItemKind.Boots => "Botas", ItemKind.Potion => "Poção", ItemKind.Ring => "Anel", ItemKind.Amulet => "Amuleto",
            _ => d.IsWings ? "Asas" : d.IsMount ? "Montaria" : d.IsPet ? "Mascote" : d.IsRune ? "Runa" : "Item",
        };

        public static string Build(Item it, KezorGame g, bool compare)
        {
            var me = g.State.Me;
            var lines = new List<string>();
            string rarity = it.Rarity == ItemRarity.Ascended ? "Ascendente" : it.Def.Epic ? "Épico" : it.Level >= 7 ? "Refinado" : "";
            lines.Add($"<size=16><b><color={g.ItemColor(it)}>{it.DisplayName}</color></b></size>");
            lines.Add($"<color=#b9ad97>{KindName(it.Def)}{(rarity != "" ? $"  ·  <color={Kz.Hex(ItemIcons.RarityColor(it) ?? Color.white)}>{rarity}</color>" : "")}{(it.Def.IsUpgradable && it.Level > 0 ? $"  ·  runa +{it.Level}" : "")}</color>");
            var stats = new List<string>();
            if (it.Def.IsWeapon) stats.Add($"Dano  <b>{it.MinDamage} ~ {it.MaxDamage}</b>");
            if (it.MagicPowerPercent > 0) stats.Add($"Poder mágico  <b>+{it.MagicPowerPercent}%</b>");
            if (it.Defense > 0) stats.Add($"Defesa  <b>{it.Defense}</b>");
            if (it.Def.IsWings) stats.Add($"Dano <b>+{it.WingDamage}%</b>  ·  Absorve <b>{it.WingAbsorb}%</b>");
            if (it.Def.IsMount) stats.Add($"Velocidade montado <b>+{it.Def.MountSpeedPercent}%</b>  (tecla X)");
            if (it.Def.Pet == PetType.Collector) stats.Add("Recolhe seus itens num raio de 6 m e dá +10% de ouro");
            if (it.Def.Pet == PetType.Guardian) stats.Add($"Defesa +{Item.GuardianPetDefense} e -{Item.GuardianPetReduction}% de dano recebido");
            if (it.Def.IsPotion) stats.Add($"Recupera <b>{it.Def.PotionDescription}</b>");
            if (it.Def.IsRune) stats.Add(RuneService.Describe(it.Def.Rune));
            if (it.Def.JewelHp > 0) stats.Add($"Vida  <b>+{it.Def.JewelHp}</b>");
            if (it.Def.JewelMp > 0) stats.Add($"Mana  <b>+{it.Def.JewelMp}</b>");
            if (it.Def.JewelResistPercent > 0) stats.Add($"Resistência  <b>-{it.Def.JewelResistPercent}%</b> de dano recebido");
            if (it.Def.JewelDamagePercent > 0) stats.Add($"Dano  <b>+{it.Def.JewelDamagePercent}%</b>");
            if (it.Def.Kind == ItemKind.Ring) stats.Add("<color=#7d7366>Usa até dois anéis (um em cada mão)</color>");
            if (stats.Count > 0) { lines.Add(""); lines.AddRange(stats); }
            if (it.Def.BaseDurability > 0)
            {
                bool low = it.Durability * 5 < it.MaxDurability;
                lines.Add($"<color={(low ? "#ff6a5a" : "#7d7366")}>Durabilidade {it.Durability}/{it.MaxDurability}{(low ? "  (eficiência pela metade)" : "")}</color>");
            }
            var opts = new List<string>();
            if (it.Luck) opts.Add("Sorte: +5% de crítico e +25% de sucesso na Runa do Eclipse");
            if (it.Skill) opts.Add("Skill da arma");
            if (it.AdditionalOption > 0) opts.Add($"Opção adicional +{it.AdditionalOption * 4}");
            foreach (AscendedOption o in System.Enum.GetValues(typeof(AscendedOption)))
                if (o != AscendedOption.None && (it.Ascended & o) != 0) opts.Add(Item.AscendedText(o));
            if (opts.Count > 0) { lines.Add(""); foreach (var o in opts) lines.Add($"<color=#6dff9e>◆ {o}</color>"); }
            if (it.Def.Epic) lines.Add("<color=#ff8a3a>◆ Arma épica: rastro e faíscas no golpe</color>");

            var req = new List<string>();
            void Req(string label, int need, int have) { if (need > 0) req.Add($"<color={(have >= need ? "#7d7366" : "#ff6a5a")}>{label} {need}</color>"); }
            Req("Nível", it.Def.ReqLevel, me.Level);
            Req("Força", it.RequiredStrength, me.Str);
            Req("Agilidade", it.RequiredAgility, me.Agi);
            Req("Energia", it.Def.ReqEnergy, me.Ene);
            if (it.Def.Slot != EquipSlot.None && (it.Def.AllowedClasses & Kezor.Core.Characters.ClassDefinition.MaskOf(me.Class)) == 0) req.Add("<color=#ff6a5a>Sua classe não pode usar</color>");
            if (req.Count > 0) { lines.Add(""); lines.Add("Requer: " + string.Join("  ·  ", req)); }

            if (it.Def.Set != ArmorSet.None)
            {
                int have = ArmorSets.Count(g.State.Equipment, it.Def.Set);
                var b = ArmorSets.Bonus(it.Def.Set);
                bool on = have >= ArmorSets.Pieces.Length;
                lines.Add("");
                lines.Add($"<color={(on ? "#ffd36a" : "#b9ad97")}><b>Conjunto {ArmorSets.Name(it.Def.Set)}</b>  ({have}/5)</color>");
                lines.Add($"<color={(on ? "#e8c27a" : "#7d7366")}>{ArmorSets.Describe(it.Def.Set).Split(':').Last().Trim()}</color>");
            }

            if (compare && it.Def.Slot != EquipSlot.None && g.State.Equipment.TryGetValue(it.Def.Slot, out var eq) && !ReferenceEquals(eq, it))
            {
                var diff = new List<string>();
                void D(string label, int a, int b, string unit = "") { if (a != b) diff.Add($"{label} <color={(a > b ? "#8fe08f" : "#ff6a5a")}>{(a > b ? "+" : "")}{a - b}{unit}</color>"); }
                D("Dano mín.", it.MinDamage, eq.MinDamage); D("Dano máx.", it.MaxDamage, eq.MaxDamage);
                D("Defesa", it.Defense, eq.Defense); D("Poder mágico", it.MagicPowerPercent, eq.MagicPowerPercent, "%");
                D("Dano das asas", it.WingDamage, eq.WingDamage, "%"); D("Absorção", it.WingAbsorb, eq.WingAbsorb, "%");
                lines.Add("");
                lines.Add($"<color=#7d7366>Comparado a {eq.DisplayName}:</color>");
                lines.Add(diff.Count > 0 ? string.Join("   ", diff) : "<color=#7d7366>mesmos atributos</color>");
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>Onde um item pode sair ou cair num arrasto.</summary>
    public enum DragZone { None, Bag, Equip, Vault, TradeMine, MyShop, Quick }

    public sealed class DropTag
    {
        public DragZone Zone; public int Index; public EquipSlot Slot;
        public GridView? Grid;
        public DropTag(DragZone z, int i = -1, EquipSlot s = EquipSlot.None) { Zone = z; Index = i; Slot = s; }
    }

    /// <summary>
    /// "Clique para pegar, clique para soltar": o 1º clique esquerdo pega o item, que fica preso ao cursor no tamanho
    /// da grade (com a prévia verde/vermelha de encaixe embaixo); o 2º clique solta na casa ou slot sob o cursor
    /// (mochila, equipamento, baú, troca, loja, atalhos Q W E R). Em cima de outro item do mesmo tamanho, os dois trocam
    /// e o antigo vai para o cursor. Clique no chão do mundo larga o item (confirma se for valioso); Esc ou clique
    /// direito devolve. O item só sai do lugar quando o servidor aceita o comando.
    /// </summary>
    public sealed class DragDrop
    {
        private readonly UiRoot _ui;
        private readonly VisualElement _ghost, _ghostIcon;
        private readonly Label _ghostText;
        private DropTag? _from; private Item? _item; private Vector2 _grab;
        private VisualElement? _originEl;
        private GridView? _hoverGrid;
        private bool _busy;   // esperando a resposta do servidor
        /// <summary>Há um item preso ao cursor.</summary>
        public bool Holding => _item != null;
        /// <summary>Compatibilidade: tooltips somem enquanto há item no cursor.</summary>
        public bool Dragging => Holding;
        public Item? Held => _item;

        public DragDrop(UiRoot ui)
        {
            _ui = ui;
            _ghost = Kz.El("kz-ghost", ui.OverlayLayer);
            _ghostIcon = Kz.El("kz-grid__icon", _ghost);
            _ghostText = Kz.Text("", "kz-slot__label", _ghost);
            _ghost.Show(false);
            ItemSpin.Hide(_ghostIcon);
            ui.Root.RegisterCallback<PointerDownEvent>(OnDownWhileHolding, TrickleDown.TrickleDown);
        }

        /// <summary>Liga um slot (equipamento, troca) como origem: clique pega.</summary>
        public void Bind(KzSlot slot, DropTag tag, System.Func<Item?> item)
        {
            slot.userData = tag;
            slot.RegisterCallback<PointerDownEvent>(e =>
            {
                if (Holding) return;
                var it = item();
                if (it == null) return;
                if (e.button == 1 && tag.Zone == DragZone.Equip) { _ui.Game.Send($"UNEQUIP {tag.Slot}"); return; }   // clique direito desequipa
                if (e.button == 0) Pick(tag, it, slot, new Vector2(GridView.Cell * 0.5f, GridView.Cell * 0.5f));
            });
        }

        /// <summary>Liga um item de grade como origem (pega pelo ponto clicado, para soltar alinhado às casas).</summary>
        public void BindGrid(VisualElement el, GridView grid, int anchor, System.Func<Item?> item)
            => el.RegisterCallback<PointerDownEvent>(e =>
            {
                if (Holding) return;
                var it = item();
                if (it == null) return;
                var g = _ui.Game;
                if (e.button == 1 && grid.Zone == DragZone.Bag) { g.ClickSlot(anchor, it, true); return; }   // clique direito: equipa/usa
                if (e.button != 0) return;
                if (grid.Zone == DragZone.Bag && (g._runeMode >= 0 || g._showForge)) return;                 // runa e forja usam o clique de seleção
                if (grid.Zone == DragZone.Bag) g._selSlot = anchor;                                            // mostra Equipar/Usar/Vender
                Pick(new DropTag(grid.Zone, anchor) { Grid = grid }, it, el, (Vector2)e.position - new Vector2(el.worldBound.xMin, el.worldBound.yMin));
            });

        private void Pick(DropTag tag, Item it, VisualElement origin, Vector2 grab)
        {
            _from = tag; _item = it; _grab = grab; _originEl = origin;
            origin.AddToClassList("kz-held-origin");
            var (w, h) = ItemSize.Of(it.Def);
            var px = GridView.CellPx(w, h);
            _ghost.style.width = px.x; _ghost.style.height = px.y;
            var sp = ItemIcons.Item(it.Def.Id);
            _ghostIcon.style.backgroundImage = sp != null ? new StyleBackground(sp) : new StyleBackground(StyleKeyword.None);
            _ghostText.Set(sp == null ? KezorGame.Short(it) : "");
            _ghost.Show(true);
            ItemSpin.Show(_ghostIcon, it.Def, px);      // o item pego continua girando preso ao cursor
            _ghost.BringToFront();
            _ui.HideItemTip();
            Follow(MousePanel());
        }

        /// <summary>Devolve o item à origem (nada muda no servidor).</summary>
        public void Cancel()
        {
            _originEl?.RemoveFromClassList("kz-held-origin");
            _from = null; _item = null; _originEl = null; _busy = false;
            _ghost.Show(false);
            ItemSpin.Hide(_ghostIcon);
            _hoverGrid?.HidePreview(); _hoverGrid = null;
        }

        /// <summary>Teste/captura: posição do cursor no painel no lugar do mouse real.</summary>
        public Vector2? FakePointer;

        private Vector2 MousePanel()
        {
            if (FakePointer.HasValue) return FakePointer.Value;
            var m = InputK.MousePosition;
            return _ui.Root.panel != null ? RuntimePanelUtils.ScreenToPanel(_ui.Root.panel, new Vector2(m.x, Screen.height - m.y)) : Vector2.zero;
        }

        /// <summary>Todo quadro: o fantasma segue o cursor (também fora das janelas) e mostra onde o item cabe.</summary>
        public void Tick()
        {
            if (!Holding) return;
            if (!_ui.Game.InWorld) { Cancel(); return; }
            Follow(MousePanel());
        }

        private void Follow(Vector2 pos)
        {
            if (_item == null || _from == null) return;
            _ghost.style.left = pos.x - _grab.x; _ghost.style.top = pos.y - _grab.y;
            var grid = TagAt(pos)?.Grid;
            if (_hoverGrid != null && _hoverGrid != grid) _hoverGrid.HidePreview();
            _hoverGrid = grid;
            if (grid != null)
            {
                int cell = grid.CellAt(pos, _grab);
                grid.ShowPreview(cell, _item.Def, cell >= 0 && (grid.Fits(_item.Def, cell, grid == _from.Grid ? _from.Index : -1) || grid.CanSwap(_item.Def, cell, grid == _from.Grid ? _from.Index : -1)));
            }
        }

        private DropTag? TagAt(Vector2 pos)
        {
            var picked = _ui.Root.panel.Pick(pos);
            for (var v = picked; v != null; v = v.parent)
                if (v.userData is DropTag t && v != _ghost) return t;
            return null;
        }

        /// <summary>Com item na mão, o próximo clique na interface decide onde ele vai.</summary>
        private void OnDownWhileHolding(PointerDownEvent e)
        {
            if (!Holding || _from == null || _item == null) return;
            if (e.button == 1) { Cancel(); e.StopPropagation(); return; }   // clique direito devolve
            if (e.button != 0) return;
            var to = TagAt(e.position);
            if (to == null) { Cancel(); return; }                          // botão ou painel qualquer: devolve e deixa o clique seguir
            int cell = to.Grid != null ? to.Grid.CellAt(e.position, _grab) : -1;
            bool same = to.Zone == _from.Zone && (to.Grid != null ? cell == _from.Index : to.Index == _from.Index && to.Slot == _from.Slot);
            if (same) { Cancel(); return; }                                // soltou onde pegou (o duplo clique segue para equipar/usar)
            e.StopPropagation();
            if (_busy) return;
            Resolve(_from, to, _item, cell);
        }

        /// <summary>Clique no chão do mundo com item na mão (chamado pelo KezorGame): larga no chão, confirmando se for valioso.</summary>
        public void DropOnGround()
        {
            if (_from == null || _item == null) return;
            if (_from.Zone != DragZone.Bag) { _ui.Game.State.AddLog("err", "Só itens da mochila podem ser largados no chão."); Cancel(); return; }
            int slot = _from.Index; var it = _item;
            bool valuable = it.Level >= 7 || it.Def.Epic || it.Rarity == ItemRarity.Ascended || it.Def.IsWings || it.Def.IsJewel || it.Def.Tier >= 2 || it.Def.Value >= 100_000;
            Cancel();
            if (!valuable) { _ui.Game.Send($"DROP {slot}"); return; }
            PromptsView.Ask($"Largar <color={_ui.Game.ItemColor(it)}>{it.DisplayName}</color> no chão?", () => _ui.Game.Send($"DROP {slot}"));
        }

        private void Done(string reply, DropTag? swapTag = null, Item? swapItem = null, VisualElement? swapEl = null)
        {
            _busy = false;
            if (reply.StartsWith("ERR")) { _ui.Game.State.AddLog("err", reply.Substring(4)); return; }   // continua na mão
            Cancel();
            if (swapTag != null && swapItem != null && reply.StartsWith("OK troca"))
            {   // o item que estava no destino vem para o cursor
                var parts = reply.Split(' ');
                if (parts.Length >= 3 && int.TryParse(parts[2], out var at)) swapTag.Index = at;
                Pick(swapTag, swapItem, swapEl ?? _ui.Root, _grab);
            }
        }

        private void Resolve(DropTag from, DropTag to, Item it, int cell)
        {
            var g = _ui.Game;
            void Send(string cmd) { _busy = true; g.Send(cmd, r => Done(r)); }
            void SendSwap(string cmd, GridView grid)
            {   // mesmo grid: se o destino tem um item do mesmo tamanho, ele vem para a mão depois da troca
                int other = grid.AnchorAt(cell);
                var ot = other >= 0 && other != from.Index ? grid.ItemAt(other) : null;
                _busy = true;
                g.Send(cmd, r => Done(r, ot != null ? new DropTag(grid.Zone, other) { Grid = grid } : null, ot, _originEl));
            }
            switch (from.Zone, to.Zone)
            {
                case (DragZone.Bag, DragZone.Bag): if (cell >= 0) SendSwap($"IMOVE {from.Index} {cell}", to.Grid!); break;
                case (DragZone.Vault, DragZone.Vault): if (cell >= 0) SendSwap($"VMOVE {from.Index} {cell}", to.Grid!); break;
                case (DragZone.Bag, DragZone.Vault): Send(cell >= 0 ? $"VPUT {from.Index} {it.Quantity} {cell}" : $"VPUT {from.Index}"); break;
                case (DragZone.Vault, DragZone.Bag): Send(cell >= 0 ? $"VTAKE {from.Index} {it.Quantity} {cell}" : $"VTAKE {from.Index}"); break;
                case (DragZone.Bag, DragZone.Equip): Send($"EQUIP {from.Index}"); break;
                case (DragZone.Equip, DragZone.Bag): Send($"UNEQUIP {from.Slot}"); break;
                case (DragZone.Bag, DragZone.TradeMine): Send($"TRADE ADD {from.Index}"); break;
                case (DragZone.TradeMine, DragZone.Bag): Send($"TRADE REMOVE {from.Index}"); break;
                case (DragZone.Bag, DragZone.Quick):
                    if (it.Def.IsPotion) Send($"QUICK {to.Index} {it.Def.Id}");
                    else g.State.AddLog("err", "Só consumíveis vão nos atalhos Q W E R.");
                    break;
                case (DragZone.Bag, DragZone.MyShop): _busy = true; g.Send($"SHOP ADD {from.Index} {g._shopPriceText.Trim()}", r => { Done(r); g.Send("SHOP MINE"); }); break;
                default: g.State.AddLog("err", "Não dá para colocar aí."); break;
            }
        }
    }
}
