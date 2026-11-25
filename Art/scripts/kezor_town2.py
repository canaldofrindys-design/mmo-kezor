# Kezor — cidade v2: props e prédios low poly "polidos" (arestas chanfradas, blocos irregulares, telhas em fileiras,
# vigas, venezianas, tábuas, copas em grupos). Arte original do Kezor. Requer kezor_lib + kezor_heroes2 (cbox, bake_ao,
# export_v2) já carregados no mesmo namespace. Frente dos modelos = -Y (vira +Z no Unity). Unidade = metro.
import math, random

T = {}  # materiais


def _m():
    if T:
        return T
    T.update(
        stone=mat('T2Stone', '#7d766b'), stoneD=mat('T2StoneD', '#5f5a52'), stoneL=mat('T2StoneL', '#958d80'),
        basalt=mat('T2Basalt', '#4a4642'), plaster=mat('T2Plaster', '#cbb994'), plaster2=mat('T2Plaster2', '#b9a988'),
        timber=mat('T2Timber', '#4a3322'), timberL=mat('T2TimberL', '#5e4330'), plank=mat('T2Plank', '#6b4c33'),
        tileR=mat('T2TileR', '#7a2e25'), tileR2=mat('T2TileR2', '#8a3a2a'), tileR3=mat('T2TileR3', '#6a2820'),
        tileS=mat('T2TileS', '#2f3e5c'), tileS2=mat('T2TileS2', '#3a4a68'), tileM=mat('T2TileM', '#4a5a3a'),
        iron=mat('T2Iron', '#2e2a27', rough=0.5, metal=0.6), gold=mat('T2Gold', '#d4a94a', rough=0.4, metal=0.7),
        bronze=mat('T2Bronze', '#8a6a3e', rough=0.45, metal=0.6),
        win=mat('T2Win', '#f5c26b', emit=2.5), lant=mat('T2Lant', '#ffae42', emit=4.0),
        ember=mat('T2Ember', '#ff6a1a', emit=4.0), ember2=mat('T2Ember2', '#ffd36a', emit=4.0),
        clothR=mat('T2ClothR', '#8a2a24'), clothB=mat('T2ClothB', '#2a4a7a'), clothY=mat('T2ClothY', '#b8902e'), clothG=mat('T2ClothG', '#3e6a3a'),
        hay=mat('T2Hay', '#c9a84a'), leaf=mat('T2Leaf', '#4f6b34'), leaf2=mat('T2Leaf2', '#5e7a3a'), leaf3=mat('T2Leaf3', '#3e5a2e'),
        pine=mat('T2Pine', '#2f5a3a'), pine2=mat('T2Pine2', '#26503a'), bark=mat('T2Bark', '#4a3627'), bark2=mat('T2Bark2', '#3a2a1e'),
        water=mat('T2Water', '#2e5a78', rough=0.2), coal=mat('T2Coal', '#1e1410'), void=mat('T2Void', '#141110'),
        rune=mat('T2Rune', '#ff8a2a', emit=2.5), crystal=mat('T2Crystal', '#ffb347', emit=4.0), flower=mat('T2Flower', '#c84a6a'),
        flower2=mat('T2Flower2', '#e0c040'), soil=mat('T2Soil', '#3e2e22'), hayD=mat('T2HayD', '#a88a3a'),
    )
    return T


def _coll():
    return collection('Environment')


def _new(name):
    wipe(name)
    return root(name, _coll(), 'Environment')


def _single(name, b, parts=None):
    """Raiz + malha principal (+ partes nomeadas: (nome, B, pivô))."""
    r = _new(name)
    body = b.obj(name + '.Mesh', _coll(), r, (0, 0, 0), name)
    for pn, bb, pv in (parts or []):
        bb.obj(name + '.' + pn, _coll(), body, pv, pn)
    return r


# ------------------------------------------------------------------ peças de construção
def stone_course(b, x0, x1, y, z, h, depth, rnd, mats, face=-1, gap=0.03, lmin=0.45, lmax=1.05):
    """Fileira de blocos de pedra irregulares ao longo de X (comprimentos e profundidades variados, arestas chanfradas)."""
    x = x0
    while x < x1 - 0.05:
        L = min(rnd.uniform(lmin, lmax), x1 - x)
        dd = depth * rnd.uniform(0.9, 1.08)
        m = mats[rnd.randrange(len(mats))]
        cbox(b, (x + L / 2, y + face * (dd - depth) / 2, z + h / 2), (L - gap, dd, h - gap), m, bev=min(0.05, h * 0.2))
        x += L


def stone_wall_block(b, cx, cy, z0, w, d, h, rnd, mats, course=0.42):
    """Parede maciça de pedra em fiadas (frente e fundos com blocos irregulares; miolo liso)."""
    cbox(b, (cx, cy, z0 + h / 2), (w - 0.06, d - 0.12, h), mats[0], bev=0.02)
    n = max(1, int(round(h / course)))
    ch = h / n
    for i in range(n):
        off = rnd.uniform(0, 0.4)
        stone_course(b, cx - w / 2 - off * 0 , cx + w / 2, cy - d / 2 + 0.06, z0 + i * ch, ch, 0.14, rnd, mats, face=-1)
        stone_course(b, cx - w / 2, cx + w / 2, cy + d / 2 - 0.06, z0 + i * ch, ch, 0.14, rnd, mats, face=1)


