using System.Collections.Generic;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.World;
using UnityEngine;

namespace Kezor.Client
{
    /// <summary>Família de voz de um monstro: decide o timbre do grito de alerta, do ataque, da dor e da morte.</summary>
    public enum MonsterVoice { Beast, Slime, Insect, Undead, Stone, Crystal, Spirit, Fire }

    /// <summary>O que o monstro está fazendo quando faz barulho.</summary>
    public enum MonsterSfx { Aggro, Attack, Hurt, Death }

    /// <summary>
    /// Sons de combate sintetizados em código, no mesmo espírito do <see cref="TownAmbience"/>: nenhum arquivo de áudio,
    /// nada de licença de terceiros, tudo gerado na primeira vez que toca e guardado em memória. Cobre o golpe de cada
    /// classe (espada, lâminas, lança, arco, magia, punhos de brasa), o impacto no alvo (carne, crítico, pedra, magia,
    /// flecha, fogo do combo, errou), a dor do herói e quatro sons por família de monstro (alerta, ataque, dor, morte).
    /// O volume e o pan saem da distância até o herói e da posição na tela, porque a câmera isométrica fica longe dele.
    /// </summary>
    public sealed class CombatAudio : MonoBehaviour
    {
        private const int Rate = 22050, Voices = 24, Variants = 3;
        /// <summary>Volume geral dos efeitos de combate (0..1), separado do ambiente da vila.</summary>
        public static float Volume = 0.85f;
        public static CombatAudio? I { get; private set; }
        /// <summary>De onde se "ouve" (o herói); sem ele, a câmera.</summary>
        public static Transform? Focus;

        private readonly AudioSource[] _pool = new AudioSource[Voices];
        private int _next;
        private readonly Dictionary<string, float> _lastKey = new Dictionary<string, float>();
        private readonly Dictionary<int, float> _lastHurt = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _lastAggro = new Dictionary<int, float>();
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        public static CombatAudio Ensure()
        {
            if (I != null) return I;
            var go = new GameObject("Sons de combate");
            DontDestroyOnLoad(go);
            var a = go.AddComponent<CombatAudio>();
            for (int i = 0; i < Voices; i++)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.loop = false; s.spatialBlend = 0f; s.dopplerLevel = 0f;
                a._pool[i] = s;
            }
            return I = a;
        }

        private void OnDestroy() { if (I == this) I = null; }

        // ---------------------------------------------------------------- tocar
        /// <summary>Toca um som num ponto do mundo. Ignora repetições do mesmo som em menos de 40 ms (golpes em área).</summary>
        public void Play(string key, Vector3 pos, float vol = 1f, float pitch = 1f, float delay = 0f)
        {
            float now = Time.time;
            if (_lastKey.TryGetValue(key, out var last) && now - last < 0.04f) return;
            _lastKey[key] = now;

            var cam = Camera.main;
            Vector3 ear = Focus != null ? Focus.position : (cam != null ? cam.transform.position : pos);
            Vector3 off = pos - ear; off.y = 0;
            float dist = off.magnitude;
            float att = 1f - Mathf.Clamp01((dist - 7f) / 33f);           // cheio até 7 m, some aos 40 m
            if (att <= 0.01f) return;
            float pan = 0f;
            if (cam != null) pan = Mathf.Clamp(Vector3.Dot(off, cam.transform.right) / 14f, -0.75f, 0.75f);

            var clip = Get(key, Random.Range(0, Variants));
            if (clip == null) return;
            var s = _pool[_next]; _next = (_next + 1) % Voices;
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(vol * att * att * Volume);
            s.pitch = pitch * Random.Range(0.94f, 1.06f);
            s.panStereo = pan;
            if (delay > 0f) s.PlayDelayed(delay); else s.Play();
        }

        /// <summary>O gesto do herói: som do golpe/disparo conforme a classe (e a skill usada).</summary>
        public void HeroSwing(CharacterClass cls, int skillId, Vector3 pos)
        {
            switch (cls)
            {
                case CharacterClass.Warden: Play("swing_heavy", pos, 0.75f, skillId != 0 ? 0.85f : 1f); break;
                case CharacterClass.Commander: Play("swing_spear", pos, 0.7f); break;
                case CharacterClass.Bladeweaver: Play("swing_light", pos, 0.7f); if (skillId != 0) Play("swing_light2", pos, 0.6f, 1.15f, 0.08f); break;
                case CharacterClass.Ranger: Play("bow", pos, 0.8f); break;
                case CharacterClass.Arcanist: Play(skillId == 4 ? "spell_fire" : "spell_arcane", pos, 0.6f); break;
                case CharacterClass.Invoker: Play("spell_summon", pos, 0.55f); break;
                case CharacterClass.EmberFist: Play("punch", pos, 0.7f); break;
            }
        }

        /// <summary>Tempo até o golpe encostar (projéteis voam antes de acertar).</summary>
        public static float ImpactDelay(CharacterClass? cls, HitType type) => type == HitType.Combo ? 0f : cls switch
        {
            CharacterClass.Ranger => 0.18f,
            CharacterClass.Invoker or CharacterClass.Arcanist => 0.28f,
            _ => 0.07f,
        };

