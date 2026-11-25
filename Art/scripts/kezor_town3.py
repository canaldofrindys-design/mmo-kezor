# Kezor — cidade v3 (mid poly): prédios maiores e compostos (taverna, casarão, casas em L e com varanda) e props de
# vida para a vila (feno, lenha, bigorna, mesas, placas, canteiros, arbustos, varal, pilhas de caixotes).
# Arte original do Kezor. Roda no mesmo namespace depois de kezor_town2 e kezor_midpoly (house, cbox, B, T, _m, midpoly).
# Frente dos modelos = -Y (vira +Z no Unity). Unidade = metro.
import math, random


def house_into(b, off, rz, w, d, floors, tiles, seed, **kw):
    """Desenha o corpo de uma casa (house() da v2) dentro de `b`, deslocado e girado: monta prédios compostos."""
    g = globals()
    oldB, oldS = g['B'], g['_single']
    g['B'] = lambda *a, **k: b
    g['_single'] = lambda name, bb, parts=None: None
    b.tf(loc=off, rot=(0, 0, rz))
    try:
        house('_tmp', w, d, floors, tiles, seed, **kw)
    finally:
        g['B'], g['_single'] = oldB, oldS
        b.reset()


def _porch(b, x0, x1, y, depth, h, tiles, rnd):
    """Varanda coberta na frente (-Y): postes, viga e telhado de uma água."""
    t = T
    n = max(2, int((x1 - x0) / 1.8) + 1)
    for i in range(n):
        x = x0 + (x1 - x0) * i / (n - 1)
        cbox(b, (x, y - depth, h / 2), (0.18, 0.18, h), t['timber'], bev=0.02)
        cbox(b, (x, y - depth, 0.08), (0.32, 0.32, 0.16), t['stone'], bev=0.03)
    b.seg((x0 - 0.2, y - depth, h), (x1 + 0.2, y - depth, h), 0.16, t['timber'], d=0.18)
    cbox(b, ((x0 + x1) / 2, y - depth / 2, 0.08), (x1 - x0 + 0.6, depth + 0.3, 0.16), t['plank'], bev=0.02)
    # telhado de uma água: fileiras de telhas inclinadas
    rows = 4
    for r in range(rows):
        f = (r + 0.5) / rows
        yy = y - depth * f - 0.15
        zz = h + 0.55 * (1 - f) + 0.08
        cbox(b, ((x0 + x1) / 2, yy, zz), (x1 - x0 + 0.8, depth / rows + 0.12, 0.1), tiles[r % len(tiles)], bev=0.02)


def _sign(b, x, y, z, cloth, emblem):
    """Placa pendurada num braço de ferro (taverna, ferreiro...)."""
    t = T
    b.seg((x, y, z), (x, y - 1.1, z), 0.06, t['iron'], d=0.06)
    b.seg((x, y - 0.1, z - 0.5), (x, y - 0.9, z - 0.02), 0.04, t['iron'], d=0.04)
    for s in (-1, 1):
        b.seg((x, y - 0.55 + s * 0.3, z), (x, y - 0.55 + s * 0.3, z - 0.25), 0.02, t['iron'], d=0.02)
    cbox(b, (x, y - 0.55, z - 0.6), (0.08, 0.85, 0.6), cloth, bev=0.03)
    cbox(b, (x, y - 0.55, z - 0.6), (0.11, 0.4, 0.3), emblem, bev=0.02)


