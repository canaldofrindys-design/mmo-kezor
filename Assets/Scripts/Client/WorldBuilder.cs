using UnityEngine;
using Kezor.Core.World;

namespace Kezor.Client
{
    /// <summary>
    /// Monta o mapa atual de forma procedural e determinística (mesma seed = mesmo mapa em todos os clientes):
    /// Vale de Brasalume (cidade murada v4, estradas, florestas), Pântano de Turfa Negra (charco, poças, juncos) e
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
            float dTown = TownDist(x, z);
            h *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TownWallRadius + 4f, TownWallRadius + 14f, dTown));
            if (IsRoad(x, z)) h *= 0.35f;
            float edge = Mathf.Min(Mathf.Min(x, z), Mathf.Min(_map.Size - x, _map.Size - z));
            if (edge < 10f) h += Mathf.Pow((10f - edge) / 10f, 1.6f) * (9f + Mathf.PerlinNoise(x * 0.2f, z * 0.2f) * 6f);
            return h;
        }

        /// <summary>Distância "quadrada" ao centro da cidade (a muralha v4 é quadrada); nos mapas sem muralha, a distância comum.</summary>
        public static float TownDist(float x, float z) =>
            _map.HasWall ? _map.TownDistance(x, z) : Vector2.Distance(new Vector2(x, z), new Vector2(_map.SafeX, _map.SafeZ));

        public static bool IsRoad(float x, float z)
        {
            if (_map.Id != MapDefinition.ValeId) return false;
            float c = _map.SafeX;
            if (TownDist(x, z) < TownWallRadius + 1f) return _map.IsTownStreet(x, z) || Mathf.Abs(TownDist(x, z) - TownWallRadius) < 1.5f;
            float wob = Mathf.Sin(z * 0.11f) * 1.5f, wob2 = Mathf.Sin(x * 0.09f) * 1.5f;
            return Mathf.Abs(x - c - wob) < 2.6f || Mathf.Abs(z - _map.SafeZ - wob2) < 2.6f;
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

        // Telhados de telha variados, casa a casa: o vermelho original do Blender (Tint 0), telha queimada (1) e terracota escura (2).
        private static readonly Color32[] RoofColors =
        {
            new Color32(142, 62, 43, 255),
            new Color32(104, 64, 44, 255),
            new Color32(120, 44, 36, 255),
        };

        /// <summary>Vermelho de telha: R domina G e B e não é emissivo (alfa ≥ 0,5).</summary>
        private static bool IsRoofColor(Color32 c) => c.a >= 128 && c.r > 60 && c.g < c.r * 0.55f && c.b < c.r * 0.55f;

        /// <summary>Troca o vermelho da telha pela cor da variação, mantendo a sombra assada (proporção do brilho).</summary>
        private static System.Func<Color32, Color32>? RoofRemap(int tint)
        {
            if (tint <= 0 || tint >= RoofColors.Length) return null;
            var src = RoofColors[0]; var dst = RoofColors[tint];
            return c =>
            {
                if (!IsRoofColor(c)) return c;
                float k = c.r / (float)src.r;
                return new Color32((byte)Mathf.Clamp(dst.r * k, 0, 255), (byte)Mathf.Clamp(dst.g * k, 0, 255), (byte)Mathf.Clamp(dst.b * k, 0, 255), c.a);
            };
        }

        /// <summary>Porta de cada prédio (x local) para o lampião aceso; NaN = sem lampião (capela, guilda e estábulo têm braseiros/nenhum).</summary>
        private static float DoorX(string kind) => kind switch
        {
            "Casa4A" => -0.8f, "Casa4B" => 0f, "Casa4C" => 0f, "Casa4D" => 1.2f, "Loja4" => -1.7f, "Taverna4" => 0f, "Ferraria4" => 0.6f,
            _ => float.NaN,
        };

        /// <summary>Chaminés (posição local x, altura, z) de cada modelo, para a fumaça.</summary>
        private static Vector3[] Chimneys(string kind) => kind switch
        {
            "Casa4A" => new[] { new Vector3(2.2f, 11.6f, -1.2f) },
            "Casa4C" => new[] { new Vector3(-2.6f, 11.0f, -1.15f) },
            "Taverna4" => new[] { new Vector3(4.2f, 14.6f, -1.6f) },
            "Ferraria4" => new[] { new Vector3(-2.2f, 11.3f, -1.15f), new Vector3(6.8f, 6.9f, -1.15f) },
            _ => System.Array.Empty<Vector3>(),
        };

        /// <summary>
        /// Cidade de Brasalume v4 (modelos do Blender, Art/scripts/kezor_city4.py): muralha quadrada de cantaria com ameias,
        /// torres redondas nos cantos e no meio dos trechos, portões fortificados nos quatro lados, piso de lajes e Fonte da
        /// Brasa na praça, quarteirões de casas de pedra e enxaimel, taverna, loja, ferraria, capela, salão das guildas,
        /// estábulo, barracas do mercado, postes e braseiros. Tudo vem do MapDefinition (o servidor usa os mesmos dados
        /// para a colisão).
        /// </summary>
        private static void BuildTown(Transform parent)
        {
            float cx = _map.SafeX, cz = _map.SafeZ, W = _map.WallRadius;
            // 8 fatias (setores de 45°) em malhas separadas: a câmera só desenha as que vê (culling por fatia)
            var slices = new StaticBatch[8];
            for (int q = 0; q < 8; q++) slices[q] = new StaticBatch();
            StaticBatch Slice(Vector3 p) { float ang = Mathf.Atan2(p.z - cz, p.x - cx) * Mathf.Rad2Deg; return slices[Mathf.Clamp((int)(Mathf.Repeat(ang, 360f) / 45f), 0, 7)]; }
            var wallPf = Env("Muralha4"); var ivyPf = Env("Muralha4Hera") ?? wallPf; var gatePf = Env("Portao4"); var towerPf = Env("Torre4");

            // ---- muralha: em cada lado, o portão no meio e segmentos de 8 m esticados até o canto (lado de fora = +Z local)
            float gateEnd = MapDefinition.GateTowerOffset + MapDefinition.GateTowerHalfW;
            for (int g = 0; g < 4; g++)
            {
                float nx = g == 0 ? 1 : g == 2 ? -1 : 0, nz = g == 1 ? 1 : g == 3 ? -1 : 0;
                float tx = -nz, tz = nx;
                var rot = Quaternion.Euler(0, MapDefinition.YawTo(nx, nz), 0);
                var gp = new Vector3(cx + nx * W, 0, cz + nz * W);
                if (gatePf != null) Slice(gp).Add(gatePf, Matrix4x4.TRS(gp, rot, Vector3.one), Color.white);
                LocalLights.Add(gp + new Vector3(nx, 0, nz) * 4.8f + Vector3.up * 2.2f, new Color(1f, 0.6f, 0.28f), 9f);   // braseiros de fora do portão
                if (wallPf == null) continue;
                float L = W - gateEnd + 0.5f;
                int n = Mathf.Max(1, Mathf.RoundToInt(L / 8f));
                float len = L / n;
                foreach (var s in new[] { -1f, 1f })
                    for (int i = 0; i < n; i++)
                    {
                        float t = s * (gateEnd - 0.3f + (i + 0.5f) * len);
                        var p = new Vector3(cx + nx * W + tx * t, 0, cz + nz * W + tz * t);
                        var pf = (i + g) % 3 == 1 ? ivyPf! : wallPf;
                        Slice(p).Add(pf, Matrix4x4.TRS(p, rot, new Vector3(len / 8f * 1.02f, 1f, 1f)), Color.white);
                    }
            }
            if (towerPf != null)
                foreach (var tw in _map.Towers)
                {
                    var p = new Vector3(tw.X, 0, tw.Z);
                    Slice(p).Add(towerPf, Matrix4x4.TRS(p, Quaternion.Euler(0, tw.Yaw, 0), Vector3.one * tw.Scale), Color.white);
                }

            // ---- prédios: telhado variado, lampião aceso na porta e fumaça nas chaminés
            foreach (var h in _map.Houses)
            {
                var pf = Env(h.Kind);
                if (pf == null) continue;
                var hp = new Vector3(h.X, 0, h.Z); var hr = Quaternion.Euler(0, h.Yaw, 0);
                Slice(hp).Add(pf, Matrix4x4.TRS(hp, hr, Vector3.one * h.Scale), Color.white, RoofRemap(h.Tint));
                float hz = MapDefinition.HouseFootprint(h.Kind)[0].HZ;
                float dx = DoorX(h.Kind);
                if (!float.IsNaN(dx)) LocalLights.Add(hp + hr * (new Vector3(dx + 1.1f, 2.4f, hz + 0.55f) * h.Scale), new Color(1f, 0.64f, 0.32f), 5.5f);
                foreach (var ch in Chimneys(h.Kind))
                    if (h.Kind != "Casa4C" || Mathf.Abs(Mathf.RoundToInt(h.X * 7.3f + h.Z * 3.1f)) % 2 == 0)
                        AmbientFx.Smoke(parent, hp + hr * (ch * h.Scale), h.Kind == "Taverna4" ? 3f : 2.2f);
                switch (h.Kind)
                {
                    case "Ferraria4":
                    {
                        var forge = hp + hr * (new Vector3(6.8f, 1.3f, -0.9f) * h.Scale);
                        LocalLights.Add(forge, new Color(1f, 0.45f, 0.15f), 8f);
                        AmbientFx.Embers(parent, forge + Vector3.up * 0.2f, 0.3f, 9f);
                        break;
                    }
                    case "Capela4":
                        foreach (var sx in new[] { -2.6f, 2.6f }) LocalLights.Add(hp + hr * (new Vector3(sx, 1.9f, 8.4f) * h.Scale), new Color(1f, 0.55f, 0.22f), 7f);
                        LocalLights.Add(hp + hr * (new Vector3(0, 6f, 7.4f) * h.Scale), new Color(1f, 0.55f, 0.3f), 7f);   // rosácea
                        break;
                    case "Guilda4":
                        foreach (var sx in new[] { -4.2f, 4.2f }) LocalLights.Add(hp + hr * (new Vector3(sx, 1.9f, 6.9f) * h.Scale), new Color(1f, 0.55f, 0.22f), 7.5f);
                        break;
                    case "Taverna4":
                        LocalLights.Add(hp + hr * (new Vector3(0f, 2.3f, 5.6f) * h.Scale), new Color(1f, 0.6f, 0.3f), 8f);   // varanda
                        break;
                }
            }

            // ---- praça: piso de lajes (malha própria) e a Fonte da Brasa com a gema girando
            var pisoPf = Env("Praca4");
            if (pisoPf != null)
            {
                var piso = new StaticBatch();
                piso.Add(pisoPf, Matrix4x4.TRS(new Vector3(cx, 0.01f, cz), Quaternion.identity, Vector3.one), Color.white);
                piso.Build("Praça da Fonte (piso)", parent, occluder: false);
            }
            var fonte = ModelLibrary.Instantiate("Environment/Fonte4", "Fonte da Brasa", parent);
            if (fonte != null)
            {
                fonte.transform.localPosition = new Vector3(cx, 0, cz);
                var gem = FindDeep(fonte.transform, "Gem");
                if (gem != null) gem.gameObject.AddComponent<Spinner>();
            }
            LocalLights.Add(new Vector3(cx, 6.8f, cz), new Color(1f, 0.6f, 0.25f), 13f);
            AmbientFx.Embers(parent, new Vector3(cx, 6.2f, cz), 0.3f, 7f);

            // ---- peças: barracas, postes, braseiros, estandartes, poço, obelisco, árvores, caixotes
            foreach (var pr in _map.Props)
            {
                var pf = Env(pr.Kind);
                if (pf == null) continue;
                var rot = Quaternion.Euler(0, pr.Yaw, 0);
                var pos = new Vector3(pr.X, 0, pr.Z);
                var sb = Slice(pos);
                // modelos v4 já têm blocos e tábuas modelados: sem a textura extra de alvenaria/tábua (Building)
                sb.Building = !(pr.Kind.EndsWith("4") || pr.Kind.StartsWith("Arvore") || pr.Kind.StartsWith("Pinheiro") || pr.Kind == "Arbusto");
                sb.Add(pf, Matrix4x4.TRS(pos, rot, Vector3.one * pr.Scale), Color.white);
                sb.Building = false;
                switch (pr.Kind)
                {
                    case "Poste4": LocalLights.Add(pos + rot * new Vector3(0, 3.3f, 0.8f), new Color(1f, 0.62f, 0.3f), 7.5f); break;
                    case "Braseiro4": LocalLights.Add(pos + Vector3.up * 2.0f, new Color(1f, 0.55f, 0.22f), 8f); AmbientFx.Embers(parent, pos + Vector3.up * 1.7f, 0.25f, 6f); break;
                    case "Obelisco4": LocalLights.Add(pos + Vector3.up * 5.9f * pr.Scale, new Color(1f, 0.55f, 0.22f), 6f); break;
                }
            }
            for (int q = 0; q < 8; q++) slices[q].Build($"Cidade de Brasalume {q}", parent, occluder: true);
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
                if (TownDist(x, z) < TownWallRadius + 8f || IsRoad(x, z) || IsRoad(x + 1.5f, z + 1.5f) || IsRoad(x - 1.5f, z - 1.5f) || NearPortal(x, z, 6f)) continue;
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
