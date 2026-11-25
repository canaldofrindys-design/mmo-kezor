using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Kezor.Client;
using Kezor.Client.UI;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using R = Kezor.Client.UI.UiRaster;

namespace Kezor.EditorTools
{
    /// <summary>
    /// Gera os atlas de ícones (128 px por ícone) em Resources/UI/Icons:
    ///  • items.png: cada item com modelo é renderizado do próprio FBX (câmera ortográfica em 3/4, luz do jogo, contorno);
    ///    poções e runas, que não têm modelo, são desenhadas em 2D no mesmo estilo;
    ///  • skills.png: emblema facetado na cor da classe com símbolo do tipo de skill.
    /// Cada atlas tem um mapa de texto (nome x y w h) que o ItemIcons usa para criar os sprites em tempo de execução.
    /// </summary>
    public static class IconBaker
    {
        private const string Dir = "Assets/Resources/UI/Icons";
        private const int Cell = 128, Px = 96, AtlasW = 2048, Layer = 31;

        /// <summary>Ícone: pixels (linha 0 embaixo) e tamanho.</summary>
        private struct Icon { public string Name; public Color[] Pix; public int W, H; }

        [MenuItem("Kezor/Gerar ícones")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(Dir);
            var items = new List<Icon>();
            foreach (var d in ItemDatabase.All.OrderBy(d => d.Id))
            {
                var (w, h) = ItemSize.Of(d);
                var px = RenderItem(d, w * Px, h * Px);
                items.Add(new Icon { Name = "item_" + d.Id, Pix = px, W = w * Px, H = h * Px });
            }
            WriteAtlas("items", items);
            var skills = SkillCatalog.All.Where(s => s.Id != 0).Select(s => new Icon { Name = "skill_" + s.Id, Pix = Flip(SkillIcon(s).Px, Cell, Cell), W = Cell, H = Cell }).ToList();
            WriteAtlas("skills", skills);
            AssetDatabase.Refresh();
            foreach (var n in new[] { "items", "skills" })
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath($"{Dir}/{n}.png");
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Default;
                imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear; imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.npotScale = TextureImporterNPOTScale.None; imp.maxTextureSize = 8192;
                imp.SaveAndReimport();
            }
            Debug.Log($"[Kezor] ícones gerados: {items.Count} itens (no formato da grade) e {skills.Count} skills em {Dir}");
        }

        /// <summary>Empacota em prateleiras (mais altos primeiro) e grava o mapa de recortes em coordenadas de textura.</summary>
        private static void WriteAtlas(string name, List<Icon> icons)
        {
            var order = icons.OrderByDescending(i => i.H).ThenByDescending(i => i.W).ToList();
            var pos = new Dictionary<string, (int x, int y)>();
            int x = 0, y = 0, shelf = 0;
            foreach (var ic in order)
            {
                if (x + ic.W > AtlasW) { x = 0; y += shelf; shelf = 0; }
                pos[ic.Name] = (x, y);
                x += ic.W; shelf = Math.Max(shelf, ic.H);
            }
            int h = y + shelf;
            var tex = new Texture2D(AtlasW, h, TextureFormat.RGBA32, false);
            tex.SetPixels(new Color[AtlasW * h]);
            var map = new StringBuilder();
            foreach (var ic in order)
            {
                var (px, py) = pos[ic.Name];
                int ty = h - py - ic.H;                                   // textura: y cresce para cima
                tex.SetPixels(px, ty, ic.W, ic.H, ic.Pix);
                map.Append(ic.Name).Append(' ').Append(px).Append(' ').Append(ty).Append(' ').Append(ic.W).Append(' ').Append(ic.H).Append((char)10);
            }
            tex.Apply();
            File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
            File.WriteAllText($"{Dir}/{name}_map.txt", map.ToString());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static Color[] Flip(Color[] top, int w, int h)
        {   // UiRaster guarda a linha 0 no topo; Texture2D quer a linha 0 embaixo
            var r = new Color[top.Length];
            for (int y = 0; y < h; y++) Array.Copy(top, y * w, r, (h - 1 - y) * w, w);
            return r;
        }

        /// <summary>Reduz/centraliza um desenho 2D (128²) para W×H (linha 0 embaixo).</summary>
        private static Color[] Fit2D(R r, int w, int h)
        {
            var src = Flip(r.Px, Cell, Cell);
            var tex = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false); tex.SetPixels(src); tex.Apply();
            var px = new Color[w * h];
            int side = Math.Min(w, h); int ox = (w - side) / 2, oy = (h - side) / 2;
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    px[(y + oy) * w + x + ox] = tex.GetPixelBilinear((x + 0.5f) / side, (y + 0.5f) / side);
            UnityEngine.Object.DestroyImmediate(tex);
            return px;
        }

