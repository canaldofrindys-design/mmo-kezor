using System.Collections.Generic;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Luzes locais do cenário (braseiros, postes, forja, portais, fonte). O shader Kezor/LowPoly soma até 8 delas,
    /// as mais próximas do foco da câmera, como poças de luz quente no chão e nas paredes. Sem Light do Unity:
    /// o mapa registra as luzes ao ser montado e a cada quadro sobem só as 8 mais perto.
    /// </summary>
    public static class LocalLights
    {
        public const int Max = 8;
        private struct L { public Vector3 Pos; public Color Col; public float Range; public float Phase; }
        private static readonly List<L> _all = new List<L>();
        private static readonly Vector4[] _pos = new Vector4[Max];
        private static readonly Vector4[] _col = new Vector4[Max];
        private static readonly int PosId = Shader.PropertyToID("_KezorPointPos");
        private static readonly int ColId = Shader.PropertyToID("_KezorPointCol");
        /// <summary>Força geral (a noite do Vale usa 1; dia claro poderia usar menos).</summary>
        public static float Strength = 1.7f;

        public static int Count => _all.Count;
        public static void Clear() { _all.Clear(); Upload(Vector3.zero); }
        public static void Add(Vector3 pos, Color col, float range) =>
            _all.Add(new L { Pos = pos, Col = col, Range = range, Phase = (pos.x * 1.7f + pos.z * 2.3f) % 6.28f });

        /// <summary>Sobe um conjunto fixo (tela de seleção) sem mexer nas luzes do mapa.</summary>
        public static void UploadSet(IReadOnlyList<(Vector3 pos, Color col, float range)> set)
        {
            float t = Time.time;
            for (int k = 0; k < Max; k++)
            {
                if (k < set.Count)
                {
                    var (p, c, r) = set[k];
                    float flick = 0.88f + Mathf.Sin(t * 9f + k * 1.7f) * 0.07f + Mathf.Sin(t * 23f + k) * 0.05f;
                    _pos[k] = new Vector4(p.x, p.y, p.z, r); _col[k] = (Vector4)(c * flick);
                }
                else { _pos[k] = new Vector4(0, -999, 0, 0.01f); _col[k] = Vector4.zero; }
            }
            Shader.SetGlobalVectorArray(PosId, _pos);
            Shader.SetGlobalVectorArray(ColId, _col);
        }

        private static readonly List<(float d, int i)> _sort = new List<(float, int)>();

        /// <summary>Escolhe as 8 luzes mais próximas de `focus` e manda para o shader (com tremulação de chama).</summary>
        public static void Upload(Vector3 focus)
        {
            _sort.Clear();
            for (int i = 0; i < _all.Count; i++) _sort.Add(((_all[i].Pos - focus).sqrMagnitude, i));
            _sort.Sort((a, b) => a.d.CompareTo(b.d));
            float t = Time.time;
            for (int k = 0; k < Max; k++)
            {
                if (k < _sort.Count)
                {
                    var l = _all[_sort[k].i];
                    float flick = 0.88f + Mathf.Sin(t * 9f + l.Phase) * 0.07f + Mathf.Sin(t * 23f + l.Phase * 2f) * 0.05f;
                    _pos[k] = new Vector4(l.Pos.x, l.Pos.y, l.Pos.z, l.Range);
                    _col[k] = (Vector4)(l.Col * (flick * Strength));
                }
                else { _pos[k] = new Vector4(0, -999, 0, 0.01f); _col[k] = Vector4.zero; }
            }
            Shader.SetGlobalVectorArray(PosId, _pos);
            Shader.SetGlobalVectorArray(ColId, _col);
        }
    }
}
