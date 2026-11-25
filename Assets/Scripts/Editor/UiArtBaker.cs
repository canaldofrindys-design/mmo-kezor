using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Kezor.Client.UI;
using R = Kezor.Client.UI.UiRaster;

namespace Kezor.EditorTools
{
    /// <summary>
    /// Gera a arte da interface (Resources/UI/Art/*.png) desenhada em código com o estilo do Kezor: carvão, metal
    /// envelhecido e acento de brasa, cantos chanfrados (low poly). Rode de novo pelo menu se mudar a paleta.
    /// O 9-slice de cada peça fica no USS (-unity-slice-*).
    /// </summary>
    public static class UiArtBaker
    {
        private const string Dir = "Assets/Resources/UI/Art";

        // paleta (igual à do docs/UI_GUIDE.md)
        private static readonly Color Coal0 = R.Hex("#0d0b0c"), Coal1 = R.Hex("#16120f"), Coal2 = R.Hex("#241c16"), Coal3 = R.Hex("#33281f");
        private static readonly Color IronD = R.Hex("#2b241f"), Iron = R.Hex("#4a3f36"), IronL = R.Hex("#6e6052");
        private static readonly Color BronzeD = R.Hex("#4e3b24"), Bronze = R.Hex("#8a6a3e"), BronzeL = R.Hex("#c49a5a");
        private static readonly Color Ember = R.Hex("#ff6a1a"), Amber = R.Hex("#ffb347");

        [MenuItem("Kezor/Gerar arte da UI")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(Dir);
            var made = new List<string>();
            void Save(string name, R r) { File.WriteAllBytes($"{Dir}/{name}.png", r.ToTexture().EncodeToPNG()); made.Add(name); }

            Save("panel", Ornate(Panel(96, 96, 9, Coal2, Coal1, 0.95f, BronzeL, BronzeD), 96, 96));
            Save("panel_dark", Panel(64, 64, 6, Coal1, Coal0, 0.92f, Bronze, BronzeD));
            Save("tooltip", Tooltip());
            Save("header", Header());
            Save("btn", Button(Iron, IronD, Bronze, BronzeD, 0f));
            Save("btn_hover", Button(IronL, Iron, BronzeL, Bronze, 0.18f));
            Save("btn_down", Button(IronD, Iron, Amber, Bronze, 0.08f));
            Save("btn_off", Button(R.Hex("#2a2724"), R.Hex("#1e1c1a"), R.Hex("#403a33"), R.Hex("#2a2622"), 0f));
            Save("btn_ember", Button(R.Hex("#7a3216"), R.Hex("#3d1a0e"), Amber, R.Hex("#8a4a1e"), 0.25f));
            Save("slot", Slot(Bronze, BronzeD, 0f));
            Save("slot_hover", Slot(BronzeL, Bronze, 0.12f));
            Save("slot_sel", Slot(Amber, R.Hex("#c4661a"), 0.3f));
            Save("bar_frame", BarFrame());
            Save("bar_fill", BarFill());
            Save("orb_frame", OrbFrame());
            Save("orb_glass", OrbGlass());
            Save("minimap_frame", MinimapFrame());
            Save("plate", Plate());
            Save("tab", Tab(false));
            Save("tab_on", Tab(true));
            Save("field", Field());
            Save("divider", Divider());
            Save("toast", Toast());
            Save("vignette", Vignette());
            foreach (var (name, draw) in Glyphs) { var g = new R(64, 64); draw(g); Save("g_" + name, g); }
            Save("silhouette", Silhouette());
            Save("hudbar", HudBar());
            Save("crest", Crest());
            Save("bar_crest", BarCrest());
            Save("orb_frame2", OrbFrame2());
            Save("cradle", Cradle());

            AssetDatabase.Refresh();
            foreach (var n in made)
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath($"{Dir}/{n}.png");
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Default;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.SaveAndReimport();
            }
            Debug.Log($"[Kezor] arte da UI gerada: {made.Count} peças em {Dir}");
        }

        // ------------------------------------------------------------------ peças
        /// <summary>Moldura facetada: fundo em gradiente com facetas sutis, aro de bronze com luz no alto e sombra embaixo.</summary>
        private static R Panel(int w, int h, float cut, Color top, Color bottom, float alpha, Color rimL, Color rimD)
        {
            var r = new R(w, h);
            r.Poly(R.Bevel(0, 0, w, h, cut), R.Hex("#050404", 0.95f));                                 // contorno escuro
            r.Poly(R.Bevel(1, 1, w - 1, h - 1, cut - 0.4f), R.VGrad(1, h - 1, rimL, rimD));          // aro de bronze
            var inner = R.Bevel(3, 3, w - 3, h - 3, cut - 1.2f);
            var t = top; t.a = alpha; var b = bottom; b.a = alpha;
            r.Poly(inner, R.VGrad(3, h - 3, t, b));
            // facetas low poly (triângulos levemente mais claros/escuros)
            r.Poly(new[] { new Vector2(3, 3), new Vector2(w * 0.55f, 3), new Vector2(3, h * 0.5f) }, new Color(1, 1, 1, 0.025f));
            r.Poly(new[] { new Vector2(w - 3, h - 3), new Vector2(w * 0.4f, h - 3), new Vector2(w - 3, h * 0.45f) }, new Color(0, 0, 0, 0.12f));
            r.Polyline(R.Bevel(3.5f, 3.5f, w - 3.5f, h - 3.5f, cut - 1.4f), 1f, new Color(0, 0, 0, 0.55f));  // filete interno
            r.Line(new Vector2(cut + 1, 3.6f), new Vector2(w - cut - 1, 3.6f), 1f, new Color(1, 0.85f, 0.6f, 0.08f));
            return r;
        }

        private static R Tooltip()
        {
            var r = Panel(64, 64, 5, R.Hex("#1a1411"), R.Hex("#0c0a09"), 0.97f, Bronze, BronzeD);
            r.Line(new Vector2(6, 2), new Vector2(58, 2), 2f, Amber);
            return r;
        }

