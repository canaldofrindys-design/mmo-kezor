#!/usr/bin/env python3
"""
Monta os sons do Kezor (versão 2, mixagem profissional).

Cada som do jogo é desenhado em camadas a partir de gravações de estúdio livres (Oculus Audio Pack, CC BY 4.0,
e pacotes CC0 de Kenney e OpenGameArt), e passa pela mesma cadeia de mixagem:

    corte -> altura -> soma das camadas -> EQ (passa-altas, prateleira de graves, passa-baixas)
          -> compressor -> reverb de sala curta (cola as camadas no mesmo espaço) -> limitador
          -> loudness alvo da categoria

O loudness alvo já deixa os sons equilibrados entre si (golpe < impacto, passo e clique bem baixos, ambiente
discreto), então no jogo quase todo volume fica em 1. Fontes e licenças em docs/SONS_LICENCAS.md.

Uso:
    python montar_sons.py <pasta-das-fontes> <pasta-Assets/Resources/Audio> [pasta-do-demo]

Pastas dentro da pasta das fontes:
    oc     = github.com/facebookincubator/Meta-open-sound  (corpora/oculus-audio-pack/ogg)
    oga1   = github.com/zuujis-cc0/opengameart-sound-effect
    oga8   = github.com/zuujis-cc0/opengameart-sound-effect-8
    kenney_impact = github.com/Boyquotes/kenney-impact-sounds-for-godot   (addons/kenney impact sounds)
    kenney_rpg    = github.com/Boyquotes/kenney-rpg-audio-for-godot       (addons/kenney rpg audio)
    wyz    = github.com/wyzowl/sfx-kit (sounds)
Precisa de ffmpeg, numpy e pedalboard (pip install numpy pedalboard).
"""
import os
import subprocess
import sys

import numpy as np
from pedalboard import (Compressor, HighpassFilter, Limiter, LowpassFilter, LowShelfFilter, Pedalboard,
                        PeakFilter, Reverb)

SR = 48000
SRC = ""

# ------------------------------------------------------------------ fontes
OC = "oc/"
C150 = "oga1/150-combat-sound-effects/150gameeffectsounds/"
MELEE, EXPL, MAGIC = C150 + "01_combat_melee/", C150 + "03_explosions_destruction/", C150 + "04_magic_spells/"
TEX1 = "oga8/medieval-sound-effects-weapon-textures/medieval_sfx_textures_1_of_2/"
KI, KR = "kenney_impact/", "kenney_rpg/"
BONE_D = "oga8/deep-bone-crackbreak-sfx/deep_breaks/Deep Break "


def oc(name, n=1):
    """Arquivo do Oculus Audio Pack pelo nome UCS (com ou sem o _OS / _LOOP do fim)."""
    for cand in (f"{name}_{n:02d}.ogg", f"{name}_OS_{n:02d}.ogg"):
        if os.path.exists(os.path.join(SRC, OC, cand)):
            return OC + cand
    raise FileNotFoundError(name)


# ------------------------------------------------------------------ leitura e edição
_cache = {}


def load(path, at=0.0, dur=None):
    key = (path, at, dur)
    if key not in _cache:
        full = os.path.join(SRC, path)
        if not os.path.exists(full):
            raise FileNotFoundError(full)
        cmd = ["ffmpeg", "-v", "error"]
        if at > 0:
            cmd += ["-ss", f"{at:.4f}"]
        if dur is not None:
            cmd += ["-t", f"{dur:.4f}"]
        cmd += ["-i", full, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"]
        _cache[key] = np.frombuffer(subprocess.run(cmd, capture_output=True, check=True).stdout, np.float32).copy()
    return _cache[key].copy()


def pitch(x, p):
    """Altura por reamostragem (como fita mais rápida/lenta): o jeito clássico de engrossar ou afinar criaturas."""
    if abs(p - 1.0) < 1e-3:
        return x
    n = int(len(x) / p)
    return np.interp(np.arange(n) * p, np.arange(len(x)), x).astype(np.float32)


def fade(x, fin=0.002, fout=0.05):
    x = x.copy()
    a, b = min(len(x), int(fin * SR)), min(len(x), int(fout * SR))
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b) ** 2
    return x


def trim(x, db=-50):
    if len(x) == 0:
        return x
    thr = np.abs(x).max() * 10 ** (db / 20)
    idx = np.where(np.abs(x) > thr)[0]
    if len(idx) == 0:
        return x
    return x[max(0, idx[0] - int(0.002 * SR)):min(len(x), idx[-1] + int(0.03 * SR))]


