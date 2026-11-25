# Kezor — marco 2: monstros e cenário do Pântano de Turfa Negra e da Cripta de Vidro Negro.
# Requer kezor_lib.py, kezor_humans.py, kezor_monsters.py, kezor_env.py e kezor_world2.py (W2_ITEMS, _done) antes.


# ================================================================ Pântano de Turfa Negra — monstros
def rastejante(coll, name='RastejanteTurfa', cat='Monsters'):
    mud = mat('PeatMud', '#3e3326'); mud2 = mat('PeatMud2', '#2c241b')
    moss = mat('PeatMoss', '#4a5a2a'); eye = mat('PeatEye', '#e8ff6a', emit=4.0); fang = mat('Fang', '#e8e2d0')
    b = B()
    b.ico((0, 0.15, 0.42), (0.5, 0.95, 0.38), mud, jit=0.12, seed=21, m2=mud2, zmin=0.06)
    for i, y in enumerate((-0.3, 0.1, 0.5)):
        b.ico((0.0, y, 0.74), (0.28, 0.2, 0.08), moss, jit=0.3, seed=30 + i)
    h = B()
    h.ico((0, -0.75, 0.45), (0.32, 0.3, 0.26), mud2, jit=0.1, seed=22)
    for x in (-0.12, 0.12):
        h.box((x, -1.0, 0.55), (0.07, 0.03, 0.05), eye)
        h.seg((x, -0.95, 0.32), (x * 0.4, -1.18, 0.26), 0.05, fang, w2=0.01)
    parts = [('Head', h, (0, -0.6, 0.45))]
    for i in range(6):
        side = 1 if i < 3 else -1
        y = -0.35 + (i % 3) * 0.45
        l = B()
        l.seg((side * 0.38, y, 0.32), (side * 0.62, y - 0.05, 0.05), 0.08, mud2, w2=0.04)
        parts.append(('Leg%d' % i, l, (side * 0.38, y, 0.32)))
    return _done(finish_custom(name, cat, coll, b, parts))


def bruxa(coll, name='BruxaJunco', cat='Monsters'):
    rag = mat('HagRag', '#3b4a33'); reed = mat('Reed', '#8a8a4a'); skin = mat('HagSkin', '#7d8a6a')
    hair = mat('HagHair', '#c8c8b8'); glow = mat('HagLantern', '#9bff6a', emit=4.0)
    P = hparts()
    b = P['Body']
    b.lathe((0, 0, 0), [(0.36, 0.0), (0.33, 0.35), (0.22, 0.95)], 7, rag, sy=0.85)
    for i in range(10):
        a = i * 2 * math.pi / 10
        b.seg((math.cos(a) * 0.3, math.sin(a) * 0.26, 0.5), (math.cos(a) * 0.4, math.sin(a) * 0.34, 0.0), 0.04, reed, w2=0.015)
    b.box((0, 0.06, 1.2), (0.36, 0.26, 0.34), rag, top=(1.1, 1.0), shift=(0, -0.08))
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.27 * side
        a.seg((x, -0.02, 1.36), (x, -0.06, 0.95), 0.07, rag)
        a.seg((x, -0.06, 0.95), (x, -0.1, 0.62), 0.05, skin)
        for f in (-1, 0, 1):
            a.seg((x + f * 0.02, -0.1, 0.62), (x + f * 0.03, -0.14, 0.5), 0.012, skin)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        P[part].seg((0.1 * side, 0, 0.6), (0.1 * side, -0.02, 0.05), 0.07, skin)
    h = P['Head']
    h.box((0, -0.06, 1.52), (0.2, 0.22, 0.22), skin, top=(0.85, 0.9))
    h.seg((0, -0.17, 1.52), (0, -0.25, 1.44), 0.04, skin, w2=0.015)
    for x in (-0.05, 0.05):
        h.box((x, -0.172, 1.57), (0.035, 0.01, 0.02), glow)
    h.box((0, 0.0, 1.62), (0.26, 0.26, 0.1), hair, top=(1.1, 1.1))
    h.seg((0, 0.08, 1.6), (0, 0.16, 1.2), 0.18, hair, w2=0.08)
    for i in range(5):
        a = -0.6 + i * 0.3
        h.seg((math.sin(a) * 0.1, 0.0, 1.66), (math.sin(a) * 0.22, 0.05, 1.95), 0.025, reed, w2=0.008)
    a = P['ArmR']
    a.seg((-0.27, -0.12, 0.1), (-0.27, -0.12, 1.75), 0.04, reed)
    a.box((-0.27, -0.12, 1.82), (0.1, 0.1, 0.12), glow)
    return _done(finish(name, cat, coll, P, piv={'Head': (0, -0.04, 1.42), 'ArmL': (0.27, 0, 1.38), 'ArmR': (-0.27, 0, 1.38), 'LegL': (0.1, 0, 0.6), 'LegR': (-0.1, 0, 0.6)}))