def tile_roof(b, c, w, d, h, rnd, tiles, overhang=0.35, ridge_m=None, gable_m=None, row=0.34):
    """Telhado de duas águas com fileiras de telhas sobrepostas, cumeeira e beiral. Cumeeira ao longo de X."""
    cx, cy, cz = c
    W, D = w + overhang * 2, d + overhang * 2
    # miolo escuro (fecha frestas)
    b.roof((cx, cy, cz - 0.02), W - 0.1, D - 0.12, h - 0.05, T['timber'], gable_m or T['plaster'])
    half = D / 2
    L = math.hypot(half, h)
    rows = max(3, int(L / row))
    for s in (-1, 1):
        nrm = (0, s * h / L, half / L)
        for i in range(rows):
            t = (i + 0.5) / rows
            y = cy + s * half * (1 - t)
            z = cz + h * t + 0.05 + (0.015 if i % 2 else 0)
            m = tiles[rnd.randrange(len(tiles))]
            b.seg((cx - W / 2, y, z), (cx + W / 2, y, z), row * 1.25, m, d=0.07, up=nrm)
    b.seg((cx - W / 2 - 0.05, cy, cz + h + 0.06), (cx + W / 2 + 0.05, cy, cz + h + 0.06), 0.26, ridge_m or tiles[0], d=0.16)
    # pontas de caibros sob o beiral
    for s in (-1, 1):
        for k in range(int(W / 0.7) + 1):
            x = cx - W / 2 + 0.2 + k * 0.7
            if x > cx + W / 2 - 0.1:
                break
            b.seg((x, cy + s * (d / 2 - 0.05), cz - 0.1), (x, cy + s * (D / 2 - 0.05), cz - 0.22), 0.1, T['timber'], d=0.1)


def window(b, x, y, z, w=0.7, h=0.85, axis='y', s=-1, shut=None):
    """Janela com moldura, vidro aceso, travessa e duas venezianas abertas. axis = parede perpendicular a Y ou X; s = lado."""
    t = T
    shut = shut or t['plank']

    def P(a, off):   # a = deslocamento ao longo da parede, off = para fora
        return (x + a, y + s * off, z) if axis == 'y' else (x + s * off, y + a, z)

    def box(a, off, zz, sa, so, sz, m, bev=0.012):
        cx, cy, _ = P(a, off)
        size = (sa, so, sz) if axis == 'y' else (so, sa, sz)
        cbox(b, (cx, cy, zz), size, m, bev=bev)
    box(0, 0.02, z, w * 0.86, 0.04, h * 0.86, t['win'])                       # vidro aceso
    box(0, 0.06, z + h / 2, w + 0.12, 0.1, 0.1, t['timber'])                  # verga
    box(0, 0.08, z - h / 2, w + 0.18, 0.14, 0.09, t['timber'])                # peitoril
    for k in (-1, 1):
        box(k * w / 2, 0.05, z, 0.08, 0.08, h, t['timber'])                   # ombreiras
    box(0, 0.05, z, 0.05, 0.06, h, t['timber'])                               # travessa vertical
    box(0, 0.05, z, w, 0.06, 0.05, t['timber'])                               # travessa horizontal
    for k in (-1, 1):                                                          # venezianas abertas (tábuas)
        for j in range(3):
            box(k * (w / 2 + 0.2 + 0.0), 0.12 + j * 0.0, z - h * 0.3 + j * h * 0.3, 0.36, 0.05, h * 0.27, shut, bev=0.01)


def door(b, x, y, z0, w=1.0, h=1.9, s=-1, axis='y'):
    """Porta de tábuas com faixas de ferro, aldrava, moldura e degrau de pedra."""
    t = T

    def box(a, off, zz, sa, so, sz, m, bev=0.012):
        cx, cy = (x + a, y + s * off) if axis == 'y' else (x + s * off, y + a)
        size = (sa, so, sz) if axis == 'y' else (so, sa, sz)
        cbox(b, (cx, cy, zz), size, m, bev=bev)
    n = 4
    for i in range(n):
        box(-w / 2 + w / n * (i + 0.5), 0.04, z0 + h / 2, w / n - 0.02, 0.07, h - 0.02, t['plank'])
    for zz in (z0 + 0.35, z0 + h - 0.4):
        box(0, 0.09, zz, w * 0.95, 0.03, 0.07, t['iron'])
    box(w * 0.3, 0.12, z0 + h * 0.5, 0.08, 0.05, 0.08, t['gold'])
    box(0, 0.06, z0 + h + 0.07, w + 0.3, 0.16, 0.14, t['timber'])
    for k in (-1, 1):
        box(k * (w / 2 + 0.07), 0.06, z0 + h / 2, 0.12, 0.14, h, t['timber'])
    box(0, 0.32, z0 - 0.06, w + 0.5, 0.5, 0.16, t['stoneL'], bev=0.04)


def chimney(b, x, y, z0, h, rnd):
    t = T
    for i in range(int(h / 0.32)):
        cbox(b, (x + rnd.uniform(-0.02, 0.02), y + rnd.uniform(-0.02, 0.02), z0 + i * 0.32 + 0.16), (0.62, 0.62, 0.3),
             [t['stone'], t['stoneD'], t['stoneL']][rnd.randrange(3)], bev=0.04)
    cbox(b, (x, y, z0 + h + 0.06), (0.78, 0.78, 0.12), t['stoneD'], bev=0.03)
    cbox(b, (x, y, z0 + h + 0.22), (0.42, 0.42, 0.2), t['void'], bev=0.02)


def timber_frame(b, cx, cy, z0, w, d, h, sides=('y-', 'y+', 'x-', 'x+')):
    """Enxaimel: pilares nos cantos, frechais e diagonais nas quatro faces."""
    t = T
    for sx in (-1, 1):
        for sy in (-1, 1):
            cbox(b, (cx + sx * w / 2, cy + sy * d / 2, z0 + h / 2), (0.2, 0.2, h), t['timber'], bev=0.02)
    for zz in (z0 + 0.08, z0 + h - 0.06):
        for sy in (-1, 1):
            cbox(b, (cx, cy + sy * (d / 2 + 0.02), zz), (w + 0.1, 0.16, 0.14), t['timber'], bev=0.02)
        for sx in (-1, 1):
            cbox(b, (cx + sx * (w / 2 + 0.02), cy, zz), (0.16, d + 0.1, 0.14), t['timber'], bev=0.02)
    for sd in sides:
        ax, sg = sd[0], (-1 if sd[1] == '-' else 1)
        L = w if ax == 'y' else d
        for k in (-1, 1):
            a0, a1 = k * L * 0.46, k * L * 0.18
            if ax == 'y':
                yy = cy + sg * (d / 2 + 0.03)
                b.seg((cx + a0, yy, z0 + 0.15), (cx + a1, yy, z0 + h - 0.15), 0.13, t['timber'], d=0.05, up=(0, 1, 0))
            else:
                xx = cx + sg * (w / 2 + 0.03)
                b.seg((xx, cy + a0, z0 + 0.15), (xx, cy + a1, z0 + h - 0.15), 0.13, t['timber'], d=0.05, up=(1, 0, 0))


