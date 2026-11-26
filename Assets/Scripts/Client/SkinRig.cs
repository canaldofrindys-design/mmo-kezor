using System.Collections.Generic;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Transforma o herói montado em peças (corpo, braços, pernas e equipamento) num personagem com esqueleto:
    /// cria as juntas que faltam (cintura, cotovelo, punho, joelho, tornozelo) e troca as malhas por malhas com pele
    /// (SkinnedMeshRenderer) que dobram nas juntas, com os mesmos pesos da prévia do Blender (kezor_anim.skin_weights).
    /// Armas e escudos passam para a mão; elmo e cabeça continuam presos à cabeça; asas sobem para o peito.
    /// </summary>
    public static class SkinRig
    {
        private const float BlenderHip = 0.92f;   // pivô da coxa no esqueleto do Blender

        public static bool Build(Rig rig, KezorAnim anim)
        {
            if (rig.Bones != null) return true;
            if (rig.Head == null || rig.ArmL == null || rig.ArmR == null || rig.LegL == null || rig.LegR == null) return false;
            var body = rig.Body;
            var root = rig.Root;
            Vector3 L(Transform t) => body.InverseTransformPoint(t.position);
            float legY = L(rig.LegL).y;
            if (legY < 0.3f) return false;
            float s = legY / BlenderHip;
            float sx = L(rig.ArmL).x < 0 ? -1f : 1f;      // o FBX espelha o X: braço esquerdo do Blender (+X) cai em -X
            rig.BoneScale = s;
            rig.MirrorX = sx;
            rig.HipPivot = anim.Rest[KezorAnim.Body].z * s;

            Transform Make(string name, Transform parent, Vector3 bodyLocal)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                t.position = body.TransformPoint(bodyLocal);
                t.rotation = body.rotation;
                return t;
            }

            var chest = Make("Chest", body, new Vector3(0, anim.Rest[KezorAnim.Chest].z * s, 0));
            rig.Head.SetParent(chest, true);
            rig.ArmL.SetParent(chest, true);
            rig.ArmR.SetParent(chest, true);
            var wings = body.Find("Wings");
            if (wings != null) wings.SetParent(chest, true);

            Transform Limb(Transform top, string mid, int midI, string end, int endI, out Transform endT)
            {
                var p = L(top);
                var m = Make(mid, top, new Vector3(p.x, anim.Rest[midI].z * s, p.z));
                endT = Make(end, m, new Vector3(p.x, anim.Rest[endI].z * s, p.z));
                return m;
            }
            var foreL = Limb(rig.ArmL, "ForeArmL", KezorAnim.ForeArmL, "HandL", KezorAnim.HandL, out var handL);
            var foreR = Limb(rig.ArmR, "ForeArmR", KezorAnim.ForeArmR, "HandR", KezorAnim.HandR, out var handR);
            var shinL = Limb(rig.LegL, "ShinL", KezorAnim.ShinL, "FootL", KezorAnim.FootL, out var footL);
            var shinR = Limb(rig.LegR, "ShinR", KezorAnim.ShinR, "FootR", KezorAnim.FootR, out var footR);

            var bones = new Transform[KezorAnim.Count];
            bones[KezorAnim.Body] = body; bones[KezorAnim.Chest] = chest; bones[KezorAnim.Head] = rig.Head;
            bones[KezorAnim.ArmL] = rig.ArmL; bones[KezorAnim.ForeArmL] = foreL; bones[KezorAnim.HandL] = handL;
            bones[KezorAnim.ArmR] = rig.ArmR; bones[KezorAnim.ForeArmR] = foreR; bones[KezorAnim.HandR] = handR;
            bones[KezorAnim.LegL] = rig.LegL; bones[KezorAnim.ShinL] = shinL; bones[KezorAnim.FootL] = footL;
            bones[KezorAnim.LegR] = rig.LegR; bones[KezorAnim.ShinR] = shinR; bones[KezorAnim.FootR] = footR;

            // armas e escudos: rígidos na mão (mantêm a posição de repouso)
            foreach (var (arm, hand) in new[] { (rig.ArmL, handL), (rig.ArmR, handR) })
            {
                var move = new List<Transform>();
                foreach (Transform c in arm)
                    if (c.name.StartsWith("Arma_") || c.name.StartsWith("Escudo_")) move.Add(c);
                foreach (var c in move) c.SetParent(hand, true);
            }

            // pele: tudo o que pende do corpo, dos braços e das pernas (menos cabeça/elmo, mãos e asas)
            var filters = body.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                var part = PartOf(mf.transform, rig, chest);
                if (part == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                Convert(rig, mf, mr, part, bones, body, s);
            }

            rig.Bones = bones;
            rig.BoneRest = new Quaternion[KezorAnim.Count];
            var inv = Quaternion.Inverse(root.rotation);
            for (int i = 0; i < KezorAnim.Count; i++) rig.BoneRest[i] = inv * bones[i].rotation;
            return true;
        }

        /// <summary>Parte (Body/ArmL/ArmR/LegL/LegR) que governa os pesos da malha; null = fica rígida.</summary>
        private static string? PartOf(Transform t, Rig rig, Transform chest)
        {
            for (var p = t; p != null && p != rig.Root; p = p.parent)
            {
                if (p == rig.Head || p.name == "Wings" || p.name.StartsWith("Hand")) return null;
                if (p == rig.ArmL) return "ArmL";
                if (p == rig.ArmR) return "ArmR";
                if (p == rig.LegL) return "LegL";
                if (p == rig.LegR) return "LegR";
                if (p == rig.Body || p == chest) return "Body";
            }
            return null;
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        /// <summary>Pesos iguais aos de kezor_anim.skin_weights (z = altura no esqueleto do Blender).</summary>
        public static void Weights(string part, float z, out int b0, out float w0, out int b1, out float w1, out int b2, out float w2)
        {
            b1 = b2 = 0; w1 = w2 = 0f;
            switch (part)
            {
                case "ArmL":
                case "ArmR":
                {
                    bool l = part == "ArmL";
                    float f = Smooth(1.13f, 1.03f, z), h = Smooth(0.83f, 0.77f, z);
                    b0 = l ? KezorAnim.ArmL : KezorAnim.ArmR; w0 = 1 - f;
                    b1 = l ? KezorAnim.ForeArmL : KezorAnim.ForeArmR; w1 = f * (1 - h);
                    b2 = l ? KezorAnim.HandL : KezorAnim.HandR; w2 = f * h;
                    return;
                }
                case "LegL":
                case "LegR":
                {
                    bool l = part == "LegL";
                    float k = Smooth(0.59f, 0.47f, z), a = Smooth(0.15f, 0.08f, z);
                    b0 = l ? KezorAnim.LegL : KezorAnim.LegR; w0 = 1 - k;
                    b1 = l ? KezorAnim.ShinL : KezorAnim.ShinR; w1 = k * (1 - a);
                    b2 = l ? KezorAnim.FootL : KezorAnim.FootR; w2 = k * a;
                    return;
                }
                default:
                {
                    float c = Smooth(0.98f, 1.14f, z);
                    b0 = KezorAnim.Body; w0 = 1 - c;
                    b1 = KezorAnim.Chest; w1 = c;
                    return;
                }
            }
        }

        private static readonly List<Vector3> _v = new List<Vector3>();

        private static void Convert(Rig rig, MeshFilter mf, MeshRenderer mr, string part, Transform[] bones, Transform body, float s)
        {
            var src = mf.sharedMesh;
            var mesh = Object.Instantiate(src);
            mesh.name = src.name + "_pele";
            src.GetVertices(_v);
            var toBody = body.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var bw = new BoneWeight[_v.Count];
            for (int i = 0; i < _v.Count; i++)
            {
                float z = toBody.MultiplyPoint3x4(_v[i]).y / s;
                Weights(part, z, out var b0, out var w0, out var b1, out var w1, out var b2, out var w2);
                // ordena por peso (o Unity espera o maior primeiro) e normaliza
                if (w1 > w0) { (b0, b1) = (b1, b0); (w0, w1) = (w1, w0); }
                if (w2 > w1) { (b1, b2) = (b2, b1); (w1, w2) = (w2, w1); }
                if (w1 > w0) { (b0, b1) = (b1, b0); (w0, w1) = (w1, w0); }
                float sum = w0 + w1 + w2;
                if (sum <= 1e-5f) { w0 = 1; sum = 1; }
                bw[i] = new BoneWeight { boneIndex0 = b0, weight0 = w0 / sum, boneIndex1 = b1, weight1 = w1 / sum, boneIndex2 = b2, weight2 = w2 / sum };
            }
            mesh.boneWeights = bw;
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * mf.transform.localToWorldMatrix;
            mesh.bindposes = bind;

            var go = mf.gameObject;
            var mats = mr.sharedMaterials;
            var mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);
            bool enabled = mr.enabled;
            var shadows = mr.shadowCastingMode;
            bool receive = mr.receiveShadows;
            int idx = rig.Renderers.IndexOf(mr);
            Object.DestroyImmediate(mr);
            Object.DestroyImmediate(mf);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = body;
            smr.sharedMaterials = mats;
            smr.SetPropertyBlock(mpb);
            smr.enabled = enabled;
            smr.shadowCastingMode = shadows;
            smr.receiveShadows = receive;
            smr.quality = SkinQuality.Bone4;
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(new Vector3(0, 1.0f, 0), new Vector3(3.2f, 3.6f, 3.2f));
            if (idx >= 0) rig.Renderers[idx] = smr; else rig.Renderers.Add(smr);
        }
    }
}
