# Kezor — humanoides: classes jogáveis, NPCs e monstros humanoides.
# Requer kezor_lib.py executado no mesmo namespace.

PIV = dict(Head=(0, 0, 1.47), ArmL=(0.31, 0, 1.40), ArmR=(-0.31, 0, 1.40), LegL=(0.12, 0, 0.92), LegR=(-0.12, 0, 0.92))
PARTS = ['Head', 'ArmL', 'ArmR', 'LegL', 'LegR']

SKIN = mat('Skin', '#d7a982')
DARK = mat('Dark', '#1a1a1a')
STEEL = mat('Steel', '#8e98a3', rough=0.45, metal=0.6)
STEEL_D = mat('SteelDark', '#5c6670', rough=0.5, metal=0.5)
BLADE = mat('Blade', '#c7cfd8', rough=0.3, metal=0.8)
GOLD = mat('Gold', '#d4a94a', rough=0.4, metal=0.7)
LEATHER = mat('Leather', '#5a3d2a')
LEATHER_L = mat('LeatherLight', '#8a6a3a')
WOOD = mat('Wood', '#6b4a2e')
STRING = mat('String', '#dddddd')


def hparts(k=1.0):
    return {p: B(k) for p in ['Body'] + PARTS}


def sgn(x):
    return 1 if x > 0 else -1


def std_body(b, shirt, belt, hips, w=1.0):
    b.box((0, 0, 0.99), (0.36 * w, 0.22, 0.16), hips)
    b.box((0, 0, 1.22), (0.40 * w, 0.23, 0.32), shirt, top=(1.25, 1.08))
    b.box((0, 0, 1.40), (0.30 * w, 0.18, 0.06), shirt, top=(0.6, 0.8))
    b.box((0, 0, 1.07), (0.39 * w, 0.235, 0.06), belt)


def std_arm(b, x, sleeve, fore, hand, w=1.0):
    b.box((x, 0, 1.22), (0.13 * w, 0.14 * w, 0.36), sleeve, top=(1.15, 1.1))
    b.box((x, 0, 0.93), (0.115 * w, 0.125 * w, 0.28), fore, top=(1.15, 1.1))
    b.box((x, -0.005, 0.735), (0.10 * w, 0.11 * w, 0.11), hand, top=(0.9, 0.9))


def std_leg(b, x, pants, shin, boot, cuff=None):
    b.box((x, 0, 0.71), (0.17, 0.19, 0.42), pants, top=(1.12, 1.08))
    b.box((x, 0, 0.33), (0.14, 0.16, 0.38), shin, top=(1.18, 1.12))
    b.box((x, -0.03, 0.07), (0.16, 0.27, 0.14), boot, top=(0.95, 0.75), shift=(0, 0.03))
    b.box((x, 0, 0.17), (0.155, 0.175, 0.08), cuff or boot)


def std_head(b, skin, hair=None, eyes=DARK):
    b.cyl((0, 0, 1.36), 0.055, 0.05, 0.14, 6, skin)
    b.box((0, 0, 1.60), (0.22, 0.24, 0.25), skin, top=(0.95, 0.95))
    b.box((0, -0.015, 1.47), (0.16, 0.18, 0.05), skin, top=(1.3, 1.25))
    b.box((0, -0.125, 1.585), (0.035, 0.03, 0.05), skin, top=(0.6, 0.6))
    for x in (-0.05, 0.05):
        b.box((x, -0.121, 1.625), (0.042, 0.01, 0.026), eyes)
    if hair is not None:
        b.box((0, 0.02, 1.70), (0.235, 0.25, 0.07), hair)
        b.box((0, 0.09, 1.6), (0.235, 0.08, 0.2), hair)


def finish(name, cat, coll, P, extra=None, piv=PIV):
    wipe(name)
    r = root(name, coll, cat)
    body = P['Body'].obj(name + '.Body', coll, r, (0, 0, 0), 'Body')
    for p in PARTS:
        if p in P and len(P[p].bm.faces):
            P[p].obj(name + '.' + p, coll, body, piv[p], p)
    for (pname, (bb, pv)) in (extra or {}).items():
        bb.obj(name + '.' + pname, coll, body, pv, pname)
    return r


# ---------------------------------------------------------------- armas
def sword(b, hand, rot=(70, 0, 0), blade=BLADE, guard=GOLD, grip=LEATHER, length=0.7):
    b.tf(loc=hand, rot=rot)
    b.box((0, 0, 0), (0.035, 0.035, 0.17), grip)
    b.box((0, 0, -0.1), (0.05, 0.05, 0.05), guard)
    b.box((0, 0, 0.1), (0.24, 0.045, 0.04), guard)
    b.box((0, 0, 0.12 + length / 2), (0.07, 0.018, length), blade)
    b.box((0, 0, 0.17 + length), (0.07, 0.018, 0.1), blade, top=(0.05, 0.4))
    b.box((0, 0, 0.12 + length * 0.45), (0.016, 0.022, length * 0.75), STEEL_D)
    b.reset()


