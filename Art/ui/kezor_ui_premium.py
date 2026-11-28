"""
Kezor — kit de interface "clássico premium" (molduras de ferro e ouro velho, gemas de brasa, pedra escura).

Gera todos os PNGs de Assets/Resources/UI/Premium em 2x (a USS usa -unity-slice-scale: 0.5), mais uma folha de
contato em Art/previews/ui_premium_kit.png. Arte 100% original, desenhada em código (kzpaint.py).

    python Art/ui/kezor_ui_premium.py            # gera tudo
    python Art/ui/kezor_ui_premium.py window btn  # só algumas peças

As medidas abaixo estão em pixels da textura final (2x). SS é o supersample interno de anti-serrilhado.
"""
from __future__ import annotations

import math
import os
import sys

import numpy as np
from PIL import Image, ImageFont

sys.path.insert(0, os.path.dirname(__file__))
from kzpaint import *  # noqa: E402,F401,F403
from kzpaint import _box  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'UI', 'Premium')
SS = 2
FONT_DIR = os.path.join(ROOT, 'Assets', 'Resources', 'Fonts')

PIECES = {}
EMBER_LACQUER = ramp([(0, '#0c0202'), (0.35, '#2c0805'), (0.6, '#561208'), (0.8, '#8a2810'), (0.93, '#c85a26'), (1, '#ffb070')])


def piece(fn):
    PIECES[fn.__name__] = fn
    return fn


def put(name, cv: Canvas):
    os.makedirs(OUT, exist_ok=True)
    im = cv.image(SS)
    im.save(os.path.join(OUT, name + '.png'), optimize=True)
    return im


# ============================================================================================== blocos comuns

def stone_fill(cv, d, s, base=0.42, contrast=0.55, seed=3, warm=True):
    """pedra/couro escuro do miolo dos painéis: nuvem de baixa frequência + grão fino + veios."""
    w, h = cv.w, cv.h
    n1 = noise(w, h, 48 * s, seed, 4)
    n2 = noise(w, h, 7 * s, seed + 1, 2)
    g = grain(w, h, seed + 2)
    v = base + (n1 - 0.5) * contrast * 0.5 + (n2 - 0.5) * 0.10 + (g - 0.5) * 0.05
    col = (STONE if warm else IRON)(v)
    return col


def inner_shadow(d, at, width, k=0.7):
    """escurece o miolo perto da borda 'at' (sombra projetada pelo aro)."""
    t = np.clip(1 - (at - d) / width, 0, 1)   # d < at dentro
    return 1 - k * smooth(t) * (d < at + 0.5)


def molding(d, s, spec):
    """
    Aro moldado: lista de (tipo, centro, largura, material) em px finais medidos da borda para dentro.
    Retorna altura total e máscaras por material.
    """
    h = np.zeros_like(d)
    masks = {}
    for kind, c, wd, mat in spec:
        c, wd = c * s, wd * s
        if kind == 'bead':
            hh = bead(d, -c, wd / 2) * wd * 0.55
            m = (np.abs(d + c) <= wd / 2 + 0.5 * s).astype(F)
        elif kind == 'band':    # faixa levemente abaulada
            t = np.clip(1 - np.abs(d + c) / (wd / 2), 0, 1)
            hh = np.sqrt(t) * wd * 0.18
            m = (np.abs(d + c) <= wd / 2).astype(F)
        else:  # 'lip' — chanfro externo
            t = np.clip((-d - (c - wd / 2)) / wd, 0, 1)
            hh = np.sin(t * math.pi) * wd * 0.35
            m = (np.abs(d + c) <= wd / 2).astype(F)
        h = np.maximum(h, hh)
        masks[mat] = np.maximum(masks.get(mat, 0), m * (hh > 0.001))
    return h, masks


def shade_molding(cv, d, s, spec, outer_alpha, noise_amt=0.6, seed=5):
    h, masks = molding(d, s, spec)
    w, hh = cv.w, cv.h
    hammer = noise(w, hh, 3 * s, seed, 2) * noise_amt * s
    col = np.zeros((hh, w, 3), F)
    acc = np.zeros((hh, w), F)
    for mat, m in masks.items():
        hm = h + hammer * (0.35 if mat in (GOLD, GOLD_PALE, BRONZE) else 1.0)
        c = metal(hm, mat, 1.0, 26 if mat in (GOLD, GOLD_PALE) else 14, 0.6 if mat in (GOLD, GOLD_PALE) else 0.3)
        col += c * m[..., None]
        acc += m
    col = col / np.maximum(acc, 1e-6)[..., None]
    cv.over(col, np.clip(acc, 0, 1) * outer_alpha)
    return h


def corner_ornament_mask(W, H, s, size=46, rim=22, mirror=True, style='full'):
    """filigrana dos 4 cantos (desenhada no canto superior esquerdo e espelhada)."""
    ink = Ink(W, H, 3)
    k = s
    c = 17
    if style == 'full':
        # placa losango atrás da gema
        ink.poly([(c * k, (c - 14) * k), ((c + 14) * k, c * k), (c * k, (c + 14) * k), ((c - 14) * k, c * k)])
        # braços externos ao longo das bordas, terminando em voluta para dentro
        scroll_arm(ink, (c + 10) * k, (c - 6) * k, (size - 4) * k, 10.5 * k, 5.2 * k, 3.0 * k, 1.6 * k, bend=0.10, cw=True, turns=1.25)
        scroll_arm(ink, (c - 6) * k, (c + 10) * k, 10.5 * k, (size - 4) * k, 5.2 * k, 3.0 * k, 1.6 * k, bend=0.10, cw=False, turns=1.25)
        # braços internos (segunda fileira), mais curtos, enrolando para fora
        scroll_arm(ink, (c + 9) * k, (c + 5) * k, (c + 22) * k, (c + 7) * k, 3.8 * k, 2.6 * k, 1.3 * k, bend=0.22, cw=False, turns=1.2)
        scroll_arm(ink, (c + 5) * k, (c + 9) * k, (c + 7) * k, (c + 22) * k, 3.8 * k, 2.6 * k, 1.3 * k, bend=0.22, cw=True, turns=1.2)
        # folha de acanto na diagonal, com duas folhinhas laterais
        leaf(ink, (c + 8) * k, (c + 8) * k, math.radians(45), 15 * k, 3.4 * k)
        leaf(ink, (c + 13) * k, (c + 13) * k, math.radians(20), 7 * k, 1.8 * k)
        leaf(ink, (c + 13) * k, (c + 13) * k, math.radians(70), 7 * k, 1.8 * k)
        ink.circle((c + 25) * k, (c + 25) * k, 1.9 * k)
        # ponta de lança no canto externo
        ink.poly([(0.5 * k, 0.5 * k), (11 * k, 3.5 * k), (3.5 * k, 11 * k)])
    elif style == 'small':
        ink.poly([(c * k * 0.7, (c * 0.7 - 8) * k), ((c * 0.7 + 8) * k, c * 0.7 * k), (c * 0.7 * k, (c * 0.7 + 8) * k), ((c * 0.7 - 8) * k, c * 0.7 * k)])
        scroll_arm(ink, (c * 0.7 + 6) * k, (c * 0.7 - 4) * k, (size - 2) * k, 8 * k, 3 * k, 2.0 * k, 1.2 * k, bend=0.1, cw=True)
        scroll_arm(ink, (c * 0.7 - 4) * k, (c * 0.7 + 6) * k, 8 * k, (size - 2) * k, 3 * k, 2.0 * k, 1.2 * k, bend=0.1, cw=False)
    m = ink.mask()
    if mirror:
        m = np.maximum.reduce([m, m[:, ::-1], m[::-1, :], m[::-1, ::-1]])
    return m


def corner_centers(W, H, c):
    return [(c, c), (W - c, c), (c, H - c), (W - c, H - c)]


# ============================================================================================== janelas e painéis