def carnical(coll, name='CarnicalAfogado', cat='Monsters'):
    skin = mat('GhoulSkin', '#5f7068'); skin2 = mat('GhoulSkin2', '#4a5852'); rag = mat('GhoulRag', '#2e3a3a')
    weed = mat('Seaweed', '#2f5a3a'); eye = mat('GhoulEye', '#6affe0', emit=4.0)
    P = hparts(1.08)
    b = P['Body']
    b.ico((0, 0, 1.15), (0.3, 0.24, 0.32), skin, jit=0.08, seed=41, m2=skin2)
    b.box((0, 0, 0.9), (0.36, 0.26, 0.2), rag, top=(1.05, 1))
    for i in range(4):
        b.seg((-0.15 + i * 0.1, -0.2, 1.3), (-0.15 + i * 0.1 + 0.03, -0.24, 0.9), 0.03, weed, d=0.01)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.32 * side
        a.seg((x, 0, 1.36), (x, -0.06, 0.92), 0.1, skin)
        a.seg((x, -0.06, 0.92), (x, -0.12, 0.55), 0.08, skin2)
        for f in (-1, 0, 1):
            a.seg((x + f * 0.03, -0.12, 0.55), (x + f * 0.04, -0.17, 0.42), 0.02, skin)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        l = P[part]; x = 0.12 * side
        l.seg((x, 0, 0.88), (x, 0, 0.45), 0.11, skin2)
        l.seg((x, 0, 0.45), (x, -0.02, 0.06), 0.09, skin)
        l.box((x, -0.06, 0.04), (0.12, 0.2, 0.06), skin2)
    h = P['Head']
    h.ico((0, -0.03, 1.6), (0.15, 0.16, 0.16), skin, jit=0.1, seed=42)
    h.box((0, -0.12, 1.5), (0.16, 0.08, 0.06), skin2)
    for x in (-0.05, 0.05):
        h.box((x, -0.16, 1.62), (0.04, 0.01, 0.025), eye)
    h.seg((0.05, 0.05, 1.72), (0.1, 0.12, 1.4), 0.05, weed, d=0.01)
    return _done(finish(name, cat, coll, P))


def mariposa(coll, name='MariposaFatua', cat='Monsters'):
    fur = mat('MothFur', '#5a4e6a'); wing = mat('MothWing', '#8a7aa0'); spot = mat('MothSpot', '#bfff8a', emit=4.0)
    b = B()
    b.ico((0, 0.0, 1.5), (0.18, 0.4, 0.18), fur, jit=0.08, seed=51)
    b.ico((0, -0.42, 1.55), (0.14, 0.14, 0.14), fur, seed=52)
    for x in (-0.06, 0.06):
        b.box((x, -0.53, 1.6), (0.06, 0.02, 0.05), spot)
        b.seg((x, -0.5, 1.68), (x * 4, -0.7, 1.95), 0.015, fur, w2=0.005)
    parts = []
    for side, pname in ((1, 'WingL'), (-1, 'WingR')):
        w = B()
        pts = [(0.12 * side, -0.2, 1.55), (0.95 * side, -0.45, 1.95), (1.1 * side, 0.1, 1.6), (0.6 * side, 0.5, 1.3), (0.12 * side, 0.25, 1.45)]
        w.poly(pts, [(0, 1, 2, 3, 4)], wing)
        w.poly(pts, [(4, 3, 2, 1, 0)], wing)
        w.gem((0.65 * side, -0.05, 1.68), 0.1, 0.06, spot)
        parts.append((pname, w, (0.12 * side, 0, 1.5)))
    for i in range(3):
        o = B(); o.gem((0.9, 0, 1.4), 0.06, 0.12, spot)
        parts.append(('Orb%d' % i, o, (0.9, 0, 1.4)))
    return _done(finish_custom(name, cat, coll, b, parts))


