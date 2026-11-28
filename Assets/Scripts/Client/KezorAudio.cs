using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Mixer do jogo: três canais (efeitos, ambiente e interface) por cima do volume geral, salvos nas Opções, e o
    /// ouvinte colado no herói. Quando há luta perto do herói, o ambiente abaixa um pouco (ducking) para os golpes
    /// aparecerem e volta devagar depois.
    /// O ouvinte fica no herói, não na câmera: a câmera isométrica fica a uns 20 m, e com ela a fonte e a forja da vila
    /// soariam sempre longe.
    /// </summary>
    public static class KezorAudio
    {
        public const string SfxPref = "kz.vol.sfx", AmbientPref = "kz.vol.amb", UiPref = "kz.vol.ui";
        private static float? _sfx, _amb, _ui;

        /// <summary>Canal de efeitos (combate, monstros, passos, moedas), 0..1.</summary>
        public static float Sfx { get => _sfx ??= PlayerPrefs.GetFloat(SfxPref, 1f); set { _sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxPref, _sfx.Value); } }
        /// <summary>Canal de ambiente (vila, natureza), 0..1.</summary>
        public static float Ambient { get => _amb ??= PlayerPrefs.GetFloat(AmbientPref, 0.8f); set { _amb = Mathf.Clamp01(value); PlayerPrefs.SetFloat(AmbientPref, _amb.Value); } }
        /// <summary>Canal de interface (cliques, janelas), 0..1.</summary>
        public static float Ui { get => _ui ??= PlayerPrefs.GetFloat(UiPref, 0.8f); set { _ui = Mathf.Clamp01(value); PlayerPrefs.SetFloat(UiPref, _ui.Value); } }

        // ---------------------------------------------------------------- ducking do ambiente
        private static float _lastCombat = -99f, _duck = 1f;
        private const float DuckLevel = 0.55f;   // cerca de -5 dB durante a luta

        /// <summary>Avisa que houve golpe perto do herói (chamado pelos sons de impacto).</summary>
        public static void NotifyCombat() => _lastCombat = Time.time;

        /// <summary>Multiplicador atual do ambiente: desce rápido quando a luta começa e sobe em ~2 s quando para.</summary>
        public static float AmbientDuck
        {
            get
            {
                float want = Time.time - _lastCombat < 2.5f ? DuckLevel : 1f;
                float speed = want < _duck ? 4f : 0.5f;
                _duck = Mathf.MoveTowards(_duck, want, speed * Time.unscaledDeltaTime);
                return _duck;
            }
        }

        // ---------------------------------------------------------------- ouvinte
        private static AudioListener? _ear;

        /// <summary>Coloca o ouvinte na cabeça do herói, virado como a câmera (esquerda e direita batem com a tela).</summary>
        public static void UpdateListener(Camera? cam, Transform? hero)
        {
            if (cam == null) return;
            if (_ear == null)
            {
#if UNITY_6000_6_OR_NEWER
                foreach (var l in Object.FindObjectsByType<AudioListener>()) l.enabled = false;
#else
                foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) l.enabled = false;
#endif
                var go = new GameObject("Ouvinte (herói)");
                Object.DontDestroyOnLoad(go);
                _ear = go.AddComponent<AudioListener>();
            }
            var fwd = cam.transform.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            _ear.transform.SetPositionAndRotation(hero != null ? hero.position + Vector3.up * 1.6f : cam.transform.position, Quaternion.LookRotation(fwd));
        }
    }
}
