using System.Collections.Generic;
using UnityEngine;
using Kezor.Core.World;

namespace Kezor.Client
{
    /// <summary>Pântano de Turfa Negra, Cripta de Vidro Negro, portais e o baú da praça.</summary>
    public static partial class WorldBuilder
    {
        // ---------------------------------------------------------------- comum
        private static bool NearPortal(float x, float z, float r)
        {
            foreach (var p in _map.Portals) if (Vector2.Distance(new Vector2(x, z), new Vector2(p.X, p.Z)) < r) return true;
            return false;
        }

        /// <summary>Cor do vórtice pelo destino do portal.</summary>
        public static Color PortalColor(int targetMap) => targetMap switch
        {
            MapDefinition.SwampId => KezorLook.Hex("#7affc8"),
            MapDefinition.CryptId => KezorLook.Hex("#a77bff"),
            MapDefinition.EscarpId => KezorLook.Hex("#ff7080"),
            _ => KezorLook.Hex("#ffd36a"),
        };

        private static void BuildPortals(Transform parent)
        {
            foreach (var p in _map.Portals)
            {
                var pos = new Vector3(p.X, Height(p.X, p.Z), p.Z);
                var go = ModelLibrary.Instantiate("Environment/PortalArco", "Portal → " + p.Label, parent);
                Color col = PortalColor(p.TargetMap);
                if (go == null)
                {   // procedural: anel brilhante
                    var b = new MeshBuilder();
                    b.Ring(Vector3.zero, 1.0f, 1.3f, 16, KezorLook.Glow(col));
                    go = KezorLook.MakeObject("Portal → " + p.Label, b.ToMesh("portal"), parent);
                    go.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    go.transform.localPosition = pos + Vector3.up * 1.3f;
                    continue;
                }
                go.transform.localPosition = pos;
                go.transform.localScale = Vector3.one * 1.8f;               // arco de ~5 m: maior que o herói
                LocalLights.Add(pos + Vector3.up * 2.5f, col, 9f);
                // de frente para o centro do mapa (ou para a vila)
                var center = _map.SafeRadius > 0 ? new Vector3(_map.SafeX, 0, _map.SafeZ) : new Vector3(_map.RespawnX, 0, _map.RespawnZ);
                var dir = center - pos; dir.y = 0;
                if (dir.sqrMagnitude > 0.01f) go.transform.localRotation = Quaternion.LookRotation(-dir.normalized);
                var vortex = FindDeep(go.transform, "Vortex");
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(KezorLook.TintId, col);
                if (vortex != null)
                {   // só o vórtice ganha a cor do destino (as runas do arco são brancas emissivas)
                    foreach (var r in vortex.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(mpb);
                    vortex.gameObject.AddComponent<PortalSwirl>();
                }
            }
        }

        private static void BuildVaultChest(Transform parent)
        {
            var chest = ModelLibrary.Instantiate("Environment/BauGuardia", "Baú da cidade", parent);
            var pos = new Vector3(MapDefinition.VaultChestX, 0, MapDefinition.VaultChestZ);
            if (chest == null)
            {
                var b = new MeshBuilder();
                b.Box(new Vector3(0, 0.3f, 0), new Vector3(1f, 0.6f, 0.62f), KezorLook.Hex("#5a3a22"));
                b.Box(new Vector3(0, 0.66f, 0), new Vector3(1.02f, 0.14f, 0.64f), KezorLook.Hex("#3e4148"));
                chest = KezorLook.MakeObject("Baú da cidade", b.ToMesh("chest"), parent);
            }
            chest.transform.localPosition = pos;
            chest.transform.localScale = Vector3.one * 1.3f;
            var dir = new Vector3(_map.SafeX, 0, _map.SafeZ) - pos;
            chest.transform.localRotation = Quaternion.LookRotation(dir.normalized);
        }

        // ---------------------------------------------------------------- Pântano de Turfa Negra
        private static float SwampHeight(float x, float z)
        {
            float h = (Mathf.PerlinNoise(x * 0.06f + 4.1f, z * 0.06f + 9.3f) - 0.5f) * 1.2f
                    + (Mathf.PerlinNoise(x * 0.2f + 1.3f, z * 0.2f + 2.9f) - 0.5f) * 0.35f;
            foreach (var o in _map.Obstacles)
            {
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(o.X, o.Z));
                if (d < o.R + 1.5f) h = Mathf.Lerp(h, -0.9f, Mathf.SmoothStep(0, 1, 1f - (d - o.R * 0.6f) / (o.R * 0.4f + 1.5f)));
            }
            float edge = Mathf.Min(Mathf.Min(x, z), Mathf.Min(_map.Size - x, _map.Size - z));
            if (edge < 10f) h += Mathf.Pow((10f - edge) / 10f, 1.6f) * (6f + Mathf.PerlinNoise(x * 0.2f, z * 0.2f) * 5f);
            return h;
        }