# ================================================================ Cripta de Vidro Negro — monstros
def _glass():
    return (mat('BlackGlass', '#1c1a26', rough=0.15, metal=0.3), mat('BlackGlass2', '#2c2838', rough=0.2, metal=0.3),
            mat('GlassCrack', '#8a6aff', emit=4.0), mat('GlassCyan', '#6ae0ff', emit=4.0))


def sentinela_vidro(coll, name='SentinelaVidro', cat='Monsters'):
    g1, g2, crack, cyan = _glass()
    P = hparts()
    b = P['Body']
    b.box((0, 0, 1.2), (0.6, 0.34, 0.5), g1, top=(1.2, 1.05))
    b.box((0, 0, 0.92), (0.44, 0.3, 0.2), g2)
    b.box((0, -0.175, 1.22), (0.05, 0.012, 0.36), cyan)
    b.gem((0, -0.18, 1.32), 0.07, 0.16, cyan)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.4 * side
        a.box((x, 0, 1.42), (0.28, 0.3, 0.2), g1, top=(0.7, 0.8))
        a.box((x, 0, 1.1), (0.18, 0.2, 0.4), g2)
        a.box((x, -0.02, 0.78), (0.2, 0.22, 0.26), g1, top=(1.2, 1.2))
        a.box((x, -0.115, 0.8), (0.03, 0.01, 0.18), crack)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        l = P[part]; x = 0.15 * side
        l.box((x, 0, 0.62), (0.2, 0.22, 0.4), g2)
        l.box((x, -0.02, 0.2), (0.22, 0.28, 0.4), g1, top=(1.1, 1.0))
    h = P['Head']
    h.gem((0, 0, 1.68), 0.17, 0.42, g1, n=5)
    h.box((0, -0.12, 1.66), (0.14, 0.01, 0.03), cyan)
    return _done(finish(name, cat, coll, P, piv={'Head': (0, 0, 1.47), 'ArmL': (0.4, 0, 1.45), 'ArmR': (-0.4, 0, 1.45), 'LegL': (0.15, 0, 0.82), 'LegR': (-0.15, 0, 0.82)}))


def aranha_obsidiana(coll, name='AranhaObsidiana', cat='Monsters'):
    g1, g2, crack, cyan = _glass()
    b = B()
    b.ico((0, 0.35, 0.55), (0.5, 0.6, 0.42), g1, jit=0.06, seed=61, m2=g2)
    b.ico((0, -0.3, 0.5), (0.3, 0.32, 0.26), g2, seed=62)
    b.gem((0, 0.35, 0.98), 0.12, 0.3, crack)
    for i, x in enumerate((-0.09, -0.04, 0.04, 0.09)):
        b.box((x, -0.58, 0.56 + (i % 2) * 0.05), (0.04, 0.02, 0.04), crack)
    parts = []
    for i in range(8):
        side = 1 if i < 4 else -1
        k = i % 4
        y = -0.4 + k * 0.22
        l = B()
        knee = (side * 0.75, y - 0.05 + k * 0.05, 0.85)
        l.seg((side * 0.25, y, 0.5), knee, 0.06, g1, w2=0.045)
        l.seg(knee, (side * 1.05, y - 0.1 + k * 0.1, 0.02), 0.045, g2, w2=0.015)
        parts.append(('Leg%d' % i, l, (side * 0.25, y, 0.5)))
    return _done(finish_custom(name, cat, coll, b, parts))


def espectro(coll, name='EspectroVitreo', cat='Monsters'):
    g1, g2, crack, cyan = _glass()
    robe = mat('WraithRobe', '#2a2440')
    b = B()
    b.lathe((0, 0, 0.5), [(0.0, 0.0), (0.22, 0.25), (0.34, 0.8), (0.24, 1.3), (0.14, 1.45)], 7, robe)
    b.ico((0, 0, 2.05), (0.17, 0.17, 0.2), g1, seed=71)
    b.box((0, -0.15, 2.05), (0.14, 0.02, 0.03), cyan)
    b.lathe((0, 0, 1.85), [(0.26, 0), (0.2, 0.3), (0.0, 0.42)], 7, robe)
    parts = []
    for side, pname in ((1, 'ArmL'), (-1, 'ArmR')):
        a = B()
        a.seg((side * 0.25, 0, 1.75), (side * 0.45, -0.25, 1.35), 0.09, robe, w2=0.05)
        a.seg((side * 0.45, -0.25, 1.35), (side * 0.5, -0.4, 1.15), 0.03, g2, w2=0.008)
        parts.append((pname, a, (side * 0.25, 0, 1.75)))
    for i in range(3):
        o = B(); o.gem((1.0, 0, 1.6), 0.08, 0.25, crack)
        parts.append(('Orb%d' % i, o, (1.0, 0, 1.6)))
    return _done(finish_custom(name, cat, coll, b, parts))