def framed_panel(W, H, rim, ch, spec, orn='full', gem_r=4.6, gem_mat=EMBER_ENAMEL, fill_alpha=0.97, seed=3,
                 gem_c=17, inner_k=0.75, fill='stone'):
    s = SS
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    d = sd_rect(x, y, 0.5 * s, 0.5 * s, (W - 0.5) * s, (H - 0.5) * s, ch=ch * s)
    alpha = cover(d, s)
    # miolo
    if fill == 'stone':
        col = stone_fill(cv, d, s, seed=seed)
    else:
        col = fill(cv, d, s)
    col = col * inner_shadow(d, -rim * s, 16 * s, inner_k)[..., None]
    cv.over(col, alpha * fill_alpha)
    # aro
    shade_molding(cv, d, s, spec, alpha, noise_amt=0.35)
    # sombra do aro sobre o miolo (contato)
    if orn:
        m = corner_ornament_mask(cv.w, cv.h, s, style=orn)
        sh = shadow(m, 2.2 * s, 0.85, 1.2 * s, 2 * s)
        cv.over(np.zeros(3, F), sh * (1 - m))
        hm = relief(m, 1.3 * s) * 2.2 * s
        cv.over(metal(hm, GOLD, 1.0, 30, 0.75, rim_k=0.25), m)
        if gem_r > 0:
            for cx, cy in corner_centers(W, H, gem_c if orn == 'full' else gem_c * 0.7):
                gem(cv, cx * s, cy * s, gem_r * s, gem_r * s, gem_mat, facets=8, glow=0.6, bezel_w=1.8 * s,
                    shape='oval')
    return cv


WINDOW_SPEC = [
    ('lip', 1.5, 3.0, IRON),
    ('bead', 5.0, 3.6, GOLD),
    ('band', 10.5, 7.4, IRON_WARM),
    ('bead', 15.6, 2.6, GOLD),
    ('bead', 18.6, 2.2, BRONZE),
]


@piece
def window():
    """moldura das janelas (9-slice 48)."""
    cv = framed_panel(256, 256, 20, 12, WINDOW_SPEC, 'full', gem_r=5.4)
    return put('window', cv)


PANEL_SPEC = [
    ('lip', 1.2, 2.4, IRON),
    ('bead', 4.0, 3.0, GOLD),
    ('band', 7.6, 4.2, IRON_WARM),
    ('bead', 10.6, 2.0, BRONZE),
]


@piece
def panel():
    """painel menor (quadro do herói, alvo, cartões) — 9-slice 32."""
    cv = framed_panel(160, 160, 12, 8, PANEL_SPEC, 'small', gem_r=3.0, gem_c=17, seed=11)
    return put('panel', cv)


@piece
def panel_dark():
    """poço interno escuro (sub-caixas dentro das janelas) — 9-slice 20."""
    s = SS
    W = H = 96
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    d = sd_rect(x, y, 0.5 * s, 0.5 * s, (W - 0.5) * s, (H - 0.5) * s, ch=5 * s)
    a = cover(d, s)
    col = stone_fill(cv, d, s, base=0.22, contrast=0.4, seed=21)
    col *= inner_shadow(d, -3 * s, 12 * s, 0.85)[..., None]
    cv.over(col, a * 0.9)
    shade_molding(cv, d, s, [('bead', 1.6, 2.4, BRONZE)], a)
    # luz inferior (borda de baixo pega luz: sensação de rebaixo)
    lo = np.clip(1 - np.abs(d + 3.4 * s) / (0.8 * s), 0, 1) * (y > H * s * 0.55) * 0.35
    cv.add(hexc('#c49a5a'), lo)
    return put('panel_dark', cv)


@piece
def tooltip():
    """tooltip: vidro escuro quase preto, filete duplo de ouro e cantos com ponta — 9-slice 28."""
    s = SS
    W = H = 128
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    d = sd_rect(x, y, 0.5 * s, 0.5 * s, (W - 0.5) * s, (H - 0.5) * s, ch=6 * s)
    a = cover(d, s)
    n = noise(cv.w, cv.h, 30 * s, 41, 3)
    col = np.stack([0.035 + n * 0.02, 0.028 + n * 0.016, 0.03 + n * 0.012], -1).astype(F)
    col *= inner_shadow(d, -2 * s, 18 * s, 0.4)[..., None]
    cv.over(col, a * 0.94)
    shade_molding(cv, d, s, [('bead', 1.8, 2.2, GOLD), ('bead', 5.2, 1.4, BRONZE)], a, noise_amt=0.2)
    # pontas de lança nos cantos
    ink = Ink(cv.w, cv.h, 3)
    for (cx, cy, sx, sy) in [(0, 0, 1, 1), (W, 0, -1, 1), (0, H, 1, -1), (W, H, -1, -1)]:
        ink.poly([((cx + sx * 2) * s, (cy + sy * 2) * s), ((cx + sx * 16) * s, (cy + sy * 4.2) * s),
                  ((cx + sx * 7) * s, (cy + sy * 7) * s), ((cx + sx * 4.2) * s, (cy + sy * 16) * s)])
        ink.circle((cx + sx * 9) * s, (cy + sy * 9) * s, 1.6 * s)
    m = ink.mask()
    cv.over(np.zeros(3, F), shadow(m, 1.5 * s, 0.8, 0.8 * s, 1.2 * s) * (1 - m))
    cv.over(metal(relief(m, 1.0 * s) * 1.5 * s, GOLD, 1.0, 30, 0.8), m)
    return put('tooltip', cv)


@piece
def chat_bg():
    """fundo do chat: vidro fumê translúcido com filete fino de bronze — 9-slice 20."""
    s = SS
    W = H = 96
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    d = sd_rect(x, y, 0.5 * s, 0.5 * s, (W - 0.5) * s, (H - 0.5) * s, ch=4 * s)
    a = cover(d, s)
    grad = np.clip(y / cv.h, 0, 1)
    col = np.stack([0.03 + 0 * grad, 0.024 + 0 * grad, 0.022 + 0 * grad], -1).astype(F)
    cv.over(col, a * (0.6 + 0.16 * grad))
    shade_molding(cv, d, s, [('bead', 1.4, 1.8, BRONZE)], a * 0.85, noise_amt=0.1)
    return put('chat_bg', cv)


# ============================================================================================== título das janelas

@piece
def header():
    """faixa do título: chapa laqueada escura com frisos de ouro e pontas em losango — 9-slice L/R 72, T/B 20."""
    s = SS
    W, H = 384, 72
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    cy = H / 2
    # silhueta: retângulo com pontas em seta nas laterais
    pts = [(14, 10), (W - 14, 10), (W - 2, cy), (W - 14, H - 10), (14, H - 10), (2, cy)]
    d = sd_poly(x, y, [(px * s, py * s) for px, py in pts])
    a = cover(d, s)
    # sombra embaixo
    cv.over(np.zeros(3, F), shadow(a, 3 * s, 0.7, 0, 3 * s))
    # laca vermelho-sangue muito escura com veio vertical
    n = noise(cv.w, cv.h, 20 * s, 51, 3)
    v = 0.3 + 0.25 * (1 - np.abs(y / cv.h - 0.45) * 2) + (n - 0.5) * 0.12
    col = ramp([(0, '#070303'), (0.4, '#1d0a07'), (0.7, '#3a140c'), (1, '#6a2a14')])(v)
    col *= inner_shadow(d, -4 * s, 10 * s, 0.6)[..., None]
    cv.over(col, a)
    shade_molding(cv, d, s, [('lip', 1.0, 2.0, IRON), ('bead', 3.4, 2.8, GOLD), ('bead', 6.4, 1.4, BRONZE)], a, noise_amt=0.25)
    # brilho de brasa no centro inferior (fica esticado: brilho difuso)
    glow = np.exp(-((y / s - (H - 14)) / 6) ** 2) * (d < -4 * s) * 0.18
    cv.add(hexc('#ff6a1a'), glow)
    # pontas: losango de ouro com gema
    for cx in (16, W - 16):
        ink = Ink(cv.w, cv.h, 3)
        ink.poly([(cx * s, (cy - 15) * s), ((cx + 9) * s, cy * s), (cx * s, (cy + 15) * s), ((cx - 9) * s, cy * s)])
        m = ink.mask()
        cv.over(np.zeros(3, F), shadow(m, 1.5 * s, 0.8, s, 1.5 * s) * (1 - m))
        cv.over(metal(relief(m, 1.4 * s) * 2.4 * s, GOLD, 1.0, 30, 0.8), m)
        gem(cv, cx * s, cy * s, 3.4 * s, 5.4 * s, EMBER_ENAMEL, 6, glow=0.5, bezel_w=1.0 * s, shape='diamond')
    return put('header', cv)


