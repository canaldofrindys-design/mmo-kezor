# Kezor — veste a "Fantasy scaled armor" (Sketchfab, nordwar, CC BY) no corpo do Patrulheiro Selvagem (set Escamas).
# Uso (Blender, cena com PB_Body/PB_Head/PB_ArmL... da PatrulheiroSelvagemBase e a armadura importada):
#   E = {}; for f in ('kezor_veu_fit.py', 'kezor_bastiao_fit.py', 'kezor_escamas_fit.py'): exec(open(D + f).read(), E)
#   K = {}; exec(open(D + 'kezor_sketchfab_set.py').read(), K); E['run_escamas'](K)
# Reaproveita do kezor_veu_fit: components, push_out (usa BODY/BVH deste namespace), keep_side;
# do kezor_bastiao_fit: sm, wcopy, limb_centers, conform, decimate, keep_faces.
import bpy, math, bmesh, os
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

BODY, HEAD = 'PB_Body', 'PB_Head'
WB = 'KZB_PesosEscamas'
SET, MODEL = 'Escamas', 'PatrulheiroSelvagemBase'
SRC = {'Helm': 'Helm', 'Torso': 'Torso', 'Legs': 'boots'}       # vazios do Sketchfab; a malha é o filho
ARMS = ('Sleeve', 'Gloves')
LEGS = ('Pants', 'Boots')
DECIMATE = {'Helm': 0.8, 'Armor': 0.85, 'Sleeve': 0.85, 'Gloves': 0.45, 'Pants': 0.8, 'Boots': 0.45}


def src_mesh(empty):
    return [x for x in bpy.data.objects if x.parent and x.parent.name == empty and x.type == 'MESH'][0]


def split_comps(o, prefix, classify):
    co = [v.co.copy() for v in o.data.vertices]
    groups = {}
    for comp in components(o.data):
        pts = [co[i] for i in comp]
        mn = [min(p[k] for p in pts) for k in range(3)]; mx = [max(p[k] for p in pts) for k in range(3)]
        groups.setdefault(classify(mn, mx), set()).update(comp)
    out = {}
    for g, keep in groups.items():
        c = o.copy(); c.data = o.data.copy()
        bpy.context.scene.collection.objects.link(c)
        old = bpy.data.objects.get(prefix + g)
        if old:
            bpy.data.objects.remove(old, do_unlink=True)
        c.name = prefix + g
        bm = bmesh.new(); bm.from_mesh(c.data); bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
        bm.to_mesh(c.data); bm.free()
        out[g] = c
    return out


def cls_torso(mn, mx):
    one = mn[0] * mx[0] > 0
    xin = min(abs(mn[0]), abs(mx[0]))
    if one and xin >= 0.15 and mx[2] < 1.45:
        return 'Sleeve'
    if not one and mn[2] < 0.3:
        return 'Pants'
    return 'Armor'


def cls_legs(mn, mx):
    one = mn[0] * mx[0] > 0
    return 'Gloves' if one and min(abs(mn[0]), abs(mx[0])) >= 0.23 and mn[2] >= 0.75 else 'Boots'


