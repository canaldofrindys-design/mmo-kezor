# Kezor — equipamento visível, asas, montarias e mascotes (etapa 5).
# Requer kezor_lib.py, kezor_humans.py e kezor_monsters.py executados antes, no mesmo namespace.
#
# Convenções (as mesmas das classes, ver PROGRESSO.md):
# - Frente do modelo = -Y no Blender (vira +Z no Unity), esquerda do personagem = +X, origem nos pés.
# - Peça de equipamento: raiz (kezor_root) -> uma malha "Piece" cujo pivô é a articulação onde ela encaixa
#   (Head, ArmL, ArmR, LegL, LegR ou Body). No Unity basta pendurar "Piece" na parte com posição local 0.
# - Peças de braço/perna têm versão L e R (não espelhamos com escala negativa no Unity).
# - Asas: "Wings" (pivô no corpo) -> WingL / WingR (pivô na escápula) para bater.
# - Montarias: Body -> Head, Leg0..Leg3 (0 = dianteira esq., 1 = dianteira dir., 2 = traseira esq., 3 = traseira dir.), Tail.
# - Mascotes: Body -> WingL/WingR (diabrete) ou Orb0..Orb2 (lume).

EQUIP_ITEMS = []   # nomes dos modelos criados (para exportar só estes)

PV_BODY = (0, 0, 0)
PV_HEAD = PIV['Head']
PV_ARM = {'L': PIV['ArmL'], 'R': PIV['ArmR']}
PV_LEG = {'L': PIV['LegL'], 'R': PIV['LegR']}
SIDE = {'L': 1, 'R': -1}

EMB = mat('EmberGlow', '#ff8a2a', emit=4.0)
EMB2 = mat('EmberCore', '#ffd36a', emit=4.0)
RUNE = mat('RuneGlow', '#6fd0ff', emit=4.0)
VOID = mat('VoidGlow', '#b07bff', emit=4.0)
BASALT = mat('BasaltDark', '#2f2d2c')
BASALT2 = mat('BasaltMid', '#45413d')
HAIR_D = mat('HairDark', '#2a1d14')
HAIR_B = mat('HairBrown', '#5b3a22')


def piece(name, pivot, build, cat='Equip', coll=None):
    """Cria um modelo de uma peça só: raiz -> 'Piece' com o pivô dado."""
    coll = coll or collection('Equip')
    wipe(name)
    r = root(name, coll, cat)
    b = B()
    build(b)
    b.obj(name + '.Piece', coll, r, pivot, 'Piece')
    EQUIP_ITEMS.append(name)
    return r


def place_row(names, y, dx=1.2, x0=0.0):
    for i, n in enumerate(names):
        o = bpy.data.objects.get(n)
        if o:
            o.location = (x0 + i * dx, y, 0)


# ================================================================ corpos base (sem equipamento)
def base_body(name, shirt, pants, boots, hair, extra=None):
    P = hparts()
    b = P['Body']
    std_body(b, shirt, LEATHER, pants)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        std_arm(P[part], 0.31 * side, shirt, SKIN, SKIN)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        std_leg(P[part], 0.12 * side, pants, pants, boots)
    std_head(P['Head'], SKIN, hair=hair)
    if extra:
        extra(P)
    coll = collection('Classes')
    r = finish(name, 'Classes', coll, P)
    EQUIP_ITEMS.append(name)
    return r


def bases():
    def gua_extra(P):
        red = mat('WardenRed', '#9b2c2c')
        P['Body'].box((0, 0.13, 0.95), (0.40, 0.03, 0.80), red, top=(0.85, 1))          # capa curta
    base_body('GuardiaoRunicoBase', mat('WardenBlue', '#3d5a80', rough=0.5, metal=0.4), mat('WardenPants', '#2b2f3a'), LEATHER, HAIR_B, gua_extra)

    def arc_extra(P):
        P['Body'].box((0, -0.122, 1.07), (0.07, 0.012, 0.05), GOLD)
    base_body('ArcanistaDoVeuBase', mat('ArcPurple', '#4b2d6b'), mat('ArcDark', '#2a1d3a'), LEATHER, HAIR_D, arc_extra)

    def pat_extra(P):
        b = P['Body']
        q0, q1 = Vector((0.12, 0.17, 0.85)), Vector((-0.08, 0.17, 1.42))
        b.seg(q0, q1, 0.13, LEATHER_L)                                                       # aljava
        for dx in (-0.03, 0.03):
            b.seg((q1.x + dx, q1.y, q1.z - 0.02), (q1.x + dx - 0.03, q1.y, q1.z + 0.14), 0.012, WOOD)
    base_body('PatrulheiroSelvagemBase', mat('RangerGreen', '#3f6b3a'), mat('RangerPants', '#4a3a28'), LEATHER_L, HAIR_B, pat_extra)