# ------------------------------------------------------------------ prédios
def taverna3(name='Taverna3'):
    _m(); t = T
    rnd = random.Random(301)
    b = B()
    house_into(b, (0, 0, 0), 0, 8.4, 6.2, 2, [t['tileR'], t['tileR2'], t['tileR3']], 301, stone_ground=True, jetty=True, roof_h=3.2)
    _porch(b, -3.6, 1.6, -3.1, 1.8, 2.5, [t['tileR3'], t['tileR']], rnd)
    _sign(b, 2.6, -3.2, 3.2, t['clothR'], t['gold'])
    # barris empilhados e uma mesa na varanda
    for k, (x, y, z) in enumerate([(3.6, -3.6, 0), (4.3, -3.4, 0), (3.95, -3.5, 0.82)]):
        b.lathe((x, y, z), [(0.3, 0), (0.36, 0.2), (0.38, 0.41), (0.36, 0.62), (0.3, 0.82)], 10, t['plank'])
        b.cyl((x, y, z + 0.1), 0.37, 0.37, 0.05, 10, t['iron'])
    cbox(b, (-1.0, -4.0, 0.75), (1.6, 0.8, 0.08), t['plank'], bev=0.02)
    for sx in (-0.65, 0.65):
        cbox(b, (-1.0 + sx, -4.0, 0.37), (0.1, 0.6, 0.74), t['timber'], bev=0.02)
    return _single(name, b)


def casarao3(name='Casarao3'):
    _m(); t = T
    b = B()
    house_into(b, (0, 0, 0), 0, 9.0, 6.4, 2, [t['tileS'], t['tileS2']], 302, roof_h=3.4)
    house_into(b, (6.6, 1.0, 0), 0, 4.2, 4.4, 1, [t['tileS2'], t['tileS']], 303, roof_h=2.4)
    rnd = random.Random(302)
    _porch(b, -3.8, 3.8, -3.2, 1.9, 2.6, [t['tileS'], t['tileS2']], rnd)
    for k in range(3):
        cbox(b, (1.6, -5.4 + k * 0.32, 0.08 + k * 0.08), (1.8, 0.4, 0.16 + k * 0.16), t['stoneL'], bev=0.03)
    return _single(name, b)


def casa3a(name='Casa3A'):
    """Casa em L: corpo principal e anexo de lado, com quintal de lenha."""
    _m(); t = T
    b = B()
    house_into(b, (0, 0, 0), 0, 6.2, 5.0, 1, [t['tileR'], t['tileR3']], 311, roof_h=2.8)
    house_into(b, (4.6, 1.6, 0), 90, 4.2, 3.6, 1, [t['tileR3'], t['tileR2']], 312, roof_h=2.2)
    for k in range(4):                                                      # lenha encostada no anexo
        for j in range(3 - (k % 2)):
            b.tf(loc=(6.7, 0.0 + j * 0.34 + (k % 2) * 0.17, 0.17 + k * 0.3), rot=(0, 90, 0))
            b.cyl((0, 0, -0.55), 0.16, 0.16, 1.1, 7, t['bark'] if (j + k) % 2 else t['timberL'])
            b.reset()
    return _single(name, b)


def casa3b(name='Casa3B'):
    """Sobrado estreito com puxado de madeira e floreiras."""
    _m(); t = T
    b = B()
    house_into(b, (0, 0, 0), 0, 5.4, 5.2, 2, [t['tileM'], t['tileR3']], 321, jetty=True, roof_h=3.0)
    rnd = random.Random(321)
    # puxado (telheiro) na lateral
    for y in (-1.6, 1.6):
        cbox(b, (4.4, y, 1.1), (0.16, 0.16, 2.2), t['timber'], bev=0.02)
    cbox(b, (3.6, 0, 1.2), (1.8, 3.6, 0.08), t['plank'], bev=0.01)
    for r in range(3):
        cbox(b, (3.2 + r * 0.55, 0, 2.4 - r * 0.22), (0.62, 3.9, 0.1), [t['tileM'], t['tileR3']][r % 2], bev=0.02)
    for k in range(3):
        cbox(b, (3.6 + (k % 2) * 0.4, -1.0 + k * 0.9, 0.35), (0.7, 0.7, 0.7), t['plank'], bev=0.03)
    return _single(name, b)


def casa3c(name='Casa3C'):
    """Casa térrea comprida com varanda na frente."""
    _m(); t = T
    b = B()
    house_into(b, (0, 0, 0), 0, 7.4, 4.8, 1, [t['tileR2'], t['tileR'], mat('T2TileB', '#5c3e2a')], 331, roof_h=2.7)
    _porch(b, -3.2, 3.2, -2.5, 1.6, 2.3, [t['tileR'], t['tileR2']], random.Random(331))
    return _single(name, b)