def kite_shield(b, c, face, rim, emblem, rotz=60):
    b.tf(loc=c, rot=(0, 0, rotz))
    kite = [(-0.22, 0.30), (0.22, 0.30), (0.22, 0.0), (0, -0.36), (-0.22, 0.0)]
    b.prism([(x * 1.09, z * 1.07 + 0.005) for x, z in kite], -0.02, 0.03, rim)
    b.prism(kite, -0.035, -0.015, face)
    b.box((0, -0.045, 0.05), (0.09, 0.03, 0.09), emblem)
    b.prism([(0, 0.24), (0.06, 0.12), (0, -0.0), (-0.06, 0.12)], -0.048, -0.034, emblem)
    b.prism([(0, -0.03), (0.04, -0.12), (0, -0.24), (-0.04, -0.12)], -0.048, -0.034, emblem)
    b.reset()


def bow(b, hand, wood=WOOD, grip=LEATHER):
    hx, hy, hz = hand
    pts = []
    for t in range(-4, 5):
        u = t / 4
        pts.append((hx, hy - 0.17 * (1 - u * u), hz + u * 0.62))
    for i in range(len(pts) - 1):
        mid = abs(i - 3.5) < 1
        b.seg(pts[i], pts[i + 1], 0.05 if mid else 0.035, grip if mid else wood, w2=0.05 if mid else 0.03, up=(1, 0, 0))
    # pontas recurvas
    b.seg(pts[0], (hx, hy - 0.06, hz - 0.70), 0.03, wood, w2=0.015, up=(1, 0, 0))
    b.seg(pts[-1], (hx, hy - 0.06, hz + 0.70), 0.03, wood, w2=0.015, up=(1, 0, 0))
    b.seg(pts[0], pts[-1], 0.007, STRING, up=(1, 0, 0))


# ---------------------------------------------------------------- classes
def guardiao(coll, name='GuardiaoRunico', cat='Classes'):
    main = mat('WardenBlue', '#3d5a80', rough=0.5, metal=0.4)
    red = mat('WardenRed', '#9b2c2c')
    pants = mat('WardenPants', '#2b2f3a')
    P = hparts()
    b = P['Body']
    std_body(b, main, LEATHER, pants)
    b.box((0, -0.125, 1.24), (0.06, 0.02, 0.24), STEEL)                         # nervura do peitoral
    b.box((0, -0.122, 1.07), (0.07, 0.012, 0.05), GOLD)                        # fivela
    b.box((0, -0.124, 0.91), (0.2, 0.015, 0.26), red, top=(1.15, 1))           # tabardo
    b.box((0, 0.13, 0.88), (0.44, 0.03, 0.98), red, top=(0.85, 1))             # capa
    b.box((0, 0, 0.965), (0.385, 0.245, 0.11), main, top=(0.95, 0.95))         # faldas
    b.cyl((0, 0, 1.36), 0.13, 0.10, 0.08, 8, STEEL)                            # gorjal
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.31 * side
        std_arm(a, x, pants, STEEL, STEEL_D)
        a.box((x + side * 0.02, 0, 1.39), (0.19, 0.22, 0.13), main, top=(0.6, 0.75))   # ombreira
        a.box((x + side * 0.02, 0, 1.325), (0.195, 0.225, 0.03), red)
        a.box((x, 0, 1.03), (0.13, 0.14, 0.05), STEEL_D)                                   # cotoveleira
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        l = P[part]; x = 0.12 * side
        std_leg(l, x, pants, STEEL, STEEL_D)
        l.box((x, -0.09, 0.52), (0.12, 0.04, 0.1), STEEL, top=(0.8, 1))           # joelheira
    h = P['Head']
    h.cyl((0, 0, 1.36), 0.055, 0.05, 0.14, 6, SKIN)
    h.box((0, 0, 1.60), (0.26, 0.28, 0.27), STEEL, top=(0.92, 0.95))
    h.box((0, 0, 1.765), (0.24, 0.26, 0.06), STEEL, top=(0.6, 0.65))
    h.box((0, 0, 1.48), (0.268, 0.288, 0.03), main)
    h.box((0, -0.141, 1.62), (0.18, 0.01, 0.025), DARK)
    h.box((0, -0.141, 1.565), (0.025, 0.01, 0.08), DARK)
    h.box((0, 0.0, 1.83), (0.04, 0.30, 0.08), red, top=(1, 0.75))               # crista
    h.seg((0, 0.13, 1.84), (0, 0.25, 1.62), 0.04, red, w2=0.02)
    sword(P['ArmR'], (-0.31, -0.03, 0.735))
    kite_shield(P['ArmL'], (0.44, -0.02, 0.95), red, STEEL, GOLD)
    return finish(name, cat, coll, P)


