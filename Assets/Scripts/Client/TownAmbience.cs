using System.Collections.Generic;
using Kezor.Core.World;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Som ambiente da Vila de Brasalume, com loops longos de estúdio (Oculus Audio Pack) em Resources/Audio/Ambiente:
    /// um fundo de natureza que acompanha a luz do Vale (grilos e floresta à noite, vento e pássaros de dia), o córrego
    /// da Fonte de Brasas, a água do poço, o martelo na bigorna da forja e o burburinho da praça do mercado. Os lugares
    /// são fontes 3D com queda suave: o som cresce ao chegar perto. De dia, de vez em quando, um pássaro canta longe.
    /// Tudo passa pelo canal de ambiente do <see cref="KezorAudio"/> e abaixa um pouco durante a luta.
    /// Fontes e licenças em docs/SONS_LICENCAS.md.
    /// </summary>
    public sealed class TownAmbience : MonoBehaviour
    {
        private static readonly Dictionary<string, AudioClip?> Clips = new Dictionary<string, AudioClip?>();
        private readonly List<(AudioSource src, float vol)> _spots = new List<(AudioSource, float)>();
        private AudioSource? _bed, _bird;
        private bool _bedIsDay;
        private float _birdTimer = 12f;
        private AudioClip?[] _birds = new AudioClip?[0];
        private readonly System.Random _rnd = new System.Random(7);
        private const float BedVol = 0.9f;

        public static TownAmbience? Build(Transform parent, MapDefinition map)
        {
            if (map.Id != MapDefinition.ValeId) return null;
            var go = new GameObject("Som ambiente da vila");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<TownAmbience>();
            float cx = map.SafeX, cz = map.SafeZ;
            a.Spot("Fonte", new Vector3(cx, 1.2f, cz), "fonte", 0.9f, 3f, 24f);
            foreach (var p in map.Props)
                if (p.Kind == "Poco4") a.Spot("Poço", new Vector3(p.X, 1.2f, p.Z), "poco", 0.7f, 1.5f, 9f);
            foreach (var h in map.Houses)
                if (h.Kind == "Ferraria4")
                {   // a forja fica no telheiro lateral da ferraria
                    var (fx, fz) = MapDefinition.LocalToWorld(h.X, h.Z, h.Yaw, h.Scale, 6.8f, -0.9f);
                    a.Spot("Forja", new Vector3(fx, 1.2f, fz), "forja", 0.75f, 3f, 26f);
                }
            if (map.Squares.Count > 0)   // a primeira pracinha é o mercado
            {
                var m = map.Squares[0];
                a.Spot("Mercado", new Vector3(m.X, 1.5f, m.Z), "mercado", 1f, 5f, 26f);
            }
            a._bed = a.Source("Fundo", Vector3.zero, null, false, true);
            a.SetBed(KezorLook.Day);
            a._bird = a.Source("Pássaro", Vector3.zero, null, false, false);
            var birds = new List<AudioClip?>();
            for (int i = 0; i < 8; i++) { var c = Resources.Load<AudioClip>("Audio/Sfx/passaro_" + i); if (c == null) break; birds.Add(c); }
            a._birds = birds.ToArray();
            return a;
        }

        private void Spot(string name, Vector3 pos, string clipKey, float vol, float min, float max)
        {
            var clip = Clip(clipKey);
            if (clip == null) return;
            var s = Source(name, pos, clip, true, true);
            s.minDistance = min; s.maxDistance = max;
            s.time = (float)_rnd.NextDouble() * clip.length * 0.9f;   // cada lugar começa num ponto diferente do loop
            s.Play();
            _spots.Add((s, vol));
        }

        private AudioSource Source(string name, Vector3 pos, AudioClip? clip, bool spatial, bool loop)
        {
            var go = new GameObject("Som: " + name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = loop; s.playOnAwake = false; s.dopplerLevel = 0f;
            s.spatialBlend = spatial ? 1f : 0f;
            // queda suave e própria (a curva logarítmica do Unity some cedo demais com distâncias curtas como estas)
            s.rolloffMode = AudioRolloffMode.Custom;
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.25f, 0.7f), new Keyframe(0.6f, 0.25f), new Keyframe(1f, 0f)));
            s.spread = spatial ? 60f : 0f;   // largura: a fonte não vira um ponto minúsculo
            s.volume = 0f;
            return s;
        }

        /// <summary>Troca o fundo entre noite (padrão do Vale) e dia (comando /dia), com o loop começando num ponto ao acaso.</summary>
        private void SetBed(bool day)
        {
            if (_bed == null) return;
            _bedIsDay = day;
            var clip = Clip(day ? "dia" : "noite");
            if (clip == null) return;
            _bed.clip = clip;
            _bed.time = (float)_rnd.NextDouble() * clip.length * 0.9f;
            _bed.Play();
        }

        private void Update()
        {
            float bus = KezorAudio.Ambient * KezorAudio.AmbientDuck;
            foreach (var (src, vol) in _spots) if (src != null) src.volume = vol * bus;
            if (_bed != null)
            {
                if (_bedIsDay != KezorLook.Day) SetBed(KezorLook.Day);
                _bed.volume = BedVol * bus;
            }
            if (_bird == null || _birds.Length == 0 || !KezorLook.Day) return;
            _birdTimer -= Time.deltaTime;
            if (_birdTimer <= 0f)
            {
                _birdTimer = 14f + (float)_rnd.NextDouble() * 22f;
                _bird.clip = _birds[_rnd.Next(_birds.Length)];
                _bird.volume = 0.6f * bus;
                _bird.pitch = 0.95f + (float)_rnd.NextDouble() * 0.1f;
                _bird.panStereo = (float)_rnd.NextDouble() * 1.2f - 0.6f;
                _bird.Play();
            }
        }

        private static AudioClip? Clip(string key)
        {
            if (Clips.TryGetValue(key, out var c) && c != null) return c;
            c = Resources.Load<AudioClip>("Audio/Ambiente/" + key);
            if (c == null) Debug.LogWarning("[Kezor] som ambiente sem arquivo em Resources/Audio/Ambiente: " + key);
            return Clips[key] = c;
        }
    }
}
