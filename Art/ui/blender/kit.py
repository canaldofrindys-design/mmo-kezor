"""
Kezor — kit de interface "clássico premium" v2, modelado e renderizado em 3D no Blender (Cycles).

    python -m pip install bpy   (Blender 5.x como módulo; já vem com Cycles)
    python Art/ui/blender/kit.py            # todas as peças
    python Art/ui/blender/kit.py window btn # algumas

Saída: Assets/Resources/UI/Premium/*.png (2x). Unidade da cena = 1 px de interface.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import bpy  # noqa: E402
from kzb import *  # noqa: E402,F401,F403
import kzb  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'UI', 'Premium')
PIECES = {}
SAMPLES = int(os.environ.get('KZ_SAMPLES', '128'))


def piece(fn):
    PIECES[fn.__name__] = fn
    return fn


def out(name):
    return os.path.join(OUT, name + '.png')


def mirror_quadrants(objs, sxs=((-1, 1), (1, -1), (-1, -1))):
    """duplica os objetos do canto superior direito para os outros 3 cantos (espelho pelo centro da cena)."""
    for sx, sy in sxs:
        for o in objs:
            c = o.copy()
            bpy.context.scene.collection.objects.link(c)
            c.scale = (o.scale[0] * sx, o.scale[1] * sy, o.scale[2])
            c.location = (o.location[0] * sx, o.location[1] * sy, o.location[2])


def new_objs(before):
    return [o for o in bpy.data.objects if o.name not in before and o.type in ('MESH', 'CURVE')]


# ============================================================================================== perfis do aro

def frame_rings(path, M, z=0.0, scale=1.0, inner_mat=None):
    """aro moldado completo: lábio de ferro, cordão de ouro, faixa de ferro martelado, filete de ouro, degrau."""
    s = scale
    sweep(path, [(0, 0), (0, 5 * s), (0.5 * s, 6.4 * s), (1.6 * s, 7.3 * s), (3.0 * s, 7.6 * s)], M['iron'], 'lip', z0=z)
    sweep(path, round_profile(3.0 * s, 9.0 * s, 7.6 * s, 11.6 * s, 12, 7.6 * s), M['gold'], 'bead1', z0=z)
    band = [(9.0 * s + i * (8.0 * s) / 10, 7.8 * s + 1.4 * s * math.sin(math.pi * i / 10)) for i in range(11)]
    sweep(path, band, M['iron'], 'band', z0=z)
    sweep(path, round_profile(17.0 * s, 20.4 * s, 7.8 * s, 10.4 * s, 10, 7.8 * s), M['gold'], 'bead2', z0=z)
    sweep(path, [(20.4 * s, 7.8 * s), (21.4 * s, 6.5 * s), (22.6 * s, 3.0 * s), (23.6 * s, 0.8 * s), (24.5 * s, 0.0)], inner_mat or M['bronze'], 'step', z0=z)
    return 24.5 * s


def mats():
    return {'gold': gold(), 'gold_pale': gold_pale(), 'iron': iron(), 'bronze': bronze(), 'leather': leather((0.009, 0.0065, 0.0055)),
            'gem': gem_mat((0.55, 0.012, 0.004), 4), 'gem_blue': gem_mat((0.1, 0.35, 1.0), 6, 'gem_blue'), 'steel': steel_dark(),
            'lacquer': lacquer(), 'stone': stone()}


def corner_ornament(M, cx, cy, k=1.0, z=9.0):
    """enfeite do canto superior direito (cx, cy = canto). Retorna os objetos criados."""
    before = set(o.name for o in bpy.data.objects)
    bx, by = cx - 22 * k, cy - 22 * k
    # placa losango de ferro com aro de ouro e 4 rebites
    R = 23 * k
    dia = [(bx, by - R), (bx + R, by), (bx, by + R), (bx - R, by)]
    plate(dia, 4.5 * k, M['iron'], 'boss', 2.0 * k, z0=z - 1)
    sweep(dia, round_profile(0, 3.6 * k, z + 3.0 * k, z + 7.2 * k, 10, z + 3.0 * k), M['gold'], 'boss_rim', z0=0)
    for i, (dx, dy) in enumerate(((0, 1), (1, 0), (0, -1), (-1, 0))):
        ball(M['gold'], bx + dx * (R - 7 * k), by + dy * (R - 7 * k), 2.0 * k, z + 4.2 * k, f'rv{i}', 0.8)
    set_gem(M['gem'], M['gold'], bx, by, 10.5 * k, z + 4.5 * k, name='cgem', claws=6, facets=16)
    # espinhos para fora na diagonal do canto
    leaf(M['steel'], bx + 15 * k, by + 15 * k, math.radians(45), 14 * k, 4.0 * k, z + 2 * k, 'spk', 3.0 * k)
    # volutas ao longo das bordas (em cima indo para a esquerda; à direita indo para baixo)
    scroll(M['gold'], bx - 13 * k, by + 8 * k, cx - 58 * k, cy - 11 * k, 6.5 * k, 2.4 * k, 1.2 * k, 0.10, cw=False, turns=1.3, z=z + 2.5 * k, name='sc_top')
    scroll(M['gold'], bx + 8 * k, by - 13 * k, cx - 11 * k, cy - 58 * k, 6.5 * k, 2.4 * k, 1.2 * k, 0.10, cw=True, turns=1.3, z=z + 2.5 * k, name='sc_side')
    # segunda fileira de volutas enrolando para dentro da janela
    scroll(M['gold'], bx - 10 * k, by - 6 * k, bx - 36 * k, by - 12 * k, 5.5 * k, 2.0 * k, 1.0 * k, 0.25, cw=True, turns=1.2, z=z + 1 * k, name='sc_in1')
    scroll(M['gold'], bx - 6 * k, by - 10 * k, bx - 12 * k, by - 36 * k, 5.5 * k, 2.0 * k, 1.0 * k, 0.25, cw=False, turns=1.2, z=z + 1 * k, name='sc_in2')
    # folhas de acanto na diagonal
    leaf(M['gold'], bx - 12 * k, by - 12 * k, math.radians(225), 24 * k, 5.0 * k, z + 1 * k, 'lf_main', 3.4 * k)
    leaf(M['gold'], bx - 16 * k, by - 16 * k, math.radians(200), 12 * k, 3.0 * k, z + 1 * k, 'lf_a', 2.2 * k)
    leaf(M['gold'], bx - 16 * k, by - 16 * k, math.radians(250), 12 * k, 3.0 * k, z + 1 * k, 'lf_b', 2.2 * k)
    ball(M['gold'], bx - 34 * k, by - 34 * k, 2.4 * k, z, 'tip_ball', 0.8)
    return new_objs(before)


# ============================================================================================== peças

@piece
def window():
    """moldura das janelas: 320x320 na tela (640 na textura), 9-slice 64 (128 na textura)."""
    reset(SAMPLES)
    M = mats()
    H = 160
    path = chamfer_rect(-H, -H, H, H, 16)
    frame_rings(path, M)
    floor_plane(-H + 20, -H + 20, H - 20, H - 20, M['leather'], 0.0, 'panel')
    objs = corner_ornament(M, H, H)
    mirror_quadrants(objs)
    camera(-H, -H, H, H)
    render(out('window'))


def wing_fan(M, cx, cy, side, z, k=1.0, n=7, length=62, spread=(8, 58), name='wf'):
    """asa de penas de metal em leque (lado = +1 direita / -1 esquerda): penas de ferro com borda dourada."""
    for i in range(n):
        t = i / (n - 1)
        ang = math.radians(spread[0] + (spread[1] - spread[0]) * t)
        a = ang if side > 0 else math.pi - ang
        ln = length * k * (1.0 - 0.38 * t)
        wd = 7.5 * k * (1 - 0.25 * t)
        zz = z + (n - i) * 0.9 * k
        leaf(M['iron'], cx, cy, a, ln, wd, zz, f'{name}_f{i}', 3.2 * k, curl=0.12 * side)
        leaf(M['gold'], cx, cy, a, ln * 1.04, wd * 0.32, zz + 2.6 * k, f'{name}_r{i}', 1.8 * k, curl=0.12 * side)


@piece
def crown():
    """coroa das janelas: medalhão alado no topo, por cima do aro (240x96 na tela)."""
    reset(SAMPLES)
    M = mats()
    W, H = 240, 72
    cx, cy = 0, -14
    wing_fan(M, cx - 14, cy + 4, -1, 6, 1.0, 7, 92, (2, 40), 'wl')
    wing_fan(M, cx + 14, cy + 4, 1, 6, 1.0, 7, 92, (2, 40), 'wr')
    # medalhão: disco de ferro, aro de ouro, gema grande
    circ = [(cx + math.cos(2 * math.pi * i / 64) * 26, cy + math.sin(2 * math.pi * i / 64) * 26) for i in range(64)]
    plate(circ, 8, M['iron'], 'med', 3, z0=14)
    sweep(circ, round_profile(0, 5, 22, 28, 12, 22), M['gold'], 'med_rim', z0=0)
    set_gem(M['gem'], M['gold'], cx, cy, 14, 23, name='mg', claws=6, facets=18)
    for i in range(8):
        a = 2 * math.pi * i / 8 + math.pi / 8
        ball(M['gold'], cx + math.cos(a) * 20.5, cy + math.sin(a) * 20.5, 1.6, 23, f'mb{i}', 0.8)
    # ponta de cima (flor-de-lis estilizada)
    leaf(M['gold'], cx, cy + 24, math.radians(90), 16, 5, 24, 'top', 4)
    leaf(M['gold'], cx, cy + 24, math.radians(130), 11, 3.2, 23, 'tl', 2.6)
    leaf(M['gold'], cx, cy + 24, math.radians(50), 11, 3.2, 23, 'tr', 2.6)
    camera(-W / 2, -H / 2 - 12, W / 2, H / 2 + 12)
    render(out('crown'))


# ============================================================================================== utilidades de peças

def inset_chamfer(x0, y0, x1, y1, c, d):
    return chamfer_rect(x0 + d, y0 + d, x1 - d, y1 - d, max(0.5, c - d * 0.414))


def variants(render_list):
    """render_list: [(nome, função que ajusta a cena)] — renderiza várias versões da mesma montagem."""
    for name, fn in render_list:
        if fn:
            fn()
        render(out(name))


def swap(objs, mat):
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(mat)


def glow_ring(path, d0, d1, z, color, strength, name='glow'):
    m = emissive(color, strength, name + '_m')
    return sweep(path, round_profile(d0, d1, z, z + 0.6, 6, z), m, name)


def face_mat(kind):
    if kind == 'iron':
        return metal('face_iron', (0.10, 0.088, 0.078), rough=(0.32, 0.5), dirt=0.3, bump=0.12, noise_scale=0.03, ao_dist=5)
    if kind == 'hover':
        return metal('face_hover', (0.22, 0.18, 0.14), rough=(0.25, 0.42), dirt=0.3, bump=0.12, noise_scale=0.03, ao_dist=5)
    if kind == 'down':
        return metal('face_down', (0.045, 0.04, 0.036), rough=(0.4, 0.6), dirt=0.3, bump=0.12, noise_scale=0.03, ao_dist=5)
    if kind == 'off':
        return metal('face_off', (0.10, 0.10, 0.10), rough=(0.6, 0.8), dirt=0.5, bump=0.1, noise_scale=0.03, ao_dist=5)
    if kind == 'ember':
        return lacquer((0.13, 0.012, 0.005), 'face_ember', 0.22)
    if kind == 'ember_hover':
        return lacquer((0.26, 0.03, 0.008), 'face_ember_h', 0.2)
    if kind == 'blue':
        return lacquer((0.02, 0.06, 0.32), 'face_blue', 0.2)
    raise KeyError(kind)


def plate_button(W, H, c, rim_scale, M, face='iron', rivets=True, name='btn'):
    x0, y0, x1, y1 = -W / 2, -H / 2, W / 2, H / 2
    path = chamfer_rect(x0, y0, x1, y1, c)
    rim = frame_rings(path, M, scale=rim_scale)
    fp = inset_chamfer(x0, y0, x1, y1, c, rim - 0.4)
    _, cap = plate(fp, 2.2 * rim_scale * 4, face_mat(face), name + '_face', 2.4 * rim_scale * 4, z0=0)
    edge = bpy.data.objects[name + '_face_edge']
    faces = [cap, edge]
    if rivets:
        for sx in (-1, 1):
            ball(M['gold'], sx * (W / 2 - rim - 4.5), 0, 1.9, 3.2 * rim_scale * 4, f'{name}_rv{sx}', 0.8)
    camera(x0, y0, x1, y1)
    return faces, rim, path


# ============================================================================================== janelas, painéis e títulos

@piece
def panel():
    """painel menor (quadro do herói, alvo): 160x160 na tela, 9-slice 36 (72 na textura)."""
    reset(SAMPLES)
    M = mats()
    H = 80
    path = chamfer_rect(-H, -H, H, H, 10)
    rim = frame_rings(path, M, scale=0.6)
    floor_plane(-H + 10, -H + 10, H - 10, H - 10, M['leather'], 0.0, 'panel')
    before = set(o.name for o in bpy.data.objects)
    bx = by = H - 13
    R = 11
    dia = [(bx, by - R), (bx + R, by), (bx, by + R), (bx - R, by)]
    plate(dia, 3, M['iron'], 'boss', 1.2, z0=6)
    sweep(dia, round_profile(0, 2.2, 8.4, 10.8, 8, 8.4), M['gold'], 'boss_rim', z0=0)
    set_gem(M['gem'], M['gold'], bx, by, 5.2, 9.6, name='pg', claws=4, facets=12)
    scroll(M['gold'], bx - 8, by + 3, H - 36, H - 7, 4.0, 1.5, 0.8, 0.1, cw=False, turns=1.2, z=8, name='ps1')
    scroll(M['gold'], bx + 3, by - 8, H - 7, H - 36, 4.0, 1.5, 0.8, 0.1, cw=True, turns=1.2, z=8, name='ps2')
    mirror_quadrants(new_objs(before))
    camera(-H, -H, H, H)
    render(out('panel'))


@piece
def panel_dark():
    """poço interno escuro: 64x64 na tela, 9-slice 12 (24 na textura)."""
    reset(SAMPLES)
    M = mats()
    H = 32
    path = chamfer_rect(-H, -H, H, H, 5)
    sweep(path, round_profile(0, 2.2, 0, 2.2, 8, 0), M['bronze'], 'lip')
    sweep(path, [(2.2, 0.2), (3.4, -1.5), (5.0, -3.0), (6.0, -3.4)], M['iron'], 'down')
    floor_plane(-H + 5.5, -H + 5.5, H - 5.5, H - 5.5, leather((0.006, 0.0045, 0.004), 'well'), -3.4, 'floor')
    camera(-H, -H, H, H)
    render(out('panel_dark'))


@piece
def header():
    """faixa do título: 384x40 na tela, 9-slice L/R 56, T/B 16 (x2 na textura)."""
    reset(SAMPLES)
    M = mats()
    W, H = 384, 40
    path = [(-W / 2 + 18, -14), (W / 2 - 18, -14), (W / 2 - 3, 0), (W / 2 - 18, 14), (-W / 2 + 18, 14), (-W / 2 + 3, 0)]
    plate(path, 3.2, lacquer((0.11, 0.012, 0.006), 'hdr'), 'hdr', 2.0, z0=0)
    sweep(path, round_profile(0, 3.4, 3, 6.8, 10, 3), M['gold'], 'hdr_rim')
    sweep(path, round_profile(4.6, 6.2, 3.2, 4.8, 6, 3.2), M['bronze'], 'hdr_rim2')
    for sx in (-1, 1):
        cx = sx * (W / 2 - 22)
        dia = [(cx, -13), (cx + 9, 0), (cx, 13), (cx - 9, 0)]
        plate(dia, 3, M['gold'], f'hd{sx}', 1.4, z0=5)
        set_gem(M['gem'], M['gold'], cx, 0, 4.2, 8.4, ry=6.4, name=f'hg{sx}', claws=0, facets=10)
        for k in range(3):
            ball(M['gold'], cx - sx * (16 + k * 6), 0, 1.4 - k * 0.25, 5, f'hb{sx}{k}', 0.8)
    camera(-W / 2, -H / 2, W / 2, H / 2)
    render(out('header'))


@piece
def crest():
    """ornamento sob o título (escala para caber): 240x22 na tela."""
    reset(SAMPLES)
    M = mats()
    W, H = 240, 22
    for sx in (-1, 1):
        pts = [(sx * (12 + i * 4.4), 0, 0) for i in range(24)]
        tube(pts, [1.6 * (1 - i / 26) + 0.3 for i in range(24)], M['gold'], f'cl{sx}', 0.7)
        scroll(M['gold'], sx * 9, 2, sx * 30, 7, 3.4, 1.4, 0.7, 0.2, cw=(sx < 0), turns=1.1, z=0, name=f'cs1{sx}')
        scroll(M['gold'], sx * 9, -2, sx * 30, -7, 3.4, 1.4, 0.7, 0.2, cw=(sx > 0), turns=1.1, z=0, name=f'cs2{sx}')
        leaf(M['gold'], sx * 31, 0, 0 if sx > 0 else math.pi, 14, 2.6, 0, f'cf{sx}', 1.8)
        ball(M['gold'], sx * 118, 0, 1.4, 0, f'ce{sx}', 0.8)
    dia = [(0, -9), (9, 0), (0, 9), (-9, 0)]
    plate(dia, 2.4, M['gold'], 'cd', 1.2)
    set_gem(M['gem'], M['gold'], 0, 0, 3.6, 3.0, ry=5.6, name='cg', claws=0, facets=10)
    camera(-W / 2, -H / 2, W / 2, H / 2)
    render(out('crest'))


@piece
def divider():
    reset(SAMPLES)
    M = mats()
    W, H = 240, 12
    for sx in (-1, 1):
        pts = [(sx * (8 + i * 4.6), 0, 0) for i in range(24)]
        tube(pts, [1.1 * (1 - i / 26) + 0.2 for i in range(24)], M['gold'], f'dl{sx}', 0.7)
        ball(M['gold'], sx * 11, 0, 1.3, 0, f'db{sx}', 0.8)
    dia = [(0, -5), (6, 0), (0, 5), (-6, 0)]
    plate(dia, 2, M['gold'], 'dd', 1)
    camera(-W / 2, -H / 2, W / 2, H / 2)
    render(out('divider'))


@piece
def tooltip():
    """tooltip: vidro negro com filete duplo de ouro e cantos com pontas: 64x64, 9-slice 14."""
    reset(SAMPLES)
    M = mats()
    H = 32
    path = chamfer_rect(-H, -H, H, H, 4)
    plate(path, 1.0, metal('glass', (0.02, 0.018, 0.018), rough=(0.15, 0.25), dirt=0.6, bump=0.0, noise_scale=0.02), 'tp', 0.6)
    sweep(path, round_profile(0.6, 2.2, 1.0, 2.6, 8, 1.0), M['gold'], 'tr1')
    sweep(path, round_profile(3.6, 4.6, 1.0, 1.8, 6, 1.0), M['bronze'], 'tr2')
    before = set(o.name for o in bpy.data.objects)
    leaf(M['gold'], H - 1.5, H - 1.5, math.radians(225), 12, 2.6, 1.2, 'tl', 1.6)
    ball(M['gold'], H - 7, H - 7, 1.3, 2.4, 'tb', 0.8)
    mirror_quadrants(new_objs(before))
    camera(-H, -H, H, H)
    render(out('tooltip'))


@piece
def toast():
    """faixa dos avisos: 320x40 na tela, 9-slice L/R 36, T/B 10."""
    reset(SAMPLES)
    M = mats()
    W, H = 320, 40
    path = [(-W / 2 + 22, -14), (W / 2 - 22, -14), (W / 2 - 2, 0), (W / 2 - 22, 14), (-W / 2 + 22, 14), (-W / 2 + 2, 0)]
    plate(path, 3, lacquer((0.05, 0.012, 0.006), 'tst'), 'tst', 2.0)
    sweep(path, round_profile(0, 3.0, 3, 6.2, 10, 3), M['gold'], 'tst_rim')
    for sx in (-1, 1):
        set_gem(M['gem'], M['gold'], sx * (W / 2 - 22), 0, 4, 4, ry=5.5, name=f'tg{sx}', claws=0, facets=10)
    camera(-W / 2, -H / 2, W / 2, H / 2)
    render(out('toast'))


@piece
def goldbox():
    """caixa do ouro: 128x40 na tela, 9-slice 12."""
    reset(SAMPLES)
    M = mats()
    W, H = 128, 40
    path = chamfer_rect(-W / 2, -H / 2, W / 2, H / 2, 5)
    rim = frame_rings(path, M, scale=0.3)
    floor_plane(-W / 2 + 6, -H / 2 + 6, W / 2 - 6, H / 2 - 6, leather((0.005, 0.004, 0.0035), 'gb'), -1, 'f')
    for sx in (-1, 1):
        for sy in (-1, 1):
            ball(M['gold'], sx * (W / 2 - 4.2), sy * (H / 2 - 4.2), 1.6, 3.4, f'gr{sx}{sy}', 0.8)
    camera(-W / 2, -H / 2, W / 2, H / 2)
    render(out('goldbox'))


# ============================================================================================== botões, abas, campos

@piece
def btn():
    """botões: 120x32 na tela, 9-slice 14 (28 na textura)."""
    reset(SAMPLES)
    M = mats()
    faces, rim, path = plate_button(120, 32, 6, 0.24, M, 'iron', name='b')
    rims = [o for o in bpy.data.objects if o.name in ('bead1', 'bead2')]
    variants([
        ('btn', None),
        ('btn_hover', lambda: (swap(faces, face_mat('hover')), swap(rims, M['gold_pale']))),
        ('btn_down', lambda: (swap(faces, face_mat('down')), swap(rims, M['gold']))),
        ('btn_off', lambda: (swap(faces, face_mat('off')), swap(rims, metal('dull', (0.3, 0.28, 0.25), rough=(0.6, 0.8), dirt=0.4)))),
        ('btn_ember', lambda: (swap(faces, face_mat('ember')), swap(rims, M['gold']))),
        ('btn_ember_hover', lambda: (swap(faces, face_mat('ember_hover')), swap(rims, M['gold_pale']))),
    ])


@piece
def ibtn():
    """botão quadrado de ícone: 48x44, 9-slice 12."""
    reset(SAMPLES)
    M = mats()
    faces, rim, path = plate_button(48, 44, 6, 0.24, M, 'iron', rivets=False, name='ib')
    rims = [o for o in bpy.data.objects if o.name in ('bead1', 'bead2')]
    variants([
        ('ibtn', None),
        ('ibtn_hover', lambda: (swap(faces, face_mat('hover')), swap(rims, M['gold_pale']))),
        ('ibtn_on', lambda: (swap(faces, face_mat('ember')), swap(rims, M['gold']))),
        ('ibtn_blue', lambda: (swap(faces, face_mat('blue')), swap(rims, M['gold']))),
    ])


@piece
def menu_btn():
    """botões do menu da barra: 42x50, 9-slice 12."""
    reset(SAMPLES)
    M = mats()
    faces, rim, path = plate_button(42, 50, 7, 0.26, M, 'iron', rivets=False, name='mb')
    rims = [o for o in bpy.data.objects if o.name in ('bead1', 'bead2')]
    variants([
        ('menu_btn', None),
        ('menu_btn_hover', lambda: (swap(faces, face_mat('hover')), swap(rims, M['gold_pale']))),
        ('menu_btn_on', lambda: (swap(faces, face_mat('ember')), swap(rims, M['gold']))),
    ])


@piece
def lvl_badge():
    reset(SAMPLES)
    M = mats()
    plate_button(48, 20, 5, 0.18, M, 'ember', rivets=False, name='lv')
    render(out('lvl_badge'))


@piece
def tab():
    """abas: 64x26 na tela, 9-slice L/R 10, T 9, B 3."""
    reset(SAMPLES)
    M = mats()
    W, H = 64, 26
    path = [(-W / 2, -H / 2 - 8), (W / 2, -H / 2 - 8), (W / 2, H / 2 - 6), (W / 2 - 6, H / 2), (-W / 2 + 6, H / 2), (-W / 2, H / 2 - 6)]
    sweep(path, round_profile(0, 2.2, 0, 2.6, 8, 0), M['bronze'], 'tr')
    _, cap = plate(inset_chamfer(-W / 2, -H / 2 - 8, W / 2, H / 2, 6, 2.2)[:], 1.6, face_mat('iron'), 'tf', 1.2)
    edge = bpy.data.objects['tf_edge']
    rim = bpy.data.objects['tr']
    camera(-W / 2, -H / 2, W / 2, H / 2)
    variants([
        ('tab', None),
        ('tab_hover', lambda: (swap([cap, edge], face_mat('hover')), swap([rim], M['gold']))),
        ('tab_on', lambda: (swap([cap, edge], face_mat('ember')), swap([rim], M['gold_pale']))),
    ])


@piece
def field():
    """campo de texto: 48x24, 9-slice 8."""
    reset(SAMPLES)
    M = mats()
    W, H = 48, 24
    path = chamfer_rect(-W / 2, -H / 2, W / 2, H / 2, 3)
    sweep(path, round_profile(0, 1.8, 0, 1.8, 6, 0), M['bronze'], 'fl')
    sweep(path, [(1.8, 0.1), (2.6, -1.2), (3.6, -2.2)], M['iron'], 'fd')
    floor_plane(-W / 2 + 3.4, -H / 2 + 3.4, W / 2 - 3.4, H / 2 - 3.4, leather((0.004, 0.0035, 0.003), 'ff'), -2.2, 'f')
    camera(-W / 2, -H / 2, W / 2, H / 2)
    render(out('field'))


@piece
def close():
    """fechar: medalhão de ouro com esmalte rubi e X (32x32 na tela)."""
    reset(SAMPLES)
    M = mats()
    R = 13
    circ = [(math.cos(2 * math.pi * i / 64) * R, math.sin(2 * math.pi * i / 64) * R) for i in range(64)]
    _, cap = plate(circ, 2.0, lacquer((0.30, 0.02, 0.01), 'cl'), 'cl', 1.2)
    sweep(circ, round_profile(-1.2, 3.0, 1.6, 5.2, 12, 1.6), M['gold'], 'clr')
    for i in range(8):
        a = 2 * math.pi * i / 8
        ball(M['gold'], math.cos(a) * (R + 1.2), math.sin(a) * (R + 1.2), 1.3, 4.6, f'cb{i}', 0.8)
    k = 5.0
    x1 = tube([(-k, -k, 3), (k, k, 3)], [1.6, 1.6], M['gold_pale'], 'x1', 0.8)
    x2 = tube([(k, -k, 3), (-k, k, 3)], [1.6, 1.6], M['gold_pale'], 'x2', 0.8)
    camera(-16, -16, 16, 16)
    variants([
        ('close', None),
        ('close_hover', lambda: swap([cap, bpy.data.objects['cl_edge']], lacquer((0.7, 0.08, 0.02), 'clh'))),
    ])


@piece
def check():
    reset(SAMPLES)
    M = mats()
    H = 10
    path = chamfer_rect(-H, -H, H, H, 2.5)
    sweep(path, round_profile(0, 1.8, 0, 2.2, 6, 0), M['gold'], 'kr')
    floor_plane(-H + 1.6, -H + 1.6, H - 1.6, H - 1.6, leather((0.004, 0.0035, 0.003), 'kf'), -0.5, 'kf')
    camera(-H, -H, H, H)
    render(out('check'))
    set_gem(M['gem'], M['gold'], 0, 0, 5.0, 0.5, name='kg', claws=0, facets=4)
    render(out('check_on'))


# ============================================================================================== slots, grade, barras

@piece
def slot():
    """slot: 64x64 na tela, 9-slice 16 (32 na textura)."""
    reset(SAMPLES)
    M = mats()
    H = 32
    path = chamfer_rect(-H, -H, H, H, 6)
    rim = frame_rings(path, M, scale=0.34)
    floor_plane(-H + 7, -H + 7, H - 7, H - 7, leather((0.006, 0.0048, 0.004), 'sl'), -0.4, 'f')
    studs = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            studs.append(ball(M['gold'], sx * (H - 4.6), sy * (H - 4.6), 1.9, 4.2, f'st{sx}{sy}', 0.8))
    rims = [o for o in bpy.data.objects if o.name in ('bead1', 'bead2')]
    camera(-H, -H, H, H)
    render(out('slot'))
    swap(rims, M['gold_pale'])
    g1 = glow_ring(path, rim + 0.2, rim + 1.6, 0.2, (1.0, 0.7, 0.4), 0.5, 'gh')
    render(out('slot_hover'))
    bpy.data.objects.remove(g1)
    swap(rims, M['gold'])
    g2 = glow_ring(path, rim + 0.0, rim + 2.2, 0.3, (1.0, 0.55, 0.12), 2.2, 'gs')
    render(out('slot_sel'))
    bpy.data.objects.remove(g2)
    glow_ring(path, rim + 0.0, rim + 2.2, 0.3, (1.0, 0.22, 0.04), 2.2, 'ge')
    render(out('slot_ember'))


@piece
def cell():
    """casa da grade do inventário: 40x40 (estica inteira)."""
    reset(SAMPLES)
    M = mats()
    H = 20
    path = chamfer_rect(-H, -H, H, H, 1.5)
    sweep(path, [(0, 0), (0.6, 0.5), (1.2, 0.0), (2.4, -1.2), (3.2, -1.5)], M['bronze'], 'ce')
    f = floor_plane(-H + 3, -H + 3, H - 3, H - 3, leather((0.007, 0.0055, 0.0045), 'cf'), -1.5, 'f')
    camera(-H, -H, H, H)
    render(out('cell'))
    swap([f], leather((0.03, 0.022, 0.016), 'cfh'))
    render(out('cell_hover'))


@piece
def item_bg():
    """fundo do item na grade: 32x32, 9-slice 6."""
    reset(SAMPLES)
    M = mats()
    H = 16
    path = chamfer_rect(-H, -H, H, H, 2)
    sweep(path, round_profile(0, 1.4, 0, 1.4, 6, 0), M['bronze'], 'ir')
    floor_plane(-H + 1.2, -H + 1.2, H - 1.2, H - 1.2, leather((0.028, 0.02, 0.015), 'if'), 0.2, 'f')
    camera(-H, -H, H, H)
    render(out('item_bg'))


@piece
def bar_frame():
    """tubo das barras: 96x18 na tela, 9-slice L/R 9, T/B 6."""
    reset(SAMPLES)
    M = mats()
    W, H = 96, 18
    path = chamfer_rect(-W / 2, -H / 2, W / 2, H / 2, 4)
    frame_rings(path, M, scale=0.2)
    floor_plane(-W / 2 + 4, -H / 2 + 4, W / 2 - 4, H / 2 - 4, metal('blk', (0.01, 0.01, 0.01), rough=(0.1, 0.2), dirt=0.8), -0.2, 'f')
    camera(-W / 2, -H / 2, W / 2, H / 2)
    render(out('bar_frame'))


@piece
def vbar_frame():
    """moldura das barras verticais (Vigor/Escudo): 24x80, sem miolo; 9-slice L/R 6, T/B 10."""
    reset(SAMPLES)
    M = mats()
    W, H = 24, 80
    path = chamfer_rect(-W / 2, -H / 2, W / 2, H / 2, 4)
    frame_rings(path, M, scale=0.2)
    for sy in (-1, 1):
        ball(M['gold'], 0, sy * (H / 2 - 2.5), 2.2, 3.5, f'vr{sy}', 0.8)
    camera(-W / 2, -H / 2, W / 2, H / 2)
    render(out('vbar_frame'))


# ============================================================================================== HUD

@piece
def hudbar():
    """console da barra de baixo: 640x96 na tela, 9-slice L/R 80, T 36, B 12 (x2 na textura)."""
    reset(SAMPLES)
    M = mats()
    W, H = 640, 96
    x0, x1 = -W / 2, W / 2
    yb, yt, ys = -H / 2 - 30, H / 2 - 6, H / 2 - 24
    outer = [(x0 + 2, yb), (x1 - 2, yb), (x1 - 2, ys), (x1 - 28, ys), (x1 - 44, yt), (x0 + 44, yt), (x0 + 28, ys), (x0 + 2, ys)]
    rim = frame_rings(outer, M, scale=0.72)
    plate(outer, 0.3, stone((0.016, 0.013, 0.011)), 'stone', 0.2, z0=-0.3)
    # canal rebaixado onde ficam os slots
    ch = chamfer_rect(x0 + 26, -H / 2 - 20, x1 - 26, ys - 18, 6)
    sweep(ch, [(-2.0, 0.0), (-1.0, 1.4), (0.0, 1.6), (1.0, 0.6), (2.4, -1.6), (3.6, -2.4)], M['bronze'], 'ch')
    floor_plane(x0 + 28, -H / 2 - 20, x1 - 28, ys - 20, leather((0.006, 0.005, 0.004), 'chf'), -2.4, 'chf')
    for sx in (-1, 1):
        ball(M['gold'], sx * (W / 2 - 13), ys - 9, 2.6, 13, f'hr{sx}', 0.8)
        ball(M['gold'], sx * (W / 2 - 13), -H / 2 + 8, 2.6, 13, f'hr2{sx}', 0.8)
        set_gem(M['gem'], M['gold'], sx * (W / 2 - 36), yt - 7, 4.2, 12, name=f'hg{sx}', claws=4, facets=10)
    camera(x0, -H / 2, x1, H / 2)
    render(out('hudbar'))


def volute_cap(M, flip=1):
    """remate da ponta do console (lado esquerdo; flip=-1 espelha para a direita)."""
    objs = []
    before = set(o.name for o in bpy.data.objects)
    # placa em arco ogival encostada no console (lado direito da peça)
    arch = []
    for i in range(25):
        t = i / 24
        a = math.pi * 0.5 * t
        arch.append((48 - 76 * math.sin(a), -50 + 92 * math.cos(a) * (1 - 0.1 * t)))
    path = [(48, -50)] + arch[1:] + [(48 - 76, -50)]
    path = [(48, -50), (48, 42)] + [(48 - 76 * math.sin(math.pi * 0.5 * i / 20), -50 + 92 * math.cos(math.pi * 0.5 * i / 20)) for i in range(1, 21)]
    path = list(reversed(path))
    plate(path, 5, M['iron'], 'cap', 2.2, z0=0)
    sweep(path, round_profile(0, 3.0, 4, 8, 10, 4), M['gold'], 'cap_rim')
    scroll(M['gold'], 38, -40, -14, 22, 13, 4.2, 1.6, bend=0.28, cw=False, turns=1.25, z=8, name='cv1')
    scroll(M['gold'], 30, -42, -40, -30, 8, 3.4, 1.4, bend=-0.18, cw=True, turns=1.2, z=7, name='cv2')
    for (lx, ly, ang, ln, wd) in ((24, -14, 125, 20, 4.4), (8, 4, 150, 18, 4), (30, 6, 95, 16, 3.6), (-8, -30, 185, 18, 4)):
        leaf(M['gold'], lx, ly, math.radians(ang), ln, wd, 8, f'cl{lx}', 3.0)
    set_gem(M['gem'], M['gold'], 34, -36, 7, 9, name='capg', claws=6, facets=14)
    objs = new_objs(before)
    if flip < 0:
        for o in objs:
            o.scale = (-o.scale[0], o.scale[1], o.scale[2])
            o.location = (-o.location[0], o.location[1], o.location[2])
    return objs


@piece
def hud_wing():
    """remates das pontas do console: 120x110 na tela cada."""
    for name, flip in (('hud_wing_l', 1), ('hud_wing_r', -1)):
        reset(SAMPLES)
        M = mats()
        volute_cap(M, flip)
        if flip > 0:
            camera(-68, -55, 52, 55)
        else:
            camera(-52, -55, 68, 55)
        render(out(name))


@piece
def hud_crest():
    """brasão alado no centro do console, por cima da borda: 300x84 na tela."""
    reset(SAMPLES)
    M = mats()
    cx, cy = 0, -18
    wing_fan(M, cx - 22, cy + 6, -1, 6, 1.0, 8, 128, (-4, 42), 'wl')
    wing_fan(M, cx + 22, cy + 6, 1, 6, 1.0, 8, 128, (-4, 42), 'wr')
    hexp = [(cx + math.cos(math.radians(90 + 60 * i)) * 30, cy + math.sin(math.radians(90 + 60 * i)) * 34) for i in range(6)]
    plate(hexp, 8, M['iron'], 'hx', 3, z0=14)
    sweep(hexp, round_profile(0, 5, 22, 28.5, 12, 22), M['gold'], 'hx_rim')
    set_gem(M['gem'], M['gold'], cx, cy, 14, 23, ry=17, name='hxg', claws=6, facets=18)
    for i in range(6):
        a = math.radians(90 + 60 * i)
        leaf(M['steel'], cx + math.cos(a) * 30, cy + math.sin(a) * 34, a, 12, 3.6, 18, f'hsp{i}', 3.0)
    camera(-150, -54, 150, 30)
    render(out('hud_crest'))


@piece
def socket():
    """engaste dos cristais de Vida/Mana: 108x124 na tela."""
    reset(SAMPLES)
    M = mats()
    rx, ry = 44, 52
    hexp = [(math.cos(math.radians(90 + 60 * i)) * rx, math.sin(math.radians(90 + 60 * i)) * ry) for i in range(6)]
    frame_rings(hexp, M, scale=0.48)
    inner = [(math.cos(math.radians(90 + 60 * i)) * (rx - 10), math.sin(math.radians(90 + 60 * i)) * (ry - 10)) for i in range(6)]
    plate(inner, 0.5, leather((0.004, 0.003, 0.003), 'sk'), 'skf', 0.3, z0=-0.5)
    for i in range(6):
        a = math.radians(90 + 60 * i)
        leaf(M['steel'], math.cos(a) * (rx - 2), math.sin(a) * (ry - 2), a, 13, 4.2, 6, f'ssp{i}', 3.2)
        if i in (1, 2, 4, 5):
            ball(M['gold'], math.cos(a) * (rx - 6.5), math.sin(a) * (ry - 6.5), 2.2, 9, f'srv{i}', 0.8)
    camera(-54, -62, 54, 62)
    render(out('socket'))


@piece
def minimap_frame():
    """anel do minimapa: 196x196 na tela, miolo transparente."""
    reset(SAMPLES)
    M = mats()
    R = 98
    circ = [(math.cos(2 * math.pi * i / 128) * R, math.sin(2 * math.pi * i / 128) * R) for i in range(128)]
    frame_rings(circ, M, scale=0.6)
    for i in range(24):
        a = 2 * math.pi * i / 24
        if i % 6 == 0:
            continue
        ball(M['gold'], math.cos(a) * (R - 8.6), math.sin(a) * (R - 8.6), 1.2, 6.6, f'tk{i}', 0.8)
    for i, a in enumerate((0, 180, 270)):
        r = math.radians(a)
        set_gem(M['gem'], M['gold'], math.cos(r) * (R - 8.5), math.sin(r) * (R - 8.5), 5.2, 8, name=f'mg{i}', claws=4, facets=12)
    # norte
    leaf(M['gold'], 0, R - 16, math.radians(90), 18, 7, 9, 'n', 5)
    scroll(M['gold'], -5, R - 10, -26, R - 5, 5, 2.2, 1.0, 0.25, cw=False, turns=1.1, z=9, name='ns1')
    scroll(M['gold'], 5, R - 10, 26, R - 5, 5, 2.2, 1.0, 0.25, cw=True, turns=1.1, z=9, name='ns2')
    set_gem(M['gem'], M['gold'], 0, R - 9, 6, 11, ry=7.5, name='ng', claws=4, facets=12)
    camera(-R, -R, R, R)
    render(out('minimap_frame'))


@piece
def portrait():
    """moldura redonda do retrato: 64x64."""
    reset(SAMPLES)
    M = mats()
    R = 32
    circ = [(math.cos(2 * math.pi * i / 96) * R, math.sin(2 * math.pi * i / 96) * R) for i in range(96)]
    frame_rings(circ, M, scale=0.3)
    plate([(math.cos(2 * math.pi * i / 96) * (R - 7), math.sin(2 * math.pi * i / 96) * (R - 7)) for i in range(96)], 0.4,
          leather((0.012, 0.007, 0.004), 'pf'), 'pf', 0.2, z0=-0.4)
    camera(-R, -R, R, R)
    render(out('portrait'))


if __name__ == '__main__':
    names = sys.argv[1:] or list(PIECES)
    for n in names:
        print('->', n, flush=True)
        PIECES[n]()