@piece
def crest():
    """ornamento sob o título (escala para caber): filete com losango central e volutas."""
    s = SS
    W, H = 480, 44
    cv = Canvas(W * s, H * s)
    ink = Ink(cv.w, cv.h, 3)
    cx, cy = W / 2, H / 2
    ink.line([((cx - 200) * s, cy * s), ((cx - 34) * s, cy * s)], 0.6 * s, 2.4 * s)
    ink.line([((cx + 34) * s, cy * s), ((cx + 200) * s, cy * s)], 2.4 * s, 0.6 * s)
    for sg in (-1, 1):
        scroll_arm(ink, (cx + sg * 16) * s, (cy - 2) * s, (cx + sg * 44) * s, (cy - 9) * s, 4.5 * s, 2.4 * s, 1.2 * s, 0.18, cw=(sg < 0))
        scroll_arm(ink, (cx + sg * 16) * s, (cy + 2) * s, (cx + sg * 44) * s, (cy + 9) * s, 4.5 * s, 2.4 * s, 1.2 * s, 0.18, cw=(sg > 0))
        leaf(ink, (cx + sg * 46) * s, cy * s, 0 if sg > 0 else math.pi, 16 * s, 2.6 * s)
        ink.circle((cx + sg * 72) * s, cy * s, 2.0 * s)
        ink.circle((cx + sg * 206) * s, cy * s, 1.6 * s)
    ink.poly([(cx * s, (cy - 13) * s), ((cx + 14) * s, cy * s), (cx * s, (cy + 13) * s), ((cx - 14) * s, cy * s)])
    m = ink.mask()
    cv.over(np.zeros(3, F), shadow(m, 1.5 * s, 0.9, s, 1.6 * s) * (1 - m))
    cv.over(metal(relief(m, 1.2 * s) * 2 * s, GOLD, 1.0, 30, 0.8), m)
    gem(cv, cx * s, cy * s, 4.2 * s, 6.6 * s, EMBER_ENAMEL, 6, glow=0.8, bezel_w=1.1 * s, shape='diamond')
    return put('crest', cv)


@piece
def divider():
    s = SS
    W, H = 480, 24
    cv = Canvas(W * s, H * s)
    ink = Ink(cv.w, cv.h, 3)
    cx, cy = W / 2, H / 2
    ink.line([(8 * s, cy * s), ((cx - 12) * s, cy * s)], 0.4 * s, 1.8 * s)
    ink.line([((cx + 12) * s, cy * s), ((W - 8) * s, cy * s)], 1.8 * s, 0.4 * s)
    ink.poly([(cx * s, (cy - 6) * s), ((cx + 8) * s, cy * s), (cx * s, (cy + 6) * s), ((cx - 8) * s, cy * s)])
    for sg in (-1, 1):
        ink.circle((cx + sg * 16) * s, cy * s, 1.8 * s)
    m = ink.mask()
    cv.over(np.zeros(3, F), shadow(m, 1.2 * s, 0.8, 0.6 * s, 1.2 * s) * (1 - m))
    cv.over(metal(relief(m, 0.9 * s) * 1.5 * s, GOLD, 1.0, 30, 0.7), m)
    return put('divider', cv)


# ============================================================================================== botões

def _hblur(a, r):
    out = a
    for _ in range(3):
        out = _box(out, int(r), 1)
    return out


def button(name, face, rim_mat=GOLD, glow=0.0, pressed=False, dim=1.0, W=192, H=64, ch=10, glow_col='#ff7a2a'):
    s = SS
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    d = sd_rect(x, y, 1 * s, 1 * s, (W - 1) * s, (H - 1) * s, ch=ch * s)
    a = cover(d, s)
    # face: metal escovado com leve abaulado vertical
    t = y / cv.h
    rows = np.random.default_rng(61).random((cv.h, cv.w)).astype(F)
    br_line = blur(_hblur(rows, 24 * s), 0.4 * s)
    br_line = (br_line - br_line.mean()) / (br_line.std() + 1e-6) * 0.5 + 0.5
    bulge = np.sin(np.clip((t - 0.06) / 0.88, 0, 1) * math.pi) * 4 * s
    if pressed:
        bulge = -bulge * 0.6
    hf = bulge + br_line * 0.16 * s + profile_bevel(d + 6 * s, 3 * s) * 2 * s
    col = metal(hf, face, 1.0, 10, 0.25, amb=0.22, rim_k=0.08)
    col *= dim
    col *= inner_shadow(d, -6 * s, 8 * s, 0.55 if not pressed else 0.8)[..., None]
    cv.over(col, a)
    # aro de ouro
    shade_molding(cv, d, s, [('lip', 1.0, 2.0, IRON), ('bead', 3.6, 3.2, rim_mat), ('bead', 6.2, 1.2, BRONZE)], a, noise_amt=0.2)
    if glow > 0:
        g = np.exp(-((y / s - (H - 10)) / 9) ** 2) * np.clip(1 - np.abs(x / s - W / 2) / (W * 0.62), 0, 1) * (d < -6 * s)
        cv.add(hexc(glow_col), g * glow)
    # rebites nas pontas
    for cx in (12, W - 12):
        rivet(cv, cx * s, (H / 2) * s, 2.4 * s, rim_mat)
    return put(name, cv)


@piece
def btn():
    button('btn', IRON_WARM)
    button('btn_hover', ramp([(0, '#0b0806'), (0.35, '#2a2019'), (0.6, '#4a3b2e'), (0.82, '#7a6550'), (1, '#c9b090')]), GOLD_PALE, glow=0.5)
    button('btn_down', ramp([(0, '#040302'), (0.4, '#120d0a'), (0.7, '#241b15'), (1, '#4a3c30')]), GOLD, pressed=True, glow=0.3)
    button('btn_off', ramp([(0, '#050505'), (0.4, '#141312'), (0.7, '#222120'), (1, '#3a3836')]),
           ramp([(0, '#0a0a0a'), (0.5, '#2e2b28'), (0.8, '#4e4a45'), (1, '#77716a')]))
    button('btn_ember', EMBER_LACQUER, GOLD, glow=0.55,
           glow_col='#ffb347')
    button('btn_ember_hover', ramp([(0, '#1a0503'), (0.35, '#521308'), (0.6, '#962c10'), (0.82, '#d85a22'), (1, '#ffc080')]), GOLD_PALE,
           glow=0.8, glow_col='#ffd07a')


@piece
def ibtn():
    """botão quadrado de ícone (rodapé do inventário, menus) — 9-slice 22."""
    button('ibtn', IRON_WARM, W=96, H=88, ch=10)
    button('ibtn_hover', ramp([(0, '#0b0806'), (0.35, '#2a2019'), (0.6, '#4a3b2e'), (0.82, '#7a6550'), (1, '#c9b090')]), GOLD_PALE,
           glow=0.6, W=96, H=88, ch=10)
    button('ibtn_on', EMBER_LACQUER, GOLD,
           glow=0.6, glow_col='#ffb347', W=96, H=88, ch=10)
    button('ibtn_blue', ramp([(0, '#020414'), (0.35, '#0a1640'), (0.6, '#1a3482'), (0.82, '#3a66c6'), (1, '#a8ccff')]), GOLD,
           glow=0.5, glow_col='#7ab8ff', W=96, H=88, ch=10)


@piece
def close():
    """botão de fechar: medalhão de ouro com esmalte rubi e X gravado (sem tinta: a arte já tem a cor)."""
    for name, mat, hl in (('close', EMBER_ENAMEL, 0.0), ('close_hover', ramp([(0, '#2a0503'), (0.35, '#7a1608'), (0.65, '#c4300f'), (0.85, '#ff6a2a'), (1, '#ffd0a0')]), 0.6)):
        s = SS
        W = H = 64
        cv = Canvas(W * s, H * s)
        x, y = cv.x, cv.y
        c = W / 2 * s
        if hl:
            cv.add(hexc('#ff7a2a'), np.clip(1 - np.hypot(x - c, y - c) / (31 * s), 0, 1) ** 2 * hl)
        d_o = sd_circle(x, y, c, c, 27 * s)
        d_i = sd_circle(x, y, c, c, 20 * s)
        cv.over(np.zeros(3, F), shadow(cover(d_o), 2 * s, 0.8, s, 2 * s))
        # aro com 8 dentes
        ang = np.arctan2(y - c, x - c)
        teeth = (np.cos(ang * 8) * 0.5 + 0.5) ** 6 * 2.5 * s
        d_t = d_o - teeth
        h = profile_bevel(d_t, 5 * s) * 4 * s * (1 - 0.6 * profile_bevel(d_i + 1 * s, 2 * s))
        cv.over(metal(h, GOLD, 1.0, 30, 0.7), cover(d_t, s))
        # esmalte
        rr = np.hypot(x - c, y - c) / (20 * s)
        v = 0.85 - rr * 0.5 + 0.18 * ((x - c) + (y - c)) / (20 * s)
        cv.over(mat(np.clip(v, 0, 1)), cover(d_i, s))
        # X gravado em ouro
        ink = Ink(cv.w, cv.h, 3)
        k = 9 * s
        ink.line([(c - k, c - k), (c + k, c + k)], 4.4 * s)
        ink.line([(c + k, c - k), (c - k, c + k)], 4.4 * s)
        m = ink.mask()
        cv.over(np.zeros(3, F), shadow(m, 1.2 * s, 0.9, 0.8 * s, 1.2 * s) * (1 - m))
        cv.over(metal(relief(m, 1.4 * s) * 2.5 * s, GOLD_PALE, 1.0, 30, 0.8), m)
        cv.add(hexc('#ffffff'), np.clip(1 - np.hypot((x - (c - 8 * s)) / (7 * s), (y - (c - 10 * s)) / (4 * s)), 0, 1) ** 2 * 0.35 * cover(d_i))
        put(name, cv)