# ------------------------------------------------------------------ casas
def house(name, w, d, floors, roof_tiles, seed, stone_ground=False, jetty=False, roof_h=None):
    _m(); t = T
    rnd = random.Random(seed)
    b = B()
    fh = 2.4
    # embasamento de pedra
    stone_course(b, -w / 2 - 0.15, w / 2 + 0.15, -d / 2 - 0.08, 0, 0.5, 0.3, rnd, [t['stone'], t['stoneD'], t['stoneL']])
    stone_course(b, -w / 2 - 0.15, w / 2 + 0.15, d / 2 + 0.08, 0, 0.5, 0.3, rnd, [t['stone'], t['stoneD'], t['stoneL']], face=1)
    cbox(b, (0, 0, 0.25), (w + 0.25, d + 0.1, 0.5), t['stoneD'], bev=0.04)
    z = 0.5
    for f in range(floors):
        ww, dd = (w + 0.4, d + 0.4) if (jetty and f > 0) else (w, d)
        if stone_ground and f == 0:
            stone_wall_block(b, 0, 0, z, ww, dd, fh, rnd, [t['stone'], t['stoneD'], t['stoneL']])
        else:
            cbox(b, (0, 0, z + fh / 2), (ww - 0.05, dd - 0.05, fh), t['plaster'] if (f + seed) % 2 == 0 else t['plaster2'], bev=0.02)
            timber_frame(b, 0, 0, z, ww, dd, fh)
        if jetty and f > 0:   # mísulas sob o andar em balanço
            for k in range(-2, 3):
                b.seg((k * w / 5, -d / 2, z - 0.4), (k * w / 5, -d / 2 - 0.22, z - 0.02), 0.14, t['timber'], d=0.14)
        # janelas
        nwin = max(1, int(ww / 1.8))
        for i in range(nwin):
            xw = -ww / 2 + ww / nwin * (i + 0.5)
            if f == 0 and abs(xw - w * 0.18) < 0.9:
                continue      # lugar da porta
            window(b, xw, -dd / 2, z + fh * 0.55, s=-1, axis='y')
            if rnd.random() < 0.7:
                window(b, xw, dd / 2, z + fh * 0.55, s=1, axis='y')
        window(b, ww / 2, 0, z + fh * 0.55, s=1, axis='x')
        if rnd.random() < 0.6:
            window(b, -ww / 2, 0, z + fh * 0.55, s=-1, axis='x')
        z += fh
    door(b, w * 0.18, -d / 2, 0.5)
    # lanterna ao lado da porta
    b.seg((w * 0.18 + 0.85, -d / 2 - 0.02, 2.05), (w * 0.18 + 0.85, -d / 2 - 0.38, 2.15), 0.05, t['iron'], d=0.05)
    cbox(b, (w * 0.18 + 0.85, -d / 2 - 0.42, 1.88), (0.2, 0.2, 0.28), t['lant'], bev=0.03)
    cbox(b, (w * 0.18 + 0.85, -d / 2 - 0.42, 2.06), (0.28, 0.28, 0.06), t['iron'], bev=0.02)
    rw, rd = (w + 0.4, d + 0.4) if jetty and floors > 1 else (w, d)
    rh = roof_h or (rd * 0.62)
    tile_roof(b, (0, 0, z), rw, rd, rh, rnd, roof_tiles, gable_m=t['plaster2'])
    chimney(b, rw * 0.28, rd * 0.18, z + rh * 0.35, rh * 0.85, rnd)
    # floreira e barril encostados
    cbox(b, (-w * 0.3, -d / 2 - 0.28, 0.25), (0.9, 0.36, 0.3), t['plank'], bev=0.03)
    for k in range(4):
        b.ico((-w * 0.3 - 0.33 + k * 0.22, -d / 2 - 0.28, 0.48), (0.12, 0.12, 0.1), [t['flower'], t['flower2'], t['leaf2']][k % 3], jit=0.25, seed=seed + k)
    return _single(name, b)


# ------------------------------------------------------------------ muralha, torre, portão
def muralha2(name='Muralha2'):
    """Segmento de 4 m (X), 1,8 m de espessura (Y, fora = +Y), 5,6 m de altura, com ameias chanfradas e adarve."""
    _m(); t = T
    rnd = random.Random(11)
    b = B()
    H, Dp = 5.0, 1.8
    cbox(b, (0, 0, H / 2), (4.0, Dp - 0.2, H), t['stoneD'], bev=0.03)
    for i in range(int(H / 0.7)):   # fiadas de blocos longos dos dois lados (otimizado: ~70 segmentos na volta da vila)
        stone_course(b, -2.0, 2.0, -Dp / 2 + 0.05, i * 0.7, 0.7, 0.18, rnd, [t['stone'], t['stoneD'], t['stoneL']], face=-1, lmin=0.9, lmax=1.7)
        stone_course(b, -2.0, 2.0, Dp / 2 - 0.05, i * 0.7, 0.7, 0.18, rnd, [t['stone'], t['stoneD'], t['basalt']], face=1, lmin=0.9, lmax=1.7)
    cbox(b, (0, 0, H + 0.08), (4.05, Dp + 0.1, 0.16), t['stoneL'], bev=0.04)          # cornija/adarve
    for k in (-1, 1):                                                                    # ameias (lado de fora)
        cbox(b, (k * 1.0, Dp / 2 - 0.25, H + 0.6), (1.2, 0.5, 0.9), t['stone'], bev=0.07, top=(0.92, 0.92))
    cbox(b, (0, -Dp / 2 + 0.15, H + 0.32), (4.0, 0.3, 0.32), t['stoneD'], bev=0.04)        # parapeito de dentro
    b.seg((-1.6, -Dp / 2 - 0.02, 3.2), (-1.6, -Dp / 2 - 0.02, 4.0), 0.1, t['void'], d=0.05)   # seteira
    return _single(name, b)