        /// <summary>O impacto no alvo. <paramref name="targetVoice"/> muda o material (pedra, vidro) quando o alvo é monstro.</summary>
        public void Impact(Vector3 pos, HitType type, CharacterClass? attacker, MonsterVoice? targetVoice, bool onMe, float delay)
        {
            if (type == HitType.Miss) { Play("miss", pos, 0.45f, 1f, delay); return; }
            if (type == HitType.Combo) { Play("hit_fire", pos, 0.9f, 1f, delay); return; }
            bool magic = attacker is CharacterClass.Arcanist or CharacterClass.Invoker;
            string k = magic ? "hit_magic"
                : attacker == CharacterClass.Ranger ? "hit_arrow"
                : targetVoice is MonsterVoice.Stone ? "hit_stone"
                : targetVoice is MonsterVoice.Crystal ? "hit_glass"
                : "hit_flesh";
            Play(k, pos, onMe ? 0.75f : 0.65f, 1f, delay);
            if (type == HitType.Critical || type == HitType.Surge) Play("hit_crit", pos, 0.6f, type == HitType.Surge ? 1.12f : 1f, delay);
            if (onMe) Play("hurt_me", pos, 0.6f, 1f, delay);
        }

        /// <summary>Barulho de monstro. Dor é limitada a um por monstro a cada 0,3 s; alerta, um a cada 6 s.</summary>
        public void Monster(int entityId, int defId, MonsterSfx what, Vector3 pos)
        {
            float now = Time.time;
            if (what == MonsterSfx.Hurt) { if (_lastHurt.TryGetValue(entityId, out var h) && now - h < 0.3f) return; _lastHurt[entityId] = now; }
            if (what == MonsterSfx.Aggro) { if (_lastAggro.TryGetValue(entityId, out var g) && now - g < 6f) return; _lastAggro[entityId] = now; }
            var def = MonsterCatalog.Get(defId);
            bool boss = def != null && def.Boss;
            float size = def != null ? def.Scale : 1f;
            // bichos grandes soam mais graves; chefes, mais graves e mais altos
            float pitch = Mathf.Clamp(1.12f - (size - 1f) * 0.28f, 0.7f, 1.15f) * (boss ? 0.85f : 1f);
            float vol = what switch { MonsterSfx.Aggro => 0.6f, MonsterSfx.Attack => 0.55f, MonsterSfx.Hurt => 0.4f, _ => 0.7f } * (boss ? 1.3f : 1f);
            string key = VoiceOf(defId).ToString().ToLowerInvariant() + "_" + what.ToString().ToLowerInvariant();
            Play(key, pos, vol, pitch, what == MonsterSfx.Hurt ? 0.07f : 0f);
            if (what == MonsterSfx.Death) _lastHurt.Remove(entityId);
        }

        public static MonsterVoice VoiceOf(int defId) => defId switch
        {
            1 => MonsterVoice.Slime,
            2 or 13 or 16 => MonsterVoice.Beast,          // lobo, lince, serpe
            3 or 6 or 11 => MonsterVoice.Insect,          // aranhas, rastejante
            4 or 8 or 15 => MonsterVoice.Undead,          // ossadas, carniçal
            5 or 53 => MonsterVoice.Stone,                // golem, colosso
            10 or 14 or 52 or 54 => MonsterVoice.Crystal, // sentinela, vigia de gelo, soberano, estilhaço
            7 or 9 or 12 or 60 or 61 => MonsterVoice.Spirit, // bruxa, mariposa, espectro, ecos
            50 or 51 => MonsterVoice.Fire,                // cria e arauto das cinzas
            _ => MonsterVoice.Beast,
        };

        // ---------------------------------------------------------------- banco de clipes
        private delegate float[] Synth(System.Random r);