@piece
def tab():
    """abas: chapa de ferro com topo chanfrado; ativa ganha laca de brasa — 9-slice L/R 20, T 18, B 6."""
    for name, face, rim, glow in (('tab', IRON_WARM, BRONZE, 0.0),
                                   ('tab_hover', ramp([(0, '#0b0806'), (0.4, '#2a2019'), (0.7, '#4a3b2e'), (1, '#9a8468')]), GOLD, 0.2),
                                   ('tab_on', ramp([(0, '#120302'), (0.35, '#3d0d06'), (0.6, '#6a1c0a'), (0.82, '#a33c18'), (1, '#ff9a5a')]), GOLD, 0.5)):
        s = SS
        W, H = 128, 52
        cv = Canvas(W * s, H * s)
        x, y = cv.x, cv.y
        pts = [(10, 1), (W - 10, 1), (W - 1, 10), (W - 1, H + 8), (1, H + 8), (1, 10)]
        d = sd_poly(x, y, [(px * s, py * s) for px, py in pts])
        a = cover(d, s)
        t = y / cv.h
        hf = np.sin(np.clip(t, 0, 1) * math.pi * 0.5) * 3 * s + _hblur(np.random.default_rng(71).random((cv.h, cv.w)).astype(F), 12 * s) * 0.5 * s
        col = metal(hf, face, 1.0, 10, 0.25, amb=0.22, rim_k=0.05)
        col *= inner_shadow(d, -4 * s, 8 * s, 0.5)[..., None]
        cv.over(col, a)
        shade_molding(cv, d, s, [('bead', 2.2, 2.6, rim), ('bead', 4.6, 1.0, BRONZE)], a * (y < (H - 2) * s), noise_amt=0.2)
        if glow:
            cv.add(hexc('#ff8a3a'), np.exp(-((y / s - 12) / 8) ** 2) * (d < -4 * s) * glow * 0.6)
        put(name, cv)


@piece
def field():
    """campo de texto: canal rebaixado em pedra com filete de bronze — 9-slice 14."""
    s = SS
    W, H = 96, 48
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    d = sd_rect(x, y, 0.5 * s, 0.5 * s, (W - 0.5) * s, (H - 0.5) * s, ch=4 * s)
    a = cover(d, s)
    col = stone_fill(cv, d, s, base=0.12, contrast=0.3, seed=81)
    col *= inner_shadow(d, -2.5 * s, 10 * s, 0.85)[..., None]
    cv.over(col, a * 0.95)
    shade_molding(cv, d, s, [('bead', 1.4, 2.0, BRONZE)], a, noise_amt=0.15)
    lo = np.clip(1 - np.abs(d + 3 * s) / (0.8 * s), 0, 1) * (y > H * s * 0.6) * 0.3
    cv.add(hexc('#c49a5a'), lo)
    return put('field', cv)


# ============================================================================================== slots e grade

def slot_art(name, rim_mat, glow=0.0, glow_col='#ffb347', W=128, H=128, studs=True, well=0.16, rim_spec=None):
    s = SS
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    d = sd_rect(x, y, 0.5 * s, 0.5 * s, (W - 0.5) * s, (H - 0.5) * s, ch=8 * s)
    a = cover(d, s)
    # poço: pedra escura com leve vinheta
    col = stone_fill(cv, d, s, base=well, contrast=0.35, seed=91)
    cxy = np.hypot(x - cv.w / 2, y - cv.h / 2) / (cv.w / 2)
    col *= (1 - 0.35 * np.clip(cxy, 0, 1) ** 2)[..., None]
    col *= inner_shadow(d, -9 * s, 14 * s, 0.85)[..., None]
    cv.over(col, a)
    spec = rim_spec or [('lip', 1.0, 2.0, IRON), ('bead', 3.6, 3.2, rim_mat), ('band', 6.6, 2.8, IRON_WARM), ('bead', 8.6, 1.4, BRONZE)]
    shade_molding(cv, d, s, spec, a, noise_amt=0.3)
    # luz de baixo do poço (borda inferior interna pega reflexo)
    lo = np.clip(1 - np.abs(d + 9.6 * s) / (0.9 * s), 0, 1) * smooth((y / cv.h - 0.5) * 2) * 0.35
    cv.add(hexc('#d8b27a'), lo)
    if glow > 0:
        ring = np.exp(-((d + 7 * s) / (4 * s)) ** 2) * glow
        cv.add(hexc(glow_col), ring * 0.6)
        inner = np.clip(1 + (d + 10 * s) / (26 * s), 0, 1) ** 2 * (d < -9 * s) * glow * 0.22
        cv.add(hexc(glow_col), inner)
    if studs:
        for cx, cy in corner_centers(W, H, 7.2):
            rivet(cv, cx * s, cy * s, 2.3 * s, rim_mat)
    return put(name, cv)


@piece
def slot():
    slot_art('slot', BRONZE)
    slot_art('slot_hover', GOLD_PALE, glow=0.35, glow_col='#ffd9a0')
    slot_art('slot_sel', GOLD, glow=0.9, glow_col='#ffb347')
    slot_art('slot_ember', GOLD, glow=0.9, glow_col='#ff6a1a')


@piece
def cell():
    """casa da grade do inventário (40 px na tela, 80 na textura; estica inteira)."""
    s = SS
    W = H = 80
    for name, base, edge in (('cell', 0.11, BRONZE), ('cell_hover', 0.2, GOLD_PALE)):
        cv = Canvas(W * s, H * s)
        x, y = cv.x, cv.y
        d = sd_rect(x, y, 0.5 * s, 0.5 * s, (W - 0.5) * s, (H - 0.5) * s, ch=3 * s)
        a = cover(d, s)
        col = stone_fill(cv, d, s, base=base, contrast=0.3, seed=101)
        col *= inner_shadow(d, -2 * s, 12 * s, 0.75)[..., None]
        cv.over(col, a)
        h, _ = molding(d, s, [('bead', 1.2, 1.6, edge)])
        top_left = ((x + y) < (cv.w + cv.h) * 0.5)
        dark = np.where(top_left, 0.05, 0.42)          # chanfro rebaixado: luz embaixo-direita
        m = (np.abs(d + 1.2 * s) <= 1.2 * s).astype(F)
        cv.over(edge(np.full_like(d, 0.0) + dark + h * 0.03), m * 0.9)
        put(name, cv)


@piece
def item_bg():
    """fundo do item na grade (ocupado): placa levemente mais clara com filete — 9-slice 12."""
    s = SS
    W = H = 64
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    d = sd_rect(x, y, 0.5 * s, 0.5 * s, (W - 0.5) * s, (H - 0.5) * s, ch=3 * s)
    a = cover(d, s)
    rr = np.hypot(x - cv.w / 2, y - cv.h / 2) / (cv.w / 2)
    v = 0.48 - 0.25 * rr
    col = ramp([(0, '#08060a'), (0.4, '#1a1410'), (0.7, '#2e231a'), (1, '#4a3a2a')])(v)
    cv.over(col, a * 0.82)
    shade_molding(cv, d, s, [('bead', 1.2, 1.6, BRONZE)], a * 0.8, noise_amt=0.1)
    return put('item_bg', cv)


# ============================================================================================== barras

@piece
def bar_frame():
    """tubo de barra (vida do alvo, grupo, exp): ferro com friso de ouro e pontas — 9-slice L/R 18, T/B 12."""
    s = SS
    W, H = 192, 36
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    d = sd_rect(x, y, 0.5 * s, 0.5 * s, (W - 0.5) * s, (H - 0.5) * s, ch=8 * s)
    a = cover(d, s)
    inner = d < -5 * s
    col = np.zeros((cv.h, cv.w, 3), F) + hexc('#040303')
    col *= inner_shadow(d, -5 * s, 8 * s, 0.6)[..., None]
    cv.over(col, a * inner * 0.9)
    shade_molding(cv, d, s, [('lip', 0.9, 1.8, IRON), ('bead', 3.0, 2.6, GOLD), ('bead', 5.0, 1.0, BRONZE)], a, noise_amt=0.2)
    # vidro: reflexo no alto do tubo
    hl = np.exp(-((y / s - 10) / 2.4) ** 2) * (d < -6 * s) * 0.18
    cv.add(hexc('#ffffff'), hl)
    return put('bar_frame', cv)