        private static R Header()
        {   // faixa do título da janela: metal escuro com filete de brasa embaixo e brilho subindo
            var r = new R(64, 30);
            r.Poly(new[] { new Vector2(0, 0), new Vector2(64, 0), new Vector2(64, 30), new Vector2(0, 30) }, R.VGrad(0, 30, R.Hex("#3a2c20", 0.98f), R.Hex("#1b1410", 0.98f)));
            for (int i = 0; i < 6; i++) r.Line(new Vector2(0, 28 - i), new Vector2(64, 28 - i), 1f, new Color(Ember.r, Ember.g, Ember.b, 0.05f * (6 - i) / 6f));
            r.Line(new Vector2(0, 29), new Vector2(64, 29), 1.4f, new Color(Ember.r, Ember.g, Ember.b, 0.85f));
            r.Line(new Vector2(0, 0.5f), new Vector2(64, 0.5f), 1f, new Color(1, 0.85f, 0.6f, 0.12f));
            return r;
        }

        private static R Button(Color top, Color bottom, Color rimL, Color rimD, float glow)
        {
            var r = new R(64, 28);
            r.Poly(R.Bevel(0, 0, 64, 28, 5), R.Hex("#050404", 0.95f));
            r.Poly(R.Bevel(1, 1, 63, 27, 4.6f), R.VGrad(1, 27, rimL, rimD));
            r.Poly(R.Bevel(2.5f, 2.5f, 61.5f, 25.5f, 3.6f), R.VGrad(2, 26, top, bottom));
            r.Line(new Vector2(6, 3.5f), new Vector2(58, 3.5f), 1f, new Color(1, 0.9f, 0.7f, 0.10f));
            if (glow > 0)
                r.Poly(R.Bevel(2.5f, 14, 61.5f, 25.5f, 3.6f), R.VGrad(14, 26, new Color(Ember.r, Ember.g, Ember.b, 0), new Color(Ember.r, Ember.g, Ember.b, glow)));
            return r;
        }

        private static R Slot(Color rimL, Color rimD, float glow)
        {   // poço escuro com sombra interna e aro fino
            var r = new R(64, 64);
            r.Poly(R.Bevel(0, 0, 64, 64, 6), R.Hex("#050404", 0.95f));
            r.Poly(R.Bevel(1, 1, 63, 63, 5.6f), R.VGrad(1, 63, rimL, rimD));
            r.Poly(R.Bevel(3, 3, 61, 61, 4.4f), (x, y) =>
            {
                float d = Mathf.Max(Mathf.Abs(x - 32), Mathf.Abs(y - 32)) / 29f;
                var c = Color.Lerp(R.Hex("#1c1612"), R.Hex("#080706"), d * d);
                c.a = 0.96f;
                return c;
            });
            for (int i = 0; i < 4; i++) r.Line(new Vector2(6, 4.5f + i), new Vector2(58, 4.5f + i), 1f, new Color(0, 0, 0, 0.18f - i * 0.04f));
            if (glow > 0)
                r.Polyline(R.Bevel(3.5f, 3.5f, 60.5f, 60.5f, 4.2f), 2f, new Color(Amber.r, Amber.g, Amber.b, glow));
            return r;
        }

        private static R BarFrame()
        {
            var r = new R(64, 16);
            r.Poly(R.Bevel(0, 0, 64, 16, 3), R.Hex("#050404", 0.95f));
            r.Poly(R.Bevel(1, 1, 63, 15, 2.6f), R.VGrad(1, 15, Bronze, BronzeD));
            r.Poly(R.Bevel(2.5f, 2.5f, 61.5f, 13.5f, 1.8f), R.Hex("#0a0807", 0.92f));
            return r;
        }

        private static R BarFill()
        {   // branco com brilho em cima; o USS pinta com a cor da barra
            var r = new R(8, 16);
            r.Poly(new[] { new Vector2(0, 0), new Vector2(8, 0), new Vector2(8, 16), new Vector2(0, 16) },
                (x, y) => y < 5 ? Color.Lerp(new Color(1, 1, 1), new Color(0.9f, 0.9f, 0.9f), y / 5f) : Color.Lerp(new Color(0.85f, 0.85f, 0.85f), new Color(0.55f, 0.55f, 0.55f), (y - 5) / 11f));
            return r;
        }

        /// <summary>Anel das orbes: 16 facetas de metal envelhecido alternando luz, lábio escuro e 4 gemas de brasa.</summary>
        private static R OrbFrame()
        {
            const int S = 256; const float c = 128;
            var r = new R(S, S);
            r.Ring(c, c, 104, 127, (x, y) =>
            {
                float a = Mathf.Atan2(y - c, x - c);
                int facet = Mathf.FloorToInt((a + Mathf.PI) / (Mathf.PI * 2f) * 16f);
                float light = 0.5f + 0.5f * Mathf.Cos(a + 2.2f);                // luz vindo do alto à esquerda
                var col = Color.Lerp(BronzeD, BronzeL, light * 0.85f);
                if (facet % 2 == 0) col = Color.Lerp(col, IronD, 0.25f);
                return col;
            }, 16);
            r.Ring(c, c, 101, 106, (x, y) => R.Hex("#050404", 0.9f), 96);
            r.Ring(c, c, 126, 128, (x, y) => R.Hex("#050404", 0.9f), 96);
            foreach (float deg in new[] { 45f, 135f, 225f, 315f })
            {
                float a = deg * Mathf.Deg2Rad; var p = new Vector2(c + Mathf.Cos(a) * 115, c + Mathf.Sin(a) * 115);
                var gem = new[] { p + new Vector2(0, -11), p + new Vector2(8, 0), p + new Vector2(0, 11), p + new Vector2(-8, 0) };
                r.Poly(gem, R.Hex("#1a0c06"));
                r.Poly(new[] { p + new Vector2(0, -8), p + new Vector2(5.5f, 0), p + new Vector2(0, 8), p + new Vector2(-5.5f, 0) },
                       (x, y) => Color.Lerp(Amber, Ember, Mathf.InverseLerp(p.y - 8, p.y + 8, y)));
                r.Poly(new[] { p + new Vector2(0, -7), p + new Vector2(3, -1), p + new Vector2(-2, -1) }, new Color(1, 1, 0.9f, 0.7f));
            }
            return r;
        }