def body_tree(objs=(BODY,), no_arms=False):
    bm = bmesh.new()
    for n in objs:
        b = bpy.data.objects[n]
        tmp = bmesh.new(); tmp.from_mesh(b.data); tmp.transform(b.matrix_world)
        if no_arms and n == BODY:
            wb = bpy.data.objects[WB]
            arm = {g.index for g in wb.vertex_groups if g.name.startswith(('Arm', 'ForeArm', 'Hand'))}
            bad = {v.index for v in wb.data.vertices if sum(g.weight for g in v.groups if g.group in arm) > 0.5}
            tmp.verts.ensure_lookup_table()
            bmesh.ops.delete(tmp, geom=[f for f in tmp.faces if any(v.index in bad for v in f.verts)], context='FACES')
        me = bpy.data.meshes.new('kz_tmp'); tmp.to_mesh(me); tmp.free()
        bm.from_mesh(me); bpy.data.meshes.remove(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    t = BVHTree.FromBMesh(bm); bm.free()
    return t


def fit_all(K, shoulder_pad=0.03, helm_margin=0.025):
    G = globals()
    K['decode_body'](bpy.data.objects[BODY], WB)
    body = body_pts((BODY,))
    head = body_pts((HEAD,))
    # 0) copia no mundo, gira 180° (o modelo olha para +Y) e põe no chão
    R = Matrix.Rotation(math.pi, 4, 'Z')
    raw = {k: wcopy(src_mesh(n), 'SA_raw' + k) for k, n in SRC.items()}
    for o in raw.values():
        o.data.transform(R)
    zmin = min(v.co.z for o in raw.values() for v in o.data.vertices)
    for o in raw.values():
        o.data.transform(Matrix.Translation((0, 0, -zmin)))
    P = {'Helm': raw['Helm']}; P['Helm'].name = 'SA_Helm'
    P.update(split_comps(raw['Torso'], 'SA_', cls_torso))
    P.update(split_comps(raw['Legs'], 'SA_', cls_legs))
    for k in ('Torso', 'Legs'):
        bpy.data.objects.remove(raw[k], do_unlink=True)
    # 1) escala: topo das ombreiras um pouco acima do ombro do corpo; tronco alinhado em y
    sh_body = max(p.z for p in body if 0.15 < abs(p.x) < 0.22)
    sh_arm = max(v.co.z for v in P['Armor'].data.vertices if 0.15 < abs(v.co.x) < 0.22)
    s = (sh_body + shoulder_pad) / sh_arm
    tor = [p for p in body if 1.15 < p.z < 1.4 and abs(p.x) < 0.15]
    by = (min(p.y for p in tor) + max(p.y for p in tor)) / 2
    av = [v.co for v in P['Armor'].data.vertices if 1.15 < v.co.z < 1.4 and abs(v.co.x) < 0.15]
    ay = (min(c.y for c in av) + max(c.y for c in av)) / 2 * s
    M = Matrix.Translation((0, by - ay, 0)) @ Matrix.Scale(s, 4)
    for o in P.values():
        o.data.transform(M)
    # 2) elmo: centrado na cabeça, um pouco maior que ela
    hv = [v.co for v in P['Helm'].data.vertices]
    hmn = [min(c[k] for c in hv) for k in range(3)]; hmx = [max(c[k] for c in hv) for k in range(3)]
    dmn = [min(p[k] for p in head) for k in range(3)]; dmx = [max(p[k] for p in head) for k in range(3)]
    kh = max(1.0, max((dmx[0] - dmn[0] + 2 * helm_margin) / (hmx[0] - hmn[0]),
                      (dmx[1] - dmn[1] + 2 * helm_margin) / (hmx[1] - hmn[1])))
    hc = Vector(((hmn[0] + hmx[0]) / 2, (hmn[1] + hmx[1]) / 2, hmx[2]))
    tgt = Vector(((dmn[0] + dmx[0]) / 2, (dmn[1] + dmx[1]) / 2, dmx[2] + helm_margin))
    P['Helm'].data.transform(Matrix.Translation(tgt) @ Matrix.Scale(kh, 4) @ Matrix.Translation(-hc))
    # 3) braços: da pose da armadura (mãos abertas) para o braço do corpo pendurado; gira no ombro e estica
    gl = [v.co for v in P['Gloves'].data.vertices if v.co.x > 0]
    hand = Vector((sum(c.x for c in gl) / len(gl), 0, min(c.z for c in gl) + 0.06))
    piv = bpy.data.objects['PB_ArmL'].matrix_world.translation
    shoulder = Vector((piv.x, 0, piv.z))
    a0 = math.atan2(shoulder.z - hand.z, hand.x - shoulder.x)
    wb = bpy.data.objects[WB]
    hg = wb.vertex_groups['HandL'].index
    bh = [v.co for v in wb.data.vertices if any(g.group == hg and g.weight > 0.5 for g in v.groups)]
    bhand = Vector((sum(c.x for c in bh) / len(bh), 0, sum(c.z for c in bh) / len(bh)))
    a1 = math.atan2(shoulder.z - bhand.z, bhand.x - shoulder.x)
    st = (bhand - shoulder).length / (hand - shoulder).length
    AW = {}
    for k in ARMS + ('Armor',):
        me = P[k].data
        AW[k] = {}
        for v in me.vertices:
            c = v.co
            sg = 1 if c.x >= 0 else -1
            if k == 'Armor':        # ombreiras: só um pouco, e só perto do ombro (a saia não mexe)
                w = 0.5 * sm(shoulder.x - 0.04, shoulder.x + 0.08, abs(c.x)) * sm(shoulder.z - 0.3, shoulder.z - 0.1, c.z)
            else:
                w = sm(shoulder.x - 0.06, shoulder.x + 0.02, abs(c.x))
            if w <= 0:
                continue
            AW[k][v.index] = w
            x, z = abs(c.x) - shoulder.x, c.z - shoulder.z
            r = math.hypot(x, z); th = math.atan2(z, x)
            r2 = r * (1 + (st - 1) * w)
            th2 = th - (a1 - a0) * w
            c.x = sg * (shoulder.x + r2 * math.cos(th2))
            c.z = shoulder.z + r2 * math.sin(th2)
        me.update()
    # 4) pernas e braços seguem o centro do membro do corpo em cada altura
    before = {k: [v.co.copy() for v in P[k].data.vertices] for k in LEGS}
    conform(P, LEGS, body, 'leg', lambda z: 1 - sm(0.80, 0.95, z), zmax=1.0)
    for k in LEGS:                       # a virilha (x ~ 0) pode cair no lado errado: limita o deslocamento
        for v, c0 in zip(P[k].data.vertices, before[k]):
            d = v.co - c0
            if d.length > 0.08:
                v.co = c0 + d.normalized() * 0.08
        P[k].data.update()
    conform(P, ARMS, body, 'arm', lambda z: 1 - sm(shoulder.z - 0.22, shoulder.z - 0.08, z), zmax=shoulder.z,
            member={k: AW[k] for k in ARMS})
    # 5) nada por dentro da pele (a saia ignora os braços; o elmo também empurra pela cabeça)
    G['BVH'] = body_tree((BODY,), no_arms=True)
    for k, gap in (('Armor', 0.014), ('Pants', 0.010)):
        push_out(P[k], gap)
    G['BVH'] = body_tree((BODY,))
    for k, gap in (('Sleeve', 0.010), ('Gloves', 0.006), ('Boots', 0.008)):
        push_out(P[k], gap)
    G['BVH'] = body_tree((HEAD,))
    push_out(P['Helm'], 0.008)
    G['P'] = P; G['AW'] = AW
    return {'scale': s, 'by-ay': by - ay, 'helm_k': kh, 'shoulder': tuple(shoulder), 'hand': tuple(hand),
            'body_hand': tuple(bhand), 'a0': math.degrees(a0), 'a1': math.degrees(a1), 'stretch': st}


# ================================================================ peças do jogo
def side_faces(src, name, sg):
    return keep_faces(src, name, lambda co, idx: sum(c.x for c in co) * sg > 0)


def cull_covered(inner, outers, reach=0.03):
    bm = bmesh.new()
    for o in outers:
        bm.from_mesh(o.data)
    t = BVHTree.FromBMesh(bm); bm.free()
    bm = bmesh.new(); bm.from_mesh(inner.data); bm.normal_update()
    kill = [f for f in bm.faces if any(t.ray_cast(f.calc_center_median() + d * 0.0005, d, reach)[0] is not None
                                       for d in (f.normal, -f.normal))]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bm.to_mesh(inner.data); bm.free()
    return len(kill)


def build_parts_escamas(K):
    P = globals()['P']
    for x in [x for x in bpy.data.objects if x.name.startswith('KZS_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    M = lambda k: P[k].data.materials[0]

    def dd(k, name):
        o = K['_dup'](P[k], name); decimate(o, DECIMATE.get(k)); return o
    out = {}
    out['Elmo'] = ([dd('Helm', 'KZS_Helm')], [M('Helm')], 'Elmo')
    out['Armadura'] = ([dd('Armor', 'KZS_Armor')], [M('Armor')], 'Armadura')
    sl, gl = dd('Sleeve', 'KZS_SleeveAll'), dd('Gloves', 'KZS_GlovesAll')
    pa, bo = dd('Pants', 'KZS_PantsAll'), dd('Boots', 'KZS_BootsAll')
    cull_covered(pa, [out['Armadura'][0][0]], reach=0.015)
    for side, sg in (('L', 1), ('R', -1)):
        out['Luva' + side] = ([side_faces(gl, 'KZS_Glove' + side, sg), side_faces(sl, 'KZS_Sleeve' + side, sg)],
                              [M('Gloves'), M('Sleeve')], 'Luva')
        out['Calca' + side] = ([side_faces(pa, 'KZS_Pants' + side, sg)], [M('Pants')], 'Calca')
        out['Bota' + side] = ([side_faces(bo, 'KZS_Bota' + side, sg)], [M('Boots')], 'Bota')
    for o in (sl, gl, pa, bo):
        bpy.data.objects.remove(o, do_unlink=True)
    return out


def skirt_mix(co, w):
    """Saia de escamas: abaixo do quadril passa a seguir as coxas."""
    f = 0.85 * sm(0.98, 0.62, co.z)
    if f <= 0:
        return w
    wl = sm(-0.07, 0.07, co.x)
    base = [(b, x * (1 - f)) for b, x in w if b in ('Body', 'Chest')] or [('Body', 1 - f)]
    return base + [('LegL', f * wl), ('LegR', f * (1 - wl))]


def pants_mix(side):
    def f(co, w):
        g = sm(0.86, 1.0, co.z)          # cós da calça acompanha o quadril
        if g <= 0:
            return w
        return [(b, x * (1 - g)) for b, x in w] + [('Body', g)]
    return f


def pivots():
    g = lambda n: tuple(bpy.data.objects[n].matrix_world.translation)
    return {'Armadura': (0.0, 0.0, 0.0), 'Elmo': g(HEAD), 'LuvaL': g('PB_ArmL'), 'LuvaR': g('PB_ArmR'),
            'CalcaL': g('PB_LegL'), 'CalcaR': g('PB_LegR'), 'BotaL': g('PB_LegL'), 'BotaR': g('PB_LegR')}


def enclosed_hide(K, parts, piece, z0, z1, bones=None, min_hits=4, neck_v=False):
    """Pele cercada pela peça (min_hits de 8 raios horizontais batem) some, mesmo onde atravessa o pano."""
    bm = bmesh.new()
    for o in parts[piece][0]:
        bm.from_mesh(o.data)
    t = BVHTree.FromBMesh(bm); bm.free()
    wb = bpy.data.objects[WB]
    gname = {g.index: g.name for g in wb.vertex_groups}
    dirs = [Vector((math.cos(a), math.sin(a), 0)) for a in [i * math.pi / 4 for i in range(8)]]
    pts = []
    for v, vw in zip(wb.data.vertices, wb.data.vertices):
        c = v.co
        if not (z0 < c.z < z1):
            continue
        w = {gname[g.group]: g.weight for g in vw.groups}
        if bones:
            if sum(x for b_, x in w.items() if b_ in bones) < 0.5:
                continue
        elif sum(x for b_, x in w.items() if b_.startswith(('Arm', 'ForeArm', 'Hand', 'Head'))) > 0.5:
            continue
        if sum(t.ray_cast(c + d * 0.001, d, 0.4)[0] is not None for d in dirs) >= min_hits:
            pts.append(tuple(int(round(x * 1000)) for x in c))
    return pts


def merge_hide(K, piece, pts):
    path = os.path.join(K['ROOT'], 'Equip', 'Hide', '%s_%s_%s.txt' % (SET, piece, MODEL))
    old = set(tuple(int(x) for x in l.split()) for l in open(path, encoding='utf-8').read().split('\n') if l.strip())
    allp = sorted(old | set(pts))
    with open(path, 'w', encoding='utf-8') as f:
        f.write('\n'.join('%d %d %d' % p for p in allp))
    return len(allp)


def run_escamas(K, export=True, **fit):
    rep = fit_all(K, **fit)
    K['ALLOWED'].update({'Armadura': ('Body', 'Chest', 'LegL', 'LegR'),
                         'CalcaL': ('LegL', 'ShinL', 'FootL', 'Body'), 'CalcaR': ('LegR', 'ShinR', 'FootR', 'Body')})
    K['WEIGHT_MIX'].update({'Armadura': skirt_mix, 'CalcaL': pants_mix('L'), 'CalcaR': pants_mix('R')})
    K['HIDE_ZMIN'].update({'Armadura': 0.6})
    parts = build_parts_escamas(K)
    if not export:
        return rep, parts
    wb = bpy.data.objects[WB]
    ex = K['export_parts'](SET, parts, wb, pivots(), textures=True)
    hide = K['write_hide'](SET, MODEL, parts, [bpy.data.objects[BODY], bpy.data.objects[HEAD]])
    hide['Armadura'] = merge_hide(K, 'Armadura', enclosed_hide(K, parts, 'Armadura', 0.62, 1.42))
    for side in 'LR':
        hide['Luva' + side] = merge_hide(K, 'Luva' + side, enclosed_hide(
            K, parts, 'Luva' + side, 0.6, 1.5, bones=('Arm' + side, 'ForeArm' + side, 'Hand' + side), min_hits=5))
        hide['Calca' + side] = merge_hide(K, 'Calca' + side, enclosed_hide(
            K, parts, 'Calca' + side, 0.2, 0.95, bones=('Leg' + side, 'Shin' + side), min_hits=5))
    return rep, ex, hide
