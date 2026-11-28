#!/usr/bin/env python3
"""
Monta os sons do Kezor a partir de gravações livres (CC0) de Kenney e OpenGameArt.

Cada efeito do jogo (golpe, impacto, magia, grito de monstro, passo, moeda, ambiente da vila) é um corte
de uma gravação real, às vezes com duas camadas somadas e a altura ajustada. Nada aqui é sintetizado.
As fontes e licenças estão em docs/SONS_LICENCAS.md.

Uso:
    python montar_sons.py <pasta-das-fontes> <pasta-Assets/Resources/Audio>

A pasta das fontes tem as cópias CC0 do GitHub (veja docs/SONS_LICENCAS.md), com estes nomes:
    oga1   = github.com/zuujis-cc0/opengameart-sound-effect
    oga5   = github.com/zuujis-cc0/opengameart-sound-effect-5
    oga8   = github.com/zuujis-cc0/opengameart-sound-effect-8
    kenney = github.com/zuujis-cc0/opengameart-kenney-sound-effect
    kenney_impact = github.com/Boyquotes/kenney-impact-sounds-for-godot   (addons/kenney impact sounds)
    kenney_rpg    = github.com/Boyquotes/kenney-rpg-audio-for-godot       (addons/kenney rpg audio)
Precisa de ffmpeg e numpy.
"""
import os
import subprocess
import sys

import numpy as np

SR = 44100
SRC = ""

# ------------------------------------------------------------------ atalhos das fontes
C150 = "oga1/150-combat-sound-effects/150gameeffectsounds/"
MELEE, EXPL, MAGIC = C150 + "01_combat_melee/", C150 + "03_explosions_destruction/", C150 + "04_magic_spells/"
TEX = "oga8/medieval-sound-effects-weapon-textures/"
TEX1, TEX2 = TEX + "medieval_sfx_textures_1_of_2/", TEX + "medieval_sfx_textures_2_of_2/"
CLASH = "oga8/medieval-sound-effects-weapon-impacts/medieval_sfx_weapon_on_weapon_1_of_2/"
DEMON = "oga8/demon-voice/all_demon_lord_audio_files_0/DL "
ORC = "oga8/orc-voice/all_orc_commander_audio_files_0/"
KI = "kenney_impact/"
KR = "kenney_rpg/"
UI = "kenney/interface-sounds/kenney_interfaceSounds/Audio/"
BONE_D = "oga8/deep-bone-crackbreak-sfx/deep_breaks/Deep Break "
BONE_W = "oga8/fleshy-bone-breaksnap-sfx/wet_breaks/Wet Break "


# ------------------------------------------------------------------ leitura e edição
_cache = {}


def load(path, at=0.0, dur=None):
    """Lê um trecho como mono float32 a 44,1 kHz."""
    key = (path, at, dur)
    if key in _cache:
        return _cache[key].copy()
    full = os.path.join(SRC, path)
    if not os.path.exists(full):
        raise FileNotFoundError(full)
    cmd = ["ffmpeg", "-v", "error"]
    if at > 0:
        cmd += ["-ss", f"{at:.4f}"]
    if dur is not None:
        cmd += ["-t", f"{dur:.4f}"]
    cmd += ["-i", full, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"]
    x = np.frombuffer(subprocess.run(cmd, capture_output=True, check=True).stdout, np.float32).copy()
    _cache[key] = x
    return x.copy()


def pitch(x, p):
    """Muda a altura (e o tempo junto), como um disco mais rápido ou mais lento."""
    if abs(p - 1.0) < 1e-3:
        return x
    n = int(len(x) / p)
    return np.interp(np.arange(n) * p, np.arange(len(x)), x).astype(np.float32)


def fade(x, fin=0.004, fout=0.05):
    x = x.copy()
    a, b = min(len(x), int(fin * SR)), min(len(x), int(fout * SR))
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b) ** 2
    return x


def lowpass(x, hz):
    k = 1 - np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc += (v - acc) * k
        y[i] = acc
    return y


def trim(x, db=-45):
    """Corta o silêncio do começo e do fim."""
    if len(x) == 0:
        return x
    thr = np.abs(x).max() * 10 ** (db / 20)
    idx = np.where(np.abs(x) > thr)[0]
    if len(idx) == 0:
        return x
    s, e = max(0, idx[0] - int(0.003 * SR)), min(len(x), idx[-1] + int(0.02 * SR))
    return x[s:e]