def arcanista(coll, name='ArcanistaDoVeu', cat='Classes'):
    main = mat('ArcPurple', '#4b2d6b')
    dark = mat('ArcDark', '#2a1d3a')
    hood = mat('ArcHood', '#3a2252')
    gem = mat('ArcGem', '#9f7bff', emit=4.0, rough=0.2)
    book = mat('Book', '#5a2a2a')
    P = hparts()
    b = P['Body']
    std_body(b, main, GOLD, dark)
    b.lathe((0, 0, 0), [(0.37, 0.03), (0.36, 0.12), (0.30, 0.55), (0.21, 1.02)], 8, [GOLD, main, main], sy=0.8)
    b.seg((0, -0.292, 0.12), (0, -0.172, 1.0), 0.08, GOLD, d=0.012)
    b.lathe((0, 0, 1.28), [(0.27, 0), (0.2, 0.1), (0.12, 0.16)], 8, hood, sy=0.75)
    b.lathe((0, 0, 1.265), [(0.275, 0), (0.275, 0.03)], 8, GOLD, sy=0.76)
    b.box((0.2, -0.04, 0.99), (0.05, 0.14, 0.18), book)
    b.box((0.08, -0.13, 0.95), (0.06, 0.015, 0.2), GOLD)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.31 * side
        a.box((x, 0, 1.22), (0.13, 0.14, 0.36), main, top=(1.15, 1.1))
        a.lathe((x, 0, 0.80), [(0.10, 0), (0.085, 0.12), (0.065, 0.42)], 6, main)
        a.lathe((x, 0, 0.795), [(0.103, 0), (0.103, 0.035)], 6, GOLD)
        a.box((x, -0.005, 0.735), (0.09, 0.10, 0.11), SKIN, top=(0.9, 0.9))
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        std_leg(P[part], 0.12 * side, dark, dark, LEATHER)
    h = P['Head']
    std_head(h, SKIN)
    h.box((0, 0.02, 1.77), (0.28, 0.30, 0.08), hood, top=(0.6, 0.65))
    for side in (-1, 1):
        h.box((side * 0.135, 0.01, 1.60), (0.035, 0.30, 0.32), hood)
        h.box((side * 0.135, -0.142, 1.60), (0.04, 0.015, 0.32), GOLD)
    h.box((0, 0.15, 1.60), (0.28, 0.04, 0.33), hood)
    h.box((0, -0.135, 1.75), (0.27, 0.015, 0.035), GOLD)
    h.seg((0, 0.10, 1.76), (0, 0.30, 1.60), 0.12, hood, w2=0.01)
    a = P['ArmR']; cx, cy = -0.31, -0.07
    a.cyl((cx, cy, 0.05), 0.03, 0.022, 0.06, 6, GOLD)
    a.cyl((cx, cy, 0.08), 0.022, 0.022, 1.66, 6, WOOD)
    a.cyl((cx, cy, 1.72), 0.035, 0.042, 0.06, 6, GOLD)
    for i in range(3):
        an = i * 2 * math.pi / 3
        ca, sa = math.cos(an), math.sin(an)
        p0 = (cx + 0.03 * ca, cy + 0.03 * sa, 1.76)
        p1 = (cx + 0.085 * ca, cy + 0.085 * sa, 1.9)
        p2 = (cx + 0.03 * ca, cy + 0.03 * sa, 2.03)
        a.seg(p0, p1, 0.022, GOLD, w2=0.016)
        a.seg(p1, p2, 0.016, GOLD, w2=0.006)
    a.gem((cx, cy, 1.9), 0.065, 0.22, gem)
    return finish(name, cat, coll, P)