# ================================================================ conjuntos de armadura
SETS = {
    # nome: (principal, secundária, detalhe, brilho)
    'Couro': (mat('GearLeather', '#6b4a30'), mat('GearLeatherD', '#4a3220'), mat('GearBrass', '#b8893a', rough=0.5, metal=0.6), None),
    'Escamas': (mat('GearScale', '#5f7a74', rough=0.45, metal=0.6), mat('GearScaleD', '#3e524e', rough=0.5, metal=0.5), mat('GearSteel', '#a9b3bd', rough=0.35, metal=0.8), None),
    'Veu': (mat('GearVeil', '#3b2a6a'), mat('GearVeilD', '#24183f'), GOLD, VOID),
    'Runica': (mat('GearPlate', '#8d99a6', rough=0.35, metal=0.8), mat('GearPlateD', '#4f5966', rough=0.4, metal=0.7), GOLD, RUNE),
}


# Conjuntos novos reaproveitam a forma de um conjunto base (cores e brilho próprios).
STYLE = {'Trama': 'Escamas', 'Bastiao': 'Runica', 'Ecos': 'Veu', 'Igneo': 'Couro'}


def helm(set_name):
    c1, c2, c3, glow = SETS[set_name]

    def build(b):
        if STYLE.get(set_name, set_name) == 'Couro':
            b.box((0, 0.01, 1.66), (0.26, 0.28, 0.18), c1, top=(0.85, 0.85))
            for s in (-1, 1):
                b.box((s * 0.135, 0.0, 1.55), (0.03, 0.12, 0.14), c2, top=(1, 0.8))         # abas
            b.box((0, -0.005, 1.585), (0.27, 0.29, 0.03), c3)
        elif STYLE.get(set_name, set_name) == 'Escamas':
            b.box((0, 0, 1.61), (0.27, 0.29, 0.28), c1, top=(0.9, 0.92))
            b.box((0, 0, 1.765), (0.22, 0.25, 0.06), c2, top=(0.6, 0.65))
            b.box((0, -0.146, 1.6), (0.19, 0.01, 0.03), DARK)                              # viseira
            b.box((0, -0.15, 1.55), (0.03, 0.012, 0.09), c3)
            b.box((0, 0.0, 1.82), (0.03, 0.22, 0.05), c3)                                  # crista baixa
        elif STYLE.get(set_name, set_name) == 'Veu':
            b.box((0, 0.02, 1.78), (0.29, 0.31, 0.08), c1, top=(0.6, 0.65))
            for s in (-1, 1):
                b.box((s * 0.14, 0.01, 1.60), (0.035, 0.31, 0.33), c1)
                b.box((s * 0.14, -0.145, 1.60), (0.04, 0.015, 0.33), c3)
            b.box((0, 0.155, 1.60), (0.29, 0.04, 0.34), c1)
            b.seg((0, 0.10, 1.77), (0, 0.32, 1.58), 0.13, c2, w2=0.01)
            b.gem((0, -0.15, 1.76), 0.03, 0.07, glow)
        else:  # Runica
            b.box((0, 0, 1.61), (0.28, 0.30, 0.29), c1, top=(0.92, 0.95))
            b.box((0, 0, 1.78), (0.25, 0.27, 0.06), c1, top=(0.6, 0.65))
            b.box((0, -0.151, 1.62), (0.19, 0.01, 0.026), glow)                             # viseira rúnica
            b.box((0, -0.151, 1.565), (0.026, 0.01, 0.08), glow)
            b.box((0, 0, 1.47), (0.29, 0.31, 0.04), c3)
            for s in (-1, 1):                                                             # chifres de placa
                b.seg((s * 0.13, 0.02, 1.72), (s * 0.24, 0.06, 1.88), 0.05, c2, w2=0.015)
            b.box((0, 0.0, 1.85), (0.04, 0.30, 0.08), c3, top=(1, 0.7))
    return piece(f'{set_name}_Elmo', PV_HEAD, build)


