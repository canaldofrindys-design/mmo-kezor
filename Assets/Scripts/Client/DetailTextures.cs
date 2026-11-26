using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Texturas do visual mid poly texturizado (Kezor/LowPoly, UV2.x = id do material; Kezor/Terrain, pesos por vértice).
    /// Geradas proceduralmente uma vez (arte 100% original, sem arquivo), repetíveis (tileable), 256 px por camada:
    /// RG = normal xy, B = tom (0,5 = cor do vértice; o shader multiplica), A = cavidade (juntas, frestas, rejunte e
    /// sujeira escurecem). Ids: 1 pele, 2 cabelo/pelo, 3 metal, 4 couro, 5 tecido, 6 madeira (veio), 7 pedra, 8 reboco,
    /// 9 telha, 10 folhagem; terreno: 11 grama, 12 terra, 13 calçamento, 14 cascalho; construções: 15 tábuas, 16 alvenaria.
    /// </summary>
    public static class DetailTextures
    {
        public const int Size = 256;
        public const int Layers = 16;
        public enum Id { None = 0, Skin, Hair, Metal, Leather, Cloth, Wood, Stone, Plaster, Tile, Foliage, Grass, Dirt, Cobble, Gravel, Planks, Masonry }

        private static Texture2DArray? _tex;
        public static Texture2DArray? Texture => _tex;

        /// <summary>Gera (se preciso) e publica a textura como global do shader.</summary>
        public static void Ensure(float tiling = 1f)
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
            { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "KezorDetail" };
            var h = new float[Size * Size];
            var a = new float[Size * Size];
            var cav = new float[Size * Size];
            var px = new Color32[Size * Size];
            for (int l = 0; l < Layers; l++)
            {
                System.Array.Clear(cav, 0, cav.Length);
                float strength = Fill((Id)(l + 1), h, a, cav);
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        float dx = H(h, x + 1, y) - H(h, x - 1, y);
                        float dy = H(h, x, y + 1) - H(h, x, y - 1);
                        float nx = Mathf.Clamp(-dx * strength, -1f, 1f), ny = Mathf.Clamp(-dy * strength, -1f, 1f);
                        int k = y * Size + x;
                        px[k] = new Color32((byte)((nx * 0.5f + 0.5f) * 255), (byte)((ny * 0.5f + 0.5f) * 255),
                            (byte)(Mathf.Clamp01(a[k]) * 255), (byte)(Mathf.Clamp01(cav[k]) * 255));
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
        /// <summary>Ruído esticado: períodos diferentes em u e v (veio de madeira, fios), ainda repetível.</summary>
        private static float Streak(float u, float v, int pu, int pv, int oct, int seed)
        {
            float s = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < oct; o++)
            {
                int qu = pu << o, qv = pv << o;
                float x = u * qu, y = v * qv;
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
                int Wu(int i) => ((i % qu) + qu) % qu; int Wv(int i) => ((i % qv) + qv) % qv;
                int sd = seed + o * 31;
                float a = Hash(Wu(x0), Wv(y0), sd), b = Hash(Wu(x0 + 1), Wv(y0), sd), c = Hash(Wu(x0), Wv(y0 + 1), sd), d = Hash(Wu(x0 + 1), Wv(y0 + 1), sd);
                s += Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy) * amp; norm += amp; amp *= 0.5f;
            }
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
        private static float Frac(float x) => x - Mathf.Floor(x);
        private static float Edge(float f, float w) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(f, 1f - f) / w));

        /// <summary>Preenche altura (h), tom (a, 0,5 = neutro) e cavidade (cav). Retorna a força do relevo.</summary>
        private static float Fill(Id id, float[] h, float[] a, float[] cav)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (float)x / Size, v = (float)y / Size;
                    float hh, aa, cc = 0f;
                    switch (id)
                    {
                        case Id.Skin:
                            hh = Fbm(u, v, 8, 3, 1) * 0.25f;
                            aa = 0.5f + (Fbm(u, v, 5, 3, 2) - 0.5f) * 0.18f;
                            break;
                        case Id.Hair:
                        {   // mechas: fios finos e longos, fundo das mechas mais escuro
                            float s = Streak(u, v, 48, 3, 3, 3);
                            float lock_ = Streak(u, v, 10, 2, 2, 4);
                            hh = s * 0.7f + lock_ * 0.3f; aa = 0.3f + s * 0.45f + lock_ * 0.2f;
                            cc = Mathf.Pow(1f - s, 3f) * 0.55f;
                            break;
                        }
                        case Id.Metal:
                        {   // escovado + riscos + marcas de martelo; bordas de uso mais claras
                            float brush = Streak(u, v, 64, 4, 2, 5);
                            var c = Voronoi(u, v, 7, 6);
                            float dent = Mathf.SmoothStep(0, 1, c.x);
                            float scratch = 0f;
                            for (int k = 0; k < 6; k++)
                            {
                                float ang = Hash(k, 1, 7) * 6.283f, off = Hash(k, 2, 7);
                                float d = Frac(u * Mathf.Cos(ang) * 3f + v * Mathf.Sin(ang) * 3f + off);
                                scratch = Mathf.Max(scratch, Mathf.Clamp01(1f - Mathf.Abs(d - 0.5f) * 140f) * (Fbm(u, v, 4, 2, 8 + k) > 0.55f ? 1f : 0f));
                            }
                            hh = dent * 0.25f + brush * 0.15f - scratch * 0.1f;
                            aa = 0.42f + brush * 0.16f + (c.z - 0.5f) * 0.1f + scratch * 0.25f;
                            cc = Mathf.Clamp01((Fbm(u, v, 3, 4, 9) - 0.55f) * 2.2f) * 0.6f;   // manchas de oxidação
                            break;
                        }
                        case Id.Leather:
                        {   // grão de couro, vincos e costura
                            var c = Voronoi(u, v, 26, 10);
                            float g = Mathf.Clamp01(c.y * 3f);
                            float crease = Mathf.Clamp01(1f - Mathf.Abs(Fbm(u, v, 3, 3, 11) - 0.5f) * 18f);
                            float stitch = (Edge(Frac(v * 2f), 0.02f) < 0.5f && Frac(u * 32f) < 0.55f) ? 1f : 0f;
                            hh = g * 0.5f + Fbm(u, v, 8, 3, 12) * 0.3f - crease * 0.3f;
                            aa = 0.32f + g * 0.25f + Fbm(u, v, 4, 3, 13) * 0.28f + stitch * 0.2f;
                            cc = (1f - g) * 0.35f + crease * 0.45f;
                            break;
                        }
                        case Id.Cloth:
                        {   // trama visível (32 fios), fios irregulares, manchas e desbotado
                            float tu = Frac(u * 32f), tv = Frac(v * 32f);
                            bool over = ((Mathf.FloorToInt(u * 32f) + Mathf.FloorToInt(v * 32f)) & 1) == 0;
                            float thread = over ? Mathf.Sin(tv * Mathf.PI) : Mathf.Sin(tu * Mathf.PI);
                            float irr = Streak(u, v, 32, 4, 2, 14);
                            hh = thread * 0.35f + irr * 0.15f;
                            aa = 0.38f + thread * 0.18f + (irr - 0.5f) * 0.12f + (Fbm(u, v, 3, 4, 15) - 0.5f) * 0.35f;
                            cc = (1f - thread) * 0.3f;
                            break;
                        }
                        case Id.Wood:
                        {   // veio longo e contínuo (cabos, troncos, carroças), nós ocasionais
                            float warp = Fbm(u, v, 3, 3, 16);
                            float ring = Frac(u * 9f + warp * 1.6f);
                            float line = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(ring - 0.5f) * 2f), 6f);
                            float grain = Streak(u, v, 48, 3, 3, 17);
                            var knot = Voronoi(u, v, 3, 18);
                            float k = knot.z > 0.7f ? Mathf.Clamp01(1f - knot.x * 6f) : 0f;
                            hh = grain * 0.4f - line * 0.3f - k * 0.4f;
                            aa = 0.36f + grain * 0.3f - line * 0.18f - k * 0.2f + (Fbm(u, v, 2, 3, 19) - 0.5f) * 0.2f;
                            cc = line * 0.45f + k * 0.6f;
                            break;
                        }
                        case Id.Stone:
                        {   // rocha natural: placas com rachaduras, rugosidade e manchas
                            var c = Voronoi(u, v, 5, 20);
                            float crack = 1f - Mathf.SmoothStep(0f, 0.08f, c.y);
                            float rough = Fbm(u, v, 8, 5, 21);
                            hh = (1f - crack) * 0.55f + rough * 0.5f;
                            aa = 0.3f + rough * 0.35f + (c.z - 0.5f) * 0.25f + (Fbm(u, v, 2, 3, 22) - 0.5f) * 0.25f;
                            cc = crack * 0.8f + Mathf.Clamp01((0.42f - rough) * 3f) * 0.3f;
                            break;
                        }
                        case Id.Plaster:
                        {   // reboco caiado: manchas de umidade, rachaduras finas, sujeira embaixo
                            float n = Fbm(u, v, 4, 5, 23);
                            float stain = Mathf.Clamp01((Fbm(u, v, 2, 4, 24) - 0.5f) * 2.5f);
                            var cv = Voronoi(u, v, 4, 25);   // rachaduras finas nas bordas de células, só em alguns trechos
                            float crack = (1f - Mathf.SmoothStep(0f, 0.025f, cv.y)) * Mathf.Clamp01((Fbm(u, v, 3, 3, 26) - 0.6f) * 6f);
                            hh = n * 0.4f - crack * 0.4f;
                            aa = 0.45f + (n - 0.5f) * 0.3f + (Fbm(u, v, 24, 2, 27) - 0.5f) * 0.12f - stain * 0.12f;
                            cc = stain * 0.45f + crack * 0.8f;
                            break;
                        }
                        case Id.Tile:
                        {   // telhas: fileiras curvas desencontradas, sombra embaixo de cada fileira, musgo
                            float rows = 6f, cols = 5f;
                            float rv = v * rows; int r = Mathf.FloorToInt(rv);
                            float cu = u * cols + (r & 1) * 0.5f;
                            float fu = Frac(cu), fv = rv - r;
                            float bump = Mathf.Sin(fu * Mathf.PI);
                            float tone = Hash(((Mathf.FloorToInt(cu) % 5) + 5) % 5, r % 6, 28);
                            float moss = Mathf.Clamp01((Fbm(u, v, 4, 4, 29) - 0.6f) * 3f);
                            hh = bump * 0.5f + fv * 0.5f;
                            aa = 0.3f + bump * 0.25f + fv * 0.2f + (tone - 0.5f) * 0.3f;
                            cc = Mathf.Pow(1f - fv, 6f) * 0.85f + (1f - bump) * 0.35f + moss * 0.3f;
                            break;
                        }
                        case Id.Grass:
                        {   // tufos: manchas finas e alongadas, claras na ponta
                            float tuft = Streak(u, v, 24, 12, 2, 31);
                            float patch = Fbm(u, v, 3, 3, 32);
                            hh = tuft * 0.6f; aa = 0.25f + tuft * 0.45f + (patch - 0.5f) * 0.35f;
                            cc = Mathf.Clamp01(0.4f - tuft) * 0.6f;
                            break;
                        }
                        case Id.Dirt:
                        {   // terra batida: torrões e pedrinhas soltas
                            float n = Fbm(u, v, 6, 4, 33);
                            var c = Voronoi(u, v, 18, 34);
                            float peb = c.x < 0.18f ? 1f - c.x / 0.18f : 0f;
                            hh = n * 0.5f + peb * 0.4f; aa = 0.35f + n * 0.3f + peb * 0.25f;
                            cc = Mathf.Clamp01(0.45f - n) * 0.8f;
                            break;
                        }
                        case Id.Cobble:
                        {   // calçamento: pedras arredondadas com rejunte fundo
                            var c = Voronoi(u, v, 6, 35);
                            float stone = Mathf.SmoothStep(0f, 0.22f, c.y);
                            float dome = 1f - Mathf.Clamp01(c.x * 1.1f);
                            float rough = Fbm(u, v, 12, 3, 36);
                            hh = stone * (0.55f + dome * 0.45f) + rough * 0.12f;
                            aa = 0.12f + stone * 0.55f + (c.z - 0.5f) * 0.25f + rough * 0.15f;
                            cc = (1f - stone) * 0.9f;
                            break;
                        }
                        case Id.Gravel:
                        {
                            var c = Voronoi(u, v, 26, 37);
                            float g = Mathf.SmoothStep(0f, 0.25f, c.y);
                            hh = g * 0.6f; aa = 0.25f + g * 0.4f + (c.z - 0.5f) * 0.35f;
                            cc = (1f - g) * 0.6f;
                            break;
                        }
                        case Id.Planks:
                        {   // tábuas verticais (5 por repetição), emendas desencontradas, veio, pregos
                            float bu = u * 5f; int b = Mathf.FloorToInt(bu); float fb = bu - b;
                            float off = Hash(b, 0, 40);
                            float bv = Frac(v + off); bool joint = bv < 0.012f || bv > 0.988f;
                            float seam = 1f - Edge(fb, 0.05f);
                            float warp = Fbm(u, v, 3, 2, 41);
                            float line = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Frac(fb * 3f + warp * 1.4f + off * 3f) - 0.5f) * 2f), 8f);
                            float grain = Streak(u, v, 80, 3, 3, 42);
                            float nail = 0f;
                            foreach (float ny in new[] { 0.03f, 0.97f })
                            { float dn = Mathf.Sqrt((fb - 0.5f) * (fb - 0.5f) * 25f + (bv - ny) * (bv - ny) * 4f); nail = Mathf.Max(nail, Mathf.Clamp01(1f - dn * 60f)); }
                            float tone = (Hash(b, 1, 43) - 0.5f) * 0.3f;
                            hh = grain * 0.3f - line * 0.2f - seam * 0.8f - (joint ? 0.6f : 0f);
                            aa = 0.4f + grain * 0.25f - line * 0.15f + tone - nail * 0.25f;
                            cc = Mathf.Max(seam, joint ? 1f : 0f) * 0.9f + line * 0.3f + nail * 0.6f;
                            break;
                        }
                        default: // Masonry — alvenaria: blocos desencontrados de tamanhos variados, rejunte, quinas lascadas
                        {
                            if (id == Id.Foliage)
                            {   // folhagem: folhas sobrepostas com nervura e vão escuro entre elas
                                var c = Voronoi(u, v, 16, 44);
                                float leaf = 1f - Mathf.Clamp01(c.x * 1.5f);
                                hh = leaf * 0.6f + Fbm(u, v, 8, 2, 45) * 0.2f;
                                aa = 0.28f + leaf * 0.42f + (c.z - 0.5f) * 0.3f;
                                cc = Mathf.Clamp01(0.35f - leaf) * 1.6f;
                                break;
                            }
                            float rows = 4f;
                            float rv = v * rows; int r = Mathf.FloorToInt(rv); float fr = rv - r;
                            float bw = 2f + Hash(r, 0, 46) * 1.0f;          // blocos por linha: 2..3
                            int nb = Mathf.Max(2, Mathf.RoundToInt(bw));
                            float cu = u * nb + Hash(r, 1, 47); int bi = Mathf.FloorToInt(cu); float fc = cu - bi;
                            float mort = 1f - Mathf.Min(Edge(fr, 0.07f), Edge(fc, 0.05f));
                            float rough = Fbm(u, v, 12, 4, 48);
                            float chip = Mathf.Clamp01((Fbm(u, v, 16, 2, 49) - 0.62f) * 4f) * (1f - Mathf.Min(Edge(fr, 0.16f), Edge(fc, 0.12f)));
                            float tone = (Hash(bi % nb, r, 50) - 0.5f) * 0.35f;
                            hh = (1f - mort) * 0.6f + rough * 0.3f - chip * 0.4f;
                            aa = 0.38f + rough * 0.28f + tone + (Fbm(u, v, 2, 3, 51) - 0.5f) * 0.15f;
                            cc = mort * 0.85f + chip * 0.5f + Mathf.Clamp01((0.4f - rough) * 2f) * 0.2f;
                            break;
                        }
                    }
                    h[y * Size + x] = hh; a[y * Size + x] = aa; cav[y * Size + x] = cc;
                }
            switch (id)
            {   // força do relevo (Size 256: um texel é metade do de antes, então o gradiente dobra)
                case Id.Stone: case Id.Cobble: case Id.Masonry: return 9f;
                case Id.Gravel: case Id.Dirt: return 6f;
                case Id.Grass: return 5f;
                case Id.Wood: case Id.Tile: case Id.Planks: return 8f;
                case Id.Leather: return 6f;
                case Id.Metal: return 4f;
                case Id.Cloth: case Id.Hair: return 5f;
                case Id.Skin: return 2f;
                default: return 7f;
            }
        }
    }
}