def patrulheiro(coll, name='PatrulheiroSelvagem', cat='Classes'):
    green = mat('RangerGreen', '#3f6b3a')
    pants = mat('RangerPants', '#4a3a28')
    cap = mat('RangerCap', '#2f4f2c')
    feather = mat('Feather', '#c94f2a')
    hair = mat('HairBrown', '#5b3a22')
    P = hparts()
    b = P['Body']
    std_body(b, green, LEATHER, pants)
    b.lathe((0, 0, 0.76), [(0.235, 0), (0.205, 0.26)], 8, green, sy=0.72)
    b.seg((0.2, -0.127, 1.36), (-0.2, -0.127, 1.08), 0.06, LEATHER, d=0.015)
    b.box((-0.17, -0.1, 1.0), (0.09, 0.06, 0.1), LEATHER_L)
    b.lathe((0, 0, 1.22), [(0.29, 0), (0.22, 0.14), (0.12, 0.2)], 8, cap, sy=0.75)
    q0, q1 = Vector((0.12, 0.17, 0.85)), Vector((-0.08, 0.17, 1.42))
    b.seg(q0, q1, 0.13, LEATHER_L)
    b.seg(q1 - (q1 - q0).normalized() * 0.04, q1, 0.14, LEATHER)
    for i, (dx, dy) in enumerate(((-0.03, -0.03), (0.03, -0.02), (-0.02, 0.03), (0.03, 0.03))):
        s0 = (q1.x + dx, q1.y + dy, q1.z - 0.02)
        s1 = (q1.x + dx - 0.03, q1.y + dy, q1.z + 0.14)
        b.seg(s0, s1, 0.012, WOOD)
        b.seg((s1[0] + 0.012, s1[1], s1[2] - 0.07), s1, 0.035, feather if i % 2 else STRING, w2=0.02, d=0.008)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.31 * side
        std_arm(a, x, green, LEATHER_L, SKIN)
        a.box((x, 0, 0.93), (0.13, 0.14, 0.2), LEATHER, top=(1.1, 1.1))
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        l = P[part]; x = 0.12 * side
        std_leg(l, x, pants, LEATHER_L, LEATHER_L)
        l.box((x, 0, 0.5), (0.165, 0.185, 0.06), LEATHER)
    h = P['Head']
    std_head(h, SKIN, hair=hair)
    h.box((0, 0, 1.72), (0.26, 0.28, 0.05), cap)
    h.box((0, 0.02, 1.79), (0.23, 0.25, 0.1), cap, top=(0.55, 0.5), shift=(0, 0.05))
    h.seg((0, 0.07, 1.83), (0, 0.25, 1.77), 0.09, cap, w2=0.01)
    h.seg((0.12, 0.02, 1.75), (0.17, 0.22, 1.98), 0.02, feather, w2=0.006, d=0.075, d2=0.02)
    bow(P['ArmL'], (0.31, -0.03, 0.735))
    return finish(name, cat, coll, P)


# ---------------------------------------------------------------- NPCs
def talia(coll, name='MercadoraTalia', cat='Npcs'):
    dress = mat('TaliaDress', '#8c5a2b')
    apron = mat('TaliaApron', '#e0c070')
    scarf = mat('TaliaScarf', '#6b3a5a')
    hair = mat('HairAuburn', '#7a3a22')
    potion = mat('PotionRed', '#c0262e', emit=1.5, rough=0.2)
    P = hparts(0.95)
    b = P['Body']
    std_body(b, dress, LEATHER, dress, w=0.92)
    b.lathe((0, 0, 0), [(0.33, 0.03), (0.30, 0.45), (0.2, 1.02)], 8, dress, sy=0.8)
    b.box((0, -0.2, 0.62), (0.26, 0.02, 0.62), apron, top=(0.75, 1), shift=(0, 0.06))
    b.box((0, -0.122, 1.22), (0.22, 0.012, 0.22), apron, top=(1.1, 1))
    b.seg((0.17, -0.12, 1.37), (-0.2, 0.0, 1.0), 0.04, LEATHER, d=0.03)                 # alça da bolsa
    b.box((-0.23, 0.02, 0.96), (0.07, 0.2, 0.16), LEATHER_L, top=(0.9, 0.9))           # bolsa
    b.box((0.2, -0.05, 1.0), (0.06, 0.06, 0.08), mat('PotionBlue', '#2c56c9', emit=1.2, rough=0.2))
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.29 * side
        std_arm(a, x, dress, SKIN, SKIN, w=0.92)
        a.box((x, 0, 1.05), (0.13, 0.14, 0.06), apron)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        std_leg(P[part], 0.11 * side, dress, dress, LEATHER)
    h = P['Head']
    std_head(h, SKIN, hair=hair)
    h.box((0, 0.0, 1.72), (0.25, 0.27, 0.08), scarf, top=(0.85, 0.85))
    h.box((0, 0.11, 1.62), (0.25, 0.06, 0.18), scarf)
    h.seg((0.0, 0.13, 1.55), (0.02, 0.17, 1.25), 0.06, hair, w2=0.03)                  # trança
    h.box((0.02, 0.17, 1.24), (0.04, 0.04, 0.04), scarf)
    a = P['ArmR']
    a.cyl((-0.29, -0.07, 0.64), 0.045, 0.045, 0.1, 6, potion)
    a.cyl((-0.29, -0.07, 0.74), 0.018, 0.018, 0.05, 5, potion)
    a.cyl((-0.29, -0.07, 0.79), 0.022, 0.022, 0.025, 5, WOOD)
    piv = {k: v for k, v in PIV.items()}
    piv['ArmL'] = (0.29, 0, 1.40); piv['ArmR'] = (-0.29, 0, 1.40); piv['LegL'] = (0.11, 0, 0.92); piv['LegR'] = (-0.11, 0, 0.92)
    return finish(name, cat, coll, P, piv=piv)


