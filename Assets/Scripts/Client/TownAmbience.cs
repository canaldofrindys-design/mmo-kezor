using System.Collections.Generic;
using Kezor.Core.World;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Som ambiente da Vila de Brasalume, com gravações reais (CC0) em Resources/Audio/Ambiente: água corrente na
    /// Fonte de Brasas e no poço, martelo e fogo da forja, pássaros na praça do mercado, crepitar dos braseiros,
    /// cigarras à noite e, de vez em quando, um corvo longe. As fontes são 3D: o som cresce ao chegar perto de cada
    /// lugar da cidade. Fontes e licenças em docs/SONS_LICENCAS.md.
    /// </summary>
    public sealed class TownAmbience : MonoBehaviour
    {
        /// <summary>Volume geral do ambiente (0..1), separado dos efeitos de combate.</summary>
        public static float Volume = 0.7f;
        private static readonly Dictionary<string, AudioClip?> Clips = new Dictionary<string, AudioClip?>();
        private AudioSource? _crow;
        private float _crowTimer = 18f;
        private readonly System.Random _rnd = new System.Random(7);

        public static TownAmbience? Build(Transform parent, MapDefinition map)
        {
            if (map.Id != MapDefinition.ValeId) return null;
            var go = new GameObject("Som ambiente da vila");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<TownAmbience>();
            float cx = map.SafeX, cz = map.SafeZ;
            a.Spot("Fonte", new Vector3(cx, 1.2f, cz), "fonte", 0.5f, 4f, 26f);
            foreach (var p in map.Props)
            {
                var pos = new Vector3(p.X, 1.2f, p.Z);
                switch (p.Kind)
                {
                    case "Braseiro4": a.Spot("Braseiro", pos, "brasa", 0.3f, 1.5f, 9f); break;
                    case "Poco4": a.Spot("Poço", pos, "poco", 0.2f, 1.5f, 8f); break;
                }
            }
            foreach (var h in map.Houses)
                if (h.Kind == "Ferraria4")
                {   // a forja fica no telheiro lateral da ferraria
                    var (fx, fz) = MapDefinition.LocalToWorld(h.X, h.Z, h.Yaw, h.Scale, 6.8f, -0.9f);
                    a.Spot("Forja", new Vector3(fx, 1.2f, fz), "forja", 0.45f, 3f, 30f);
                }
            if (map.Squares.Count > 0)   // a primeira pracinha é o mercado
            {
                var m = map.Squares[0];
                a.Spot("Mercado", new Vector3(m.X, 1.5f, m.Z), "mercado", 0.35f, 5f, 28f);
            }
            a.Spot("Noite", Vector3.zero, "noite", 0.22f, 1f, 1f, spatial: false);
            a._crow = a.Spot("Corvo", Vector3.zero, "coruja", 0.2f, 1f, 1f, spatial: false, loop: false);
            return a;
        }

        private AudioSource? Spot(string name, Vector3 pos, string clipKey, float vol, float min, float max, bool spatial = true, bool loop = true)
        {
            var clip = Clip(clipKey);
            if (clip == null) return null;
            var go = new GameObject("Som: " + name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = loop; s.volume = vol * Volume; s.playOnAwake = false;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = min; s.maxDistance = max;
            s.dopplerLevel = 0f;
            if (loop) { s.time = (float)_rnd.NextDouble() * clip.length * 0.9f; s.Play(); }
            return s;
        }

        private void Update()
        {
            if (_crow == null) return;
            _crowTimer -= Time.deltaTime;
            if (_crowTimer <= 0f)
            {
                _crowTimer = 25f + (float)_rnd.NextDouble() * 35f;
                _crow.pitch = 0.92f + (float)_rnd.NextDouble() * 0.16f;
                _crow.panStereo = (float)_rnd.NextDouble() * 1.4f - 0.7f;
                _crow.Play();
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
