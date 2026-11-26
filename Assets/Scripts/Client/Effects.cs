using System.Collections.Generic;
using UnityEngine;
using Kezor.Core.Combat;

namespace Kezor.Client
{
    /// <summary>Efeitos visuais simples (somente cliente): projéteis, anéis de área, colunas de level up e números de dano.</summary>
    public sealed class Effects : MonoBehaviour
    {
        public struct Floater { public Vector3 Pos; public string Text; public Color Color; public float T; public float Size; }
        public readonly List<Floater> Floaters = new List<Floater>();
        private static Mesh? _orb, _ring, _column, _arrow;
        private static readonly Color G = new Color(1, 1, 1, 0); // branco emissivo; a cor vem do _Tint

        private static Mesh Orb => _orb != null ? _orb : (_orb = Build(b => b.Ico(Vector3.zero, Vector3.one * 0.18f, G)));
        private static Mesh Ring => _ring != null ? _ring : (_ring = Build(b => b.Ring(Vector3.zero, 0.85f, 1f, 24, G)));
        private static Mesh Column => _column != null ? _column : (_column = Build(b => b.Cylinder(Vector3.zero, 0.7f, 0.5f, 3f, 8, G)));
        private static Mesh Arrow => _arrow != null ? _arrow : (_arrow = Build(b => { b.Box(Vector3.zero, new Vector3(0.04f, 0.04f, 0.7f), G); b.Gem(new Vector3(0, 0, 0.38f), 0.07f, 0.07f, G); }));
        private static Mesh Build(System.Action<MeshBuilder> f) { var b = new MeshBuilder(); f(b); return b.ToMesh("fx"); }

        private GameObject Spawn(Mesh mesh, Color glow, Vector3 pos)
        {
            var go = KezorLook.MakeObject("fx", mesh, transform);
            go.transform.position = pos;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(KezorLook.TintId, glow);
            mpb.SetFloat(KezorLook.FlashId, 0.35f);
            go.GetComponent<Renderer>().SetPropertyBlock(mpb);
            return go;
        }

        public void Projectile(Vector3 from, Vector3 to, bool arrow, Color color)
        {
            var go = Spawn(arrow ? Arrow : Orb, color, from);
            StartCoroutine(Fly(go.transform, from, to, arrow ? 0.18f : 0.28f));
        }