        private static R OrbGlass()
        {
            const int S = 256; const float c = 128;
            var r = new R(S, S);
            r.Poly(R.Circle(c - 26, c - 34, 58, 48), (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c - 40, c - 52)) / 64f;
                return new Color(1, 1, 1, Mathf.Clamp01(0.32f - d * 0.34f));
            });
            r.Ring(c, c, 92, 102, (x, y) => new Color(0, 0, 0, Mathf.Clamp01((y - c) / 102f) * 0.45f), 96);
            r.Ring(c, c, 96, 102, (x, y) => new Color(1, 0.9f, 0.75f, Mathf.Clamp01((c - y) / 102f) * 0.10f), 96);
            return r;
        }

        private static R MinimapFrame()
        {
            const int S = 256; const float c = 128;
            var r = new R(S, S);
            r.Ring(c, c, 114, 127, (x, y) =>
            {
                float a = Mathf.Atan2(y - c, x - c);
                float light = 0.5f + 0.5f * Mathf.Cos(a + 2.2f);
                return Color.Lerp(BronzeD, BronzeL, light * 0.8f);
            }, 24);
            r.Ring(c, c, 111, 115, (x, y) => R.Hex("#050404", 0.9f), 96);
            // marcador do norte
            r.Poly(new[] { new Vector2(c, 2), new Vector2(c + 11, 20), new Vector2(c - 11, 20) }, R.Hex("#050404"));
            r.Poly(new[] { new Vector2(c, 5), new Vector2(c + 8, 18), new Vector2(c - 8, 18) }, (x, y) => Color.Lerp(Amber, Ember, (y - 5) / 13f));
            // marcas a cada 45°
            for (int i = 0; i < 8; i++)
            {
                if (i == 6) continue;   // topo (y para baixo: 270° é o alto)
                float a = i * Mathf.PI / 4f;
                r.Line(new Vector2(c + Mathf.Cos(a) * 116, c + Mathf.Sin(a) * 116), new Vector2(c + Mathf.Cos(a) * 125, c + Mathf.Sin(a) * 125), 2.2f, R.Hex("#1a120c"));
            }
            return r;
        }

        private static R Plate()
        {   // base da barra inferior: chapa larga com pontas anguladas e friso de bronze
            var r = new R(256, 96);
            var shape = new[] { new Vector2(24, 8), new Vector2(232, 8), new Vector2(256, 32), new Vector2(256, 96), new Vector2(0, 96), new Vector2(0, 32) };
            r.Poly(shape, R.Hex("#050404", 0.95f));
            var rim = new[] { new Vector2(24.5f, 9), new Vector2(231.5f, 9), new Vector2(255, 32.5f), new Vector2(255, 96), new Vector2(1, 96), new Vector2(1, 32.5f) };
            r.Poly(rim, R.VGrad(9, 40, BronzeL, BronzeD));
            var fill = new[] { new Vector2(26, 12), new Vector2(230, 12), new Vector2(252, 34), new Vector2(252, 96), new Vector2(4, 96), new Vector2(4, 34) };
            r.Poly(fill, R.VGrad(12, 96, R.Hex("#2a2019", 0.96f), R.Hex("#0e0b09", 0.96f)));
            r.Poly(new[] { new Vector2(26, 12), new Vector2(150, 12), new Vector2(4, 60), new Vector2(4, 34) }, new Color(1, 1, 1, 0.025f));
            r.Line(new Vector2(28, 13), new Vector2(228, 13), 1f, new Color(Ember.r, Ember.g, Ember.b, 0.5f));
            return r;
        }

        private static R Tab(bool on)
        {
            var r = new R(48, 26);
            var shape = new[] { new Vector2(5, 0), new Vector2(43, 0), new Vector2(48, 5), new Vector2(48, 26), new Vector2(0, 26), new Vector2(0, 5) };
            r.Poly(shape, R.Hex("#050404", 0.95f));
            r.Poly(new[] { new Vector2(5.4f, 1), new Vector2(42.6f, 1), new Vector2(47, 5.4f), new Vector2(47, 26), new Vector2(1, 26), new Vector2(1, 5.4f) },
                   R.VGrad(1, 26, on ? BronzeL : Bronze, on ? Bronze : BronzeD));
            r.Poly(new[] { new Vector2(6, 2.5f), new Vector2(42, 2.5f), new Vector2(45.5f, 6), new Vector2(45.5f, 26), new Vector2(2.5f, 26), new Vector2(2.5f, 6) },
                   R.VGrad(2, 26, on ? R.Hex("#3d2e20") : R.Hex("#221a14"), on ? R.Hex("#241a12") : R.Hex("#140f0c")));
            if (on) r.Line(new Vector2(6, 3.5f), new Vector2(42, 3.5f), 1.4f, Amber);
            return r;
        }

        private static R Field()
        {
            var r = new R(32, 24);
            r.Poly(R.Bevel(0, 0, 32, 24, 3), R.VGrad(0, 24, BronzeD, Bronze));
            r.Poly(R.Bevel(1.5f, 1.5f, 30.5f, 22.5f, 2.2f), R.Hex("#0b0908", 0.96f));
            r.Line(new Vector2(4, 3), new Vector2(28, 3), 1f, new Color(0, 0, 0, 0.4f));
            return r;
        }

        private static R Divider()
        {   // filete com losango de brasa no meio
            var r = new R(128, 12);
            r.Poly(new[] { new Vector2(0, 6), new Vector2(56, 5), new Vector2(56, 7) }, R.Hex("#8a6a3e", 0.8f));
            r.Poly(new[] { new Vector2(128, 6), new Vector2(72, 5), new Vector2(72, 7) }, R.Hex("#8a6a3e", 0.8f));
            r.Poly(new[] { new Vector2(64, 0.5f), new Vector2(70, 6), new Vector2(64, 11.5f), new Vector2(58, 6) }, R.Hex("#050404"));
            r.Poly(new[] { new Vector2(64, 2.5f), new Vector2(68, 6), new Vector2(64, 9.5f), new Vector2(60, 6) }, (x, y) => Color.Lerp(Amber, Ember, (y - 2.5f) / 7f));
            return r;
        }

        private static R Toast()
        {
            var r = new R(64, 40);
            var shape = new[] { new Vector2(10, 0), new Vector2(54, 0), new Vector2(64, 20), new Vector2(54, 40), new Vector2(10, 40), new Vector2(0, 20) };
            r.Poly(shape, R.Hex("#050404", 0.92f));
            r.Poly(new[] { new Vector2(10.6f, 1.2f), new Vector2(53.4f, 1.2f), new Vector2(62.6f, 20), new Vector2(53.4f, 38.8f), new Vector2(10.6f, 38.8f), new Vector2(1.4f, 20) }, R.VGrad(0, 40, Bronze, BronzeD));
            r.Poly(new[] { new Vector2(11.5f, 3), new Vector2(52.5f, 3), new Vector2(60.5f, 20), new Vector2(52.5f, 37), new Vector2(11.5f, 37), new Vector2(3.5f, 20) }, R.VGrad(3, 37, R.Hex("#2a1f17", 0.95f), R.Hex("#120d0a", 0.95f)));
            return r;
        }

        /// <summary>Silhueta facetada do herói (frente) para o painel de equipamento, com halo de brasa embaixo.</summary>
        private static R Silhouette()
        {
            var r = new R(220, 330);
            var dark = R.Hex("#1a1411", 0.95f);
            Func<float, float, Color> body = (x, y) => Color.Lerp(R.Hex("#3a2e24", 0.9f), R.Hex("#1c1612", 0.9f), y / 330f);
            r.Poly(R.Circle(110, 322, 70, 40), (x, y) => new Color(Ember.r, Ember.g, Ember.b, Mathf.Clamp01(0.22f - Vector2.Distance(new Vector2(x, y * 3.2f), new Vector2(110, 322 * 3.2f)) / 320f)));
            r.Poly(new[] { new Vector2(110, 18), new Vector2(132, 30), new Vector2(136, 58), new Vector2(124, 80), new Vector2(96, 80), new Vector2(84, 58), new Vector2(88, 30) }, body);   // cabeça
            r.Poly(new[] { new Vector2(98, 80), new Vector2(122, 80), new Vector2(124, 94), new Vector2(96, 94) }, body);                                                                    // pescoço
            r.Poly(new[] { new Vector2(62, 96), new Vector2(158, 96), new Vector2(170, 116), new Vector2(150, 200), new Vector2(70, 200), new Vector2(50, 116) }, body);                  // tronco
            r.Poly(new[] { new Vector2(50, 112), new Vector2(64, 100), new Vector2(54, 176), new Vector2(46, 226), new Vector2(32, 224), new Vector2(36, 170) }, body);                 // braço esq.
            r.Poly(new[] { new Vector2(170, 112), new Vector2(156, 100), new Vector2(166, 176), new Vector2(174, 226), new Vector2(188, 224), new Vector2(184, 170) }, body);           // braço dir.
            r.Poly(new[] { new Vector2(72, 198), new Vector2(108, 198), new Vector2(104, 312), new Vector2(78, 312), new Vector2(70, 250) }, body);                                    // perna esq.
            r.Poly(new[] { new Vector2(112, 198), new Vector2(148, 198), new Vector2(150, 250), new Vector2(142, 312), new Vector2(116, 312) }, body);                                // perna dir.
            r.Polyline(new[] { new Vector2(62, 96), new Vector2(158, 96), new Vector2(170, 116), new Vector2(150, 200), new Vector2(70, 200), new Vector2(50, 116) }, 1.2f, new Color(Bronze.r, Bronze.g, Bronze.b, 0.35f));
            r.Line(new Vector2(110, 100), new Vector2(110, 196), 1f, new Color(0, 0, 0, 0.35f));
            r.Poly(new[] { new Vector2(98, 46), new Vector2(122, 46), new Vector2(120, 52), new Vector2(100, 52) }, dark);
            return r;
        }

        /// <summary>Barra única da base da tela: carvão facetado, friso de bronze com rebites, filete de brasa e rebaixo central.</summary>
        private static R HudBar()
        {
            var r = new R(512, 96);
            var shape = new[] { new Vector2(0, 14), new Vector2(40, 6), new Vector2(472, 6), new Vector2(512, 14), new Vector2(512, 96), new Vector2(0, 96) };
            r.Poly(shape, R.Hex("#050404", 0.96f));
            r.Poly(new[] { new Vector2(0, 15.5f), new Vector2(40, 7.5f), new Vector2(472, 7.5f), new Vector2(512, 15.5f), new Vector2(512, 24), new Vector2(0, 24) }, R.VGrad(7, 24, BronzeL, BronzeD));
            r.Poly(new[] { new Vector2(0, 24), new Vector2(512, 24), new Vector2(512, 96), new Vector2(0, 96) }, R.VGrad(24, 96, R.Hex("#2a2019", 0.97f), R.Hex("#0c0908", 0.97f)));
            for (int i = 0; i < 8; i++) r.Poly(new[] { new Vector2(i * 64, 24), new Vector2(i * 64 + 40, 24), new Vector2(i * 64 + 6, 70), new Vector2(i * 64 - 20, 70) }, new Color(1, 1, 1, 0.018f));   // facetas
            r.Line(new Vector2(0, 25), new Vector2(512, 25), 1.2f, new Color(Ember.r, Ember.g, Ember.b, 0.55f));
            r.Line(new Vector2(0, 94), new Vector2(512, 94), 2f, R.Hex("#050404"));
            for (int i = 0; i < 16; i++)
            {   // rebites no friso
                float x = 16 + i * 32;
                r.Poly(R.Circle(x, 15.5f, 2.6f, 10), R.Hex("#2a1e12"));
                r.Poly(R.Circle(x - 0.5f, 15f, 1.6f, 8), R.Hex("#e8c27a"));
            }
            return r;
        }

        /// <summary>Berço das orbes nas pontas da barra: meia-lua de bronze com garras.</summary>
        private static R Cradle()
        {
            var r = new R(200, 120);
            r.Ring(100, 120, 84, 100, (x, y) => y < 22 ? Color.clear : Color.Lerp(BronzeL, BronzeD, (y - 20) / 100f), 48);
            r.Ring(100, 120, 80, 85, (x, y) => y < 26 ? Color.clear : R.Hex("#050404", 0.9f), 48);
            foreach (float s in new[] { -1f, 1f })
            {
                float cx = 100 + s * 92;
                r.Poly(new[] { new Vector2(cx - s * 6, 52), new Vector2(cx + s * 12, 30), new Vector2(cx + s * 8, 58), new Vector2(cx + s * 2, 74) }, R.VGrad(30, 74, BronzeL, BronzeD));
                r.Poly(new[] { new Vector2(cx + s * 4, 46), new Vector2(cx + s * 10, 36), new Vector2(cx + s * 8, 50) }, new Color(Amber.r, Amber.g, Amber.b, 0.9f));
            }
            return r;
        }

        /// <summary>Cantos de filigrana: losango de bronze, volutas e ponto de brasa (dentro dos 20 px do 9-slice).</summary>
        private static R Ornate(R r, int w, int h)
        {
            foreach (var (cx, cy, sx, sy) in new[] { (0f, 0f, 1f, 1f), (w, 0f, -1f, 1f), (0f, h, 1f, -1f), (w, h, -1f, -1f) })
            {
                Vector2 P(float x, float y) => new Vector2(cx + sx * x, cy + sy * y);
                r.Poly(new[] { P(2, 9), P(9, 2), P(16, 9), P(9, 16) }, R.Hex("#050404"));
                r.Poly(new[] { P(4, 9), P(9, 4), P(14, 9), P(9, 14) }, R.VGrad(cy + sy * 4, cy + sy * 14, BronzeL, BronzeD));
                r.Poly(new[] { P(7, 9), P(9, 7), P(11, 9), P(9, 11) }, Ember);
                r.Polyline(new[] { P(16, 6), P(22, 4), P(28, 6) }, 1.4f, BronzeL, false);
                r.Polyline(new[] { P(6, 16), P(4, 22), P(6, 28) }, 1.4f, BronzeL, false);
                r.Poly(R.Circle(cx + sx * 29, cy + sy * 6, 1.6f, 8), Amber);
                r.Poly(R.Circle(cx + sx * 6, cy + sy * 29, 1.6f, 8), Amber);
            }
            return r;
        }

        /// <summary>Brasão sob o título das janelas: chama de brasa entre duas asas de metal.</summary>
        private static R Crest()
        {
            var r = new R(140, 28);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 P(float x, float y) => new Vector2(70 + s * x, y);
                r.Poly(new[] { P(12, 14), P(60, 8), P(66, 12), P(30, 20), P(12, 18) }, R.Hex("#050404"));
                r.Poly(new[] { P(14, 15), P(58, 10), P(62, 12), P(30, 18), P(14, 17) }, R.VGrad(8, 20, BronzeL, BronzeD));
                r.Line(P(22, 16), P(54, 12), 1f, new Color(Ember.r, Ember.g, Ember.b, 0.7f));
            }
            r.Poly(new[] { new Vector2(70, 1), new Vector2(81, 14), new Vector2(70, 27), new Vector2(59, 14) }, R.Hex("#050404"));
            r.Poly(new[] { new Vector2(70, 4), new Vector2(78, 14), new Vector2(70, 24), new Vector2(62, 14) }, R.VGrad(4, 24, BronzeL, BronzeD));
            r.Poly(new[] { new Vector2(70, 7), new Vector2(74, 13), new Vector2(72, 12), new Vector2(73, 19), new Vector2(70, 21), new Vector2(67, 19), new Vector2(68, 12), new Vector2(66, 13) }, (x, y) => Color.Lerp(Amber, Ember, (y - 7) / 14f));
            return r;
        }

        /// <summary>Brasão central da barra (sobre as skills): asas de metal largas com gema de brasa.</summary>
        private static R BarCrest()
        {
            var r = new R(420, 44);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 P(float x, float y) => new Vector2(210 + s * x, y);
                r.Poly(new[] { P(24, 30), P(120, 18), P(200, 22), P(208, 30), P(150, 34), P(24, 40) }, R.Hex("#050404", 0.95f));
                r.Poly(new[] { P(26, 31), P(120, 20), P(198, 24), P(204, 30), P(150, 32), P(26, 38) }, R.VGrad(18, 40, BronzeL, BronzeD));
                for (int i = 0; i < 5; i++) r.Line(P(40 + i * 32, 34 - i * 2.2f), P(52 + i * 32, 26 - i * 1.6f), 1.2f, R.Hex("#3a2a18"));
                r.Line(P(30, 36), P(196, 27), 1f, new Color(Ember.r, Ember.g, Ember.b, 0.6f));
            }
            r.Poly(new[] { new Vector2(210, 0), new Vector2(232, 22), new Vector2(210, 44), new Vector2(188, 22) }, R.Hex("#050404"));
            r.Poly(new[] { new Vector2(210, 4), new Vector2(228, 22), new Vector2(210, 40), new Vector2(192, 22) }, R.VGrad(4, 40, BronzeL, BronzeD));
            r.Poly(new[] { new Vector2(210, 10), new Vector2(222, 22), new Vector2(210, 34), new Vector2(198, 22) }, (x, y) => Color.Lerp(Amber, Ember, (y - 10) / 24f));
            r.Poly(new[] { new Vector2(206, 14), new Vector2(212, 12), new Vector2(208, 22) }, new Color(1, 1, 0.9f, 0.7f));
            return r;
        }

        /// <summary>Anel das orbes v2: 24 facetas, aro duplo, 8 cravos e coroa de chama no alto.</summary>
        private static R OrbFrame2()
        {
            const int S = 256; const float c = 128;
            var r = new R(S, S);
            r.Ring(c, c, 100, 121, (x, y) =>
            {
                float a = Mathf.Atan2(y - c, x - c);
                int facet = Mathf.FloorToInt((a + Mathf.PI) / (Mathf.PI * 2f) * 24f);
                float light = 0.5f + 0.5f * Mathf.Cos(a + 2.2f);
                var col = Color.Lerp(BronzeD, BronzeL, light * 0.9f);
                return facet % 2 == 0 ? Color.Lerp(col, IronD, 0.3f) : col;
            }, 24);
            r.Ring(c, c, 97, 102, (x, y) => R.Hex("#050404", 0.92f), 96);
            r.Ring(c, c, 109, 112, (x, y) => R.Hex("#1a120c", 0.85f), 96);
            r.Ring(c, c, 120, 123, (x, y) => R.Hex("#050404", 0.9f), 96);
            for (int i = 0; i < 8; i++)
            {   // cravos para fora
                float a = (i + 0.5f) * Mathf.PI / 4f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var n = new Vector2(-d.y, d.x);
                var b0 = new Vector2(c, c) + d * 119;
                r.Poly(new[] { b0 + n * 7, b0 + d * 9, b0 - n * 7 }, R.Hex("#050404"));
                r.Poly(new[] { b0 + n * 5, b0 + d * 7, b0 - n * 5 }, R.VGrad(b0.y - 7, b0.y + 7, BronzeL, BronzeD));
            }
            foreach (float deg in new[] { 45f, 135f, 225f, 315f })
            {
                float a = deg * Mathf.Deg2Rad; var p = new Vector2(c + Mathf.Cos(a) * 110, c + Mathf.Sin(a) * 110);
                r.Poly(new[] { p + new Vector2(0, -10), p + new Vector2(8, 0), p + new Vector2(0, 10), p + new Vector2(-8, 0) }, R.Hex("#1a0c06"));
                r.Poly(new[] { p + new Vector2(0, -7), p + new Vector2(5, 0), p + new Vector2(0, 7), p + new Vector2(-5, 0) }, (x, y) => Color.Lerp(Amber, Ember, Mathf.InverseLerp(p.y - 7, p.y + 7, y)));
            }
            // coroa de chama no alto
            var top = new[] { new Vector2(c - 22, 12), new Vector2(c - 14, -2 + 6), new Vector2(c - 6, 8), new Vector2(c, -4 + 4), new Vector2(c + 6, 8), new Vector2(c + 14, 4), new Vector2(c + 22, 12), new Vector2(c, 22) };
            r.Poly(top.Select(v => v + new Vector2(0, 1)).ToArray(), R.Hex("#050404"));
            r.Poly(top, (x, y) => Color.Lerp(Amber, Ember, y / 22f));
            return r;
        }

        private static R Vignette()
        {   // escurece as bordas da tela de entrada
            var r = new R(128, 72);
            for (int y = 0; y < 72; y++)
                for (int x = 0; x < 128; x++)
                {
                    float dx = (x - 64f) / 64f, dy = (y - 36f) / 36f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    r.Px[y * 128 + x] = new Color(0.02f, 0.015f, 0.01f, Mathf.Clamp01((d - 0.55f) * 1.1f));
                }
            return r;
        }

        // ------------------------------------------------------------------ glifos (64×64, cor clara; o USS tinge)
        private static readonly Color G = new Color(0.94f, 0.88f, 0.76f);
        private static readonly (string, Action<R>)[] Glyphs =
        {
            ("char", g =>
            {   // elmo de frente
                g.Poly(new[] { new Vector2(32, 8), new Vector2(50, 18), new Vector2(52, 40), new Vector2(44, 56), new Vector2(20, 56), new Vector2(12, 40), new Vector2(14, 18) }, G);
                g.Poly(new[] { new Vector2(18, 30), new Vector2(46, 30), new Vector2(46, 36), new Vector2(18, 36) }, Color.clear);
                g.Poly(new[] { new Vector2(18, 30), new Vector2(46, 30), new Vector2(44, 35), new Vector2(20, 35) }, R.Hex("#1a120c"));
                g.Poly(new[] { new Vector2(30, 36), new Vector2(34, 36), new Vector2(34, 52), new Vector2(30, 52) }, R.Hex("#1a120c"));
            }),
            ("bag", g =>
            {
                g.Poly(new[] { new Vector2(14, 24), new Vector2(50, 24), new Vector2(54, 56), new Vector2(10, 56) }, G);
                g.Poly(new[] { new Vector2(22, 24), new Vector2(24, 12), new Vector2(40, 12), new Vector2(42, 24), new Vector2(37, 24), new Vector2(36, 17), new Vector2(28, 17), new Vector2(27, 24) }, G);
                g.Poly(new[] { new Vector2(14, 32), new Vector2(50, 32), new Vector2(50, 35), new Vector2(14, 35) }, R.Hex("#1a120c"));
                g.Poly(new[] { new Vector2(29, 30), new Vector2(35, 30), new Vector2(35, 40), new Vector2(29, 40) }, R.Hex("#ffb347"));
            }),
            ("party", g =>
            {
                g.Poly(R.Circle(22, 22, 8), G); g.Poly(new[] { new Vector2(8, 52), new Vector2(12, 34), new Vector2(32, 34), new Vector2(36, 52) }, G);
                g.Poly(R.Circle(42, 18, 7), R.Hex("#c9b89a")); g.Poly(new[] { new Vector2(34, 30), new Vector2(50, 30), new Vector2(56, 48), new Vector2(38, 48) }, R.Hex("#c9b89a"));
            }),
            ("guild", g =>
            {   // escudo com faixa
                g.Poly(new[] { new Vector2(12, 10), new Vector2(52, 10), new Vector2(50, 36), new Vector2(32, 56), new Vector2(14, 36) }, G);
                g.Poly(new[] { new Vector2(28, 10), new Vector2(36, 10), new Vector2(36, 50), new Vector2(32, 55), new Vector2(28, 50) }, R.Hex("#8a2a24"));
            }),
            ("rank", g =>
            {   // pódio
                g.Poly(new[] { new Vector2(24, 22), new Vector2(40, 22), new Vector2(40, 54), new Vector2(24, 54) }, G);
                g.Poly(new[] { new Vector2(8, 34), new Vector2(24, 34), new Vector2(24, 54), new Vector2(8, 54) }, R.Hex("#c9b89a"));
                g.Poly(new[] { new Vector2(40, 40), new Vector2(56, 40), new Vector2(56, 54), new Vector2(40, 54) }, R.Hex("#a8977a"));
                g.Poly(new[] { new Vector2(32, 6), new Vector2(35, 13), new Vector2(42, 13), new Vector2(36, 17), new Vector2(38, 24), new Vector2(32, 20), new Vector2(26, 24), new Vector2(28, 17), new Vector2(22, 13), new Vector2(29, 13) }, R.Hex("#ffb347"));
            }),
            ("map", g =>
            {
                g.Poly(new[] { new Vector2(8, 14), new Vector2(22, 8), new Vector2(22, 50), new Vector2(8, 56) }, G);
                g.Poly(new[] { new Vector2(23, 8), new Vector2(41, 14), new Vector2(41, 56), new Vector2(23, 50) }, R.Hex("#c9b89a"));
                g.Poly(new[] { new Vector2(42, 14), new Vector2(56, 8), new Vector2(56, 50), new Vector2(42, 56) }, G);
                g.Polyline(new[] { new Vector2(14, 44), new Vector2(26, 32), new Vector2(36, 38), new Vector2(48, 22) }, 2.2f, R.Hex("#ff6a1a"), false);
            }),
            ("options", g =>
            {   // engrenagem
                var pts = new List<Vector2>();
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2 / 16f; float rr = i % 2 == 0 ? 24 : 18;
                    pts.Add(new Vector2(32 + Mathf.Cos(a - 0.12f) * rr, 32 + Mathf.Sin(a - 0.12f) * rr));
                    pts.Add(new Vector2(32 + Mathf.Cos(a + 0.12f) * rr, 32 + Mathf.Sin(a + 0.12f) * rr));
                }
                g.Poly(pts.ToArray(), G);
                g.Poly(R.Circle(32, 32, 8), R.Hex("#1a120c"));
            }),
            ("help", g =>
            {   // livro aberto
                g.Poly(new[] { new Vector2(6, 16), new Vector2(31, 20), new Vector2(31, 54), new Vector2(6, 50) }, G);
                g.Poly(new[] { new Vector2(33, 20), new Vector2(58, 16), new Vector2(58, 50), new Vector2(33, 54) }, R.Hex("#c9b89a"));
                for (int i = 0; i < 4; i++) g.Line(new Vector2(11, 26 + i * 6), new Vector2(26, 28 + i * 6), 1.6f, R.Hex("#5a4a3a"));
            }),
            ("close", g =>
            {
                g.Line(new Vector2(16, 16), new Vector2(48, 48), 7f, G);
                g.Line(new Vector2(48, 16), new Vector2(16, 48), 7f, G);
            }),
            ("coin", g =>
            {
                g.Poly(R.Circle(32, 32, 22), R.Hex("#8a6a1e"));
                g.Poly(R.Circle(32, 32, 19), (x, y) => Color.Lerp(R.Hex("#ffe08a"), R.Hex("#c4901e"), (y - 13) / 38f));
                g.Poly(new[] { new Vector2(32, 20), new Vector2(40, 32), new Vector2(32, 44), new Vector2(24, 32) }, R.Hex("#a8761a"));
            }),
            ("potion_hp", g => Potion(g, R.Hex("#e0301e"), R.Hex("#7a1008"))),
            ("potion_mp", g => Potion(g, R.Hex("#3a7aff"), R.Hex("#122a7a"))),
            ("skull", g =>
            {
                g.Poly(new[] { new Vector2(32, 8), new Vector2(50, 18), new Vector2(50, 36), new Vector2(42, 44), new Vector2(42, 54), new Vector2(22, 54), new Vector2(22, 44), new Vector2(14, 36), new Vector2(14, 18) }, G);
                g.Poly(R.Circle(24, 30, 5, 12), R.Hex("#1a120c")); g.Poly(R.Circle(40, 30, 5, 12), R.Hex("#1a120c"));
            }),
            ("rmb", g =>
            {   // mouse com o botão direito aceso (magia ativa)
                g.Poly(new[] { V(18, 22), V(46, 22), V(48, 48), V(40, 58), V(24, 58), V(16, 48) }, G);
                g.Poly(new[] { V(18, 6), V(31, 6), V(31, 20), V(18, 20) }, G);
                g.Poly(new[] { V(33, 6), V(46, 6), V(46, 20), V(33, 20) }, R.Hex("#ff8a2a"));
            }),
            // ---- emblemas da Árvore da Ascensão (um por efeito; a USS tinge pela cor do ramo)
            ("a_hp", g =>
            {   // coração
                g.Poly(R.Circle(24, 24, 12, 20), G); g.Poly(R.Circle(40, 24, 12, 20), G);
                g.Poly(new[] { V(12, 28), V(52, 28), V(32, 56) }, G);
            }),
            ("a_mp", g =>
            {   // gota
                g.Poly(new[] { V(32, 6), V(46, 30), V(48, 40), V(42, 52), V(32, 57), V(22, 52), V(16, 40), V(18, 30) }, G);
                g.Poly(new[] { V(24, 38), V(28, 34), V(28, 48), V(24, 46) }, R.Hex("#1a120c", 0.5f));
            }),
            ("a_def", g => { g.Poly(new[] { V(10, 8), V(54, 8), V(52, 34), V(32, 58), V(12, 34) }, G); }),
            ("a_red", g =>
            {   // escudo com seta para baixo
                g.Poly(new[] { V(10, 8), V(54, 8), V(52, 34), V(32, 58), V(12, 34) }, G);
                g.Poly(new[] { V(28, 14), V(36, 14), V(36, 30), V(42, 30), V(32, 44), V(22, 30), V(28, 30) }, R.Hex("#1a120c", 0.75f));
            }),
            ("a_dmg", g =>
            {   // espada inclinada
                g.Line(V(14, 50), V(50, 14), 6f, G);
                g.Line(V(12, 38), V(26, 52), 4f, G);
                g.Line(V(8, 56), V(16, 48), 4f, G);
            }),
            ("a_crit", g =>
            {   // estrela de oito pontas
                var pts = new List<Vector2>();
                for (int i = 0; i < 16; i++) { float a = i * Mathf.PI / 8f; float rr = i % 2 == 0 ? 27 : 11; pts.Add(V(32 + Mathf.Cos(a) * rr, 32 + Mathf.Sin(a) * rr)); }
                g.Poly(pts.ToArray(), G);
            }),
            ("a_ar", g =>
            {   // mira
                g.Polyline(R.Circle(32, 32, 20, 32), 4f, G, true);
                g.Line(V(32, 4), V(32, 22), 4f, G); g.Line(V(32, 42), V(32, 60), 4f, G);
                g.Line(V(4, 32), V(22, 32), 4f, G); g.Line(V(42, 32), V(60, 32), 4f, G);
            }),
            ("a_dr", g =>
            {   // olho
                g.Poly(new[] { V(4, 32), V(18, 18), V(32, 14), V(46, 18), V(60, 32), V(46, 46), V(32, 50), V(18, 46) }, G);
                g.Poly(R.Circle(32, 32, 9, 16), R.Hex("#1a120c", 0.8f));
            }),
            ("a_vig", g => { g.Poly(new[] { V(36, 4), V(14, 36), V(30, 36), V(24, 60), V(50, 24), V(34, 24), V(42, 4) }, G); }),
            ("a_sh", g =>
            {   // bolha de escudo
                g.Polyline(R.Circle(32, 34, 22, 32), 5f, G, true);
                g.Poly(new[] { V(22, 22), V(30, 16), V(26, 28) }, G);
            }),
            ("a_spd", g =>
            {   // três chevrons
                for (int i = 0; i < 3; i++) g.Polyline(new[] { V(10 + i * 14, 14), V(24 + i * 14, 32), V(10 + i * 14, 50) }, 5f, G, false);
            }),
            ("a_lok", g =>
            {   // caveira com gota
                g.Poly(new[] { V(26, 8), V(42, 16), V(42, 32), V(36, 38), V(36, 46), V(18, 46), V(18, 38), V(12, 32), V(12, 16) }, G);
                g.Poly(R.Circle(21, 26, 4, 10), R.Hex("#1a120c")); g.Poly(R.Circle(33, 26, 4, 10), R.Hex("#1a120c"));
                g.Poly(new[] { V(48, 34), V(56, 48), V(54, 56), V(48, 58), V(42, 56), V(40, 48) }, R.Hex("#e0301e"));
            }),
            ("a_mag", g =>
            {   // runa: losango com centro
                g.Poly(new[] { V(32, 4), V(58, 32), V(32, 60), V(6, 32) }, G);
                g.Poly(new[] { V(32, 18), V(44, 32), V(32, 46), V(20, 32) }, R.Hex("#1a120c", 0.6f));
                g.Poly(R.Circle(32, 32, 5, 12), G);
            }),
            // ---- guias dos slots vazios do inventário (silhuetas claras; a USS deixa bem transparentes)
            ("s_helm", g =>
            {
                g.Poly(new[] { V(32, 6), V(48, 14), V(52, 34), V(48, 52), V(38, 58), V(26, 58), V(16, 52), V(12, 34), V(16, 14) }, G);
                g.Poly(new[] { V(18, 30), V(46, 30), V(44, 36), V(20, 36) }, R.Hex("#1a120c", 0.8f));
                g.Poly(new[] { V(30, 36), V(34, 36), V(34, 52), V(30, 52) }, R.Hex("#1a120c", 0.8f));
            }),
            ("s_armor", g =>
            {
                g.Poly(new[] { V(20, 8), V(26, 12), V(38, 12), V(44, 8), V(58, 16), V(54, 30), V(48, 28), V(48, 58), V(16, 58), V(16, 28), V(10, 30), V(6, 16) }, G);
                g.Line(V(32, 14), V(32, 56), 2f, R.Hex("#1a120c", 0.6f));
            }),
            ("s_pants", g =>
            {
                g.Poly(new[] { V(16, 8), V(48, 8), V(52, 58), V(38, 58), V(32, 24), V(26, 58), V(12, 58) }, G);
            }),
            ("s_gloves", g =>
            {
                g.Poly(new[] { V(18, 58), V(18, 30), V(14, 26), V(18, 20), V(22, 26), V(22, 12), V(27, 10), V(28, 24), V(30, 8), V(35, 8), V(35, 24), V(38, 10), V(43, 12), V(42, 26), V(46, 16), V(50, 18), V(46, 34), V(46, 58) }, G);
            }),
            ("s_boots", g =>
            {
                g.Poly(new[] { V(18, 6), V(36, 6), V(36, 40), V(56, 46), V(58, 58), V(14, 58), V(16, 40) }, G);
            }),
            ("s_weapon", g =>
            {
                g.Poly(new[] { V(30, 4), V(34, 4), V(36, 40), V(28, 40) }, G);
                g.Poly(new[] { V(18, 40), V(46, 40), V(46, 45), V(18, 45) }, G);
                g.Poly(new[] { V(29, 45), V(35, 45), V(35, 56), V(29, 56) }, G);
                g.Poly(R.Circle(32, 59, 4, 10), G);
            }),
            ("s_shield", g =>
            {
                g.Poly(new[] { V(10, 8), V(54, 8), V(52, 34), V(32, 58), V(12, 34) }, G);
                g.Poly(new[] { V(30, 12), V(34, 12), V(34, 50), V(32, 53), V(30, 50) }, R.Hex("#1a120c", 0.55f));
            }),
            ("s_wings", g =>
            {
                g.Poly(new[] { V(30, 30), V(22, 10), V(8, 14), V(2, 30), V(8, 46), V(18, 40), V(28, 40) }, G);
                g.Poly(new[] { V(34, 30), V(42, 10), V(56, 14), V(62, 30), V(56, 46), V(46, 40), V(36, 40) }, G);
            }),
            ("s_amulet", g =>
            {
                g.Polyline(new[] { V(14, 8), V(20, 26), V(32, 34), V(44, 26), V(50, 8) }, 3f, G, false);
                g.Poly(new[] { V(32, 32), V(44, 44), V(32, 60), V(20, 44) }, G);
            }),
            ("s_ring", g =>
            {
                g.Polyline(R.Circle(32, 38, 15, 32), 6f, G, true);
                g.Poly(new[] { V(32, 6), V(42, 16), V(32, 24), V(22, 16) }, G);
            }),
            ("s_pet", g =>
            {
                g.Poly(R.Circle(32, 36, 16, 24), G);
                g.Poly(new[] { V(18, 26), V(16, 8), V(28, 20) }, G); g.Poly(new[] { V(46, 26), V(48, 8), V(36, 20) }, G);
                g.Poly(R.Circle(26, 34, 3, 10), R.Hex("#1a120c", 0.7f)); g.Poly(R.Circle(38, 34, 3, 10), R.Hex("#1a120c", 0.7f));
            }),
            ("s_mount", g =>
            {   // ferradura
                g.Polyline(new[] { V(18, 56), V(14, 36), V(18, 18), V(32, 10), V(46, 18), V(50, 36), V(46, 56) }, 8f, G, false);
            }),
        };

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        private static void Potion(R g, Color liquid, Color dark)
        {
            g.Poly(new[] { new Vector2(26, 8), new Vector2(38, 8), new Vector2(38, 14), new Vector2(26, 14) }, R.Hex("#8a6a3e"));
            g.Poly(new[] { new Vector2(27, 14), new Vector2(37, 14), new Vector2(37, 22), new Vector2(50, 34), new Vector2(50, 52), new Vector2(42, 58), new Vector2(22, 58), new Vector2(14, 52), new Vector2(14, 34), new Vector2(27, 22) }, new Color(0.85f, 0.9f, 1f, 0.55f));
            g.Poly(new[] { new Vector2(16, 36), new Vector2(48, 36), new Vector2(48, 51), new Vector2(41, 56), new Vector2(23, 56), new Vector2(16, 51) }, (x, y) => Color.Lerp(liquid, dark, (y - 36) / 20f));
            g.Poly(new[] { new Vector2(19, 38), new Vector2(23, 38), new Vector2(23, 50), new Vector2(19, 48) }, new Color(1, 1, 1, 0.4f));
        }
    }
}