def soberano(coll, name='SoberanoVidroNegro', cat='Monsters'):
    g1, g2, crack, cyan = _glass()
    gold = mat('SovGold', '#9a7a3a', rough=0.4, metal=0.7)
    P = hparts()
    b = P['Body']
    b.box((0, 0, 1.22), (0.5, 0.3, 0.4), g1, top=(1.25, 1.05))
    b.box((0, 0, 0.95), (0.42, 0.28, 0.2), g2)
    b.lathe((0, 0, 0.35), [(0.38, 0), (0.3, 0.5)], 8, g2, sy=0.75)
    b.gem((0, -0.17, 1.28), 0.1, 0.24, crack)
    b.box((0, 0.17, 0.95), (0.5, 0.03, 0.95), mat('SovCape', '#3a1f4a'), top=(0.8, 1))
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.33 * side
        a.box((x, 0, 1.4), (0.26, 0.28, 0.16), g1, top=(0.6, 0.75))
        for k in range(3):
            a.seg((x + side * 0.05, 0, 1.46), (x + side * (0.12 + k * 0.05), 0.02 * k, 1.62 + k * 0.05), 0.04, crack, w2=0.008)
        a.box((x, 0, 1.12), (0.14, 0.15, 0.36), g2)
        a.box((x, -0.01, 0.82), (0.15, 0.16, 0.26), g1)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        l = P[part]; x = 0.12 * side
        l.box((x, 0, 0.66), (0.18, 0.2, 0.42), g2)
        l.box((x, -0.02, 0.24), (0.18, 0.26, 0.48), g1, top=(1.05, 0.9))
    h = P['Head']
    h.box((0, 0, 1.62), (0.24, 0.26, 0.28), g1, top=(0.9, 0.95))
    h.box((0, -0.135, 1.62), (0.17, 0.01, 0.03), crack)
    for i in range(5):
        a = -0.8 + i * 0.4
        h.seg((math.sin(a) * 0.1, math.cos(a) * 0.02, 1.76), (math.sin(a) * 0.16, 0.02, 2.0 - abs(a) * 0.08), 0.04, crack, w2=0.008)
    h.box((0, 0, 1.765), (0.25, 0.27, 0.03), gold)
    a = P['ArmR']
    a.tf(loc=(-0.33, -0.04, 0.72), rot=(70, 0, 0))
    a.box((0, 0, 0), (0.05, 0.05, 0.24), gold)
    a.box((0, 0, 0.14), (0.34, 0.06, 0.05), gold)
    a.box((0, 0, 0.75), (0.12, 0.03, 1.15), g1)
    a.box((0, 0, 0.75), (0.02, 0.035, 1.0), crack)
    a.box((0, 0, 1.4), (0.12, 0.03, 0.16), g1, top=(0.05, 0.4))
    a.reset()
    return _done(finish(name, cat, coll, P))


# ================================================================ cenário dos mapas novos
def junco(coll, name='Junco'):
    reed = mat('Reed', '#8a8a4a'); reed2 = mat('Reed2', '#5f6a34'); top = mat('ReedTop', '#4a3a24')
    b = B()
    rnd = random.Random(81)
    for i in range(9):
        a = rnd.uniform(0, 6.28); r = rnd.uniform(0, 0.35); h = rnd.uniform(0.9, 1.6)
        x, y = math.cos(a) * r, math.sin(a) * r
        b.seg((x, y, 0), (x * 1.3, y * 1.3, h), 0.035, reed if i % 2 else reed2, w2=0.012)
        if i % 3 == 0:
            b.seg((x * 1.3, y * 1.3, h - 0.25), (x * 1.32, y * 1.32, h), 0.05, top)
    return _done(env_single(name, coll, b))