def cut(path, at=0.0, dur=None, p=1.0, g=1.0, fout=0.06):
    """Um trecho já aparado, na altura pedida e com ganho g (em escala linear)."""
    return fade(trim(pitch(load(path, at, dur), p)), 0.002, fout) * g


def db(v):
    return 10 ** (v / 20)


def hits(path, n, pre=0.03, length=0.5, thr_db=-26):
    """Os n golpes mais fortes de uma gravação com vários seguidos."""
    x = load(path)
    hop = int(SR * 0.01)
    e = np.array([np.sqrt(np.mean(x[i * hop:(i + 1) * hop] ** 2) + 1e-12) for i in range(len(x) // hop)])
    d = 20 * np.log10(e / e.max())
    on, i = [], 0
    while i < len(d):
        if d[i] > thr_db:
            j = i
            while j < len(d) and d[j] > thr_db - 6:
                j += 1
            on.append((e[i:j].max(), i * 0.01))
            i = j + 20
        else:
            i += 1
    ts = sorted(t for _, t in sorted(on, reverse=True)[:n])
    return [fade(load(path, max(0.0, t - pre), length), 0.002, 0.15) for t in ts]


def mix(*parts):
    """Camadas: (som, começo em segundos, ganho em dB)."""
    end = max(int(at * SR) + len(s) for s, at, _ in parts)
    out = np.zeros(end, np.float32)
    for s, at, gdb in parts:
        o = int(at * SR)
        out[o:o + len(s)] += s * db(gdb)
    return out


# ------------------------------------------------------------------ cadeia de mixagem
def chain(x, hp=60, lp=None, low=None, presence=None, comp=(-20, 3.0), verb=0.0, room=0.22, tail=0.35):
    """EQ -> compressor -> reverb de sala -> limitador. low/presence = (Hz, dB)."""
    fx = [HighpassFilter(cutoff_frequency_hz=hp), HighpassFilter(cutoff_frequency_hz=hp)]   # 24 dB/oitava: grave limpo
    if low:
        fx.append(LowShelfFilter(cutoff_frequency_hz=low[0], gain_db=low[1], q=0.7))
    if presence:
        fx.append(PeakFilter(cutoff_frequency_hz=presence[0], gain_db=presence[1], q=1.0))
    if lp:
        fx.append(LowpassFilter(cutoff_frequency_hz=lp))
    if comp:
        fx.append(Compressor(threshold_db=comp[0], ratio=comp[1], attack_ms=3, release_ms=90))
    if verb > 0:
        x = np.concatenate([x, np.zeros(int(tail * SR), np.float32)])
        fx.append(Reverb(room_size=room, damping=0.55, wet_level=verb, dry_level=1.0, width=0.0))
    y = Pedalboard(fx)(x[None, :].astype(np.float32), SR)[0]
    return y


def limit(x, ceiling=-1.0):
    """Limitador transparente: segura os picos e deixa o teto em `ceiling` dBFS (o Limiter do JUCE sobe o ganho)."""
    y = Pedalboard([Limiter(threshold_db=-4.0, release_ms=60)])(x[None, :].astype(np.float32), SR)[0]
    pk = np.abs(y).max()
    return (y * (db(ceiling) / pk)).astype(np.float32) if pk > 1e-9 else y


def peak_rms(x, w=0.1):
    w = int(w * SR)
    if len(x) < w:
        x = np.concatenate([x, np.zeros(w - len(x), np.float32)])
    c = np.cumsum(np.concatenate([[0.0], x.astype(np.float64) ** 2]))
    return np.sqrt((c[w:] - c[:-w]) / w).max()


def loud(x, target):
    """Leva o trecho mais forte (janela de 100 ms) a `target` dB RMS, sem passar de -1 dBFS de pico."""
    for _ in range(3):   # o limitador come um pouco do RMS: ajusta de novo
        x = x * (db(target) / max(peak_rms(x), 1e-9))
        if np.abs(x).max() > db(-1.0):
            x = limit(x)
    return trim(x.astype(np.float32), -60)


def loop(x, xfade=2.0):
    n = int(xfade * SR)
    head, body, tail = x[:n], x[n:-n], x[-n:]
    t = np.linspace(0, 1, n)
    return np.concatenate([body, tail * np.cos(t * np.pi / 2) + head * np.sin(t * np.pi / 2)]).astype(np.float32)


def write(x, path, q=6):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm = np.clip(x, -1, 1).astype(np.float32).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
                    "-c:a", "libvorbis", "-q:a", str(q), path], input=pcm, check=True)


# ------------------------------------------------------------------ níveis por categoria (dB RMS, janela 100 ms)
SWING, BOW, SPELL, IMPACT, CRIT, VOICE_BIG, VOICE, VOICE_SMALL = -17, -16, -15, -12, -11, -12, -13.5, -15
HERO_VOX, LEVEL, BUFF, COIN, STEP, UI = -15, -13, -16, -19, -24, -21


def sfx():
    """Chave do jogo -> [(som pronto)]. O jogo sorteia uma variação a cada toque."""
    R = {}

    def S(key, target, sounds, **ch):
        R[key] = [loud(chain(s, **ch), target) for s in sounds]

    swoosh = lambda i, p, g=0.0: cut(oc("SWSH_Swoosh", i), p=p, g=db(g), fout=0.12)
    whoosh_s = lambda i, p=1.0: cut(oc("WHSH_WhooshSmall", i), p=p, fout=0.06)
    whoosh_m = lambda i, p=1.0: cut(oc("WHSH_WhooshMedium", i), p=p, fout=0.08)
    punch = lambda kind, i, p=1.0: cut(oc("FGHTImpt_Punch" + kind, i), p=p, fout=0.12)
    slice_ = cut(KR + "knife_slice.ogg", fout=0.05)
    slice2 = cut(KR + "knife_slice_2.ogg", fout=0.05)

    # ---- herói: o gesto (ar cortado pela arma; espada pesada mais grave e larga, lâminas rápidas mais agudas)
    S("swing_heavy", SWING, [mix((swoosh(i, 0.88), 0, 0), (whoosh_m(j, 0.75), 0.04, -9)) for i, j in ((1, 1), (3, 3), (4, 1))],
      hp=70, low=(180, 3), presence=(2500, 2))
    S("swing_light", SWING, [swoosh(i, 1.18) for i in (2, 5, 6)], hp=220, presence=(4000, 2))
    S("swing_light2", SWING, [swoosh(i, 1.35) for i in (1, 3, 4)], hp=300, presence=(5000, 2))
    S("swing_spear", SWING, [mix((swoosh(i, 0.95), 0, 0), (whoosh_m(j, 0.8), 0.02, -6)) for i, j in ((4, 2), (6, 3), (2, 1))],
      hp=120, presence=(3000, 2))
    bows = hits(TEX1 + "English Longbow Shoot.wav", 3, 0.01, 0.55) + hits(TEX1 + "Scythian Recurve Shoot.wav", 2, 0.01, 0.55)
    S("bow", BOW, [mix((trim(b), 0, 0), (whoosh_s(k % 3 + 1, 1.4), 0.03, -10)) for k, b in enumerate(bows)], hp=150, verb=0.06)
    S("punch", SWING, [mix((whoosh_s(i, 0.95), 0, 0), (cut(oc("CLOTHMvmt_ClothingMovement", i), 0, 0.3, fout=0.1), 0, -12)) for i in (1, 2, 3)],
      hp=120)
    S("spell_arcane", SPELL, [cut(oc("MAGSpel_MagicSpell", 1), 0, 1.5, fout=0.5), cut(oc("MAGSpel_MagicSpell", 2), 0, 1.5, fout=0.5),
                              mix((cut(oc("MAGSpel_MagicSpell", 1), 0, 1.5, p=1.12, fout=0.5), 0, 0), (cut(MAGIC + "Wind Spell Impact 1_s07.wav", 0, 1.2, fout=0.5), 0, -8))],
      hp=150, verb=0.12, room=0.4)
    S("spell_fire", SPELL, [cut(MAGIC + f"Fire Spell Impact {i}_s06.wav", 0, 1.4, fout=0.5) for i in (2, 3, 5)], hp=110, presence=(2500, 2), verb=0.08)
    S("spell_summon", SPELL, [mix((cut(oc("MAGSpel_MagicSpell", 2), 0, 1.8, p=0.85, fout=0.6), 0, 0),
                                  (cut(oc("CREAMnstr_CreatureCreepyReverb", k), 0, 1.6, p=1.2, fout=0.6), 0.1, -14)) for k in (1, 3)],
      hp=100, verb=0.15, room=0.5)

    # ---- impactos: corpo (soco grave) + material (corte, pedra, vidro...) na mesma sala
    S("hit_flesh", IMPACT, [mix((punch("Regular", i), 0, 0), (s, 0, -9)) for i, s in ((1, slice_), (3, slice2), (5, slice_), (6, slice2))],
      hp=55, low=(120, 3), presence=(3500, 2), verb=0.07)
    clash = cut(oc("WEAPSwrd_SwordClash"), 0, 0.5, fout=0.25)
    S("hit_crit", CRIT, [mix((punch("Harder", i), 0, 0), (clash, 0, -6), (s, 0, -8)) for i, s in ((1, slice_), (3, slice2), (4, slice_))],
      hp=45, low=(110, 4), presence=(3000, 2), verb=0.1)
    S("hit_stone", IMPACT, [mix((cut(KI + f"impact_mining_00{i}.ogg"), 0, 0), (punch("Regular", i + 1), 0, -8), (cut(EXPL + f"Break_0{b}_s01.wav"), 0, -7)) for i, b in ((0, 1), (1, 2), (3, 1))],
      hp=70, verb=0.07)
    S("hit_glass", IMPACT, [mix((cut(KI + f"impact_glass_medium_00{i}.ogg"), 0, 0), (punch("Regular", i + 2), 0, -10), (cut(EXPL + f"BreakGlass_0{i + 1}_s01.wav"), 0, -9)) for i in (0, 1, 2)],
      hp=120, verb=0.09)
    S("hit_magic", IMPACT, [mix((cut(MAGIC + f"Ice Spell Impact {i}_s06.wav", 0, 0.8, fout=0.35), 0, 0), (punch("Regular", i), 0, -10)) for i in (2, 3, 4)],
      hp=100, verb=0.1)
    S("hit_arrow", IMPACT, [mix((cut(KI + f"impact_wood_light_00{i}.ogg"), 0, 0), (punch("Regular", i + 4), 0, -6), (slice_ if i % 2 else slice2, 0, -11)) for i in (0, 1, 2)],
      hp=90, verb=0.06)
    S("hit_fire", IMPACT, [mix((cut(MAGIC + f"Fire Spell Impact {i}_s06.wav", 0, 1.2, fout=0.5), 0, 0), (punch("Harder", 2 + i), 0, -6)) for i in (1, 2)],
      hp=80, presence=(2500, 2), verb=0.08)
    S("miss", SWING - 4, [swoosh(i, 1.25) for i in (2, 4, 5)], hp=250)
    S("hurt_me", IMPACT - 1, [mix((punch("Harder", i), 0, 0), (cut(oc("CLOTHMvmt_MovementWithGear", i), 0, 0.4, fout=0.15), 0.01, -10)) for i in (1, 2, 3)],
      hp=50, low=(110, 3), verb=0.06)
    S("death_me", IMPACT - 2, [mix((punch("Hardest", 1, 0.7), 0, 0), (cut(oc("CLOTHMvmt_MovementWithGear", i), 0, 0.8, fout=0.3), 0.08, -6)) for i in (2, 4)],
      hp=40, low=(100, 3), verb=0.1)
    # vozes do herói (dor e queda), por voz masculina ou feminina
    S("hurt_male", HERO_VOX, [cut(oc(f"VOXMale_Male0{m}Hurt", i), fout=0.08) for m in (1, 2) for i in (1, 2, 3)], hp=120, verb=0.06)
    S("hurt_female", HERO_VOX, [cut(oc(f"VOXFem_Female0{m}Hurt", i), fout=0.08) for m in (1, 2) for i in (1, 2, 3)], hp=150, verb=0.06)
    S("death_male", HERO_VOX + 1, [cut(oc("VOXMale_Male01Death", i), fout=0.3) for i in (1, 2)] + [cut(oc("VOXMale_Male02Death", 1), fout=0.3)],
      hp=110, verb=0.12)
    S("death_female", HERO_VOX + 1, [cut(oc("VOXFem_Female01Death", i), fout=0.3) for i in (1, 2)] + [cut(oc("VOXFem_Female02Death", 1), fout=0.3)],
      hp=140, verb=0.12)

    # ---- progresso e habilidades
    S("levelup", LEVEL, [mix((cut(oc("MUSCStngr_LevelUpOrchestral", i), fout=0.6), 0, 0), (cut(MAGIC + "Healing Spell Impact 4_s08.wav", fout=0.6), 0, -12)) for i in (1, 2)],
      hp=60, comp=(-16, 2.0), verb=0.1, room=0.5)
    S("cast_buff", BUFF, [cut(f"oga8/cure-magic/curemagic/Cure{i}.wav", fout=0.4) for i in (1, 3, 5)], hp=150, verb=0.15, room=0.45)
    S("cast_banner", BUFF + 1, [mix((cut(oc("CLOTHFlp_FlagFlapping_LOOP"), a, 1.0, fout=0.4), 0, 0), (cut(oc("MUSCStngr_MarchingSnare"), 0, 1.3, fout=0.4), 0.05, -5)) for a in (2.0, 9.0)],
      hp=90, verb=0.1)
    boom = cut("wyz/booms-and-bass/boom-hit-medium.wav", fout=0.4)
    S("area", IMPACT, [mix((cut(MAGIC + f"Earth Spell Impact {i}_s07.wav", 0, 1.4, fout=0.5), 0, 0), (punch("Hardest", 1, 0.8), 0, -5), (boom, 0, -10)) for i in (3, 4)],
      hp=50, low=(90, 1.5), verb=0.12, room=0.4)

    # ---- mundo e interface
    S("coin", COIN, [cut(oc("OBJCoin_CoinDrop"), fout=0.2), cut(KR + "handle_coins.ogg", fout=0.1), cut(KR + "handle_coins_2.ogg", fout=0.1)], hp=300)
    S("step", STEP, [cut(oc("FEETHmn_ShoeGrassWalk", i), fout=0.08) for i in range(1, 6)] + [cut(oc("FEETHmn_ShoeDirtWalk", i), fout=0.08) for i in range(1, 6)],
      hp=90, comp=None)
    S("ui_click", UI, [cut(oc("UIClick_WoodConfirm")), cut(oc("UIClick_WoodBack"))], hp=150, comp=None)
    S("ui_open", UI, [cut(oc("UIClick_WoodOpen")), cut(oc("OBJBook_BookOpen"), 0, 0.6, fout=0.2)], hp=150, comp=None)
    S("ui_close", UI, [cut(oc("UIClick_WoodClose")), cut(oc("OBJBook_BookClose"), fout=0.15)], hp=150, comp=None)

    # ---- monstros: vozes de criatura de estúdio, engrossadas ou afinadas por família, mais camadas de material
    V = dict(hp=60, verb=0.1, room=0.3)
    # fera (lobo, lince, serpe)
    S("beast_aggro", VOICE, [cut(oc("ANMLDog_DogBark", i), p=0.72, fout=0.15) for i in (1, 2)] + [cut(oc("CREAHmn_TrollEffort"), p=1.12, fout=0.15)], **V)
    S("beast_attack", VOICE, [cut(oc("CREAHmn_TrollEffort"), p=1.2, fout=0.1), cut(oc("CREAHmn_TrollHurt", 2), p=1.15, fout=0.1),
                              cut(oc("ANMLDog_DogBark", 1), p=0.8, fout=0.1)], **V)
    S("beast_hurt", VOICE_SMALL, [cut(oc("ANMLFarm_PigSqueal", 1), 0, 0.35, p=0.82, fout=0.1), cut(oc("CREAHmn_TrollHurt", 1), p=1.2, fout=0.1),
                                  cut(oc("CREAHmn_TrollHurt", 3), p=1.25, fout=0.1)], **V)
    S("beast_death", VOICE, [cut(oc("CREAHmn_TrollDeath", i), p=1.1, fout=0.3) for i in (1, 2)], **V)
    # gosma
    sq = lambda i, a, d, p=1.0: cut(oc("GORESqsh_BloodSquishSlimy", i), a, d, p=p, fout=0.15)
    S("slime_aggro", VOICE_SMALL, [sq(1, 0, 0.8, 0.8), sq(3, 0, 0.8, 0.85)], hp=80, verb=0.08)
    S("slime_attack", VOICE_SMALL, [mix((cut(MELEE + "slime_000_48kHz_16bit_0.5s_691d3d6e_s04.wav"), 0, 0), (sq(2, 0, 0.4), 0, -4)), sq(4, 0.5, 0.5, 1.1)], hp=80, verb=0.08)
    S("slime_hurt", VOICE_SMALL, [sq(2, 1.0, 0.35, 1.2), sq(4, 2.0, 0.35, 1.2)], hp=100, verb=0.06)
    S("slime_death", VOICE, [sq(3, 0, 1.6, 0.9), sq(1, 0.5, 1.6, 0.85)], hp=60, low=(140, 2), verb=0.1)
    # inseto (aranhas, rastejante)
    scr = "oga8/evil-screech-1/evil_screech_1.ogg"
    S("insect_aggro", VOICE, [mix((cut(scr, 0.4, 0.6, p=1.6, fout=0.2), 0, 0), (cut(oc("CREAMnstr_CreatureCreepy", 2), 0, 0.8, p=1.6, fout=0.3), 0, -6)),
                              mix((cut(scr, 1.9, 0.7, p=1.7, fout=0.25), 0, 0), (cut(oc("CREAMnstr_CreatureCreepy", 3), 0, 0.8, p=1.7, fout=0.3), 0, -6))], hp=200, verb=0.08)
    S("insect_attack", VOICE_SMALL, [mix((cut(scr, 3.85, 0.35, p=1.9, fout=0.12), 0, 0), (slice_, 0, -4)), mix((cut(scr, 5.65, 0.35, p=2.0, fout=0.12), 0, 0), (slice2, 0, -4))], hp=200, verb=0.06)
    S("insect_hurt", VOICE_SMALL - 2, [cut(scr, 0.45, 0.2, p=2.1, fout=0.08), cut(scr, 1.95, 0.2, p=2.2, fout=0.08)], hp=300, verb=0.05)
    S("insect_death", VOICE, [mix((cut(oc("GOREBone_BonesBreaking", i), 0, 0.7, p=1.35, fout=0.25), 0, 0), (cut(scr, t, 0.6, p=1.5, fout=0.3), 0.04, -6)) for i, t in ((1, 3.86), (3, 5.66))],
      hp=120, verb=0.08)
    # morto-vivo (ossadas, carniçal)
    S("undead_aggro", VOICE, [mix((cut(oc("VOXMale_GroanCreepy"), 0, 1.6, p=0.85, fout=0.5), 0, 0), (cut(oc("CHAINMvmt_ChainsCreepy", 2), 0, 1.2, fout=0.5), 0, -12)),
                              mix((cut(oc("VOXMale_GroanCreepy"), 2.0, 1.6, p=0.8, fout=0.5), 0, 0), (cut(oc("CHAINMvmt_ChainsCreepy", 1), 0, 1.2, fout=0.5), 0, -12))], **V)
    S("undead_attack", VOICE_SMALL, [mix((swoosh(i, 0.9), 0, 0), (cut(oc("GOREBone_BonesBreaking", 3), 0, 0.4, p=1.25, fout=0.15), 0.05, -8)) for i in (3, 5)], hp=90, verb=0.08)
    S("undead_hurt", VOICE_SMALL, [cut(BONE_D + f"{i}.wav", fout=0.15) for i in (3, 6)] + [cut(oc("GOREBone_SkullCracking"), 0, 0.5, fout=0.15)], hp=90, verb=0.07)
    S("undead_death", VOICE, [mix((cut(oc("GOREBone_BonesBreaking", 2), fout=0.3), 0, 0), (cut(oc("WOODImpt_LargeWoodDrop"), p=1.3, fout=0.3), 0.15, -7)),
                              mix((cut(oc("GOREBone_BonesBreaking", 1), fout=0.3), 0, 0), (cut(oc("WOODImpt_LargeWoodDrop"), p=1.4, fout=0.3), 0.12, -7))], **V)
    # pedra (golem, colosso)
    S("stone_aggro", VOICE_BIG, [mix((cut(oc("CREAHmn_GiantEffort"), 0, 1.8, p=0.78, fout=0.5), 0, 0), (cut(KI + "impact_mining_002.ogg", p=0.7), 0.1, -9))], hp=65, presence=(1200, 3), verb=0.12)
    S("stone_attack", VOICE, [mix((swoosh(i, 0.6), 0, 0), (punch("Hardest", 1, 0.6), 0.2, -3)) for i in (1, 4)], hp=60, presence=(1500, 2), verb=0.08)
    S("stone_hurt", VOICE_SMALL, [mix((cut(KI + f"impact_mining_00{i}.ogg", p=0.8), 0, 0), (cut(oc("CREAHmn_GiantHurt", j), 0, 0.6, p=0.85, fout=0.2), 0, -7)) for i, j in ((0, 1), (4, 2))],
      hp=70, presence=(1800, 3), verb=0.08)
    S("stone_death", VOICE_BIG, [mix((cut(oc("CREAHmn_GiantDeath", 1), 0, 2.4, p=0.82, fout=0.6), 0, 0), (cut(EXPL + "Break_01_s01.wav"), 0.3, -4), (boom, 0.3, -6))],
      hp=55, presence=(1500, 3), verb=0.12, room=0.4)
    # cristal (sentinela, vigia de gelo, soberano, estilhaço)
    S("crystal_aggro", VOICE, [mix((cut(oc("UIMisc_BelltreeUp"), 0, 1.6, fout=0.6), 0, 0), (cut(oc("CREAMnstr_CreatureCreepy", 3), 0, 1.2, p=1.4, fout=0.4), 0, -8))], hp=200, verb=0.15, room=0.45)
    S("crystal_attack", VOICE_SMALL, [mix((swoosh(i, 1.4), 0, 0), (cut(KI + f"impact_glass_light_00{k}.ogg"), 0.1, -6)) for i, k in ((2, 1), (6, 3))], hp=250, verb=0.08)
    S("crystal_hurt", VOICE_SMALL, [mix((cut(KI + f"impact_glass_light_00{i}.ogg"), 0, 0), (cut(EXPL + f"BreakGlass_0{i + 1}_s01.wav"), 0, -6)) for i in (0, 2)], hp=200, verb=0.1)
    S("crystal_death", VOICE, [mix((cut(EXPL + f"BreakGlass_0{i}_s01.wav"), 0, 0), (cut(oc("UIMisc_BelltreeDown"), 0, 1.6, fout=0.6), 0.05, -6)) for i in (1, 3)], hp=150, verb=0.15, room=0.45)
    # espírito (bruxa, mariposa, espectro, ecos)
    S("spirit_aggro", VOICE, [cut(oc("VOXWhsp_WhispersCreepyReverb"), 0, 1.8, fout=0.7), cut(oc("VOXFem_GroanCreepy"), 0, 1.6, fout=0.6)], hp=150, verb=0.15, room=0.6)
    S("spirit_attack", VOICE_SMALL, [mix((whoosh_m(i, 0.9), 0, 0), (cut(oc("VOXWhsp_WhispersCreepy"), a, 0.5, fout=0.2), 0, -6)) for i, a in ((1, 0.5), (3, 2.0))], hp=150, verb=0.12, room=0.5)
    S("spirit_hurt", VOICE_SMALL, [cut(oc("VOXFem_GroanCreepy"), a, 0.4, p=1.2, fout=0.15) for a in (0.3, 2.2)], hp=200, verb=0.1, room=0.5)
    S("spirit_death", VOICE, [mix((cut(oc("VOXScrm_FemaleScream"), 0, 1.6, p=0.8, fout=0.7), 0, 0), (cut("wyz/reverse-whooshes/reverse-whoosh-medium-soft.wav"), 0, -10))],
      hp=150, verb=0.2, room=0.7)
    # fogo (cria e arauto das cinzas)
    fire = lambda i, d=0.6: cut(MAGIC + f"Fire Spell Impact {i}_s06.wav", 0, d, fout=0.3)
    S("fire_aggro", VOICE_BIG, [mix((cut(oc("CREAHmn_OgreLaugh", i), 0, 1.6, p=0.85, fout=0.5), 0, 0), (fire(3, 1.2), 0, -8)) for i in (1, 3)], hp=50, low=(110, 2), verb=0.1)
    S("fire_attack", VOICE, [mix((cut(oc("CREAHmn_OgreEffort"), p=0.9, fout=0.15), 0, 0), (fire(5), 0, -6))], hp=50, verb=0.08)
    S("fire_hurt", VOICE_SMALL, [cut(oc("CREAHmn_OgreHurt", i), fout=0.12) for i in (1, 2, 3)], hp=60, verb=0.07)
    S("fire_death", VOICE_BIG, [mix((cut(oc("CREAHmn_OgreDeathScream"), 0, 1.8, fout=0.6), 0, 0), (cut(EXPL + "explosion_crunch_004_48kHz_16bit_1.98s_b6838d5c_s04.wav", fout=0.6), 0.2, -6))],
      hp=45, low=(100, 2), verb=0.1)
    return R


def ambient():
    """Loops longos de estúdio, tratados para ficar atrás do jogo (sem graves embolando, sem agudos chiando)."""
    A = {}

    def bed(path, at, dur, target, hp=120, lp=9000, xf=2.5, notch=None):
        x = load(path, at, dur)
        eq = [PeakFilter(cutoff_frequency_hz=f, gain_db=g, q=4.0) for f, g in (notch or [])]
        x = Pedalboard([HighpassFilter(cutoff_frequency_hz=hp), LowpassFilter(cutoff_frequency_hz=lp)] + eq + [
                        Compressor(threshold_db=-28, ratio=2.0, attack_ms=30, release_ms=400)])(x[None, :], SR)[0]
        x = loop(x, xf)
        rms = np.sqrt(np.mean(x ** 2))
        x = (x * db(target) / max(rms, 1e-9)).astype(np.float32)
        return limit(x, -6.0) if np.abs(x).max() > db(-6.0) else x

    A["noite"] = bed(oc("AMBForst_Night_LOOP"), 0, 45.0, -32, hp=150, lp=7000, notch=[(2860, -14), (2870, -6), (2000, -6)])   # grilos sem apito constante
    A["dia"] = bed(oc("WINDVege_LightWindTreesBirds_LOOP"), 0, 62.0, -30, hp=150, lp=9000)
    A["fonte"] = bed(oc("WATRFlow_CreekInWoods_LOOP"), 0, 44.5, -24, hp=200, lp=9000)
    A["poco"] = bed(oc("WATRLap_WaterLapping_LOOP"), 0, 44.5, -26, hp=200, lp=7000)
    A["forja"] = bed(oc("METLImpt_HammerAnvilAmbient"), 0, 15.1, -24, hp=120, lp=11000, xf=1.0)
    A["mercado"] = bed(oc("CRWDConv_Restaurant_LOOP"), 0, 47.5, -30, hp=180, lp=1600)   # burburinho de gente, sem palavras
    birds = [loud(chain(cut(oc("AMBForst_ForestBirds", i), fout=0.6), hp=400, comp=None), -28) for i in (1, 2, 3)]
    return A, {"passaro": birds}


def main():
    global SRC
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)
    SRC, out = sys.argv[1], sys.argv[2]
    demo_dir = sys.argv[3] if len(sys.argv) > 3 else None
    R = sfx()
    A, extra = ambient()
    R.update(extra)
    total = 0
    for key, variants in R.items():
        for i, v in enumerate(variants):
            write(v, os.path.join(out, "Sfx", f"{key}_{i}.ogg"))
            total += 1
    for key, v in A.items():
        write(v, os.path.join(out, "Ambiente", f"{key}.ogg"), q=4)
        total += 1
    print(f"{total} arquivos em {out}")
    if demo_dir:
        demo(R, A, demo_dir)


