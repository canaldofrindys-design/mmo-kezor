using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kezor.Client.UI
{
    /// <summary>Atalhos para montar a interface em código com as classes do tema (Resources/UI/Kezor.uss).</summary>
    public static class Kz
    {
        private static readonly Dictionary<string, Texture2D?> ArtCache = new Dictionary<string, Texture2D?>();

        public static Texture2D? Art(string name)
        {
            if (!ArtCache.TryGetValue(name, out var t)) ArtCache[name] = t = Resources.Load<Texture2D>("UI/Art/" + name);
            return t;
        }

        public static T Put<T>(this VisualElement parent, T child) where T : VisualElement { parent.Add(child); return child; }

        public static VisualElement El(string classes, VisualElement? parent = null, bool pick = false)
        {
            var e = new VisualElement();
            foreach (var c in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) e.AddToClassList(c);
            e.pickingMode = pick ? PickingMode.Position : PickingMode.Ignore;
            parent?.Add(e);
            return e;
        }

        public static Label Text(string text, string classes, VisualElement? parent = null)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = true };
            foreach (var c in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) l.AddToClassList(c);
            parent?.Add(l);
            return l;
        }

        public static Button Btn(string text, Action onClick, string classes = "kz-btn", VisualElement? parent = null)
        {
            var b = new Button(onClick) { text = text };
            b.ClearClassList();
            foreach (var c in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) b.AddToClassList(c);
            parent?.Add(b);
            return b;
        }

        public static VisualElement Icon(Texture2D? tex, string classes, VisualElement? parent = null)
        {
            var e = El(classes, parent);
            if (tex != null) e.style.backgroundImage = new StyleBackground(tex);
            return e;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        public static void Show(this VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Muda o texto só se mudou (evita refazer o layout todo quadro).</summary>
        public static void Set(this Label l, string text) { if (l.text != text) l.text = text; }

        public static void SetClass(this VisualElement e, string cls, bool on) { if (on) e.AddToClassList(cls); else e.RemoveFromClassList(cls); }

        public static string Time(float s) => s >= 60 ? $"{Mathf.CeilToInt(s / 60f)}m" : s >= 10 ? $"{Mathf.CeilToInt(s)}" : $"{s:0.0}";
    }

    /// <summary>Líquido das orbes: onda animada, faixa clara na superfície e uma segunda onda mais escura atrás.</summary>
    public sealed class OrbLiquid : VisualElement
    {
        public float Fill = 1f;
        public Color Deep, Mid, Surface;
        private float _shown = 1f;
        private readonly float _phase = UnityEngine.Random.value * 6f;

        public OrbLiquid(Color deep, Color mid, Color surface)
        {
            Deep = deep; Mid = mid; Surface = surface;
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-orb__liquid");
            generateVisualContent += Draw;
            schedule.Execute(() => { _shown = Mathf.Lerp(_shown, Fill, 0.18f); MarkDirtyRepaint(); }).Every(33);
        }

        private void Draw(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (rect.width < 4) return;
            var p = mgc.painter2D;
            float r = Mathf.Min(rect.width, rect.height) * 0.5f * (101f / 128f);
            var c = rect.center;
            float t = Time.realtimeSinceStartup + _phase;

            // fundo do vidro
            p.fillColor = new Color(0.03f, 0.02f, 0.02f, 0.92f);
            p.BeginPath(); p.Arc(c, r, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
            float f = Mathf.Clamp01(_shown);
            if (f <= 0.001f) return;
            float level = c.y + r - f * 2f * r;

            Layer(p, c, r, x => level + 3f + Mathf.Sin(x * 0.07f - t * 1.7f) * 2.2f, Deep * 0.8f, 0f);                  // onda de trás
            Layer(p, c, r, x => level + Mathf.Sin(x * 0.055f + t * 2.3f) * 3f + Mathf.Sin(x * 0.11f + t * 1.1f) * 1.2f, Deep, 0f);
            Layer(p, c, r, x => level + Mathf.Sin(x * 0.055f + t * 2.3f) * 3f + Mathf.Sin(x * 0.11f + t * 1.1f) * 1.2f, Mid, r * 0.55f);
            Layer(p, c, r, x => level + Mathf.Sin(x * 0.055f + t * 2.3f) * 3f + Mathf.Sin(x * 0.11f + t * 1.1f) * 1.2f, Surface, 4.5f);
        }

        /// <summary>Preenche o círculo abaixo da onda; com <paramref name="band"/> &gt; 0 só a faixa logo abaixo da superfície.</summary>
        private static void Layer(Painter2D p, Vector2 c, float r, Func<float, float> wave, Color col, float band)
        {
            const int N = 96;
            p.fillColor = col;
            p.BeginPath();
            for (int i = 0; i < N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                float x = c.x + Mathf.Cos(a) * r, y = c.y + Mathf.Sin(a) * r;
                float dx = x - c.x;
                float lower = c.y + Mathf.Sqrt(Mathf.Max(0, r * r - dx * dx));
                float w = wave(x);
                float yy = Mathf.Min(Mathf.Max(y, w), lower);
                if (band > 0) yy = Mathf.Min(yy, Mathf.Min(lower, w + band));
                if (i == 0) p.MoveTo(new Vector2(x, yy)); else p.LineTo(new Vector2(x, yy));
            }
            p.ClosePath();
            p.Fill();
        }
    }

    /// <summary>Recarga radial: setor escuro que encolhe no sentido horário a partir do topo.</summary>
    public sealed class CooldownSector : VisualElement
    {
        private float _frac;
        public float Frac { get => _frac; set { if (Mathf.Abs(value - _frac) > 0.002f) { _frac = value; MarkDirtyRepaint(); } } }

        public CooldownSector()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kz-slot__cd");
            generateVisualContent += mgc =>
            {
                if (_frac <= 0.001f) return;
                var r = contentRect; var p = mgc.painter2D;
                var c = r.center; float rad = Mathf.Max(r.width, r.height) * 0.75f;
                p.fillColor = new Color(0, 0, 0, 0.62f);
                p.BeginPath(); p.MoveTo(c);
                p.Arc(c, rad, Angle.Degrees(-90), Angle.Degrees(-90 + 360f * _frac));
                p.ClosePath(); p.Fill();
                // borda de brasa no ponteiro
                float a = (-90 + 360f * _frac) * Mathf.Deg2Rad;
                p.strokeColor = new Color(1f, 0.6f, 0.2f, 0.9f); p.lineWidth = 1.5f;
                p.BeginPath(); p.MoveTo(c); p.LineTo(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad); p.Stroke();
            };
            style.overflow = Overflow.Hidden;
        }
    }

    /// <summary>Barra com moldura, preenchimento tingido pelo USS (kz-bar--hp, --mp, --exp...) e texto opcional.</summary>
    public sealed class KzBar : VisualElement
    {
        private readonly VisualElement _fill;
        public readonly Label Label;
        private float _v = -1;

        public KzBar(string mod, bool text = false)
        {
            AddToClassList("kz-bar"); AddToClassList(mod);
            pickingMode = PickingMode.Ignore;
            _fill = Kz.El("kz-bar__fill", this);
            Label = Kz.Text("", "kz-bar__text", this);
            Label.Show(text);
        }

        public void Set(float v, string? text = null)
        {
            v = Mathf.Clamp01(v);
            if (Mathf.Abs(v - _v) > 0.001f) { _v = v; _fill.style.width = Length.Percent(v * 100f); }
            if (text != null) Label.Set(text);
        }
    }

    /// <summary>Slot de ícone (skill, item, buff): moldura, ícone, rótulos de tecla/quantidade/nível e recarga.</summary>
    public sealed class KzSlot : VisualElement
    {
        public readonly VisualElement IconEl;
        public readonly Label Key, Count, Lvl, Caption;
        public CooldownSector? Cd;
        public Label? CdText;
        private Texture2D? _icon;

        public KzSlot(string extra = "", bool cooldown = false)
        {
            AddToClassList("kz-slot");
            foreach (var c in extra.Split(' ', StringSplitOptions.RemoveEmptyEntries)) AddToClassList(c);
            pickingMode = PickingMode.Position;
            IconEl = Kz.El("kz-slot__icon", this);
            Caption = Kz.Text("", "kz-slot__label", this);
            if (cooldown) { Cd = new CooldownSector(); Add(Cd); CdText = Kz.Text("", "kz-slot__cdtext", this); }
            Key = Kz.Text("", "kz-slot__key", this);
            Count = Kz.Text("", "kz-slot__count", this);
            Lvl = Kz.Text("", "kz-slot__lvl", this);
        }

        public void SetIcon(Texture2D? t)
        {
            if (t == _icon && _sprite == null) return;
            _icon = t; _sprite = null;
            IconEl.style.backgroundImage = t != null ? new StyleBackground(t) : new StyleBackground(StyleKeyword.None);
        }

        private Sprite? _sprite;
        public void SetSprite(Sprite? s)
        {
            if (s == _sprite && _icon == null) return;
            _sprite = s; _icon = null;
            IconEl.style.backgroundImage = s != null ? new StyleBackground(s) : new StyleBackground(StyleKeyword.None);
        }

        private VisualElement? _rarity; private Color? _rarityColor;
        /// <summary>Filete interno na cor da raridade (null tira).</summary>
        public void SetRarity(Color? c)
        {
            if (c == _rarityColor) return;
            _rarityColor = c;
            if (_rarity == null) { _rarity = Kz.El("kz-slot__rarity"); Insert(1, _rarity); }
            var col = c ?? new Color(0, 0, 0, 0);
            _rarity.style.borderLeftColor = _rarity.style.borderRightColor = _rarity.style.borderTopColor = _rarity.style.borderBottomColor = col;
            _rarity.style.backgroundColor = c.HasValue ? new Color(col.r, col.g, col.b, 0.08f) : new Color(0, 0, 0, 0);
        }
    }

    /// <summary>
    /// Janela do jogo: moldura com faixa de título (arrasta por ela), X para fechar e posição salva em PlayerPrefs.
    /// O conteúdo vai em <see cref="Body"/>.
    /// </summary>
    public class KzWindow : VisualElement
    {
        public readonly VisualElement Header, Body;
        public readonly Label Title;
        public Action? OnClose;
        private readonly string _key;
        private bool _dragging; private Vector2 _grab;

        public KzWindow(string key, string title, float width, Vector2 defaultPos)
        {
            _key = "kz.win." + key;
            AddToClassList("kz-window");
            pickingMode = PickingMode.Position;
            style.width = width;
            Header = Kz.El("kz-window__header", this, pick: true);
            Title = Kz.Text(title, "kz-title kz-window__title", Header);
            var close = Kz.Btn("", () => OnClose?.Invoke(), "kz-close", Header);
            close.tooltip = "Fechar (Esc)";
            Kz.El("kz-window__crest", this);                                   // brasão sob o título
            Body = Kz.El("kz-window__body", this, pick: true);
            var pos = new Vector2(PlayerPrefs.GetFloat(_key + ".x", defaultPos.x), PlayerPrefs.GetFloat(_key + ".y", defaultPos.y));
            style.left = pos.x; style.top = pos.y;

            Header.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; _dragging = true; _grab = (Vector2)e.position - new Vector2(resolvedStyle.left, resolvedStyle.top); Header.CapturePointer(e.pointerId); BringToFront(); });
            Header.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_dragging) return;
                var p = (Vector2)e.position - _grab;
                var root = parent?.contentRect ?? new Rect(0, 0, 1600, 900);
                p.x = Mathf.Clamp(p.x, -resolvedStyle.width + 80, root.width - 80);
                p.y = Mathf.Clamp(p.y, 0, root.height - 40);
                style.left = p.x; style.top = p.y;
            });
            Header.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!_dragging) return;
                _dragging = false; Header.ReleasePointer(e.pointerId);
                PlayerPrefs.SetFloat(_key + ".x", resolvedStyle.left); PlayerPrefs.SetFloat(_key + ".y", resolvedStyle.top);
            });
            RegisterCallback<PointerDownEvent>(_ => BringToFront(), TrickleDown.TrickleDown);
        }
    }
}
