using Kezor.Core.World;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Terreno mid poly: malha suave (vértices compartilhados, normais pela inclinação da altura) com splat por vértice
    /// — grama, terra batida, calçamento e cascalho — desenhado pelo shader Kezor/Terrain.
    /// </summary>
    public static partial class WorldBuilder
    {
        public const float TerrainCell = 1.25f;
        private const int TerrainChunk = 32;
        private static Material? _terrainMat;
        /// <summary>Layer "Ground" (terreno, calçamento, lajes): alvo do clique para andar. 0 se a layer não existir.</summary>
        public static int GroundLayer => LayerMask.NameToLayer("Ground");
        public static int GroundMask => GroundLayer >= 0 ? 1 << GroundLayer : 0;

        /// <summary>Põe o objeto na layer Ground com colisor de malha (só para o raio do clique).</summary>
        public static void MakeGround(GameObject go, Mesh mesh)
        {
            if (GroundLayer < 0) return;
            go.layer = GroundLayer;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        public static Material TerrainMaterial
        {
            get
            {
                if (_terrainMat != null) return _terrainMat;
                var sh = Shader.Find("Kezor/Terrain");
                _terrainMat = sh != null ? new Material(sh) { name = "Kezor Terrain" } : KezorLook.Material;
                if (sh != null) _terrainMat.SetFloat("_Tiling", 0.5f);
                return _terrainMat;
            }
        }

        /// <summary>smoothstep do GLSL (o Mathf.SmoothStep do Unity é outra coisa: interpola entre from e to).</summary>
        public static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

        private static float Soft(float dist, float half, float feather = 0.9f) => 1f - Smooth(half - feather, half + feather, dist);

        /// <summary>Quanto o ponto é rua (0..1, com borda suave). Dentro da vila = calçamento; fora = estrada de terra.</summary>
        public static float RoadAmount(float x, float z)
        {
            if (_map.Id != MapDefinition.ValeId) return 0f;
            float c = _map.SafeX;
            if (_map.HasWall && TownDist(x, z) < TownWallRadius + 1f)
            {   // vila quadrada: avenidas em cruz, anel interno, rua junto à muralha, praça, pátios e largos calçados
                float dx = Mathf.Abs(x - c), dz = Mathf.Abs(z - _map.SafeZ), dq = Mathf.Max(dx, dz);
                float r = Mathf.Max(Soft(dx, MapDefinition.AvenueHalfWidth), Soft(dz, MapDefinition.AvenueHalfWidth));
                r = Mathf.Max(r, Soft(Mathf.Abs(dq - MapDefinition.RingRoadRadius), MapDefinition.RingHalfWidth));
                r = Mathf.Max(r, Smooth(MapDefinition.LaneRadius - 0.9f, MapDefinition.LaneRadius + 0.9f, dq));
                foreach (var sq in _map.Squares) r = Mathf.Max(r, Soft(Vector2.Distance(new Vector2(x, z), new Vector2(sq.X, sq.Z)), sq.R, 0.6f));
                foreach (var pv in _map.Paved)
                {
                    float ox = Mathf.Max(pv.X0 - x, x - pv.X1), oz = Mathf.Max(pv.Z0 - z, z - pv.Z1);
                    r = Mathf.Max(r, 1f - Smooth(-0.6f, 0.6f, Mathf.Max(ox, oz)));
                }
                return Mathf.Max(r, Soft(Vector2.Distance(new Vector2(x, z), new Vector2(_map.SafeX, _map.SafeZ)), MapDefinition.PlazaRadius, 0.6f));
            }
            float wob = Mathf.Sin(z * 0.11f) * 1.5f, wob2 = Mathf.Sin(x * 0.09f) * 1.5f;
            return Mathf.Max(Soft(Mathf.Abs(x - c - wob), 2.2f, 1.2f), Soft(Mathf.Abs(z - _map.SafeZ - wob2), 2.2f, 1.2f));
        }

        /// <summary>Cor base e pesos (grama, terra, calçamento, cascalho) de um ponto do terreno.</summary>
        private static Color TerrainSplat(float x, float z, float h, float slope, out Vector4 w)
        {
            float jit = Mathf.PerlinNoise(x * 0.9f + 5.1f, z * 0.9f + 2.3f);
            if (_map.Id == MapDefinition.EscarpId) return FrostColor(x, z, h, slope, jit, out w);
            if (_map.Id == MapDefinition.SwampId)
            {   // pântano: lama (terra) com manchas de musgo (grama)
                var sc = SwampColor(x, z, h, jit);
                float moss = Smooth(0.45f, 0.65f, Mathf.PerlinNoise(x * 0.08f + 3f, z * 0.08f + 9f));
                w = new Vector4(moss, 1f - moss, 0, slope * 0.5f);
                return sc;
            }
            float dTown = TownDist(x, z);
            float road = RoadAmount(x, z);
            var grassNear = Color.Lerp(KezorLook.Hex("#4f6b34"), KezorLook.Hex("#5e7a3a"), jit);
            var grassDeep = Color.Lerp(KezorLook.Hex("#2f4a35"), KezorLook.Hex("#3a5238"), jit);
            float far = Mathf.InverseLerp(85f, 150f, dTown);
            var grass = Color.Lerp(grassNear, grassDeep, far);
            // manchas de musgo/grama alta mais escura e de terra seca
            float patch = Mathf.PerlinNoise(x * 0.07f + 13f, z * 0.07f + 4f);
            grass = Color.Lerp(grass, grass * 0.78f, Smooth(0.55f, 0.75f, patch));
            var dirt = Color.Lerp(KezorLook.Hex("#6b5638"), KezorLook.Hex("#5a4730"), jit);
            var gravel = Color.Lerp(KezorLook.Hex("#6e6a64"), KezorLook.Hex("#57534e"), jit);
            if (dTown < TownWallRadius + 0.5f)
            {   // vila: calçamento nas ruas e na praça, quintais de grama com terra
                bool plaza = Vector2.Distance(new Vector2(x, z), new Vector2(_map.SafeX, _map.SafeZ)) < MapDefinition.PlazaRadius + 0.6f;
                var cobble = plaza ? Color.Lerp(KezorLook.Hex("#8a8174"), KezorLook.Hex("#7a7266"), jit) : Color.Lerp(KezorLook.Hex("#6f665a"), KezorLook.Hex("#5f574d"), jit);
                float yardDirt = Smooth(0.35f, 0.7f, Mathf.PerlinNoise(x * 0.15f, z * 0.15f));
                var yard = Color.Lerp(grass, dirt, yardDirt * 0.7f);
                w = new Vector4((1f - road) * (1f - yardDirt * 0.7f), (1f - road) * yardDirt * 0.7f, road, 0);
                return Color.Lerp(yard, cobble, road);
            }
            // fora da vila: estradas de terra, encostas e montanhas de cascalho/rocha, cantos queimados dos golens
            float C = MapDefinition.ValeCenter;
            float ash = Mathf.Max(0, 1f - Mathf.Min(Vector2.Distance(new Vector2(x, z), new Vector2(C - 120, C + 120)), Vector2.Distance(new Vector2(x, z), new Vector2(C + 120, C - 120))) / 24f);
            float rock = Mathf.Max(Mathf.InverseLerp(3.5f, 6f, h), Mathf.InverseLerp(0.45f, 0.75f, slope));
            float wDirt = Mathf.Max(road, Smooth(0.62f, 0.8f, patch) * 0.6f);
            float wGravel = Mathf.Max(rock, ash * 0.8f);
            float wGrass = Mathf.Max(0f, 1f - wDirt - wGravel);
            w = new Vector4(wGrass, wDirt * (1f - wGravel), 0, wGravel);
            var rockC = Color.Lerp(gravel, KezorLook.Hex("#8c8a86"), Mathf.InverseLerp(4f, 12f, h));
            var col = grass * w.x + dirt * w.y + rockC * w.w;
            float sum = w.x + w.y + w.w;
            col = sum > 0.001f ? col / sum : grass;
            col = Color.Lerp(col, KezorLook.Hex("#4a4038"), ash * 0.85f);
            col.a = 1f;
            return col;
        }

        private static void BuildTerrain(Transform parent)
        {
            float cell = TerrainCell;
            int n = Mathf.CeilToInt((_map.Size + 20f) / cell);
            float o = -10f;
            for (int cx = 0; cx < n; cx += TerrainChunk)
                for (int cz = 0; cz < n; cz += TerrainChunk)
                {
                    int nx = Mathf.Min(TerrainChunk, n - cx), nz = Mathf.Min(TerrainChunk, n - cz);
                    int vx = nx + 1, vz = nz + 1;
                    var verts = new Vector3[vx * vz];
                    var norms = new Vector3[vx * vz];
                    var cols = new Color[vx * vz];
                    var uv1 = new Vector2[vx * vz];
                    var uv2 = new Vector2[vx * vz];
                    for (int j = 0; j < vz; j++)
                        for (int i = 0; i < vx; i++)
                        {
                            float x = o + (cx + i) * cell, z = o + (cz + j) * cell;
                            // leve deslocamento orgânico para o grid não aparecer
                            float jx = (Mathf.PerlinNoise(x * 0.31f, z * 0.31f) - 0.5f) * cell * 0.35f;
                            float jz = (Mathf.PerlinNoise(x * 0.31f + 7f, z * 0.31f + 3f) - 0.5f) * cell * 0.35f;
                            if (cx + i == 0 || cz + j == 0 || cx + i == n || cz + j == n) { jx = 0; jz = 0; }
                            x += jx; z += jz;
                            float h = Height(x, z);
                            float e = 0.6f;
                            var nrm = new Vector3(Height(x - e, z) - Height(x + e, z), 2f * e, Height(x, z - e) - Height(x, z + e)).normalized;
                            float slope = 1f - nrm.y;
                            int k = j * vx + i;
                            verts[k] = new Vector3(x, h, z);
                            norms[k] = nrm;
                            cols[k] = TerrainSplat(x, z, h, slope * 3f, out var w);
                            uv1[k] = new Vector2(w.x, w.y);
                            uv2[k] = new Vector2(w.z, w.w);
                        }
                    var tris = new int[nx * nz * 6];
                    int t = 0;
                    for (int j = 0; j < nz; j++)
                        for (int i = 0; i < nx; i++)
                        {
                            int a = j * vx + i, b = a + 1, c = a + vx, d = c + 1;
                            if (((cx + i + cz + j) & 1) == 0) { tris[t++] = a; tris[t++] = c; tris[t++] = d; tris[t++] = a; tris[t++] = d; tris[t++] = b; }
                            else { tris[t++] = a; tris[t++] = c; tris[t++] = b; tris[t++] = b; tris[t++] = c; tris[t++] = d; }
                        }
                    var mesh = new Mesh { name = "terrain" };
                    mesh.vertices = verts; mesh.normals = norms; mesh.colors = cols;
                    mesh.SetUVs(1, uv1); mesh.SetUVs(2, uv2);
                    mesh.triangles = tris;
                    mesh.RecalculateBounds();
                    var go = KezorLook.MakeObject($"Terreno {cx},{cz}", mesh, parent);
                    go.GetComponent<MeshRenderer>().sharedMaterial = TerrainMaterial;
                    MakeGround(go, mesh);
                }
        }
    }
}