# ------------------------------------------------------------------ props de vida
def feno(name='Feno'):
    _m(); t = T
    b = B()
    b.lathe((0, 0, 0), [(0.95, 0), (1.05, 0.35), (1.0, 0.8), (0.75, 1.2), (0.35, 1.5), (0.0, 1.6)], 12, t['hay'])
    for k in range(6):
        a = k * 1.05
        b.seg((math.cos(a) * 0.9, math.sin(a) * 0.9, 0.2), (math.cos(a) * 1.15, math.sin(a) * 1.15, 0.0), 0.08, t['hayD'], d=0.03)
    b.seg((0.3, -0.4, 0.0), (0.2, -0.2, 2.0), 0.05, t['timberL'], d=0.05)        # forcado espetado
    return _single(name, b)


def lenha(name='Lenha'):
    _m(); t = T
    b = B()
    for k in range(4):
        for j in range(5 - (k % 2)):
            b.tf(loc=(-0.85 + j * 0.36 + (k % 2) * 0.18, 0, 0.17 + k * 0.3), rot=(90, 0, 0))
            b.cyl((0, 0, -0.5), 0.16, 0.16, 1.0, 7, t['bark'] if (j + k) % 2 else t['bark2'])
            b.reset()
    b.cyl((1.3, 0.2, 0), 0.35, 0.38, 0.55, 9, t['bark'])                          # toco com machado
    b.seg((1.3, 0.2, 0.55), (1.0, 0.0, 1.15), 0.05, t['timberL'], d=0.05)
    cbox(b, (1.32, 0.18, 0.62), (0.08, 0.28, 0.2), t['iron'], bev=0.01)
    return _single(name, b)


def bigorna(name='Bigorna'):
    _m(); t = T
    b = B()
    b.cyl((0, 0, 0), 0.42, 0.46, 0.6, 10, t['bark'])
    cbox(b, (0, 0, 0.72), (0.34, 0.26, 0.24), t['iron'], bev=0.03)
    cbox(b, (0, 0, 0.92), (0.7, 0.3, 0.16), t['iron'], bev=0.04)
    b.seg((0.35, 0, 0.92), (0.62, 0, 0.94), 0.12, t['iron'], w2=0.03, d=0.14, d2=0.04)   # bico
    b.seg((-0.1, 0.05, 1.02), (-0.3, 0.3, 1.04), 0.04, t['timberL'], d=0.04)     # martelo
    cbox(b, (-0.08, 0.03, 1.04), (0.16, 0.08, 0.08), t['iron'], bev=0.01)
    return _single(name, b)


def mesa_taverna(name='MesaTaverna'):
    _m(); t = T
    b = B()
    cbox(b, (0, 0, 0.75), (1.8, 0.9, 0.08), t['plank'], bev=0.02)
    for sx in (-0.75, 0.75):
        cbox(b, (sx, 0, 0.37), (0.1, 0.7, 0.74), t['timber'], bev=0.02)
    for sy in (-1, 1):
        cbox(b, (0, sy * 0.75, 0.45), (1.7, 0.3, 0.06), t['plank'], bev=0.015)
        for sx in (-0.7, 0.7):
            cbox(b, (sx, sy * 0.75, 0.22), (0.08, 0.26, 0.44), t['timber'], bev=0.015)
    for k, x in enumerate((-0.4, 0.2, 0.55)):                                   # canecas
        b.cyl((x, (k % 2) * 0.2 - 0.1, 0.79), 0.06, 0.06, 0.13, 7, t['bronze'] if k % 2 else t['plank'])
    return _single(name, b)


