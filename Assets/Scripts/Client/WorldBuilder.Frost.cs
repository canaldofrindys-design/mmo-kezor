using UnityEngine;
using Kezor.Core.World;

namespace Kezor.Client
{
    /// <summary>
    /// Escarpa da Geada Rubra (mapa 4): planalto de neve com fendas fundas (obstáculos grandes do servidor), agulhas de
    /// gelo (obstáculos pequenos), rochas e árvores mortas cobertas de geada, e a marca da Fenda no centro.
    /// As posições de colisão vêm do <see cref="MapDefinition.Escarpment"/>; aqui só se desenha.
    /// </summary>
    public static partial class WorldBuilder
    {
        private static float FrostHeight(float x, float z)
        {
            float h = (Mathf.PerlinNoise(x * 0.05f + 21.1f, z * 0.05f + 3.7f) - 0.5f) * 2.0f
                    + (Mathf.PerlinNoise(x * 0.18f + 6.3f, z * 0.18f + 8.9f) - 0.5f) * 0.45f;
            foreach (var o in _map.Obstacles)
            {
                if (o.R < 2f) continue;                                  // agulhas não afundam o chão
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(o.X, o.Z));
                if (d < o.R + 2f) h = Mathf.Lerp(h, -3.2f, Mathf.SmoothStep(0, 1, 1f - (d - o.R * 0.55f) / (o.R * 0.45f + 2f)));
            }
            float edge = Mathf.Min(Mathf.Min(x, z), Mathf.Min(_map.Size - x, _map.Size - z));
            if (edge < 12f) h += Mathf.Pow((12f - edge) / 12f, 1.5f) * (11f + Mathf.PerlinNoise(x * 0.17f, z * 0.17f) * 8f);
            return h;
        }