# ------------------------------------------------------------------ demo: uma luta curta como soa no jogo
def demo(R, A, out_dir):
    """Renderiza uma cena de ~40 s com os volumes do jogo, para ouvir sem abrir o Unity."""
    rng = np.random.default_rng(3)
    T = 36.0
    track = np.zeros(int(T * SR), np.float32)

    def put(key, t, vol=1.0, i=None):
        v = R[key]
        s = v[i if i is not None else rng.integers(len(v))]
        o = int(t * SR)
        n = min(len(s), len(track) - o)
        track[o:o + n] += s[:n] * vol

    noite = np.tile(A["noite"], 2)[:len(track)] * 0.55
    fonte = np.tile(A["fonte"], 2)[:len(track)]
    forja = np.tile(A["forja"], 4)[:len(track)]
    env = np.clip(1 - np.arange(len(track)) / (12 * SR), 0, 1)      # a vila some quando o herói sai dela
    track += noite + (fonte * 0.45 + forja * 0.3) * env
    t = 0.5
    for _ in range(14):                                            # andando para fora da vila
        put("step", t, 0.75); t += 0.42
    put("ui_open", 1.5, 0.8); put("ui_click", 2.6, 0.8); put("ui_close", 3.4, 0.8)
    t = 7.0
    put("beast_aggro", t, 0.8); t += 1.0
    for k in range(4):                                             # Guardião contra lobo
        put("swing_heavy", t, 0.8); put("hit_crit" if k == 2 else "hit_flesh", t + 0.07, 0.9); put("beast_hurt", t + 0.14, 0.6)
        put("beast_attack", t + 0.55, 0.7); put("hit_flesh", t + 0.67, 0.75); put("hurt_me", t + 0.67, 0.6)
        if k == 1:
            put("hurt_male", t + 0.7, 0.7)
        t += 1.05
    put("beast_death", t, 0.85); put("coin", t + 0.6, 0.8); t += 1.6
    put("levelup", t, 0.75); t += 2.6
    put("undead_aggro", t, 0.8); t += 1.2                          # arqueiro contra esqueleto
    for k in range(3):
        put("bow", t, 0.85); put("hit_arrow", t + 0.18, 0.85); put("undead_hurt", t + 0.24, 0.6); t += 0.8
    put("undead_death", t, 0.85); t += 1.6
    put("stone_aggro", t, 0.85); t += 1.4                          # arcanista contra golem
    for k in range(2):
        put("spell_arcane", t, 0.7); put("hit_magic", t + 0.28, 0.85); put("stone_hurt", t + 0.35, 0.6); t += 1.2
    put("spell_fire", t, 0.7); put("hit_fire", t + 0.28, 0.9); put("area", t + 0.3, 0.6); put("stone_death", t + 0.4, 0.9); t += 2.6
    put("spirit_aggro", t, 0.75); t += 1.0                          # lâmina contra espectro
    for k in range(3):
        put("swing_light", t, 0.8); put("swing_light2", t + 0.08, 0.6); put("hit_flesh", t + 0.1, 0.8); put("spirit_hurt", t + 0.16, 0.6); t += 0.7
    put("miss", t, 0.6); t += 0.6
    put("spirit_death", t, 0.85); put("coin", t + 0.8, 0.8)
    track = Pedalboard([Limiter(threshold_db=-1.0)])(track[None, :], SR)[0]
    os.makedirs(out_dir, exist_ok=True)
    pcm = np.clip(track, -1, 1).astype(np.float32).tobytes()
    path = os.path.join(out_dir, "demo_combate_kezor.mp3")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", "-c:a", "libmp3lame", "-q:a", "2", path],
                   input=pcm, check=True)
    print("demo:", path)


if __name__ == "__main__":
    main()