def torre2(name='Torre2'):
    """Torre redonda (16 lados) em fiadas de pedra, seteiras, cinta de madeira e telhado cônico de telhas."""
    _m(); t = T
    rnd = random.Random(12)
    b = B()
    H, R = 8.6, 2.0
    n = 12
    for i in range(int(H / 0.72)):
        z = i * 0.72
        rr = R * (1.0 - 0.02 * i / 16)
        for k in range(n):
            a0 = (k + (0.5 if i % 2 else 0)) * 2 * math.pi / n
            ca, sa = math.cos(a0), math.sin(a0)
            L = 2 * math.pi * rr / n
            b.tf(loc=(ca * rr, sa * rr, z + 0.36), rot=(0, 0, math.degrees(a0) + 90))
            cbox(b, (0, 0, 0), (L - 0.04, 0.36 * rnd.uniform(0.9, 1.1), 0.68), [t['stone'], t['stoneD'], t['stoneL']][rnd.randrange(3)], bev=0.05)
            b.reset()
    b.cyl((0, 0, 0), R - 0.25, R - 0.3, H, n, t['stoneD'])
    b.cyl((0, 0, H), R + 0.25, R + 0.3, 0.3, n, t['timber'])                                # cinta
    for k in range(n):
        a0 = k * 2 * math.pi / n
        b.seg((math.cos(a0) * (R + 0.1), math.sin(a0) * (R + 0.1), H - 0.4), (math.cos(a0) * (R + 0.35), math.sin(a0) * (R + 0.35), H + 0.05), 0.12, t['timber'], d=0.12)
    # telhado cônico em anéis de telhas
    rings = 7
    for i in range(rings):
        r0 = (R + 0.55) * (1 - i / rings)
        b.cyl((0, 0, H + 0.3 + i * 0.5), r0, r0 * 0.82, 0.52, n, [t['tileS'], t['tileS2']][i % 2])
    b.cone((0, 0, H + 0.3 + rings * 0.5), 0.3, 0.9, 8, t['iron'])
    for a in (0.4, 2.0, 3.6, 5.2):                                                            # seteiras
        b.seg((math.cos(a) * (R + 0.18), math.sin(a) * (R + 0.18), 3.0), (math.cos(a) * (R + 0.18), math.sin(a) * (R + 0.18), 4.0), 0.12, t['void'], d=0.06)
    # bandeira
    flag = B()
    flag.seg((0, 0, H + 3.8), (0, 0, H + 5.6), 0.06, t['iron'])
    flag.prism([(0, 0), (1.1, -0.1), (1.0, -0.4), (1.1, -0.7), (0, -0.6)], -0.03, 0.03, t['clothR'])
    return _single(name, b)


def portao2(name='PortaoArco', span=8.0):
    """Arco de portão em pedra sobre o vão (largura span, ao longo de X), com aduelas, chave de runa e grade erguida."""
    _m(); t = T
    rnd = random.Random(13)
    b = B()
    H = 5.0
    for sx in (-1, 1):
        stone_wall_block(b, sx * (span / 2 + 0.6), 0, 0, 1.2, 2.0, H + 1.4, rnd, [t['stone'], t['stoneD'], t['stoneL']])
    nseg = 11
    for i in range(nseg):
        a0, a1 = math.pi * i / nseg, math.pi * (i + 1) / nseg
        rr = span / 2
        p0 = (math.cos(a0) * rr, 0, H - 1.2 + math.sin(a0) * 2.2)
        p1 = (math.cos(a1) * rr, 0, H - 1.2 + math.sin(a1) * 2.2)
        b.seg((p0[0], -1.0, p0[2]), (p1[0], -1.0, p1[2]), 0.62, [t['stone'], t['stoneL']][i % 2], d=0.55, up=(0, 1, 0))
        b.seg((p0[0], 1.0, p0[2]), (p1[0], 1.0, p1[2]), 0.62, [t['stone'], t['stoneL']][i % 2], d=0.55, up=(0, 1, 0))
    cbox(b, (0, 0, H + 1.4), (span + 2.4, 2.0, 1.0), t['stoneD'], bev=0.05)
    for k in range(-3, 4):
        cbox(b, (k * 1.3, 0.8, H + 2.2), (0.8, 0.4, 0.7), t['stone'], bev=0.06, top=(0.9, 0.9))
    cbox(b, (0, -1.15, H + 1.0), (0.7, 0.12, 0.7), t['rune'], bev=0.03)                     # chave com runa acesa
    for k in range(-3, 4):                                                                     # grade erguida
        b.seg((k * 0.55, 0, H + 0.95), (k * 0.55, 0, H - 0.2), 0.07, t['iron'], d=0.07)
        b.cone((k * 0.55, 0, H - 0.45), 0.06, 0.25, 4, t['iron'], rot0=0)
    b.seg((-span / 2 + 0.3, 0, H - 0.05), (span / 2 - 0.3, 0, H - 0.05), 0.08, t['iron'], d=0.08)
    return _single(name, b)


