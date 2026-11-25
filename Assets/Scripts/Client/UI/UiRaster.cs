using System;
using UnityEngine;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Rasterizador simples (polígonos com antisserrilhado por supersampling 4×4, gradientes por pixel) para gerar a arte
    /// da interface em código: molduras facetadas, botões, slots, anéis das orbes e glifos dos menus. Linha 0 = topo.
    /// Usado pelo gerador do editor (Kezor ▸ Gerar arte da UI), que grava PNGs em Resources/UI/Art.
    /// </summary>
    public sealed class UiRaster
    {
        public readonly int W, H;
        public readonly Color[] Px;

        public UiRaster(int w, int h) { W = w; H = h; Px = new Color[w * h]; }

        public static Color Hex(string h, float a = 1f) { ColorUtility.TryParseHtmlString(h, out var c); c.a = a; return c; }

        private void Blend(int x, int y, Color c, float cover)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || cover <= 0f) return;
            float a = c.a * cover;
            ref var d = ref Px[y * W + x];
            float outA = a + d.a * (1f - a);
            if (outA <= 0.0001f) { d = Color.clear; return; }
            d.r = (c.r * a + d.r * d.a * (1f - a)) / outA;
            d.g = (c.g * a + d.g * d.a * (1f - a)) / outA;
            d.b = (c.b * a + d.b * d.a * (1f - a)) / outA;
            d.a = outA;
        }

        private static bool Inside(Vector2[] p, float x, float y)
        {
            bool c = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x) c = !c;
            return c;
        }

        /// <summary>Preenche o polígono; a cor de cada pixel vem de <paramref name="shade"/>(x, y) em coordenadas da imagem.</summary>
        public void Poly(Vector2[] p, Func<float, float, Color> shade)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var v in p) { x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y); }
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0)), iy0 = Mathf.Max(0, Mathf.FloorToInt(y0));
            int ix1 = Mathf.Min(W - 1, Mathf.CeilToInt(x1)), iy1 = Mathf.Min(H - 1, Mathf.CeilToInt(y1));
            for (int y = iy0; y <= iy1; y++)
                for (int x = ix0; x <= ix1; x++)
                {
                    int n = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (Inside(p, x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f)) n++;
                    if (n > 0) Blend(x, y, shade(x + 0.5f, y + 0.5f), n / 16f);
                }
        }

        public void Poly(Vector2[] p, Color c) => Poly(p, (x, y) => c);

        /// <summary>Gradiente vertical entre y0 (cor a) e y1 (cor b).</summary>
        public static Func<float, float, Color> VGrad(float y0, float y1, Color a, Color b) =>
            (x, y) => Color.Lerp(a, b, Mathf.InverseLerp(y0, y1, y));

        public void Line(Vector2 a, Vector2 b, float w, Color c)
        {
            var d = (b - a).normalized; var n = new Vector2(-d.y, d.x) * (w * 0.5f);
            Poly(new[] { a + n, b + n, b - n, a - n }, c);
        }

        public void Polyline(Vector2[] p, float w, Color c, bool closed = true)
        {
            for (int i = 0; i < p.Length - (closed ? 0 : 1); i++) Line(p[i], p[(i + 1) % p.Length], w, c);
        }

        public static Vector2[] Circle(float cx, float cy, float r, int seg = 48)
        {
            var p = new Vector2[seg];
            for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2f / seg; p[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r); }
            return p;
        }

        /// <summary>Anel entre r0 e r1 (dois polígonos: o de fora pinta, o de dentro apaga).</summary>
        public void Ring(float cx, float cy, float r0, float r1, Func<float, float, Color> shade, int seg = 64)
        {
            var outer = Circle(cx, cy, r1, seg);
            Poly(outer, (x, y) =>
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                return d < r0 - 0.7f ? Color.clear : shade(x, y);
            });
            // borda interna antisserrilhada: suaviza o alfa perto de r0
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    if (d < r0 + 0.5f && d > r0 - 1.5f) { ref var c = ref Px[y * W + x]; c.a *= Mathf.Clamp01(d - r0 + 0.5f); }
                }
        }

        /// <summary>Retângulo com cantos chanfrados (o "low poly" das molduras).</summary>
        public static Vector2[] Bevel(float x0, float y0, float x1, float y1, float cut) => new[]
        {
            new Vector2(x0 + cut, y0), new Vector2(x1 - cut, y0), new Vector2(x1, y0 + cut), new Vector2(x1, y1 - cut),
            new Vector2(x1 - cut, y1), new Vector2(x0 + cut, y1), new Vector2(x0, y1 - cut), new Vector2(x0, y0 + cut),
        };

        public static Vector2[] Inset(Vector2[] p, float d)
        {   // encolhe um polígono convexo em sentido horário (imagem com y para baixo)
            var c = Vector2.zero; foreach (var v in p) c += v; c /= p.Length;
            var r = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                var prev = p[(i + p.Length - 1) % p.Length]; var next = p[(i + 1) % p.Length];
                var n1 = Perp(p[i] - prev); var n2 = Perp(next - p[i]);
                var n = (n1 + n2).normalized;
                if (Vector2.Dot(n, c - p[i]) < 0) n = -n;
                float cos = Mathf.Max(0.35f, Vector2.Dot(n, n1.normalized) * (Vector2.Dot(n1, c - p[i]) < 0 ? -1 : 1));
                r[i] = p[i] + n * (d / cos);
            }
            return r;
        }

        private static Vector2 Perp(Vector2 v) => new Vector2(-v.y, v.x).normalized;

        public Texture2D ToTexture()
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var flip = new Color[Px.Length];
            for (int y = 0; y < H; y++) Array.Copy(Px, y * W, flip, (H - 1 - y) * W, W);
            t.SetPixels(flip);
            t.Apply();
            return t;
        }
    }
}