def toco(coll, name='TocoPantano'):
    bark = mat('SwampBark', '#3a3128'); moss = mat('PeatMoss', '#4a5a2a')
    b = B()
    b.lathe((0, 0, 0), [(0.55, 0), (0.42, 0.25), (0.36, 0.9), (0.3, 1.2)], 7, bark)
    for a in (0.3, 2.2, 4.1):
        b.seg((math.cos(a) * 0.35, math.sin(a) * 0.35, 0.2), (math.cos(a) * 0.95, math.sin(a) * 0.95, -0.05), 0.14, bark, w2=0.05)
    b.ico((0.1, 0.0, 1.2), (0.32, 0.28, 0.08), moss, jit=0.3, seed=82)
    return _done(env_single(name, coll, b))


def cogumelo(coll, name='CogumeloFatuo'):
    stem = mat('MushStemPale', '#d8d0b8'); cap = mat('FatuoCap', '#7affc8', emit=3.0); cap2 = mat('FatuoCap2', '#4ad0a0', emit=2.0)
    b = B()
    for (x, y, s) in ((0, 0, 1.0), (0.25, 0.1, 0.65), (-0.2, 0.18, 0.5), (0.05, -0.25, 0.4)):
        b.cyl((x, y, 0), 0.05 * s, 0.04 * s, 0.4 * s, 5, stem)
        b.lathe((x, y, 0.38 * s), [(0.2 * s, 0), (0.15 * s, 0.07 * s), (0, 0.11 * s)], 6, cap if s > 0.6 else cap2)
    return _done(env_single(name, coll, b))


def cristal(coll, name='CristalNegro'):
    g1, g2, crack, cyan = _glass()
    b = B()
    rnd = random.Random(91)
    b.ico((0, 0, 0.1), (0.6, 0.6, 0.2), mat('CryptStone', '#3a3842'), jit=0.2, seed=92, zmin=0)
    for i in range(6):
        a = rnd.uniform(0, 6.28); r = rnd.uniform(0, 0.35); h = rnd.uniform(0.6, 1.6)
        x, y = math.cos(a) * r, math.sin(a) * r
        b.seg((x, y, 0.1), (x + x * 0.6, y + y * 0.6, h), 0.2, g1 if i % 2 else g2, w2=0.02)
        if i % 2 == 0:
            b.seg((x, y, 0.3), (x + x * 0.42, y + y * 0.42, h * 0.8), 0.05, crack, w2=0.01)
    return _done(env_single(name, coll, b))


def pilar(coll, name='PilarCripta'):
    st = mat('CryptStone', '#3a3842'); st2 = mat('CryptStone2', '#4a4854'); g1, g2, crack, cyan = _glass()
    b = B()
    b.box((0, 0, 0.15), (1.1, 1.1, 0.3), st2)
    b.box((0, 0, 2.0), (0.75, 0.75, 3.4), st, top=(0.92, 0.92))
    b.box((0, 0, 3.8), (1.0, 1.0, 0.3), st2)
    for (dx, dy) in ((0, -0.38), (0, 0.38), (0.38, 0), (-0.38, 0)):
        b.box((dx, dy, 2.2), (0.12 if dy else 0.02, 0.02 if dy else 0.12, 1.2), crack)
    return _done(env_single(name, coll, b))


def parede(coll, name='ParedeCripta'):
    st = mat('CryptStone', '#3a3842'); st2 = mat('CryptStone2', '#4a4854')
    b = B()
    b.box((0, 0, 1.6), (2.0, 0.8, 3.2), st)          # bloco de 2 m (X) por 0,8 m, 3,2 m de altura
    b.box((0, 0, 3.3), (2.05, 0.9, 0.2), st2)
    for x in (-0.5, 0.5):
        b.box((x, -0.405, 1.6), (0.9, 0.02, 0.06), st2)
    return _done(env_single(name, coll, b))


def build_maps():
    W2_ITEMS.clear()
    cm = collection('Monsters'); ce = collection('Environment')
    for f in (rastejante, bruxa, carnical, mariposa, sentinela_vidro, aranha_obsidiana, espectro, soberano):
        f(cm)
    for f in (junco, toco, cogumelo, cristal, pilar, parede):
        f(ce)
    for i, n in enumerate(W2_ITEMS):
        bpy.data.objects[n].location = (40 + (i % 7) * 3.0, 10 + (i // 7) * 4, 0)
    return list(W2_ITEMS)
