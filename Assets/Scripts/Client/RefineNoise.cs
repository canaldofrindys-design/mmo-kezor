using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Ruído cinza 256×256 repetível (procedural) do fluxo de refino +11/+13/+15: publicado como global "_NoiseMap".
    /// </summary>
    public static class RefineNoise
    {
        public const int Size = 256;
        private static Texture2D? _tex;
        public static Texture2D Texture => _tex ??= Build();

        public static void Ensure() => Shader.SetGlobalTexture("_NoiseMap", Texture);

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xffffff) / 16777215f;
            }
        }

        private static float Value(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int W(int i) => ((i % period) + period) % period;
            float a = Hash(W(x0), W(y0), seed), b = Hash(W(x0 + 1), W(y0), seed);
            float c = Hash(W(x0), W(y0 + 1), seed), d = Hash(W(x0 + 1), W(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static Texture2D Build()
        {
            var t = new Texture2D(Size, Size, TextureFormat.R8, true, true)
            { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "KezorRefineNoise" };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (float)x / Size, v = (float)y / Size;
                    float s = 0, amp = 0.5f, norm = 0;
                    for (int o = 0; o < 4; o++) { s += Value(u, v, 4 << o, 31 + o * 7) * amp; norm += amp; amp *= 0.5f; }
                    s /= norm;
                    s = Mathf.Clamp01((s - 0.5f) * 1.6f + 0.5f);   // mais contraste: veios de energia
                    byte b = (byte)(s * 255);
                    px[y * Size + x] = new Color32(b, b, b, 255);
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }
    }
}
