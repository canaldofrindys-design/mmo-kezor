# Kezor — corpo de cada classe (feminino, forte, esguio) a partir do corpo v4 salvo em Art/data, com rosto, cabelo
# e pele próprios, e rig no Blender: pesos automáticos (calor) no corpo nu, transferidos para a roupa e para cada
# peça de armadura, gravados nos canais UV2..UV5 (osso, peso) que o SkinRig do Unity lê.
# Requer o namespace KZ com kezor_hero4 (assemble, clothes, eyes, hair, paint_all, export_hero4) e kezor_anim
# (build_armature, BONES) já executados; kezor_armor6 para a classe Shell (cabelo longo).
import bpy, bmesh, json, math, os
from mathutils import Vector, Matrix

DATA = r'C:\MuProtoUnity\Art\data'
KZG = bpy.app.driver_namespace['KZ']

# ---------------------------------------------------------------- classes
CLASSES = {
    'GuardiaoRunico': dict(body='masc', hair='short', skin='#d9a77f', hairc='#3a2416', tunic='#2c4a7c', tunic2='#22385e', pants='#3a2b22', iris='#5a3a22'),
    'ArcanistaDoVeu': dict(body='fem', hair='long', skin='#ecc8ac', hairc='#d2d4e2', tunic='#2c3f8a', tunic2='#1d2a60', pants='#1d1f33', iris='#3a6ab0', lips='#c0707a'),
    'LaminaArcana': dict(body='esguio', hair='ponytail', skin='#c99a76', hairc='#1a1414', tunic='#7a1f2b', tunic2='#4a1219', pants='#241818', iris='#7a2a6a'),
    'ComandanteEstandarte': dict(body='forte', hair='slick', beard=True, skin='#d6a07a', hairc='#c9a24a', tunic='#4a2a6a', tunic2='#2e1a44', pants='#1d1a24', iris='#4a5a7a'),
    'InvocadoraEcos': dict(body='fem', hair='long', skin='#f0d2bc', hairc='#b48ac9', tunic='#9a88c0', tunic2='#6a5a90', pants='#3b2e52', iris='#8a4ac0', lips='#c8707e'),
    'PunhoBrasa': dict(body='forte', hair='mohawk', skin='#b07a55', hairc='#5a1a12', tunic='#a3431c', tunic2='#6a2a10', pants='#2a1d15', iris='#a04a10'),
    'PatrulheiroSelvagem': dict(body='esguio', hair='shaggy', skin='#c8956a', hairc='#7a3d1c', tunic='#3a6b35', tunic2='#244a22', pants='#4a3523', iris='#3a6a2a'),
}

# índices dos elementos do metaball do v4 (lado esquerdo; o direito é +21 a partir de 6)
TORSO = dict(chest=0, upper=1, neckb=2, belly=3, waist=4, pelvis=5, neck=48)
LEFT = dict(pec=6, lats=7, butt=8, clav=9, delt=10, uarm=11, bic=12, tri=13, farm=14, fmus=15, hand=16, fing=17, thumb=18,
            thigh=19, quad=20, ham=21, knee=22, shin=23, calf=24, foot=25, heel=26)

# perfis: raio multiplicado; 'mv' desloca o centro (lado esquerdo, espelhado), 'sz' multiplica size_x/y/z
PROFILES = {
    'masc': {},
    'forte': dict(trap=1.15, chest=1.06, upper=1.07, waist=1.04, pec=1.12, lats=1.12, clav=1.12, delt=1.16, uarm=1.12, bic=1.16, tri=1.14,
                  farm=1.1, fmus=1.14, hand=1.05, thigh=1.05, quad=1.07, ham=1.06, calf=1.08, neck=1.2, neckb=1.06),
    'esguio': dict(chest=0.97, upper=0.97, waist=0.94, belly=0.95, pec=0.9, lats=0.9, delt=0.92, uarm=0.93, bic=0.9, tri=0.9,
                   farm=0.94, fmus=0.9, quad=0.95, ham=0.95, calf=0.95, neck=0.94),
    'fem': dict(trap=0.85, chest=0.95, upper=0.9, neckb=0.88, belly=0.92, waist=0.86, pelvis=1.06, pec=0.75, lats=0.88, butt=1.12, clav=0.88,
                delt=0.9, uarm=0.9, bic=0.86, tri=0.88, farm=0.9, fmus=0.86, hand=0.88, fing=0.9, thumb=0.9, thigh=1.02, ham=1.03,
                knee=0.94, shin=0.92, calf=0.93, foot=0.88, heel=0.9, neck=0.82,
                mv=dict(butt=(0.006, 0.004, 0.0), thigh=(0.004, 0, 0), quad=(0.004, 0, 0), ham=(0.004, 0, 0)),
                add=[('ELLIPSOID', (0.064, -0.07, 1.292), 0.105, (0.62, 0.55, 0.6))]),
}


