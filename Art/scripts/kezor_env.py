# Kezor — cenário. Requer kezor_lib.py + kezor_monsters.py (finish_custom).

WALL = mat('Wall', '#7b7468')
WALL_TOP = mat('WallTop', '#8f887c')
WALL_D = mat('WallDark', '#655f55')
ROOF_RED = mat('RoofRed', '#7a2e25')
ROOF_BLUE = mat('RoofBlue', '#2f3e5c')
TIMBER = mat('Timber', '#4a3424')
PLASTER = mat('Plaster', '#c9b896')
WINDOW = mat('WindowGlow', '#f5c26b', emit=2.5)
EMBER_L = mat('Lantern', '#ffae42', emit=4.0)
TRUNK = mat('Trunk', '#4a3627')
VOID = mat('Void', '#1c1814')


def env_single(name, coll, b, extra=None):
    wipe(name)
    r = root(name, coll, 'Environment')
    body = b.obj(name + '.Mesh', coll, r, (0, 0, 0), name)
    for pname, bb, pv in (extra or []):
        bb.obj(name + '.' + pname, coll, body, pv, pname)
    return r


def casa(coll, name='CasaEnxaimel'):
    b = B()
    W, D = 4.0, 3.4
    b.box((0, 0, 0.25), (W + 0.2, D + 0.2, 0.5), WALL, top=(0.98, 0.98))              # base de pedra
    b.box((0, 0, 1.4), (W, D, 1.8), PLASTER)                                            # térreo
    b.box((0, 0, 3.05), (W + 0.3, D + 0.3, 1.5), PLASTER)                              # andar em balanço
    # vigas horizontais
    for z, w, d in ((0.56, W + 0.04, D + 0.04), (2.27, W + 0.06, D + 0.06), (2.36, W + 0.34, D + 0.34), (3.78, W + 0.34, D + 0.34)):
        b.box((0, 0, z), (w, d, 0.12), TIMBER)
    # pilares
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((sx * W / 2, sy * D / 2, 1.4), (0.18, 0.18, 1.8), TIMBER)
            b.box((sx * (W + 0.3) / 2, sy * (D + 0.3) / 2, 3.05), (0.18, 0.18, 1.5), TIMBER)
    for x in (-0.8, 0.8):
        b.box((x, -D / 2 - 0.01, 1.4), (0.14, 0.04, 1.8), TIMBER)
        b.box((x, D / 2 + 0.01, 1.4), (0.14, 0.04, 1.8), TIMBER)
    for y in (-0.6, 0.6):
        for sx in (-1, 1):
            b.box((sx * (W / 2 + 0.01), y, 1.4), (0.04, 0.14, 1.8), TIMBER)
    # diagonais do andar de cima (frente e fundos)
    yf = -(D + 0.3) / 2 - 0.02
    for sy in (-1, 1):
        y = sy * ((D + 0.3) / 2 + 0.02)
        for x0, x1 in ((-2.1, -1.0), (-1.0, -2.1), (1.0, 2.1), (2.1, 1.0)):
            b.seg((x0, y, 2.42), (x1, y, 3.72), 0.12, TIMBER, d=0.04, up=(0, 1, 0))
        b.box((0, y, 3.05), (0.14, 0.04, 1.5), TIMBER)
    for sx in (-1, 1):
        x = sx * ((W + 0.3) / 2 + 0.02)
        for y0, y1 in ((-1.8, -0.6), (1.8, 0.6)):
            b.seg((x, y0, 2.42), (x, y1, 3.72), 0.12, TIMBER, d=0.04, up=(1, 0, 0))
    # porta e janelas
    b.box((0.9, -D / 2 - 0.03, 1.15), (0.9, 0.06, 1.3), TIMBER)
    b.box((0.9, -D / 2 - 0.06, 1.15), (0.05, 0.02, 1.2), VOID)
    b.box((0.9, -D / 2 - 0.05, 1.82), (1.1, 0.08, 0.12), TIMBER)
    b.box((0.6, -D / 2 - 0.08, 1.1), (0.06, 0.04, 0.06), mat('Gold', '#d4a94a', rough=0.4, metal=0.7))
    for x, y, z, rx in ((-1.0, -D / 2 - 0.02, 1.45, 0), (-0.45, -(D + 0.3) / 2 - 0.02, 3.05, 0), (0.45, -(D + 0.3) / 2 - 0.02, 3.05, 0),
                        (-1.0, D / 2 + 0.02, 1.45, 0), (0.0, (D + 0.3) / 2 + 0.02, 3.05, 0)):
        b.box((x, y, z), (0.6, 0.05, 0.55), WINDOW)
        b.box((x, y, z - 0.31), (0.75, 0.1, 0.07), TIMBER)
        b.box((x, y, z), (0.05, 0.07, 0.55), TIMBER)
    for sx in (-1, 1):
        x = sx * ((W + 0.3) / 2 + 0.02)
        b.box((x, 0, 3.05), (0.05, 0.6, 0.55), WINDOW)
    # telhado (empenas nas laterais X)
    b.roof((0, 0, 3.84), W + 0.9, D + 1.2, 2.1, ROOF_RED, PLASTER)
    b.box((0, 0, 5.96), (W + 0.95, 0.22, 0.12), mat('RoofRidge', '#5e231c'))
    for sx in (-1, 1):
        x = sx * (W + 0.9) / 2
        b.seg((x, -(D + 1.2) / 2, 3.86), (x, 0, 5.94), 0.14, TIMBER, d=0.1, up=(1, 0, 0))
        b.seg((x, (D + 1.2) / 2, 3.86), (x, 0, 5.94), 0.14, TIMBER, d=0.1, up=(1, 0, 0))
        b.box((x, 0, 4.6), (0.1, 0.14, 1.5), TIMBER)
    b.box((1.2, 0.8, 5.4), (0.55, 0.55, 2.0), WALL, top=(0.95, 0.95))                   # chaminé
    b.box((1.2, 0.8, 6.45), (0.68, 0.68, 0.12), WALL_D)
    # barril e caixote na porta
    b.cyl((-1.75, -2.0, 0.0), 0.3, 0.3, 0.75, 8, mat('Wood', '#6b4a2e'))
    b.cyl((-1.75, -2.0, 0.3), 0.315, 0.315, 0.08, 8, mat('SteelDark', '#5c6670', rough=0.5, metal=0.5))
    b.box((-1.1, -2.05, 0.25), (0.5, 0.5, 0.5), mat('Wood', '#6b4a2e'))
    return env_single(name, coll, b)


