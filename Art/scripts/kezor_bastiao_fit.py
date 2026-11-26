# Kezor — veste a "Judgement Armor" (Sketchfab, CC BY, GarrettMarshallArt) no corpo do Comandante do Estandarte.
# Uso (Blender, cena com CMD_Body/CMD_Head/CMD_ArmL... e a armadura importada):
#   exec(open(r'...\kezor_bastiao_fit.py').read(), G); G['fit_all']()
import bpy, math
from mathutils import Vector, Matrix

SRC = {'Hood': 'Hood_M_HelmetShoulders_0', 'Faceplate': 'Faceplate_M_HelmetShoulders_0',
       'Shoulders': 'Shoulders_M_HelmetShoulders_0', 'Shirt': 'Shirt_M_ShirtPants_0', 'Belt': 'Belt_M_BeltKneesBoots_0',
       'Drapery': 'Drapery_M_Drapery_0', 'Pants': 'Pants_M_ShirtPants_0', 'Knees': 'Knees_M_BeltKneesBoots_0',
       'Boots': 'Boots_M_BeltKneesBoots_0', 'Gloves': 'Gloves_M_Gloves_0'}
ARMS = ('Shirt', 'Gloves')
LEGS = ('Pants', 'Knees', 'Boots')


def sm(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def wcopy(src, name):
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    o = src.copy(); o.data = src.data.copy()
    bpy.context.scene.collection.objects.link(o)
    mw = src.matrix_world.copy()
    o.parent = None
    o.data.transform(mw)
    o.matrix_world = Matrix.Identity(4)
    o.name = name
    o.hide_set(False); o.hide_render = False; o.hide_viewport = False
    return o


def body_pts(names=('CMD_Body',)):
    out = []
    for n in names:
        o = bpy.data.objects[n]; mw = o.matrix_world
        out += [mw @ v.co for v in o.data.vertices]
    return out


def limb_centers(pts, side, kind):
    """Centro (x, y) do braço ou da perna do corpo em cada altura z."""
    sg = 1 if side == 'L' else -1
    res = {}
    for i in range(0, 175):
        z = i * 0.01
        if kind == 'arm':
            s = [p for p in pts if abs(p.z - z) < 0.012 and p.x * sg > 0.205]
        else:
            s = [p for p in pts if abs(p.z - z) < 0.012 and 0.0 < p.x * sg < 0.2]
        if len(s) >= 6:
            xs = [p.x for p in s]; ys = [p.y for p in s]
            res[i] = ((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (max(xs) - min(xs)) / 2, (max(ys) - min(ys)) / 2)
    return res


def fit_all(scale=0.94, arm_angle=None, stretch=None):
    G = globals()
    body = body_pts()
    piv_arm = bpy.data.objects['CMD_ArmL'].matrix_world.translation.copy()      # (0.2295, 0, 1.3855)
    piv_leg = bpy.data.objects['CMD_LegL'].matrix_world.translation.copy()      # (0.1066, 0, 0.92)
    # 1) escala uniforme e alinhamento do tronco em y (a armadura tem o centro mais para trás)
    P = {}
    for k, n in SRC.items():
        P[k] = wcopy(bpy.data.objects[n], 'BA_' + k)
    tor = [p for p in body if 1.15 < p.z < 1.4 and abs(p.x) < 0.17]
    by = (min(p.y for p in tor) + max(p.y for p in tor)) / 2
    sv = [v.co for v in P['Shirt'].data.vertices if 1.15 < v.co.z < 1.4 and abs(v.co.x) < 0.17]
    ay = (min(c.y for c in sv) + max(c.y for c in sv)) / 2 * scale
    M = Matrix.Translation((0, by - ay, 0)) @ Matrix.Scale(scale, 4)
    for o in P.values():
        o.data.transform(M)
    # 2) braços: da pose da armadura para o braço do corpo (pende para baixo); gira em torno do ombro e estica
    gl = [v.co for v in P['Gloves'].data.vertices if v.co.x > 0]
    hand = max(gl, key=lambda c: c.x)
    sl = [v.co.z for v in P['Shirt'].data.vertices if abs(abs(v.co.x) - 0.23) < 0.01]
    shoulder = Vector((0.205, 0, (min(sl) + max(sl)) / 2))
    a0 = math.atan2(shoulder.z - hand.z, hand.x - shoulder.x)                  # ângulo abaixo da horizontal
    bc = limb_centers(body, 'L', 'arm')
    zs = sorted(bc)
    top, bot = bc[zs[-1]], bc[zs[0]]
    a1 = math.atan2(zs[-1] / 100 - zs[0] / 100, bot[0] - top[0]) if arm_angle is None else arm_angle
    L0 = (hand - shoulder).length
    L1 = shoulder.z - zs[0] / 100
    s = (L1 / L0) if stretch is None else stretch
    AW = {}
    for k in ARMS:
        me = P[k].data
        AW[k] = {}
        for v in me.vertices:
            c = v.co
            sg = 1 if c.x >= 0 else -1
            w = sm(0.17, 0.27, abs(c.x))
            if w <= 0:
                continue
            AW[k][v.index] = w
            x, z = abs(c.x) - shoulder.x, c.z - shoulder.z
            ang = -(a1 - a0) * w
            # estica ao longo do braço (coordenada radial a partir do ombro)
            r = math.hypot(x, z); th = math.atan2(z, x)
            r2 = r * (1 + (s - 1) * w)
            th2 = th + ang
            c.x = sg * (shoulder.x + r2 * math.cos(th2))
            c.z = shoulder.z + r2 * math.sin(th2)
        me.update()
    # 3) pernas abertas e braços: em cada altura o centro da peça vai para o centro do membro do corpo
    lc = limb_centers(body, 'L', 'leg')
    conform(P, LEGS, body, 'leg', lambda z: 1 - sm(0.80, 0.95, z), zmax=1.0)
    conform(P, ARMS, body, 'arm', lambda z: 1 - sm(shoulder.z - 0.22, shoulder.z - 0.08, z), zmax=shoulder.z, member=AW)
    G['AW'] = AW
    G['P'] = P; G['a0'] = a0; G['a1'] = a1; G['s'] = s; G['bc'] = bc; G['lc'] = lc
    return {'shoulder': tuple(shoulder), 'a0': math.degrees(a0), 'a1': math.degrees(a1), 's': s, 'L0': L0, 'L1': L1, 'by-ay': by - ay}


def conform(P, keys, body, kind, wfun, zmax, member=None, step=0.02, margin=1.08):
    for side, sg in (('L', 1), ('R', -1)):
        bc = limb_centers(body, side, kind)
        verts = []
        for k in keys:
            mw = member.get(k, {}) if member else None
            for v in P[k].data.vertices:
                if v.co.x * sg > 0 and v.co.z < zmax and (mw is None or mw.get(v.index, 0) > 0):
                    verts.append((v, mw.get(v.index, 1.0) if mw else 1.0))
        bins = {}
        for v, mwv in verts:
            if mwv > 0.6:
                bins.setdefault(int(v.co.z / step), []).append(v.co)
        ac = {}
        for i, cs in bins.items():
            if len(cs) < 8:
                continue
            xs = [c.x for c in cs]; ys = [c.y for c in cs]
            ac[i] = ((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (max(xs) - min(xs)) / 2, (max(ys) - min(ys)) / 2)
        def bcen(z):
            zi = int(round(z * 100))
            for d in range(0, 30):
                for q in (zi - d, zi + d):
                    if q in bc:
                        return bc[q]
            return None
        def acen(z):
            i = z / step - 0.5
            i0 = int(math.floor(i)); k = i - i0
            a, b = ac.get(i0), ac.get(i0 + 1)
            if a is None and b is None:
                for d in range(1, 20):
                    a = ac.get(i0 - d) or ac.get(i0 + 1 + d)
                    if a: return a
                return None
            if a is None: return b
            if b is None: return a
            return tuple(a[j] * (1 - k) + b[j] * k for j in range(4))
        for v, mwv in verts:
            c = v.co
            w = wfun(c.z) * mwv
            if w <= 0:
                continue
            A = acen(c.z); Bc = bcen(c.z)
            if A is None or Bc is None:
                continue
            fx = max(1.0, Bc[2] * margin / max(A[2], 1e-4)); fy = max(1.0, Bc[3] * margin / max(A[3], 1e-4))
            nx = Bc[0] + (c.x - A[0]) * fx
            ny = Bc[1] + (c.y - A[1]) * fy
            c.x = c.x + (nx - c.x) * w
            c.y = c.y + (ny - c.y) * w
    for k in keys:
        P[k].data.update()


# ================================================================ peças do jogo (usa as funções do kezor_sketchfab_set, namespace K)
DECIMATE = {'Drapery': 0.3, 'Boots': 0.35, 'Gloves': 0.4, 'Belt': 0.4, 'Hood': 0.7, 'Shoulders': 1.0}


def decimate(o, ratio):
    if not ratio or ratio >= 1:
        return
    m = o.modifiers.new('dec', 'DECIMATE')
    m.ratio = ratio
    with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
        bpy.ops.object.modifier_apply(modifier='dec')


def keep_faces(src, name, pred):
    """Cópia só com as faces cujo predicado (lista de coordenadas, lista de índices) é verdadeiro."""
    import bmesh
    o = src.copy(); o.data = src.data.copy()
    bpy.context.scene.collection.objects.link(o)
    o.name = name
    bm = bmesh.new(); bm.from_mesh(o.data)
    bm.faces.ensure_lookup_table()
    kill = [f for f in bm.faces if not pred([v.co for v in f.verts], [v.index for v in f.verts])]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(o.data); bm.free()
    return o


def build_parts_bastiao(K):
    P, AW = globals()['P'], globals()['AW']
    for x in [x for x in bpy.data.objects if x.name.startswith('KZS_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    mat = lambda k: P[k].data.materials[0]
    out = {}
    # elmo: capuz + máscara (mesmo material)
    h = K['_dup'](P['Hood'], 'KZS_Hood'); decimate(h, DECIMATE['Hood'])
    f = K['_dup'](P['Faceplate'], 'KZS_Face')
    out['Elmo'] = ([K['_join']([h, f], 'KZS_Elmo')], [mat('Hood')], 'Elmo')
    # tronco: ombreiras, camisa sem as mangas, cinto e tabardo (4 materiais em grade 2x2)
    aw = AW['Shirt']
    sleeve = lambda idx: all(aw.get(i, 0) > 0.5 for i in idx)
    torso = keep_faces(P['Shirt'], 'KZS_Torso', lambda co, idx: not sleeve(idx))
    sh = K['_dup'](P['Shoulders'], 'KZS_Shoulders')
    be = K['_dup'](P['Belt'], 'KZS_Belt'); decimate(be, DECIMATE['Belt'])
    dr = K['_dup'](P['Drapery'], 'KZS_Drapery'); decimate(dr, DECIMATE['Drapery'])
    out['Armadura'] = ([sh, torso, be, dr], [mat('Shoulders'), mat('Shirt'), mat('Belt'), mat('Drapery')], 'Armadura')
    # braços: manopla + manga de cada lado
    gl = K['_dup'](P['Gloves'], 'KZS_GlovesAll'); decimate(gl, DECIMATE['Gloves'])
    for side, sg in (('L', 1), ('R', -1)):
        sl = keep_faces(P['Shirt'], 'KZS_Sleeve' + side, lambda co, idx: sleeve(idx) and sum(c.x for c in co) * sg > 0)
        g = keep_faces(gl, 'KZS_Glove' + side, lambda co, idx: sum(c.x for c in co) * sg > 0)
        out['Luva' + side] = ([g, sl], [mat('Gloves'), mat('Shirt')], 'Luva')
    bpy.data.objects.remove(gl, do_unlink=True)
    # pernas: calça + joelheira = Calça; bota = Bota
    bo = K['_dup'](P['Boots'], 'KZS_BootsAll'); decimate(bo, DECIMATE['Boots'])
    for side, sg in (('L', 1), ('R', -1)):
        pa = keep_faces(P['Pants'], 'KZS_Pants' + side, lambda co, idx: sum(c.x for c in co) * sg > 0)
        kn = keep_faces(P['Knees'], 'KZS_Knee' + side, lambda co, idx: sum(c.x for c in co) * sg > 0)
        b = keep_faces(bo, 'KZS_Bota' + side, lambda co, idx: sum(c.x for c in co) * sg > 0)
        out['Calca' + side] = ([pa, kn], [mat('Pants'), mat('Knees')], 'Calca')
        out['Bota' + side] = ([b], [mat('Boots')], 'Bota')
    bpy.data.objects.remove(bo, do_unlink=True)
    return out


def drapery_mix(co, w):
    """Tabardo/saia: em cima segue o tronco (pesos transferidos); abaixo do quadril passa a seguir as coxas."""
    f = 0.85 * sm(0.98, 0.62, co.z)
    if f <= 0:
        return w
    wl = sm(-0.07, 0.07, co.x)
    base = [(b, x * (1 - f)) for b, x in w if b in ('Body', 'Chest')]
    if not base:
        base = [('Body', 1 - f)]
    return base + [('LegL', f * wl), ('LegR', f * (1 - wl))]


def pivots_cmd():
    g = lambda n: tuple(bpy.data.objects[n].matrix_world.translation)
    return {'Armadura': (0.0, 0.0, 0.0), 'Elmo': g('CMD_Head'), 'LuvaL': g('CMD_ArmL'), 'LuvaR': g('CMD_ArmR'),
            'CalcaL': g('CMD_LegL'), 'CalcaR': g('CMD_LegR'), 'BotaL': g('CMD_LegL'), 'BotaR': g('CMD_LegR')}
