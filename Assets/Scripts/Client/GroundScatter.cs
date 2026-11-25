using System.Collections.Generic;
using Kezor.Core.World;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Vegetação e miudezas do chão desenhadas por instância (GPU instancing): tufos de grama, flores, arbustos, folhas
    /// caídas e pedrinhas. Guardadas por blocos de 20 m; só os blocos perto do foco da câmera são desenhados.
    /// A grama e as flores balançam com o vento no shader (_Wind).
    /// </summary>
    public sealed class GroundScatter : MonoBehaviour
    {
        private const float Block = 20f;
        public float DrawRadius = 62f;

        private sealed class Kind
        {
            public Mesh Mesh = null!;
            public Material Mat = null!;
            public readonly Dictionary<long, List<Matrix4x4>> Blocks = new Dictionary<long, List<Matrix4x4>>();
        }

        private readonly List<Kind> _kinds = new List<Kind>();
        private static readonly Matrix4x4[] Buf = new Matrix4x4[1023];
        public int Instances { get; private set; }

        private static long Key(int bx, int bz) => ((long)bx << 32) ^ (uint)bz;

        private Kind AddKind(Mesh mesh, float wind)
        {
            var mat = new Material(KezorLook.Material) { name = "Kezor Scatter", enableInstancing = true };
            mat.SetFloat("_Wind", wind);
            var k = new Kind { Mesh = mesh, Mat = mat };
            _kinds.Add(k);
            return k;
        }

        private void Put(Kind k, Vector3 p, float yaw, float s)
        {
            int bx = Mathf.FloorToInt(p.x / Block), bz = Mathf.FloorToInt(p.z / Block);
            long key = Key(bx, bz);
            if (!k.Blocks.TryGetValue(key, out var list)) k.Blocks[key] = list = new List<Matrix4x4>();
            list.Add(Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one * s));
            Instances++;
        }

        // ---------------------------------------------------------------- malhas (arte original, geradas aqui)
        private static Mesh GrassTuft(Color baseC, Color tipC, int seed)
        {
            var b = new MeshBuilder();
            var r = new System.Random(seed);
            int blades = 5;
            for (int i = 0; i < blades; i++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f, rr = (float)r.NextDouble() * 0.16f;
                var root = new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr);
                float h = 0.4f + (float)r.NextDouble() * 0.35f, w = 0.05f + (float)r.NextDouble() * 0.03f;
                float lean = (float)r.NextDouble() * Mathf.PI * 2f;
                var side = new Vector3(Mathf.Cos(a + 1.57f), 0, Mathf.Sin(a + 1.57f)) * w;
                var mid = root + new Vector3(Mathf.Cos(lean), 0, Mathf.Sin(lean)) * h * 0.18f + Vector3.up * h * 0.55f;
                var tip = root + new Vector3(Mathf.Cos(lean), 0, Mathf.Sin(lean)) * h * 0.45f + Vector3.up * h;
                var c0 = Color.Lerp(baseC, tipC, 0.3f + (float)r.NextDouble() * 0.2f);
                // lâmina em dois trechos (curva), dupla face
                b.Quad(root - side, root + side, mid + side * 0.6f, mid - side * 0.6f, baseC);
                b.Quad(mid - side * 0.6f, mid + side * 0.6f, root + side, root - side, baseC);
                b.Tri(mid - side * 0.6f, mid + side * 0.6f, tip, c0);
                b.Tri(tip, mid + side * 0.6f, mid - side * 0.6f, c0);
            }
            return UpNormals(b.ToMesh("grama"));
        }

        /// <summary>Folhas finas iluminadas como o chão (normal para cima): sem lâminas pretas contra a luz.</summary>
        private static Mesh UpNormals(Mesh m)
        {
            var v = m.vertices; var n = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) n[i] = (Vector3.up * 1.6f + new Vector3(v[i].x, 0, v[i].z)).normalized;
            m.normals = n;
            return m;
        }

        private static Mesh Flower(Color petal, Color heart, int seed)
        {
            var b = new MeshBuilder();
            var r = new System.Random(seed);
            var stem = KezorLook.Hex("#3f5a2a");
            for (int f = 0; f < 3; f++)
            {
                float a = (float)r.NextDouble() * 6.28f, rr = 0.05f + (float)r.NextDouble() * 0.12f;
                var root = new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr);
                float h = 0.28f + (float)r.NextDouble() * 0.2f;
                var top = root + Vector3.up * h;
                var sd = new Vector3(0.012f, 0, 0.012f);
                b.Quad(root - sd, root + sd, top + sd, top - sd, stem);
                b.Quad(top - sd, top + sd, root + sd, root - sd, stem);
                for (int p = 0; p < 5; p++)
                {
                    float pa = p * 1.2566f + a;
                    var dir = new Vector3(Mathf.Cos(pa), 0.25f, Mathf.Sin(pa));
                    var side = new Vector3(-dir.z, 0, dir.x) * 0.035f;
                    b.Tri(top, top + dir * 0.075f + side, top + dir * 0.075f - side, petal);
                    b.Tri(top, top + dir * 0.075f - side, top + dir * 0.075f + side, petal * 0.85f);
                }
                b.Ico(top + Vector3.up * 0.01f, Vector3.one * 0.022f, heart);
            }
            // folhinhas na base
            var leaf = KezorLook.Hex("#4a6a30");
            for (int l = 0; l < 3; l++)
            {
                float a = l * 2.1f;
                var d = new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a));
                var sd = new Vector3(-d.z, 0, d.x) * 0.04f;
                b.Tri(Vector3.zero, d * 0.16f + sd, d * 0.16f - sd, leaf);
                b.Tri(Vector3.zero, d * 0.16f - sd, d * 0.16f + sd, leaf);
            }
            return UpNormals(b.ToMesh("flor"));
        }

        private static Mesh Bush(Color leaf, int seed)
        {
            var b = new MeshBuilder();
            var r = new System.Random(seed);
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3(((float)r.NextDouble() - 0.5f) * 0.6f, 0.28f + (float)r.NextDouble() * 0.2f, ((float)r.NextDouble() - 0.5f) * 0.6f);
                float s = 0.3f + (float)r.NextDouble() * 0.2f;
                b.Ico(c, new Vector3(s, s * 0.8f, s), Color.Lerp(leaf, leaf * 1.2f, (float)r.NextDouble()), 0.18f, seed + i, leaf * 0.85f);
            }
            return b.ToMesh("arbusto");
        }

        private static Mesh Pebbles(int seed)
        {
            var b = new MeshBuilder();
            var r = new System.Random(seed);
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3(((float)r.NextDouble() - 0.5f) * 0.5f, 0.02f, ((float)r.NextDouble() - 0.5f) * 0.5f);
                float s = 0.05f + (float)r.NextDouble() * 0.07f;
                var g = Color.Lerp(KezorLook.Hex("#7a756c"), KezorLook.Hex("#5a5650"), (float)r.NextDouble());
                b.Ico(c, new Vector3(s, s * 0.6f, s * 0.9f), g, 0.2f, seed + i, g * 0.85f);
            }
            return b.ToMesh("pedrinhas");
        }

        private static Mesh Leaves(int seed)
        {
            var b = new MeshBuilder();
            var r = new System.Random(seed);
            var cols = new[] { KezorLook.Hex("#8a5a2a"), KezorLook.Hex("#a3702e"), KezorLook.Hex("#6e4a26"), KezorLook.Hex("#7d6a2c") };
            for (int i = 0; i < 7; i++)
            {
                var c = new Vector3(((float)r.NextDouble() - 0.5f) * 0.9f, 0.015f + i * 0.002f, ((float)r.NextDouble() - 0.5f) * 0.9f);
                float a = (float)r.NextDouble() * 6.28f;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.07f;
                var s = new Vector3(-d.z, 0.01f, d.x) * 0.55f;
                b.Quad(c - d, c - s, c + d, c + s, cols[r.Next(cols.Length)]);
            }
            return UpNormals(b.ToMesh("folhas"));
        }

        private static Mesh Puddle(int seed, Color waterC)
        {
            var b = new MeshBuilder();
            var r = new System.Random(seed);
            int n = 14;
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float rr = 0.7f + (float)r.NextDouble() * 0.45f;
                pts[i] = new Vector3(Mathf.Cos(a) * rr * 1.3f, 0.03f, Mathf.Sin(a) * rr);
            }
            // água: tom do céu noturno, levemente emissiva (reflexo); borda de lama escura
            var water = KezorLook.Glow(waterC, 0.12f);
            var mud = KezorLook.Hex("#3a2e22");
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                b.Tri(Vector3.up * 0.03f, pts[j], pts[i], water);
                b.Quad(pts[i], pts[j], pts[j] * 1.18f - Vector3.up * 0.02f, pts[i] * 1.18f - Vector3.up * 0.02f, mud);
            }
            return b.ToMesh("poca");
        }

        // ---------------------------------------------------------------- distribuição
        public static GroundScatter Build(Transform parent, MapDefinition map)
        {
            var go = new GameObject("Vegetação (instâncias)");
            go.transform.SetParent(parent, false);
            var gs = go.AddComponent<GroundScatter>();
            gs.Populate(map);
            return gs;
        }

        private bool Blocked(MapDefinition map, float x, float z, float pad)
        {
            foreach (var h in map.Houses) if ((h.X - x) * (h.X - x) + (h.Z - z) * (h.Z - z) < (3.6f * h.Scale + pad) * (3.6f * h.Scale + pad)) return true;
            foreach (var p in map.Props) if ((p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z) < (1.2f + pad) * (1.2f + pad)) return true;
            foreach (var o in map.Obstacles) if ((o.X - x) * (o.X - x) + (o.Z - z) * (o.Z - z) < (o.R + pad) * (o.R + pad)) return true;
            return false;
        }

        private void Populate(MapDefinition map)
        {
            bool swamp = map.Id == MapDefinition.SwampId;
            var grassA = AddKind(GrassTuft(KezorLook.Hex(swamp ? "#3a4224" : "#3f5a2a"), KezorLook.Hex(swamp ? "#7a8040" : "#94b052"), 1), 0.10f);
            var grassB = AddKind(GrassTuft(KezorLook.Hex(swamp ? "#2f3a22" : "#35502a"), KezorLook.Hex(swamp ? "#66724a" : "#82a446"), 2), 0.10f);
            var grassDry = AddKind(GrassTuft(KezorLook.Hex("#5a5430"), KezorLook.Hex("#a39a5a"), 3), 0.10f);
            var flowerW = AddKind(Flower(KezorLook.Hex("#e8e2cf"), KezorLook.Hex("#e0b040"), 4), 0.12f);
            var flowerY = AddKind(Flower(KezorLook.Hex("#e6b43a"), KezorLook.Hex("#8a4a20"), 5), 0.12f);
            var flowerP = AddKind(Flower(KezorLook.Hex("#9a6ab8"), KezorLook.Hex("#f0d070"), 6), 0.12f);
            var flowerE = AddKind(Flower(KezorLook.Hex("#e0702a"), KezorLook.Hex("#ffd080"), 7), 0.12f);   // flor-brasa (cor do Kezor)
            var bush = AddKind(Bush(KezorLook.Hex(swamp ? "#34401f" : "#3e5a2c"), 8), 0.02f);
            var leaves = AddKind(Leaves(9), 0f);
            var pebbles = AddKind(Pebbles(10), 0f);
            var puddle = AddKind(Puddle(11, KezorLook.Hex(swamp ? "#14201a" : "#1c2638")), 0f);

            var rnd = new System.Random(map.Id * 7919 + 17);
            float size = map.Size;
            float cx = map.SafeX, cz = map.SafeZ;
            int tries = Mathf.RoundToInt(size * size * 3.2f);
            for (int i = 0; i < tries; i++)
            {
                float x = (float)rnd.NextDouble() * size, z = (float)rnd.NextDouble() * size;
                float edge = Mathf.Min(Mathf.Min(x, z), Mathf.Min(size - x, size - z));
                if (edge < 6f) continue;
                float road = WorldBuilder.RoadAmount(x, z);
                float dTown = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                bool town = map.Id == MapDefinition.ValeId && dTown < WorldBuilder.TownWallRadius + 1.5f;
                if (town && Mathf.Abs(dTown - WorldBuilder.TownWallRadius) < 2.2f) continue;     // pé da muralha
                if (Blocked(map, x, z, 0.4f)) continue;
                float h = WorldBuilder.Height(x, z);
                if (swamp && h < -0.35f) continue;                                               // dentro da água
                if (h > 4.5f) { if (rnd.NextDouble() < 0.03) Put(pebbles, new Vector3(x, h, z), Rnd(rnd, 360), 1f + (float)rnd.NextDouble()); continue; }
                float dens = Mathf.PerlinNoise(x * 0.06f + 2f, z * 0.06f + 8f);
                float yaw = Rnd(rnd, 360f);
                var p = new Vector3(x, h, z);
                double k = rnd.NextDouble();
                if (road > 0.4f)
                {   // rua/estrada: só pedrinhas, folhas e poças nas estradas de terra
                    if (k < 0.015) Put(pebbles, p, yaw, 0.8f + (float)rnd.NextDouble() * 0.6f);
                    else if (!town && k < 0.019) Put(leaves, p, yaw, 1f);
                    else if (!town && road > 0.8f && k < 0.0202) Put(puddle, p, yaw, 0.9f + (float)rnd.NextDouble() * 1.1f);
                    continue;
                }
                if (town)
                {   // quintais: grama mais rala, canteiros de flores
                    if (k < 0.3 * dens) Put(k < 0.15 ? grassA : grassB, p, yaw, 0.8f + (float)rnd.NextDouble() * 0.5f);
                    else if (k < 0.3 * dens + 0.012) Put(k < 0.3 * dens + 0.006 ? flowerW : flowerE, p, yaw, 1f);
                    else if (k < 0.3 * dens + 0.014) Put(pebbles, p, yaw, 1f);
                    continue;
                }
                // campo (por tentativa: ~55% grama nas áreas densas; flores, arbustos e miudezas bem mais raros)
                if (k < 0.5 * dens + 0.06) Put(dens > 0.62 ? grassDry : (k < 0.28 ? grassA : grassB), p, yaw, 0.85f + (float)rnd.NextDouble() * 0.6f);
                else if (k < 0.5 * dens + 0.075) Put(PickFlower(rnd, flowerW, flowerY, flowerP, flowerE, swamp), p, yaw, 0.9f + (float)rnd.NextDouble() * 0.4f);
                else if (k < 0.5 * dens + 0.081) Put(bush, p, yaw, 0.8f + (float)rnd.NextDouble() * 0.7f);
                else if (k < 0.5 * dens + 0.085) Put(leaves, p, yaw, 1f);
                else if (k < 0.5 * dens + 0.089) Put(pebbles, p, yaw, 1f);
                else if (swamp && k < 0.5 * dens + 0.0905) Put(puddle, p, yaw, 1f + (float)rnd.NextDouble());
            }
        }

        private static float Rnd(System.Random r, float max) => (float)r.NextDouble() * max;

        private static Kind PickFlower(System.Random r, Kind w, Kind y, Kind p, Kind e, bool swamp)
        {
            double f = r.NextDouble();
            if (swamp) return f < 0.6 ? p : w;
            return f < 0.35 ? w : f < 0.65 ? y : f < 0.85 ? p : e;
        }

        // ---------------------------------------------------------------- desenho
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // foco: onde o centro da tela encosta no chão (câmera isométrica olha para o herói)
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            Vector3 focus = cam.transform.position;
            if (Mathf.Abs(ray.direction.y) > 0.05f) focus = ray.origin + ray.direction * (-(ray.origin.y) / ray.direction.y);
            int r = Mathf.CeilToInt(DrawRadius / Block);
            int fx = Mathf.FloorToInt(focus.x / Block), fz = Mathf.FloorToInt(focus.z / Block);
            var rp = new RenderParams { layer = gameObject.layer, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            foreach (var k in _kinds)
            {
                rp.material = k.Mat;
                for (int bx = fx - r; bx <= fx + r; bx++)
                    for (int bz = fz - r; bz <= fz + r; bz++)
                    {
                        float dx = (bx + 0.5f) * Block - focus.x, dz = (bz + 0.5f) * Block - focus.z;
                        if (dx * dx + dz * dz > (DrawRadius + Block) * (DrawRadius + Block)) continue;
                        if (!k.Blocks.TryGetValue(Key(bx, bz), out var list)) continue;
                        for (int s = 0; s < list.Count; s += Buf.Length)
                        {
                            int n = Mathf.Min(Buf.Length, list.Count - s);
                            list.CopyTo(s, Buf, 0, n);
                            Graphics.RenderMeshInstanced(rp, k.Mesh, 0, Buf, n);
                        }
                    }
            }
        }

        private void OnDestroy()
        {
            foreach (var k in _kinds) { Destroy(k.Mat); Destroy(k.Mesh); }
        }
    }
}
