using Kezor.Core.World;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Partículas de ambiente: vagalumes que acompanham o foco da câmera (Vale à noite, Pântano), brasas subindo dos
    /// braseiros e da forja, poeira de cripta. Usa o material de efeitos (emissivo → bloom).
    /// </summary>
    public sealed class AmbientFx : MonoBehaviour
    {
        private ParticleSystem? _follow;

        public static AmbientFx Build(Transform parent, MapDefinition map)
        {
            var go = new GameObject("Ambiente (partículas)");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<AmbientFx>();
            switch (map.Id)
            {
                case MapDefinition.SwampId:
                    fx._follow = Cloud(go.transform, "Vagalumes", KezorLook.Hex("#b8f07a"), 70, new Vector3(46, 3, 46), 0.09f, 0.6f, true);
                    break;
                case MapDefinition.CryptId:
                    fx._follow = Cloud(go.transform, "Poeira", KezorLook.Hex("#a8a0ff"), 40, new Vector3(34, 4, 34), 0.05f, 0.25f, false);
                    break;
                default:
                    fx._follow = Cloud(go.transform, "Vagalumes", KezorLook.Hex("#e8f07a"), 55, new Vector3(52, 2.6f, 52), 0.08f, 0.55f, true);
                    break;
            }
            return fx;
        }

        /// <summary>Brasas subindo de um fogo (braseiro, forja, fonte).</summary>
        public static ParticleSystem Embers(Transform parent, Vector3 pos, float radius = 0.3f, float rate = 7f)
        {
            var ps = MakePs(parent, "Brasas", pos);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(Emis(KezorLook.Hex("#ffb347")), Emis(KezorLook.Hex("#ff5a1a")));
            main.gravityModifier = -0.05f;
            main.maxParticles = 40;
            var em = ps.emission; em.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius;
            shape.rotation = new Vector3(-90, 0, 0);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.8f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            ps.Play();
            return ps;
        }

        private static Color Emis(Color c) { c.a = 0f; return c; }

        private static ParticleSystem MakePs(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.duration = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = GearFx.FxMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static ParticleSystem Cloud(Transform parent, string name, Color c, int count, Vector3 box, float size, float speed, bool blink)
        {
            var ps = MakePs(parent, name, Vector3.zero);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, speed * 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.2f);
            main.startColor = Emis(c);
            main.maxParticles = count * 2;
            var em = ps.emission; em.rateOverTime = count / 6f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = box;
            shape.position = new Vector3(0, box.y * 0.5f + 0.4f, 0);
            var noise = ps.noise; noise.enabled = true; noise.strength = speed; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            var curve = blink
                ? new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.15f, 1), new Keyframe(0.3f, 0.2f), new Keyframe(0.5f, 1), new Keyframe(0.7f, 0.3f), new Keyframe(0.85f, 1), new Keyframe(1, 0))
                : new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.3f, 1), new Keyframe(1, 0));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
            ps.Play();
            return ps;
        }

        private void LateUpdate()
        {
            if (_follow == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Mathf.Abs(ray.direction.y) < 0.05f) return;
            var focus = ray.origin + ray.direction * (-(ray.origin.y) / ray.direction.y);
            focus.y = WorldBuilder.Height(focus.x, focus.z);
            _follow.transform.position = focus;   // espaço do mundo: as que já nasceram ficam onde estão
        }
    }
}
