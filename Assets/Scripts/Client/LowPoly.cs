using System.Collections.Generic;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Gerador de malhas low poly com sombreamento flat (vértices não compartilhados) e cor por vértice.
    /// Substitui assets do Blender: todo modelo do jogo é montado aqui a partir de primitivas.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Color> _c = new List<Color>();
        private readonly List<int> _t = new List<int>();
        public Matrix4x4 M = Matrix4x4.identity;
        public int VertexCount => _v.Count;

        public MeshBuilder SetTRS(Vector3 pos, Quaternion rot, Vector3 scale) { M = Matrix4x4.TRS(pos, rot, scale); return this; }
        public MeshBuilder ResetMatrix() { M = Matrix4x4.identity; return this; }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            int i = _v.Count;
            _v.Add(M.MultiplyPoint3x4(a)); _v.Add(M.MultiplyPoint3x4(b)); _v.Add(M.MultiplyPoint3x4(c));
            _c.Add(col); _c.Add(col); _c.Add(col);
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col) { Tri(a, b, c, col); Tri(a, c, d, col); }

        /// <summary>Caixa centrada em c, tamanho s.</summary>
        public void Box(Vector3 c, Vector3 s, Color col, Color? top = null)
        {
            Vector3 h = s * 0.5f;
            Vector3 p0 = c + new Vector3(-h.x, -h.y, -h.z), p1 = c + new Vector3(h.x, -h.y, -h.z), p2 = c + new Vector3(h.x, -h.y, h.z), p3 = c + new Vector3(-h.x, -h.y, h.z);
            Vector3 p4 = c + new Vector3(-h.x, h.y, -h.z), p5 = c + new Vector3(h.x, h.y, -h.z), p6 = c + new Vector3(h.x, h.y, h.z), p7 = c + new Vector3(-h.x, h.y, h.z);
            Quad(p4, p7, p6, p5, top ?? col);   // cima
            Quad(p0, p1, p2, p3, col);          // baixo
            Quad(p0, p4, p5, p1, col);          // -z
            Quad(p2, p6, p7, p3, col);          // +z
            Quad(p3, p7, p4, p0, col);          // -x
            Quad(p1, p5, p6, p2, col);          // +x
        }

        /// <summary>Tronco de cone / cilindro / cone (rTop = 0) com base em 'b' e altura h.</summary>
        public void Cylinder(Vector3 b, float rBottom, float rTop, float h, int sides, Color col, Color? capCol = null, float twist = 0)
        {
            Vector3 top = b + Vector3.up * h;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2 + twist, a1 = ((i + 1) / (float)sides) * Mathf.PI * 2 + twist;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = b + d0 * rBottom, b1 = b + d1 * rBottom, t0 = top + d0 * rTop, t1 = top + d1 * rTop;
                if (rTop > 0.0001f) { Quad(b0, t0, t1, b1, col); Tri(top, t1, t0, capCol ?? col); }
                else Tri(b0, top, b1, col);
                Tri(b, b0, b1, capCol ?? col);
            }
        }

        public void Cone(Vector3 b, float r, float h, int sides, Color col) => Cylinder(b, r, 0, h, sides, col);

        /// <summary>Octaedro (gemas, cristais).</summary>
        public void Gem(Vector3 c, float r, float h, Color col)
        {
            Vector3 t = c + Vector3.up * h, d = c - Vector3.up * h;
            Vector3[] e = { c + new Vector3(r, 0, 0), c + new Vector3(0, 0, r), c + new Vector3(-r, 0, 0), c + new Vector3(0, 0, -r) };
            for (int i = 0; i < 4; i++) { var a = e[i]; var b = e[(i + 1) % 4]; Tri(a, t, b, col); Tri(b, d, a, col); }
        }

        private static readonly Vector3[] IcoV;
        private static readonly int[] IcoT = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        static MeshBuilder()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            IcoV = new[] { new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0), new Vector3(0,-1,t), new Vector3(0,1,t),
                           new Vector3(0,-1,-t), new Vector3(0,1,-t), new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) };
            for (int i = 0; i < IcoV.Length; i++) IcoV[i] = IcoV[i].normalized;
        }

        /// <summary>Icosaedro (opcionalmente distorcido) — pedras, copas de árvore, lodos.</summary>
        public void Ico(Vector3 c, Vector3 radius, Color col, float jitter = 0, int seed = 0, Color? col2 = null)
        {
            var v = new Vector3[IcoV.Length];
            var rnd = new System.Random(seed);
            for (int i = 0; i < v.Length; i++)
            {
                float j = 1f + (float)(rnd.NextDouble() * 2 - 1) * jitter;
                v[i] = c + Vector3.Scale(IcoV[i] * j, radius);
            }
            for (int i = 0; i < IcoT.Length; i += 3)
            {
                Color cc = col;
                if (col2.HasValue && rnd.NextDouble() < 0.35) cc = col2.Value;
                Tri(v[IcoT[i]], v[IcoT[i + 1]], v[IcoT[i + 2]], cc);
            }
        }

        /// <summary>Anel plano (efeitos de área, bases).</summary>
        public void Ring(Vector3 c, float rIn, float rOut, int sides, Color col)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2, a1 = (i + 1) / (float)sides * Mathf.PI * 2;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Quad(c + d0 * rIn, c + d0 * rOut, c + d1 * rOut, c + d1 * rIn, col);
                Quad(c + d1 * rIn, c + d1 * rOut, c + d0 * rOut, c + d0 * rIn, col);
            }
        }

        /// <summary>Prisma triangular (telhados).</summary>
        public void Roof(Vector3 c, Vector3 s, Color col, Color gable)
        {
            Vector3 h = s * 0.5f;
            Vector3 a = c + new Vector3(-h.x, 0, -h.z), b = c + new Vector3(h.x, 0, -h.z), d = c + new Vector3(h.x, 0, h.z), e = c + new Vector3(-h.x, 0, h.z);
            Vector3 r0 = c + new Vector3(-h.x, s.y, 0), r1 = c + new Vector3(h.x, s.y, 0);
            Quad(a, r0, r1, b, col);
            Quad(d, r1, r0, e, col);
            Tri(e, r0, a, gable);
            Tri(b, r1, d, gable);
        }

        public Mesh ToMesh(string name = "kezor")
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(_v); m.SetColors(_c); m.SetTriangles(_t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>Material único do jogo e parâmetros globais de luz/névoa.</summary>
    public static class KezorLook
    {
        private static Material? _mat;
        public static readonly int FlashId = Shader.PropertyToID("_Flash");
        public static readonly int TintId = Shader.PropertyToID("_Tint");

        public static Material Material
        {
            get
            {
                if (_mat != null) return _mat;
                var sh = Shader.Find("Kezor/LowPoly");
                if (sh == null) { Debug.LogWarning("[Kezor] Shader Kezor/LowPoly não encontrado; usando fallback."); sh = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"); }
                _mat = new Material(sh) { name = "KezorLowPoly", enableInstancing = true };
                return _mat;
            }
        }

        private static Material? _occluder;
        public static readonly int SeeTargetId = Shader.PropertyToID("_KezorSeeTarget");

        /// <summary>
        /// Mesmo material, com _Occluder = 1: o shader abre um "furo" pontilhado onde a malha fica entre a câmera e o
        /// herói (muralha, torres, casas, árvores). Usado nas malhas combinadas, onde não dá para esconder objeto a objeto.
        /// </summary>
        public static Material OccluderMaterial
        {
            get
            {
                if (_occluder != null) return _occluder;
                _occluder = new Material(Material) { name = "KezorLowPoly (oclusor)" };
                _occluder.SetFloat("_Occluder", 1f);
                return _occluder;
            }
        }

        /// <summary>Ponto que a câmera precisa enxergar (w = 1 liga o recorte; 0 desliga).</summary>
        public static void SetSeeThroughTarget(Vector3 pos, bool enabled) =>
            Shader.SetGlobalVector(SeeTargetId, new Vector4(pos.x, pos.y, pos.z, enabled ? 1f : 0f));

        /// <summary>Clima de fantasia sombria: sol baixo alaranjado, céu azul-acinzentado, névoa fria.</summary>
        public static void ApplyEnvironment(Camera cam) => ApplyEnvironment(cam, Kezor.Core.World.MapDefinition.ValeId);

        /// <summary>Luz e névoa por mapa: Vale (noite de luar), Pântano (névoa verde baixa) e Cripta (escuro violeta).</summary>
        public static void ApplyEnvironment(Camera cam, int mapId)
        {
            Vector3 sun; Color sunC, sky, ground, fog; Vector4 fogP;
            switch (mapId)
            {
                case Kezor.Core.World.MapDefinition.SwampId:
                    sun = Quaternion.Euler(55f, 20f, 0) * Vector3.forward;
                    sunC = new Color(0.62f, 0.7f, 0.55f); sky = new Color(0.32f, 0.38f, 0.33f); ground = new Color(0.14f, 0.15f, 0.11f);
                    fog = new Color(0.17f, 0.21f, 0.17f); fogP = new Vector4(10f, 46f, 0.92f, 0);
                    break;
                case Kezor.Core.World.MapDefinition.CryptId:
                    sun = Quaternion.Euler(70f, -60f, 0) * Vector3.forward;
                    sunC = new Color(0.5f, 0.45f, 0.66f); sky = new Color(0.42f, 0.38f, 0.56f); ground = new Color(0.16f, 0.13f, 0.2f);
                    fog = new Color(0.05f, 0.04f, 0.08f); fogP = new Vector4(12f, 40f, 0.95f, 0);
                    break;
                default:   // noite de luar: azul frio vindo de cima, o calor vem dos braseiros, postes e da fonte
                    sun = Quaternion.Euler(50f, -35f, 0) * Vector3.forward;
                    sunC = new Color(0.5f, 0.6f, 0.9f) * 0.5f; sky = new Color(0.17f, 0.21f, 0.33f); ground = new Color(0.07f, 0.06f, 0.07f);
                    fog = new Color(0.05f, 0.065f, 0.11f); fogP = new Vector4(34f, 82f, 0.88f, 0);
                    break;
            }
            Shader.SetGlobalVector("_KezorSunDir", sun.normalized);
            Shader.SetGlobalColor("_KezorSunColor", sunC);
            Shader.SetGlobalColor("_KezorSkyColor", sky);
            Shader.SetGlobalColor("_KezorGroundColor", ground);
            Shader.SetGlobalColor("_KezorFogColor", fog);
            Shader.SetGlobalVector("_KezorFogParams", fogP);
            Shader.SetGlobalFloat("_KezorEmissive", 1f);
            // névoa rasteira (x = topo, y = espessura, z = intensidade, w = puxa para o tom do céu)
            Shader.SetGlobalVector("_KezorLowFog", mapId == Kezor.Core.World.MapDefinition.SwampId ? new Vector4(0.7f, 1.1f, 0.75f, 0.25f)
                : mapId == Kezor.Core.World.MapDefinition.CryptId ? new Vector4(0.35f, 0.7f, 0.5f, 0.45f) : new Vector4(0.8f, 1.2f, 0.5f, 0.35f));
            DetailTextures.Ensure();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = fog;
        }

        public static GameObject MakeObject(string name, Mesh mesh, Transform? parent = null, bool occluder = false)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = occluder ? OccluderMaterial : Material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        /// <summary>Cor emissiva: alfa abaixo de 0,5 brilha ignorando a luz (0 = brilho total).</summary>
        public static Color Glow(Color c, float amount = 1f) { c.a = 0.5f * (1f - Mathf.Clamp01(amount)); return c; }
    }
}