def armor(set_name):
    c1, c2, c3, glow = SETS[set_name]

    def build(b):
        if STYLE.get(set_name, set_name) == 'Veu':                                                          # túnica longa
            b.lathe((0, 0, 0.0), [(0.38, 0.03), (0.37, 0.12), (0.31, 0.55), (0.22, 1.02)], 8, [c3, c1, c1], sy=0.82)
            b.box((0, 0, 1.23), (0.44, 0.26, 0.34), c1, top=(1.22, 1.06))
            b.lathe((0, 0, 1.28), [(0.28, 0), (0.21, 0.1), (0.13, 0.16)], 8, c2, sy=0.76)
            b.seg((0, -0.30, 0.12), (0, -0.18, 1.0), 0.08, c3, d=0.012)
            b.gem((0, -0.14, 1.3), 0.04, 0.09, glow)
            return
        w = 1.0 if STYLE.get(set_name, set_name) == 'Couro' else 1.04
        b.box((0, 0, 1.23), (0.44 * w, 0.27, 0.34), c1, top=(1.22, 1.06))               # peitoral
        b.box((0, 0, 1.075), (0.43 * w, 0.265, 0.07), c3)                              # cinto
        b.box((0, 0, 0.97), (0.41 * w, 0.26, 0.14), c2, top=(0.95, 0.95))              # faldas
        for s in (-1, 1):                                                             # ombreiras
            b.box((s * 0.33, 0, 1.40), (0.21 * w, 0.24, 0.12), c1, top=(0.6, 0.75))
            b.box((s * 0.33, 0, 1.335), (0.215 * w, 0.245, 0.03), c3)
        if STYLE.get(set_name, set_name) == 'Couro':
            b.seg((0.2, -0.14, 1.38), (-0.2, -0.14, 1.1), 0.05, c2, d=0.015)
        if STYLE.get(set_name, set_name) == 'Escamas':
            for i in range(3):
                b.box((0, -0.138, 1.33 - i * 0.08), (0.36 - i * 0.04, 0.01, 0.05), c2, top=(0.85, 1))
        if STYLE.get(set_name, set_name) == 'Runica':
            b.box((0, -0.14, 1.25), (0.07, 0.012, 0.24), glow)                          # runa central
            for s in (-1, 1):
                b.box((s * 0.12, -0.137, 1.28), (0.035, 0.01, 0.12), glow)
                b.seg((s * 0.30, 0.0, 1.47), (s * 0.40, 0.04, 1.58), 0.06, c2, w2=0.02)  # espigões
    return piece(f'{set_name}_Armadura', PV_BODY, build)


def pants(set_name, side):
    c1, c2, c3, glow = SETS[set_name]
    x = 0.12 * SIDE[side]

    def build(b):
        b.box((x, 0, 0.72), (0.19, 0.21, 0.42), c1, top=(1.1, 1.06))
        b.box((x, 0, 0.34), (0.16, 0.18, 0.36), c2, top=(1.15, 1.1))
        b.box((x, -0.10, 0.52), (0.13, 0.04, 0.11), c3, top=(0.8, 1))                   # joelheira
        if glow is not None:
            b.box((x, -0.101, 0.72), (0.03, 0.01, 0.16), glow)
    return piece(f'{set_name}_Calca{side}', PV_LEG[side], build)


def gloves(set_name, side):
    c1, c2, c3, glow = SETS[set_name]
    x = 0.31 * SIDE[side]

    def build(b):
        b.box((x, 0, 0.90), (0.135, 0.145, 0.22), c1, top=(1.15, 1.1))                  # braçal
        b.box((x, -0.005, 0.735), (0.115, 0.125, 0.12), c2, top=(0.9, 0.9))             # mão
        b.box((x, 0, 1.01), (0.145, 0.155, 0.04), c3)
        if glow is not None:
            b.box((x, -0.075, 0.91), (0.03, 0.01, 0.12), glow)
    return piece(f'{set_name}_Luva{side}', PV_ARM[side], build)


def boots(set_name, side):
    c1, c2, c3, glow = SETS[set_name]
    x = 0.12 * SIDE[side]

    def build(b):
        if STYLE.get(set_name, set_name) == 'Veu':                                                         # sandálias
            b.box((x, -0.03, 0.03), (0.17, 0.29, 0.05), c2)
            for z in (0.08, 0.16):
                b.box((x, 0, z), (0.165, 0.185, 0.03), c3)
            return
        b.box((x, -0.03, 0.075), (0.175, 0.29, 0.16), c1, top=(0.95, 0.78), shift=(0, 0.03))
        b.box((x, 0, 0.21), (0.165, 0.185, 0.14), c2, top=(1.1, 1.08))
        b.box((x, 0, 0.29), (0.175, 0.195, 0.04), c3)
    return piece(f'{set_name}_Bota{side}', PV_LEG[side], build)