def torre(coll, name='TorrePortao'):
    b = B()
    n = 10
    b.lathe((0, 0, 0), [(1.45, 0), (1.35, 0.6), (1.25, 4.4)], n, [WALL_D, WALL])
    b.lathe((0, 0, 4.4), [(1.25, 0), (1.5, 0.35), (1.5, 0.7)], n, [WALL_TOP, WALL_TOP], caps=True)
    for i in range(n):
        an = (i + 0.5) * 2 * math.pi / n
        b.tf(loc=(math.cos(an) * 1.42, math.sin(an) * 1.42, 5.35), rot=(0, 0, math.degrees(an)))
        if i % 2 == 0:
            b.box((0, 0, 0), (0.25, 0.6, 0.5), WALL_TOP)
    b.reset()
    b.lathe((0, 0, 5.3), [(1.0, 0), (1.0, 0.6)], 8, WALL)
    b.lathe((0, 0, 5.85), [(1.3, 0), (0, 2.5)], 8, ROOF_BLUE)
    b.cyl((0, 0, 8.3), 0.03, 0.03, 1.0, 5, TIMBER)
    b.prism([(0.03, 9.25), (0.65, 9.12), (0.5, 8.98), (0.65, 8.85), (0.03, 8.8)], -0.01, 0.01, mat('Banner', '#ff7a2a'))
    for an_deg, z in ((-90, 1.2), (-90, 3.0), (0, 2.2), (180, 2.2), (90, 3.0)):
        an = math.radians(an_deg)
        b.tf(loc=(math.cos(an) * 1.28, math.sin(an) * 1.28, z), rot=(0, 0, an_deg))
        b.box((0, 0, 0), (0.1, 0.12, 0.55), VOID)
    b.reset()
    b.lathe((0, -1.33, 0), [(0.55, 0), (0.55, 1.5), (0.0, 1.95)], 6, TIMBER, sy=0.15, rot0=0)   # porta em arco
    b.box((0, -1.4, 0.05), (1.3, 0.3, 0.1), WALL_D)
    b.box((0.75, -1.45, 1.9), (0.08, 0.3, 0.08), TIMBER)
    b.box((0.75, -1.58, 1.75), (0.22, 0.22, 0.28), EMBER_L)
    return env_single(name, coll, b)