def bram(coll, name='FerreiroBram', cat='Npcs'):
    shirt = mat('BramShirt', '#2a2a2a')
    apron = mat('BramApron', '#5a4a3a')
    beard = mat('BeardRed', '#7a4a2a')
    pants = mat('BramPants', '#3a3028')
    P = hparts(1.1)
    b = P['Body']
    std_body(b, shirt, LEATHER, pants, w=1.15)
    b.box((0, -0.06, 1.24), (0.48, 0.17, 0.34), shirt, top=(1.12, 1))                 # peito largo
    b.box((0, -0.15, 0.95), (0.36, 0.03, 0.66), apron, top=(0.85, 1))                  # avental
    b.box((0, -0.16, 1.03), (0.12, 0.012, 0.1), LEATHER_L)                              # bolso
    for sx in (-0.13, 0.13):
        b.seg((sx, -0.155, 1.28), (sx * 1.1, -0.05, 1.42), 0.035, apron, d=0.012)
    piv = {k: v for k, v in PIV.items()}
    piv['ArmL'] = (0.34, 0, 1.40); piv['ArmR'] = (-0.34, 0, 1.40)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.34 * side
        a.box((x, 0, 1.30), (0.17, 0.18, 0.2), shirt, top=(1.1, 1.05))
        a.box((x, 0, 1.11), (0.15, 0.16, 0.2), SKIN, top=(1.1, 1.05))
        a.box((x, 0, 0.93), (0.15, 0.16, 0.24), SKIN, top=(0.95, 0.95))
        a.box((x, -0.005, 0.74), (0.13, 0.14, 0.13), LEATHER, top=(0.9, 0.9))
        a.box((x, 0, 0.83), (0.15, 0.16, 0.06), LEATHER)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        std_leg(P[part], 0.12 * side, pants, pants, LEATHER)
    h = P['Head']
    std_head(h, SKIN)
    h.box((0, -0.06, 1.5), (0.23, 0.15, 0.16), beard, top=(1.0, 1.0))
    h.box((0, -0.1, 1.38), (0.17, 0.08, 0.12), beard, top=(1.3, 1.3))
    h.box((0, -0.125, 1.57), (0.12, 0.02, 0.025), beard)                                 # bigode
    for x in (-0.05, 0.05):
        h.box((x, -0.122, 1.65), (0.06, 0.02, 0.02), beard)                             # sobrancelhas
    h.box((0, 0.04, 1.55), (0.235, 0.17, 0.1), beard)                                    # costeletas
    a = P['ArmR']; hx = -0.34
    a.tf(loc=(hx, -0.04, 0.74), rot=(20, 0, 0))
    a.box((0, 0, 0.1), (0.04, 0.04, 0.5), WOOD)
    a.box((0, 0, 0.37), (0.09, 0.22, 0.1), STEEL_D)
    a.box((0, -0.13, 0.37), (0.08, 0.05, 0.08), STEEL)
    a.reset()
    return finish(name, cat, coll, P, piv=piv)


def orvo(coll, name='SentinelaOrvo', cat='Npcs'):
    grey = mat('OrvoGrey', '#6b6b70')
    amber = mat('OrvoAmber', '#ffae42')
    pants = mat('WardenPants', '#2b2f3a')
    P = hparts()
    b = P['Body']
    std_body(b, STEEL_D, LEATHER, pants)
    b.box((0, -0.005, 1.0), (0.42, 0.25, 0.62), grey, top=(1.05, 1.0))                # sobreveste
    b.box((0, -0.13, 1.08), (0.12, 0.012, 0.16), amber)
    b.prism([(0, 0.07), (0.04, -0.01), (0, -0.07), (-0.04, -0.01)], -0.139, -0.135, mat('Ember', '#ff7a2a', emit=2.5))
    b.cyl((0, 0, 1.36), 0.13, 0.11, 0.08, 8, STEEL)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.31 * side
        std_arm(a, x, STEEL_D, STEEL, LEATHER)
        a.box((x + side * 0.015, 0, 1.38), (0.17, 0.2, 0.1), STEEL, top=(0.7, 0.8))
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        std_leg(P[part], 0.12 * side, pants, STEEL_D, LEATHER)
    h = P['Head']
    std_head(h, SKIN, hair=mat('HairDark', '#2a2420'))
    h.box((0, -0.02, 1.6), (0.12, 0.02, 0.02), mat('HairDark', '#2a2420'))
    h.lathe((0, 0, 1.68), [(0.25, 0), (0.25, 0.02), (0.14, 0.04), (0.13, 0.14), (0.0, 0.17)], 8, STEEL)   # chapéu de ferro
    h.lathe((0, 0, 1.67), [(0.252, 0), (0.252, 0.012)], 8, amber)
    a = P['ArmR']; cx, cy = -0.31, -0.08
    a.cyl((cx, cy, 0.02), 0.022, 0.022, 2.05, 6, WOOD)
    a.box((cx, cy, 2.18), (0.07, 0.02, 0.26), BLADE, top=(0.1, 0.5))
    a.box((cx, cy, 2.05), (0.1, 0.04, 0.03), STEEL_D)
    a.box((cx, cy - 0.03, 1.93), (0.015, 0.04, 0.16), amber)
    a = P['ArmL']
    a.tf(loc=(0.43, -0.03, 0.95), rot=(90, 0, 60))
    a.lathe((0, 0, -0.03), [(0.29, 0), (0.29, 0.04)], 8, STEEL)
    a.lathe((0, 0, -0.045), [(0.25, 0), (0.25, 0.02)], 8, amber)
    a.lathe((0, 0, -0.08), [(0.07, 0), (0.0, 0.04)], 8, STEEL)
    a.reset()
    return finish(name, cat, coll, P)


