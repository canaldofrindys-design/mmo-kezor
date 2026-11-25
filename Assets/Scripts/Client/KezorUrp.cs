using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kezor.Client
{
    /// <summary>
    /// Ponte entre o visual do Kezor e a URP. Os shaders Kezor/LowPoly e Kezor/Terrain usam a luz da URP (sombra da luz
    /// principal, luzes adicionais Forward+), mas o ambiente e a névoa continuam vindo dos globais _Kezor* por mapa.
    /// Aqui ficam: o sol (Light direcional com sombra), o conjunto de luzes pontuais dos braseiros e postes, a sombra
    /// das malhas e o pós-processamento (Bloom e vinheta num Volume global, no lugar do KezorBloom).
    /// </summary>
    public static class KezorUrp
    {
        /// <summary>true quando o projeto está rodando com um Render Pipeline (URP) ativo.</summary>
        public static bool Active => GraphicsSettings.currentRenderPipeline != null;

        /// <summary>No Play sobrevive à troca de cena; no editor (ícones) não vai para a cena salva.</summary>
        private static void Keep(GameObject go)
        {
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            else go.hideFlags = HideFlags.HideAndDontSave;
        }

        // ---------------------------------------------------------------- sol
        private static Light? _sun;

        /// <summary>Posiciona o sol: direção para onde a luz aponta e cor (a mesma de _KezorSunColor).</summary>
        public static void SetSun(Vector3 dir, Color color, float shadowStrength = 0.8f)
        {
            if (!Active) return;
            if (_sun == null)
            {
                var go = new GameObject("Kezor Sol");
                Keep(go);
                _sun = go.AddComponent<Light>();
                _sun.type = LightType.Directional;
                _sun.shadows = LightShadows.Soft;
                _sun.shadowBias = 0.05f;
                _sun.shadowNormalBias = 0.4f;
            }
            _sun.enabled = true;
            _sun.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.down);
            _sun.color = new Color(color.r, color.g, color.b, 1f);
            _sun.intensity = 1f;
            _sun.shadowStrength = shadowStrength;
            RenderSettings.sun = _sun;
            RenderSettings.fog = false;   // a névoa é do próprio shader (_KezorFogParams)
        }

        // ---------------------------------------------------------------- luzes pontuais
        /// <summary>Quantas luzes pontuais da URP ficam acesas ao mesmo tempo (as mais perto do foco).</summary>
        public const int PointPool = 16;
        /// <summary>Converte a cor de LocalLights (pensada para o shader antigo) em intensidade de Light da URP.</summary>
        public static float PointIntensity = 3f;
        private static readonly List<Light> _points = new List<Light>();
        private static GameObject? _pointRoot;

        /// <summary>Liga a luz pontual `k` do conjunto em `pos` com cor e alcance; as que sobram ficam apagadas.</summary>
        public static void SetPoint(int k, Vector3 pos, Color col, float range)
        {
            if (!Active || k >= PointPool) return;
            var l = Point(k);
            l.transform.position = pos;
            l.color = new Color(col.r, col.g, col.b, 1f);
            l.intensity = PointIntensity;
            l.range = Mathf.Max(0.5f, range);
            l.enabled = col.maxColorComponent > 0.001f;
        }

        public static void HidePointsFrom(int k)
        {
            for (int i = k; i < _points.Count; i++) if (_points[i] != null) _points[i].enabled = false;
        }

        private static Light Point(int k)
        {
            if (_pointRoot == null) { _pointRoot = new GameObject("Kezor Luzes Locais"); Keep(_pointRoot); _points.Clear(); }
            while (_points.Count <= k)
            {
                var go = new GameObject("Luz " + _points.Count);
                go.transform.SetParent(_pointRoot.transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                l.enabled = false;
                _points.Add(l);
            }
            return _points[k];
        }

        // ---------------------------------------------------------------- sombras das malhas
        /// <summary>Na URP as malhas do cenário e os heróis projetam e recebem sombra do sol; no Built-in, como antes.</summary>
        public static void Shadows(Renderer r, bool cast = true)
        {
            if (Active)
            {
                r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
            else
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        // ---------------------------------------------------------------- pós-processamento
        private static Volume? _volume;
        private static Bloom? _bloom;
        private static Vignette? _vignette;

        /// <summary>Liga o pós da URP nesta câmera (HDR, SMAA) e garante o Volume global do Kezor.</summary>
        public static void EnsurePost(Camera cam, float scale = 1f)
        {
            if (!Active) return;
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;
            if (_volume == null)
            {
                var go = new GameObject("Kezor Pós (Volume)");
                Keep(go);
                _volume = go.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 10f;
                var p = ScriptableObject.CreateInstance<VolumeProfile>();
                p.name = "Kezor Pós";
                _bloom = p.Add<Bloom>(true);
                _bloom.threshold.Override(1.0f);
                _bloom.scatter.Override(0.65f);
                _bloom.tint.Override(new Color(1f, 0.93f, 0.85f));
                // sem tonemapping: o projeto usa espaço de cor Gamma, e o Neutral escurecia os tons médios da noite
                _vignette = p.Add<Vignette>(true);
                _vignette.intensity.Override(0.16f);
                _vignette.smoothness.Override(0.45f);
                _vignette.color.Override(new Color(0.02f, 0.015f, 0.03f));
                _volume.sharedProfile = p;
            }
            RefreshPost();
        }

        /// <summary>Aplica as Opções (liga/desliga e intensidade do brilho) no Bloom do Volume.</summary>
        public static void RefreshPost()
        {
            if (_bloom == null) return;
            _bloom.active = KezorBloom.On && KezorBloom.Intensity > 0.001f;
            _bloom.intensity.Override(KezorBloom.Intensity * 0.9f);
        }
    }
}