def cut(path, at=0.0, dur=None, p=1.0, g=1.0, fout=0.06, lp=None):
    x = load(path, at, dur)
    if lp:
        x = lowpass(x, lp)
    return fade(trim(pitch(x, p)), 0.003, fout) * g


def hits(path, n, pre=0.08, length=0.45, skip=0, thr_db=-26):
    """Os n golpes mais fortes de uma gravação com vários golpes seguidos (cada um começando um pouco antes)."""
    x = load(path)
    hop = int(SR * 0.01)
    e = np.array([np.sqrt(np.mean(x[i * hop:(i + 1) * hop] ** 2) + 1e-12) for i in range(len(x) // hop)])
    db = 20 * np.log10(e / e.max())
    onsets, i = [], 0
    while i < len(db):
        if db[i] > thr_db:
            j = i
            while j < len(db) and db[j] > thr_db - 6:
                j += 1
            onsets.append((e[i:j].max(), i * 0.01))
            i = j + 20
        else:
            i += 1
    best = sorted(onsets, reverse=True)[skip:skip + n]
    return sorted(t for _, t in best), (pre, length)


def pieces(path, n, pre=0.08, length=0.45, skip=0, p=1.0, g=1.0, fout=0.12, thr_db=-26):
    ts, _ = hits(path, n, pre, length, skip, thr_db)
    return [fade(pitch(load(path, max(0.0, t - pre), length), p), 0.003, fout) * g for t in ts]


def mix(*parts):
    """Soma camadas: (som, começo em segundos, ganho)."""
    end = max(int(at * SR) + len(s) for s, at, _ in parts)
    out = np.zeros(end, np.float32)
    for s, at, gain in parts:
        o = int(at * SR)
        out[o:o + len(s)] += s * gain
    return out


def norm(x, peak_db=-1.0):
    m = np.abs(x).max()
    return x if m < 1e-9 else (x / m * 10 ** (peak_db / 20)).astype(np.float32)


def loop(x, xfade=1.0):
    """Fecha o loop: o fim entra no começo sem estalo."""
    n = int(xfade * SR)
    head, body, tail = x[:n], x[n:-n], x[-n:]
    t = np.linspace(0, 1, n)
    return np.concatenate([body, tail * np.cos(t * np.pi / 2) + head * np.sin(t * np.pi / 2)]).astype(np.float32)


def write(x, path, q=5):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm = np.clip(x, -1, 1).astype(np.float32).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
                    "-c:a", "libvorbis", "-q:a", str(q), path], input=pcm, check=True)


# ------------------------------------------------------------------ receitas
def sfx():
    """Chave do jogo -> lista de variações (o jogo sorteia uma a cada toque)."""
    R = {}
    swing_axe = TEX2 + "Axe Swing.wav"
    swing_dag = TEX2 + "Dagger Swing.wav"
    swing_kat = TEX1 + "Katana Swing.wav"
    swing_seax = TEX2 + "Seax Swing.wav"
    clash = CLASH + "Dagger Norse Sword Blade on Blade.wav"

    # ---- herói: o gesto
    R["swing_heavy"] = pieces(swing_axe, 3, 0.18, 0.6, p=0.92)
    R["swing_light"] = pieces(swing_dag, 3, 0.12, 0.42)
    R["swing_light2"] = pieces(swing_kat, 3, 0.12, 0.42, p=1.08)
    R["swing_spear"] = [cut(TEX2 + "Spear Swing.wav", 0.0, 0.7, fout=0.25), cut(TEX2 + "Spear Swing.wav", 2.15, 0.7, fout=0.25)] \
        + pieces(swing_seax, 1, 0.15, 0.5)
    R["bow"] = pieces(TEX1 + "English Longbow Shoot.wav", 3, 0.03, 0.55) + pieces(TEX1 + "Scythian Recurve Shoot.wav", 1, 0.03, 0.55)
    R["punch"] = pieces(swing_seax, 3, 0.12, 0.38, skip=1, p=0.9)
    R["spell_arcane"] = [cut(MAGIC + f"Wind Spell Impact {i}_s07.wav", 0, 1.3, fout=0.5) for i in (1, 2, 3)]
    R["spell_fire"] = [cut(MAGIC + f"Fire Spell Impact {i}_s06.wav", 0, 1.5, fout=0.5) for i in (3, 4, 5)]
    R["spell_summon"] = [cut(MAGIC + f"Earth Spell Impact {i}_s07.wav", 0, 1.6, fout=0.6) for i in (2, 4)] \
        + [cut(MAGIC + "Plant Spell Impact 1_s07.wav", 0, 1.6, fout=0.6)]

    # ---- impactos
    slice_ = MELEE + "knife_slice_48kHz_16bit_0.51s_9438a1db_s03.wav"
    R["hit_flesh"] = [mix((cut(MELEE + f), 0, 1.0), (cut(slice_, g=0.35), 0, 1.0)) for f in (
        "impact_punch_medium_000_48kHz_16bit_0.36s_bd322da3_s02.wav",
        "impact_punch_medium_002_48kHz_16bit_0.42s_212c5895_s02.wav",
        "impact_punch_medium_004_48kHz_16bit_0.44s_0fa50bbe_s02.wav")] \
        + [mix((cut(KI + "impact_punch_medium_001.ogg"), 0, 1.0), (cut(KR + "knife_slice_2.ogg", g=0.35), 0, 1.0))]
    clang = pieces(clash, 3, 0.01, 0.5, fout=0.3)
    R["hit_crit"] = [mix((cut(MELEE + f), 0, 1.0), (c, 0, 0.55)) for f, c in zip((
        "impact_punch_heavy_000_48kHz_16bit_0.65s_8fb58ba7_s02.wav",
        "impact_punch_heavy_002_48kHz_16bit_0.46s_565485a1_s02.wav",
        "impact_punch_heavy_004_48kHz_16bit_0.54s_b437d453_s02.wav"), clang)]
    R["hit_stone"] = [cut(KI + f"impact_mining_00{i}.ogg") for i in (0, 1, 3)]
    R["hit_glass"] = [cut(KI + f"impact_glass_medium_00{i}.ogg") for i in (0, 1, 2)]
    R["hit_magic"] = [cut(MAGIC + f"Ice Spell Impact {i}_s06.wav", 0, 0.9, fout=0.4) for i in (1, 2, 3)]
    R["hit_arrow"] = [mix((cut(KI + f"impact_wood_light_00{i}.ogg"), 0, 0.8), (cut(KI + f"impact_soft_medium_00{i}.ogg"), 0, 1.0)) for i in (0, 1, 2)]
    R["hit_fire"] = [mix((cut(MAGIC + f"Fire Spell Impact {i}_s06.wav", 0, 1.4, fout=0.5), 0, 1.0),
                         (cut(EXPL + "explosion_crunch_000_48kHz_16bit_0.78s_e875be4e_s04.wav"), 0, 0.5)) for i in (1, 2)]
    R["miss"] = pieces(swing_dag, 3, 0.1, 0.35, skip=3, p=1.12)
    R["hurt_me"] = [mix((cut(KI + f"impact_punch_heavy_00{i}.ogg"), 0, 1.0), (cut(KI + f"impact_plate_light_00{i}.ogg"), 0.02, 0.35)) for i in (0, 1, 2)]
    R["death_me"] = [mix((cut(KI + "impact_soft_heavy_000.ogg"), 0, 1.0), (cut(KI + "impact_plate_medium_001.ogg"), 0.14, 0.6),
                         (cut(KI + "impact_metal_light_002.ogg"), 0.3, 0.35)),
                     mix((cut(KI + "impact_soft_heavy_002.ogg"), 0, 1.0), (cut(KI + "impact_plate_medium_003.ogg"), 0.16, 0.6),
                         (cut(KI + "impact_metal_light_000.ogg"), 0.34, 0.35))]

    # ---- progresso e habilidades
    R["levelup"] = [cut(MAGIC + "Healing Spell Impact 4_s08.wav", fout=0.6), cut(MAGIC + "Healing Spell Impact 10_s08.wav", fout=0.6)]
    R["cast_buff"] = [cut(f"oga8/cure-magic/curemagic/Cure{i}.wav", fout=0.4) for i in (1, 3, 5)]
    R["cast_banner"] = [mix((cut(KI + f"impact_bell_heavy_00{i}.ogg", fout=0.4), 0, 1.0), (cut(KR + f"cloth_{j}.ogg"), 0.05, 0.6)) for i, j in ((0, 1), (2, 3))]
    R["area"] = [mix((cut(EXPL + f, fout=0.4), 0, 1.0), (cut(MAGIC + "Earth Spell Impact 3_s07.wav", 0, 1.0, fout=0.5), 0, 0.5)) for f in (
        "explosion_crunch_001_48kHz_16bit_1.36s_5803a7f0_s04.wav",
        "explosion_crunch_002_48kHz_16bit_1.26s_e0852ce0_s04.wav",
        "explosion_crunch_003_48kHz_16bit_1.55s_4b973e9c_s04.wav")]

    # ---- mundo e interface
    R["coin"] = [cut(KR + "handle_coins.ogg"), cut(KR + "handle_coins_2.ogg"), cut("oga8/gold-coin-6/coin.flac", fout=0.2)]
    R["step"] = [cut(KI + f"footstep_grass_00{i}.ogg", fout=0.04) for i in range(5)]
    UIS = "kenney/51-ui-sound-effects-buttons-switches-and-clicks/UI_SFX_Set/"
    R["ui_click"] = [cut(UIS + "click1.wav", fout=0.02), cut(UIS + "click3.wav", fout=0.02), cut(KR + "metal_click.ogg", fout=0.03)]
    R["ui_open"] = [cut(UI + "open_001.ogg", fout=0.03), cut(KR + "book_open.ogg", fout=0.05)]
    R["ui_close"] = [cut(UI + "close_001.ogg", fout=0.03), cut(KR + "book_close.ogg", fout=0.05)]

    # ---- monstros: alerta, ataque, dor e morte por família de voz
    growl = lambda n, p, **k: cut(DEMON + f"DEMON GROWL {n}.wav", p=p, fout=0.2, **k)
    snarl = lambda n, p, **k: cut(DEMON + f"DEMON SNARL {n}.wav", p=p, fout=0.15, **k)
    dies = DEMON + "DEMON DIES.wav"
    # fera (lobo, lince, serpe)
    R["beast_aggro"] = [growl(1, 1.22), growl(3, 1.3), cut("oga8/dog-growl/dog-growl.ogg", p=0.9, fout=0.15)]
    R["beast_attack"] = [snarl(3, 1.3), cut(ORC + "ORC FIGHT 2.wav", p=1.35, fout=0.1), cut(ORC + "ORC FIGHT.wav", p=1.4, fout=0.1)]
    R["beast_hurt"] = [cut("oga1/boar-0/boar_0.ogg", fout=0.1), cut(ORC + "ORC GRUNT 1.wav", p=1.4, fout=0.06), cut(ORC + "ORC GRUNT 2.wav", p=1.45, fout=0.06)]
    R["beast_death"] = [cut(dies, 0.8, 0.75, p=1.2, fout=0.3), cut(ORC + "ORC DIES.wav", 0.05, 0.55, p=1.3, fout=0.25)]
    # gosma
    slime0 = MELEE + "slime_000_48kHz_16bit_0.5s_691d3d6e_s04.wav"
    slime1 = MELEE + "slime_001_48kHz_16bit_4.71s_56e4987f_s04.wav"
    R["slime_aggro"] = [cut(slime0, p=0.85), cut(slime1, 0, 0.9, fout=0.3)]
    R["slime_attack"] = [mix((cut("oga8/slime-jump-effect/slime_jump.wav", p=0.8), 0, 1.0), (cut(slime0, p=1.2), 0.03, 0.8)), cut(slime1, 1.0, 0.6, fout=0.2)]
    R["slime_hurt"] = [cut(slime0, p=1.3), cut(BONE_W + "9.wav", p=1.2, g=0.8)]
    R["slime_death"] = [mix((cut(slime1, 2.0, 1.2, fout=0.4), 0, 1.0), (cut(BONE_W + "1.wav"), 0, 0.6))]
    # inseto (aranhas, rastejante)
    scr = "oga8/evil-screech-1/evil_screech_1.ogg"
    cic = "oga1/cicada-sounds/082526-cicadasounds_0.ogg"
    R["insect_aggro"] = [mix((cut(scr, 0.4, 0.6, p=1.6, fout=0.2), 0, 0.8), (cut(cic, 3.0, 0.7, fout=0.3), 0, 0.6)), cut(scr, 1.9, 0.7, p=1.7, fout=0.25)]
    R["insect_attack"] = [mix((cut(scr, 3.85, 0.4, p=1.9, fout=0.15), 0, 0.8), (cut(slice_), 0, 0.6)), mix((cut(scr, 5.65, 0.4, p=2.0, fout=0.15), 0, 0.8), (cut(KR + "knife_slice.ogg"), 0, 0.6))]
    R["insect_hurt"] = [cut(scr, 0.45, 0.2, p=2.1, fout=0.08), cut(scr, 1.95, 0.2, p=2.2, fout=0.08)]
    R["insect_death"] = [mix((cut(BONE_W + "2.wav"), 0, 1.0), (cut(scr, 3.86, 0.7, p=1.5, fout=0.3), 0.05, 0.6)),
                         mix((cut(BONE_W + "4.wav"), 0, 1.0), (cut(scr, 5.66, 0.7, p=1.45, fout=0.3), 0.05, 0.6))]
    # morto-vivo (ossadas, carniçal)
    R["undead_aggro"] = [mix((cut(ORC + "ORC GROWL.wav", p=0.78, fout=0.3), 0, 1.0), (cut("oga8/horror-breathing/horrorbreathing/horrorbreathing/horrorbreathing.wav", 0, 1.2, fout=0.4), 0, 0.5)),
                         cut(ORC + "ORC GROWL 2.wav", p=0.8, fout=0.3)]
    R["undead_attack"] = [mix((s, 0, 1.0), (cut(BONE_D + "5.wav", g=0.5), 0.05, 1.0)) for s in pieces(swing_axe, 2, 0.15, 0.5, skip=3)]
    R["undead_hurt"] = [cut(BONE_D + f"{i}.wav") for i in (3, 6, 8)]
    R["undead_death"] = [mix((cut(BONE_D + "1.wav"), 0, 1.0), (cut(BONE_D + "4.wav"), 0.18, 0.8), (cut(KI + "impact_wood_heavy_001.ogg"), 0.35, 0.6)),
                         mix((cut(BONE_D + "7.wav"), 0, 1.0), (cut(BONE_D + "2.wav"), 0.2, 0.8), (cut(KI + "impact_wood_heavy_003.ogg"), 0.4, 0.6))]
    # pedra (golem, colosso)
    rumble = "oga5/rumbling-sound-effects/rumbling18.wav"
    R["stone_aggro"] = [mix((cut(rumble, 10, 1.4, fout=0.5), 0, 1.0), (cut("oga8/stone-door/stone_door.ogg", fout=0.3), 0, 0.8))]
    R["stone_attack"] = [mix((s, 0, 1.0), (cut(KI + "impact_mining_004.ogg", p=0.8), 0.18, 0.8)) for s in pieces(swing_axe, 2, 0.18, 0.6, p=0.65)]
    R["stone_hurt"] = [cut(KI + f"impact_mining_00{i}.ogg", p=0.8) for i in (0, 2, 4)]
    R["stone_death"] = [mix((cut(EXPL + "Break_01_s01.wav"), 0, 0.8), (cut(rumble, 30, 1.4, fout=0.6), 0, 1.0), (cut(KI + "impact_mining_002.ogg", p=0.7), 0.12, 0.9))]
    # cristal (sentinela, vigia de gelo, soberano, estilhaço)
    R["crystal_aggro"] = [mix((cut(MAGIC + "Ice Spell Impact 4_s06.wav", 0, 1.2, fout=0.5), 0, 1.0), (cut(KI + "impact_glass_light_001.ogg"), 0, 0.6))]
    R["crystal_attack"] = [mix((s, 0, 1.0), (cut(KI + "impact_glass_light_003.ogg"), 0.12, 0.7)) for s in pieces(swing_dag, 2, 0.1, 0.4, p=1.25)]
    R["crystal_hurt"] = [cut(KI + f"impact_glass_heavy_00{i}.ogg") for i in (0, 2)]
    R["crystal_death"] = [mix((cut(EXPL + f"BreakGlass_0{i}_s01.wav"), 0, 1.0), (cut(MAGIC + "Ice Spell Impact 5_s06.wav", 0, 1.2, fout=0.5), 0, 0.6)) for i in (1, 3)]
    # espírito (bruxa, mariposa, espectro, ecos)
    ghost = "oga8/ghostly-humming/ghostly_humming.ogg"
    R["spirit_aggro"] = [cut(ghost, 0.6, 1.6, fout=0.6), cut(ghost, 19.7, 1.6, fout=0.6)]
    R["spirit_attack"] = [cut(MAGIC + f"Wind Spell Impact {i}_s07.wav", 0, 0.7, fout=0.3) for i in (4, 5)]
    R["spirit_hurt"] = [cut(ghost, 5.5, 0.35, p=1.25, fout=0.15), cut(ghost, 26.3, 0.35, p=1.25, fout=0.15)]
    R["spirit_death"] = [mix((cut(MAGIC + "Wind Spell Impact 2_s07.wav", 0, 1.6, fout=0.6), 0, 0.9), (cut(ghost, 41.3, 1.6, p=1.3, fout=0.8), 0, 0.8))]
    # fogo (cria e arauto das cinzas)
    fire = "oga5/some-sounds-0/all-in-one_0/fire seamless.wav"
    R["fire_aggro"] = [mix((growl(4, 0.8), 0, 1.0), (cut(fire, 2, 1.6, fout=0.5), 0, 0.5)), mix((growl(2, 0.82), 0, 1.0), (cut(fire, 6, 1.2, fout=0.5), 0, 0.5))]
    R["fire_attack"] = [mix((snarl(3, 0.85), 0, 0.9), (cut(MAGIC + "Fire Spell Impact 5_s06.wav", 0, 0.6, fout=0.3), 0, 0.6))]
    R["fire_hurt"] = [cut(ORC + "ORC GRUNT 2.wav", p=0.8, fout=0.08), cut(ORC + "ORC GRUNT 1.wav", p=0.78, fout=0.08)]
    R["fire_death"] = [mix((cut(dies, 0.8, 0.75, p=0.85, fout=0.3), 0, 1.0), (cut(EXPL + "explosion_crunch_004_48kHz_16bit_1.98s_b6838d5c_s04.wav", fout=0.6), 0.15, 0.7))]
    return R


def ambient():
    """Loops da vila (tocados em 3D perto de cada lugar) e o pio da noite."""
    A = {}
    river = "oga8/park-ambiences/park_ambience_river.wav"
    birds = "oga8/park-ambiences/park_ambience_birds.wav"
    fire = "oga5/some-sounds-0/all-in-one_0/fire seamless.wav"
    A["fonte"] = loop(load(river, 40, 13), 1.0)
    A["poco"] = loop(lowpass(load(river, 120, 11), 1800), 1.0)
    A["brasa"] = loop(load(fire, 0, 9), 1.0)
    A["mercado"] = loop(load(birds, 60, 16), 1.5)       # praça de dia: pássaros (não há burburinho CC0 bom)
    A["noite"] = loop(load("oga1/cicada-sounds/082526-cicadasounds2_0.ogg", 5, 13), 1.5)
    A["coruja"] = fade(trim(load("oga1/crow/crow_0.ogg")), 0.003, 0.1)   # corvo distante
    # forja: fogo baixo com marteladas no metal (três batidas e uma pausa)
    bed = load(fire, 10, 9) * 0.45
    parts = [(bed, 0, 1.0)]
    t = 0.4
    hammer = [cut(KI + f"impact_metal_heavy_00{i}.ogg") for i in range(5)]
    k = 0
    while t < 7.6:
        for _ in range(3):
            parts.append((hammer[k % 5], t, 0.9 if k % 3 else 1.0))
            t += 0.62
            k += 1
        t += 1.3
    A["forja"] = loop(mix(*parts)[:int(9 * SR)], 0.6)
    return A


def main():
    global SRC
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(1)
    SRC, out = sys.argv[1], sys.argv[2]
    total = 0
    for key, variants in sfx().items():
        for i, v in enumerate(variants):
            write(norm(v), os.path.join(out, "Sfx", f"{key}_{i}.ogg"))
            total += 1
    for key, v in ambient().items():
        write(norm(v, -3.0), os.path.join(out, "Ambiente", f"{key}.ogg"), q=4)
        total += 1
    print(f"{total} arquivos em {out}")


if __name__ == "__main__":
    main()