@piece
def bar_fill():
    """líquido da barra (tingido pela USS): branco com volume cilíndrico, reflexo e borbulhas."""
    s = SS
    W, H = 64, 24
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    t = y / cv.h
    v = 0.55 + 0.45 * np.sin(np.clip(t, 0, 1) * math.pi) ** 0.6 - 0.25 * t
    col = np.stack([v, v, v], -1).astype(F)
    cv.over(col, np.ones_like(t))
    cv.add(hexc('#ffffff'), np.exp(-((t - 0.22) / 0.08) ** 2) * 0.45)
    cv.mul(1 - 0.35 * np.exp(-((t - 0.98) / 0.08) ** 2))
    return put('bar_fill', cv)


@piece
def vbar_frame():
    """moldura vertical (Vigor / Escudo) por cima do tubo desenhado em código — 9-slice T/B 18, L/R 8."""
    s = SS
    W, H = 36, 160
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    d = sd_rect(x, y, 0.5 * s, 0.5 * s, (W - 0.5) * s, (H - 0.5) * s, ch=6 * s)
    a = cover(d, s)
    ring = (d > -6.2 * s)
    shade_molding(cv, d, s, [('lip', 0.9, 1.8, IRON), ('bead', 3.0, 2.6, GOLD), ('bead', 5.2, 1.2, BRONZE)], a * ring, noise_amt=0.2)
    hl = np.exp(-((x / s - 12) / 2.2) ** 2) * (d < -6 * s) * 0.16
    cv.add(hexc('#ffffff'), hl)
    for cy in (6, H - 6):
        rivet(cv, (W / 2) * s, cy * s, 2.6 * s, GOLD)
    return put('vbar_frame', cv)


# ============================================================================================== HUD

def console_mask_spec(W, H):
    """silhueta do console da base: topo com degraus nas pontas."""
    return [(0, 26), (40, 26), (56, 10), (W - 56, 10), (W - 40, 26), (W, 26), (W, H), (0, H)]


@piece
def hudbar():
    """console da barra de baixo: pedra escura, moldura de ferro e ouro com degraus — 9-slice L/R 120, T 56, B 20."""
    s = SS
    W, H = 640, 168
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    outer = [(2, 30), (34, 30), (52, 8), (W - 52, 8), (W - 34, 30), (W - 2, 30), (W - 2, H + 20), (2, H + 20)]
    d = sd_poly(x, y, [(px * s, py * s) for px, py in outer])
    a = cover(d, s)
    cv.over(np.zeros(3, F), shadow(a, 6 * s, 0.75, 0, -2 * s) * (1 - a))
    col = stone_fill(cv, d, s, base=0.40, contrast=0.7, seed=111)
    # canal central mais escuro (onde ficam os slots)
    ch = sd_rect(x, y, 24 * s, 36 * s, (W - 24) * s, (H - 14) * s, ch=8 * s)
    col *= (1 - 0.3 * cover(ch + 4 * s, 8 * s))[..., None]
    col *= inner_shadow(d, -16 * s, 18 * s, 0.6)[..., None]
    cv.over(col, a * 0.97)
    # friso do canal
    shade_molding(cv, ch, s, [('bead', -1.2, 2.2, BRONZE)], cover(ch - 3 * s, s) * (1 - cover(ch + 3 * s, s)) + 0, noise_amt=0.2)
    # moldura externa (só topo e laterais aparecem; a base sai da tela)
    shade_molding(cv, d, s, [('lip', 1.5, 3.0, IRON), ('bead', 5.4, 4.0, GOLD), ('band', 11.5, 8.0, IRON_WARM),
                             ('bead', 16.6, 2.4, GOLD)], a, noise_amt=0.6)
    # rebites ao longo do topo das pontas
    for cx in (16, W - 16):
        rivet(cv, cx * s, 41 * s, 3.0 * s, GOLD)
    # filete de brasa sob o friso de cima (fica esticado no centro: luz contínua)
    gl = np.exp(-((y / s - 22) / 2.0) ** 2) * (x > 60 * s) * (x < (W - 60) * s) * 0.5
    cv.add(hexc('#ff6a1a'), gl * (d < -18 * s))
    return put('hudbar', cv)


def cap_masks(W, H, s):
    """ornamento da ponta do console (lado esquerdo): placa de ferro em arco gótico + volutas de ouro e folhas."""
    plate = Ink(W * s, H * s, 3)
    k = s
    # placa em arco ogival encostada na barra (lado direito da imagem)
    pts = []
    for i in range(41):
        t = i / 40
        a = math.pi * (0.5 + 0.5 * t)       # de cima para a esquerda
        pts.append((W - 8 + math.cos(a) * 120 * 0.78, H - 4 + math.sin(a) * -1 * (H - 30) * (1 - 0.15 * t)))
    plate.poly([((W - 8) * k, (H - 4) * k)] + [(x * k, y * k) for x, y in pts] + [((W - 8 - 94) * k, (H - 4) * k)])
    pm = plate.mask()
    gold = Ink(W * s, H * s, 3)
    # voluta principal: sobe pela borda do arco e enrola para fora no alto
    scroll_arm(gold, (W - 22) * k, (H - 14) * k, (W - 92) * k, 70 * k, 26 * k, 9 * k, 3.2 * k, bend=0.28, cw=False, turns=1.25)
    # voluta de baixo: corre para a esquerda e enrola para baixo
    scroll_arm(gold, (W - 40) * k, (H - 20) * k, 70 * k, (H - 46) * k, 16 * k, 7 * k, 2.6 * k, bend=-0.18, cw=True, turns=1.2)
    # folhas de acanto ao longo da voluta principal
    for (lx, ly, ang, ln, wd) in ((W - 46, H - 70, -120, 34, 7), (W - 70, H - 110, -150, 30, 6), (W - 40, H - 120, -80, 26, 5.5), (W - 110, H - 40, 180, 30, 6), (W - 90, H - 28, 200, 22, 5)):
        leaf(gold, lx * k, ly * k, math.radians(ang), ln * k, wd * k)
    # bolas de remate
    gold.circle((W - 128) * k, 48 * k, 5 * k)
    # aro dourado no pé da placa
    gold.line([((W - 8 - 94) * k, (H - 5) * k), ((W - 8) * k, (H - 5) * k)], 6 * k)
    gm = gold.mask()
    return pm, gm


@piece
def hud_wing():
    """ornamentos das pontas da barra (esquerda e direita): 240x220 -> 120x110 na tela."""
    s = SS
    W, H = 240, 220
    pm0, gm0 = cap_masks(W, H, s)
    for name, flip in (('hud_wing_l', False), ('hud_wing_r', True)):
        pm, gm = (pm0[:, ::-1], gm0[:, ::-1]) if flip else (pm0, gm0)
        cv = Canvas(W * s, H * s)
        allm = np.maximum(pm, gm)
        cv.over(np.zeros(3, F), shadow(allm, 4 * s, 0.8, 3 * s if flip else -3 * s, 4 * s) * (1 - allm))
        # placa: ferro com friso de bronze na borda e pedra no miolo
        hp = relief(pm, 5 * s) * 6 * s + noise(cv.w, cv.h, 5 * s, 171, 2) * 0.4 * s
        edge = np.clip((pm - blur(pm, 3 * s)) * 3.5, 0, 1)
        col = metal(hp, IRON, 1.0, 14, 0.3, amb=0.12) * (1 - edge[..., None]) + metal(hp, BRONZE, 1.0, 24, 0.6) * edge[..., None]
        cv.over(col, pm)
        cv.over(np.zeros(3, F), shadow(gm, 1.8 * s, 0.9, (1 if flip else -1) * 1.5 * s, 2.2 * s) * (1 - gm) * pm)
        hg = relief(gm, 1.8 * s) * 4 * s
        cv.over(metal(hg, GOLD, 1.0, 30, 0.8, rim_k=0.25), gm)
        gx = (W - 30) if not flip else 30
        gem(cv, gx * s, (H - 30) * s, 10 * s, 10 * s, EMBER_ENAMEL, 10, glow=0.9, bezel_w=3.2 * s)
        put(name, cv)


