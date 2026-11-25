using UnityEngine;
using Kezor.Core.Net;

namespace Kezor.Client
{
    /// <summary>Representação visual de uma entidade do servidor: interpola posição e anima o rig proceduralmente.</summary>
    public sealed class EntityView : MonoBehaviour
    {
        public int Id;
        public EntityKind Kind;
        [System.NonSerialized] public Rig Rig = null!;
        public Vector3 Target;
        public int TypeId, WeaponId;
        public string Look = "";
        public float Height => Rig.Height;
        public bool Dying { get; private set; }

        private float _walk, _speed, _attackT = 1f, _hitT = 1f, _dieT, _phase;
        private Vector3 _lastPos;
        private Vector3? _face;
        private MaterialPropertyBlock? _mpb;
        /// <summary>Animator opcional (modelo com clipes): recebe "Speed"; sem ele, vale a animação procedural.</summary>
        private Animator? _anim;
        private bool _animHasSpeed;
        private static readonly int SpeedId = Animator.StringToHash("Speed");

        public static EntityView Create(Rig rig, int id, EntityKind kind, Vector3 pos)
        {
            var v = rig.Root.gameObject.AddComponent<EntityView>();
            v.Rig = rig; v.Id = id; v.Kind = kind;
            v.transform.position = v.Target = v._lastPos = pos;
            v._phase = Random.value * 10f;
            v._anim = rig.Root.GetComponentInChildren<Animator>();
            if (v._anim != null && v._anim.runtimeAnimatorController != null)
                foreach (var prm in v._anim.parameters) if (prm.nameHash == SpeedId && prm.type == AnimatorControllerParameterType.Float) v._animHasSpeed = true;
            return v;
        }

        /// <summary>
        /// Remontagem do visual (troca de equipamento): a vista nova herda posição já interpolada, direção, passada,
        /// velocidade e fase da antiga, para o herói não pular, girar nem reiniciar a animação.
        /// </summary>
        public void InheritFrom(EntityView old)
        {
            transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
            Target = old.Target; _lastPos = old._lastPos;
            _walk = old._walk; _speed = old._speed; _phase = old._phase; _face = old._face;
            _attackT = old._attackT; _hitT = old._hitT;
        }

        public void PlayAttack(Vector3 towards) { _attackT = 0; _face = towards; Rig.OnAttack?.Invoke(); }
        public void PlayHit() { _hitT = 0; }
        public void PlayDeath() { Dying = true; _dieT = 0; }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Dying)
            {
                _dieT += dt;
                Rig.Body.localRotation = Quaternion.Euler(Mathf.Min(90f, _dieT * 240f), 0, 0);
                transform.position += Vector3.down * dt * (_dieT > 0.5f ? 1.2f : 0f);
                if (_dieT > 1.6f) Destroy(gameObject);
                return;
            }

            // Interpolação suave até a posição do servidor (snapshots a 10 Hz)
            var p = transform.position;
            var dist = Vector3.Distance(p, Target);
            p = dist > 8f ? Target : Vector3.Lerp(p, Target, 1f - Mathf.Exp(-12f * dt));
            transform.position = p;
            var delta = p - _lastPos; delta.y = 0;
            _speed = Mathf.Lerp(_speed, delta.magnitude / Mathf.Max(dt, 0.0001f), 1f - Mathf.Exp(-10f * dt));
            _lastPos = p;