        private static Color SwampColor(float x, float z, float h, float rnd)
        {
            if (h > 2.5f) return Color.Lerp(KezorLook.Hex("#3d3a34"), KezorLook.Hex("#55524a"), Mathf.InverseLerp(2.5f, 10f, h) + rnd * 0.1f);
            if (h < -0.4f) return Color.Lerp(KezorLook.Hex("#2a2418"), KezorLook.Hex("#33291c"), rnd);       // margem de turfa
            var a = Color.Lerp(KezorLook.Hex("#3b4426"), KezorLook.Hex("#4a4f2c"), rnd);
            var b = Color.Lerp(KezorLook.Hex("#3a3324"), KezorLook.Hex("#463c28"), rnd);
            return Color.Lerp(a, b, Mathf.PerlinNoise(x * 0.08f, z * 0.08f));
        }

        private static void BuildSwamp(Transform parent)
        {
            BuildTerrain(parent);
            // poças fundas: espelho escuro um pouco abaixo do chão (o servidor bloqueia o passo nelas)
            var water = new MeshBuilder();
            foreach (var o in _map.Obstacles)
            {
                water.Cylinder(new Vector3(o.X, -0.55f, o.Z), o.R + 0.6f, o.R + 0.6f, 0.05f, 12, KezorLook.Hex("#16201a"));
                water.Ring(new Vector3(o.X, -0.48f, o.Z), o.R * 0.35f, o.R * 0.42f, 12, KezorLook.Glow(KezorLook.Hex("#2f6a4a"), 0.3f));
            }
            KezorLook.MakeObject("Poças de turfa", water.ToMesh("pools"), parent);

            var rnd = new System.Random(7070);
            var batch = new StaticBatch();
            var dead = Env("ArvoreMorta"); var stump = Env("TocoPantano"); var reed = Env("Junco"); var mush = Env("CogumeloFatuo"); var rock = Env("Pedra");
            var proc = new MeshBuilder();
            for (int k = 0; k < 700; k++)
            {
                float x = (float)rnd.NextDouble() * _map.Size, z = (float)rnd.NextDouble() * _map.Size;
                double kind = rnd.NextDouble();
                float s = (0.7f + (float)rnd.NextDouble() * 0.7f) * 1.5f;
                var rot = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                if (NearPortal(x, z, 5f) || Vector2.Distance(new Vector2(x, z), new Vector2(_map.RespawnX, _map.RespawnZ)) < 6f) continue;
                bool nearPool = false, inPool = false;
                foreach (var o in _map.Obstacles)
                {
                    float d = Vector2.Distance(new Vector2(x, z), new Vector2(o.X, o.Z));
                    if (d < o.R + 0.3f) inPool = true;
                    else if (d < o.R + 2.5f) nearPool = true;
                }
                if (inPool) continue;
                bool nearSpawn = false;
                foreach (var g in _map.Spawns) if (Vector2.Distance(new Vector2(x, z), new Vector2(g.X, g.Z)) < g.Radius * 0.6f) { nearSpawn = true; break; }
                if (nearSpawn && rnd.NextDouble() < 0.75) continue;
                var p = new Vector3(x, Height(x, z) - 0.05f, z);
                var tint = Color.Lerp(Color.white, new Color(0.75f, 0.85f, 0.7f), (float)rnd.NextDouble() * 0.5f);
                if (nearPool && reed != null) batch.Add(reed, p, rot, Vector3.one * s, tint);
                else if (kind < 0.32 && dead != null) batch.Add(dead, p, rot, Vector3.one * (s * 1.2f), new Color(0.7f, 0.72f, 0.66f));
                else if (kind < 0.5 && stump != null) batch.Add(stump, p, rot, Vector3.one * s, Color.white);
                else if (kind < 0.62 && mush != null) batch.Add(mush, p, rot, Vector3.one * s, Color.white);
                else if (kind < 0.72 && rock != null) batch.Add(rock, p, rot, Vector3.one * (s * 0.7f), new Color(0.6f, 0.62f, 0.55f));
                else if (kind < 0.9 && reed != null) batch.Add(reed, p, rot, Vector3.one * (s * 0.8f), tint);
                else if (dead == null)
                {
                    proc.SetTRS(p, rot, Vector3.one * s);
                    proc.Cylinder(Vector3.zero, 0.25f, 0.1f, 3f, 5, KezorLook.Hex("#3a312a"));
                }
            }
            batch.Build("Pântano (Blender)", parent, occluder: true);
            if (proc.VertexCount > 0) KezorLook.MakeObject("Pântano", proc.ToMesh("swamp"), parent, occluder: true);
        }