@piece
def socket():
    """engaste dos cristais de Vida e Mana: medalhão hexagonal de ferro e ouro com pontas — 200x232 (100x116 na tela)."""
    s = SS
    W, H = 200, 232
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    cx, cy = W / 2, H / 2

    def hexa(rx, ry):
        return [(cx, cy - ry), (cx + rx, cy - ry * 0.48), (cx + rx, cy + ry * 0.48), (cx, cy + ry), (cx - rx, cy + ry * 0.48), (cx - rx, cy - ry * 0.48)]

    d_o = sd_poly(x, y, [(px * s, py * s) for px, py in hexa(84, 106)])
    d_i = sd_poly(x, y, [(px * s, py * s) for px, py in hexa(66, 86)])
    # pontas (6 espinhos de ouro saindo dos vértices)
    ink = Ink(cv.w, cv.h, 3)
    for (px, py) in hexa(84, 106):
        ang = math.atan2(py - cy, px - cx)
        ln = 14
        tip = (px + math.cos(ang) * ln, py + math.sin(ang) * ln)
        nx, ny = -math.sin(ang) * 7, math.cos(ang) * 7
        ink.poly([((px + nx) * s, (py + ny) * s), (tip[0] * s, tip[1] * s), ((px - nx) * s, (py - ny) * s),
                  ((px - math.cos(ang) * 6) * s, (py - math.sin(ang) * 6) * s)])
    spikes = ink.mask()
    a_o = cover(d_o, s)
    allm = np.maximum(a_o, spikes)
    cv.over(np.zeros(3, F), shadow(allm, 5 * s, 0.85, 0, 4 * s) * (1 - allm))
    cv.over(metal(relief(spikes, 2 * s) * 4 * s, GOLD, 1.0, 30, 0.8), spikes)
    ring = a_o * (1 - cover(d_i + 1 * s, s))
    shade_molding(cv, d_o, s, [('lip', 1.5, 3.0, IRON), ('bead', 5.6, 4.4, GOLD), ('band', 12.0, 8.4, IRON_WARM), ('bead', 17.4, 2.6, GOLD)],
                  ring, noise_amt=0.6)
    # fundo do engaste (escuro, onde o cristal é desenhado por cima)
    col = np.zeros((cv.h, cv.w, 3), F) + hexc('#050304')
    col *= inner_shadow(d_i, 0, 10 * s, 0.4)[..., None]
    cv.over(col, cover(d_i, s) * 0.85)
    # gemas pequenas nos vértices laterais
    for (px, py) in hexa(75, 96)[1:3] + hexa(75, 96)[4:6]:
        rivet(cv, px * s, py * s, 3.2 * s, GOLD)
    return put('socket', cv)


@piece
def menu_btn():
    """botões do menu da barra (42x50 na tela): chapa com gema de brasa no topo."""
    for name, rim, face, glow in (('menu_btn', BRONZE, IRON_WARM, 0.0),
                                   ('menu_btn_hover', GOLD_PALE, ramp([(0, '#0b0806'), (0.35, '#2a2019'), (0.6, '#4a3b2e'), (0.82, '#7a6550'), (1, '#c9b090')]), 0.35),
                                   ('menu_btn_on', GOLD, ramp([(0, '#120302'), (0.35, '#3d0d06'), (0.6, '#6a1c0a'), (0.82, '#a33c18'), (1, '#ff9a5a')]), 0.6)):
        s = SS
        W, H = 84, 100
        cv = Canvas(W * s, H * s)
        x, y = cv.x, cv.y
        d = sd_rect(x, y, 1 * s, 1 * s, (W - 1) * s, (H - 1) * s, ch=12 * s)
        a = cover(d, s)
        hf = profile_bevel(d + 6 * s, 10 * s) * 3 * s + _hblur(np.random.default_rng(131).random((cv.h, cv.w)).astype(F), 12 * s) * 0.5 * s
        col = metal(hf, face, 1.0, 10, 0.25, amb=0.2, rim_k=0.06)
        col *= inner_shadow(d, -6 * s, 10 * s, 0.6)[..., None]
        cv.over(col, a)
        shade_molding(cv, d, s, [('lip', 1.0, 2.0, IRON), ('bead', 3.6, 3.2, rim), ('bead', 6.2, 1.2, BRONZE)], a, noise_amt=0.2)
        if glow:
            cv.add(hexc('#ff8a3a'), np.exp(-(((y / s) - (H - 14)) / 10) ** 2) * (d < -6 * s) * glow * 0.7)
        put(name, cv)


@piece
def minimap_frame():
    """anel do minimapa: bronze e ouro com marcas de rumo, rosa dos ventos no norte e 4 gemas."""
    s = SS
    W = H = 448
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    c = W / 2 * s
    R_o, R_i = 218, 194
    d_o = sd_circle(x, y, c, c, R_o * s)
    d_i = sd_circle(x, y, c, c, R_i * s)
    ring_a = cover(d_o, s) * (1 - cover(d_i, s))
    cv.over(np.zeros(3, F), shadow(cover(d_o, s), 6 * s, 0.6, 0, 4 * s) * (1 - cover(d_o, s)))
    # aro: perfil radial com dois frisos de ouro e banda de ferro com marcas
    rr = np.hypot(x - c, y - c) / s
    h = np.zeros_like(rr)
    gold = np.zeros_like(rr)

    def b(center, width, mat_gold):
        t = np.clip(1 - np.abs(rr - center) / (width / 2), 0, 1)
        return np.sqrt(1 - (1 - t) ** 2) * width * 0.55, (t > 0) * (1.0 if mat_gold else 0.0)
    for cen, wd, g in ((R_o - 3, 5, True), (R_o - 12, 13, False), (R_i + 4, 6, True)):
        hh, gg = b(cen, wd, g)
        h = np.maximum(h, hh)
        gold = np.maximum(gold, gg * (hh > 0.01))
    ang = np.arctan2(y - c, x - c)
    ticks = ((np.cos(ang * 36) * 0.5 + 0.5) ** 30) * (np.abs(rr - (R_o - 12)) < 4) * 2.5
    h = h + ticks
    n = noise(cv.w, cv.h, 3 * s, 141, 2) * 0.6 * s
    col = metal(h * s + n, IRON_WARM, 1.0, 14, 0.3) * (1 - gold[..., None]) + metal(h * s + n * 0.3, GOLD, 1.0, 28, 0.7) * gold[..., None]
    cv.over(col, ring_a)
    # sombra interna sobre o mapa
    ish = np.clip(1 - (R_i - rr) / 18, 0, 1) ** 2 * (rr < R_i) * 0.75
    cv.over(np.zeros(3, F), ish)
    # norte: ponta de lança com gema
    ink = Ink(cv.w, cv.h, 3)
    top = (W / 2, 4)
    ink.poly([((W / 2) * s, -2 * s), ((W / 2 + 16) * s, 26 * s), ((W / 2) * s, 40 * s), ((W / 2 - 16) * s, 26 * s)])
    for sg in (-1, 1):
        scroll_arm(ink, (W / 2 + sg * 10) * s, 30 * s, (W / 2 + sg * 46) * s, 22 * s, 7 * s, 4 * s, 2 * s, 0.25, cw=(sg < 0))
    m = ink.mask()
    cv.over(np.zeros(3, F), shadow(m, 2 * s, 0.85, s, 2 * s) * (1 - m))
    cv.over(metal(relief(m, 1.6 * s) * 3.5 * s, GOLD, 1.0, 30, 0.8), m)
    gem(cv, (W / 2) * s, 22 * s, 6 * s, 9 * s, EMBER_ENAMEL, 6, glow=0.8, bezel_w=1.6 * s, shape='diamond')
    for a_deg in (90, 180, 0):
        a = math.radians(a_deg)
        gx, gy = W / 2 + math.cos(a) * (R_o - 12), H / 2 + math.sin(a) * (R_o - 12)
        gem(cv, gx * s, gy * s, 6 * s, 6 * s, EMBER_ENAMEL, 8, glow=0.4, bezel_w=2.2 * s)
    return put('minimap_frame', cv)


@piece
def portrait():
    """moldura redonda do retrato do herói (64 px na tela) e plaquinha do nível."""
    s = SS
    W = H = 128
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    c = W / 2 * s
    d_o = sd_circle(x, y, c, c, 62 * s)
    d_i = sd_circle(x, y, c, c, 50 * s)
    cv.over(np.zeros(3, F), shadow(cover(d_o), 3 * s, 0.7, 0, 2 * s) * (1 - cover(d_o)))
    # fundo do retrato (fica atrás do glifo)
    rr = np.hypot(x - c, y - c) / (50 * s)
    cv.over(ramp([(0, '#040303'), (0.6, '#1a120c'), (1, '#3a2414')])(np.clip(0.9 - rr * 0.8, 0, 1)), cover(d_i, s))
    ring = cover(d_o, s) * (1 - cover(d_i, s))
    rr2 = np.hypot(x - c, y - c) / s
    h = np.maximum.reduce([bead(-rr2, -60, 2.5) * 2.6, bead(-rr2, -55.5, 3.5) * 1.6, bead(-rr2, -51.5, 1.8) * 1.6]) * s
    gold = ((np.abs(rr2 - 60) < 2.5) | (np.abs(rr2 - 51.5) < 1.8)).astype(F)
    n = noise(cv.w, cv.h, 3 * s, 151, 2) * 0.5 * s
    col = metal(h + n, IRON_WARM, 1.0, 14, 0.3) * (1 - gold[..., None]) + metal(h + n * 0.3, GOLD, 1.0, 28, 0.7) * gold[..., None]
    cv.over(col, ring)
    put('portrait', cv)
    # plaquinha do nível
    button('lvl_badge', EMBER_LACQUER, GOLD, glow=0.3, W=96, H=40, ch=8)