def muralha(coll, name='MuralhaSegmento'):
    b = B()
    L = 4.0
    b.box((0, 0, 0.2), (L, 1.4, 0.4), WALL_D)
    b.box((0, 0, 1.6), (L, 1.2, 2.4), WALL, top=(1, 0.92))
    b.box((0, 0, 2.86), (L, 1.25, 0.12), WALL_TOP)
    for i in range(4):
        x = -L / 2 + 0.5 + i * 1.0
        b.box((x, -0.5, 3.2), (0.6, 0.25, 0.56), WALL_TOP)                               # ameias (lado de fora = -Y)
        b.box((x, -0.63, 3.35), (0.08, 0.02, 0.22), VOID)
    b.box((0, 0.55, 3.05), (L, 0.15, 0.25), WALL_TOP)                                     # parapeito interno
    rnd = random.Random(5)
    for k in range(10):
        x = rnd.uniform(-1.7, 1.7); z = rnd.uniform(0.6, 2.4)
        sy = rnd.choice((-1, 1))
        b.box((x, sy * (0.6 - (z - 0.4) * 0.02), z), (rnd.uniform(0.4, 0.8), 0.06, rnd.uniform(0.2, 0.35)), WALL_D if k % 2 else WALL_TOP)
    return env_single(name, coll, b)


def fonte(coll, name='FonteDeBrasas'):
    ember = mat('EmberPool', '#ff7a2a', emit=3.0)
    gemm = mat('EmberGem', '#ffb347', emit=6.0, rough=0.2)
    gold = mat('Gold', '#d4a94a', rough=0.4, metal=0.7)
    b = B()
    b.lathe((0, 0, 0), [(3.3, 0), (3.2, 0.15), (3.1, 0.6), (3.25, 0.62), (3.25, 0.75)], 8, [WALL_D, WALL, WALL_TOP, WALL_TOP], rot0=math.pi / 8)
    b.cyl((0, 0, 0.2), 2.75, 2.75, 0.45, 8, ember, rot0=math.pi / 8)                    # espelho de brasa
    b.lathe((0, 0, 0.6), [(0.9, 0), (0.8, 0.25), (0.5, 0.35), (0.42, 1.9), (0.62, 2.1), (0.62, 2.3)], 8, [WALL_TOP, WALL, WALL, WALL_TOP, WALL_TOP])
    for i in range(3):
        an = i * 2 * math.pi / 3
        ca, sa = math.cos(an), math.sin(an)
        b.seg((ca * 0.5, sa * 0.5, 2.85), (ca * 0.75, sa * 0.75, 3.3), 0.1, gold, w2=0.07)
        b.seg((ca * 0.75, sa * 0.75, 3.3), (ca * 0.45, sa * 0.45, 3.85), 0.07, gold, w2=0.02)
    for i in range(8):
        an = (i + 0.5) * 2 * math.pi / 8
        if i % 2 == 0:
            b.tf(loc=(math.cos(an) * 3.25, math.sin(an) * 3.25, 0.75), rot=(0, 0, math.degrees(an)))
            b.box((0, 0, 0.25), (0.4, 0.4, 0.5), WALL_TOP)
            b.lathe((0, 0, 0.5), [(0.22, 0), (0.18, 0.12)], 6, VOID)
            b.cone((0, 0, 0.55), 0.14, 0.3, 4, EMBER_L)
            b.reset()
    g = B()
    g.gem((0, 0, 3.4), 0.5, 0.95, gemm, n=6)
    return env_single(name, coll, b, [('Gem', g, (0, 0, 3.4))])