def armor_sets():
    for s in ('Couro', 'Escamas', 'Runica'):
        helm(s); armor(s)
    for s in ('Couro', 'Escamas', 'Runica'):
        for side in ('L', 'R'):
            pants(s, side); gloves(s, side); boots(s, side)
    helm('Veu'); armor('Veu')
    for side in ('L', 'R'):
        pants('Veu', side); gloves('Veu', side); boots('Veu', side)


# ================================================================ armas e escudos
HAND_R = (-0.31, -0.03, 0.735)
HAND_L = (0.31, -0.03, 0.735)


def sword_model(name, length, blade, guard, grip, glow=None, wide=1.0):
    def build(b):
        b.tf(loc=HAND_R, rot=(70, 0, 0))
        b.box((0, 0, 0), (0.035, 0.035, 0.17), grip)
        b.box((0, 0, -0.1), (0.05, 0.05, 0.05), guard)
        b.box((0, 0, 0.1), (0.24 * wide, 0.045, 0.04), guard)
        b.box((0, 0, 0.12 + length / 2), (0.07 * wide, 0.018, length), blade)
        b.box((0, 0, 0.17 + length), (0.07 * wide, 0.018, 0.1), blade, top=(0.05, 0.4))
        b.box((0, 0, 0.12 + length * 0.45), (0.016, 0.022, length * 0.75), glow or STEEL_D)
        if glow is not None:
            for s in (-1, 1):
                b.seg((s * 0.12 * wide, 0, 0.1), (s * 0.2 * wide, 0, 0.22), 0.03, glow, w2=0.01)
        b.reset()
    return piece(name, PV_ARM['R'], build)


def axe_model(name, head, haft, glow=None):
    def build(b):
        b.tf(loc=HAND_R, rot=(70, 0, 0))
        b.box((0, 0, 0.25), (0.04, 0.04, 0.8), haft)
        b.prism([(0.02, 0.62), (0.26, 0.72), (0.3, 0.5), (0.26, 0.36), (0.02, 0.46)], -0.02, 0.02, head)
        b.prism([(-0.02, 0.6), (-0.12, 0.64), (-0.12, 0.5), (-0.02, 0.52)], -0.018, 0.018, head)
        if glow is not None:
            b.prism([(0.24, 0.7), (0.28, 0.5), (0.24, 0.38), (0.27, 0.5)], -0.022, 0.022, glow)
        b.reset()
    return piece(name, PV_ARM['R'], build)


def staff_model(name, wood, metal, gem_m, length=1.66, crown=3, big=False):
    cx, cy = -0.31, -0.07

    def build(b):
        b.cyl((cx, cy, 0.05), 0.03, 0.022, 0.06, 6, metal)
        b.cyl((cx, cy, 0.08), 0.022, 0.022, length, 6, wood)
        top = 0.08 + length
        b.cyl((cx, cy, top - 0.02), 0.035, 0.042, 0.06, 6, metal)
        r = 0.11 if big else 0.085
        for i in range(crown):
            an = i * 2 * math.pi / crown
            ca, sa = math.cos(an), math.sin(an)
            p0 = (cx + 0.03 * ca, cy + 0.03 * sa, top + 0.02)
            p1 = (cx + r * ca, cy + r * sa, top + 0.16)
            p2 = (cx + 0.03 * ca, cy + 0.03 * sa, top + 0.3)
            b.seg(p0, p1, 0.022, metal, w2=0.016)
            b.seg(p1, p2, 0.016, metal, w2=0.006)
        b.gem((cx, cy, top + 0.16), 0.08 if big else 0.06, 0.26 if big else 0.2, gem_m)
        if big:
            for z in (0.5, 0.9, 1.3):
                b.cyl((cx, cy, z), 0.03, 0.03, 0.03, 6, gem_m)
    return piece(name, PV_ARM['R'], build)


def bow_model(name, wood, grip, glow=None, size=1.0):
    def build(b):
        hx, hy, hz = HAND_L
        pts = []
        for t in range(-4, 5):
            u = t / 4
            pts.append((hx, hy - 0.17 * size * (1 - u * u), hz + u * 0.62 * size))
        for i in range(len(pts) - 1):
            mid = abs(i - 3.5) < 1
            b.seg(pts[i], pts[i + 1], 0.05 if mid else 0.035, grip if mid else wood, w2=0.05 if mid else 0.03, up=(1, 0, 0))
        b.seg(pts[0], (hx, hy - 0.06, hz - 0.70 * size), 0.03, glow or wood, w2=0.015, up=(1, 0, 0))
        b.seg(pts[-1], (hx, hy - 0.06, hz + 0.70 * size), 0.03, glow or wood, w2=0.015, up=(1, 0, 0))
        b.seg(pts[0], pts[-1], 0.007, glow or STRING, up=(1, 0, 0))
        if glow is not None:
            for k in (2, 6):
                b.gem(pts[k], 0.03, 0.08, glow)
    return piece(name, PV_ARM['L'], build)