@piece
def toast():
    """faixa dos avisos no alto da tela: estandarte com pontas em seta — 9-slice L/R 64, T/B 18."""
    s = SS
    W, H = 320, 80
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    cy = H / 2
    pts = [(30, 12), (W - 30, 12), (W - 4, cy), (W - 30, H - 12), (30, H - 12), (4, cy)]
    d = sd_poly(x, y, [(px * s, py * s) for px, py in pts])
    a = cover(d, s)
    cv.over(np.zeros(3, F), shadow(a, 4 * s, 0.7, 0, 3 * s) * (1 - a))
    v = 0.28 + 0.2 * (1 - np.abs(y / cv.h - 0.5) * 2)
    col = ramp([(0, '#040202'), (0.4, '#160906'), (0.7, '#2e120a'), (1, '#5a2410')])(v)
    col *= inner_shadow(d, -4 * s, 12 * s, 0.5)[..., None]
    cv.over(col, a * 0.94)
    shade_molding(cv, d, s, [('bead', 2.4, 2.8, GOLD), ('bead', 5.2, 1.2, BRONZE)], a, noise_amt=0.2)
    for cx in (22, W - 22):
        gem(cv, cx * s, cy * s, 4 * s, 6 * s, EMBER_ENAMEL, 6, glow=0.6, bezel_w=1.2 * s, shape='diamond')
    return put('toast', cv)


@piece
def goldbox():
    """caixa do ouro no rodapé do inventário — 9-slice 24."""
    s = SS
    W, H = 256, 80
    cv = Canvas(W * s, H * s)
    x, y = cv.x, cv.y
    d = sd_rect(x, y, 0.5 * s, 0.5 * s, (W - 0.5) * s, (H - 0.5) * s, ch=8 * s)
    a = cover(d, s)
    col = stone_fill(cv, d, s, base=0.1, contrast=0.3, seed=161)
    col *= inner_shadow(d, -6 * s, 14 * s, 0.8)[..., None]
    cv.over(col, a)
    shade_molding(cv, d, s, [('lip', 0.9, 1.8, IRON), ('bead', 3.2, 2.8, GOLD), ('band', 5.8, 2.4, IRON_WARM), ('bead', 7.6, 1.2, BRONZE)], a, noise_amt=0.3)
    for cx, cy in corner_centers(W, H, 7):
        rivet(cv, cx * s, cy * s, 2.0 * s, GOLD)
    return put('goldbox', cv)


@piece
def scroll():
    """barra de rolagem: trilho rebaixado e puxador de ouro — 9-slice 8."""
    s = SS
    W, H = 24, 96
    for name, is_drag in (('scroll_track', False), ('scroll_drag', True)):
        cv = Canvas(W * s, H * s)
        x, y = cv.x, cv.y
        d = sd_rect(x, y, (3 if not is_drag else 2) * s, 1 * s, (W - (3 if not is_drag else 2)) * s, (H - 1) * s, r=(9 if not is_drag else 10) * s)
        a = cover(d, s)
        if is_drag:
            h = profile_bevel(d, 6 * s) * 4 * s
            cv.over(metal(h, GOLD, 1.0, 28, 0.6), a)
            for cy in (H / 2 - 6, H / 2, H / 2 + 6):
                g = np.exp(-((y / s - cy) / 0.9) ** 2) * (np.abs(x / s - W / 2) < 5) * a
                cv.over(hexc('#3a2410'), g * 0.7)
        else:
            col = np.zeros((cv.h, cv.w, 3), F) + hexc('#070505')
            cv.over(col, a * 0.8)
            shade_molding(cv, d, s, [('bead', 1.0, 1.6, BRONZE)], a * 0.8, noise_amt=0.1)
        put(name, cv)


@piece
def check():
    """caixa de marcar (Toggle): poço com filete e marca em losango de brasa."""
    s = SS
    W = H = 40
    for name, on in (('check', False), ('check_on', True)):
        cv = Canvas(W * s, H * s)
        x, y = cv.x, cv.y
        d = sd_rect(x, y, 1 * s, 1 * s, (W - 1) * s, (H - 1) * s, ch=4 * s)
        a = cover(d, s)
        cv.over(np.zeros(3, F) + hexc('#060404'), a * 0.95)
        shade_molding(cv, d, s, [('bead', 1.6, 2.4, GOLD)], a, noise_amt=0.1)
        if on:
            gem(cv, W / 2 * s, H / 2 * s, 8 * s, 8 * s, EMBER_ENAMEL, 4, glow=0.5, bezel_w=0.6 * s, shape='diamond')
        put(name, cv)


# ============================================================================================== glifos dourados

def glyph_canvas(draw_fn, name, size=128, mat=GOLD):
    s = SS
    cv = Canvas(size * s, size * s)
    ink = Ink(cv.w, cv.h, 3)
    draw_fn(ink, s, size)
    m = ink.mask()
    # contorno escuro (lê em qualquer fundo)
    out = np.clip(blur(m, 2.2 * s) * 3.2, 0, 1)
    cv.over(hexc('#0a0503'), out * 0.92)
    h = relief(m, 1.8 * s) * 3.2 * s
    cv.over(metal(h, mat, 1.0, 30, 0.8, amb=0.28, rim_k=0.2), m)
    return put(name, cv)


def _g_bag(k, s, S):
    c = S / 2
    # saco de couro amarrado
    k.d.ellipse([(c - 38) * s * 3, (c - 6) * s * 3, (c + 38) * s * 3, (c + 46) * s * 3], fill=255)
    k.poly([((c - 12) * s, (c - 22) * s), ((c + 12) * s, (c - 22) * s), ((c + 30) * s, (c + 6) * s), ((c - 30) * s, (c + 6) * s)])
    k.poly([((c - 12) * s, (c - 24) * s), ((c - 24) * s, (c - 44) * s), ((c - 6) * s, (c - 36) * s), (c * s, (c - 46) * s),
            ((c + 6) * s, (c - 36) * s), ((c + 24) * s, (c - 44) * s), ((c + 12) * s, (c - 24) * s)])
    k.line([((c - 16) * s, (c - 22) * s), ((c + 16) * s, (c - 22) * s)], 6 * s, v=0)
    k.line([((c - 14) * s, (c - 22) * s), ((c + 14) * s, (c - 22) * s)], 3 * s)


def _g_char(k, s, S):
    c = S / 2
    # elmo de frente com crista
    k.poly([((c - 30) * s, (c + 4) * s), ((c - 26) * s, (c - 26) * s), (c * s, (c - 40) * s), ((c + 26) * s, (c - 26) * s),
            ((c + 30) * s, (c + 4) * s), ((c + 18) * s, (c + 40) * s), ((c - 18) * s, (c + 40) * s)])
    k.d.polygon([(p[0] * s * 3, p[1] * s * 3) for p in [(c - 22, c - 8), (c - 4, c - 4), (c - 4, c + 2), (c - 22, c)]], fill=0)
    k.d.polygon([(p[0] * s * 3, p[1] * s * 3) for p in [(c + 22, c - 8), (c + 4, c - 4), (c + 4, c + 2), (c + 22, c)]], fill=0)
    k.d.rectangle([(c - 3) * s * 3, (c + 4) * s * 3, (c + 3) * s * 3, (c + 34) * s * 3], fill=0)
    k.line([(c * s, (c - 40) * s), (c * s, (c - 54) * s)], 7 * s, 3 * s)


def _g_guild(k, s, S):
    c = S / 2
    # estandarte com brasão
    k.line([((c - 32) * s, (c - 48) * s), ((c - 32) * s, (c + 50) * s)], 7 * s)
    k.circle((c - 32) * s, (c - 50) * s, 6 * s)
    k.poly([((c - 26) * s, (c - 42) * s), ((c + 38) * s, (c - 42) * s), ((c + 38) * s, (c + 22) * s), ((c + 6) * s, (c + 36) * s), ((c - 26) * s, (c + 22) * s)])
    k.d.polygon([(p[0] * s * 3, p[1] * s * 3) for p in [(c + 6, c - 28), (c + 18, c - 8), (c + 6, c + 14), (c - 6, c - 8)]], fill=0)