# ------------------------------------------------------------------ fonte da praça
def fonte2(name='Fonte2'):
    _m(); t = T
    rnd = random.Random(14)
    b = B()
    n = 12
    for k in range(n):                                                                          # degrau
        a0 = k * 2 * math.pi / n
        b.tf(loc=(math.cos(a0) * 3.6, math.sin(a0) * 3.6, 0.12), rot=(0, 0, math.degrees(a0) + 90))
        cbox(b, (0, 0, 0), (1.85, 0.7, 0.24), [t['stone'], t['stoneL']][k % 2], bev=0.05)
        b.reset()
    for k in range(n):                                                                          # borda da bacia
        a0 = (k + 0.5) * 2 * math.pi / n
        b.tf(loc=(math.cos(a0) * 2.95, math.sin(a0) * 2.95, 0.55), rot=(0, 0, math.degrees(a0) + 90))
        cbox(b, (0, 0, 0), (1.5, 0.42, 0.62), t['stoneL'], bev=0.07, top=(0.95, 0.9))
        b.reset()
    b.cyl((0, 0, 0.1), 2.8, 2.8, 0.55, n, t['stoneD'])
    b.cyl((0, 0, 0.62), 2.75, 2.75, 0.04, n, t['water'])
    b.lathe((0, 0, 0.6), [(0.9, 0), (0.75, 0.3), (0.55, 0.4), (0.5, 1.6), (0.7, 1.8), (1.2, 2.0), (1.25, 2.15), (0.8, 2.25)], 10, t['stone'])
    for k in range(4):
        a0 = k * math.pi / 2 + 0.4
        b.seg((math.cos(a0) * 0.55, math.sin(a0) * 0.55, 1.4), (math.cos(a0) * 1.15, math.sin(a0) * 1.15, 2.6), 0.14, t['bronze'], d=0.14)
    b.lathe((0, 0, 2.8), [(0.0, 0), (0.55, 0.05), (0.5, 0.12)], 10, t['ember'])
    gem = B()
    gem.gem((0, 0, 3.5), 0.55, 1.1, t['crystal'], n=6)
    return _single(name, b, [('Gem', gem, (0, 0, 3.5))])


# ------------------------------------------------------------------ natureza
def arvore2(name, seed, h=4.2):
    _m(); t = T
    rnd = random.Random(seed)
    b = B()
    for k in range(4):                                                                       # raízes
        a = k * math.pi / 2 + rnd.uniform(-0.3, 0.3)
        b.seg((0, 0, 0.35), (math.cos(a) * 0.6, math.sin(a) * 0.6, -0.05), 0.2, t['bark'], w2=0.08)
    b.seg((0, 0, 0), (0.1, 0, h * 0.55), 0.42, t['bark'], w2=0.28)
    tips = []
    for k in range(5):
        a = k * 2 * math.pi / 5 + rnd.uniform(-0.3, 0.3)
        z0 = h * rnd.uniform(0.42, 0.55)
        L = rnd.uniform(0.9, 1.4)
        tip = (math.cos(a) * L, math.sin(a) * L, z0 + L * 0.9)
        b.seg((0.05, 0, z0), tip, 0.16, t['bark2'], w2=0.06)
        tips.append(tip)
    tips.append((0, 0, h * 0.85))
    for i, (x, y, z) in enumerate(tips):                                                     # copa em grupos
        s = rnd.uniform(0.85, 1.15)
        b.ico((x, y, z + 0.3), (1.05 * s, 1.05 * s, 0.85 * s), [t['leaf'], t['leaf2'], t['leaf3']][i % 3], jit=0.18, seed=seed + i, sub=1, m2=t['leaf3'], share=0.25)
    return _single(name, b)


def pinheiro2(name, seed, h=5.0):
    _m(); t = T
    rnd = random.Random(seed)
    b = B()
    b.seg((0, 0, -0.1), (0, 0, h * 0.4), 0.32, t['bark'], w2=0.22)
    tiers = 4
    for i in range(tiers):
        z = h * 0.18 + i * h * 0.19
        r = 1.6 * (1 - i / (tiers + 0.6))
        pts = []
        n = 9
        for k in range(n):
            a = k * 2 * math.pi / n + rnd.uniform(-0.12, 0.12)
            rr = r * rnd.uniform(0.85, 1.1)
            pts.append((math.cos(a) * rr, math.sin(a) * rr, z + rnd.uniform(-0.12, 0.05)))
        top = (rnd.uniform(-0.08, 0.08), rnd.uniform(-0.08, 0.08), z + h * 0.32)
        inner = (0, 0, z + 0.18)
        verts = pts + [top, inner]
        faces = [(k, (k + 1) % n, n) for k in range(n)] + [((k + 1) % n, k, n + 1) for k in range(n)]
        b.poly(verts, faces, [[t['pine'], t['pine2']][(k + i) % 2] for k in range(n)] + [t['pine2']] * n)
    return _single(name, b)


def pedra2(name, seed, s=(1.0, 0.8, 0.6)):
    _m(); t = T
    b = B()
    b.ico((0, 0, s[2] * 0.45), s, t['stone'], jit=0.28, seed=seed, sub=1, m2=t['stoneD'], share=0.4, zmin=0)
    b.ico((s[0] * 0.7, s[1] * 0.3, s[2] * 0.2), (s[0] * 0.4, s[1] * 0.4, s[2] * 0.35), t['stoneL'], jit=0.3, seed=seed + 1, sub=1, zmin=0)
    return _single(name, b)


# ------------------------------------------------------------------ props
def barril(name='Barril'):
    _m(); t = T
    b = B()
    b.lathe((0, 0, 0), [(0.32, 0), (0.38, 0.22), (0.4, 0.45), (0.38, 0.68), (0.32, 0.9)], 12, t['plank'])
    for z in (0.12, 0.78):
        b.cyl((0, 0, z), 0.37, 0.37, 0.06, 12, t['iron'])
    b.cyl((0, 0, 0.88), 0.3, 0.3, 0.03, 12, t['timberL'])
    return _single(name, b)


def caixote(name='Caixote'):
    _m(); t = T
    b = B()
    cbox(b, (0, 0, 0.35), (0.7, 0.7, 0.7), t['plank'], bev=0.03)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cbox(b, (sx * 0.33, sy * 0.33, 0.35), (0.08, 0.08, 0.72), t['timber'], bev=0.015)
    for s in (-1, 1):
        b.seg((-0.3, s * 0.36, 0.06), (0.3, s * 0.36, 0.64), 0.07, t['timber'], d=0.03, up=(0, 1, 0))
        b.seg((s * 0.36, -0.3, 0.06), (s * 0.36, 0.3, 0.64), 0.07, t['timber'], d=0.03, up=(1, 0, 0))
    return _single(name, b)