def placa(name='Placa'):
    _m(); t = T
    b = B()
    cbox(b, (0, 0, 0.12), (0.4, 0.4, 0.24), t['stone'], bev=0.03)
    b.cyl((0, 0, 0.24), 0.07, 0.06, 2.3, 7, t['timber'])
    for k, (z, yaw) in enumerate(((2.15, 20), (1.8, -35), (1.45, 70))):
        b.tf(loc=(0, 0, z), rot=(0, 0, yaw))
        b.prism([(0.08, -0.13), (0.95, -0.13), (1.12, 0.0), (0.95, 0.13), (0.08, 0.13)], -0.03, 0.03, t['plank'] if k != 1 else t['timberL'])
        b.reset()
    return _single(name, b)


def canteiro(name='Canteiro'):
    _m(); t = T
    b = B()
    cbox(b, (0, 0, 0.18), (1.8, 0.8, 0.36), t['stoneL'], bev=0.05)
    cbox(b, (0, 0, 0.36), (1.6, 0.6, 0.04), t['soil'], bev=0.01)
    rnd = random.Random(51)
    for k in range(9):
        x = -0.65 + (k % 5) * 0.32 + rnd.uniform(-0.05, 0.05)
        y = -0.15 + (k // 5) * 0.3
        b.ico((x, y, 0.5 + rnd.uniform(0, 0.08)), (0.17, 0.17, 0.15), [t['flower'], t['flower2'], t['leaf2'], t['leaf']][k % 4], jit=0.25, seed=k)
    return _single(name, b)


def arbusto(name='Arbusto'):
    _m(); t = T
    b = B()
    rnd = random.Random(61)
    for k in range(5):
        a = k * 1.3
        r = 0.35 if k else 0
        b.ico((math.cos(a) * r, math.sin(a) * r, 0.45 + rnd.uniform(-0.05, 0.1)), (0.55, 0.55, 0.45), [t['leaf'], t['leaf2'], t['leaf3']][k % 3], jit=0.2, seed=k)
    return _single(name, b)


def varal(name='Varal'):
    _m(); t = T
    b = B()
    for x in (-1.6, 1.6):
        b.cyl((x, 0, 0), 0.06, 0.05, 2.0, 6, t['timber'])
    b.seg((-1.6, 0, 1.9), (1.6, 0, 1.85), 0.015, t['timberL'], d=0.015)
    for k, (x, c) in enumerate(((-1.0, t['clothB']), (-0.3, t['plaster']), (0.4, t['clothR']), (1.05, t['clothY']))):
        cbox(b, (x, 0, 1.55), (0.5, 0.03, 0.62 - (k % 2) * 0.15), c, bev=0.01)
    return _single(name, b)


def caixote_pilha(name='CaixotePilha'):
    _m(); t = T
    b = B()
    for (x, y, z, rz) in ((0, 0, 0, 0), (0.75, 0.05, 0, 8), (0.35, 0.0, 0.7, -6), (-0.1, 0.75, 0, 15)):
        b.tf(loc=(x, y, z), rot=(0, 0, rz))
        cbox(b, (0, 0, 0.35), (0.7, 0.7, 0.7), t['plank'], bev=0.03)
        for sx in (-1, 1):
            for sy in (-1, 1):
                cbox(b, (sx * 0.33, sy * 0.33, 0.35), (0.08, 0.08, 0.72), t['timber'], bev=0.015)
        b.reset()
    return _single(name, b)


CITY3 = ['Taverna3', 'Casarao3', 'Casa3A', 'Casa3B', 'Casa3C',
         'Feno', 'Lenha', 'Bigorna', 'MesaTaverna', 'Placa', 'Canteiro', 'Arbusto', 'Varal', 'CaixotePilha']


def build_city3():
    taverna3(); casarao3(); casa3a(); casa3b(); casa3c()
    feno(); lenha(); bigorna(); mesa_taverna(); placa(); canteiro(); arbusto(); varal(); caixote_pilha()
    for n in CITY3:
        r = bpy.data.objects.get(n)
        if r is not None:
            midpoly(r, level=1 if n in ('Feno', 'Arbusto') else 0, sharp_deg=70)
    return CITY3