        // ---------------------------------------------------------------- Cripta de Vidro Negro
        private static bool CryptFloor(float x, float z)
        {
            foreach (var f in _map.Floors) if (f.Contains(x, z, 0f)) return true;
            return false;
        }

        private static void BuildCrypt(Transform parent)
        {
            // chão: lajes de 2 m (shader de terreno com textura de calçamento/cascalho), rejunte escuro por baixo
            var floor = new MeshBuilder();
            var seam = KezorLook.Hex("#1e1c24");
            var fv = new List<Vector3>(); var fc = new List<Color>(); var fu1 = new List<Vector2>(); var fu2 = new List<Vector2>(); var ft = new List<int>();
            var frnd = new System.Random(808);
            for (int x = 0; x < (int)_map.Size; x += 2)
                for (int z = 0; z < (int)_map.Size; z += 2)
                {
                    if (!CryptFloor(x + 1, z + 1)) continue;
                    var col = Color.Lerp(KezorLook.Hex("#3a3744"), KezorLook.Hex("#2b2932"), (float)frnd.NextDouble());
                    float g = (float)frnd.NextDouble() * 0.35f;                           // lajes mais gastas (cascalho)
                    float y = -0.02f + (float)frnd.NextDouble() * 0.03f;
                    int b0 = fv.Count;
                    // laje com chanfro: topo + 4 bordas inclinadas
                    float o = 0.97f, i0 = 0.9f;
                    var top = new[] { new Vector3(x + 1 - i0, y, z + 1 - i0), new Vector3(x + 1 - i0, y, z + 1 + i0), new Vector3(x + 1 + i0, y, z + 1 + i0), new Vector3(x + 1 + i0, y, z + 1 - i0) };
                    var bot = new[] { new Vector3(x + 1 - o, y - 0.07f, z + 1 - o), new Vector3(x + 1 - o, y - 0.07f, z + 1 + o), new Vector3(x + 1 + o, y - 0.07f, z + 1 + o), new Vector3(x + 1 + o, y - 0.07f, z + 1 - o) };
                    fv.AddRange(top); fv.AddRange(bot);
                    for (int k = 0; k < 8; k++) { fc.Add(k < 4 ? col : col * 0.8f); fu1.Add(Vector2.zero); fu2.Add(new Vector2(1f - g, g)); }
                    ft.AddRange(new[] { b0, b0 + 1, b0 + 2, b0, b0 + 2, b0 + 3 });
                    for (int k = 0; k < 4; k++)
                    {
                        int a0 = b0 + k, a1 = b0 + (k + 1) % 4, c0 = b0 + 4 + k, c1 = b0 + 4 + (k + 1) % 4;
                        ft.AddRange(new[] { c0, c1, a1, c0, a1, a0 });
                    }
                    floor.Box(new Vector3(x + 1, -0.12f, z + 1), new Vector3(2f, 0.08f, 2f), seam);
                }
            if (fv.Count > 0)
            {
                var fm = new Mesh { name = "crypt slabs", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                fm.SetVertices(fv); fm.SetColors(fc); fm.SetUVs(1, fu1); fm.SetUVs(2, fu2); fm.SetTriangles(ft, 0);
                fm.RecalculateNormals(); fm.RecalculateBounds();
                var fgo = KezorLook.MakeObject("Cripta — lajes", fm, parent);
                fgo.GetComponent<MeshRenderer>().sharedMaterial = TerrainMaterial;
                MakeGround(fgo, fm);
            }
            // tapete rúnico até o trono
            floor.Box(new Vector3(103, 0.01f, 60), new Vector3(20, 0.02f, 2.4f), KezorLook.Hex("#3a1f4a"));
            floor.Box(new Vector3(103, 0.02f, 60), new Vector3(20, 0.02f, 0.2f), KezorLook.Glow(KezorLook.Hex("#a77bff"), 0.6f));
            KezorLook.MakeObject("Cripta — chão", floor.ToMesh("crypt floor"), parent);

            // paredes: blocos de 2 m nas bordas dos retângulos onde o lado de fora é parede (não há outro chão)
            var walls = new StaticBatch();
            var wallPf = Env("ParedeCripta");
            var proc = new MeshBuilder();
            var wallCol = KezorLook.Hex("#3a3842");
            foreach (var f in _map.Floors)
            {
                void Edge(float ax, float az, float bx, float bz, float nx, float nz, float yaw)
                {
                    float len = Vector2.Distance(new Vector2(ax, az), new Vector2(bx, bz));
                    int n = Mathf.Max(1, Mathf.RoundToInt(len / 2f));
                    for (int i = 0; i < n; i++)
                    {
                        float t = (i + 0.5f) / n;
                        float x = Mathf.Lerp(ax, bx, t), z = Mathf.Lerp(az, bz, t);
                        if (CryptFloor(x + nx * 0.6f, z + nz * 0.6f)) continue;     // abertura para outro salão/corredor
                        var p = new Vector3(x + nx * 0.4f, 0, z + nz * 0.4f);
                        var rot = Quaternion.Euler(0, yaw, 0);
                        if (wallPf != null) walls.Add(wallPf, p, rot, new Vector3(len / n / 2f * 1.02f, 1.7f, 1f), Color.white);
                        else { proc.SetTRS(p, rot, Vector3.one); proc.Box(new Vector3(0, 1.6f, 0), new Vector3(len / n, 3.2f, 0.8f), wallCol); }
                    }
                }
                Edge(f.X0, f.Z0, f.X1, f.Z0, 0, -1, 0);      // sul
                Edge(f.X0, f.Z1, f.X1, f.Z1, 0, 1, 180);     // norte
                Edge(f.X0, f.Z0, f.X0, f.Z1, -1, 0, 90);     // oeste
                Edge(f.X1, f.Z0, f.X1, f.Z1, 1, 0, -90);     // leste
            }
            // pilares (mesmos obstáculos do servidor) e cristais junto das paredes
            var pillar = Env("PilarCripta"); var crystal = Env("CristalNegro");
            foreach (var o in _map.Obstacles)
                if (pillar != null) walls.Add(pillar, new Vector3(o.X, 0, o.Z), Quaternion.identity, new Vector3(1f, 1.6f, 1f), Color.white);
            var rnd = new System.Random(5151);
            foreach (var f in _map.Floors)
            {
                float w = f.X1 - f.X0, d = f.Z1 - f.Z0;
                if (w < 12 || d < 12) continue;                 // só nos salões
                for (int i = 0; i < 6; i++)
                {
                    bool side = rnd.NextDouble() < 0.5;
                    float x = side ? (rnd.NextDouble() < 0.5 ? f.X0 + 1.2f : f.X1 - 1.2f) : Mathf.Lerp(f.X0 + 2, f.X1 - 2, (float)rnd.NextDouble());
                    float z = side ? Mathf.Lerp(f.Z0 + 2, f.Z1 - 2, (float)rnd.NextDouble()) : (rnd.NextDouble() < 0.5 ? f.Z0 + 1.2f : f.Z1 - 1.2f);
                    if (crystal != null) walls.Add(crystal, new Vector3(x, 0, z), Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0), Vector3.one * (1.2f + (float)rnd.NextDouble() * 0.9f), Color.white);
                }
            }
            // trono do soberano
            var throne = new MeshBuilder();
            throne.Box(new Vector3(111.5f, 0.3f, 60), new Vector3(2.6f, 0.6f, 4f), KezorLook.Hex("#2c2838"));
            throne.Box(new Vector3(112.6f, 2.0f, 60), new Vector3(0.6f, 3.4f, 2.6f), KezorLook.Hex("#1c1a26"));
            throne.Gem(new Vector3(112.3f, 3.9f, 60), 0.4f, 0.9f, KezorLook.Glow(KezorLook.Hex("#a77bff")));
            KezorLook.MakeObject("Trono", throne.ToMesh("throne"), parent);
            walls.Build("Cripta (Blender)", parent, occluder: true);
            if (proc.VertexCount > 0) KezorLook.MakeObject("Cripta — paredes", proc.ToMesh("crypt walls"), parent, occluder: true);
        }
    }

    /// <summary>Vórtice do portal: gira em torno do próprio eixo e pulsa.</summary>
    public sealed class PortalSwirl : MonoBehaviour
    {
        private Vector3 _baseScale;
        private void Start() { _baseScale = transform.localScale; }
        private void Update()
        {
            transform.localRotation = Quaternion.Euler(0, 0, Time.time * 70f);
            float s = 1f + Mathf.Sin(Time.time * 2.5f) * 0.05f;
            transform.localScale = _baseScale * s;
        }
    }
}