        // ------------------------------------------------------------------ itens 3D
        private static Color[] RenderItem(ItemDefinition d, int w, int h)
        {
            var paths = Gear.ModelPathsFor(d);
            if (paths.Count == 0) return Fit2D(Item2D(d), w, h);
            var root = new GameObject("IconRoot");
            try
            {
                bool pair = d.Kind == ItemKind.Gauntlet || d.Slot == EquipSlot.Pants || d.Slot == EquipSlot.Gloves || d.Slot == EquipSlot.Boots;
                foreach (var p in pair ? paths.Skip(paths.Count - 1) : paths) ModelLibrary.Instantiate(p, p, root.transform);
                var rs = root.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) return Fit2D(Item2D(d), w, h);
                if (d.IsWeapon || d.Kind == ItemKind.Shield) Orient(root, rs, d.Kind == ItemKind.Shield);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
                return Shoot(root, rs, d, w, h);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>Armas: eixo mais longo na vertical (a grade é vertical); escudos: face para a câmera.</summary>
        private static void Orient(GameObject root, Renderer[] rs, bool shield)
        {
            Bounds B() { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
            var sz = B().size;
            if (shield)
            {
                if (sz.x <= sz.y && sz.x <= sz.z) root.transform.rotation = Quaternion.Euler(0, 90, 0);
                else if (sz.y <= sz.x && sz.y <= sz.z) root.transform.rotation = Quaternion.Euler(90, 0, 0);
                return;
            }
            if (sz.z >= sz.x && sz.z >= sz.y) root.transform.rotation = Quaternion.Euler(-90, 0, 0);
            else if (sz.x >= sz.y && sz.x >= sz.z) root.transform.rotation = Quaternion.Euler(0, 0, 90);
        }

        private static Color[] Shoot(GameObject root, Renderer[] rs, ItemDefinition d, int W, int H)
        {
            Kezor.Client.DetailTextures.Ensure();
            Shader.SetGlobalVector("_KezorLowFog", Vector4.zero);
            Shader.SetGlobalVector("_KezorSunDir", (Quaternion.Euler(40f, -30f, 0) * Vector3.forward).normalized);
            Shader.SetGlobalColor("_KezorSunColor", new Color(1f, 0.9f, 0.75f));
            Shader.SetGlobalColor("_KezorSkyColor", new Color(0.55f, 0.58f, 0.66f));
            Shader.SetGlobalColor("_KezorGroundColor", new Color(0.25f, 0.21f, 0.19f));
            Shader.SetGlobalColor("_KezorFogColor", Color.black);
            Shader.SetGlobalVector("_KezorFogParams", new Vector4(5000f, 6000f, 0f, 0f));
            Shader.SetGlobalVector("_KezorSeeTarget", Vector4.zero);

            var camGo = new GameObject("IconCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layer;
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 100f;
            bool weapon = d.IsWeapon || d.Kind == ItemKind.Shield;
            var rot = Quaternion.Euler(d.IsMount ? 18f : weapon ? 6f : 14f, 180f + (d.IsWings ? 0f : weapon ? 22f : 28f), 0f);
            cam.transform.rotation = rot;
            float aspect = W / (float)H;

            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var inv = Quaternion.Inverse(rot);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var v = inv * (c - b.center);
                min = Vector2.Min(min, v); max = Vector2.Max(max, v);
            }
            var mid = (min + max) * 0.5f;
            cam.orthographicSize = Mathf.Max((max.y - min.y) * 0.5f, (max.x - min.x) * 0.5f / aspect) * 1.1f;
            cam.transform.position = b.center + rot * new Vector3(mid.x, mid.y, 0) - cam.transform.forward * (b.extents.magnitude * 2f + 2f);

            int SW = W * 2, SH = H * 2;
            var rt = RenderTexture.GetTemporary(SW, SH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
            cam.targetTexture = rt;
            cam.aspect = aspect;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var big = new Texture2D(SW, SH, TextureFormat.RGBA32, false);
            big.ReadPixels(new Rect(0, 0, SW, SH), 0, 0); big.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(camGo);

            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c = Color.clear;
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) c += big.GetPixel(x * 2 + dx, y * 2 + dy);
                    c /= 4f;
                    c.a = Mathf.Clamp01(c.a * 1.15f);
                    px[y * W + x] = c;
                }
            UnityEngine.Object.DestroyImmediate(big);
            return Outline(px, W, H);
        }

        private static Color[] Outline(Color[] px, int W, int H)
        {
            var r = (Color[])px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (px[y * W + x].a > 0.4f) continue;
                    float near = 0;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                            near = Mathf.Max(near, px[ny * W + nx].a * (1f - (Mathf.Abs(dx) + Mathf.Abs(dy)) * 0.18f));
                        }
                    if (near > 0.05f)
                    {
                        var o = new Color(0.02f, 0.01f, 0.01f, near * 0.85f);
                        var c = px[y * W + x];
                        r[y * W + x] = c.a <= 0.01f ? o : Color.Lerp(o, c, c.a);
                    }
                }
            return r;
        }

        // ------------------------------------------------------------------ itens 2D (poções, runas e o resto)
        private static readonly Color Bronze = R.Hex("#8a6a3e"), BronzeL = R.Hex("#c49a5a"), Amber = R.Hex("#ffb347"), Ember = R.Hex("#ff6a1a");

        private static R Item2D(ItemDefinition d)
        {
            var g = new R(Cell, Cell);
            if (d.IsPotion)
            {
                bool hp = d.Potion == PotionType.Health; bool big = d.PotionAmount >= 150;
                Color liquid = hp ? R.Hex("#e0301e") : R.Hex("#3a7aff"), dark = hp ? R.Hex("#6a0e06") : R.Hex("#10236a");
                float s = big ? 1.12f : 1f;
                V2 P(float x, float y) => new V2(64 + (x - 64) * s, 70 + (y - 70) * s);
                g.Poly(new[] { P(50, 14), P(78, 14), P(78, 26), P(50, 26) }, Bronze);
                g.Poly(new[] { P(52, 26), P(76, 26), P(76, 40), P(100, 62), P(100, 100), P(84, 114), P(44, 114), P(28, 100), P(28, 62), P(52, 40) }, new Color(0.8f, 0.86f, 0.95f, 0.5f));
                g.Poly(new[] { P(32, 66), P(96, 66), P(96, 98), P(82, 110), P(46, 110), P(32, 98) }, (x, y) => Color.Lerp(liquid, dark, (y - 66) / 44f));
                g.Poly(new[] { P(38, 70), P(46, 70), P(46, 98), P(38, 94) }, new Color(1, 1, 1, 0.45f));
                g.Polyline(new[] { P(52, 26), P(76, 26), P(76, 40), P(100, 62), P(100, 100), P(84, 114), P(44, 114), P(28, 100), P(28, 62), P(52, 40) }, 2.5f, R.Hex("#1a120c", 0.9f));
                if (big) g.Poly(new[] { P(64, 76), P(70, 86), P(64, 96), P(58, 86) }, new Color(1, 0.95f, 0.8f, 0.8f));
                return g;
            }
            if (d.IsRune)
            {   // gema facetada na cor da runa, com engaste de bronze
                var col = Models.RuneColor(d.Rune);
                var dark = Color.Lerp(col, Color.black, 0.55f); var light = Color.Lerp(col, Color.white, 0.45f);
                bool vortex = d.Rune == RuneType.Vortex;
                var outer = vortex ? Hex(64, 64, 50, 6) : new[] { new V2(64, 10), new V2(104, 52), new V2(64, 118), new V2(24, 52) };
                g.Poly(outer.Select(p => new V2(64 + (p.x - 64) * 1.08f, 64 + (p.y - 64) * 1.08f)).ToArray(), R.Hex("#1a120c", 0.95f));
                g.Poly(outer, dark);
                var top = vortex ? Hex(64, 60, 30, 6) : new[] { new V2(64, 18), new V2(92, 50), new V2(64, 62), new V2(36, 50) };
                g.Poly(top, (x, y) => Color.Lerp(light, col, (y - 18) / 46f));
                g.Poly(new[] { new V2(64, 62), new V2(92, 50), new V2(64, 108) }, Color.Lerp(col, dark, 0.3f));
                g.Poly(new[] { new V2(54, 26), new V2(64, 22), new V2(58, 40) }, new Color(1, 1, 1, 0.6f));
                return g;
            }
            if (d.IsJewel)
            {   // joias: cor da gema pelo atributo (vida vermelha, mana azul, resistência aço, dano brasa; amuletos dourado/violeta)
                Color gem = d.JewelHp > 0 && d.Kind == ItemKind.Ring ? R.Hex("#e0301e") : d.JewelMp > 0 && d.Kind == ItemKind.Ring ? R.Hex("#3a7aff")
                          : d.JewelResistPercent > 0 && d.Kind == ItemKind.Ring ? R.Hex("#9fb4c8") : d.Kind == ItemKind.Ring ? R.Hex("#ff8a2a")
                          : d.JewelDamagePercent > 0 ? R.Hex("#a77bff") : R.Hex("#ffd36a");
                var gemD = Color.Lerp(gem, Color.black, 0.5f); var gemL = Color.Lerp(gem, Color.white, 0.5f);
                var gold = d.Tier >= 2 ? R.Hex("#e8c27a") : BronzeL; var goldD = d.Tier >= 2 ? R.Hex("#8a6a3e") : Bronze;
                if (d.Kind == ItemKind.Ring)
                {
                    g.Polyline(R.Circle(64, 76, 30, 40), 15f, R.Hex("#1a120c", 0.95f), true);   // aro: contorno escuro e metal
                    g.Polyline(R.Circle(64, 76, 30, 40), 10f, goldD, true);
                    g.Polyline(R.Circle(64, 74, 30, 40), 4f, gold, true);
                    // engaste e gema no alto do aro
                    g.Poly(new[] { new V2(42, 44), new V2(86, 44), new V2(80, 30), new V2(48, 30) }, goldD);
                    g.Poly(new[] { new V2(64, 6), new V2(88, 30), new V2(64, 48), new V2(40, 30) }, R.Hex("#1a120c", 0.95f));
                    g.Poly(new[] { new V2(64, 10), new V2(84, 30), new V2(64, 44), new V2(44, 30) }, gemD);
                    g.Poly(new[] { new V2(64, 14), new V2(78, 28), new V2(64, 32), new V2(50, 28) }, (x, y) => Color.Lerp(gemL, gem, (y - 14) / 18f));
                    g.Poly(new[] { new V2(56, 20), new V2(62, 17), new V2(58, 26) }, new Color(1, 1, 1, 0.7f));
                }
                else
                {
                    g.Polyline(new Vector2[] { new V2(22, 12), new V2(30, 40), new V2(46, 62), new V2(64, 70), new V2(82, 62), new V2(98, 40), new V2(106, 12) }, 4f, goldD, false);
                    for (int i = 0; i < 7; i++) { float t = i / 6f; var cpt = new V2(Mathf.Lerp(26, 102, t), 16 + Mathf.Sin(t * Mathf.PI) * 50); g.Poly(R.Circle(cpt.x, cpt.y, 3.2f, 8), gold); }
                    g.Poly(new[] { new V2(64, 62), new V2(92, 88), new V2(64, 122), new V2(36, 88) }, R.Hex("#1a120c", 0.95f));
                    g.Poly(new[] { new V2(64, 66), new V2(88, 88), new V2(64, 118), new V2(40, 88) }, (x, y) => Color.Lerp(gold, goldD, (y - 66) / 52f));
                    g.Poly(new[] { new V2(64, 74), new V2(80, 88), new V2(64, 106), new V2(48, 88) }, gemD);
                    g.Poly(new[] { new V2(64, 78), new V2(76, 88), new V2(64, 94), new V2(52, 88) }, (x, y) => Color.Lerp(gemL, gem, (y - 78) / 16f));
                    g.Poly(new[] { new V2(58, 82), new V2(63, 80), new V2(60, 88) }, new Color(1, 1, 1, 0.7f));
                }
                return g;
            }
            // genérico: baú pequeno
            g.Poly(R.Bevel(20, 40, 108, 108, 8), (x, y) => Color.Lerp(R.Hex("#6a4a2a"), R.Hex("#3a2616"), (y - 40) / 68f));
            g.Poly(R.Bevel(20, 30, 108, 56, 6), BronzeL);
            g.Poly(new[] { new V2(58, 52), new V2(70, 52), new V2(70, 68), new V2(58, 68) }, Amber);
            return g;
        }

        private struct V2
        {
            public float x, y;
            public V2(float x, float y) { this.x = x; this.y = y; }
            public static implicit operator Vector2(V2 v) => new Vector2(v.x, v.y);
        }

        private static void Poly(this R g, V2[] p, Color c) => g.Poly(p.Select(v => (Vector2)v).ToArray(), c);
        private static void Poly(this R g, V2[] p, Func<float, float, Color> f) => g.Poly(p.Select(v => (Vector2)v).ToArray(), f);
        private static void Polyline(this R g, V2[] p, float w, Color c) => g.Polyline(p.Select(v => (Vector2)v).ToArray(), w, c);

        private static V2[] Hex(float cx, float cy, float r, int n)
        {
            var p = new V2[n];
            for (int i = 0; i < n; i++) { float a = -Mathf.PI / 2 + i * Mathf.PI * 2 / n; p[i] = new V2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r); }
            return p;
        }

        // ------------------------------------------------------------------ skills 2D
        private static Color ClassColor(ClassMask m) => m switch
        {
            ClassMask.Warden => R.Hex("#3d5a80"), ClassMask.Arcanist => R.Hex("#5a3580"), ClassMask.Ranger => R.Hex("#3f6b3a"),
            ClassMask.Bladeweaver => R.Hex("#23646f"), ClassMask.Commander => R.Hex("#7a2a22"), ClassMask.Invoker => R.Hex("#2e2e6a"),
            ClassMask.EmberFist => R.Hex("#8a3a10"), _ => R.Hex("#444444"),
        };

        private static Color Accent(SkillDefinition s)
        {
            if (s.Kind == SkillKind.Banner) return Models.BannerColor((int)s.Buff);
            if (s.Kind == SkillKind.Summon) return R.Hex("#d0b0ff");
            if (s.SlowPercent > 0 || s.DotPercent > 0) return R.Hex("#b8ff8a");
            if (s.Hybrid) return R.Hex("#8fe8ff");
            if (s.Magic) return R.Hex("#ffb36a");
            if (s.Classes == ClassMask.EmberFist) return R.Hex("#ffa04a");
            return R.Hex("#f2ead8");
        }

        private static R SkillIcon(SkillDefinition s)
        {
            var g = new R(Cell, Cell);
            var bg = ClassColor(s.Classes); var acc = Accent(s);
            // fundo: losango facetado com brilho radial
            g.Poly(R.Bevel(2, 2, 126, 126, 14), (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(54, 46)) / 100f;
                return Color.Lerp(Color.Lerp(bg, Color.white, 0.18f), Color.Lerp(bg, Color.black, 0.65f), d);
            });
            g.Poly(new[] { new Vector2(2, 16), new Vector2(16, 2), new Vector2(80, 2), new Vector2(2, 70) }, new Color(1, 1, 1, 0.05f));
            if (s.ReqLevel >= 80) Burst(g, 64, 64, 56, 12, new Color(acc.r, acc.g, acc.b, 0.28f));        // skill final: explosão atrás
            if (s.AreaRadius > 0) g.Ring(64, 64, 44, 50, (x, y) => new Color(acc.r, acc.g, acc.b, 0.75f), 48);
            var dark = R.Hex("#120c08", 0.9f);
            switch (s.Kind)
            {
                case SkillKind.SelfBuff: Chevrons(g, acc, dark); break;
                case SkillKind.Banner: Flag(g, acc, dark); break;
                case SkillKind.Summon: Ghost(g, acc, dark, s.SummonId == 61); break;
                default:
                    if (s.SlowPercent > 0 && s.DotPercent <= 0 && s.Magic) Chains(g, acc, dark);
                    else if (s.DotPercent > 0) Spiral(g, acc);
                    else if (s.Hits > 1) Triple(g, acc, dark);
                    else ClassGlyph(g, s, acc, dark);
                    break;
            }
            g.Polyline(R.Bevel(2.5f, 2.5f, 125.5f, 125.5f, 13.6f), 2f, new Color(0, 0, 0, 0.55f));
            return g;
        }

        private static void ClassGlyph(R g, SkillDefinition s, Color acc, Color dark)
        {
            switch (s.Classes)
            {
                case ClassMask.Ranger: Arrow(g, acc, dark); break;
                case ClassMask.Arcanist: case ClassMask.Invoker: Orb(g, acc, dark); break;
                case ClassMask.Commander: Lance(g, acc, dark); break;
                case ClassMask.EmberFist: Fist(g, acc, dark); break;
                default: Sword(g, acc, dark, s.Hybrid); break;
            }
        }

        private static Vector2[] Rot(Vector2[] p, float deg, float cx = 64, float cy = 64)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            return p.Select(v => new Vector2(cx + (v.x - cx) * c - (v.y - cy) * sn, cy + (v.x - cx) * sn + (v.y - cy) * c)).ToArray();
        }

        private static void Shape(R g, Vector2[] p, Color fill, Color dark, float deg = 0)
        {
            var q = Rot(p, deg);
            g.Polyline(q, 5f, dark); g.Poly(q, fill);
        }

        private static void Sword(R g, Color acc, Color dark, bool rune)
        {
            var blade = new[] { new Vector2(60, 14), new Vector2(68, 14), new Vector2(70, 82), new Vector2(64, 92), new Vector2(58, 82) };
            Shape(g, blade, Color.Lerp(acc, Color.white, 0.35f), dark, 45);
            Shape(g, new[] { new Vector2(44, 84), new Vector2(84, 84), new Vector2(84, 92), new Vector2(44, 92) }, R.Hex("#c49a5a"), dark, 45);
            Shape(g, new[] { new Vector2(60, 92), new Vector2(68, 92), new Vector2(68, 112), new Vector2(60, 112) }, R.Hex("#5a3a22"), dark, 45);
            if (rune) for (int i = 0; i < 3; i++) g.Poly(Rot(new[] { new Vector2(62, 30 + i * 16), new Vector2(66, 30 + i * 16), new Vector2(66, 38 + i * 16), new Vector2(62, 38 + i * 16) }, 45), acc);
        }

        private static void Arrow(R g, Color acc, Color dark)
        {
            Shape(g, new[] { new Vector2(62, 24), new Vector2(66, 24), new Vector2(66, 104), new Vector2(62, 104) }, R.Hex("#c9b08a"), dark, 45);
            Shape(g, new[] { new Vector2(64, 8), new Vector2(76, 28), new Vector2(52, 28) }, acc, dark, 45);
            Shape(g, new[] { new Vector2(56, 96), new Vector2(64, 104), new Vector2(72, 96), new Vector2(72, 116), new Vector2(64, 108), new Vector2(56, 116) }, R.Hex("#e0402a"), dark, 45);
        }

        private static void Orb(R g, Color acc, Color dark)
        {
            g.Poly(R.Circle(64, 64, 30, 32), dark);
            g.Poly(R.Circle(64, 64, 26, 32), (x, y) => Color.Lerp(Color.white, acc, Vector2.Distance(new Vector2(x, y), new Vector2(56, 54)) / 34f));
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.094f;
                g.Line(new Vector2(64 + Mathf.Cos(a) * 34, 64 + Mathf.Sin(a) * 34), new Vector2(64 + Mathf.Cos(a + 0.6f) * 44, 64 + Mathf.Sin(a + 0.6f) * 44), 4f, new Color(acc.r, acc.g, acc.b, 0.8f));
            }
        }

        private static void Lance(R g, Color acc, Color dark)
        {
            Shape(g, new[] { new Vector2(61, 34), new Vector2(67, 34), new Vector2(67, 116), new Vector2(61, 116) }, R.Hex("#7a5a3a"), dark, 40);
            Shape(g, new[] { new Vector2(64, 6), new Vector2(76, 40), new Vector2(64, 46), new Vector2(52, 40) }, Color.Lerp(acc, Color.white, 0.3f), dark, 40);
        }

        private static void Fist(R g, Color acc, Color dark)
        {
            var fist = new[] { new Vector2(38, 46), new Vector2(86, 40), new Vector2(94, 52), new Vector2(92, 82), new Vector2(76, 96), new Vector2(46, 96), new Vector2(34, 80) };
            Shape(g, fist, R.Hex("#c47a4a"), dark);
            for (int i = 0; i < 3; i++) g.Line(new Vector2(50 + i * 12, 46), new Vector2(50 + i * 12, 62), 3f, dark);
            g.Poly(new[] { new Vector2(46, 96), new Vector2(76, 96), new Vector2(74, 112), new Vector2(48, 112) }, R.Hex("#5a3a22"));
            Burst(g, 96, 34, 18, 7, acc);
        }

        private static void Triple(R g, Color acc, Color dark)
        {
            for (int i = -1; i <= 1; i++)
            {
                var p = new[] { new Vector2(30, 60 + i * 22), new Vector2(98, 48 + i * 22), new Vector2(98, 56 + i * 22), new Vector2(30, 66 + i * 22) };
                g.Polyline(p, 4f, dark); g.Poly(p, Color.Lerp(acc, Color.white, 0.25f));
            }
        }

        private static void Chevrons(R g, Color acc, Color dark)
        {
            for (int i = 0; i < 3; i++)
            {
                float y = 34 + i * 24;
                var p = new[] { new Vector2(64, y), new Vector2(94, y + 22), new Vector2(84, y + 30), new Vector2(64, y + 14), new Vector2(44, y + 30), new Vector2(34, y + 22) };
                g.Polyline(p, 4f, dark); g.Poly(p, Color.Lerp(acc, Color.white, i * 0.2f));
            }
        }

        private static void Flag(R g, Color acc, Color dark)
        {
            Shape(g, new[] { new Vector2(34, 14), new Vector2(42, 14), new Vector2(42, 116), new Vector2(34, 116) }, R.Hex("#7a5a3a"), dark);
            Shape(g, new[] { new Vector2(42, 20), new Vector2(100, 20), new Vector2(100, 72), new Vector2(72, 62), new Vector2(42, 72) }, acc, dark);
            g.Poly(new[] { new Vector2(62, 34), new Vector2(80, 34), new Vector2(80, 52), new Vector2(62, 52) }, R.Hex("#d4a94a"));
        }

        private static void Ghost(R g, Color acc, Color dark, bool light)
        {
            var body = new List<Vector2>();
            for (int i = 0; i <= 16; i++) { float a = Mathf.PI + i * Mathf.PI / 16f; body.Add(new Vector2(64 + Mathf.Cos(a) * 30, 54 + Mathf.Sin(a) * 30)); }
            body.AddRange(new[] { new Vector2(94, 110), new Vector2(82, 100), new Vector2(72, 112), new Vector2(64, 100), new Vector2(56, 112), new Vector2(46, 100), new Vector2(34, 110) });
            var p = body.ToArray();
            g.Polyline(p, 5f, dark);
            g.Poly(p, light ? Color.Lerp(acc, Color.white, 0.55f) : Color.Lerp(acc, R.Hex("#2a2440"), 0.55f));
            g.Poly(R.Circle(53, 58, 6, 12), light ? R.Hex("#2a5a8a") : acc); g.Poly(R.Circle(75, 58, 6, 12), light ? R.Hex("#2a5a8a") : acc);
        }

        private static void Spiral(R g, Color acc)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < 90; i++) { float t = i / 89f; float a = t * Mathf.PI * 5f; float r = 6 + t * 40; pts.Add(new Vector2(64 + Mathf.Cos(a) * r, 64 + Mathf.Sin(a) * r)); }
            g.Polyline(pts.ToArray(), 9f, R.Hex("#120c08", 0.9f), false);
            g.Polyline(pts.ToArray(), 5f, acc, false);
        }

        private static void Chains(R g, Color acc, Color dark)
        {
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector2(36 + i * 19, 36 + i * 19);
                g.Ring(c.x, c.y, 7, 12, (x, y) => i % 2 == 0 ? acc : Color.Lerp(acc, Color.black, 0.3f), 20);
            }
            g.Poly(new[] { new Vector2(84, 92), new Vector2(110, 92), new Vector2(104, 114), new Vector2(90, 114) }, Color.Lerp(acc, Color.black, 0.4f));
        }

        private static void Burst(R g, float cx, float cy, float r, int n, Color c)
        {
            var p = new Vector2[n * 2];
            for (int i = 0; i < n * 2; i++) { float a = i * Mathf.PI / n; float rr = i % 2 == 0 ? r : r * 0.45f; p[i] = new Vector2(cx + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr); }
            g.Poly(p, c);
        }
    }
}
