using System.Collections.Generic;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Modelos low poly feitos no Blender (Art/Kezor_Models.blend + Art/scripts), importados de Assets/Resources/Models.
    /// As cores vêm na cor de vértice (alfa &lt; 0,5 = emissivo), então todos usam o material único Kezor/LowPoly.
    /// Se um modelo não existir, quem chama usa o modelo procedural de Models.cs/WorldBuilder.cs.
    /// </summary>
    public static class ModelLibrary
    {
        public const string Folder = "Models/";
        /// <summary>Desligue para forçar os modelos procedurais (comparação/depuração).</summary>
        public static bool Enabled = true;
        private static readonly Dictionary<string, GameObject?> _cache = new Dictionary<string, GameObject?>();

        public static GameObject? Prefab(string path)
        {
            if (!Enabled) return null;
            if (_cache.TryGetValue(path, out var p)) return p;
            p = Resources.Load<GameObject>(Folder + path);
            if (p == null) Debug.LogWarning($"[Kezor] Modelo '{path}' não encontrado em Resources/{Folder}; usando o procedural.");
            _cache[path] = p;
            return p;
        }

        /// <summary>True se todas as malhas do prefab podem ser lidas na CPU (necessário para combinar por chunk).</summary>
        public static bool IsReadable(GameObject prefab)
        {
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) return false;
            return true;
        }

        /// <summary>Troca os materiais importados pelo material Kezor/LowPoly (cor por vértice, _Flash/_Tint).</summary>
        public static void ApplyLook(GameObject go, List<Renderer>? into = null)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                int n = Mathf.Max(1, r.sharedMaterials.Length);
                var mats = new Material[n];
                for (int i = 0; i < n; i++) mats[i] = KezorLook.Material;
                r.sharedMaterials = mats;
                KezorUrp.Shadows(r);
                into?.Add(r);
            }
        }

        public static GameObject? Instantiate(string path, string name, Transform? parent = null)
        {
            var pf = Prefab(path);
            if (pf == null) return null;
            var go = Object.Instantiate(pf, parent, false);
            go.name = name;
            ApplyLook(go);
            return go;
        }

        /// <summary>
        /// Instancia um personagem/monstro e monta o Rig pelos nomes dos filhos:
        /// Body → Head, ArmL, ArmR, LegL, LegR, Leg0..N (quadrúpedes/aranha) e Orb0..N (orbes do Arauto).
        /// </summary>
        public static Rig? Rig(string path, string name, bool bouncy = false, bool hover = false)
        {
            var pf = Prefab(path);
            if (pf == null) return null;
            var go = Object.Instantiate(pf);
            go.name = name;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var body = go.transform.Find("Body");
            if (body == null)
            {
                // Importado sem "Preserve Hierarchy": o próprio objeto raiz é o Body. Cria uma raiz por cima.
                if (go.transform.Find("Head") == null && go.transform.Find("Leg0") == null && go.GetComponent<MeshFilter>() == null)
                {
                    Debug.LogWarning($"[Kezor] Modelo '{path}' sem objeto 'Body'; usando o procedural.");
                    Object.Destroy(go);
                    return null;
                }
                var root = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.name = "Body";
                body = go.transform;
                go = root;
            }
            var rig = new Rig { Root = go.transform, Body = body, Bouncy = bouncy, Hover = hover };
            rig.Head = body.Find("Head");
            rig.ArmL = body.Find("ArmL");
            rig.ArmR = body.Find("ArmR");
            rig.LegL = body.Find("LegL");
            rig.LegR = body.Find("LegR");
            rig.WingL = body.Find("WingL"); rig.WingR = body.Find("WingR");   // mariposa: asas que batem
            for (int i = 0; i < 32; i++)
            {
                var l = body.Find("Leg" + i) ?? body.Find("Orb" + i);
                if (l == null) break;
                rig.ExtraLegs.Add(l);
            }
            ApplyLook(go, rig.Renderers);
            bool has = false;
            var b = new Bounds();
            foreach (var r in rig.Renderers)
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            rig.Height = has ? Mathf.Max(0.5f, b.max.y) + (hover ? 0.4f : 0f) : 2f;
            return rig;
        }
    }

    /// <summary>Junta várias instâncias de modelos estáticos numa malha só (natureza, muralhas, casas).</summary>
    public sealed class StaticBatch
    {
        private sealed class Src
        {
            public Vector3[] V = null!, N = null!;
            public Color32[] C = null!;
            public Vector2[] U = null!;   // UV2: id da textura de detalhe
            public int[] T = null!;
        }

        private static readonly Dictionary<Mesh, Src> _src = new Dictionary<Mesh, Src>();
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Color32> _c = new List<Color32>();
        private readonly List<Vector2> _u = new List<Vector2>();
        private readonly List<Vector3> _tp = new List<Vector3>();   // UV3: posição no modelo (× escala) — textura segue o modelo
        private readonly List<Vector3> _tn = new List<Vector3>();   // UV4: normal no modelo
        private readonly List<int> _t = new List<int>();
        public int VertexCount => _v.Count;
        /// <summary>Construções: madeira vira tábuas (15) e pedra vira alvenaria (16) na textura.</summary>
        public bool Building;

        private static Src Get(Mesh m)
        {
            if (_src.TryGetValue(m, out var s)) return s;
            s = new Src { V = m.vertices, N = m.normals, C = m.colors32 };
            var uv = new List<Vector2>(); m.GetUVs(1, uv);
            s.U = uv.Count == s.V.Length ? uv.ToArray() : new Vector2[s.V.Length];
            var tris = new List<int>();
            for (int i = 0; i < m.subMeshCount; i++) tris.AddRange(m.GetTriangles(i));
            s.T = tris.ToArray();
            if (s.N.Length != s.V.Length) { m.RecalculateNormals(); s.N = m.normals; }
            if (s.C.Length != s.V.Length) { s.C = new Color32[s.V.Length]; for (int i = 0; i < s.C.Length; i++) s.C[i] = new Color32(255, 255, 255, 255); }
            _src[m] = s;
            return s;
        }

        /// <summary>Adiciona o prefab com a matriz dada; tint multiplica o RGB (mantém o alfa = emissão).</summary>
        public void Add(GameObject prefab, Matrix4x4 m, Color tint) => Add(prefab, m, tint, null);

        /// <summary>Como Add, com troca de cor por vértice (ex.: cor do telhado) antes do tint.</summary>
        public void Add(GameObject prefab, Matrix4x4 m, Color tint, System.Func<Color32, Color32>? remap)
        {
            var toRoot = prefab.transform.worldToLocalMatrix;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var s = Get(mesh);
                var mm = m * toRoot * mf.transform.localToWorldMatrix;
                var nm = mm.inverse.transpose;                    // normais certas com escala não uniforme (muralha esticada)
                var lm = toRoot * mf.transform.localToWorldMatrix;  // vértice → espaço do modelo
                var lnm = lm.inverse.transpose;
                var sc = new Vector3(((Vector3)m.GetColumn(0)).magnitude, ((Vector3)m.GetColumn(1)).magnitude, ((Vector3)m.GetColumn(2)).magnitude);
                int baseIdx = _v.Count;
                for (int i = 0; i < s.V.Length; i++)
                {
                    _v.Add(mm.MultiplyPoint3x4(s.V[i]));
                    _n.Add(nm.MultiplyVector(s.N[i]).normalized);
                    var uv = s.U[i];
                    if (Building) { int id = Mathf.RoundToInt(uv.x); if (id == 6) uv.x = 15; else if (id == 7) uv.x = 16; }
                    _u.Add(uv);
                    _tp.Add(Vector3.Scale(lm.MultiplyPoint3x4(s.V[i]), sc));
                    var ln = lnm.MultiplyVector(s.N[i]);
                    _tn.Add(ln.sqrMagnitude > 1e-8f ? ln.normalized : Vector3.up);
                    var c = remap != null ? remap(s.C[i]) : s.C[i];
                    _c.Add(new Color32((byte)(c.r * tint.r), (byte)(c.g * tint.g), (byte)(c.b * tint.b), c.a));
                }
                bool flip = mm.determinant < 0;
                for (int i = 0; i < s.T.Length; i += 3)
                {
                    if (flip) { _t.Add(baseIdx + s.T[i]); _t.Add(baseIdx + s.T[i + 2]); _t.Add(baseIdx + s.T[i + 1]); }
                    else { _t.Add(baseIdx + s.T[i]); _t.Add(baseIdx + s.T[i + 1]); _t.Add(baseIdx + s.T[i + 2]); }
                }
            }
        }

        public void Add(GameObject prefab, Vector3 pos, Quaternion rot, Vector3 scale, Color tint) => Add(prefab, Matrix4x4.TRS(pos, rot, scale), tint);

        public GameObject? Build(string name, Transform parent, bool occluder = false)
        {
            if (_v.Count == 0) return null;
            var mesh = new Mesh { name = name };
            if (_v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_v); mesh.SetNormals(_n); mesh.SetColors(_c); mesh.SetUVs(1, _u); mesh.SetUVs(3, _tp); mesh.SetUVs(4, _tn); mesh.SetTriangles(_t, 0);
            mesh.RecalculateBounds();
            return KezorLook.MakeObject(name, mesh, parent, occluder);
        }
    }

    /// <summary>Gira e flutua um objeto (gema da Fonte de Brasas).</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public float DegreesPerSecond = 40f, BobHeight = 0.12f, BobSpeed = 1.5f;
        private Vector3 _base;
        private void Start() { _base = transform.localPosition; }
        private void Update()
        {
            transform.localRotation = Quaternion.Euler(0, Time.time * DegreesPerSecond, 0);
            transform.localPosition = _base + Vector3.up * (Mathf.Sin(Time.time * BobSpeed) * BobHeight);
        }
    }
}