        private static AudioClip? Get(string key, int variant)
        {
            string id = key + "#" + variant;
            if (Clips.TryGetValue(id, out var c) && c != null) return c;
            var synth = SynthFor(key);
            if (synth == null) { Debug.LogWarning("[Kezor] som desconhecido: " + key); return null; }
            var data = Finish(synth(new System.Random(StableHash(id))));
            c = AudioClip.Create("kzfx_" + id, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return Clips[id] = c;
        }

        /// <summary>Hash estável entre execuções (string.GetHashCode muda a cada processo no .NET novo).</summary>
        private static int StableHash(string s) { unchecked { int h = 23; foreach (char ch in s) h = h * 31 + ch; return h; } }

        private static Synth? SynthFor(string key) => key switch
        {
            // ---- golpes do herói
            "swing_heavy" => r => Whoosh(r, 0.30f, 260f, 950f, 1.3f, 0.5f),
            "swing_light" => r => Whoosh(r, 0.16f, 900f, 2300f, 1.6f, 0.4f),
            "swing_light2" => r => Whoosh(r, 0.14f, 1100f, 2700f, 1.8f, 0.35f),
            "swing_spear" => r => Mix(0.3f, (Whoosh(r, 0.22f, 500f, 1600f, 2.2f, 0.3f), 0f, 1f), (Clicks(r, 0.05f, 1, 2600f, 9f, 0.1f, 0f), 0f, 0.25f)),
            "bow" => r => Mix(0.45f, (Pluck(r, 0.4f, R(r, 105f, 125f), 0.994f), 0f, 1f), (Whoosh(r, 0.2f, 2600f, 1700f, 3f, 0.25f), 0.02f, 0.35f)),
            "punch" => r => Whoosh(r, 0.13f, 500f, 1500f, 1.1f, 0.55f),
            "spell_arcane" => r => Mix(0.6f, (Chirp(r, 0.4f, 320f, 980f, 0.04f, 0.18f, 2f), 0f, 1f), (Sparkle(r, 0.45f, 18), 0.05f, 0.35f)),
            "spell_fire" => r => Mix(0.6f, (Roar(r, 0.5f, 300f, 1600f), 0f, 1f), (Tone(r, 0.45f, 90f, 60f, 0.2f), 0f, 0.5f)),
            "spell_summon" => r => Echo(Mix(0.5f, (Chirp(r, 0.35f, 520f, 260f, 0.03f, 0.15f, 3f), 0f, 1f), (Sparkle(r, 0.4f, 12), 0f, 0.3f)), 0.11f, 0.35f, 0.4f),

            // ---- impactos
            "hit_flesh" => r => Thud(r, 0.18f, 190f, 70f, 0.05f, 0.7f),
            "hit_crit" => r => Mix(0.6f, (Thud(r, 0.25f, 160f, 55f, 0.08f, 0.9f), 0f, 0.8f), (Bell(r, 0.55f, R(r, 1150f, 1350f), 0.14f), 0f, 0.55f)),
            "hit_stone" => r => Mix(0.25f, (Thud(r, 0.18f, 140f, 60f, 0.05f, 0.4f), 0f, 0.8f), (Clicks(r, 0.2f, 7, 1300f, 5f, 0.35f, 0f), 0f, 0.8f)),
            "hit_glass" => r => Mix(0.4f, (Thud(r, 0.14f, 200f, 90f, 0.04f, 0.5f), 0f, 0.6f), (Bell(r, 0.4f, R(r, 2200f, 2700f), 0.08f), 0f, 0.7f)),
            "hit_magic" => r => Mix(0.3f, (Sweep(r, 0.26f, 3200f, 600f, 4f), 0f, 0.8f), (Tone(r, 0.25f, 760f, 200f, 0.08f), 0f, 0.6f)),
            "hit_arrow" => r => Mix(0.15f, (Thud(r, 0.12f, 320f, 120f, 0.03f, 1f), 0f, 1f), (Clicks(r, 0.04f, 1, 1900f, 6f, 0.1f, 0f), 0f, 0.4f)),
            "hit_fire" => r => Mix(0.7f, (Thud(r, 0.5f, 130f, 38f, 0.14f, 0.4f), 0f, 0.9f), (Roar(r, 0.55f, 1100f, 250f), 0f, 0.8f), (Crackle(r, 0.6f, 24), 0.08f, 0.35f)),
            "miss" => r => Whoosh(r, 0.18f, 1300f, 2700f, 1.2f, 0.3f),
            "hurt_me" => r => Mix(0.25f, (Thud(r, 0.2f, 140f, 60f, 0.07f, 0.8f), 0f, 0.7f), (Growl(r, 0.18f, 160f, 105f, 0.3f, 700f, 1500f, 0.01f), 0.01f, 0.45f)),
            "death_me" => r => Echo(Mix(1.3f, (Thud(r, 0.6f, 120f, 35f, 0.2f, 0.5f), 0f, 0.8f), (Tone(r, 1.1f, 440f, 210f, 0.5f), 0.1f, 0.5f)), 0.16f, 0.35f, 0.6f),

            // ---- herói e skills sem alvo
            "levelup" => r => Echo(Arpeggio(r, new[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.09f, 0.55f), 0.13f, 0.3f, 0.5f),
            "cast_buff" => r => Echo(Mix(0.6f, (Chirp(r, 0.5f, 400f, 1200f, 0.05f, 0.25f, 1.5f), 0f, 1f), (Sparkle(r, 0.55f, 16), 0.1f, 0.3f)), 0.12f, 0.3f, 0.4f),
            "cast_banner" => r => Horn(r, 0.85f, 110f),
            "area" => r => Mix(0.7f, (Thud(r, 0.6f, 95f, 34f, 0.18f, 0.5f), 0f, 1f), (Roar(r, 0.5f, 900f, 200f), 0f, 0.6f)),

            // ---- feras (lobo, lince, serpe)
            "beast_aggro" => r => Growl(r, 0.9f, 82f, 96f, 0.85f, 480f, 1100f, 0.15f),
            "beast_attack" => r => Mix(0.3f, (Growl(r, 0.26f, 190f, 120f, 0.7f, 900f, 2000f, 0.02f), 0f, 1f), (Clicks(r, 0.05f, 2, 2400f, 4f, 0.5f, 0f), 0.16f, 0.4f)),
            "beast_hurt" => r => Growl(r, 0.18f, 720f, 380f, 0.15f, 1200f, 2600f, 0.01f),
            "beast_death" => r => Mix(0.9f, (Growl(r, 0.8f, 520f, 150f, 0.35f, 1000f, 2200f, 0.02f), 0f, 1f), (Thud(r, 0.3f, 110f, 45f, 0.1f, 0.5f), 0.55f, 0.5f)),

            // ---- lodos
            "slime_aggro" => r => Blips(r, 0.6f, 9, 140f, 320f, 0.05f),
            "slime_attack" => r => Mix(0.3f, (Tone(r, 0.24f, 270f, 85f, 0.08f), 0f, 1f), (Roar(r, 0.2f, 700f, 250f), 0f, 0.4f)),
            "slime_hurt" => r => Mix(0.2f, (Sweep(r, 0.16f, 850f, 300f, 3f), 0f, 0.7f), (Tone(r, 0.14f, 420f, 200f, 0.05f), 0f, 0.7f)),
            "slime_death" => r => Mix(0.8f, (Roar(r, 0.3f, 900f, 200f), 0f, 0.8f), (Blips(r, 0.75f, 14, 90f, 260f, 0.06f), 0.05f, 0.8f)),

            // ---- aranhas e rastejantes
            "insect_aggro" => r => Mix(0.65f, (Clicks(r, 0.6f, 24, 3200f, 8f, 0.9f, 0f), 0f, 1f), (Hiss(r, 0.6f, 5200f, 0.4f), 0f, 0.35f)),
            "insect_attack" => r => Mix(0.3f, (Hiss(r, 0.26f, 4400f, 0.2f), 0f, 1f), (Clicks(r, 0.2f, 5, 2800f, 7f, 0.6f, 0f), 0f, 0.6f)),
            "insect_hurt" => r => Mix(0.2f, (Squeal(r, 0.16f, 2300f, 1600f), 0f, 0.8f), (Clicks(r, 0.15f, 4, 3000f, 8f, 0.8f, 0f), 0f, 0.5f)),
            "insect_death" => r => Mix(0.9f, (Clicks(r, 0.85f, 30, 2800f, 7f, 1f, 0.3f), 0f, 1f), (Thud(r, 0.2f, 160f, 70f, 0.05f, 0.9f), 0f, 0.6f), (Squeal(r, 0.4f, 2000f, 900f), 0f, 0.4f)),

            // ---- mortos-vivos
            "undead_aggro" => r => Mix(1.0f, (Growl(r, 1.0f, 112f, 92f, 0.25f, 620f, 960f, 0.25f), 0f, 0.9f), (Clicks(r, 0.5f, 10, 2000f, 10f, 0.8f, 0f), 0.4f, 0.5f)),
            "undead_attack" => r => Mix(0.3f, (Whoosh(r, 0.22f, 400f, 1300f, 1.4f, 0.45f), 0f, 0.7f), (Clicks(r, 0.25f, 8, 2200f, 10f, 0.8f, 0f), 0f, 0.8f)),
            "undead_hurt" => r => Clicks(r, 0.22f, 10, 2300f, 10f, 0.8f, 0f),
            "undead_death" => r => Mix(1.0f, (Clicks(r, 1.0f, 32, 1900f, 9f, 0.9f, 0.45f), 0f, 1f), (Thud(r, 0.35f, 120f, 50f, 0.1f, 0.6f), 0.15f, 0.6f), (Growl(r, 0.6f, 100f, 70f, 0.3f, 600f, 900f, 0.02f), 0f, 0.4f)),

            // ---- golens de pedra
            "stone_aggro" => r => Mix(1.0f, (Rumble(r, 1.0f, 46f), 0f, 1f), (Clicks(r, 0.6f, 6, 800f, 4f, 0.8f, 0f), 0.2f, 0.4f)),
            "stone_attack" => r => Mix(0.5f, (Whoosh(r, 0.38f, 140f, 520f, 1f, 0.6f), 0f, 0.8f), (Thud(r, 0.3f, 90f, 38f, 0.1f, 0.6f), 0.2f, 0.9f)),
            "stone_hurt" => r => Mix(0.2f, (Clicks(r, 0.2f, 8, 900f, 4f, 0.5f, 0f), 0f, 1f), (Thud(r, 0.15f, 100f, 50f, 0.05f, 0.4f), 0f, 0.6f)),
            "stone_death" => r => Mix(1.3f, (Clicks(r, 1.2f, 42, 700f, 3f, 0.9f, 0.6f), 0f, 1f), (Rumble(r, 1.3f, 40f), 0f, 0.9f), (Thud(r, 0.5f, 80f, 30f, 0.18f, 0.6f), 0.1f, 0.8f)),

            // ---- vidro e gelo
            "crystal_aggro" => r => Echo(Mix(0.8f, (Bell(r, 0.8f, R(r, 1700f, 1900f), 0.3f), 0f, 0.8f), (Bell(r, 0.7f, R(r, 2500f, 2700f), 0.25f), 0.08f, 0.5f), (Rumble(r, 0.8f, 60f), 0f, 0.4f)), 0.12f, 0.35f, 0.4f),
            "crystal_attack" => r => Mix(0.4f, (Whoosh(r, 0.3f, 900f, 2600f, 2f, 0.45f), 0f, 0.8f), (Bell(r, 0.3f, R(r, 2900f, 3300f), 0.07f), 0.12f, 0.5f)),
            "crystal_hurt" => r => Bell(r, 0.35f, R(r, 2300f, 2600f), 0.08f),
            "crystal_death" => r => Echo(Mix(0.9f, (Clicks(r, 0.8f, 36, 4200f, 12f, 0.7f, 0.25f), 0f, 1f), (Bell(r, 0.8f, 1600f, 0.2f), 0f, 0.5f), (Bell(r, 0.7f, 3100f, 0.15f), 0.05f, 0.4f), (Hiss(r, 0.5f, 6000f, 0.15f), 0f, 0.3f)), 0.1f, 0.3f, 0.4f),

            // ---- espíritos (bruxa, mariposa, espectro, ecos)
            "spirit_aggro" => r => Echo(Wail(r, 1.1f, 640f, 520f), 0.14f, 0.4f, 0.6f),
            "spirit_attack" => r => Echo(Mix(0.4f, (Whoosh(r, 0.35f, 800f, 2400f, 2f, 0.4f), 0f, 0.7f), (Chirp(r, 0.3f, 500f, 1100f, 0.02f, 0.12f, 1f), 0f, 0.5f)), 0.1f, 0.3f, 0.3f),
            "spirit_hurt" => r => Echo(Wail(r, 0.22f, 940f, 720f), 0.08f, 0.3f, 0.25f),
            "spirit_death" => r => Echo(Mix(1.2f, (Chirp(r, 1.2f, 420f, 1450f, 0.05f, 0.6f, 0.5f), 0f, 0.8f), (Hiss(r, 1.0f, 1800f, 0.5f), 0f, 0.35f)), 0.15f, 0.45f, 0.8f),

            // ---- fogo (cria e arauto das cinzas)
            "fire_aggro" => r => Mix(1.0f, (Growl(r, 0.9f, 68f, 80f, 0.95f, 420f, 900f, 0.1f), 0f, 0.9f), (Roar(r, 0.9f, 400f, 900f), 0f, 0.6f)),
            "fire_attack" => r => Mix(0.45f, (Roar(r, 0.38f, 300f, 1500f), 0f, 1f), (Crackle(r, 0.4f, 12), 0.05f, 0.4f)),
            "fire_hurt" => r => Mix(0.25f, (Crackle(r, 0.22f, 10), 0f, 0.8f), (Thud(r, 0.15f, 110f, 55f, 0.05f, 0.4f), 0f, 0.6f)),
            "fire_death" => r => Mix(1.1f, (Thud(r, 0.8f, 105f, 30f, 0.25f, 0.5f), 0f, 0.9f), (Roar(r, 0.7f, 1200f, 200f), 0f, 0.8f), (Crackle(r, 1.0f, 30), 0.2f, 0.4f)),
            _ => null,
        };

        // ---------------------------------------------------------------- síntese
        private static float N(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);
        private static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        private static float[] New(float sec) => new float[Mathf.Max(1, (int)(sec * Rate))];
        private const float Tau = Mathf.PI * 2f;

        /// <summary>Filtro passa-banda (biquad RBJ) com frequência que pode mudar a cada amostra.</summary>
        private struct BandPass
        {
            private float _x1, _x2, _y1, _y2;
            public float Run(float x, float f, float q)
            {
                f = Mathf.Clamp(f, 20f, Rate * 0.45f);
                float w = Tau * f / Rate, al = Mathf.Sin(w) / (2f * q), c = Mathf.Cos(w), a0 = 1f + al;
                float y = (al * x - al * _x2 + 2f * c * _y1 - (1f - al) * _y2) / a0;
                _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
                return y;
            }
        }

        /// <summary>Passa-baixa de um polo.</summary>
        private struct LowPass
        {
            private float _y;
            public float Run(float x, float f) { float k = 1f - Mathf.Exp(-Tau * f / Rate); return _y += (x - _y) * k; }
        }

        private static float[] Norm(float[] d, float peak = 1f)
        {
            float m = 0f;
            foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v));
            if (m > 1e-6f) { float k = peak / m; for (int i = 0; i < d.Length; i++) d[i] *= k; }
            return d;
        }