def carroca(name='Carroca'):
    _m(); t = T
    b = B()
    cbox(b, (0, 0, 0.75), (2.4, 1.3, 0.12), t['plank'], bev=0.02)
    for s in (-1, 1):
        cbox(b, (0, s * 0.62, 1.0), (2.4, 0.08, 0.45), t['plank'], bev=0.02)
        cbox(b, (s * 1.16, 0, 1.0), (0.08, 1.3, 0.45), t['plank'], bev=0.02)
    for sx in (-0.7, 0.7):
        for sy in (-1, 1):
            b.tf(loc=(sx, sy * 0.78, 0.52), rot=(90, 0, 0))
            b.cyl((0, 0, -0.06), 0.52, 0.52, 0.12, 12, t['timber'])
            b.cyl((0, 0, -0.09), 0.14, 0.14, 0.18, 8, t['iron'])
            b.reset()
    b.seg((-1.2, -0.3, 0.75), (-2.6, -0.2, 0.55), 0.08, t['timber'])
    b.seg((-1.2, 0.3, 0.75), (-2.6, 0.2, 0.55), 0.08, t['timber'])
    for k in range(3):
        b.lathe((k * 0.6 - 0.4, 0, 0.82), [(0.22, 0), (0.26, 0.2), (0.22, 0.4)], 10, t['hay'] if k % 2 else t['plank'])
    return _single(name, b)


def poco(name='Poco'):
    _m(); t = T
    rnd = random.Random(15)
    b = B()
    n = 10
    for i in range(3):
        for k in range(n):
            a0 = (k + (0.5 if i % 2 else 0)) * 2 * math.pi / n
            b.tf(loc=(math.cos(a0) * 0.95, math.sin(a0) * 0.95, 0.15 + i * 0.28), rot=(0, 0, math.degrees(a0) + 90))
            cbox(b, (0, 0, 0), (0.6, 0.34, 0.26), [t['stone'], t['stoneD'], t['stoneL']][rnd.randrange(3)], bev=0.04)
            b.reset()
    b.cyl((0, 0, 0.05), 0.8, 0.8, 0.8, n, t['void'])
    for s in (-1, 1):
        cbox(b, (s * 1.05, 0, 1.4), (0.16, 0.16, 2.0), t['timber'], bev=0.02)
    b.seg((-1.1, 0, 1.9), (1.1, 0, 1.9), 0.1, t['timberL'])
    b.roof((0, 0, 2.3), 2.6, 1.8, 0.7, t['tileR'], t['timber'])
    b.seg((0, 0, 1.88), (0, 0, 1.2), 0.02, t['iron'])
    b.lathe((0, 0, 0.98), [(0.16, 0), (0.2, 0.22)], 8, t['plank'])
    return _single(name, b)


def banco(name='Banco'):
    _m(); t = T
    b = B()
    for k in range(3):
        cbox(b, (0, -0.12 + k * 0.12, 0.47), (1.8, 0.11, 0.06), t['plank'], bev=0.015)
    for s in (-1, 1):
        cbox(b, (s * 0.72, 0, 0.22), (0.1, 0.36, 0.44), t['timber'], bev=0.02)
    cbox(b, (0, 0.2, 0.75), (1.8, 0.06, 0.24), t['plank'], bev=0.015)
    return _single(name, b)


def vaso(name='Vaso'):
    _m(); t = T
    b = B()
    b.lathe((0, 0, 0), [(0.22, 0), (0.3, 0.2), (0.34, 0.42), (0.38, 0.46)], 10, t['stoneL'])
    b.cyl((0, 0, 0.4), 0.33, 0.33, 0.04, 10, t['soil'])
    for k in range(5):
        a = k * 1.25
        b.ico((math.cos(a) * 0.15, math.sin(a) * 0.15, 0.6 + (k % 2) * 0.1), (0.16, 0.16, 0.14), [t['leaf'], t['leaf2'], t['flower']][k % 3], jit=0.25, seed=k)
    return _single(name, b)


def cerca(name='Cerca'):
    _m(); t = T
    b = B()
    for x in (-0.95, 0, 0.95):
        cbox(b, (x, 0, 0.5), (0.12, 0.12, 1.0), t['timber'], bev=0.02, top=(0.6, 0.6))
    for z in (0.4, 0.8):
        b.seg((-1.0, 0, z), (1.0, 0, z + 0.03), 0.09, t['plank'], d=0.05)
    return _single(name, b)


def estandarte(name='Estandarte'):
    _m(); t = T
    b = B()
    cbox(b, (0, 0, 0.15), (0.5, 0.5, 0.3), t['stone'], bev=0.04)
    b.seg((0, 0, 0.3), (0, 0, 4.0), 0.09, t['timber'])
    b.seg((-0.7, 0, 3.8), (0.7, 0, 3.8), 0.06, t['timber'])
    b.prism([(-0.62, 3.75), (0.62, 3.75), (0.62, 2.2), (0, 1.9), (-0.62, 2.2)], -0.02, 0.02, t['clothR'])
    b.prism([(-0.2, 3.4), (0.2, 3.4), (0.2, 2.8), (0, 2.6), (-0.2, 2.8)], -0.04, 0.04, t['clothY'])
    b.cone((0, 0, 4.0), 0.1, 0.25, 4, t['gold'])
    return _single(name, b)