# ---------------------------------------------------------------- monstros humanoides
def ossada(coll, name='OssadaErrante', cat='Monsters'):
    bone = mat('Bone', '#d9d2bf')
    bone_d = mat('BoneDark', '#a89f88')
    rust = mat('Rust', '#7a5034', rough=0.7, metal=0.3)
    socket = mat('Socket', '#2a2620')
    eye = mat('SkullEye', '#6ad7ff', emit=5.0)
    cloth = mat('RagCloth', '#3d3530')
    P = hparts(1.05)
    b = P['Body']
    b.box((0, 0, 0.98), (0.30, 0.18, 0.12), bone, top=(1.15, 1))                     # pelve
    b.seg((0, 0.04, 1.0), (0, 0.06, 1.42), 0.06, bone_d)                               # coluna
    for i in range(4):
        z = 1.14 + i * 0.07
        w = 0.30 + i * 0.03
        b.box((0, 0.0, z), (w, 0.2, 0.035), bone, top=(1.0, 1.0))
    b.box((0, -0.095, 1.24), (0.05, 0.02, 0.22), bone)                                # esterno
    b.box((0, 0.02, 1.39), (0.48, 0.12, 0.05), bone, top=(0.8, 1))                   # clavículas
    b.box((0, -0.03, 0.93), (0.36, 0.25, 0.16), cloth, top=(1.05, 1.0))              # trapo
    b.box((0.08, -0.13, 0.78), (0.14, 0.02, 0.18), cloth, top=(1.0, 1), shift=(0.02, 0))
    b.box((0, 0, 1.03), (0.34, 0.22, 0.04), rust)                                     # cinto enferrujado
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.29 * side
        a.box((x, 0, 1.39), (0.1, 0.1, 0.08), bone)
        a.seg((x, 0, 1.38), (x, 0, 1.05), 0.055, bone)
        a.box((x, 0, 1.04), (0.07, 0.07, 0.06), bone_d)
        a.seg((x, 0, 1.03), (x, -0.02, 0.78), 0.045, bone)
        a.box((x, -0.02, 0.74), (0.08, 0.09, 0.07), bone_d)
        for f in (-1, 0, 1):
            a.seg((x + f * 0.025, -0.03, 0.71), (x + f * 0.028, -0.05, 0.64), 0.015, bone)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        l = P[part]; x = 0.11 * side
        l.seg((x, 0, 0.92), (x, 0, 0.52), 0.065, bone)
        l.box((x, -0.01, 0.5), (0.08, 0.08, 0.07), bone_d)
        l.seg((x, 0, 0.48), (x, 0, 0.1), 0.05, bone)
        l.box((x, -0.06, 0.04), (0.1, 0.2, 0.06), bone, top=(0.9, 0.8))
    h = P['Head']
    h.seg((0, 0.03, 1.36), (0, 0.02, 1.5), 0.05, bone_d)
    h.box((0, 0, 1.64), (0.22, 0.25, 0.22), bone, top=(0.85, 0.9))                   # crânio
    h.box((0, -0.03, 1.5), (0.16, 0.17, 0.07), bone, top=(1.2, 1.2))                 # maxilar
    h.box((0, -0.04, 1.44), (0.14, 0.15, 0.04), bone_d)                               # mandíbula
    for x in (-0.05, 0.05):
        h.box((x, -0.122, 1.6), (0.07, 0.02, 0.06), socket)
        h.box((x, -0.13, 1.6), (0.03, 0.01, 0.025), eye)
    h.box((0, -0.124, 1.53), (0.03, 0.02, 0.04), socket)
    for i in range(5):
        h.box((-0.06 + i * 0.03, -0.12, 1.475), (0.018, 0.012, 0.025), bone)
    h.box((0.02, 0, 1.75), (0.24, 0.27, 0.06), rust, top=(0.7, 0.75))                # elmo quebrado
    h.box((0.0, 0.0, 1.72), (0.25, 0.27, 0.03), rust)
    sword(P['ArmR'], (-0.29, -0.04, 0.74), rot=(60, 0, 0), blade=rust, guard=rust, grip=cloth, length=0.62)
    return finish(name, cat, coll, P, piv={'Head': (0, 0, 1.47), 'ArmL': (0.29, 0, 1.40), 'ArmR': (-0.29, 0, 1.40), 'LegL': (0.11, 0, 0.92), 'LegR': (-0.11, 0, 0.92)})


