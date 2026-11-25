using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Cristal de Vida / Mana da HUD v4: gema hexagonal alongada com engaste de bronze, líquido que sobe e desce com
    /// superfície ondulando, facetas claras por cima e brilho de brasa quando está baixo. Desenhado com Painter2D.
    /// </summary>
    public sealed class HudCrystal : VisualElement
    {
        public float Fill = 1f;
        public bool Warn;
        private readonly Color _deep, _mid, _surf;
        private float _shown = 1f;
        private readonly float _phase = Random.value * 6f;

        public HudCrystal(Color deep, Color mid, Color surf)
        {
            _deep = deep; _mid = mid; _surf = surf;
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud4-crystal__gem");
            generateVisualContent += Draw;
            schedule.Execute(() => { _shown = Mathf.Lerp(_shown, Fill, 0.2f); MarkDirtyRepaint(); }).Every(33);
        }

        private static List<Vector2> Shape(Rect r, float inset)
        {
            float cx = r.center.x, w = r.width * 0.5f - inset, top = r.yMin + inset * 1.4f, bot = r.yMax - inset * 1.4f, h = bot - top;
            return new List<Vector2>
            {
                new Vector2(cx, top), new Vector2(cx + w, top + h * 0.26f), new Vector2(cx + w, top + h * 0.7f),
                new Vector2(cx, bot), new Vector2(cx - w, top + h * 0.7f), new Vector2(cx - w, top + h * 0.26f),
            };
        }

        /// <summary>Recorta o polígono convexo deixando só o que fica abaixo da linha y = f(x) (aprox. por reta inclinada).</summary>
        private static List<Vector2> ClipBelow(List<Vector2> poly, float level, float tilt, float cx)
        {
            float Y(float x) => level + (x - cx) * tilt;
            var o = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                bool ain = a.y >= Y(a.x), bin = b.y >= Y(b.x);
                if (ain) o.Add(a);
                if (ain != bin)
                {   // interseção da aresta com a reta
                    float da = a.y - Y(a.x), db = b.y - Y(b.x);
                    float t = da / (da - db);
                    o.Add(Vector2.Lerp(a, b, t));
                }
            }
            return o;
        }

        private static void Path(Painter2D p, List<Vector2> pts)
        {
            p.BeginPath();
            for (int i = 0; i < pts.Count; i++) { if (i == 0) p.MoveTo(pts[i]); else p.LineTo(pts[i]); }
            p.ClosePath();
        }

        private void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width < 8) return;
            var p = mgc.painter2D;
            float t = Time.realtimeSinceStartup + _phase;
            var outer = Shape(r, 1f);
            var inner = Shape(r, 7f);
            // engaste de bronze
            p.fillColor = new Color(0.31f, 0.23f, 0.14f, 1f); Path(p, outer); p.Fill();
            p.strokeColor = new Color(0.77f, 0.6f, 0.35f, 1f); p.lineWidth = 1.6f; Path(p, outer); p.Stroke();
            // fundo do vidro
            p.fillColor = new Color(0.03f, 0.02f, 0.03f, 0.95f); Path(p, inner); p.Fill();
            float f = Mathf.Clamp01(_shown);
            float top = inner[0].y, bot = inner[3].y, cx = r.center.x;
            if (f > 0.001f)
            {
                float level = bot - f * (bot - top);
                float tilt = Mathf.Sin(t * 1.6f) * 0.06f;
                var deep = ClipBelow(inner, level + 2f, tilt, cx);
                if (deep.Count >= 3) { p.fillColor = _deep; Path(p, deep); p.Fill(); }
                var mid = ClipBelow(inner, level + Mathf.Sin(t * 2.3f) * 1.5f, tilt, cx);
                if (mid.Count >= 3)
                {
                    p.fillColor = _mid; Path(p, mid); p.Fill();
                    // linha clara da superfície
                    float wl = level + Mathf.Sin(t * 2.3f) * 1.5f;
                    p.strokeColor = _surf; p.lineWidth = 2f;
                    float xa = Mathf.Lerp(inner[5].x, cx, Mathf.Clamp01((inner[5].y - wl) / Mathf.Max(1f, inner[5].y - top))) + 2f;
                    float xb = 2f * cx - xa;
                    if (wl > inner[5].y) { xa = inner[5].x + 2f; xb = inner[1].x - 2f; }
                    if (wl > inner[4].y) { float k = Mathf.Clamp01((wl - inner[4].y) / Mathf.Max(1f, bot - inner[4].y)); xa = Mathf.Lerp(inner[4].x, cx, k) + 2f; xb = 2f * cx - xa; }
                    p.BeginPath(); p.MoveTo(new Vector2(xa, wl + (xa - cx) * tilt)); p.LineTo(new Vector2(xb, wl + (xb - cx) * tilt)); p.Stroke();
                }
            }
            // facetas e reflexo
            p.strokeColor = new Color(1f, 1f, 1f, 0.16f); p.lineWidth = 1f;
            var c = new Vector2(cx, top + (bot - top) * 0.45f);
            foreach (var v in inner) { p.BeginPath(); p.MoveTo(c); p.LineTo(v); p.Stroke(); }
            p.fillColor = new Color(1f, 1f, 1f, 0.13f);
            p.BeginPath(); p.MoveTo(inner[0]); p.LineTo(inner[5]); p.LineTo(new Vector2(inner[5].x + 6, inner[5].y + 26)); p.LineTo(new Vector2(cx - 3, top + 12)); p.ClosePath(); p.Fill();
            if (Warn)
            {   // pouca vida: borda pulsando em brasa
                float a = 0.35f + 0.35f * Mathf.Sin(t * 6f);
                p.strokeColor = new Color(1f, 0.35f, 0.1f, a); p.lineWidth = 3f; Path(p, outer); p.Stroke();
            }
        }
    }

    /// <summary>Barra vertical fina (Vigor, Escudo): enche de baixo para cima, com marcas a cada 25%.</summary>
    public sealed class HudVBar : VisualElement
    {
        public float Fill;
        private float _shown;
        private readonly Color _col, _top;

        public HudVBar(Color col, Color top)
        {
            _col = col; _top = top;
            pickingMode = PickingMode.Position;
            AddToClassList("hud4-vbar");
            generateVisualContent += Draw;
            schedule.Execute(() => { if (Mathf.Abs(_shown - Fill) > 0.002f) { _shown = Mathf.Lerp(_shown, Fill, 0.25f); MarkDirtyRepaint(); } }).Every(33);
        }

        private void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.height < 8) return;
            var p = mgc.painter2D;
            var inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
            p.fillColor = new Color(0.02f, 0.02f, 0.02f, 0.9f);
            p.BeginPath(); p.MoveTo(new Vector2(r.xMin, r.yMin)); p.LineTo(new Vector2(r.xMax, r.yMin)); p.LineTo(new Vector2(r.xMax, r.yMax)); p.LineTo(new Vector2(r.xMin, r.yMax)); p.ClosePath(); p.Fill();
            float f = Mathf.Clamp01(_shown);
            if (f > 0.001f)
            {
                float y = inner.yMax - inner.height * f;
                p.fillColor = _col;
                p.BeginPath(); p.MoveTo(new Vector2(inner.xMin, y)); p.LineTo(new Vector2(inner.xMax, y)); p.LineTo(new Vector2(inner.xMax, inner.yMax)); p.LineTo(new Vector2(inner.xMin, inner.yMax)); p.ClosePath(); p.Fill();
                p.fillColor = _top;
                p.BeginPath(); p.MoveTo(new Vector2(inner.xMin, y)); p.LineTo(new Vector2(inner.xMax, y)); p.LineTo(new Vector2(inner.xMax, y + 3)); p.LineTo(new Vector2(inner.xMin, y + 3)); p.ClosePath(); p.Fill();
            }
            p.strokeColor = new Color(0, 0, 0, 0.6f); p.lineWidth = 1f;
            for (int k = 1; k < 4; k++) { float yy = inner.yMax - inner.height * k / 4f; p.BeginPath(); p.MoveTo(new Vector2(inner.xMin, yy)); p.LineTo(new Vector2(inner.xMax, yy)); p.Stroke(); }
            p.strokeColor = new Color(0.54f, 0.42f, 0.24f, 1f); p.lineWidth = 1.2f;
            p.BeginPath(); p.MoveTo(new Vector2(r.xMin, r.yMin)); p.LineTo(new Vector2(r.xMax, r.yMin)); p.LineTo(new Vector2(r.xMax, r.yMax)); p.LineTo(new Vector2(r.xMin, r.yMax)); p.ClosePath(); p.Stroke();
        }
    }
}