def shield_model(name, kind, c1, c2, c3):
    def build(b):
        if kind == 'round':
            b.tf(loc=(0.44, -0.02, 0.95), rot=(90, 0, 60))
            b.lathe((0, 0, -0.03), [(0.27, 0), (0.27, 0.04), (0.2, 0.06)], 10, [c2, c1])
            b.cone((0, 0, 0.06), 0.07, 0.06, 6, c3)
            b.reset()
        else:
            kite_shield(b, (0.44, -0.02, 0.95), c1, c2, c3)
    return piece(name, PV_ARM['L'], build)


def weapons():
    sword_model('Arma_LaminaFerro', 0.62, BLADE, STEEL_D, LEATHER)
    axe_model('Arma_MachadoGuerra', STEEL, WOOD)
    sword_model('Arma_MontanteTempestade', 0.86, mat('StormBlade', '#bfd3e6', rough=0.25, metal=0.85), GOLD, LEATHER, wide=1.2)
    sword_model('Arma_LaminaVortice', 0.82, mat('VortexBlade', '#3c3f5c', rough=0.3, metal=0.8), GOLD, LEATHER, glow=VOID, wide=1.1)
    sword_model('Arma_FendenteRubro', 0.95, mat('CrimsonBlade', '#7a1d1d', rough=0.3, metal=0.7), mat('CrimsonGuard', '#2a1a1a'), LEATHER, glow=EMB, wide=1.35)
    staff_model('Arma_CajadoAprendiz', WOOD, LEATHER_L, mat('ArcGemSoft', '#9f7bff', emit=2.0))
    staff_model('Arma_VarinhaAmbar', mat('WandWood', '#4a2e1c'), GOLD, mat('AmberGem', '#ffb347', emit=3.0), length=1.2)
    staff_model('Arma_CajadoOraculo', mat('OracleWood', '#2f2a3a'), GOLD, mat('ArcGem', '#9f7bff', emit=4.0), crown=4)
    staff_model('Arma_CetroAuroraVelada', mat('DawnWood', '#e8e2d0'), GOLD, EMB2, length=1.75, crown=5, big=True)
    bow_model('Arma_ArcoCaca', WOOD, LEATHER)
    bow_model('Arma_ArcoComposto', mat('BowHorn', '#4a3a2a'), LEATHER_L, size=1.08)
    bow_model('Arma_ArcoVentoNorte', mat('NorthWood', '#c8d4dc'), mat('NorthGrip', '#3a5a7a'), size=1.15)
    bow_model('Arma_ArcoSussurroBrasa', mat('EmberBowWood', '#2a1d1a'), LEATHER, glow=EMB, size=1.2)
    shield_model('Escudo_Bronze', 'round', mat('Bronze', '#a8743a', rough=0.45, metal=0.7), mat('BronzeD', '#6e4a24', metal=0.5), GOLD)
    shield_model('Escudo_PavesJuramento', 'kite', mat('WardenRed', '#9b2c2c'), STEEL, GOLD)


