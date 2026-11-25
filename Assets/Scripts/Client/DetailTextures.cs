using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Texturas de detalhe do visual mid poly (Kezor/LowPoly, UV2.x = id do material). Geradas proceduralmente uma vez
    /// (arte 100% original, sem arquivo): cada camada guarda normal xy no RG e variação de tom no B, tudo repetível
    /// (tileable). Ids: 1 pele, 2 cabelo/pelo, 3 metal, 4 couro, 5 tecido, 6 madeira, 7 pedra, 8 reboco, 9 telha, 10 folhagem;
    /// terreno (Kezor/Terrain): 11 grama, 12 terra, 13 calçamento, 14 cascalho.
    /// </summary>
    public static class DetailTextures
    {
        public const int Size = 128;
        public const int Layers = 14;
        public enum Id { None = 0, Skin, Hair, Metal, Leather, Cloth, Wood, Stone, Plaster, Tile, Foliage, Grass, Dirt, Cobble, Gravel }

        private static Texture2DArray _tex;
        public static Texture2DArray Texture => _tex;

        /// <summary>Gera (se preciso) e publica a textura como global do shader.</summary>
        public static void Ensure(float tiling = 2.2f)
        {
            if (_tex == null) _tex = Build();
            Shader.SetGlobalTexture("_KezorDetail", _tex);
            Shader.SetGlobalFloat("_KezorDetailOn", 1f);
            Shader.SetGlobalFloat("_KezorDetailTiling", tiling);
            RefineNoise.Ensure();
        }

        public static Texture2DArray Build()
        {
            var t = new Texture2DArray(Size, Size, Layers, TextureFormat.RGBA32, true, true)
            { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "KezorDetail" };
            var h = new float[Size * Size];
            var a = new float[Size * Size];
            var px = new Color32[Size * Size];
            for (int l = 0; l < Layers; l++)
            {
                float strength = Fill((Id)(l + 1), h, a);
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        float dx = H(h, x + 1, y) - H(h, x - 1, y);
                        float dy = H(h, x, y + 1) - H(h, x, y - 1);
                        float nx = Mathf.Clamp(-dx * strength, -1f, 1f), ny = Mathf.Clamp(-dy * strength, -1f, 1f);
                        px[y * Size + x] = new Color32((byte)((nx * 0.5f + 0.5f) * 255), (byte)((ny * 0.5f + 0.5f) * 255),
                            (byte)(Mathf.Clamp01(a[y * Size + x]) * 255), 255);
                    }
                t.SetPixels32(px, l, 0);
            }
            t.Apply(true, true);
            return t;
        }

        private static float H(float[] h, int x, int y) => h[((y % Size + Size) % Size) * Size + ((x % Size + Size) % Size)];

        // ---- ruído periódico (repete a cada "period" células dentro da textura) ----
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
            int Wrap(int i) => ((i % period) + period) % period;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        private static float Fbm(float u, float v, int period, int oct, int seed)
        {
            float s = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < oct; o++) { s += Value(u, v, period << o, seed + o * 17) * amp; norm += amp; amp *= 0.5f; }
            return s / norm;
        }
        /// <summary>Distância à borda de células de Voronoi periódicas (pedras, escamas): x = F1, y = F2 - F1, z = id.</summary>
        private static Vector3 Voronoi(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float f1 = 9, f2 = 9, id = 0;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j;
                    int wx = ((gx % period) + period) % period, wy = ((gy % period) + period) % period;
                    float px = gx + 0.15f + 0.7f * Hash(wx, wy, seed), py = gy + 0.15f + 0.7f * Hash(wx, wy, seed + 5);
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < f1) { f2 = f1; f1 = d; id = Hash(wx, wy, seed + 9); } else if (d < f2) f2 = d;
                }
            return new Vector3(f1, f2 - f1, id);
        }

        /// <summary>Preenche altura (h) e tom (a, ~0.5 = neutro). Retorna a força do relevo.</summary>
        private static float Fill(Id id, float[] h, float[] a)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (float)x / Size, v = (float)y / Size;
                    float hh, aa;
                    switch (id)
                    {
                        case Id.Skin:
                            hh = Fbm(u, v, 8, 3, 1) * 0.3f;
                            aa = 0.5f + (Fbm(u, v, 6, 3, 2) - 0.5f) * 0.25f;
                            break;
                        case Id.Hair:
                        {   // fios: ruído esticado em uma direção
                            float s = Fbm(u * 1f, v * 0.25f + u * 0.05f, 16, 2, 3);
                            hh = s; aa = 0.35f + s * 0.4f;
                            break;
                        }
                        case Id.Metal:
                        {   // escovado + marcas de martelo
                            float brush = Fbm(u, v * 0.25f, 16, 2, 4);
                            var c = Voronoi(u, v, 6, 5);
                            float dent = Mathf.SmoothStep(0, 1, c.x) * 0.5f;
                            hh = dent * 0.2f + brush * 0.2f; aa = 0.45f + brush * 0.15f + (c.z - 0.5f) * 0.05f;
                            break;
                        }
                        case Id.Leather:
                        {
                            var c = Voronoi(u, v, 22, 6);
                            float g = Mathf.Clamp01(c.y * 3f);
                            hh = g * 0.5f + Fbm(u, v, 8, 3, 7) * 0.3f; aa = 0.35f + g * 0.25f + Fbm(u, v, 4, 3, 8) * 0.25f;
                            break;
                        }
                        case Id.Cloth:
                        {   // trama de tecido
                            float wx = Mathf.Sin(u * Mathf.PI * 2 * 16), wy = Mathf.Sin(v * Mathf.PI * 2 * 16);
                            float weave = ((x / 4 + y / 4) & 1) == 0 ? wx : wy;
                            hh = weave * 0.05f + Fbm(u, v, 8, 2, 9) * 0.2f; aa = 0.5f + weave * 0.04f + (Fbm(u, v, 4, 3, 10) - 0.5f) * 0.25f;
                            break;
                        }
                        case Id.Wood:
                        {   // veios: anéis distorcidos ao longo de v
                            float n = Fbm(u, v, 4, 3, 11);
                            float ring = Mathf.Sin((u * 14f + n * 3f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                            float grain = Fbm(u, v * 0.25f, 16, 2, 12);
                            hh = ring * 0.35f + grain * 0.35f; aa = 0.3f + ring * 0.35f + grain * 0.25f;
                            break;
                        }
                        case Id.Stone:
                        {   // pedras com juntas + rugosidade
                            var c = Voronoi(u, v, 5, 13);
                            float joint = Mathf.SmoothStep(0f, 0.12f, c.y);
                            float rough = Fbm(u, v, 8, 4, 14);
                            hh = joint * 0.7f + rough * 0.45f; aa = 0.25f + joint * 0.35f + rough * 0.3f + (c.z - 0.5f) * 0.2f;
                            break;
                        }
                        case Id.Plaster:
                        {
                            float n = Fbm(u, v, 4, 5, 15);
                            hh = n * 0.5f; aa = 0.45f + (n - 0.5f) * 0.4f + (Fbm(u, v, 16, 2, 16) - 0.5f) * 0.15f;
                            break;
                        }
                        case Id.Tile:
                        {   // telhas: fileiras com bordas curvas
                            float rows = 8f, cols = 6f;
                            float rv = v * rows; int r = Mathf.FloorToInt(rv);
                            float cu = u * cols + (r & 1) * 0.5f;
                            float fu = cu - Mathf.Floor(cu), fv = rv - r;
                            float bump = Mathf.Sin(fu * Mathf.PI);
                            hh = bump * 0.4f + fv * 0.5f; aa = 0.35f + bump * 0.25f + fv * 0.15f + (Hash(Mathf.FloorToInt(cu) % 6, r % 8, 17) - 0.5f) * 0.25f;
                            break;
                        }
                        case Id.Grass:
                        {   // tufos: manchas finas e alongadas, claras na ponta
                            float tuft = Fbm(u * 1f, v * 0.5f, 24, 2, 21);
                            float patch = Fbm(u, v, 3, 3, 22);
                            hh = tuft * 0.6f; aa = 0.25f + tuft * 0.45f + (patch - 0.5f) * 0.35f;
                            break;
                        }
                        case Id.Dirt:
                        {   // terra batida: torrões e pedrinhas soltas
                            float n = Fbm(u, v, 6, 4, 23);
                            var c = Voronoi(u, v, 18, 24);
                            float peb = c.x < 0.18f ? 1f - c.x / 0.18f : 0f;
                            hh = n * 0.5f + peb * 0.4f; aa = 0.35f + n * 0.3f + peb * 0.25f;
                            break;
                        }
                        case Id.Cobble:
                        {   // calçamento: pedras arredondadas com rejunte fundo
                            var c = Voronoi(u, v, 6, 25);
                            float stone = Mathf.SmoothStep(0f, 0.22f, c.y);
                            float dome = 1f - Mathf.Clamp01(c.x * 1.1f);
                            float rough = Fbm(u, v, 12, 3, 26);
                            hh = stone * (0.55f + dome * 0.45f) + rough * 0.12f;
                            aa = 0.12f + stone * 0.55f + (c.z - 0.5f) * 0.25f + rough * 0.15f;
                            break;
                        }
                        case Id.Gravel:
                        {
                            var c = Voronoi(u, v, 26, 27);
                            float g = Mathf.SmoothStep(0f, 0.25f, c.y);
                            hh = g * 0.6f; aa = 0.25f + g * 0.4f + (c.z - 0.5f) * 0.35f;
                            break;
                        }
                        default: // folhagem: manchas de folhas
                        {
                            var c = Voronoi(u, v, 14, 18);
                            float leaf = 1f - Mathf.Clamp01(c.x * 1.4f);
                            hh = leaf * 0.5f + Fbm(u, v, 8, 2, 19) * 0.2f; aa = 0.3f + leaf * 0.4f + (c.z - 0.5f) * 0.25f;
                            break;
                        }
                    }
                    h[y * Size + x] = hh; a[y * Size + x] = aa;
                }
            switch (id)
            {   // relevo suave: o visual estilizado vem da forma e do degradê; a textura só quebra a uniformidade
                case Id.Stone: case Id.Cobble: return 6f;
                case Id.Gravel: case Id.Dirt: return 4f;
                case Id.Grass: return 3f;
                case Id.Wood: case Id.Tile: return 4.5f;
                case Id.Leather: return 3.5f;
                case Id.Metal: return 2.5f;
                case Id.Cloth: case Id.Hair: return 3f;
                case Id.Skin: return 1.2f;
                default: return 4f;
            }
        }
    }
}