def pinheiro(coll, name='Pinheiro'):
    g1 = mat('PineA', '#2f5a3a'); g2 = mat('PineB', '#376a43')
    b = B()
    b.lathe((0, 0, 0), [(0.26, 0), (0.18, 0.2), (0.14, 1.3)], 6, TRUNK)
    b.cone((0, 0, 0.9), 1.55, 2.0, 7, g1, rot0=0.1)
    b.cone((0, 0, 2.0), 1.2, 1.8, 7, g2, rot0=0.5)
    b.cone((0, 0, 3.0), 0.8, 1.6, 7, g1, rot0=0.9)
    return env_single(name, coll, b)


def arvore_copa(coll, name='ArvoreCopa'):
    leaf = mat('LeafA', '#5b7d34'); leaf2 = mat('LeafB', '#4c6b2c')
    b = B()
    b.lathe((0, 0, 0), [(0.32, 0), (0.22, 0.3), (0.18, 1.7)], 6, TRUNK)
    b.seg((0, 0, 1.5), (0.55, 0.1, 2.2), 0.14, TRUNK, w2=0.08)
    b.seg((0, 0, 1.6), (-0.45, -0.2, 2.3), 0.12, TRUNK, w2=0.07)
    b.ico((0, 0, 2.6), (1.35, 1.3, 1.05), leaf, jit=0.18, seed=1, m2=leaf2)
    b.ico((0.75, 0.2, 2.25), (0.8, 0.8, 0.7), leaf2, jit=0.2, seed=2)
    b.ico((-0.6, -0.35, 2.35), (0.75, 0.75, 0.65), leaf, jit=0.2, seed=3, m2=leaf2)
    return env_single(name, coll, b)


def arvore_morta(coll, name='ArvoreMorta'):
    dead = mat('DeadWood', '#3a312a')
    b = B()
    pts = [(0, 0, 0), (0.05, 0.02, 1.0), (-0.08, 0.05, 2.0), (0.06, -0.04, 3.0), (0.0, 0.0, 3.5)]
    ws = [0.42, 0.3, 0.22, 0.13, 0.04]
    for i in range(len(pts) - 1):
        b.seg(pts[i], pts[i + 1], ws[i], dead, w2=ws[i + 1])
    for p0, p1, w in (((0.05, 0.02, 1.6), (0.8, 0.2, 2.5), 0.14), ((0.8, 0.2, 2.5), (1.1, 0.1, 3.0), 0.07),
                      ((-0.05, 0.04, 2.2), (-0.8, -0.3, 2.9), 0.12), ((-0.8, -0.3, 2.9), (-0.9, -0.5, 3.4), 0.05),
                      ((0.03, 0.0, 2.7), (0.3, 0.6, 3.3), 0.08), ((-0.4, -0.15, 2.55), (-0.6, 0.3, 2.9), 0.05)):
        b.seg(p0, p1, w, dead, w2=w * 0.4)
    for an in (0.3, 2.4, 4.4):
        b.seg((0, 0, 0.3), (math.cos(an) * 0.6, math.sin(an) * 0.6, 0.0), 0.2, dead, w2=0.05)
    return env_single(name, coll, b)


def pedra(coll, name='Pedra'):
    r1 = mat('Stone', '#6e6a64', rough=0.95); r2 = mat('Stone2', '#5c5853', rough=0.95)
    b = B()
    b.ico((0, 0, 0.35), (0.9, 0.75, 0.6), r1, jit=0.22, seed=12, m2=r2, zmin=0.0)
    b.ico((0.75, -0.35, 0.15), (0.35, 0.3, 0.25), r2, jit=0.25, seed=13, zmin=0.0)
    return env_single(name, coll, b)