        /// <summary>Entrada e saída suaves (sem estalo), saturação leve e pico em 0,9.</summary>
        private static float[] Finish(float[] d)
        {
            int fin = Mathf.Min(48, d.Length / 4), fout = Mathf.Min(Rate / 60, d.Length / 3);
            for (int i = 0; i < fin; i++) d[i] *= i / (float)fin;
            for (int i = 0; i < fout; i++) d[d.Length - 1 - i] *= i / (float)fout;
            Norm(d);
            for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] * 1.4f) / 0.885f;   // cola o som e dá corpo
            return Norm(d, 0.9f);
        }

        /// <summary>Soma camadas: (som, começa em s, ganho). O tamanho final cobre a camada mais longa.</summary>
        private static float[] Mix(float minLen, params (float[] s, float at, float g)[] parts)
        {
            int n = (int)(minLen * Rate);
            foreach (var p in parts) n = Mathf.Max(n, (int)(p.at * Rate) + p.s.Length);
            var d = new float[n];
            foreach (var p in parts)
            {
                int o = (int)(p.at * Rate);
                for (int i = 0; i < p.s.Length; i++) d[o + i] += p.s[i] * p.g;
            }
            return Norm(d);
        }

        /// <summary>Eco simples de duas linhas (sala de pedra / ar fantasmagórico).</summary>
        private static float[] Echo(float[] d, float delay, float fb, float tail)
        {
            var o = new float[d.Length + (int)(tail * Rate)];
            System.Array.Copy(d, o, d.Length);
            int a = (int)(delay * Rate), b = (int)(delay * 1.37f * Rate);
            for (int i = a; i < o.Length; i++) o[i] += o[i - a] * fb;
            for (int i = b; i < o.Length; i++) o[i] += o[i - b] * fb * 0.6f;
            return Norm(o);
        }

        /// <summary>Vento de golpe: ruído num passa-banda que varre de f0 a f1, com volume em arco.</summary>
        private static float[] Whoosh(System.Random r, float dur, float f0, float f1, float q, float peak)
        {
            var d = New(dur); var bp = new BandPass(); int n = d.Length;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = u < peak ? Mathf.Pow(u / peak, 2f) : Mathf.Pow(1f - (u - peak) / (1f - peak), 1.6f);
                float f = Mathf.Lerp(f0, f1, Mathf.Sin(u * Mathf.PI * 0.5f));
                d[i] = bp.Run(N(r), f, q) * env;
            }
            return Norm(d);
        }

        /// <summary>Ruído filtrado que só desce/sobe de frequência, com queda rápida (zap, chape).</summary>
        private static float[] Sweep(System.Random r, float dur, float f0, float f1, float q)
        {
            var d = New(dur); var bp = new BandPass(); int n = d.Length;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                d[i] = bp.Run(N(r), Mathf.Lerp(f0, f1, u), q) * Mathf.Exp(-u * 4f) * Mathf.Min(1f, u * 40f);
            }
            return Norm(d);
        }

        /// <summary>Pancada: seno grave que cai de altura + estalo de ruído abafado no começo.</summary>
        private static float[] Thud(System.Random r, float dur, float f0, float f1, float decay, float noise)
        {
            var d = New(dur); float ph = 0f; var lp = new LowPass();
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = f1 + (f0 - f1) * Mathf.Exp(-t * 30f);
                ph += f / Rate;
                d[i] = Mathf.Sin(Tau * ph) * Mathf.Exp(-t / decay) + lp.Run(N(r), 2000f) * noise * 2.5f * Mathf.Exp(-t * 55f);
            }
            return Norm(d);
        }

        /// <summary>Tom puro que desliza de f0 a f1 e decai (bolha, gemido curto).</summary>
        private static float[] Tone(System.Random r, float dur, float f0, float f1, float decay)
        {
            var d = New(dur); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, u = i / (float)d.Length;
                ph += Mathf.Lerp(f0, f1, u) / Rate;
                d[i] = (Mathf.Sin(Tau * ph) + 0.25f * Mathf.Sin(Tau * ph * 2f)) * Mathf.Exp(-t / decay) * Mathf.Min(1f, t * 300f);
            }
            return Norm(d);
        }

        /// <summary>Sino metálico/vidro: parciais inarmônicas que somem em velocidades diferentes.</summary>
        private static float[] Bell(System.Random r, float dur, float f, float decay)
        {
            var d = New(dur);
            float[] parts = { 1f, 2.76f, 5.4f, 8.93f };
            for (int p = 0; p < parts.Length; p++)
            {
                float fp = f * parts[p] * R(r, 0.99f, 1.01f), dp = decay / (1f + p * 0.8f), a = 1f / (p + 1);
                if (fp > Rate * 0.45f) continue;
                for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] += Mathf.Sin(Tau * fp * t) * Mathf.Exp(-t / dp) * a; }
            }
            return Norm(d);
        }

        /// <summary>Estalos ressonantes espalhados (ossos, pedras, quitina, vidro). decay &gt; 0 = vão rareando.</summary>
        private static float[] Clicks(System.Random r, float dur, int count, float f, float q, float spread, float decay)
        {
            var d = New(dur); int len = Mathf.Min(900, d.Length);
            for (int k = 0; k < count; k++)
            {
                float at = count == 1 ? 0f : (float)r.NextDouble() * spread * dur;
                if (decay > 0f) at = -Mathf.Log(1f - (float)r.NextDouble() * 0.95f) * decay * 0.5f;   // mais estalos no começo
                int o = (int)(at * Rate); if (o >= d.Length - 32) continue;
                float ff = f * R(r, 0.7f, 1.35f), amp = R(r, 0.5f, 1f) * (decay > 0f ? Mathf.Exp(-at / decay) : 1f);
                var bp = new BandPass();
                for (int j = 0; j < len && o + j < d.Length; j++) d[o + j] += bp.Run(j < 14 ? N(r) : 0f, ff, q) * amp;
            }
            return Norm(d);
        }

        /// <summary>Corda dedilhada (Karplus-Strong): o estalo da corda do arco.</summary>
        private static float[] Pluck(System.Random r, float dur, float f, float damp)
        {
            var d = New(dur); int len = Mathf.Max(2, (int)(Rate / f));
            var buf = new float[len];
            for (int k = 0; k < len; k++) buf[k] = N(r);
            int p = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float a = buf[p], b = buf[(p + 1) % len];
                buf[p] = (a + b) * 0.5f * damp;
                d[i] = a; p = (p + 1) % len;
            }
            return Norm(d);
        }

        /// <summary>Voz de bicho: dente de serra áspero (tremor ~28 Hz) passando por dois formantes.</summary>
        private static float[] Growl(System.Random r, float dur, float f0, float f1, float rough, float fa, float fb, float attack)
        {
            var d = New(dur); int n = d.Length; float ph = 0f;
            var b1 = new BandPass(); var b2 = new BandPass(); var jit = new LowPass(); var am = new LowPass();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = i / (float)n;
                float wob = jit.Run(N(r), 12f) * 6f;
                float f = Mathf.Lerp(f0, f1, u) * (1f + wob * 0.04f * (0.4f + rough));
                ph += f / Rate; ph -= Mathf.Floor(ph);
                float src = ph * 2f - 1f;
                float trem = 1f - rough * 0.55f * (1f + Mathf.Sin(Tau * 28f * t + am.Run(N(r), 40f) * 20f));
                src = src * trem + N(r) * rough * 0.25f;
                float env = Mathf.Min(1f, t / Mathf.Max(0.005f, attack)) * (u > 0.65f ? (1f - u) / 0.35f : 1f);
                d[i] = (b1.Run(src, fa, 3.5f) + b2.Run(src, fb, 5f) * 0.7f + src * 0.06f) * env;
            }
            return Norm(d);
        }

        /// <summary>Ruído passa-alta (sibilo de aranha, vapor).</summary>
        private static float[] Hiss(System.Random r, float dur, float f, float attack)
        {
            var d = New(dur); int n = d.Length; var bp = new BandPass();
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = u < attack ? u / attack : Mathf.Pow(1f - (u - attack) / (1f - attack), 1.5f);
                d[i] = bp.Run(N(r), f, 0.8f) * env;
            }
            return Norm(d);
        }

        /// <summary>Guincho agudo com vibrato rápido.</summary>
        private static float[] Squeal(System.Random r, float dur, float f0, float f1)
        {
            var d = New(dur); int n = d.Length; float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = i / (float)n;
                ph += Mathf.Lerp(f0, f1, u) * (1f + 0.06f * Mathf.Sin(Tau * 32f * t)) / Rate;
                d[i] = Mathf.Sin(Tau * ph) * Mathf.Sin(Mathf.PI * u);
            }
            return Norm(d);
        }

        /// <summary>Rugido de fogo: ruído abafado com o filtro abrindo/fechando de f0 a f1.</summary>
        private static float[] Roar(System.Random r, float dur, float f0, float f1)
        {
            var d = New(dur); int n = d.Length; var lp = new LowPass(); var lp2 = new LowPass();
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float f = Mathf.Lerp(f0, f1, u);
                float env = Mathf.Min(1f, u * 12f) * Mathf.Pow(1f - u, 1.3f);
                d[i] = lp2.Run(lp.Run(N(r), f), f * 1.5f) * env;
            }
            return Norm(d);
        }

        /// <summary>Crepitar: estalos secos de brasa.</summary>
        private static float[] Crackle(System.Random r, float dur, int count)
        {
            var d = New(dur);
            for (int k = 0; k < count; k++)
            {
                int o = r.Next(Mathf.Max(1, d.Length - 400)); float a = R(r, 0.3f, 1f);
                for (int j = 0; j < 260 && o + j < d.Length; j++) d[o + j] += N(r) * a * Mathf.Exp(-j / 30f);
            }
            return Norm(d);
        }

        /// <summary>Ronco de pedra: ruído muito grave + seno subgrave com tremor.</summary>
        private static float[] Rumble(System.Random r, float dur, float f)
        {
            var d = New(dur); int n = d.Length; var lp = new LowPass(); var lp2 = new LowPass();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = i / (float)n;
                float env = Mathf.Min(1f, u * 6f) * Mathf.Pow(1f - u, 0.8f);
                float trem = 0.7f + 0.3f * Mathf.Sin(Tau * 7f * t);
                d[i] = (lp2.Run(lp.Run(N(r), 140f), 180f) * 6f + Mathf.Sin(Tau * f * t) * 0.5f) * env * trem;
            }
            return Norm(d);
        }

        /// <summary>Glissando com brilho de FM (magia).</summary>
        private static float[] Chirp(System.Random r, float dur, float f0, float f1, float attack, float decay, float index)
        {
            var d = New(dur); int n = d.Length; float ph = 0f, pm = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = i / (float)n;
                float f = f0 * Mathf.Pow(f1 / f0, u);
                ph += f / Rate; pm += f * 3.01f / Rate;
                float env = Mathf.Min(1f, t / attack) * Mathf.Exp(-Mathf.Max(0f, t - attack) / decay);
                d[i] = Mathf.Sin(Tau * ph + index * Mathf.Sin(Tau * pm) * env) * env;
            }
            return Norm(d);
        }

        /// <summary>Faíscas: pings agudos curtos e aleatórios.</summary>
        private static float[] Sparkle(System.Random r, float dur, int count)
        {
            var d = New(dur);
            for (int k = 0; k < count; k++)
            {
                int o = r.Next(Mathf.Max(1, d.Length - 1500)); float f = R(r, 2500f, 5500f), a = R(r, 0.3f, 1f);
                for (int j = 0; j < 1500 && o + j < d.Length; j++) { float t = j / (float)Rate; d[o + j] += Mathf.Sin(Tau * f * t) * a * Mathf.Exp(-t * 45f); }
            }
            return Norm(d);
        }

        /// <summary>Bolhas de lodo: senos curtos que sobem de altura.</summary>
        private static float[] Blips(System.Random r, float dur, int count, float fLo, float fHi, float len)
        {
            var d = New(dur); int m = (int)(len * Rate);
            for (int k = 0; k < count; k++)
            {
                int o = r.Next(Mathf.Max(1, d.Length - m)); float f = R(r, fLo, fHi), a = R(r, 0.4f, 1f);
                for (int j = 0; j < m && o + j < d.Length; j++)
                {
                    float t = j / (float)Rate, u = j / (float)m;
                    d[o + j] += Mathf.Sin(Tau * (f + f * 1.8f * u) * t) * a * Mathf.Sin(Mathf.PI * u);
                }
            }
            return Norm(d);
        }

        /// <summary>Lamento fantasma: três senos levemente desafinados com vibrato lento.</summary>
        private static float[] Wail(System.Random r, float dur, float f0, float f1)
        {
            var d = New(dur); int n = d.Length; float p1 = 0f, p2 = 0f, p3 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = i / (float)n;
                float f = Mathf.Lerp(f0, f1, u) * (1f + 0.025f * Mathf.Sin(Tau * 5.5f * t));
                p1 += f / Rate; p2 += f * 1.007f / Rate; p3 += f * 0.993f * 2f / Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.7f));
                d[i] = (Mathf.Sin(Tau * p1) + Mathf.Sin(Tau * p2) + 0.35f * Mathf.Sin(Tau * p3)) * env;
            }
            return Norm(d);
        }

        /// <summary>Arpejo de sino (subir de nível).</summary>
        private static float[] Arpeggio(System.Random r, float[] notes, float step, float ring)
        {
            var d = New(step * notes.Length + ring);
            for (int k = 0; k < notes.Length; k++)
            {
                int o = (int)(k * step * Rate);
                for (int j = 0; o + j < d.Length; j++)
                {
                    float t = j / (float)Rate, e = Mathf.Exp(-t / (ring * 0.45f)) * Mathf.Min(1f, t * 400f);
                    d[o + j] += (Mathf.Sin(Tau * notes[k] * t) + 0.3f * Mathf.Sin(Tau * notes[k] * 2f * t) + 0.12f * Mathf.Sin(Tau * notes[k] * 3f * t)) * e;
                }
            }
            return Norm(d);
        }

        /// <summary>Corneta de guerra (estandarte do Comandante): quinta justa, filtro abafado.</summary>
        private static float[] Horn(System.Random r, float dur, float f)
        {
            var d = New(dur); int n = d.Length; float p1 = 0f, p2 = 0f; var lp = new LowPass();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = i / (float)n;
                float bend = 1f - 0.04f * Mathf.Exp(-t * 18f);
                p1 += f * bend / Rate; p2 += f * 1.5f * bend / Rate;
                p1 -= Mathf.Floor(p1); p2 -= Mathf.Floor(p2);
                float env = Mathf.Min(1f, t / 0.06f) * (u > 0.6f ? (1f - u) / 0.4f : 1f);
                d[i] = lp.Run((p1 * 2f - 1f) + 0.7f * (p2 * 2f - 1f), 700f + 500f * env) * env;
            }
            return Norm(d);
        }
    }
}
