using System.IO;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Vitrine de desenvolvimento: monta as 7 classes com o conjunto típico de cada uma, lado a lado, e salva uma foto
    /// (comparação antes/depois do visual mid poly). Só em play mode (o Gear usa Destroy).
    /// </summary>
    public static class HeroLineup
    {
        private static readonly (CharacterClass cls, int set, int weapon, int off)[] Kits =
        {
            (CharacterClass.Warden, 140, 1, 120), (CharacterClass.Arcanist, 130, 11, 0), (CharacterClass.Ranger, 110, 21, 0),
            (CharacterClass.Bladeweaver, 150, 51, 0), (CharacterClass.Commander, 160, 54, 0), (CharacterClass.Invoker, 170, 57, 0),
            (CharacterClass.EmberFist, 180, 60, 0),
        };

        public static GameObject Build(Vector3 origin, int level = 9, bool gear = true)
        {
            var root = new GameObject("HeroLineup");
            root.transform.position = origin;
            for (int i = 0; i < Kits.Length; i++)
            {
                var k = Kits[i];
                var a = new Appearance();
                if (gear)
                {
                    Put(a, k.weapon, level); Put(a, k.off, level);
                    for (int id = k.set; id < k.set + 5; id++) Put(a, id, level);
                }
                var rig = Gear.Player(k.cls.ToString(), k.cls, a);
                rig.Root.SetParent(root.transform, false);
                rig.Root.localPosition = new Vector3((i - 3) * 1.55f, 0, (i % 2) * 0.35f);
                rig.Root.localRotation = Quaternion.Euler(0, 180f + (i - 3) * 6f, 0);
            }
            return root;
        }

        private static void Put(Appearance a, int id, int level)
        {
            if (id == 0) return;
            var d = ItemDatabase.Get(id);
            if (d == null) return;
            int s = System.Array.IndexOf(Appearance.Slots, d.Slot);
            if (s >= 0) a.Pieces[s] = new Appearance.Piece { Id = id, Level = level };
        }

        /// <summary>Fotografa a vitrine. close = enquadra só os três do meio (detalhe).</summary>
        public static string Capture(string path, Vector3 origin, int width = 1920, int height = 1080, bool close = false, bool detail = true)
        {
            var go = new GameObject("LineupCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = close ? 24f : 30f;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
            var look = origin + new Vector3(0, close ? 1.15f : 1.0f, 0);
            go.transform.position = origin + (close ? new Vector3(0, 1.9f, -6.2f) : new Vector3(0, 3.2f, -13.5f));
            go.transform.LookAt(look);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.075f, 0.09f);
            var bloom = KezorBloom.Attach(cam); bloom.Scale = 0.85f;
            DetailTextures.Ensure();
            Shader.SetGlobalVector("_KezorLowFog", Vector4.zero);
            if (!detail) Shader.SetGlobalFloat("_KezorDetailOn", 0f);
            Shader.SetGlobalVector("_KezorSunDir", (Quaternion.Euler(38f, 32f, 0) * Vector3.forward).normalized);
            Shader.SetGlobalColor("_KezorSunColor", new Color(1f, 0.9f, 0.78f) * 0.95f);
            Shader.SetGlobalColor("_KezorSkyColor", new Color(0.42f, 0.45f, 0.55f));
            Shader.SetGlobalColor("_KezorGroundColor", new Color(0.16f, 0.13f, 0.11f));
            Shader.SetGlobalVector("_KezorFogParams", new Vector4(400f, 500f, 0f, 0));
            KezorUrp.SetSun(Quaternion.Euler(38f, 32f, 0) * Vector3.forward, new Color(1f, 0.9f, 0.78f) * 0.95f, 0.5f);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex); rt.Release(); Object.Destroy(rt); Object.Destroy(go);
            return path;
        }

        /// <summary>Foto de uma câmera existente (a do jogo), sem a interface.</summary>
        public static string CaptureCamera(Camera cam, string path, int width = 1920, int height = 1080)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = old;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex); rt.Release(); Object.Destroy(rt);
            return path;
        }
    }
}

namespace Kezor.Client
{
    /// <summary>Mede o FPS médio (para conferir desempenho em desenvolvimento).</summary>
    public sealed class FpsProbe : UnityEngine.MonoBehaviour
    {
        public static float Average, Worst;
        private float _t, _worst; private int _n;
        private void Update()
        {
            float dt = UnityEngine.Time.unscaledDeltaTime;
            _t += dt; _n++; if (dt > _worst) _worst = dt;
            if (_t >= 2f) { Average = _n / _t; Worst = 1f / _worst; _t = 0; _n = 0; _worst = 0; }
        }
    }
}
