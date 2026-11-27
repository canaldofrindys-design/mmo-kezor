# Kezor — veste o "Witcher Outfit" (Sketchfab, Blue-Spirit, CC BY) como o set Couro, que serve às 7 classes.
# A roupa é ajustada no corpo da Lâmina Arcana (medidas no meio das classes) com o skinning por distância do
# kezor_trama_fit, empurrada para fora de TODOS os corpos de classe (tronco/pernas no lugar, braços pelo pivô do
# ombro) e ganha uma lista de pele escondida para cada modelo de classe.
# Uso (Blender, cena kezor_couro_set.blend: LB_* + outros corpos na coleção KZ_Bodies + roupa importada):
#   E = {}; for f in ('kezor_veu_fit.py', 'kezor_bastiao_fit.py', 'kezor_escamas_fit.py', 'kezor_trama_fit.py',
#                     'kezor_couro_fit.py'): exec(open(D + f).read(), E)
#   K = {}; exec(open(D + 'kezor_sketchfab_set.py').read(), K); E['run_couro'](K)
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

BODY, HEAD = 'LB_Body', 'LB_Head'
WB = 'KZB_PesosCouro'
SET, MODEL = 'Couro', 'LaminaArcanaBase'
MODELS = {'LB_': 'LaminaArcanaBase', 'AV_': 'ArcanistaDoVeuBase', 'CE_': 'ComandanteEstandarteBase',
          'GR_': 'GuardiaoRunicoBase', 'IE_': 'InvocadoraEcosBase', 'PS_': 'PatrulheiroSelvagemBase', 'FB_': 'PunhoBrasaBase'}
PIECES = {'Jacket': 'Jacket', 'Belts': 'Belts', 'Gloves': 'Gloves', 'Pants': 'Pants', 'Boots': 'Boots'}
DECIMATE = {'Helm': 0.35, 'Armor': 1.0, 'Belts': 0.45, 'Gloves': 0.35, 'Pants': 1.0, 'Boots': 0.12}
TWIST = {}

SJ = {'SL': (0.18, 0.03, 1.30), 'EL': (0.30, -0.005, 1.085), 'WL': (0.395, -0.115, 0.96), 'TL': (0.455, -0.195, 0.825),
      'HL': (0.09, 0.0, 0.90), 'KL': (0.10, -0.02, 0.50), 'AL': (0.115, 0.0, 0.09), 'FL': (0.11, -0.18, 0.01)}
TJ = {'SL': (0.211, 0.0, 1.381), 'EL': (0.21, -0.022, 1.095), 'WL': (0.212, -0.019, 0.80), 'TL': (0.20, 0.0, 0.62),
      'HL': (0.095, 0.0, 0.92), 'KL': (0.12, 0.0, 0.544), 'AL': (0.113, 0.01, 0.12), 'FL': (0.11, -0.225, 0.01)}
for D_ in (SJ, TJ):
    for k, v in list(D_.items()):
        D_[k[0] + 'R'] = (-v[0], v[1], v[2])
BONES = {}
for s_ in 'LR':
    BONES.update({'UpArm' + s_: ('S' + s_, 'E' + s_, 0.07, 1.04), 'ForeArm' + s_: ('E' + s_, 'W' + s_, 0.06, 1.04),
                  'Hand' + s_: ('W' + s_, 'T' + s_, 0.05, 1.05),
                  'Thigh' + s_: ('H' + s_, 'K' + s_, 0.09, 1.06), 'Shin' + s_: ('K' + s_, 'A' + s_, 0.07, 1.06),
                  'Foot' + s_: ('A' + s_, 'F' + s_, 0.06, 1.08)})
SPINE_R = 0.16
SPINE = {'sx': 1.06, 'sy': 1.03, 'z0': (0.90, 0.92), 'z1': (1.30, 1.381)}


def spine_map(p):
    """Tronco: altura do quadril->quadril e ombro->ombro, largura/profundidade pelo corpo."""
    (a0, b0), (a1, b1) = SPINE['z0'], SPINE['z1']
    q = p.copy()
    q[:, 2] = b0 + (p[:, 2] - a0) * (b1 - b0) / (a1 - a0)
    q[:, 0] *= SPINE['sx']
    q[:, 1] = -0.027 + (p[:, 1] + 0.027) * SPINE['sy']
    return q


def prep():
    col = bpy.data.collections.get('TR_work') or bpy.data.collections.new('TR_work')
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    out = []
    for o in [o for o in bpy.data.objects if o.type == 'MESH' and o.data.materials and o.name.startswith('Object_')]:
        m = o.data.materials[0].name
        o.hide_set(True)
        if m not in PIECES:
            continue
        c = wcopy(o, 'TR_' + PIECES[m])
        for cc in c.users_collection:
            cc.objects.unlink(c)
        col.objects.link(c)
        c['src'] = o.name
        c['kind'] = {'Gloves': 'Hand', 'Boots': 'Boots'}.get(m, 'Torso')
        out.append(c)
    return out