# correções de anatomia do corpo v4 para todas as classes: caixa torácica em V (sem barril embaixo da axila),
# dorsais mais altos, cintura e barriga mais secas, braços mais cheios
BASEFIX = dict(chest=dict(sx=0.86, sy=0.97), lats=dict(co=(0.104, 0.036, 1.285), sx=0.52, sz=0.85), waist=dict(r=0.96),
               belly=dict(r=0.94), delt=dict(r=1.05), bic=dict(r=1.07), tri=dict(r=1.07), fmus=dict(r=1.05), upper=dict(sx=1.03))


def load_body(profile):
    """Recria os elementos do KZ_CorpoMB a partir do corpo v4 salvo, com o perfil da classe."""
    D = json.load(open(os.path.join(DATA, 'corpo_v4_metaballs.json')))
    B = bpy.data.objects['KZ_CorpoMB']
    mb = B.data
    P = PROFILES[profile]
    mul = {}
    mv = {}
    sz = {}
    for k, i in TORSO.items():
        mul[i] = P.get(k, 1.0)
    for k, i in LEFT.items():
        for j, sg in ((i, 1), (i + 21, -1)):
            mul[j] = P.get(k, 1.0)
            if k in P.get('mv', {}):
                d = P['mv'][k]
                mv[j] = (d[0] * sg, d[1], d[2])
            if k in P.get('sz', {}):
                sz[j] = P['sz'][k]
    FIX = {}
    for k, f in BASEFIX.items():
        if k in TORSO:
            FIX[TORSO[k]] = f
        else:
            FIX[LEFT[k]] = f
            FIX[LEFT[k] + 21] = f
    while len(mb.elements):
        mb.elements.remove(mb.elements[0])
    for i, e in enumerate(D['elements']):
        n = mb.elements.new(type=e['type'])
        c = e['co']
        d = mv.get(i, (0, 0, 0))
        n.co = (c[0] + d[0], c[1] + d[1], c[2] + d[2])
        n.radius = e['r'] * mul.get(i, 1.0)
        s = sz.get(i, (1, 1, 1))
        n.size_x, n.size_y, n.size_z = e['s'][0] * s[0], e['s'][1] * s[1], e['s'][2] * s[2]
        fx = FIX.get(i)
        if fx:
            if 'co' in fx:
                c0 = fx['co']
                n.co = (c0[0] * (1 if c[0] >= 0 else -1) + d[0], c0[1] + d[1], c0[2] + d[2])
            n.radius *= fx.get('r', 1.0)
            n.size_x *= fx.get('sx', 1.0)
            n.size_y *= fx.get('sy', 1.0)
            n.size_z *= fx.get('sz', 1.0)
        n.rotation = e['rot']
        n.stiffness = e['k']
        n.use_negative = e['neg']
    for t, c, r, sz3 in [('ELLIPSOID', (0.088, 0.026, 1.468), 0.13 * P.get('trap', 1.0), (0.9, 0.5, 0.5))] + P.get('add', []):                 # elementos extras (espelhados): busto
        for sg in (1, -1):
            n = mb.elements.new(type=t)
            n.co = (c[0] * sg, c[1], c[2])
            n.radius = r
            n.size_x, n.size_y, n.size_z = sz3
            n.stiffness = 2.0
    mb.threshold = D['threshold']
    return B