def cria(coll, name='CriaDasCinzas', cat='Monsters'):
    ash = mat('Ash', '#2a2422')
    ash_d = mat('AshDark', '#1e1a19')
    ember = mat('EmberHot', '#ff7a2a', emit=4.0)
    iron = mat('AshIron', '#3a3533', rough=0.6, metal=0.5)
    P = hparts(0.85)
    b = P['Body']
    b.box((0, 0.02, 0.99), (0.34, 0.22, 0.16), ash_d)
    b.box((0, 0.04, 1.2), (0.40, 0.25, 0.32), ash, top=(1.2, 1.1), shift=(0, 0.05))  # corcunda
    b.box((0, 0.1, 1.38), (0.30, 0.2, 0.08), ash, top=(0.6, 0.8))
    b.lathe((0, 0.02, 0.3), [(0.3, 0), (0.26, 0.4), (0.2, 0.72)], 7, ash, sy=0.8)      # trapos
    for i in range(7):
        an = (i + 0.5) * 2 * math.pi / 7
        b.seg((math.cos(an) * 0.28, 0.02 + math.sin(an) * 0.22, 0.32), (math.cos(an) * 0.3, 0.02 + math.sin(an) * 0.24, 0.12 + (i % 3) * 0.06), 0.1, ash_d, w2=0.02, d=0.02)
    b.seg((-0.12, -0.115, 1.32), (0.02, -0.13, 1.18), 0.025, ember, d=0.01)            # rachaduras
    b.seg((0.02, -0.13, 1.18), (-0.05, -0.135, 1.05), 0.022, ember, d=0.01)
    b.seg((0.02, -0.13, 1.18), (0.13, -0.12, 1.12), 0.018, ember, d=0.01)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.31 * side
        a.box((x, 0, 1.22), (0.11, 0.12, 0.36), ash, top=(1.15, 1.1))
        a.box((x, -0.02, 0.93), (0.09, 0.1, 0.28), ash_d, top=(1.2, 1.1))
        a.box((x, -0.03, 0.75), (0.1, 0.1, 0.08), ash)
        for f in (-1, 0, 1):
            a.seg((x + f * 0.03, -0.04, 0.72), (x + f * 0.04, -0.09, 0.6), 0.02, ember, w2=0.004)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        l = P[part]; x = 0.12 * side
        l.seg((x, 0, 0.92), (x, -0.04, 0.5), 0.12, ash_d, w2=0.09)
        l.seg((x, -0.04, 0.5), (x, 0.03, 0.1), 0.09, ash, w2=0.07)
        l.box((x, -0.06, 0.05), (0.13, 0.22, 0.1), ash_d, top=(0.7, 0.6))
    h = P['Head']
    h.box((0, -0.05, 1.55), (0.3, 0.32, 0.3), ash, top=(0.65, 0.7), shift=(0, 0.04))
    h.seg((0, 0.05, 1.68), (0, 0.3, 1.82), 0.13, ash, w2=0.01)
    h.box((0, -0.19, 1.52), (0.2, 0.04, 0.2), ash_d)                                       # rosto vazio
    for x in (-0.05, 0.05):
        h.box((x, -0.215, 1.55), (0.05, 0.02, 0.025), ember, top=(0.6, 1))
    a = P['ArmR']
    a.tf(loc=(-0.31, -0.05, 0.74), rot=(75, 0, 0))
    a.box((0, 0, 0.0), (0.04, 0.04, 0.2), ash_d)
    a.prism([(-0.02, 0.1), (0.02, 0.1), (0.1, 0.32), (0.06, 0.45), (0.11, 0.6), (-0.02, 0.66), (-0.05, 0.4)], -0.012, 0.012, iron)
    a.prism([(0.1, 0.32), (0.13, 0.34), (0.09, 0.45), (0.14, 0.6), (0.11, 0.6), (0.06, 0.45)], -0.008, 0.008, ember)
    a.reset()
    return finish(name, cat, coll, P)


