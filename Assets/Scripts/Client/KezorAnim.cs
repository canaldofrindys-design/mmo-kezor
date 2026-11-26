using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>Um clipe de animação feito no Blender: quadril e rotação de cada osso por quadro (espaço do personagem).</summary>
    public sealed class AnimClip
    {
        public string Name = "";
        public int Frames;
        public bool Loop;
        public float Duration;
        public Vector3[] Hip = null!;          // por quadro (Blender, metros)
        public float[] Rot = null!;            // por quadro e osso: w, x, y, z (Blender)
    }

    /// <summary>
    /// Animações dos heróis (Assets/Resources/Anim/Heroi.txt, geradas por Art/scripts/kezor_anim.py no Blender).
    /// Cada rotação é a do osso em relação à pose de repouso, no espaço do personagem: o jogo converte para o espaço do
    /// Unity com o espelhamento de eixos do FBX e aplica sobre o esqueleto do SkinRig (ou sobre as partes rígidas).
    /// </summary>
    public sealed class KezorAnim
    {
        public static readonly string[] BoneNames =
        {
            "Body", "Chest", "Head", "ArmL", "ForeArmL", "HandL", "ArmR", "ForeArmR", "HandR",
            "LegL", "ShinL", "FootL", "LegR", "ShinR", "FootR",
        };
        public const int Body = 0, Chest = 1, Head = 2, ArmL = 3, ForeArmL = 4, HandL = 5, ArmR = 6, ForeArmR = 7, HandR = 8,
            LegL = 9, ShinL = 10, FootL = 11, LegR = 12, ShinR = 13, FootR = 14, Count = 15;

        public readonly Dictionary<string, AnimClip> Clips = new Dictionary<string, AnimClip>();
        public readonly Dictionary<string, float> Stride = new Dictionary<string, float>();
        /// <summary>Cabeça de cada osso em repouso (Blender: X esquerda, Y trás, Z cima).</summary>
        public readonly Vector3[] Rest = new Vector3[Count];
        private readonly int[] _map = new int[Count];   // índice do osso do arquivo para cada osso nosso

        private static KezorAnim? _hero;
        private static bool _tried;

        /// <summary>Animações do herói (null se o arquivo não existir: o jogo usa a animação procedural).</summary>
        public static KezorAnim? Hero
        {
            get
            {
                if (_tried) return _hero;
                _tried = true;
                var ta = Resources.Load<TextAsset>("Anim/Heroi");
                if (ta == null) return null;
                try { _hero = Parse(ta.text); }
                catch (System.Exception e) { Debug.LogWarning("[Kezor] Anim/Heroi inválido: " + e.Message); _hero = null; }
                return _hero;
            }
        }

        public static KezorAnim Parse(string text)
        {
            var a = new KezorAnim();
            var ci = CultureInfo.InvariantCulture;
            string[]? fileBones = null;
            AnimClip? cur = null;
            int fi = 0;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                var t = line.Split(' ');
                switch (t[0])
                {
                    case "bones":
                        fileBones = new string[t.Length - 1];
                        System.Array.Copy(t, 1, fileBones, 0, fileBones.Length);
                        for (int i = 0; i < Count; i++) a._map[i] = System.Array.IndexOf(fileBones, BoneNames[i]);
                        break;
                    case "rest":
                    {
                        int i = System.Array.IndexOf(BoneNames, t[1]);
                        if (i >= 0) a.Rest[i] = new Vector3(float.Parse(t[2], ci), float.Parse(t[3], ci), float.Parse(t[4], ci));
                        break;
                    }
                    case "stride":
                        a.Stride[t[1]] = float.Parse(t[2], ci);
                        break;
                    case "clip":
                        cur = new AnimClip { Name = t[1], Frames = int.Parse(t[2], ci), Loop = t[3] == "1", Duration = float.Parse(t[4], ci) };
                        cur.Hip = new Vector3[cur.Frames];
                        cur.Rot = new float[cur.Frames * Count * 4];
                        for (int k = 0; k < cur.Rot.Length; k += 4) cur.Rot[k] = 1f;
                        a.Clips[cur.Name] = cur;
                        fi = 0;
                        break;
                    case "f":
                    {
                        if (cur == null || fileBones == null || fi >= cur.Frames) break;
                        cur.Hip[fi] = new Vector3(float.Parse(t[1], ci), float.Parse(t[2], ci), float.Parse(t[3], ci));
                        for (int i = 0; i < Count; i++)
                        {
                            int fb = a._map[i];
                            if (fb < 0) continue;
                            int s = 4 + fb * 4, d = (fi * Count + i) * 4;
                            for (int c = 0; c < 4; c++) cur.Rot[d + c] = float.Parse(t[s + c], ci);
                        }
                        fi++;
                        break;
                    }
                }
            }
            if (fileBones == null || a.Clips.Count == 0) throw new System.FormatException("sem ossos ou clipes");
            return a;
        }

        /// <summary>Blender → Unity: X espelhado conforme o FBX (sx), Z do Blender vira Y e a frente (-Y) vira +Z.</summary>
        public static Vector3 ToUnity(Vector3 b, float sx) => new Vector3(sx * b.x, b.z, -b.y);

        /// <summary>Rotação do Blender (w,x,y,z) no espaço do Unity. Com espelhamento (det &lt; 0) o eixo troca de sinal.</summary>
        public static Quaternion ToUnity(float w, float x, float y, float z, float sx)
        {
            var v = new Vector3(sx * x, z, -y);
            if (sx < 0) v = -v;                  // det(M) = sx
            return new Quaternion(v.x, v.y, v.z, w);
        }

        /// <summary>Amostra o clipe em u (0..1) e escreve rotações (Unity) e quadril (Unity, sem escala).</summary>
        public static void Sample(AnimClip c, float u, float sx, Quaternion[] rot, out Vector3 hip)
        {
            float f;
            int i0, i1;
            if (c.Loop) { u -= Mathf.Floor(u); f = u * c.Frames; i0 = Mathf.FloorToInt(f) % c.Frames; i1 = (i0 + 1) % c.Frames; }
            else { u = Mathf.Clamp01(u); f = u * (c.Frames - 1); i0 = Mathf.Min(Mathf.FloorToInt(f), c.Frames - 1); i1 = Mathf.Min(i0 + 1, c.Frames - 1); }
            float k = f - Mathf.Floor(f);
            hip = ToUnity(Vector3.Lerp(c.Hip[i0], c.Hip[i1], k), sx);
            for (int b = 0; b < Count; b++)
            {
                int a0 = (i0 * Count + b) * 4, a1 = (i1 * Count + b) * 4;
                var q0 = ToUnity(c.Rot[a0], c.Rot[a0 + 1], c.Rot[a0 + 2], c.Rot[a0 + 3], sx);
                var q1 = ToUnity(c.Rot[a1], c.Rot[a1 + 1], c.Rot[a1 + 2], c.Rot[a1 + 3], sx);
                rot[b] = Quaternion.Slerp(q0, q1, k);
            }
        }

        /// <summary>Mistura b sobre a (peso w por osso, opcionalmente com máscara).</summary>
        public static void Blend(Quaternion[] a, Vector3 hipA, Quaternion[] b, Vector3 hipB, float w, out Vector3 hip, float[]? mask = null)
        {
            for (int i = 0; i < Count; i++)
            {
                float k = mask == null ? w : w * mask[i];
                if (k <= 0f) continue;
                a[i] = k >= 1f ? b[i] : Quaternion.Slerp(a[i], b[i], k);
            }
            hip = Vector3.Lerp(hipA, hipB, mask == null ? w : w * mask[Body]);
        }
    }
}
