using System.Collections.Generic;
using Kezor.Core.World;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>
    /// Som ambiente da Vila de Brasalume, sintetizado em código (sem arquivos de áudio): água da Fonte de Brasas,
    /// martelo e fogo da forja, burburinho do mercado, crepitar dos braseiros, vento e grilos da noite e, de vez em
    /// quando, uma coruja longe. As fontes são 3D: o som cresce ao chegar perto de cada lugar da cidade.
    /// </summary>
    public sealed class TownAmbience : MonoBehaviour
    {
        private const int Rate = 22050;
        /// <summary>Volume geral do ambiente (0..1), separado dos efeitos de combate.</summary>
        public static float Volume = 0.7f;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private AudioSource? _night, _owl;
        private float _owlTimer = 18f;
        private readonly System.Random _rnd = new System.Random(7);

        public static TownAmbience? Build(Transform parent, MapDefinition map)
        {
            if (map.Id != MapDefinition.ValeId) return null;
            var go = new GameObject("Som ambiente da vila");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<TownAmbience>();
            float cx = map.SafeX, cz = map.SafeZ;
            a.Spot("Fonte", new Vector3(cx, 1.2f, cz), Clip("fonte", Fountain), 0.55f, 4f, 26f);
            foreach (var p in map.Props)
            {
                var pos = new Vector3(p.X, 1.2f, p.Z);
                switch (p.Kind)
                {
                    case "Forja2": a.Spot("Forja", pos, Clip("forja", Forge), 0.6f, 3f, 30f); break;
                    case "Braseiro2": a.Spot("Braseiro", pos, Clip("brasa", Crackle), 0.22f, 1.5f, 9f); break;
                    case "Poco": a.Spot("Poço", pos, Clip("poco", Well), 0.18f, 1.5f, 8f); break;
                }
            }
            if (map.Squares.Count > 0)   // a primeira pracinha é o mercado
            {
                var m = map.Squares[0];
                a.Spot("Mercado", new Vector3(m.X, 1.5f, m.Z), Clip("mercado", Murmur), 0.4f, 5f, 28f);
            }
            a._night = a.Spot("Noite", Vector3.zero, Clip("noite", Night), 0.32f, 1f, 1f, spatial: false);
            a._owl = a.Spot("Coruja", Vector3.zero, Clip("coruja", Owl), 0.25f, 1f, 1f, spatial: false, loop: false);
            return a;
        }

        private AudioSource Spot(string name, Vector3 pos, AudioClip clip, float vol, float min, float max, bool spatial = true, bool loop = true)
        {
            var go = new GameObject("Som: " + name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = loop; s.volume = vol * Volume; s.playOnAwake = false;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = min; s.maxDistance = max;
            s.dopplerLevel = 0f;
            if (loop) { s.time = (float)_rnd.NextDouble() * clip.length; s.Play(); }
            return s;
        }

        private void Update()
        {
            if (_owl == null) return;
            _owlTimer -= Time.deltaTime;
            if (_owlTimer <= 0f)
            {
                _owlTimer = 25f + (float)_rnd.NextDouble() * 35f;
                _owl.pitch = 0.92f + (float)_rnd.NextDouble() * 0.16f;
                _owl.panStereo = (float)_rnd.NextDouble() * 1.4f - 0.7f;
                _owl.Play();
            }
        }

        // ---------------------------------------------------------------- síntese
        private delegate float[] Synth(System.Random r);

        private static AudioClip Clip(string key, Synth synth)
        {
            if (Clips.TryGetValue(key, out var c) && c != null) return c;
            var data = synth(new System.Random(key.GetHashCode()));
            LoopFade(data, Rate / 10);
            c = AudioClip.Create("kz_" + key, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return Clips[key] = c;
        }

        /// <summary>Cruza o fim com o começo para o loop não estalar.</summary>
        private static void LoopFade(float[] d, int n)
        {
            n = Mathf.Min(n, d.Length / 4);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                d[i] = d[i] * t + d[d.Length - n + i] * (1f - t);
            }
            System.Array.Resize(ref d, d.Length);
        }

        private static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        /// <summary>Água caindo: ruído rosado com bolhas (senos curtos de altura aleatória).</summary>
        private static float[] Fountain(System.Random r)
        {
            int n = Rate * 6; var d = new float[n];
            float b0 = 0, b1 = 0, b2 = 0;
            for (int i = 0; i < n; i++)
            {
                float w = Noise(r);
                b0 = 0.997f * b0 + w * 0.029f; b1 = 0.985f * b1 + w * 0.032f; b2 = 0.95f * b2 + w * 0.048f;
                d[i] = (b0 + b1 + b2 + w * 0.02f) * 0.55f;
            }
            for (int k = 0; k < 140; k++)
            {
                int at = r.Next(n - 2000); float f = 500f + (float)r.NextDouble() * 900f, a = 0.05f + (float)r.NextDouble() * 0.06f;
                for (int i = 0; i < 1400; i++) { float t = i / (float)Rate; d[at + i] += Mathf.Sin(2 * Mathf.PI * (f + 2600f * t) * t) * a * Mathf.Exp(-t * 60f); }
            }
            return d;
        }

        /// <summary>Forja: fogo (ruído grave) e marteladas na bigorna com parciais metálicas.</summary>
        private static float[] Forge(System.Random r)
        {
            int n = Rate * 6; var d = Crackle(r);
            System.Array.Resize(ref d, n);
            float lp = 0;
            for (int i = 0; i < n; i++) { lp += (Noise(r) - lp) * 0.02f; d[i] = d[i] * 0.6f + lp * 0.5f; }
            float[] parts = { 1f, 2.76f, 5.4f, 8.93f };
            for (float t0 = 0.3f; t0 < 5.6f; t0 += 0.75f + (float)r.NextDouble() * 0.45f)
            {
                int at = (int)(t0 * Rate); float f = 820f + (float)r.NextDouble() * 60f;
                for (int i = 0; i < Rate / 2 && at + i < n; i++)
                {
                    float t = i / (float)Rate, s = 0;
                    for (int p = 0; p < parts.Length; p++) s += Mathf.Sin(2 * Mathf.PI * f * parts[p] * t) * Mathf.Exp(-t * (9f + p * 7f)) / (p + 1);
                    d[at + i] += s * 0.32f + (i < 60 ? Noise(r) * 0.3f : 0f);
                }
            }
            return d;
        }

        /// <summary>Crepitar de fogo: estalos curtos aleatórios sobre um chiado baixo.</summary>
        private static float[] Crackle(System.Random r)
        {
            int n = Rate * 4; var d = new float[n];
            float hp = 0, prev = 0;
            for (int i = 0; i < n; i++) { float w = Noise(r); hp = 0.9f * (hp + w - prev); prev = w; d[i] = hp * 0.04f; }
            for (int k = 0; k < 90; k++)
            {
                int at = r.Next(n - 400); float a = 0.15f + (float)r.NextDouble() * 0.35f;
                for (int i = 0; i < 300; i++) d[at + i] += Noise(r) * a * Mathf.Exp(-i / 40f);
            }
            return d;
        }

        /// <summary>Poço: gotas d'água ecoando.</summary>
        private static float[] Well(System.Random r)
        {
            int n = Rate * 5; var d = new float[n];
            for (int k = 0; k < 9; k++)
            {
                int at = r.Next(n - Rate / 2); float f = 900f + (float)r.NextDouble() * 700f;
                for (int e = 0; e < 3; e++)
                {
                    int o = at + e * 2600; if (o >= n - 3000) break;
                    for (int i = 0; i < 3000; i++) { float t = i / (float)Rate; d[o + i] += Mathf.Sin(2 * Mathf.PI * (f + 900f * t) * t) * Mathf.Exp(-t * 35f) * 0.25f / (e + 1); }
                }
            }
            return d;
        }

        /// <summary>Burburinho de mercado: várias "vozes" de formantes graves que sobem e descem, sem palavras.</summary>
        private static float[] Murmur(System.Random r)
        {
            int n = Rate * 8; var d = new float[n];
            for (int v = 0; v < 7; v++)
            {
                float f0 = 95f + (float)r.NextDouble() * 120f, ph = 0, lp = 0;
                float syl = 3.2f + (float)r.NextDouble() * 2.5f, off = (float)r.NextDouble() * 10f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Max(0f, Mathf.Sin(2 * Mathf.PI * syl * t + off)) * (0.5f + 0.5f * Mathf.Sin(t * 0.7f + off * 3f));
                    float f = f0 * (1f + 0.12f * Mathf.Sin(t * 2.1f + off));
                    ph += f / Rate;
                    float src = (ph % 1f) * 2f - 1f;          // dente de serra = voz
                    lp += (src - lp) * 0.08f;                  // abafa como se viesse de longe
                    d[i] += lp * env * 0.07f;
                }
            }
            return d;
        }

        /// <summary>Noite: vento baixo que vai e vem e grilos em pulsos rápidos.</summary>
        private static float[] Night(System.Random r)
        {
            int n = Rate * 10; var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise(r) - lp) * 0.015f;
                d[i] = lp * (0.6f + 0.4f * Mathf.Sin(t * 0.6f)) * 0.9f;
            }
            for (int c = 0; c < 3; c++)
            {
                float f = 4200f + c * 380f, rate = 2.2f + c * 0.7f, off = (float)r.NextDouble();
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate, cyc = (t * rate + off) % 1f;
                    if (cyc > 0.35f) continue;
                    float pulse = Mathf.Sin(cyc / 0.35f * Mathf.PI * 6f); pulse *= pulse;
                    d[i] += Mathf.Sin(2 * Mathf.PI * f * t) * pulse * 0.035f / (c + 1);
                }
            }
            return d;
        }

        /// <summary>Coruja: dois pios graves.</summary>
        private static float[] Owl(System.Random r)
        {
            int n = Rate * 2; var d = new float[n];
            foreach (var (t0, len, f) in new[] { (0.0f, 0.32f, 390f), (0.55f, 0.7f, 370f) })
            {
                int at = (int)(t0 * Rate), m = (int)(len * Rate);
                for (int i = 0; i < m && at + i < n; i++)
                {
                    float t = i / (float)Rate, env = Mathf.Sin(Mathf.PI * i / m);
                    d[at + i] += (Mathf.Sin(2 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * f * t)) * env * 0.35f;
                }
            }
            return d;
        }
    }
}
