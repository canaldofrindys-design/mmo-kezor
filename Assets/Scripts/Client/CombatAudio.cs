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
    /// Sons do jogo tocados a partir de gravações reais (espada, arco, magia, impacto, gritos de monstro, passos,
    /// moedas e cliques), no estilo dos MMOs clássicos de fantasia medieval. Os arquivos ficam em Resources/Audio/Sfx
    /// com várias variações por som, e cada toque sorteia uma. Cobre o golpe de cada classe, o impacto no alvo
    /// (carne, crítico, pedra, cristal, magia, flecha, fogo do combo, errou), a dor e a queda do herói e quatro sons
    /// por família de monstro (alerta, ataque, dor, morte).
    /// O volume, o pan e o abafado (passa-baixas) saem da distância até o herói e da posição na tela: perto soa cheio
    /// e brilhante, longe soa baixo e abafado, como num MMO de câmera isométrica. Volume final = canal de efeitos do
    /// <see cref="KezorAudio"/> × volume geral das Opções. Os arquivos já vêm mixados no mesmo padrão de loudness
    /// (Tools/Sons/montar_sons.py), então os volumes aqui só fazem o equilíbrio de jogo.
    /// </summary>
    public sealed class CombatAudio : MonoBehaviour
    {
        private const int Voices = 24;
        public static CombatAudio? I { get; private set; }
        /// <summary>De onde se "ouve" (o herói); sem ele, a câmera.</summary>
        public static Transform? Focus;

        private readonly AudioSource[] _pool = new AudioSource[Voices];
        private readonly AudioLowPassFilter[] _lp = new AudioLowPassFilter[Voices];
        private float _lastHeroVoice = -9f;
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
            {   // uma voz por objeto: cada uma tem o seu passa-baixas (o filtro do Unity vale para o objeto inteiro)
                var v = new GameObject("Voz " + i);
                v.transform.SetParent(go.transform, false);
                var s = v.AddComponent<AudioSource>();
                s.playOnAwake = false; s.loop = false; s.spatialBlend = 0f; s.dopplerLevel = 0f;
                a._pool[i] = s;
                a._lp[i] = v.AddComponent<AudioLowPassFilter>();
                a._lp[i].cutoffFrequency = 22000f;
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

            var clip = Get(key);
            if (clip == null) return;
            int vi = NextVoice();
            var s = _pool[vi];
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(vol * att * att * KezorAudio.Sfx);
            s.pitch = pitch * Random.Range(0.95f, 1.05f);
            s.panStereo = pan;
            // longe = abafado: 20 kHz até 8 m, cai até ~2,5 kHz aos 40 m
            _lp[vi].cutoffFrequency = Mathf.Lerp(20000f, 2500f, Mathf.Pow(Mathf.Clamp01((dist - 8f) / 32f), 0.6f));
            if (delay > 0f) s.PlayDelayed(delay); else s.Play();
        }

        /// <summary>Próxima voz livre; se todas tocam, rouba a que começou primeiro (rodízio).</summary>
        private int NextVoice()
        {
            for (int k = 0; k < Voices; k++)
            {
                int i = (_next + k) % Voices;
                if (!_pool[i].isPlaying) { _next = (i + 1) % Voices; return i; }
            }
            int r = _next; _next = (_next + 1) % Voices; return r;
        }

        /// <summary>O gesto do herói: som do golpe/disparo conforme a classe (e a skill usada).</summary>
        public void HeroSwing(CharacterClass cls, int skillId, Vector3 pos)
        {
            switch (cls)
            {
                case CharacterClass.Warden: Play("swing_heavy", pos, 0.9f, skillId != 0 ? 0.9f : 1f); break;
                case CharacterClass.Commander: Play("swing_spear", pos, 0.9f); break;
                case CharacterClass.Bladeweaver: Play("swing_light", pos, 0.9f); if (skillId != 0) Play("swing_light2", pos, 0.75f, 1.05f, 0.08f); break;
                case CharacterClass.Ranger: Play("bow", pos, 0.9f); break;
                case CharacterClass.Arcanist: Play(skillId == 4 ? "spell_fire" : "spell_arcane", pos, 0.85f); break;
                case CharacterClass.Invoker: Play("spell_summon", pos, 0.8f); break;
                case CharacterClass.EmberFist: Play("punch", pos, 0.9f); break;
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
            if (type == HitType.Miss) { Play("miss", pos, 0.8f, 1f, delay); return; }
            KezorAudio.NotifyCombat();
            if (type == HitType.Combo) { Play("hit_fire", pos, 1f, 1f, delay); return; }
            bool magic = attacker is CharacterClass.Arcanist or CharacterClass.Invoker;
            string k = magic ? "hit_magic"
                : attacker == CharacterClass.Ranger ? "hit_arrow"
                : targetVoice is MonsterVoice.Stone ? "hit_stone"
                : targetVoice is MonsterVoice.Crystal ? "hit_glass"
                : "hit_flesh";
            bool crit = type == HitType.Critical || type == HitType.Surge;
            // crítico troca o impacto comum pelo pesado (com tilintar de lâmina), em vez de empilhar os dois
            if (crit && k == "hit_flesh") Play("hit_crit", pos, 1f, type == HitType.Surge ? 0.92f : 1f, delay);
            else { Play(k, pos, onMe ? 0.95f : 0.85f, 1f, delay); if (crit) Play("hit_crit", pos, 0.6f, 1f, delay); }
            if (onMe) Play("hurt_me", pos, 0.75f, 1f, delay);
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
            float pitch = Mathf.Clamp(1.08f - (size - 1f) * 0.2f, 0.82f, 1.12f) * (boss ? 0.9f : 1f);
            float vol = what switch { MonsterSfx.Aggro => 0.85f, MonsterSfx.Attack => 0.75f, MonsterSfx.Hurt => 0.6f, _ => 0.9f } * (boss ? 1.15f : 1f);
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

        /// <summary>Voz do meu herói: gemido de dor (no máximo um a cada 0,9 s, e só em parte dos golpes) ou grito de queda.</summary>
        public void HeroVoice(bool female, bool death, Vector3 pos, float delay = 0f)
        {
            float now = Time.time;
            if (!death && (now - _lastHeroVoice < 0.9f || Random.value > 0.6f)) return;
            _lastHeroVoice = now;
            Play((death ? "death_" : "hurt_") + (female ? "female" : "male"), pos, death ? 0.95f : 0.8f, 1f, delay);
            if (death) Play("death_me", pos, 0.9f, 1f, delay + 0.25f);
        }

        /// <summary>Voz feminina? (a Invocadora; as outras classes usam a voz masculina)</summary>
        public static bool FemaleVoice(CharacterClass cls) => cls == CharacterClass.Invoker;

        // ---------------------------------------------------------------- interface e mundo
        /// <summary>Som de interface (clique, abrir e fechar janela): sem distância nem pan.</summary>
        public void Ui(string key, float vol = 0.5f)
        {
            float now = Time.unscaledTime;
            if (_lastKey.TryGetValue(key, out var last) && now - last < 0.05f) return;
            _lastKey[key] = now;
            var clip = Get(key);
            if (clip == null) return;
            int vi = NextVoice();
            var s = _pool[vi];
            s.Stop();
            s.clip = clip; s.volume = Mathf.Clamp01(vol * KezorAudio.Ui); s.pitch = Random.Range(0.97f, 1.03f); s.panStereo = 0f;
            _lp[vi].cutoffFrequency = 22000f;
            s.Play();
        }

        /// <summary>Passo no chão. Não passa pelo filtro de repetição de <see cref="Play"/> (vários heróis andando juntos).</summary>
        public void Step(Vector3 pos, float vol)
        {
            _lastKey.Remove("step");
            Play("step", pos, vol, Random.Range(0.92f, 1.05f));
        }

        // ---------------------------------------------------------------- banco de clipes
        // Gravações reais (CC0, Kenney e OpenGameArt) em Resources/Audio/Sfx, uma por variação: "hit_flesh_0", "hit_flesh_1"...
        // Montadas por Tools/Sons/montar_sons.py; fontes e licenças em docs/SONS_LICENCAS.md.
        private static Dictionary<string, AudioClip[]>? _bank;
        private static readonly Dictionary<string, int> LastVariant = new Dictionary<string, int>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        private static AudioClip? Get(string key)
        {
            if (_bank == null) _bank = LoadBank();
            if (!_bank.TryGetValue(key, out var clips) || clips.Length == 0)
            {
                if (Missing.Add(key)) Debug.LogWarning("[Kezor] som sem arquivo em Resources/Audio/Sfx: " + key);
                return null;
            }
            if (clips.Length == 1) return clips[0];
            // sorteia uma variação diferente da última, para golpes seguidos não soarem iguais
            int last = LastVariant.TryGetValue(key, out var l) ? l : -1;
            int i = Random.Range(0, clips.Length - (last >= 0 ? 1 : 0));
            if (last >= 0 && i >= last) i++;
            LastVariant[key] = i;
            return clips[i];
        }

        private static Dictionary<string, AudioClip[]> LoadBank()
        {
            var groups = new Dictionary<string, List<AudioClip>>();
            foreach (var c in Resources.LoadAll<AudioClip>("Audio/Sfx"))
            {
                string key = KeyOf(c.name);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<AudioClip>();
                list.Add(c);
            }
            var bank = new Dictionary<string, AudioClip[]>();
            foreach (var kv in groups)
            {
                kv.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                bank[kv.Key] = kv.Value.ToArray();
            }
            return bank;
        }

        /// <summary>"hit_flesh_2" -> "hit_flesh" (tira o número da variação).</summary>
        public static string KeyOf(string clipName)
        {
            int u = clipName.LastIndexOf('_');
            if (u <= 0 || u == clipName.Length - 1) return clipName;
            for (int i = u + 1; i < clipName.Length; i++) if (!char.IsDigit(clipName[i])) return clipName;
            return clipName.Substring(0, u);
        }
    }
}
