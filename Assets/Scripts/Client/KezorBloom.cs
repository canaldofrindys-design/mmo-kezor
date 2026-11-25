using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Bloom próprio para o pipeline Built-in (sem pacote de pós-processamento): o shader Kezor/LowPoly escreve o
    /// emissivo acima de 1 no buffer HDR (refino +11..+15, armas épicas, asas de 3º nível, fogo) e este passe espalha
    /// esse excesso num halo. Liga/desliga e intensidade ficam nas Opções (PlayerPrefs "kz.bloomOn" e "kz.bloom").
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class KezorBloom : MonoBehaviour
    {
        public const string PrefOn = "kz.bloomOn", PrefIntensity = "kz.bloom";
        public static bool On = true;
        /// <summary>0 a 2 (1 = padrão).</summary>
        public static float Intensity = 1f;
        public float Threshold = 1.05f, Knee = 0.5f, Spread = 1f;
        /// <summary>Multiplicador desta câmera (a tela de seleção, mais iluminada, usa menos).</summary>
        public float Scale = 1f;
        private const int Levels = 6;
        private static Material? _mat;
        private readonly RenderTexture?[] _chain = new RenderTexture?[Levels];
        private static readonly int ParamsId = Shader.PropertyToID("_Params"), BloomId = Shader.PropertyToID("_BloomTex");

        public static void LoadPrefs()
        {
            On = PlayerPrefs.GetInt(PrefOn, 1) == 1;
            Intensity = Mathf.Clamp(PlayerPrefs.GetFloat(PrefIntensity, 1f), 0f, 2f);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(PrefOn, On ? 1 : 0);
            PlayerPrefs.SetFloat(PrefIntensity, Intensity);
            PlayerPrefs.Save();
        }

        public static KezorBloom Attach(Camera cam)
        {
            cam.allowHDR = true;
            var b = cam.GetComponent<KezorBloom>();
            return b != null ? b : cam.gameObject.AddComponent<KezorBloom>();
        }

        private static Material? Mat
        {
            get
            {
                if (_mat != null) return _mat;
                var sh = Resources.Load<Shader>("Shaders/KezorBloom");
                if (sh == null || !sh.isSupported) return null;
                return _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            var m = Mat;
            if (!On || Intensity <= 0.001f || m == null) { Graphics.Blit(src, dst); return; }
            m.SetVector(ParamsId, new Vector4(Threshold, Knee, Intensity * 0.9f * Scale, Spread));
            var fmt = src.format == RenderTextureFormat.DefaultHDR || src.format == RenderTextureFormat.ARGBHalf ? src.format : RenderTextureFormat.DefaultHDR;
            int w = src.width / 2, h = src.height / 2;
            int n = 0;
            RenderTexture? prev = null;
            for (; n < Levels && w >= 8 && h >= 8; n++, w /= 2, h /= 2)
            {
                var rt = RenderTexture.GetTemporary(w, h, 0, fmt);
                rt.filterMode = FilterMode.Bilinear;
                _chain[n] = rt;
                Graphics.Blit(n == 0 ? src : prev, rt, m, n == 0 ? 0 : 1);
                prev = rt;
            }
            for (int i = n - 1; i > 0; i--) Graphics.Blit(_chain[i], _chain[i - 1], m, 2);   // soma ampliando
            m.SetTexture(BloomId, _chain[0]);
            Graphics.Blit(src, dst, m, 3);
            for (int i = 0; i < n; i++) { RenderTexture.ReleaseTemporary(_chain[i]); _chain[i] = null; }
        }
    }
}