def arauto(coll, name='ArautoDasCinzas', cat='Monsters'):
    robe = mat('HeraldRobe', '#1f1a1a')
    robe2 = mat('HeraldRobe2', '#2e2422')
    ember = mat('HeraldEmber', '#ff5a1a', emit=5.0)
    gold = mat('HeraldGold', '#b58a3a', rough=0.4, metal=0.7)
    mask = mat('HeraldMask', '#3a2f2a')
    P = {p: B() for p in ['Body', 'Head', 'ArmL', 'ArmR']}
    b = P['Body']
    b.lathe((0, 0, 0), [(0.0, 0.35), (0.42, 0.55), (0.62, 1.0), (0.6, 1.5), (0.5, 1.9), (0.42, 2.12), (0.22, 2.3)], 7, [robe2, robe, robe, robe, robe, robe2], sy=0.8)
    for i in range(7):
        an = (i + 0.5) * 2 * math.pi / 7
        r0 = 0.5
        b.seg((math.cos(an) * r0, math.sin(an) * r0 * 0.8, 0.62), (math.cos(an) * 0.4, math.sin(an) * 0.32, 0.12 + (i % 2) * 0.12), 0.18, robe, w2=0.03, d=0.03)
    b.box((0, -0.43, 1.25), (0.14, 0.06, 0.9), ember, top=(0.6, 1))                    # fenda de brasa
    b.box((0, -0.46, 1.25), (0.22, 0.04, 0.95), gold, top=(0.6, 1))
    b.lathe((0, 0, 2.05), [(0.46, 0), (0.42, 0.22), (0.36, 0.42)], 7, robe2, sy=0.75, caps=False)   # gola alta
    b.gem((0, -0.36, 1.85), 0.1, 0.26, ember)
    for side in (-1, 1):
        b.box((side * 0.48, 0, 2.15), (0.3, 0.38, 0.14), gold, top=(0.6, 0.7))
        b.cone((side * 0.55, 0, 2.2), 0.06, 0.35, 4, gold)
    h = P['Head']
    h.box((0, 0, 2.47), (0.36, 0.38, 0.42), robe, top=(0.7, 0.8))                       # capuz
    h.box((0, -0.15, 2.45), (0.24, 0.1, 0.3), mask, top=(0.85, 1))                      # máscara
    for x in (-0.06, 0.06):
        h.box((x, -0.205, 2.48), (0.07, 0.015, 0.025), ember)
    h.lathe((0, 0, 2.66), [(0.21, 0), (0.21, 0.05)], 7, gold, sy=0.95)
    for i in range(7):
        an = (i + 0.25) * 2 * math.pi / 7
        x, y = math.cos(an) * 0.2, math.sin(an) * 0.19
        tip = (math.cos(an) * 0.3, math.sin(an) * 0.29, 2.98 + (i % 2) * 0.12)
        h.seg((x, y, 2.68), tip, 0.055, gold if i % 2 else ember, w2=0.005)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.6 * side
        a.box((x, 0, 1.8), (0.2, 0.22, 0.5), robe, top=(1.2, 1.15))
        a.lathe((x, 0, 1.0), [(0.17, 0), (0.12, 0.3), (0.1, 0.56)], 6, robe2)
        a.box((x, -0.01, 0.94), (0.1, 0.1, 0.12), mask)
        for f in (-1, 0, 1):
            a.seg((x + f * 0.03, -0.03, 0.9), (x + f * 0.04, -0.07, 0.72), 0.022, gold, w2=0.004)
    a = P['ArmR']; cx, cy = -0.6, -0.1
    a.cyl((cx, cy, 0.2), 0.03, 0.03, 2.3, 6, gold)
    a.gem((cx, cy, 2.75), 0.13, 0.4, ember)
    for i in range(4):
        an = i * math.pi / 2
        a.seg((cx + 0.04 * math.cos(an), cy + 0.04 * math.sin(an), 2.48), (cx + 0.16 * math.cos(an), cy + 0.16 * math.sin(an), 2.9), 0.03, gold, w2=0.005)
    extra = {}
    for i in range(3):
        an = i * 2 * math.pi / 3 + math.pi / 2
        c = (math.cos(an) * 1.15, math.sin(an) * 1.15, 1.7)
        nb = B()
        nb.ico(c, (0.14, 0.14, 0.14), ember, jit=0.12, seed=90 + i)
        for k in range(3):
            ak = k * 2 * math.pi / 3
            nb.seg((c[0] + math.cos(ak) * 0.16, c[1] + math.sin(ak) * 0.16, c[2] - 0.1), (c[0] + math.cos(ak) * 0.2, c[1] + math.sin(ak) * 0.2, c[2] + 0.12), 0.06, robe2, w2=0.01)
        extra['Orb%d' % i] = (nb, c)
    piv = {'Head': (0, 0, 2.28), 'ArmL': (0.6, 0, 2.08), 'ArmR': (-0.6, 0, 2.08)}
    return finish(name, cat, coll, P, extra=extra, piv=piv)