        private static Color FrostColor(float x, float z, float h, float slope, float rnd, out Vector4 w)
        {
            var snow = Color.Lerp(KezorLook.Hex("#c9d4e0"), KezorLook.Hex("#dfe6ee"), rnd);
            var ice = Color.Lerp(KezorLook.Hex("#8fb0c8"), KezorLook.Hex("#a7c2d6"), rnd);
            var rock = Color.Lerp(KezorLook.Hex("#4a4f58"), KezorLook.Hex("#5d626b"), rnd);
            float rocky = Mathf.Max(Mathf.InverseLerp(4f, 8f, h), Mathf.InverseLerp(0.5f, 0.8f, slope));
            float icy = Mathf.Max(Mathf.InverseLerp(-0.8f, -2.4f, h), Smooth(0.62f, 0.78f, Mathf.PerlinNoise(x * 0.07f + 9f, z * 0.07f + 2f)) * 0.6f);
            // geada rubra: manchas avermelhadas perto da Fenda
            float rift = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, z), new Vector2(MapDefinition.RiftX, MapDefinition.RiftZ)) / 16f);
            var c = Color.Lerp(snow, ice, icy);
            c = Color.Lerp(c, KezorLook.Hex("#b0707a"), rift * 0.45f);
            c = Color.Lerp(c, rock, rocky);
            w = new Vector4(0, 0, 0.25f * (1f - rocky), 0.35f + rocky * 0.65f);
            return c;
        }

        private static void BuildEscarpment(Transform parent)
        {
            BuildTerrain(parent);
            var b = new MeshBuilder();
            var rnd = new System.Random(4040);
            foreach (var o in _map.Obstacles)
            {
                if (o.R >= 2f)
                {   // fenda: fundo escuro com brilho azul lá embaixo
                    b.Cylinder(new Vector3(o.X, -3.0f, o.Z), o.R + 1f, o.R + 1f, 0.05f, 14, KezorLook.Hex("#0c1420"));
                    b.Ring(new Vector3(o.X, -2.9f, o.Z), o.R * 0.3f, o.R * 0.55f, 14, KezorLook.Glow(KezorLook.Hex("#3f8fd0"), 0.35f));
                    continue;
                }
                // agulha de gelo: três pontas de cristal inclinadas
                float y = Height(o.X, o.Z) - 0.1f;
                for (int k = 0; k < 3; k++)
                {
                    float a = k * 2.1f + (float)rnd.NextDouble();
                    var tilt = Quaternion.Euler((float)rnd.NextDouble() * 18f - 9f, a * Mathf.Rad2Deg, (float)rnd.NextDouble() * 18f - 9f);
                    b.SetTRS(new Vector3(o.X + Mathf.Cos(a) * 0.35f, y, o.Z + Mathf.Sin(a) * 0.35f), tilt, Vector3.one);
                    b.Cylinder(Vector3.zero, 0.55f - k * 0.1f, 0, 3.6f + k * 1.1f + (float)rnd.NextDouble(), 5, k == 0 ? KezorLook.Hex("#bfe3f5") : KezorLook.Hex("#9cc7e2"));
                }
                b.ResetMatrix();
            }
            // marca da Fenda: anel de gelo rubro e cristais em volta do centro
            float ry = Height(MapDefinition.RiftX, MapDefinition.RiftZ) + 0.04f;
            b.Ring(new Vector3(MapDefinition.RiftX, ry, MapDefinition.RiftZ), 3.2f, 3.8f, 24, KezorLook.Glow(KezorLook.Hex("#ff4a5a"), 0.5f));
            b.Ring(new Vector3(MapDefinition.RiftX, ry, MapDefinition.RiftZ), 0.8f, 1.2f, 16, KezorLook.Glow(KezorLook.Hex("#ff8a8a"), 0.7f));
            for (int k = 0; k < 7; k++)
            {
                float a = k * Mathf.PI * 2 / 7f;
                float x = MapDefinition.RiftX + Mathf.Cos(a) * 9.5f, z = MapDefinition.RiftZ + Mathf.Sin(a) * 9.5f;
                b.Gem(new Vector3(x, Height(x, z) + 0.9f, z), 0.45f, 1.8f, KezorLook.Glow(KezorLook.Hex("#e0505e"), 0.25f));
            }
            KezorLook.MakeObject("Escarpa (gelo)", b.ToMesh("frost"), parent, occluder: true);

            // rochas e árvores mortas cobertas de geada (modelos do Blender com tom frio)
            var batch = new StaticBatch();
            var dead = Env("ArvoreMorta"); var rockM = Env("Pedra"); var stump = Env("TocoPantano");
            var proc = new MeshBuilder();
            for (int k = 0; k < 520; k++)
            {
                float x = (float)rnd.NextDouble() * _map.Size, z = (float)rnd.NextDouble() * _map.Size;
                if (NearPortal(x, z, 5f) || Vector2.Distance(new Vector2(x, z), new Vector2(_map.RespawnX, _map.RespawnZ)) < 6f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(MapDefinition.RiftX, MapDefinition.RiftZ)) < 13f) continue;
                bool blocked = false;
                foreach (var o in _map.Obstacles) if (Vector2.Distance(new Vector2(x, z), new Vector2(o.X, o.Z)) < o.R + 1.5f) { blocked = true; break; }
                if (blocked) continue;
                bool nearSpawn = false;
                foreach (var g in _map.Spawns) if (Vector2.Distance(new Vector2(x, z), new Vector2(g.X, g.Z)) < g.Radius * 0.6f) { nearSpawn = true; break; }
                if (nearSpawn && rnd.NextDouble() < 0.8) continue;
                var p = new Vector3(x, Height(x, z) - 0.05f, z);
                var rot = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                float s = (0.7f + (float)rnd.NextDouble() * 0.8f) * 1.4f;
                double kind = rnd.NextDouble();
                var frost = Color.Lerp(new Color(0.78f, 0.86f, 0.95f), new Color(0.92f, 0.95f, 1f), (float)rnd.NextDouble());
                if (kind < 0.45 && rockM != null) batch.Add(rockM, p, rot, Vector3.one * (s * 0.9f), frost);
                else if (kind < 0.75 && dead != null) batch.Add(dead, p, rot, Vector3.one * (s * 1.1f), frost);
                else if (kind < 0.85 && stump != null) batch.Add(stump, p, rot, Vector3.one * s, frost);
                else
                {   // monte de neve
                    proc.Ico(p, new Vector3(1.1f, 0.45f, 1.1f) * s, KezorLook.Hex("#d5dee8"), 0.15f, k);
                }
            }
            batch.Build("Escarpa (Blender)", parent, occluder: true);
            if (proc.VertexCount > 0) KezorLook.MakeObject("Escarpa (neve)", proc.ToMesh("snow"), parent, occluder: true);
        }
    }
}
