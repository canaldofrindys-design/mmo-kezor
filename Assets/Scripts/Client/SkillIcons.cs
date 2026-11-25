using System.Collections.Generic;
using UnityEngine;
using Kezor.Core.Characters;
using Kezor.Core.Combat;

namespace Kezor.Client
{
    /// <summary>
    /// Ícones das skills desenhados em código (32×32): fundo na cor da classe e um símbolo pelo tipo da skill —
    /// golpe (lâmina diagonal), área (anel), buff (seta para cima), estandarte (bandeira), invocação (vulto com olhos),
    /// maldição (espiral) e rajada (três riscos). Sem arquivos de imagem; cache por id.
    /// </summary>
    public static class SkillIcons
    {
        private const int S = 32;
        private static readonly Dictionary<int, Texture2D> Cache = new Dictionary<int, Texture2D>();

        private static Color ClassColor(ClassMask m) => m switch
        {
            ClassMask.Warden => KezorLook.Hex("#3d5a80"),
            ClassMask.Arcanist => KezorLook.Hex("#4b2d6b"),
            ClassMask.Ranger => KezorLook.Hex("#3f6b3a"),
            ClassMask.Bladeweaver => KezorLook.Hex("#2a5f6a"),
            ClassMask.Commander => KezorLook.Hex("#6a2a24"),
            ClassMask.Invoker => KezorLook.Hex("#2a2a5a"),
            ClassMask.EmberFist => KezorLook.Hex("#7a3a12"),
            _ => KezorLook.Hex("#444444"),
        };

        private static Color GlyphColor(SkillDefinition s)
        {
            if (s.Kind == SkillKind.Banner) return Models.BannerColor((int)s.Buff);
            if (s.Kind == SkillKind.Summon) return KezorLook.Hex("#c9a8ff");
            if (s.SlowPercent > 0 || s.DotPercent > 0) return KezorLook.Hex("#b0ff8a");
            if (s.Hybrid) return KezorLook.Hex("#8fe8ff");
            if (s.Magic) return KezorLook.Hex("#ffb36a");
            if (s.Classes == ClassMask.EmberFist) return KezorLook.Hex("#ff8a3a");
            return KezorLook.Hex("#f0eadc");
        }

        public static Texture2D Get(SkillDefinition s)
        {
            if (Cache.TryGetValue(s.Id, out var t) && t != null) return t;
            t = new Texture2D(S, S, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            var bg = ClassColor(s.Classes);
            var g = GlyphColor(s);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - 15.5f, dy = y - 15.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    bool border = x < 2 || y < 2 || x > S - 3 || y > S - 3;
                    var c = border ? new Color(0.55f, 0.43f, 0.22f) : bg * (0.6f + 0.4f * (1f - r / 23f));
                    c.a = 1f;
                    if (!border && Glyph(s, x, y, dx, dy, r)) c = g;
                    px[y * S + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            Cache[s.Id] = t;
            return t;
        }

        private static bool Glyph(SkillDefinition s, int x, int y, float dx, float dy, float r)
        {
            switch (s.Kind)
            {
                case SkillKind.SelfBuff:   // seta para cima
                    return (Mathf.Abs(dx) < 2.2f && dy > -10 && dy < 4) || (dy >= 2 && dy < 11 && Mathf.Abs(dx) < 11 - dy + 1 && Mathf.Abs(dx) > 9 - dy - 1);
                case SkillKind.Banner:     // mastro + bandeira
                    return (dx > -9 && dx < -6 && dy > -12 && dy < 12) || (dx >= -6 && dx < 8 && dy > 2 && dy < 11 && dx < 8 - (dy - 6.5f) * (dy - 6.5f) * 0.1f);
                case SkillKind.Summon:     // vulto com dois olhos
                {
                    bool body = (r < 9 && dy > -2) || (Mathf.Abs(dx) < 9 && dy <= -2 && dy > -11 && ((int)(dx + 20) % 4 != 0 || dy > -8));
                    bool eye = (Mathf.Abs(dx + 3.5f) < 1.6f || Mathf.Abs(dx - 3.5f) < 1.6f) && Mathf.Abs(dy - 3) < 1.6f;
                    return body && !eye;
                }
            }
            if (s.SlowPercent > 0 || s.DotPercent > 0)
            {   // espiral
                float a = Mathf.Atan2(dy, dx);
                float want = (a + Mathf.PI) / (2f * Mathf.PI) * 5f;
                for (int k = 0; k < 3; k++) if (Mathf.Abs(r - (want + k * 5f)) < 1.3f && r > 1.5f) return true;
                return false;
            }
            if (s.Hits > 1)            // rajada: três riscos
                return (Mathf.Abs(dy - dx * 0.4f) < 1.6f || Mathf.Abs(dy - dx * 0.4f - 7) < 1.6f || Mathf.Abs(dy - dx * 0.4f + 7) < 1.6f) && Mathf.Abs(dx) < 10;
            if (s.AreaRadius > 0)      // anel com ponto
                return Mathf.Abs(r - 9f) < 1.8f || r < 2.6f;
            // golpe: lâmina diagonal com guarda
            bool blade = Mathf.Abs(dx - dy) < 2.4f && dx > -6 && dx < 11;
            bool guard = Mathf.Abs(dx + dy + 10) < 2f && dx > -11 && dx < 1;
            bool grip = Mathf.Abs(dx - dy) < 1.6f && dx <= -6 && dx > -11;
            return blade || guard || grip;
        }
    }
}
