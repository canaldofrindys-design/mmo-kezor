using UnityEngine;
using Kezor.Core.World;

namespace Kezor.Client
{
    /// <summary>
    /// Monta o mapa atual de forma procedural e determinística (mesma seed = mesmo mapa em todos os clientes):
    /// Vale de Brasalume (vila murada, estradas, florestas), Pântano de Turfa Negra (charco, poças, juncos) e
    /// Cripta de Vidro Negro (salões e corredores com paredes e cristais). Colisão e posições vêm do MapDefinition.
    /// </summary>
    public static partial class WorldBuilder
    {
        public static float TownWallRadius => _map.HasWall ? _map.WallRadius : 45f;
        /// <summary>Árvores e pedras do bosque (modelos v2 já em tamanho real): 3–5× a altura do herói.</summary>
        public const float TreeScale = 1.25f;
        private static MapDefinition _map = MapDefinition.Get(MapDefinition.ValeId);
        public static MapDefinition Current => _map;

        public static float Height(float x, float z)
        {
            if (_map.Id == MapDefinition.SwampId) return SwampHeight(x, z);
            if (_map.Id == MapDefinition.CryptId) return 0f;
            if (_map.Id == MapDefinition.EscarpId) return FrostHeight(x, z);
            float h = (Mathf.PerlinNoise(x * 0.045f + 11.3f, z * 0.045f + 7.1f) - 0.5f) * 3.2f
                    + (Mathf.PerlinNoise(x * 0.16f + 3.7f, z * 0.16f + 1.9f) - 0.5f) * 0.7f;
            float dTown = Vector2.Distance(new Vector2(x, z), new Vector2(_map.SafeX, _map.SafeZ));
            h *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TownWallRadius + 1f, TownWallRadius + 9f, dTown));
            if (IsRoad(x, z)) h *= 0.35f;
            float edge = Mathf.Min(Mathf.Min(x, z), Mathf.Min(_map.Size - x, _map.Size - z));
            if (edge < 10f) h += Mathf.Pow((10f - edge) / 10f, 1.6f) * (9f + Mathf.PerlinNoise(x * 0.2f, z * 0.2f) * 6f);
            return h;
        }

        public static bool IsRoad(float x, float z)
        {
            if (_map.Id != MapDefinition.ValeId) return false;
            float c = _map.SafeX;
            float d = Vector2.Distance(new Vector2(x, z), new Vector2(_map.SafeX, _map.SafeZ));
            if (d < TownWallRadius + 1f)
            {   // dentro da vila: quatro avenidas, duas ruas circulares, a praça e as pracinhas dos bairros
                if (Mathf.Abs(x - c) < MapDefinition.AvenueHalfWidth || Mathf.Abs(z - _map.SafeZ) < MapDefinition.AvenueHalfWidth) return true;
                if (Mathf.Abs(d - MapDefinition.RingRoadRadius) < MapDefinition.RingHalfWidth || Mathf.Abs(d - MapDefinition.OuterRingRadius) < MapDefinition.RingHalfWidth) return true;
                if (d < MapDefinition.PlazaRadius) return true;
                foreach (var sq in _map.Squares) if ((x - sq.X) * (x - sq.X) + (z - sq.Z) * (z - sq.Z) < sq.R * sq.R) return true;
                return false;
            }
            float wob = Mathf.Sin(z * 0.11f) * 1.5f, wob2 = Mathf.Sin(x * 0.09f) * 1.5f;
            return Mathf.Abs(x - c - wob) < 2.2f || Mathf.Abs(z - _map.SafeZ - wob2) < 2.2f;
        }

        /// <summary>Prefab do Blender (Resources/Models/Environment) com malhas legíveis, ou null → usa o procedural.</summary>
        private static GameObject? Env(string name)
        {
            var pf = ModelLibrary.Prefab("Environment/" + name);
            if (pf == null) return null;
            if (!ModelLibrary.IsReadable(pf)) { Debug.LogWarning($"[Kezor] {name}.fbx sem Read/Write; usando o procedural."); return null; }
            return pf;
        }

        private static Transform? FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        public static GameObject Build() => Build(MapDefinition.Get(MapDefinition.ValeId));

        public static GameObject Build(MapDefinition map)
        {
            _map = map;
            LocalLights.Clear();
            var root = new GameObject("Mundo — " + map.Name);
            switch (map.Id)
            {
                case MapDefinition.SwampId: BuildSwamp(root.transform); break;
                case MapDefinition.CryptId: BuildCrypt(root.transform); break;
                case MapDefinition.EscarpId: BuildEscarpment(root.transform); break;
                default:
                    BuildTerrain(root.transform);
                    BuildTown(root.transform);
                    BuildNature(root.transform);
                    BuildVaultChest(root.transform);
                    break;
            }
            BuildPortals(root.transform);
            if (map.Id != MapDefinition.CryptId && map.Id != MapDefinition.EscarpId) GroundScatter.Build(root.transform, map);   // grama, flores, pedrinhas, poças
            AmbientFx.Build(root.transform, map);                                            // vagalumes, poeira
            if (map.Id == MapDefinition.ValeId) TownAmbience.Build(root.transform, map);    // fonte, forja, mercado, grilos, coruja
            return root;
        }

        // Telhados variados: o vermelho original do Blender (K_Telhado) é trocado por uma destas cores, casa a casa.
        private static readonly Color32[] RoofColors =
        {
            new Color32(122, 46, 37, 255),  // vermelho-tijolo (original)
            new Color32(52, 66, 96, 255),   // ardósia azul
            new Color32(66, 86, 52, 255),   // musgo
            new Color32(92, 62, 42, 255),   // telha queimada
        };

        /// <summary>Vermelho de telhado: R domina G e B e não é emissivo (alfa ≥ 0,5).</summary>
        private static bool IsRoofColor(Color32 c) => c.a >= 128 && c.r > 70 && c.g < c.r * 0.55f && c.b < c.r * 0.55f;

        /// <summary>
        /// Vila de Brasalume v2 (modelos do Blender, kezor_town2.py): muralha em blocos, torres redondas e arcos nos quatro
        /// portões, fonte na praça, casas e sobrados em dois anéis, distritos (mercado, forja, templo, estábulo) e postes
        /// ao longo das ruas. Tudo vem do MapDefinition (o servidor usa os mesmos dados para a colisão).
        /// </summary>
        private static void BuildTown(Transform parent)
        {
            float cx = _map.SafeX, cz = _map.SafeZ;
            // 8 fatias (setores de 45°) em malhas separadas: a câmera só desenha as que vê (culling por fatia)
            var slices = new StaticBatch[8];
            for (int q = 0; q < 8; q++) slices[q] = new StaticBatch();
            StaticBatch Slice(Vector3 p) { float ang = Mathf.Atan2(p.z - cz, p.x - cx) * Mathf.Rad2Deg; return slices[Mathf.Clamp((int)(Mathf.Repeat(ang, 360f) / 45f), 0, 7)]; }
            var wallPf = Env("Muralha2"); var ivyPf = Env("Muralha2Hera") ?? wallPf; var towerPf = Env("Torre2"); var archPf = Env("PortaoArco");
            float R = TownWallRadius;
            // muralha: segmentos de 4 m (lado de fora = -Z local do modelo), deixando o vão de cada portão para o arco e as torres
            int n = Mathf.RoundToInt(2f * Mathf.PI * R / 4f);
            float len = 2f * Mathf.PI * R / n;
            float skip = _map.TowerAngle + Mathf.Rad2Deg * (_map.TowerRadius + 0.6f) / R;
            if (wallPf != null)
                for (int i = 0; i < n; i++)
                {
                    float a = (i + 0.5f) / n * 360f;
                    float rel = Mathf.Repeat(a + 45f, 90f) - 45f;
                    if (Mathf.Abs(rel) < skip) continue;
                    float rad = a * Mathf.Deg2Rad;
                    var p = new Vector3(cx + Mathf.Cos(rad) * R, 0, cz + Mathf.Sin(rad) * R);
                    Slice(p).Add(i % 3 == 1 ? ivyPf! : wallPf, p, Quaternion.Euler(0, 270f - a, 0), new Vector3(len / 4f * 1.03f, 1f, 1f), Color.white);
                }
            for (int g = 0; g < 4; g++)
            {
                float ga = g * 90f;
                var gp = new Vector3(cx + Mathf.Cos(ga * Mathf.Deg2Rad) * R, 0, cz + Mathf.Sin(ga * Mathf.Deg2Rad) * R);
                if (archPf != null) Slice(gp).Add(archPf, gp, Quaternion.Euler(0, 90f - ga, 0), Vector3.one, Color.white);
                for (int s2 = -1; s2 <= 1; s2 += 2)
                {
                    float deg = ga + s2 * _map.TowerAngle, a = deg * Mathf.Deg2Rad;
                    var p = new Vector3(cx + Mathf.Cos(a) * R, 0, cz + Mathf.Sin(a) * R);
                    if (towerPf != null) Slice(p).Add(towerPf, p, Quaternion.Euler(0, 90f - deg, 0), Vector3.one * 1.1f, Color.white);
                }
            }
            // casas
            foreach (var h in _map.Houses)
            {
                var pf = Env(string.IsNullOrEmpty(h.Kind) ? "Casa2A" : h.Kind) ?? Env("CasaEnxaimel");
                if (pf == null) continue;
                var hp = new Vector3(h.X, 0, h.Z); var hr = Quaternion.Euler(0, -h.Angle - 90f, 0);
                Slice(hp).Add(pf, Matrix4x4.TRS(hp, hr, Vector3.one * h.Scale), Color.white);
                // vida: lampião aceso na porta de cada casa e fumaça em metade das chaminés
                bool tall = h.Kind is "Sobrado2" or "Casa3B" or "Taverna3" or "Casarao3";
                bool big = h.Kind is "Taverna3" or "Casarao3";
                float front = big ? 3.4f : h.Kind is "Casa3A" or "Casa3C" ? 3.1f : 2.8f;
                LocalLights.Add(hp + hr * (new Vector3(1.6f, 1.9f, front) * h.Scale), new Color(1f, 0.64f, 0.32f), big ? 8f : 5.5f);
                int hash = Mathf.Abs(Mathf.RoundToInt(h.X * 7.3f + h.Z * 3.1f));
                if (big || hash % 2 == 0)
                    AmbientFx.Smoke(parent, hp + hr * (new Vector3(1.5f, tall ? 8.6f : 6.2f, -0.9f) * h.Scale), big ? 3f : 2.2f);
            }
            // fonte da praça (gema gira)
            var fonte = ModelLibrary.Instantiate("Environment/Fonte2", "Fonte de Brasas", parent) ?? ModelLibrary.Instantiate("Environment/FonteDeBrasas", "Fonte de Brasas", parent);
            if (fonte != null)
            {
                fonte.transform.localPosition = new Vector3(cx, 0, cz);
                var gem = FindDeep(fonte.transform, "Gem");
                if (gem != null) gem.gameObject.AddComponent<Spinner>();
            }
            LocalLights.Add(new Vector3(cx, 4.2f, cz), new Color(1f, 0.6f, 0.25f), 11f);
            // peças dos distritos, postes e braseiros
            var lamps = new MeshBuilder();
            foreach (var pr in _map.Props)
            {
                var pf = Env(pr.Kind);
                if (pf == null) continue;
                var rot = Quaternion.Euler(0, pr.Yaw, 0);
                var pos = new Vector3(pr.X, 0, pr.Z);
                Slice(pos).Add(pf, Matrix4x4.TRS(pos, rot, Vector3.one * pr.Scale), Color.white);
                switch (pr.Kind)
                {
                    case "Poste2": LocalLights.Add(pos + rot * new Vector3(0, 3.3f, 0.78f), new Color(1f, 0.62f, 0.3f), 7.5f); break;
                    case "Braseiro2": LocalLights.Add(pos + Vector3.up * 2f, new Color(1f, 0.55f, 0.22f), 8f); AmbientFx.Embers(parent, pos + Vector3.up * 1.6f, 0.25f, 6f); break;
                    case "Forja2": LocalLights.Add(pos + rot * new Vector3(1.3f, 1.0f, 0.2f), new Color(1f, 0.45f, 0.15f), 7f); AmbientFx.Embers(parent, pos + rot * new Vector3(1.3f, 1.2f, 0.2f), 0.3f, 9f); break;
                    case "Templo": LocalLights.Add(pos + rot * new Vector3(0, 2.0f, 3.4f), new Color(1f, 0.55f, 0.25f), 8f); break;
                }
            }
            for (int q = 0; q < 8; q++) slices[q].Build($"Vila de Brasalume {q}", parent, occluder: true);
        }

        private static void BuildNature(Transform parent)
        {
            var rnd = new System.Random(4242);
            int nc = Mathf.CeilToInt(_map.Size / 40f);
            var chunks = new MeshBuilder[nc, nc];
            var batches = new StaticBatch[nc, nc];
            for (int i = 0; i < nc; i++) for (int j = 0; j < nc; j++) { chunks[i, j] = new MeshBuilder(); batches[i, j] = new StaticBatch(); }
            var rockPfs = new[] { Env("Pedra2A") ?? Env("Pedra"), Env("Pedra2B") ?? Env("Pedra") };
            var pinePfs = new[] { Env("Pinheiro2A") ?? Env("Pinheiro"), Env("Pinheiro2B") ?? Env("Pinheiro") };
            var leafPfs = new[] { Env("Arvore2A") ?? Env("ArvoreCopa"), Env("Arvore2B") ?? Env("ArvoreCopa") };
            var deadPf = Env("ArvoreMorta");
            var trunk = KezorLook.Hex("#4a3627");
            int count = Mathf.RoundToInt(700f * (_map.Size * _map.Size) / (160f * 160f));
            for (int k = 0; k < count; k++)
            {
                float x = (float)rnd.NextDouble() * _map.Size, z = (float)rnd.NextDouble() * _map.Size;
                float dTown = Vector2.Distance(new Vector2(x, z), new Vector2(_map.SafeX, _map.SafeZ));
                if (dTown < TownWallRadius + 5f || IsRoad(x, z) || IsRoad(x + 1.5f, z + 1.5f) || IsRoad(x - 1.5f, z - 1.5f) || NearPortal(x, z, 6f)) continue;
                bool nearSpawn = false;
                foreach (var g in _map.Spawns) if (Vector2.Distance(new Vector2(x, z), new Vector2(g.X, g.Z)) < g.Radius * 0.6f) { nearSpawn = true; break; }
                if (nearSpawn && rnd.NextDouble() < 0.8) continue;
                int ci = Mathf.Clamp((int)(x / 40), 0, nc - 1), cj = Mathf.Clamp((int)(z / 40), 0, nc - 1);
                var rockPf = rockPfs[k & 1]; var pinePf = pinePfs[k & 1]; var leafPf = leafPfs[(k >> 1) & 1];
                var b = chunks[ci, cj];
                var sb = batches[ci, cj];
                float y = Height(x, z);
                var p = new Vector3(x, y - 0.1f, z);
                float s = (0.7f + (float)rnd.NextDouble() * 0.8f) * TreeScale;
                double kind = rnd.NextDouble();
                float far = Mathf.InverseLerp(85f, 150f, dTown);
                var rot = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                b.SetTRS(p, rot, Vector3.one * s);
                var cool = Color.Lerp(Color.white, new Color(0.66f, 0.7f, 0.8f), far);
                if (kind < 0.12 && rockPf != null) sb.Add(rockPf, p, rot, Vector3.one * (s * 0.8f), Color.white);
                else if (kind >= 0.12 && kind < 0.55 + far * 0.25 && pinePf != null) sb.Add(pinePf, p, rot, Vector3.one * s, cool);
                else if (!(kind < 0.55 + far * 0.25) && far > 0.6 && kind > 0.9 && deadPf != null) { rnd.NextDouble(); sb.Add(deadPf, p, rot, Vector3.one * s, Color.white); }
                else if (!(kind < 0.55 + far * 0.25) && !(far > 0.6 && kind > 0.9) && leafPf != null) sb.Add(leafPf, p, rot, Vector3.one * s, cool);
                else if (kind < 0.12)
                {   // pedra
                    b.Ico(new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.6f, 0.8f), KezorLook.Hex("#6e6a64"), 0.25f, k, KezorLook.Hex("#5c5853"));
                }
                else if (kind < 0.55 + far * 0.25)
                {   // pinheiro
                    var g1 = Color.Lerp(KezorLook.Hex("#2f5a3a"), KezorLook.Hex("#1f3d2c"), far);
                    var g2 = g1 * 1.15f; g2.a = 1;
                    b.Cylinder(Vector3.zero, 0.22f, 0.16f, 1.0f, 5, trunk);
                    b.Cone(new Vector3(0, 0.8f, 0), 1.5f, 2.0f, 6, g1);
                    b.Cone(new Vector3(0, 1.9f, 0), 1.15f, 1.8f, 6, g2);
                    b.Cone(new Vector3(0, 2.9f, 0), 0.75f, 1.5f, 6, g1);
                }
                else if (far > 0.6 && kind > 0.9)
                {   // árvore morta
                    b.Cylinder(Vector3.zero, 0.25f, 0.1f, 3.2f, 5, KezorLook.Hex("#3a312a"));
                    b.SetTRS(p + Vector3.up * 2f * s, Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 50f), Vector3.one * s);
                    b.Cylinder(Vector3.zero, 0.1f, 0.03f, 1.3f, 4, KezorLook.Hex("#3a312a"));
                }
                else
                {   // árvore de copa
                    var leaf = Color.Lerp(KezorLook.Hex("#5b7d34"), KezorLook.Hex("#3c5a2e"), far);
                    b.Cylinder(Vector3.zero, 0.25f, 0.18f, 1.6f, 5, trunk);
                    b.Ico(new Vector3(0, 2.3f, 0), new Vector3(1.4f, 1.2f, 1.4f), leaf, 0.2f, k, leaf * 0.85f);
                }
            }
            for (int i = 0; i < nc; i++) for (int j = 0; j < nc; j++)
                {
                    if (chunks[i, j].VertexCount > 0) KezorLook.MakeObject($"Natureza {i},{j}", chunks[i, j].ToMesh("nature"), parent, occluder: true);
                    batches[i, j].Build($"Natureza (Blender) {i},{j}", parent, occluder: true);
                }
        }
    }
}