def poste2(name='Poste2'):
    _m(); t = T
    b = B()
    cbox(b, (0, 0, 0.2), (0.55, 0.55, 0.4), t['stone'], bev=0.05, top=(0.8, 0.8))
    b.cyl((0, 0, 0.4), 0.11, 0.09, 3.6, 8, t['timber'])
    b.seg((0, 0, 3.7), (0, -0.75, 3.8), 0.08, t['iron'])
    b.seg((0, 0, 3.2), (0, -0.55, 3.75), 0.05, t['iron'])
    lamp = B()
    lamp.lathe((0, -0.78, 3.15), [(0.0, 0), (0.18, 0.05), (0.22, 0.2), (0.22, 0.42), (0.28, 0.48), (0.0, 0.62)], 6, t['iron'])
    lamp.cyl((0, -0.78, 3.22), 0.17, 0.17, 0.28, 6, t['lant'])
    return _single(name, b, [('Lanterna', lamp, (0, -0.78, 3.4))])


def braseiro2(name='Braseiro2'):
    _m(); t = T
    b = B()
    for k in range(3):
        a = k * 2 * math.pi / 3
        b.seg((math.cos(a) * 0.45, math.sin(a) * 0.45, 0), (math.cos(a) * 0.18, math.sin(a) * 0.18, 1.1), 0.07, t['iron'])
    b.lathe((0, 0, 1.05), [(0.15, 0), (0.48, 0.25), (0.55, 0.34)], 10, t['iron'])
    for k in range(6):
        a = k * math.pi / 3
        b.cone((math.cos(a) * 0.52, math.sin(a) * 0.52, 1.36), 0.04, 0.16, 4, t['bronze'])
    b.ico((0, 0, 1.36), (0.42, 0.42, 0.08), t['coal'], jit=0.3, seed=3)
    fire = B()
    for k, (sc, m) in enumerate(((1.0, t['ember']), (0.6, t['ember2']))):
        fire.lathe((0, 0, 1.38), [(0.32 * sc, 0), (0.25 * sc, 0.3 * sc), (0.12 * sc, 0.62 * sc), (0.0, 0.95 * sc)], 6, m, rot0=k * 0.5)
    return _single(name, b, [('Fogo', fire, (0, 0, 1.38))])


def bau2(name='BauGuardia'):
    _m(); t = T
    b = B()
    cbox(b, (0, 0, 0.32), (1.1, 0.66, 0.6), t['plank'], bev=0.03)
    b.tf(loc=(0, 0, 0.62), rot=(90, 0, 90))
    b.lathe((0, 0, -0.56), [(0.33, 0), (0.33, 1.12)], 10, t['plank'], caps=True)
    b.reset()
    for x in (-0.42, 0, 0.42):
        cbox(b, (x, 0, 0.5), (0.08, 0.7, 0.9), t['iron'], bev=0.015)
    cbox(b, (0, -0.36, 0.6), (0.16, 0.06, 0.2), t['gold'], bev=0.02)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cbox(b, (sx * 0.52, sy * 0.3, 0.05), (0.12, 0.12, 0.1), t['iron'], bev=0.02)
    return _single(name, b)


def barraca2(name, cloth, seed):
    """Barraca de feira: balcão, quatro postes e toldo de tecido com caída curva e franja; caixotes e mercadorias."""
    _m(); t = T
    rnd = random.Random(seed)
    b = B()
    cbox(b, (0, -0.5, 0.5), (2.8, 0.7, 1.0), t['plank'], bev=0.03)
    cbox(b, (0, -0.5, 1.03), (3.0, 0.85, 0.08), t['timberL'], bev=0.02)
    for sx in (-1.4, 1.4):
        for sy in (-0.9, 0.9):
            cbox(b, (sx, sy, 1.25), (0.13, 0.13, 2.5), t['timber'], bev=0.02)
    # toldo curvo: faixas que caem para a frente
    n = 8
    for i in range(n):
        x0 = -1.6 + i * 3.2 / n
        x1 = x0 + 3.2 / n
        m = cloth if i % 2 == 0 else T['plaster']
        pts = []
        for k in range(5):
            u = k / 4
            y = 1.05 - u * 2.4
            z = 2.75 - 0.5 * u - 0.25 * math.sin(u * math.pi)
            pts.append((y, z))
        for k in range(4):
            (ya, za), (yb, zb) = pts[k], pts[k + 1]
            b.poly([(x0, ya, za), (x1, ya, za), (x1, yb, zb), (x0, yb, zb)], [(0, 1, 2, 3), (3, 2, 1, 0)], m)
        b.prism([(x0, 0), (x1, 0), ((x0 + x1) / 2, -0.25)], -1.36, -1.34, m)
        b.tf(loc=(0, 0, 2.0)); b.reset()
    # mercadorias
    for k in range(5):
        b.ico((-1.1 + k * 0.55, -0.55, 1.18), (0.13, 0.13, 0.11), [t['clothR'], t['flower2'], t['leaf2'], t['clothB'], t['hay']][k], jit=0.2, seed=seed + k)
    for k in range(2):
        cbox(b, (1.9 + k * 0.1, 0.2 - k * 0.8, 0.3 + k * 0.0), (0.6, 0.6, 0.6), t['plank'], bev=0.03)
    return _single(name, b)


def forja2(name='Forja2'):
    _m(); t = T
    rnd = random.Random(16)
    b = B()
    stone_wall_block(b, 0, 1.2, 0, 4.2, 0.6, 2.6, rnd, [t['stone'], t['stoneD'], t['basalt']])
    for sx in (-2.0, 2.0):
        cbox(b, (sx, -0.6, 1.3), (0.2, 0.2, 2.6), t['timber'], bev=0.02)
    tile_roof(b, (0, 0.3, 2.6), 4.4, 3.0, 1.2, rnd, [t['tileS'], t['tileS2']])
    for i in range(10):                                                                        # fornalha
        cbox(b, (1.2 + (i % 2) * 0.05, 0.6, 0.2 + i * 0.3), (1.3 - i * 0.05, 1.2 - i * 0.05, 0.28), [t['stoneD'], t['basalt']][i % 2], bev=0.04)
    cbox(b, (1.2, 0.0, 0.85), (0.7, 0.1, 0.5), t['ember'], bev=0.03)
    chimney(b, 1.2, 0.6, 3.0, 2.0, rnd)
    b.lathe((-1.0, -0.2, 0), [(0.32, 0), (0.25, 0.45), (0.2, 0.5)], 8, t['timber'])            # cepo
    cbox(b, (-1.0, -0.2, 0.62), (0.7, 0.24, 0.2), t['iron'], bev=0.03)                          # bigorna
    b.cone((-1.4, -0.2, 0.62), 0.12, 0.3, 4, t['iron'])
    b.lathe((0.0, -0.4, 0), [(0.3, 0), (0.32, 0.5)], 10, t['plank'])                            # barril d'água
    b.cyl((0.0, -0.4, 0.48), 0.28, 0.28, 0.02, 10, t['water'])
    return _single(name, b)