def make_band(K, belts, margin=0.012):
    """Faixa de couro na testa: o cinto da cintura (maior anel) vai para a cabeça."""
    co = _np(belts)
    best = set()
    for comp in components(belts.data):                 # o cinto vem em vários pedaços: junta os da cintura
        p = co[list(comp)]
        if 0.96 < p[:, 2].mean() < 1.09 and p[:, 2].max() - p[:, 2].min() < 0.1:
            best |= set(comp)
    band = K['_dup'](belts, 'TA_Helm')
    bm = bmesh.new(); bm.from_mesh(band.data); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in best], context='VERTS')
    bm.to_mesh(band.data); bm.free()
    p = _np(band)
    wb = bpy.data.objects[WB]; gi = wb.vertex_groups['Head'].index; mw = wb.matrix_world
    head = np.array([tuple(mw @ v.co) for v in wb.data.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)])
    zt = head[:, 2].max() - 0.075                                   # altura da testa
    ring = head[abs(head[:, 2] - zt) < 0.012]
    hc = (ring.min(0) + ring.max(0)) / 2; hs = (ring.max(0) - ring.min(0)) / 2 + margin
    bc = (p.min(0) + p.max(0)) / 2; bs = (p.max(0) - p.min(0)) / 2
    k = min(1.0, 0.035 / max(bs[2], 1e-3))                           # faixa com ~3,5 cm de altura no máximo
    q = (p - bc) * np.array([hs[0] / bs[0], hs[1] / bs[1], k]) + np.array([hc[0], hc[1], zt])
    _set(band, q)
    for cc in band.users_collection:
        cc.objects.unlink(band)
    bpy.data.collections['TR_work'].objects.link(band)
    return band


