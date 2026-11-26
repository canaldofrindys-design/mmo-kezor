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
            // heróis e NPCs humanos: esqueleto com pele e animações feitas no Blender (Anim/Heroi)
            if (kind == EntityKind.Player && rig.ArmR != null && rig.LegL != null && !rig.Bouncy && !rig.Hover && KezorAnim.Hero is KezorAnim ka)
            {
                try { if (SkinRig.Build(rig, ka)) v._clips = ka; }
                catch (System.Exception e) { Debug.LogWarning("[Kezor] esqueleto do herói falhou: " + e.Message); }
            }
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
            _gait = old._gait; _idleT = old._idleT; _flyT = old._flyT; _fly = old._fly; _move = old._move; _atkClip = old._atkClip;
        }

        public void PlayAttack(Vector3 towards)
        {
            _attackT = 0; _face = towards; Rig.OnAttack?.Invoke();
            _atkClip = IsCaster(WeaponId) ? "Cast" : (_atkClip == "Slash1" ? "Slash2" : "Slash1");   // golpes alternados
        }

        /// <summary>Cajados, varinhas, arcos, tomos e sinos: o golpe é o gesto de magia/disparo.</summary>
        private static bool IsCaster(int w) => w is 10 or 11 or 12 or 41 or 71 or 20 or 21 or 22 or 42 or 72 or 56 or 57 or 58 or 45 or 75;
        public void PlayHit() { _hitT = 0; }
        public void PlayDeath() { Dying = true; _dieT = 0; }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Dying && _clips != null && Rig.Bones != null && _clips.Clips.TryGetValue("Death", out var dc))
            {   // morte animada: cai de costas, fica no chão e afunda
                _dieT += dt;
                KezorAnim.Sample(dc, _dieT / dc.Duration, Rig.MirrorX, _qb, out var dh);
                KezorAnim.Blend(_qa, _hip, _qb, dh, Mathf.Clamp01(_dieT / 0.15f), out _hip);
                ApplyPose(_hip);
                if (_dieT > 1.7f) transform.position += Vector3.down * dt * 0.8f;
                if (_dieT > 2.8f) Destroy(gameObject);
                return;
            }
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
            float run = Mathf.Clamp01((_speed - 0.4f) / 4.5f);                 // 0 parado .. 1 correndo
            _move = Mathf.Lerp(_move, moving ? 1f : 0f, 1f - Mathf.Exp(-8f * dt));
            // Asas só levantam voo fora da vila/zonas seguras; dentro delas o herói pousa e anda no chão.
            var here = transform.position; var map = WorldBuilder.Current;
            bool safe = map != null && map.InSafeZone(here.x, here.z);
            _fly = Mathf.Lerp(_fly, Rig.Flying && !Rig.Seated && !safe ? 1f : 0f, 1f - Mathf.Exp(-3f * dt));
            _walk += dt * (moving ? Mathf.Clamp(_speed * 1.9f, 4f, 13f) : 0f);
            _attackT += dt;
            bool humanoid = Rig.ArmR != null && Rig.LegL != null && !Rig.Bouncy && !Rig.Hover;

            if (humanoid)
            {
                if (_clips != null && Rig.Bones != null) AnimateClips(dt, t);
                else AnimateHumanoid(dt, t, run);
                _hitT += dt;
                SetFlash(_hitT < 0.15f ? 0.65f * (1f - _hitT / 0.15f) : 0f);
                return;
            }

            float swing = moving ? Mathf.Sin(_walk) * 35f : 0f;
            if (Rig.LegL != null) Rig.LegL.localRotation = Quaternion.Euler(swing, 0, 0);
            if (Rig.LegR != null) Rig.LegR.localRotation = Quaternion.Euler(-swing, 0, 0);
            if (Rig.WingL != null && Rig.WingR != null)
            {   // asas de monstro (mariposa): batida lenta parado, mais forte andando
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
            else if (moving) bodyPos.y = Mathf.Abs(Mathf.Sin(_walk)) * 0.06f;
            bodyPos.z = atk * 0.25f;
            Rig.Body.localPosition = Rig.BodyBase + bodyPos;
            if (Rig.Bouncy) Rig.Body.localScale = bodyScale * _baseScale;

            _hitT += dt;
            float flash = _hitT < 0.15f ? 0.65f * (1f - _hitT / 0.15f) : 0f;
            SetFlash(flash);
        }

        private float _move, _fly, _glide;

        /// <summary>
        /// Herói, NPC e humanoides (estilo MMO clássico): respiração parado, corrida com o tronco inclinado e braços em
        /// contraposição, golpe em três tempos (prepara, corta, volta) com giro do tronco e avanço; com asas, voo baixo:
        /// flutua ~0,7 m, o corpo deita para a frente conforme a velocidade, pernas soltas para trás, braços abertos e
        /// as asas alternam batidas fortes e planeio.
        /// </summary>
        private void AnimateHumanoid(float dt, float t, float run)
        {
            float mv = _move, fl = _fly;
            float ph = _walk;
            // ---- golpe: 0..0,12 prepara (braço sobe atrás), 0,12..0,28 corta, 0,28..0,5 volta
            float a = _attackT, wind = 0f, strike = 0f;
            if (a < 0.12f) wind = Mathf.SmoothStep(0, 1, a / 0.12f);
            else if (a < 0.28f) { wind = 1f - (a - 0.12f) / 0.16f; strike = Mathf.Sin((a - 0.12f) / 0.16f * Mathf.PI * 0.5f); }
            else if (a < 0.5f) strike = 1f - Mathf.SmoothStep(0, 1, (a - 0.28f) / 0.22f);
            float atkAny = Mathf.Max(wind, strike);

            // ---- chão: passada, contraposição, respiração
            float stride = Mathf.Lerp(28f, 46f, run) * mv * (1f - fl);
            float legS = Mathf.Sin(ph) * stride;
            float breathe = Mathf.Sin(t * 1.9f);
            float armSwing = -Mathf.Sin(ph) * Mathf.Lerp(22f, 40f, run) * mv * (1f - fl);
            float lean = (Mathf.Lerp(4f, 11f, run) * mv) * (1f - fl);
            float bob = Mathf.Abs(Mathf.Sin(ph)) * Mathf.Lerp(0.04f, 0.09f, run) * mv * (1f - fl);

            // ---- voo
            _glide += dt * (mv > 0.5f ? 1f : 0.45f);
            float cyc = Mathf.Repeat(_glide, 2.2f);                        // ciclo: 2 batidas fortes e planeio
            bool flapping = mv < 0.5f || cyc < 1.0f;
            float flapF = flapping ? Mathf.Sin(t * (mv > 0.5f ? 11f : 4.2f)) : Mathf.Sin(t * 1.3f) * 0.15f;
            float flyLean = Mathf.Lerp(6f, 38f, run * mv);                  // deita mais quanto mais rápido
            float hover = 0.7f + Mathf.Sin(t * 2.1f) * 0.08f + (flapping ? Mathf.Max(0f, flapF) * 0.05f : 0f);

            float pitch = (lean * (1f - fl) + flyLean * fl) * (Rig.Seated ? 0.3f : 1f);
            float twist = (wind * -22f + strike * 18f);
            var rot = Quaternion.Euler(pitch + strike * 6f, twist, Mathf.Sin(ph) * 2.5f * mv * (1f - fl));
            var pivot = new Vector3(0, 1.0f, 0);                             // gira em volta do quadril, não dos pés
            var pos = Rig.BodyBase + pivot - rot * pivot;
            pos.y += bob + hover * fl + breathe * 0.006f * (1f - mv);
            pos += rot * new Vector3(0, 0, strike * 0.22f);                  // avanço no golpe
            Rig.Body.localPosition = pos;
            Rig.Body.localRotation = rot;

            for (int i = 0; i < Rig.ExtraLegs.Count; i++)   // montaria: as patas é que andam
            {
                var leg = Rig.ExtraLegs[i];
                float s = mv > 0.1f ? Mathf.Sin(ph + i * 1.7f) * 25f * mv : 0f;
                var e = leg.localRotation.eulerAngles;
                leg.localRotation = Quaternion.Euler(s, e.y, e.z);
            }
            // pernas
            if (Rig.Seated)
            {
                Rig.LegL!.localRotation = Quaternion.Euler(-72f, 0, 28f);
                Rig.LegR!.localRotation = Quaternion.Euler(-72f, 0, -28f);
            }
            else
            {
                float trail = fl * (Mathf.Lerp(8f, 34f, run * mv));          // pernas soltas para trás no voo
                float dangle = fl * Mathf.Sin(t * 1.7f) * 6f;
                Rig.LegL!.localRotation = Quaternion.Euler(legS + trail + dangle, 0, fl * 4f);
                Rig.LegR!.localRotation = Quaternion.Euler(-legS + trail * 0.8f - dangle, 0, -fl * 4f);
            }

            // braços: contraposição; no voo abertos e para trás; golpe no braço direito
            float idle = breathe * 2.5f * (1f - mv);
            float flyArmX = fl * Mathf.Lerp(-10f, 28f, run * mv), flyArmZ = fl * 22f;
            float baseR = armSwing + idle + flyArmX, rx;
            if (a < 0.12f) rx = Mathf.Lerp(baseR, -165f, wind);                               // prepara: braço acima da cabeça
            else if (a < 0.28f) rx = Mathf.Lerp(-165f, -35f, 1f - (1f - (a - 0.12f) / 0.16f) * (1f - (a - 0.12f) / 0.16f));   // corta
            else if (a < 0.5f) rx = Mathf.Lerp(baseR, -35f, strike);                             // volta
            else rx = baseR;
            Rig.ArmR!.localRotation = Quaternion.Euler(rx, 0, -flyArmZ - atkAny * 8f - 3f);
            Rig.ArmL!.localRotation = Quaternion.Euler(-armSwing - idle + flyArmX - atkAny * 25f, 0, flyArmZ + 3f + atkAny * 10f);
            if (Rig.Head != null) Rig.Head.localRotation = Quaternion.Euler(-pitch * 0.55f + breathe * 1.2f * (1f - mv), -twist * 0.5f, 0);

            AnimateWings(t, mv, fl, flapping, flapF);
        }

        private void AnimateWings(float t, float mv, float fl, bool flapping, float flapF)
        {
            // asas do herói: abertas e firmes; batida forte voando, lenta parado no chão; planeio aberto
            if (Rig.WingL != null && Rig.WingR != null)
            {
                float open = 10f + fl * 8f;
                float amp = fl > 0.5f ? (flapping ? (mv > 0.5f ? 34f : 18f) : 4f) : (mv > 0.5f ? 20f : 9f);
                float f = flapF * amp;
                float sweep = -12f - fl * mv * 14f;                           // varrida para trás no voo rápido
                Rig.WingL.localRotation = Quaternion.Euler(fl * 6f, sweep, f + open);
                Rig.WingR.localRotation = Quaternion.Euler(fl * 6f, -sweep, -f - open);
            }
        }

        // ================================================================ animações do Blender (KezorAnim)
        private KezorAnim? _clips;
        private float _gait, _idleT, _flyT;
        private string _atkClip = "Slash1";
        private Vector3 _hip;
        private readonly Quaternion[] _qa = new Quaternion[KezorAnim.Count], _qb = new Quaternion[KezorAnim.Count], _qc = new Quaternion[KezorAnim.Count];
        private readonly float[] _mask = new float[KezorAnim.Count];
        private const float AttackTime = 0.55f;

        private static float Smooth01(float a, float b, float x) { float k = Mathf.Clamp01((x - a) / (b - a)); return k * k * (3 - 2 * k); }

        /// <summary>
        /// Mistura os clipes: parado ↔ andar ↔ correr (fase sincronizada com a distância, pé sem deslizar), chão ↔ voo
        /// (pouso e decolagem suaves), golpe no tronco e braços por cima da corrida e montaria com as pernas na sela.
        /// </summary>
        private void AnimateClips(float dt, float t)
        {
            var A = _clips!;
            float sx = Rig.MirrorX, sc = Rig.BoneScale;
            float spd = _speed / Mathf.Max(0.3f, sc);
            float runW = Smooth01(1.6f, 3.4f, spd);
            float sw = A.Stride.TryGetValue("Walk", out var a1) ? a1 : 1f, sr = A.Stride.TryGetValue("Run", out var a2) ? a2 : 3.1f;
            _gait += dt * spd / Mathf.Lerp(sw, sr, runW);
            _idleT += dt; _flyT += dt;
            float mv = _move, fl = _fly;

            Vector3 hA, hB, hC;
            var idle = A.Clips["Idle"];
            KezorAnim.Sample(idle, _idleT / idle.Duration, sx, _qa, out hA);
            if (mv > 0.001f)
            {
                KezorAnim.Sample(A.Clips["Walk"], _gait, sx, _qb, out hB);
                KezorAnim.Sample(A.Clips["Run"], _gait, sx, _qc, out hC);
                KezorAnim.Blend(_qb, hB, _qc, hC, runW, out hB);
                KezorAnim.Blend(_qa, hA, _qb, hB, mv, out hA);
            }
            if (fl > 0.001f)
            {
                var fi = A.Clips["FlyIdle"]; var fm = A.Clips["FlyMove"];
                KezorAnim.Sample(fi, _flyT / fi.Duration, sx, _qb, out hB);
                KezorAnim.Sample(fm, _flyT / fm.Duration, sx, _qc, out hC);
                KezorAnim.Blend(_qb, hB, _qc, hC, mv, out hB);
                KezorAnim.Blend(_qa, hA, _qb, hB, fl, out hA);
            }
            if (_attackT < AttackTime && A.Clips.TryGetValue(_atkClip, out var atk))
            {
                float u = _attackT / AttackTime;
                float w = Smooth01(0f, 0.06f, _attackT) * (1f - Smooth01(0.78f, 1f, u));
                KezorAnim.Sample(atk, u, sx, _qb, out hB);
                float lower = (1f - mv) * (1f - fl) * (Rig.Seated ? 0f : 1f);   // correndo/voando, as pernas seguem o movimento
                for (int i = 0; i < KezorAnim.Count; i++) _mask[i] = i >= KezorAnim.Chest && i <= KezorAnim.HandR ? 1f : lower;
                KezorAnim.Blend(_qa, hA, _qb, hB, w, out hA, _mask);
            }
            if (Rig.Seated)
            {   // montado: quadril parado na sela, coxas para a frente e abertas, canelas penduradas
                hA = Vector3.zero;
                _qa[KezorAnim.Body] = Quaternion.Slerp(Quaternion.identity, _qa[KezorAnim.Body], 0.3f);
                _qa[KezorAnim.LegL] = Quaternion.Euler(0, sx * 14f, 0) * Quaternion.Euler(-78f, 0, 0);
                _qa[KezorAnim.LegR] = Quaternion.Euler(0, -sx * 14f, 0) * Quaternion.Euler(-78f, 0, 0);
                _qa[KezorAnim.ShinL] = _qa[KezorAnim.ShinR] = Quaternion.Euler(-8f, 0, 0);
                _qa[KezorAnim.FootL] = _qa[KezorAnim.FootR] = Quaternion.Euler(10f, 0, 0);
                for (int i = 0; i < Rig.ExtraLegs.Count; i++)
                {
                    var leg = Rig.ExtraLegs[i];
                    float s = mv > 0.1f ? Mathf.Sin(_walk + i * 1.7f) * 25f * mv : 0f;
                    var e = leg.localRotation.eulerAngles;
                    leg.localRotation = Quaternion.Euler(s, e.y, e.z);
                }
            }
            _hip = hA;
            ApplyPose(hA);

            // asas: mesmo ritmo do voo procedural
            _glide += dt * (mv > 0.5f ? 1f : 0.45f);
            float cyc = Mathf.Repeat(_glide, 2.2f);
            bool flapping = mv < 0.5f || cyc < 1.0f;
            float flapF = flapping ? Mathf.Sin(t * (mv > 0.5f ? 11f : 4.2f)) : Mathf.Sin(t * 1.3f) * 0.15f;
            AnimateWings(t, mv, fl, flapping, flapF);
        }

        /// <summary>Aplica a pose: rotação de cada osso no espaço da raiz × repouso; o Body gira em volta do quadril.</summary>
        private void ApplyPose(Vector3 hip)
        {
            var bones = Rig.Bones!; var rest = Rig.BoneRest!;
            var rr = Rig.Root.rotation;
            for (int i = 0; i < KezorAnim.Count; i++) bones[i].rotation = rr * _qa[i] * rest[i];
            var pivot = new Vector3(0, Rig.HipPivot, 0);
            Rig.Body.localPosition = Rig.BodyBase + pivot + hip * Rig.BoneScale - _qa[KezorAnim.Body] * pivot;
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