        private System.Collections.IEnumerator Fly(Transform t, Vector3 a, Vector3 b, float dur)
        {
            for (float x = 0; x < 1f; x += Time.deltaTime / dur)
            {
                if (t == null) yield break;
                var p = Vector3.Lerp(a, b, x) + Vector3.up * Mathf.Sin(x * Mathf.PI) * 0.6f;
                if ((b - a).sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(b - a);
                t.position = p;
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        public void AreaRing(Vector3 pos, float radius, Color color)
        {
            var go = Spawn(Ring, color, pos + Vector3.up * 0.1f);
            StartCoroutine(Grow(go.transform, radius, 0.45f));
        }

        private System.Collections.IEnumerator Grow(Transform t, float radius, float dur)
        {
            for (float x = 0; x < 1f; x += Time.deltaTime / dur)
            {
                if (t == null) yield break;
                float r = Mathf.Lerp(0.2f, radius, Mathf.Sqrt(x));
                t.localScale = new Vector3(r, 1, r);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        public void LevelUp(Vector3 pos)
        {
            var go = Spawn(Column, KezorLook.Hex("#ffd36a"), pos);
            StartCoroutine(Rise(go.transform, 1.2f));
        }

        private System.Collections.IEnumerator Rise(Transform t, float dur)
        {
            var start = t.position;
            for (float x = 0; x < 1f; x += Time.deltaTime / dur)
            {
                if (t == null) yield break;
                t.position = start + Vector3.up * x * 1.5f;
                t.localScale = new Vector3(1f - x * 0.8f, 1f, 1f - x * 0.8f);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        public void Damage(Vector3 pos, HitType type, int dmg, bool onMe)
        {
            string text = type == HitType.Miss ? "Errou" : dmg.ToString();
            Color c = onMe ? new Color(1f, 0.35f, 0.3f) : Color.white;
            float size = 1f;
            switch (type)
            {
                case HitType.Critical: c = new Color(0.45f, 0.75f, 1f); size = 1.25f; break;
                case HitType.Surge: c = new Color(0.4f, 1f, 0.55f); size = 1.35f; text += "!"; break;
                case HitType.Combo: c = new Color(1f, 0.75f, 0.2f); size = 1.6f; text = "COMBO " + text; break;
                case HitType.Miss: c = new Color(0.75f, 0.75f, 0.75f); break;
            }
            Floaters.Add(new Floater { Pos = pos + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f)), Text = text, Color = c, T = 0, Size = size });
        }

        public void Text(Vector3 pos, string text, Color c, float size = 1.2f) => Floaters.Add(new Floater { Pos = pos, Text = text, Color = c, Size = size });

        private void Update()
        {
            for (int i = Floaters.Count - 1; i >= 0; i--)
            {
                var f = Floaters[i];
                f.T += Time.deltaTime;
                f.Pos += Vector3.up * Time.deltaTime * 1.4f;
                if (f.T > 1.1f) Floaters.RemoveAt(i); else Floaters[i] = f;
            }
        }
    }

    /// <summary>Câmera isométrica que segue o personagem. Roda do mouse = zoom; botão do meio arrastando = girar.</summary>
    public sealed class IsoCamera : MonoBehaviour
    {
        public Transform? Target;
        public Vector3 OrbitCenter;
        // Câmera no estilo do MU: alta (50°) e perto, o herói ocupa ~1/4 da altura da tela no zoom padrão.
        // (a chave do zoom salvo mudou para todo mundo começar no novo padrão)
        public const float DefaultDistance = 11f, DefaultYaw = 45f;
        public const string PrefYaw = "kz.camYaw", PrefDist = "kz.camDist2";
        public float Yaw = DefaultYaw, Pitch = 50f, Distance = DefaultDistance, MinDistance = 7f, MaxDistance = 22f;
        /// <summary>Distância pedida pela roda do mouse; a real chega nela suavemente.</summary>
        private float _wantDistance = DefaultDistance;
        private float _orbitYaw;
        private Vector3 _focus;
        private float _saveAt = -1f;
        /// <summary>Tempo de suavização do seguir (SmoothDamp) e do zoom.</summary>
        public float FollowSmoothTime = 0.15f, ZoomSmoothTime = 0.12f;
        private Vector3 _followVel;
        private float _zoomVel;

        private void Awake() => Load();

        /// <summary>Rotação e zoom do jogador ficam salvos (valem entre mapas e ao relogar). Só a tecla Home volta ao padrão.</summary>
        public void Load()
        {
            Yaw = PlayerPrefs.GetFloat(PrefYaw, DefaultYaw);
            _wantDistance = Distance = Mathf.Clamp(PlayerPrefs.GetFloat(PrefDist, DefaultDistance), MinDistance, MaxDistance);
        }

        public void ResetView()
        {
            Yaw = DefaultYaw; _wantDistance = DefaultDistance;
            SaveSoon();
        }

        private void SaveSoon() => _saveAt = Time.unscaledTime + 0.5f;

        private void LateUpdate()
        {
            if (_saveAt > 0 && Time.unscaledTime >= _saveAt)
            {
                _saveAt = -1f;
                PlayerPrefs.SetFloat(PrefYaw, Mathf.Repeat(Yaw, 360f)); PlayerPrefs.SetFloat(PrefDist, _wantDistance); PlayerPrefs.Save();
            }
            if (Target == null)
            {   // sem personagem (login): passeio cinematográfico com o seu próprio ângulo (não mexe na câmera do jogador)
                KezorLook.SetSeeThroughTarget(Vector3.zero, false);
                _orbitYaw += Time.deltaTime * 3f;
                float pitch = 20f + Mathf.Sin(Time.time * 0.11f) * 6f;
                var orbit = Quaternion.Euler(pitch, _orbitYaw, 0);
                transform.position = OrbitCenter - orbit * Vector3.forward * (40f + Mathf.Sin(Time.time * 0.07f) * 6f);
                transform.rotation = orbit;
                _focus = OrbitCenter;
                return;
            }
            _focus = Vector3.SmoothDamp(_focus, Target.position + Vector3.up * 1f, ref _followVel, FollowSmoothTime);
            float wheel = InputK.Scroll;
            if (Mathf.Abs(wheel) > 0.01f && !KezorGame.PointerOverUi)
            {
                _wantDistance = Mathf.Clamp(_wantDistance - wheel * 1.0f, MinDistance, MaxDistance);
                SaveSoon();
            }
            Distance = Mathf.SmoothDamp(Distance, _wantDistance, ref _zoomVel, ZoomSmoothTime);       // zoom suave
            if (InputK.MouseHeld(2)) { Yaw += InputK.MouseDeltaX * 4f; SaveSoon(); }
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            transform.position = _focus - rot * Vector3.forward * Distance;
            transform.rotation = rot;
            KezorLook.SetSeeThroughTarget(Target.position + Vector3.up * 1.1f, true); // muralha/casas/árvores na frente viram pontilhado
        }

        public void Snap() => SnapToTarget();

        /// <summary>Encaixa a câmera no herói sem suavização (entrar no mundo, trocar de mapa, teleporte).</summary>
        public void SnapToTarget()
        {
            if (Target == null) return;
            _focus = Target.position + Vector3.up;
            _followVel = Vector3.zero; _zoomVel = 0f;
            Distance = _wantDistance;
        }
    }
}