def split_attr(K, o, name_in, name_out, thr=0.5):
    """Divide pela média do peso de braço (kz_arm) de cada face: manga x tronco."""
    at = o.data.attributes['kz_arm']
    w = np.empty(len(o.data.vertices), np.float32); at.data.foreach_get('value', w)
    a = K['_dup'](o, name_in); b = K['_dup'](o, name_out)
    for obj, keep_arm in ((a, False), (b, True)):
        bm = bmesh.new(); bm.from_mesh(obj.data)
        kill = [f for f in bm.faces if (np.mean([w[v.index] for v in f.verts]) > thr) != keep_arm]
        bmesh.ops.delete(bm, geom=kill, context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        bm.to_mesh(obj.data); bm.free()
        for cc in obj.users_collection:
            cc.objects.unlink(obj)
        bpy.data.collections['TR_work'].objects.link(obj)
    return a, b


def categorize(K, objs):
    for x in [x for x in bpy.data.objects if x.name.startswith('TA_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    O = {o.name[3:]: o for o in objs}
    P = {}
    P['Jacket'], P['Sleeve'] = split_attr(K, O['Jacket'], 'TA_Jacket', 'TA_Sleeve')
    P['Belts'], P['ArmBelts'] = split_attr(K, O['Belts'], 'TA_Belts', 'TA_ArmBelts')
    for k in ('Gloves', 'Pants', 'Boots'):
        P[k] = K['_dup'](O[k], 'TA_' + k)
        for cc in P[k].users_collection:
            cc.objects.unlink(P[k])
        bpy.data.collections['TR_work'].objects.link(P[k])
    return P


# ---------------------------------------------------------------- todos os corpos de classe
def _decode_all(K):
    for pre in MODELS:
        n = 'KZB_W_' + pre
        if not bpy.data.objects.get(n):
            K['decode_body'](bpy.data.objects[pre + 'Body'], n)
        bpy.data.objects[n].hide_set(True)


def region_tree(pre, region, shift=None):
    """BVH de uma região do corpo da classe (torso/pernas sem braços, ou só um braço), deslocada pelo pivô."""
    wb = bpy.data.objects['KZB_W_' + pre]
    gname = {g.index: g.name for g in wb.vertex_groups}
    want = {'torso': lambda n: not n.startswith(('Arm', 'ForeArm', 'Hand', 'Head')),
            'armL': lambda n: n in ('ArmL', 'ForeArmL', 'HandL'), 'armR': lambda n: n in ('ArmR', 'ForeArmR', 'HandR')}[region]
    ok = set()
    for v in wb.data.vertices:
        w = sum(g.weight for g in v.groups if want(gname[g.group]))
        if w > 0.5:
            ok.add(v.index)
    bm = bmesh.new(); bm.from_mesh(wb.data); bm.transform(wb.matrix_world)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(v.index in ok for v in f.verts)], context='FACES')
    if shift is not None:
        bm.transform(Matrix.Translation(shift))
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    t = BVHTree.FromBMesh(bm); bm.free()
    return t


def piv(pre, n):
    return bpy.data.objects[pre + n].matrix_world.translation.copy()


def settle_all(K, P):
    """Empurra a roupa para fora de todos os corpos: tronco e pernas no lugar, cada braço trazido ao ombro da Lâmina."""
    G = globals()
    _decode_all(K)
    for pre in MODELS:
        G['BVH'] = region_tree(pre, 'torso')
        for k in ('Jacket', 'Belts', 'Pants', 'Boots'):
            push_out(P[k], {'Jacket': 0.010, 'Belts': 0.016, 'Pants': 0.008, 'Boots': 0.006}[k])
        for side, sg in (('L', 1), ('R', -1)):
            G['BVH'] = region_tree(pre, 'arm' + side, piv('LB_', 'Arm' + side) - piv(pre, 'Arm' + side))
            for k in ('Sleeve', 'ArmBelts', 'Gloves'):
                o = P[k]
                sel = [v for v in o.data.vertices if v.co.x * sg > 0]
                tmp = {v.index: v.co.copy() for v in o.data.vertices if v.co.x * sg <= 0}
                push_out(o, {'Sleeve': 0.008, 'ArmBelts': 0.014, 'Gloves': 0.004}[k])
                for i, c in tmp.items():
                    o.data.vertices[i].co = c
                o.data.update()


def settle_parts(parts):
    """Depois de reduzir as malhas: empurra de novo cada peça para fora de todos os corpos."""
    G = globals()
    gap = {'Armadura': 0.012, 'CalcaL': 0.010, 'CalcaR': 0.010, 'BotaL': 0.006, 'BotaR': 0.006}
    for pre in MODELS:
        G['BVH'] = region_tree(pre, 'torso')
        for piece, g in gap.items():
            for o in parts[piece][0]:
                push_out(o, g)
        for side in 'LR':
            t = region_tree(pre, 'arm' + side)                       # ombreira do gibão fica no tronco: braço no lugar
            sg = 1 if side == 'L' else -1
            for o in parts['Armadura'][0]:
                for v in o.data.vertices:
                    if v.co.z < 1.15 or v.co.x * sg < 0.12:
                        continue
                    h = t.find_nearest(v.co, 0.06)
                    if h[0] is None or (h[1].x * sg < 0.2 and h[1].z < 0.5):   # só para fora/para cima
                        continue
                    d = (v.co - h[0]).dot(h[1])
                    if d < 0.010:
                        v.co = v.co + h[1] * (0.010 - d)
                o.data.update()
            G['BVH'] = region_tree(pre, 'arm' + side, piv('LB_', 'Arm' + side) - piv(pre, 'Arm' + side))
            for o in parts['Luva' + side][0]:
                push_out(o, 0.008)


def build_parts_couro(K, P, band):
    for x in [x for x in bpy.data.objects if x.name.startswith('KZS_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    M = lambda o: o.data.materials[0]

    def dd(o, name, r):
        c = K['_dup'](o, name); decimate(c, r); return c
    out = {'Elmo': ([dd(band, 'KZS_Band', DECIMATE['Helm'])], [M(band)], 'Elmo'),
           'Armadura': ([dd(P['Jacket'], 'KZS_Jacket', DECIMATE['Armor']), dd(P['Belts'], 'KZS_Belts', DECIMATE['Belts'])],
                        [M(P['Jacket']), M(P['Belts'])], 'Armadura')}
    sl = dd(P['Sleeve'], 'KZS_SleeveAll', DECIMATE['Armor'])
    ab = dd(P['ArmBelts'], 'KZS_ArmBeltsAll', DECIMATE['Belts'])
    gl = dd(P['Gloves'], 'KZS_GlovesAll', DECIMATE['Gloves'])
    pa = dd(P['Pants'], 'KZS_PantsAll', DECIMATE['Pants'])
    bo = dd(P['Boots'], 'KZS_BootsAll', DECIMATE['Boots'])
    for side, sg in (('L', 1), ('R', -1)):
        out['Luva' + side] = ([side_faces(sl, 'KZS_Sleeve' + side, sg), side_faces(ab, 'KZS_ArmBelt' + side, sg),
                               side_faces(gl, 'KZS_Glove' + side, sg)], [M(P['Sleeve']), M(P['Belts']), M(P['Gloves'])], 'Luva')
        out['Calca' + side] = ([side_faces(pa, 'KZS_Pants' + side, sg)], [M(P['Pants'])], 'Calca')
        out['Bota' + side] = ([side_faces(bo, 'KZS_Bota' + side, sg)], [M(P['Boots'])], 'Bota')
    for o in (sl, ab, gl, pa, bo):
        bpy.data.objects.remove(o, do_unlink=True)
    settle_parts(out)
    return out


def pivots(pre='LB_'):
    g = lambda n: tuple(bpy.data.objects[pre + n].matrix_world.translation)
    return {'Armadura': (0.0, 0.0, 0.0), 'Elmo': g('Head'), 'LuvaL': g('ArmL'), 'LuvaR': g('ArmR'),
            'CalcaL': g('LegL'), 'CalcaR': g('LegR'), 'BotaL': g('LegL'), 'BotaR': g('LegR')}


def fit_couro(K, debug=False):
    K['decode_body'](bpy.data.objects[BODY], WB)
    bpy.data.objects[WB].hide_set(True)
    objs = prep()
    repose(objs, debug=debug)
    P = categorize(K, objs)
    band = make_band(K, P['Belts'])
    G = globals()
    G['BVH'] = body_tree((BODY,), no_arms=True)
    settle_all(K, P)
    G['BVH'] = body_tree((HEAD,))
    push_out(band, 0.006)
    for o in objs:
        o.hide_set(True)
    G['P'] = P; G['BAND'] = band
    return P, band


def hide_for_model(K, parts, pre):
    """Lista de pele escondida para o modelo de outra classe: cada peça vai para o pivô dela naquele corpo."""
    base, mine = pivots('LB_'), pivots(pre)
    moved = {}
    tmp = []
    for piece, (objs, mats, tex) in parts.items():
        d = Vector(mine[piece]) - Vector(base[piece])
        lst = []
        for o in objs:
            c = K['_dup'](o, 'KZS_h_' + o.name); c.data.transform(Matrix.Translation(d)); lst.append(c); tmp.append(c)
        moved[piece] = (lst, mats, tex)
    out = K['write_hide'](SET, MODELS[pre], moved, [bpy.data.objects[pre + 'Body'], bpy.data.objects[pre + 'Head']])
    G = globals(); old = G['WB']; G['WB'] = 'KZB_W_' + pre
    try:
        out['Armadura'] = merge_hide_m(K, 'Armadura', MODELS[pre], enclosed_hide(K, moved, 'Armadura', 0.85, 1.42))
    finally:
        G['WB'] = old
    for piece, pts in poke_hide(K, moved, pre).items():
        out[piece] = merge_hide_m(K, piece, MODELS[pre], pts)
    for c in tmp:
        bpy.data.objects.remove(c, do_unlink=True)
    return out


def poke_hide(K, parts, pre, reach=0.06):
    """Pele que fura a roupa: o vértice fica fora, mas logo atrás dele (para dentro) há pano da peça -> some também."""
    out = {}
    o = K['world_copy'](bpy.data.objects[pre + 'Body'], 'KZS_poke_tmp')
    bm = bmesh.new(); bm.from_mesh(o.data); bm.normal_update()
    for piece, (objs, _, _) in parts.items():
        if piece == 'Elmo':
            continue
        tb = bmesh.new()
        for x in objs:
            tb.from_mesh(x.data)
        t = BVHTree.FromBMesh(tb); tb.free()
        pts = []
        for v in bm.verts:
            if v.co.z < 0.05:
                continue
            h = t.ray_cast(v.co - v.normal * 0.0005, -v.normal, reach)
            if h[0] is not None:
                pts.append(tuple(int(round(c * 1000)) for c in v.co))
        out[piece] = pts
    bm.free(); bpy.data.objects.remove(o, do_unlink=True)
    return out


def merge_hide_m(K, piece, model, pts):
    path = os.path.join(K['ROOT'], 'Equip', 'Hide', '%s_%s_%s.txt' % (SET, piece, model))
    old = set(tuple(int(x) for x in l.split()) for l in open(path, encoding='utf-8').read().split('\n') if l.strip())
    allp = sorted(old | set(pts))
    with open(path, 'w', encoding='utf-8') as f:
        f.write('\n'.join('%d %d %d' % p for p in allp))
    return len(allp)


def run_couro(K, export=True, **fit):
    P, band = fit_couro(K, **fit)
    K['ALLOWED'].update({'Armadura': ('Body', 'Chest'), 'Elmo': ('Head',),
                         'CalcaL': ('LegL', 'ShinL', 'FootL', 'Body'), 'CalcaR': ('LegR', 'ShinR', 'FootR', 'Body')})
    K['WEIGHT_MIX'].update({'CalcaL': pants_mix('L'), 'CalcaR': pants_mix('R')})
    K['WEIGHT_MIX'].pop('Armadura', None)
    K['HIDE_ZMIN'].update({'Armadura': 0.75})
    parts = build_parts_couro(K, P, band)
    if not export:
        return parts
    ex = K['export_parts'](SET, parts, bpy.data.objects[WB], pivots(), textures=True)
    hides = {pre: hide_for_model(K, parts, pre) for pre in MODELS}
    return ex, hides
