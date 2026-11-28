using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.World;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Rótulos presos ao mundo em UI Toolkit: nomes com barra de vida, placa de loja, guilda, estandartes, ecos, drops,
    /// NPCs, portais e os números de dano (com contorno, maiores para crítico e combo). Reaproveita os elementos.
    /// </summary>
    public sealed class WorldLabels
    {
        private sealed class Plate
        {
            public VisualElement Root = null!; public Label Name = null!, Sub = null!, Shop = null!; public VisualElement ShopBox = null!; public KzBar Bar = null!;
            public bool Used;
        }

        private readonly UiRoot _ui;
        private readonly Dictionary<int, Plate> _plates = new Dictionary<int, Plate>();
        private readonly List<Plate> _free = new List<Plate>();
        private readonly List<Label> _floats = new List<Label>();
        private readonly List<Label> _fixed = new List<Label>();
        private KezorGame G => _ui.Game;
        private ClientState S => _ui.Game.State;

        public WorldLabels(UiRoot ui) { _ui = ui; }

        private Plate NewPlate()
        {
            if (_free.Count > 0) { var f = _free[_free.Count - 1]; _free.RemoveAt(_free.Count - 1); f.Root.Show(true); return f; }
            var p = new Plate();
            p.Root = Kz.El("wl-plate", _ui.WorldLayer);
            p.ShopBox = Kz.El("kz-panel-dark wl-shop", p.Root);
            p.Shop = Kz.Text("", "kz-gold kz-num", p.ShopBox);
            p.Sub = Kz.Text("", "wl-sub", p.Root);
            p.Name = Kz.Text("", "wl-name", p.Root);
            p.Bar = p.Root.Put(new KzBar("kz-bar--hp"));
            p.Bar.AddToClassList("wl-bar");
            return p;
        }

        public void Tick()
        {
            foreach (var p in _plates.Values) p.Used = false;
            var me = S.Me;
            foreach (var kv in G.Views)
            {
                var v = kv.Value;
                if (v == null || !S.Entities.TryGetValue(kv.Key, out var e)) continue;
                var pos = _ui.WorldToPanel(v.transform.position + Vector3.up * (v.Height + 0.35f));
                if (pos == null) continue;
                bool hover = kv.Key == G.HoverId || kv.Key == G.SelectedTarget;
                bool show = true; string name = "", sub = "", shop = ""; Color nameColor = new Color(0.94f, 0.9f, 0.82f);
                float bar = -1; string barMod = "kz-bar--hp";
                switch (e.Kind)
                {
                    case EntityKind.Drop:
                    {
                        var def = ItemDatabase.Get(e.TypeId);
                        nameColor = e.Rarity == ItemRarity.Ascended ? KezorLook.Hex("#6dff9e") : def != null && def.IsRune ? Models.RuneColor(def.Rune) : e.ItemLevel >= 7 ? KezorLook.Hex("#ffd36a") : KezorLook.Hex("#e8e2d0");
                        name = e.Name;
                        break;
                    }
                    case EntityKind.Banner:
                        nameColor = Models.BannerColor(e.TypeId);
                        name = $"Estandarte da {(e.TypeId == 1 ? "Fúria" : e.TypeId == 2 ? "Muralha" : "Vento")}";
                        sub = $"de {e.Name}";
                        pos = pos.Value + new Vector2(0, -(e.TypeId - 1) * 16);
                        break;
                    case EntityKind.Monster when e.OwnerId != 0:
                        nameColor = e.OwnerId == S.MyId ? KezorLook.Hex("#b8f0ff") : KezorLook.Hex("#8fe08f");
                        name = e.Name; sub = S.Entities.TryGetValue(e.OwnerId, out var oe) ? $"eco de {oe.Name}" : "";
                        bar = e.Hp / (float)Mathf.Max(1, e.MaxHp); barMod = "kz-bar--echo";
                        break;
                    case EntityKind.Monster:
                    {
                        if (!hover && e.Hp >= e.MaxHp) { show = false; break; }
                        var md = MonsterCatalog.Get(e.TypeId);
                        nameColor = md != null && md.Level > me.Level + 10 ? KezorLook.Hex("#ff6a6a") : md != null && md.Boss ? KezorLook.Hex("#ffa04a") : KezorLook.Hex("#efe6d4");
                        name = e.Name; sub = $"Nv {e.Level}";
                        bar = e.Hp / (float)Mathf.Max(1, e.MaxHp);
                        break;
                    }
                    default:
                    {
                        bool mine = kv.Key == S.MyId;
                        bool party = me.PartyId != 0 && e.PartyId == me.PartyId;
                        nameColor = mine ? KezorLook.Hex("#f2e6c8") : party ? KezorLook.Hex("#8fe08f") : e.Infamy >= 2 ? KezorLook.Hex("#ff5a5a") : e.Infamy == 1 ? KezorLook.Hex("#ff9a3c") : KezorLook.Hex("#9fc6ff");   // PK: aviso laranja, assassino vermelho
                        name = $"{e.Name}  <size=10><color=#b9ad97>Nv {e.Level}</color></size>";
                        if (e.Guild != "") sub = $"<color={(e.Guild == G.WarEnemyGuild ? "#ff6a5a" : "#8fd0ff")}>‹{e.Guild}›</color>";
                        shop = e.Shop;
                        if (!mine) { bar = e.Hp / (float)Mathf.Max(1, e.MaxHp); barMod = party ? "kz-bar--ally" : "kz-bar--hp"; }
                        break;
                    }
                }
                if (!show) continue;
                if (!_plates.TryGetValue(kv.Key, out var p)) { p = NewPlate(); _plates[kv.Key] = p; }
                p.Used = true;
                p.Root.style.left = pos.Value.x; p.Root.style.top = pos.Value.y - 52;
                p.Name.Set(name); p.Name.style.color = nameColor;
                p.Sub.Set(sub); p.Sub.Show(sub != "");
                p.ShopBox.Show(shop != ""); if (shop != "") p.Shop.Set(shop);
                p.Bar.Show(bar >= 0);
                if (bar >= 0)
                {
                    foreach (var m in new[] { "kz-bar--hp", "kz-bar--ally", "kz-bar--echo" }) p.Bar.SetClass(m, m == barMod);
                    p.Bar.Set(bar);
                }
            }
            var gone = new List<int>();
            foreach (var kv in _plates) if (!kv.Value.Used) gone.Add(kv.Key);
            foreach (var id in gone) { var p = _plates[id]; p.Root.Show(false); _free.Add(p); _plates.Remove(id); }

            // NPCs e portais
            int fi = 0;
            Label Fixed()
            {
                if (fi >= _fixed.Count) { var l = Kz.Text("", "wl-name wl-float", _ui.WorldLayer); l.style.fontSize = 13; _fixed.Add(l); }
                var r = _fixed[fi++]; r.Show(true); return r;
            }
            foreach (var (def, view) in G.Npcs)
            {
                if (view == null) continue;
                var pp = _ui.WorldToPanel(view.transform.position + Vector3.up * 2.55f);
                if (pp == null) continue;
                var l = Fixed(); l.Set(def.Name); l.style.color = KezorLook.Hex("#ffd36a");
                l.style.left = pp.Value.x; l.style.top = pp.Value.y - 10;
            }
            foreach (var portal in G.Map.Portals)
            {
                var pp = _ui.WorldToPanel(new Vector3(portal.X, WorldBuilder.Height(portal.X, portal.Z) + 3.4f, portal.Z));
                if (pp == null) continue;
                var l = Fixed(); l.Set($"» {portal.Label} «"); l.style.color = WorldBuilder.PortalColor(portal.TargetMap);
                l.style.left = pp.Value.x; l.style.top = pp.Value.y - 10;
            }
            for (int i = fi; i < _fixed.Count; i++) _fixed[i].Show(false);

            // números de dano e textos flutuantes
            int n = 0;
            foreach (var f in G.Fx.Floaters)
            {
                var pp = _ui.WorldToPanel(f.Pos);
                if (pp == null) continue;
                if (n >= _floats.Count) _floats.Add(Kz.Text("", "wl-float", _ui.WorldLayer));
                var l = _floats[n++];
                l.Show(true);
                l.Set(f.Text);
                // estoura no começo (pop) e some no fim
                float pop = f.T < 0.12f ? Mathf.Lerp(1.5f, 1f, f.T / 0.12f) : 1f;
                l.style.fontSize = Mathf.Round(19f * f.Size * pop);
                var c = f.Color; c.a = Mathf.Clamp01(1.4f - f.T * 1.25f);
                l.style.color = c;
                l.style.left = pp.Value.x; l.style.top = pp.Value.y - 14;
            }
            for (int i = n; i < _floats.Count; i++) _floats[i].Show(false);
        }
    }
}