# ================================================================ asas
def wing_half(b, side, kind):
    """Asa esquerda (side=+1) ou direita (-1) em coordenadas do corpo; o pivô fica na escápula."""
    s = side
    sx, sy, sz = 0.10 * s, 0.17, 1.32
    if kind == 'cinza':                                                              # membrana de morcego
        bone = mat('AshBone', '#3a3533'); memb = mat('AshMembrane', '#6d6560')
        tips = [(0.95, 0.25, 1.85), (1.15, 0.30, 1.35), (0.95, 0.28, 0.85), (0.55, 0.24, 0.65)]
        elbow = (0.55 * s, 0.24, 1.62)
        b.seg((sx, sy, sz), elbow, 0.06, bone, w2=0.045, up=(0, 1, 0))
        prev = None
        for (tx, ty, tz) in tips:
            t = (tx * s, ty, tz)
            b.seg(elbow, t, 0.035, bone, w2=0.012, up=(0, 1, 0))
            if prev is not None:
                b.poly([elbow, prev, t], [(0, 1, 2) if s > 0 else (0, 2, 1)], memb)
                b.poly([elbow, prev, t], [(0, 2, 1) if s > 0 else (0, 1, 2)], memb)
            prev = t
        quad = [(sx, sy, sz), elbow, prev, (0.25 * s, 0.2, 1.0)]
        b.poly(quad, [(0, 1, 2, 3)], memb)   # dupla face: cada chamada cria vértices próprios
        b.poly(quad, [(0, 3, 2, 1)], memb)
    else:                                                                            # penas em camadas
        if kind == 'crepusculo':
            c1, c2, c3 = mat('DuskFeather', '#4b3a7a'), mat('DuskFeatherD', '#2a2148'), GOLD
            n, span = 6, 1.15
        else:
            c1, c2, c3 = mat('FlameFeather', '#c4421a'), mat('FlameFeatherD', '#6e1f10'), EMB
            n, span = 8, 1.45
        arm_end = (0.6 * span * s, 0.25, 1.75 if kind == 'crepusculo' else 1.85)
        b.seg((sx, sy, sz), arm_end, 0.08, c2, w2=0.05, up=(0, 1, 0))
        for i in range(n):
            u = i / (n - 1)
            base = Vector((sx, sy, sz)).lerp(Vector(arm_end), 0.35 + 0.65 * u)
            length = (0.55 + 0.6 * (1 - abs(u - 0.75))) * span * 0.75
            ang = math.radians(-70 + 85 * u)
            tip = base + Vector((math.cos(ang) * 0.35 * s, 0.05, math.sin(ang) * length - length * 0.55))
            w = 0.16 if kind == 'crepusculo' else 0.14
            b.seg(base, tip, w, c1 if i % 2 else c2, w2=w * 0.35, d=0.03, up=(0, 1, 0))
            b.seg(tip.lerp(base, 0.25), tip + (tip - base).normalized() * 0.08, w * 0.4, c3, w2=0.01, d=0.035, up=(0, 1, 0))
        for i in range(n - 1):                                                       # cobertura interna
            u = i / (n - 1)
            p = Vector((sx, sy, sz)).lerp(Vector(arm_end), 0.2 + 0.7 * u)
            b.seg(p, p + Vector((0.12 * s, 0.03, -0.35)), 0.15, c2, w2=0.06, d=0.04, up=(0, 1, 0))


def wings_model(name, kind):
    coll = collection('Wings')
    wipe(name)
    r = root(name, coll, 'Wings')
    hub = B()
    hub.box((0, 0.155, 1.3), (0.16, 0.05, 0.14), mat('WingHub', '#2a2420'))
    if kind == 'brasalume':
        hub.gem((0, 0.19, 1.3), 0.05, 0.12, EMB2)
    hub_o = hub.obj(name + '.Wings', coll, r, (0, 0, 0), 'Wings')
    for side, pname in ((1, 'WingL'), (-1, 'WingR')):
        b = B()
        wing_half(b, side, kind)
        b.obj(name + '.' + pname, coll, hub_o, (0.10 * side, 0.17, 1.32), pname)
    EQUIP_ITEMS.append(name)
    return r


def all_wings():
    wings_model('Asas_Cinza', 'cinza')
    wings_model('Asas_Crepusculo', 'crepusculo')
    wings_model('Asas_Brasalume', 'brasalume')


# ================================================================ montarias
def saddle(b, z, y0, y1, c, c2):
    b.box((0, (y0 + y1) / 2, z), (0.42, abs(y1 - y0), 0.07), c, top=(0.95, 0.95))
    b.box((0, y0 + 0.03, z + 0.06), (0.3, 0.06, 0.1), c2)
    b.box((0, y1 - 0.03, z + 0.06), (0.3, 0.06, 0.12), c2)
    for s in (-1, 1):
        b.box((s * 0.23, (y0 + y1) / 2, z - 0.18), (0.03, 0.06, 0.3), c2)