def _sm(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def load_head(profile):
    """Volta a cabeça e as orelhas ao v4 e esculpe o rosto do perfil (mandíbula, queixo, sobrancelha, nariz)."""
    D = json.load(open(os.path.join(DATA, 'cabeca_v4.json')))
    for n, d in D.items():
        o = bpy.data.objects[n]
        Mi = o.matrix_world.inverted()
        for v, w in zip(o.data.vertices, d['v']):
            p = Vector(w)
            x, y, z = p
            low = _sm(1.675, 1.585, z)
            if n == 'KZ_Cabeca':
                if profile == 'fem':
                    x *= 1 - 0.12 * low
                    y += 0.006 * low * _sm(-0.02, -0.08, y)                         # queixo menor
                    if 1.70 < z < 1.74 and y < -0.05:
                        y += 0.0035                                                 # sobrancelha suave
                    if abs(x) < 0.016 and y < -0.085 and 1.625 < z < 1.69:
                        y += 0.004                                                  # nariz menor
                    if abs(z - 1.668) < 0.02 and 0.03 < abs(x) < 0.06 and y < -0.05:
                        y -= 0.002                                                  # maçãs do rosto
                elif profile == 'forte':
                    x *= 1 + 0.085 * low
                    y -= 0.005 * low * _sm(-0.02, -0.08, y) * _sm(0.04, 0.0, abs(x))  # queixo quadrado
                    if 1.70 < z < 1.74 and y < -0.05:
                        y -= 0.0035                                                 # testa marcada
                elif profile == 'esguio':
                    x *= 1 - 0.045 * low
                    if abs(z - 1.645) < 0.02 and 0.03 < abs(x) < 0.07 and y < -0.02:
                        x *= 0.985                                                  # rosto mais fino
            o.data.vertices[v.index].co = Mi @ Vector((x, y, z))
        o.data.update()


# ---------------------------------------------------------------- cabelo por estilo
def _hair_shell_obj(shells):
    objs = []
    for s in shells:
        objs.append(s.build())
    return objs


def hair_cls(body, style):
    _region_copy = KZG['_region_copy']
    _smooth = KZG['_smooth']
    gain = {'short': 1.0, 'long': 1.0, 'ponytail': 0.8, 'slick': 0.55, 'shaggy': 1.9, 'mohawk': 1.0}[style]

    def keep(p):
        if p.z < 1.62:
            return False
        if style == 'mohawk':
            return abs(p.x) < 0.026 + 0.004 * _smooth(1.79, 1.7, p.z) and p.y > -0.075 and p.z > 1.66
        front = p.y < -0.02
        if front:
            line = 1.735 if style != 'shaggy' else 1.715
            return p.z > line + 0.02 * (abs(p.x) / 0.08) ** 2
        if abs(p.x) > 0.06 and p.z < 1.69 and p.y < 0.03:
            if style in ('long', 'shaggy'):
                return p.z > 1.64 and p.y > -0.045
            return p.z > 1.665 and abs(p.x) < 0.085 and p.y > -0.04
        return True

    def extra(p):
        ang = math.atan2(p.x, -p.y)
        strand = 0.0035 * math.sin(ang * 18 + p.z * 40) + 0.002 * math.sin(ang * 41 - p.z * 90)
        top = 0.008 * _smooth(1.70, 1.80, p.z)
        if style == 'mohawk':
            return 0.006 + 0.03 * _smooth(1.70, 1.80, p.z) * (1 - abs(p.x) / 0.03) + 0.004 * math.sin(p.y * 160)
        if style == 'shaggy':
            return gain * strand + top + 0.006 * _smooth(1.66, 1.62, p.z) * abs(math.sin(ang * 9))
        return gain * strand + top
    hr = _region_copy(body, 'KZ_Cabelo', keep, 0.009 if style != 'slick' else 0.0065, extra, solid=0.008)
    parts = [hr]
    Shell = KZG['Shell']
    C = Vector((0.0, 0.016, 1.655))
    if style == 'long':
        def drape(u, v):
            t = 1 - v
            half = 110 - 25 * t ** 1.3                       # embaixo fica só atrás dos ombros
            az = (180 + half * (2 * u - 1)) * math.pi / 180
            tips = abs(math.sin(math.pi * 9 * u)) ** 0.5     # pontas das mechas na barra
            z = 1.70 - (0.30 + 0.04 * tips) * t
            k = 1.0 + 0.22 * t ** 1.4 - 0.06 * math.sin(math.pi * min(1.0, t * 1.4))
            rx, ry = 0.097 * k, 0.12 * k
            wav = 0.005 * math.sin(u * 40 + t * 3) * (0.3 + t)
            return (C.x + (rx + wav) * math.sin(az), C.y - (ry + wav) * math.cos(az) + 0.02 * t, z)
        parts.append(Shell('KZ_CabeloLongo', drape, 72, 18, thick=0.012, kind='hair').build())
    if style == 'ponytail':
        def tail(u, v):
            a = u * 2 * math.pi
            t = v
            base = Vector((0.0, 0.135 + 0.06 * t, 1.70 - 0.25 * t * t - 0.02 * t))
            r = 0.026 * (1 - 0.65 * t) + 0.004 * math.sin(t * math.pi)
            return (base.x + r * math.cos(a), base.y + r * 0.8 * math.sin(a), base.z + 0.002 * math.sin(a * 3))
        parts.append(Shell('KZ_RaboCavalo', tail, 14, 12, thick=0.0, kind='hair').build())
    return parts


def beard(body):
    _region_copy = KZG['_region_copy']

    def keep(p):
        if not (1.57 < p.z < 1.652 and p.y < 0.01):
            return False
        if abs(p.z - 1.611) < 0.009 and abs(p.x) < 0.03 and p.y < -0.06:
            return False                                              # boca livre
        if p.z > 1.632 and abs(p.x) < 0.022 and p.y < -0.07:
            return False                                              # buço curto
        return abs(p.x) < 0.09
    return _region_copy(body, 'KZ_Barba', keep, 0.005, lambda p: 0.002 * math.sin(p.x * 300 + p.z * 200), solid=0.004)


# ---------------------------------------------------------------- montagem de uma classe
def build_class_body(cls):
    cfg = CLASSES[cls]
    load_body(cfg['body'])
    load_head(cfg['body'])
    body = KZG['assemble'](None)
    cl = KZG['clothes'](body)
    hp = hair_cls(body, cfg['hair'])
    if cfg.get('beard'):
        hp.append(beard(body))
    hair = KZG['_join'](hp, 'KZ_Cabelo') if len(hp) > 1 else hp[0]
    parts = dict(body=body, eyes=KZG['eyes'](), hair=hair, **cl)
    for k, n in KZG['BUDGET'].items():
        KZG['decimate'](parts[k], n + (1500 if k == 'hair' and cfg['hair'] == 'long' else 0))
    pal = KZG['PALETTE']
    old = dict(pal)
    pal.update(skin=cfg['skin'], hair=cfg['hairc'], brow=cfg['hairc'] if cfg['hairc'] not in ('#d2d4e2', '#c9a24a') else '#6a5a50',
               tunic=cfg['tunic'], tunic2=cfg['tunic2'], pants=cfg['pants'], iris=cfg['iris'], lips=cfg.get('lips', old['lips']))
    try:
        KZG['paint_all'](parts)
    finally:
        pal.clear()
        pal.update(old)
    return parts


# ---------------------------------------------------------------- rig: pesos do Blender
BONE_ORDER = ['Body', 'Chest', 'Head', 'ArmL', 'ForeArmL', 'HandL', 'ArmR', 'ForeArmR', 'HandR',
              'LegL', 'ShinL', 'FootL', 'LegR', 'ShinR', 'FootR']                       # = KezorAnim.BoneNames


def rig_body(body):
    """Pesos automáticos (calor) do Blender no corpo nu, com o esqueleto do jogo; suaviza e limita a 4 ossos."""
    arm = bpy.data.objects.get('KZ_Esqueleto') or KZG['build_armature']()
    for g in list(body.vertex_groups):
        body.vertex_groups.remove(g)
    for m in list(body.modifiers):
        if m.type == 'ARMATURE':
            body.modifiers.remove(m)
    body.parent = None
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    body.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    bpy.context.view_layer.objects.active = body
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    body.select_set(True)
    bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
    try:
        bpy.ops.object.vertex_group_smooth(group_select_mode='ALL', factor=0.5, repeat=2, expand=0.0)
        bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
        bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    finally:
        bpy.ops.object.mode_set(mode='OBJECT')
    for m in list(body.modifiers):
        if m.type == 'ARMATURE':
            body.modifiers.remove(m)
    mw = body.matrix_world.copy()
    body.parent = None
    body.matrix_world = mw
    missing = [v.index for v in body.data.vertices if not v.groups]
    return arm, len(missing)


ALLOWED = {
    'Body': None,
    'Armadura': ('Body', 'Chest'),
    'Elmo': ('Head',), 'Head': ('Head',),
    'LuvaL': ('ArmL', 'ForeArmL', 'HandL'), 'LuvaR': ('ArmR', 'ForeArmR', 'HandR'),
    'CalcaL': ('LegL', 'ShinL', 'FootL'), 'CalcaR': ('LegR', 'ShinR', 'FootR'),
    'BotaL': ('LegL', 'ShinL', 'FootL'), 'BotaR': ('LegR', 'ShinR', 'FootR'),
}


def transfer_weights(src, dst, allowed=None):
    """Copia os pesos do corpo para a malha (roupa/armadura) pela face mais próxima e grava em UV2..UV5."""
    for g in list(dst.vertex_groups):
        dst.vertex_groups.remove(g)
    for g in src.vertex_groups:
        dst.vertex_groups.new(name=g.name)
    m = dst.modifiers.new('KZ_Pesos', 'DATA_TRANSFER')
    m.object = src
    m.use_vert_data = True
    m.data_types_verts = {'VGROUP_WEIGHTS'}
    m.vert_mapping = 'POLYINTERP_NEAREST'
    m.layers_vgroup_select_src = 'ALL'
    m.layers_vgroup_select_dst = 'NAME'
    KZG['_apply'](dst, 'KZ_Pesos')
    encode_uv(dst, allowed)


def encode_uv(o, allowed=None):
    me = o.data
    gi = {g.index: g.name for g in o.vertex_groups}
    per_v = []
    for v in me.vertices:
        w = [(gi[g.group], g.weight) for g in v.groups if g.weight > 1e-4 and gi[g.group] in BONE_ORDER]
        if allowed:
            w = [(b, x) for b, x in w if b in allowed]
            if not w:
                w = [(allowed[0], 1.0)]
        w.sort(key=lambda t: -t[1])
        w = w[:4]
        tot = sum(x for _, x in w) or 1.0
        w = [(BONE_ORDER.index(b), x / tot) for b, x in w]
        while len(w) < 4:
            w.append((0, 0.0))
        per_v.append(w)
    names = ['KZ_W0', 'KZ_W1', 'KZ_W2', 'KZ_W3']
    while len(me.uv_layers) < 2:
        me.uv_layers.new(name='UVMap' if len(me.uv_layers) == 0 else 'MatId')
    idx = [0] * len(me.loops)
    me.loops.foreach_get('vertex_index', idx)
    for k, nm in enumerate(names):
        lay = me.uv_layers.get(nm) or me.uv_layers.new(name=nm)
        flat = []
        for i in idx:
            b, x = per_v[i][k]
            flat += (float(b), x)
        lay.data.foreach_set('uv', flat)


# ---------------------------------------------------------------- exportação da base da classe com os pesos
def export_class(parts, cls):
    """Usa export_hero4, mas o 'Body' recebe os pesos do Blender (transferidos do corpo nu)."""
    body = parts['body']
    old_join = KZG['_join']

    def join_w(objs, name):
        o = old_join(objs, name)
        if name == 'Body':
            transfer_weights(body, o)
        return o
    KZG['_join'] = join_w
    try:
        return KZG['export_hero4'](parts, name=cls + 'Base')
    finally:
        KZG['_join'] = old_join


def weighted_copy_factory(body, part_of):
    """Para export_named (sets): cada cópia de peça recebe os pesos do corpo, limitados aos ossos da peça."""
    oldcopy = KZG['_copy']

    def wcopy(o, matid):
        c = oldcopy(o, matid)
        transfer_weights(body, c, ALLOWED.get(part_of(o.name)))
        return c
    return wcopy