def _g_a_mag(k, s, S):
    c = S / 2
    # estrela arcana de 8 pontas
    pts = []
    for i in range(16):
        a = i * math.pi / 8 - math.pi / 2
        r = 50 if i % 2 == 0 else 20
        pts.append(((c + math.cos(a) * r) * s, (c + math.sin(a) * r) * s))
    k.poly(pts)
    k.d.ellipse([(c - 11) * s * 3, (c - 11) * s * 3, (c + 11) * s * 3, (c + 11) * s * 3], fill=0)
    k.circle(c * s, c * s, 6 * s)


def _g_options(k, s, S):
    c = S / 2
    pts = []
    for i in range(48):
        a = i * 2 * math.pi / 48
        r = 46 if (i // 3) % 2 == 0 else 36
        pts.append(((c + math.cos(a) * r) * s, (c + math.sin(a) * r) * s))
    k.poly(pts)
    k.d.ellipse([(c - 16) * s * 3, (c - 16) * s * 3, (c + 16) * s * 3, (c + 16) * s * 3], fill=0)


def _g_map(k, s, S):
    c = S / 2
    k.poly([((c - 44) * s, (c - 30) * s), ((c - 14) * s, (c - 40) * s), ((c + 14) * s, (c - 30) * s), ((c + 44) * s, (c - 40) * s),
            ((c + 44) * s, (c + 34) * s), ((c + 14) * s, (c + 44) * s), ((c - 14) * s, (c + 34) * s), ((c - 44) * s, (c + 44) * s)])
    k.line([((c - 14) * s, (c - 36) * s), ((c - 14) * s, (c + 38) * s)], 3 * s, v=0)
    k.line([((c + 14) * s, (c - 34) * s), ((c + 14) * s, (c + 40) * s)], 3 * s, v=0)
    k.line([((c - 32) * s, (c + 16) * s), ((c - 6) * s, (c - 4) * s), ((c + 8) * s, (c + 10) * s), ((c + 32) * s, (c - 14) * s)], 4 * s, v=0)


def _g_party(k, s, S):
    c = S / 2
    for dx, dy, sc in ((-26, 6, 0.78), (26, 6, 0.78), (0, 0, 1.0)):
        cx, top = c + dx, c + dy
        k.circle(cx * s, (top - 20 * sc) * s, 13 * sc * s, v=0 if sc == 1.0 else 255)
        k.circle(cx * s, (top - 20 * sc) * s, 11 * sc * s)
        k.d.ellipse([(cx - 25 * sc) * s * 3, (top + 2 * sc) * s * 3, (cx + 25 * sc) * s * 3, (top + 60 * sc) * s * 3], fill=255)
        if sc == 1.0:
            k.d.ellipse([(cx - 29) * s * 3, (top - 2) * s * 3, (cx + 29) * s * 3, (top + 64) * s * 3], outline=0, width=int(4 * s * 3))
    k.d.rectangle([0, int((c + 42) * s * 3), S * s * 3, S * s * 3], fill=0)


def _g_rank(k, s, S):
    c = S / 2
    k.poly([((c - 30) * s, (c - 40) * s), ((c + 30) * s, (c - 40) * s), ((c + 26) * s, (c - 4) * s), ((c + 6) * s, (c + 12) * s), ((c - 6) * s, (c + 12) * s), ((c - 26) * s, (c - 4) * s)])
    k.line([((c - 30) * s, (c - 32) * s), ((c - 46) * s, (c - 30) * s), ((c - 40) * s, (c - 10) * s), ((c - 24) * s, (c - 8) * s)], 6 * s)
    k.line([((c + 30) * s, (c - 32) * s), ((c + 46) * s, (c - 30) * s), ((c + 40) * s, (c - 10) * s), ((c + 24) * s, (c - 8) * s)], 6 * s)
    k.d.rectangle([(c - 6) * s * 3, (c + 10) * s * 3, (c + 6) * s * 3, (c + 30) * s * 3], fill=255)
    k.d.rectangle([(c - 24) * s * 3, (c + 30) * s * 3, (c + 24) * s * 3, (c + 42) * s * 3], fill=255)


def _g_help(k, s, S):
    c = S / 2
    k.ring(c * s, c * s, 46 * s, 9 * s)
    f = ImageFont.truetype(os.path.join(FONT_DIR, 'Cinzel.ttf'), int(70 * s * 3))
    k.text((c * s, (c + 2) * s), '?', f)


def _g_close(k, s, S):
    c = S / 2
    k.line([((c - 34) * s, (c - 34) * s), ((c + 34) * s, (c + 34) * s)], 16 * s)
    k.line([((c + 34) * s, (c - 34) * s), ((c - 34) * s, (c + 34) * s)], 16 * s)


def _g_coin(k, s, S):
    c = S / 2
    k.circle(c * s, c * s, 46 * s)
    k.ring(c * s, c * s, 36 * s, 4 * s, v=0)
    f = ImageFont.truetype(os.path.join(FONT_DIR, 'Cinzel.ttf'), int(52 * s * 3))
    k.d.text((c * s * 3, (c + 1) * s * 3), 'K', fill=0, font=f, anchor='mm')


def _g_rmb(k, s, S):
    c = S / 2
    k.poly([((c - 28) * s, (c - 10) * s), ((c + 28) * s, (c - 10) * s), ((c + 28) * s, (c + 22) * s), ((c + 10) * s, (c + 46) * s), ((c - 10) * s, (c + 46) * s), ((c - 28) * s, (c + 22) * s)])
    k.poly([((c + 4) * s, (c - 46) * s), ((c + 28) * s, (c - 40) * s), ((c + 28) * s, (c - 14) * s), ((c + 4) * s, (c - 14) * s)])
    k.poly([((c - 4) * s, (c - 46) * s), ((c - 28) * s, (c - 40) * s), ((c - 28) * s, (c - 14) * s), ((c - 4) * s, (c - 14) * s)], v=120)


def _g_potion(k, s, S):
    c = S / 2
    k.circle(c * s, (c + 14) * s, 34 * s)
    k.d.rectangle([(c - 10) * s * 3, (c - 40) * s * 3, (c + 10) * s * 3, (c - 10) * s * 3], fill=255)
    k.d.rectangle([(c - 16) * s * 3, (c - 48) * s * 3, (c + 16) * s * 3, (c - 38) * s * 3], fill=255)


GLYPHS = {
    'g_bag': _g_bag, 'g_char': _g_char, 'g_guild': _g_guild, 'g_a_mag': _g_a_mag, 'g_options': _g_options, 'g_map': _g_map,
    'g_party': _g_party, 'g_rank': _g_rank, 'g_help': _g_help, 'g_close': _g_close, 'g_coin': _g_coin, 'g_rmb': _g_rmb,
}


@piece
def glyphs():
    for name, fn in GLYPHS.items():
        glyph_canvas(fn, name, mat=GOLD_PALE if name in ('g_close',) else GOLD)


# ============================================================================================== folha de contato

def contact_sheet():
    files = sorted(f for f in os.listdir(OUT) if f.endswith('.png'))
    ims = [(f, Image.open(os.path.join(OUT, f))) for f in files]
    cols = 6
    cw, chh = 300, 240
    rows = (len(ims) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cw, rows * chh), (58, 62, 54, 255))
    from PIL import ImageDraw
    dr = ImageDraw.Draw(sheet)
    try:
        f = ImageFont.truetype(os.path.join(FONT_DIR, 'Inter.ttf'), 14)
    except Exception:
        f = None
    for i, (name, im) in enumerate(ims):
        r, c = divmod(i, cols)
        im2 = im.copy()
        im2.thumbnail((cw - 20, chh - 40))
        x0 = c * cw + (cw - im2.width) // 2
        y0 = r * chh + 10 + (chh - 40 - im2.height) // 2
        # xadrez atrás
        sheet.alpha_composite(im2, (x0, y0))
        dr.text((c * cw + 10, r * chh + chh - 24), f'{name} {im.width}x{im.height}', fill=(230, 220, 200, 255), font=f)
    p = os.path.join(ROOT, 'Art', 'previews', 'ui_premium_kit.png')
    os.makedirs(os.path.dirname(p), exist_ok=True)
    sheet.convert('RGB').save(p)
    return p


if __name__ == '__main__':
    names = sys.argv[1:] or list(PIECES)
    for n in names:
        print('->', n, flush=True)
        PIECES[n]()
    print(contact_sheet())