def mount_wolf(name='Montaria_LoboBrasas'):
    k = 1.35
    fur = mat('EmberWolfFur', '#2b2624'); dark = mat('EmberWolfDark', '#171413')
    sad = mat('SaddleRed', '#7a2a1e')
    body = B(k)
    body.box((0, 0.08, 0.76), (0.42, 0.95, 0.38), fur, top=(0.9, 0.95))
    body.box((0, 0.1, 0.6), (0.3, 0.7, 0.06), dark)
    body.box((0, -0.3, 0.83), (0.52, 0.45, 0.52), dark, top=(0.85, 0.8))
    for i in range(6):                                                               # juba de brasas
        y = -0.42 + i * 0.15
        body.seg((0, y, 1.02 - i * 0.03), (0, y + 0.12, 1.2 - i * 0.04), 0.11, EMB if i % 2 == 0 else EMB2, w2=0.01, d=0.05)
    for s in (-1, 1):
        body.seg((s * 0.2, -0.1, 0.8), (s * 0.21, 0.35, 0.7), 0.02, EMB, d=0.01)       # rachaduras de brasa
    saddle(body, 0.97, -0.12, 0.3, sad, LEATHER)
    head = B(k)
    head.box((0, -0.68, 1.0), (0.33, 0.3, 0.3), fur, top=(0.85, 0.9))
    head.box((0, -0.9, 0.95), (0.17, 0.22, 0.14), dark, top=(0.85, 0.9))
    head.box((0, -1.015, 0.98), (0.06, 0.02, 0.05), DARK)
    for x in (-0.1, 0.1):
        head.box((x, -0.83, 1.06), (0.065, 0.02, 0.035), EMB2)
        head.seg((x, -0.62, 1.12), (x * 1.3, -0.6, 1.3), 0.08, dark, w2=0.01)
    tail = B(k)
    tail.seg((0, 0.53, 0.86), (0, 0.85, 0.75), 0.14, fur, w2=0.1)
    tail.seg((0, 0.85, 0.75), (0, 1.05, 0.95), 0.1, EMB, w2=0.01)
    legs = []
    for i, (x, y) in enumerate(((0.13, -0.28), (-0.13, -0.28), (0.13, 0.42), (-0.13, 0.42))):
        l = B(k)
        l.box((x, y, 0.4), (0.13, 0.15, 0.48), fur, top=(0.8, 0.8))
        l.box((x, y - 0.02, 0.06), (0.14, 0.18, 0.1), dark)
        legs.append(('Leg%d' % i, l, (x, y, 0.62)))   # pivô sem escala: B.obj já multiplica por k
    coll = collection('Mounts')
    r = finish_custom(name, 'Mounts', coll, body, [('Head', head, (0, -0.5, 0.95)), ('Tail', tail, (0, 0.53, 0.86))] + legs)
    EQUIP_ITEMS.append(name)
    return r


def mount_lizard(name='Montaria_LagartoBasalto'):
    sad = mat('SaddleBrown', '#5a3a22')
    body = B()
    body.box((0, 0.1, 0.55), (0.5, 1.2, 0.32), BASALT, top=(0.85, 0.9))
    body.box((0, 0.1, 0.4), (0.42, 1.0, 0.06), BASALT2)
    for i in range(7):                                                               # placas dorsais com lava
        y = -0.4 + i * 0.16
        body.seg((0, y, 0.7), (0, y + 0.06, 0.82 - abs(i - 3) * 0.02), 0.1, BASALT2, w2=0.02, d=0.12)
        if i % 2 == 0:
            body.box((0, y + 0.03, 0.715), (0.03, 0.08, 0.02), EMB)
    for s in (-1, 1):
        body.seg((s * 0.24, -0.4, 0.58), (s * 0.24, 0.55, 0.55), 0.015, EMB, d=0.008)
    saddle(body, 0.74, -0.15, 0.3, sad, LEATHER)
    head = B()
    head.box((0, -0.68, 0.6), (0.3, 0.4, 0.22), BASALT, top=(0.8, 0.85))
    head.box((0, -0.92, 0.56), (0.22, 0.2, 0.14), BASALT2, top=(0.85, 0.8))
    for x in (-0.1, 0.1):
        head.box((x, -0.78, 0.69), (0.05, 0.05, 0.03), EMB2)
        head.seg((x, -0.55, 0.7), (x * 1.6, -0.4, 0.82), 0.05, BASALT2, w2=0.01)
    tail = B()
    tail.seg((0, 0.65, 0.52), (0, 1.15, 0.36), 0.24, BASALT, w2=0.12)
    tail.seg((0, 1.15, 0.36), (0, 1.55, 0.25), 0.12, BASALT2, w2=0.02)
    tail.box((0, 1.1, 0.48), (0.02, 0.2, 0.02), EMB)
    legs = []
    for i, (x, y) in enumerate(((0.26, -0.3), (-0.26, -0.3), (0.26, 0.48), (-0.26, 0.48))):
        l = B()
        s = 1 if x > 0 else -1
        l.seg((x, y, 0.5), (x + s * 0.14, y, 0.3), 0.12, BASALT)
        l.seg((x + s * 0.14, y, 0.3), (x + s * 0.16, y - 0.04, 0.04), 0.1, BASALT2)
        l.box((x + s * 0.16, y - 0.08, 0.03), (0.14, 0.2, 0.05), BASALT2)
        legs.append(('Leg%d' % i, l, (x, y, 0.5)))
    coll = collection('Mounts')
    r = finish_custom(name, 'Mounts', coll, body, [('Head', head, (0, -0.5, 0.58)), ('Tail', tail, (0, 0.65, 0.52))] + legs)
    EQUIP_ITEMS.append(name)
    return r