def templo(name='Templo'):
    _m(); t = T
    rnd = random.Random(17)
    b = B()
    for i in range(3):
        cbox(b, (0, 0.2 - i * 0.35, 0.15 + i * 0.3), (8.0 - i * 0.6, 7.0 - i * 0.6, 0.3), [t['stoneL'], t['stone']][i % 2], bev=0.05)
    stone_wall_block(b, 0, 1.0, 0.9, 6.2, 4.6, 4.2, rnd, [t['stoneL'], t['stone'], t['plaster']])
    for k in range(-2, 3):                                                                       # colunata frontal
        x = k * 1.45
        b.lathe((x, -2.4, 0.9), [(0.34, 0), (0.3, 0.2), (0.26, 0.3), (0.24, 3.6), (0.34, 3.8), (0.38, 4.0)], 10, t['stoneL'])
    cbox(b, (0, -0.6, 5.2), (7.2, 6.2, 0.4), t['stone'], bev=0.06)
    tile_roof(b, (0, -0.6, 5.4), 6.8, 5.8, 2.2, rnd, [t['tileR'], t['tileR2']], overhang=0.25)
    cbox(b, (0, -3.0, 6.1), (0.9, 0.1, 0.9), t['rune'], bev=0.03)                                 # rosácea de brasa
    door(b, 0, -1.3, 0.9, w=1.6, h=2.6)
    for s in (-1, 1):
        b.lathe((s * 3.6, -3.2, 0), [(0.3, 0), (0.25, 0.9), (0.4, 1.1)], 8, t['stone'])
        b.cone((s * 3.6, -3.2, 1.12), 0.28, 0.55, 6, t['ember'])
    return _single(name, b)


def estabulo(name='Estabulo'):
    _m(); t = T
    rnd = random.Random(18)
    b = B()
    cbox(b, (0, 1.2, 1.4), (6.0, 0.25, 2.8), t['plank'], bev=0.02)
    for x in (-3, -1, 1, 3):
        cbox(b, (x, -1.0, 1.4), (0.2, 0.2, 2.8), t['timber'], bev=0.02)
        cbox(b, (x, 1.2, 1.4), (0.2, 0.2, 2.8), t['timber'], bev=0.02)
        if abs(x) < 3:
            cbox(b, (x, 0.1, 0.6), (0.12, 2.0, 1.2), t['plank'], bev=0.02)                       # baias
    for s in (-1, 1):
        cbox(b, (s * 3.0, 0.1, 1.4), (0.2, 2.4, 2.8), t['plank'], bev=0.02)
    tile_roof(b, (0, 0.1, 2.8), 6.2, 2.6, 1.3, rnd, [t['tileM'], t['tileR3']])
    for k in range(4):
        cbox(b, (-2.2 + k * 1.4, -1.6, 0.35), (0.9, 0.6, 0.6), t['hay'] if k % 2 else t['hayD'], bev=0.08, top=(0.9, 0.9))
    for k in range(3):
        b.seg((-3, -1.0, 0.5 + k * 0.4), (3, -1.0, 0.5 + k * 0.4), 0.08, t['plank'], d=0.05)
    return _single(name, b)


# ------------------------------------------------------------------ montagem
CITY_PROPS = ['Casa2A', 'Casa2B', 'Casa2C', 'Sobrado2', 'Muralha2', 'Torre2', 'PortaoArco', 'Fonte2', 'Arvore2A', 'Arvore2B',
              'Pinheiro2A', 'Pinheiro2B', 'Pedra2A', 'Pedra2B', 'Barril', 'Caixote', 'Carroca', 'Poco', 'Banco', 'Vaso', 'Cerca',
              'Estandarte', 'Poste2', 'Braseiro2', 'BauGuardia', 'BarracaR', 'BarracaB', 'BarracaY', 'Forja2', 'Templo', 'Estabulo']


def build_city_props():
    _m(); t = T
    house('Casa2A', 5.0, 4.4, 1, [t['tileR'], t['tileR2'], t['tileR3']], 21, roof_h=2.6)
    house('Casa2B', 4.2, 3.8, 1, [t['tileS'], t['tileS2']], 22, stone_ground=True, roof_h=2.3)
    house('Casa2C', 5.6, 4.0, 1, [t['tileR3'], t['tileR'], mat('T2TileB', '#5c3e2a')], 23, roof_h=2.4)
    house('Sobrado2', 5.2, 4.4, 2, [t['tileR2'], t['tileR'], t['tileR3']], 24, stone_ground=True, jetty=True, roof_h=2.8)
    muralha2(); torre2(); portao2(); fonte2()
    arvore2('Arvore2A', 31); arvore2('Arvore2B', 32, h=3.6)
    pinheiro2('Pinheiro2A', 33); pinheiro2('Pinheiro2B', 34, h=4.2)
    pedra2('Pedra2A', 35); pedra2('Pedra2B', 36, (0.7, 0.9, 0.5))
    barril(); caixote(); carroca(); poco(); banco(); vaso(); cerca(); estandarte(); poste2(); braseiro2(); bau2()
    barraca2('BarracaR', t['clothR'], 41); barraca2('BarracaB', t['clothB'], 42); barraca2('BarracaY', t['clothY'], 43)
    forja2(); templo(); estabulo()
    return CITY_PROPS