            if (Kind != EntityKind.Drop)
            {
                Vector3 look = Vector3.zero;
                if (_face.HasValue && _attackT < 0.6f) look = _face.Value - p;
                else if (delta.sqrMagnitude > 0.0004f) look = delta;
                look.y = 0;
                if (look.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-(Id == KezorGame.MyIdForViews ? 22f : 14f) * dt));
            }
            if (_animHasSpeed && _anim != null) { _anim.SetFloat(SpeedId, _speed, 0.1f, dt); return; }   // modelo com Animator
            Animate(dt);
        }

        private void Animate(float dt)
        {
            float t = Time.time + _phase;
            if (Kind == EntityKind.Drop)
            {
                Rig.Body.localRotation = Quaternion.Euler(0, t * 90f, 0);
                Rig.Body.localPosition = new Vector3(0, 0.1f + Mathf.Sin(t * 3f) * 0.08f, 0);
                return;
            }
            bool moving = _speed > 0.4f;
            _walk += dt * (moving ? Mathf.Clamp(_speed * 1.8f, 4f, 12f) : 0f);
            float swing = moving ? Mathf.Sin(_walk) * 35f : 0f;
            if (Rig.Seated)
            {   // montado: pernas abertas em volta da sela, sem passada
                if (Rig.LegL != null) Rig.LegL.localRotation = Quaternion.Euler(-72f, 0, 28f);
                if (Rig.LegR != null) Rig.LegR.localRotation = Quaternion.Euler(-72f, 0, -28f);
            }
            else
            {
                if (Rig.LegL != null) Rig.LegL.localRotation = Quaternion.Euler(swing, 0, 0);
                if (Rig.LegR != null) Rig.LegR.localRotation = Quaternion.Euler(-swing, 0, 0);
            }
            if (Rig.WingL != null && Rig.WingR != null)
            {   // asas: batida lenta parado, mais forte andando
                float f = Mathf.Sin(t * (moving ? 7f : 2.4f)) * (moving ? 26f : 10f);
                Rig.WingL.localRotation = Quaternion.Euler(0, -18f, f + 6f);
                Rig.WingR.localRotation = Quaternion.Euler(0, 18f, -f - 6f);
            }
            for (int i = 0; i < Rig.ExtraLegs.Count; i++)
            {
                var leg = Rig.ExtraLegs[i];
                if (Rig.Hover) { float a = t * 1.5f + i * 2.094f; leg.localPosition = new Vector3(Mathf.Cos(a) * 1.1f, 1.6f + Mathf.Sin(t * 3 + i) * 0.2f, Mathf.Sin(a) * 1.1f); continue; }
                float s = moving ? Mathf.Sin(_walk + i * 1.7f) * 25f : 0f;
                var e = leg.localRotation.eulerAngles;
                leg.localRotation = Quaternion.Euler(s, e.y, e.z);
            }

            _attackT += dt;
            float atk = _attackT < 0.35f ? Mathf.Sin(_attackT / 0.35f * Mathf.PI) : 0f;
            float idleArm = Mathf.Sin(t * 2f) * 3f;
            if (Rig.ArmR != null) Rig.ArmR.localRotation = Quaternion.Euler(-swing * 0.6f - atk * 110f + idleArm, 0, 0);
            if (Rig.ArmL != null) Rig.ArmL.localRotation = Quaternion.Euler(swing * 0.6f - atk * 20f - idleArm, 0, 0);

            var bodyPos = Vector3.zero;
            var bodyScale = Vector3.one;
            if (Rig.Bouncy)
            {
                float k = Mathf.Abs(Mathf.Sin(t * (moving ? 7f : 3f)));
                bodyPos.y = k * (moving ? 0.35f : 0.08f);
                bodyScale = new Vector3(1f + (1 - k) * 0.12f, 1f - (1 - k) * 0.15f + atk * 0.3f, 1f + (1 - k) * 0.12f);
            }
            else if (Rig.Hover) bodyPos.y = 0.4f + Mathf.Sin(t * 1.6f) * 0.15f;
            else if (moving) bodyPos.y = Mathf.Abs(Mathf.Sin(_walk)) * (Rig.Seated ? 0.1f : 0.06f);
            bodyPos.z = atk * 0.25f;
            Rig.Body.localPosition = Rig.BodyBase + bodyPos;
            if (Rig.Bouncy) Rig.Body.localScale = bodyScale * _baseScale;

            _hitT += dt;
            float flash = _hitT < 0.15f ? 0.65f * (1f - _hitT / 0.15f) : 0f;
            SetFlash(flash);
        }

        private float _baseScale = 1f;
        private void Start() { _baseScale = Rig.Body.localScale.x; }

        private float _lastFlash = -1;
        private void SetFlash(float v)
        {
            if (Mathf.Approximately(v, _lastFlash)) return;
            _lastFlash = v;
            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in Rig.Renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);            // preserva o brilho de runa (_Glow) posto pelo GearFx
                _mpb.SetFloat(KezorLook.FlashId, v);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