# ================================================================ mascotes
def pet_imp(name='Mascote_DiabreteCinza'):
    skin = mat('ImpAsh', '#5e5753'); dark = mat('ImpDark', '#2e2a28')
    body = B()
    body.ico((0, 0, 0.32), (0.13, 0.11, 0.15), skin, sub=1)
    body.ico((0, -0.02, 0.52), (0.12, 0.11, 0.11), skin, sub=1)
    for x in (-0.045, 0.045):
        body.box((x, -0.115, 0.54), (0.035, 0.01, 0.022), EMB2)
        body.seg((x * 1.4, -0.02, 0.6), (x * 2.2, 0.02, 0.72), 0.035, dark, w2=0.006)
    body.seg((0, 0.08, 0.26), (0, 0.24, 0.14), 0.03, dark, w2=0.008)                # cauda
    body.cone((0, 0.25, 0.13), 0.025, -0.05, 4, EMB)
    for s in (-1, 1):
        body.seg((s * 0.1, -0.02, 0.36), (s * 0.15, -0.06, 0.22), 0.04, skin, w2=0.025)
        body.seg((s * 0.05, 0, 0.2), (s * 0.06, -0.02, 0.06), 0.045, skin, w2=0.03)
    parts = []
    for side, pname in ((1, 'WingL'), (-1, 'WingR')):
        w = B()
        e = (0.24 * side, 0.1, 0.55)
        w.seg((0.06 * side, 0.08, 0.42), e, 0.025, dark, w2=0.01, up=(0, 1, 0))
        pts = [(0.06 * side, 0.08, 0.42), e, (0.26 * side, 0.12, 0.3), (0.12 * side, 0.1, 0.28)]
        w.poly(pts, [(0, 1, 2, 3)], mat('ImpWing', '#7a4a3a'))
        w.poly(pts, [(0, 3, 2, 1)], mat('ImpWing', '#7a4a3a'))
        parts.append((pname, w, (0.06 * side, 0.08, 0.42)))
    coll = collection('Pets')
    r = finish_custom(name, 'Pets', coll, body, parts)
    EQUIP_ITEMS.append(name)
    return r


def pet_wisp(name='Mascote_LumeGuardiao'):
    core = mat('WispCore', '#bff4ff', emit=4.0)
    shell = mat('WispShell', '#6fd0ff', emit=2.0)
    body = B()
    body.ico((0, 0, 0.45), (0.12, 0.12, 0.16), core, sub=1)
    body.lathe((0, 0, 0.41), [(0.17, 0), (0.17, 0.02)], 8, GOLD)
    body.cone((0, 0, 0.6), 0.05, 0.14, 5, shell)
    body.cone((0, 0, 0.3), 0.05, -0.12, 5, shell)
    parts = []
    for i in range(3):
        o = B()
        o.gem((0.3, 0, 0.45), 0.04, 0.09, shell)
        parts.append(('Orb%d' % i, o, (0.3, 0, 0.45)))
    coll = collection('Pets')
    r = finish_custom(name, 'Pets', coll, body, parts)
    EQUIP_ITEMS.append(name)
    return r


def build_all():
    EQUIP_ITEMS.clear()
    bases()
    armor_sets()
    weapons()
    all_wings()
    mount_wolf(); mount_lizard()
    pet_imp(); pet_wisp()
    # vitrine (só para as prévias do Blender)
    place_row(['GuardiaoRunicoBase', 'ArcanistaDoVeuBase', 'PatrulheiroSelvagemBase'], 40, 1.6)
    place_row([n for n in EQUIP_ITEMS if n.endswith(('_Elmo', '_Armadura'))], 44, 1.2)
    place_row([n for n in EQUIP_ITEMS if n.startswith(('Couro_', 'Escamas_', 'Runica_', 'Veu_')) and not n.endswith(('_Elmo', '_Armadura'))], 47, 0.9)
    place_row([n for n in EQUIP_ITEMS if n.startswith(('Arma_', 'Escudo_'))], 50, 1.0)
    place_row(['Asas_Cinza', 'Asas_Crepusculo', 'Asas_Brasalume'], 54, 3.2)
    place_row(['Montaria_LoboBrasas', 'Montaria_LagartoBasalto', 'Mascote_DiabreteCinza', 'Mascote_LumeGuardiao'], 59, 3.0)
    return list(EQUIP_ITEMS)
