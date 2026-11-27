# Kezor — veste a "Mithril Armor set" (Sketchfab, 12oyraj, CC BY) no corpo da Lâmina Arcana (set Trama).
# O modelo vem numa pose de ação (braço erguido, pernas abertas), então cada pedaço é reposicionado
# osso a osso (skinning por distância às juntas estimadas da armadura -> juntas do corpo).
# Uso (Blender, cena com LB_Body/LB_Head/LB_ArmL... da LaminaArcanaBase e a armadura importada):
#   E = {}; for f in ('kezor_veu_fit.py', 'kezor_bastiao_fit.py', 'kezor_escamas_fit.py', 'kezor_trama_fit.py'): exec(open(D + f).read(), E)
#   K = {}; exec(open(D + 'kezor_sketchfab_set.py').read(), K); E['run_trama'](K)
import bpy, math, bmesh, os
import numpy as np
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree

BODY, HEAD = 'LB_Body', 'LB_Head'
WB = 'KZB_PesosTrama'
SET, MODEL = 'Trama', 'LaminaArcanaBase'
DROP = ('beard', 'CLOAK', 'eyes4', 'gum', 'haft', 'head2')
DECIMATE = {'Helm': 0.8, 'Armor': 0.9, 'Gloves': 0.6, 'Pants': 0.9, 'Boots': 0.6}

# juntas da armadura (depois de girar 180° e pôr no chão), lidas das projeções
SJ = {'SL': (0.25, 0.0, 1.38), 'EL': (0.34, -0.02, 1.20), 'WL': (0.31, -0.16, 1.00), 'TL': (0.29, -0.23, 0.87),
      'SR': (-0.25, 0.0, 1.38), 'ER': (-0.34, 0.08, 1.18), 'WR': (-0.45, -0.13, 1.24), 'TR': (-0.53, -0.26, 1.24),
      'HL': (0.10, -0.02, 0.92), 'KL': (0.30, -0.01, 0.47), 'AL': (0.36, 0.13, 0.12), 'FL': (0.555, -0.03, 0.02),
      'HR': (-0.10, -0.02, 0.92), 'KR': (-0.30, -0.09, 0.45), 'AR': (-0.34, -0.01, 0.12), 'FR': (-0.54, -0.176, 0.02)}
# juntas do corpo da Lâmina (fronteiras dos pesos do KZB_PesosTrama)
TJ = {'SL': (0.211, 0.0, 1.381), 'EL': (0.21, -0.022, 1.095), 'WL': (0.212, -0.019, 0.80), 'TL': (0.20, 0.0, 0.62),
      'HL': (0.095, 0.0, 0.92), 'KL': (0.12, 0.0, 0.544), 'AL': (0.113, 0.01, 0.12), 'FL': (0.11, -0.225, 0.01)}
for k, v in list(TJ.items()):
    TJ[k[0] + 'R'] = (-v[0], v[1], v[2])

# osso: (junta inicial, junta final, raio de influência, escala radial)
BONES = {}
for s in 'LR':
    BONES.update({'UpArm' + s: ('S' + s, 'E' + s, 0.075, 0.85), 'ForeArm' + s: ('E' + s, 'W' + s, 0.065, 0.85),
                  'Hand' + s: ('W' + s, 'T' + s, 0.05, 0.9),
                  'Thigh' + s: ('H' + s, 'K' + s, 0.10, 0.88), 'Shin' + s: ('K' + s, 'A' + s, 0.075, 1.0),
                  'Foot' + s: ('A' + s, 'F' + s, 0.06, 1.18)})
SPINE_R = 0.17
# giro extra em torno do eixo do osso no corpo (graus): as pernas da armadura estão viradas para fora
TWIST = {'ThighL': 45.0, 'ShinL': 45.0, 'ThighR': -45.0, 'ShinR': -45.0}


def spine_scale(z):
    """Escala do tronco em x/y: a armadura é de um homem largo; a saia abre um pouco mais."""
    return 0.76 + 0.10 * sm(1.0, 0.75, z)


def prep():
    col = bpy.data.collections.get('TR_work') or bpy.data.collections.new('TR_work')
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    R = Matrix.Rotation(math.pi, 4, 'Z')
    srcs = [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith(('LB_', 'TR_', 'KZ', 'TRJ_'))
            and not o.name.startswith(DROP) and o.name != WB]
    out = []
    for i, o in enumerate(srcs):
        c = wcopy(o, 'TR_s%02d' % i)
        for cc in c.users_collection:
            cc.objects.unlink(c)
        col.objects.link(c)
        c.data.transform(R); c['src'] = o.name
        c['kind'] = kind_of(c)
        out.append(c)
    zmin = min(v.co.z for c in out for v in c.data.vertices)
    for c in out:
        c.data.transform(Matrix.Translation((0, 0, -zmin)))
    for o in bpy.data.objects:
        if o.type == 'MESH' and (o in srcs or o.name.startswith(DROP)):
            o.hide_set(True)
    return out


def kind_of(o):
    if 'kind' in o:
        return o['kind']
    s = o['src']
    if 'HELMET' in s:
        return 'Helm'
    if 'ElvenGauntlets' in s:
        return 'Hand'
    if 'Boots_1' in s:
        return 'Boots'
    if 'polySurface2_MASTERMITHRIL_MARMOSET:GAUNTLET' in s:
        return 'Bracer'
    vs = [v.co for v in o.data.vertices]
    if len(vs) < 80 and min(abs(c.x) for c in vs) > 0.2:      # rebites dos braceletes
        return 'Bracer'
    return 'Torso'


def _np(o):
    a = np.empty(len(o.data.vertices) * 3); o.data.vertices.foreach_get('co', a); return a.reshape(-1, 3)


def _set(o, a):
    o.data.vertices.foreach_set('co', a.reshape(-1)); o.data.update()


def bone_xf(b):
    a0, b0 = Vector(SJ[BONES[b][0]]), Vector(SJ[BONES[b][1]])
    a1, b1 = Vector(TJ[BONES[b][0]]), Vector(TJ[BONES[b][1]])
    d0, d1 = b0 - a0, b1 - a1
    q = d0.normalized().rotation_difference(d1.normalized())
    Rm = Matrix.Rotation(math.radians(TWIST.get(b, 0.0)), 3, d1.normalized()) @ q.to_matrix()
    return np.array(a0), np.array(d0.normalized()), d0.length, np.array(a1), np.array(Rm), d1.length / d0.length


def seg_dist(p, a, d, L):
    t = np.clip((p - a) @ d, 0, L)
    return np.linalg.norm(p - (a + t[:, None] * d), axis=1)


def repose(objs, allowed=None, sharp=2.0, debug=False):
    XF = {b: bone_xf(b) for b in BONES}
    zs = np.linspace(0.85, 1.6, 16)
    for o in objs:
        p = _np(o)
        names, outs, ws = [], [], []
        k = kind_of(o)
        bl = list(BONES)
        if k == 'Hand':
            bl = [b for b in BONES if b.startswith(('Hand', 'ForeArm'))]
        elif k == 'Bracer':
            bl = [b for b in BONES if b.startswith(('Hand', 'ForeArm', 'UpArm'))]
        elif k == 'Boots':
            bl = [b for b in BONES if b.startswith(('Shin', 'Foot', 'Thigh'))]
        for b in bl:
            a0, d0, L0, a1, Rm, st = XF[b]
            rel = p - a0
            t = rel @ d0
            perp = rel - t[:, None] * d0
            loc = t[:, None] * d0 * st + perp * BONES[b][3]
            outs.append(a1 + loc @ Rm.T)
            ws.append(np.exp(-sharp * (seg_dist(p, a0, d0, L0) / BONES[b][2]) ** 2))
            names.append(b)
        if k in ('Torso',):
            s = np.array([spine_scale(z) for z in p[:, 2]])
            q = p.copy(); q[:, 0] *= s; q[:, 1] = -0.025 + (p[:, 1] + 0.025) * s
            outs.append(q)
            zc = np.clip(p[:, 2], 0.95, 1.5)
            d = np.sqrt(p[:, 0] ** 2 + (p[:, 1] + 0.025) ** 2 + (p[:, 2] - zc) ** 2)
            ws.append(np.exp(-sharp * (d / SPINE_R) ** 2) + 1e-6)
            names.append('Spine')
        W = np.array(ws); W /= W.sum(0) + 1e-12
        newp = sum(W[i][:, None] * outs[i] for i in range(len(outs)))
        _set(o, newp)
        if debug:
            print(o.name, k, len(p), {n: round(float(W[i].mean()), 2) for i, n in enumerate(names) if W[i].mean() > 0.02})


def fit_helm(helm, margin=0.02, pitch=0.0):
    """Endireita o elmo (o eixo de simetria vira o eixo x; pitch em graus gira para a frente) e encaixa na cabeça."""
    head = body_pts((HEAD,))
    p = _np(helm)
    c = p.mean(0)
    lat = np.linalg.svd(p - c, full_matrices=False)[2][2]
    if lat[0] < 0:
        lat = -lat
    q = Vector(lat).rotation_difference(Vector((1, 0, 0)))
    R = Matrix.Rotation(math.radians(pitch), 4, 'X') @ q.to_matrix().to_4x4()
    helm.data.transform(R @ Matrix.Translation(-Vector(c)))
    hv = [v.co for v in helm.data.vertices]
    hmn = [min(c_[k] for c_ in hv) for k in range(3)]; hmx = [max(c_[k] for c_ in hv) for k in range(3)]
    dmn = [min(p_[k] for p_ in head) for k in range(3)]; dmx = [max(p_[k] for p_ in head) for k in range(3)]
    kh = max((dmx[0] - dmn[0] + 2 * margin) / (hmx[0] - hmn[0]), (dmx[1] - dmn[1] + 2 * margin) / (hmx[1] - hmn[1]))
    hc = Vector(((hmn[0] + hmx[0]) / 2, (hmn[1] + hmx[1]) / 2, hmx[2]))
    top = Vector(((dmn[0] + dmx[0]) / 2, (dmn[1] + dmx[1]) / 2, dmx[2] + margin))
    helm.data.transform(Matrix.Translation(top) @ Matrix.Scale(kh, 4) @ Matrix.Translation(-hc))
    return kh


def body_tree_t(objs=(BODY,), no_arms=False):
    G = globals()
    G['WB'] = WB
    return body_tree(objs, no_arms)


# ================================================================ categorias, ajuste final e peças do jogo
def flat_maps(mat):
    """O pelo (FUR) só tem cor: cria normal plana e metal/rugosidade neutros para o atlas."""
    nt = mat.node_tree
    for key, rgba in (('normal', (0.5, 0.5, 1.0, 1.0)), ('metallicRoughness', (1.0, 0.95, 0.0, 1.0))):
        if any(nd.type == 'TEX_IMAGE' and nd.image and key in nd.image.name for nd in nt.nodes):
            continue
        name = 'KZ_%s_%s' % (mat.name[-8:], key)
        img = bpy.data.images.get(name) or bpy.data.images.new(name, 4, 4, alpha=False)
        img.pixels.foreach_set(list(rgba) * 16)
        nd = nt.nodes.new('ShaderNodeTexImage'); nd.image = img


def mat_key(o):
    n = o.data.materials[0].name
    for k in ('UPPERBODY', 'ARMOR_LOWER', 'GAUNTLET', 'FUR', 'lambert2'):
        if k in n:
            return k
    return n


def cls_lower(mn, mx):
    return ('PantsL' if mn[0] + mx[0] > 0 else 'PantsR') if (mx[2] < 1.0 and mn[2] < 0.65) else 'Rest'


def join_objs(K, objs, name):
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    objs = [K['_dup'](o, name + '_tmp%d' % i) for i, o in enumerate(objs)]
    return K['_join'](objs, name) if len(objs) > 1 else (setattr(objs[0], 'name', name) or objs[0])


def categorize(K, objs):
    """TR_s* reposicionados -> TA_Helm, TA_Armor_<mat>, TA_Gloves_<mat>, TA_PantsL/R, TA_Boots."""
    for x in [x for x in bpy.data.objects if x.name.startswith('TA_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    groups = {}
    tmp = []
    for o in objs:
        k = kind_of(o); m = mat_key(o)
        if m == 'lambert2':
            continue
        if k == 'Helm':
            groups.setdefault('Helm', []).append(o)
        elif k in ('Hand', 'Bracer'):
            groups.setdefault('Gloves_' + m, []).append(o)
        elif k == 'Boots':
            groups.setdefault('Boots', []).append(o)
        elif 'polySurface2_ARMOR_LOWER' in o['src']:
            for g, c in split_comps(o, 'TRx_', cls_lower).items():
                c.select_set(False)
                tmp.append(c)
                groups.setdefault(g if g != 'Rest' else 'Armor_' + m, []).append(c)
        else:
            groups.setdefault('Armor_' + m, []).append(o)
    bpy.context.view_layer.update()
    for x in bpy.context.selected_objects:
        x.select_set(False)
    P = {g: join_objs(K, l, 'TA_' + g) for g, l in groups.items()}
    for c in tmp:
        bpy.data.objects.remove(c, do_unlink=True)
    for o in P.values():
        for c in o.users_collection:
            c.objects.unlink(o)
        bpy.data.collections['TR_work'].objects.link(o)
    return P


def settle(P, gaps=None):
    G = globals()
    g = {'Armor': 0.012, 'Pants': 0.010, 'Gloves': 0.006, 'Boots': 0.008, 'Helm': 0.008}
    g.update(gaps or {})
    G['BVH'] = body_tree((BODY,), no_arms=True)
    for k, o in P.items():
        if k.startswith(('Armor', 'Pants')):
            push_out(o, g[k.split('_')[0].rstrip('LR')])
    G['BVH'] = body_tree((BODY,))
    for k, o in P.items():
        if k.startswith(('Gloves', 'Boots')):
            push_out(o, g[k.split('_')[0]])
    G['BVH'] = body_tree((HEAD,))
    push_out(P['Helm'], g['Helm'])


def build_parts_trama(K, P):
    for x in [x for x in bpy.data.objects if x.name.startswith('KZS_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    M = lambda k: P[k].data.materials[0]

    def dd(k, name, r):
        o = K['_dup'](P[k], name); decimate(o, r); return o
    out = {'Elmo': ([dd('Helm', 'KZS_Helm', DECIMATE['Helm'])], [M('Helm')], 'Elmo')}
    am = [k for k in ('Armor_UPPERBODY', 'Armor_ARMOR_LOWER', 'Armor_GAUNTLET', 'Armor_FUR') if k in P]
    for k in am:
        if 'FUR' in k:
            flat_maps(M(k))
    out['Armadura'] = ([dd(k, 'KZS_' + k, DECIMATE['Armor']) for k in am], [M(k) for k in am], 'Armadura')
    gm = [k for k in ('Gloves_GAUNTLET', 'Gloves_ARMOR_LOWER') if k in P]
    gl = [dd(k, 'KZS_%sAll' % k, DECIMATE['Gloves']) for k in gm]
    bo = dd('Boots', 'KZS_BootsAll', DECIMATE['Boots'])
    for side, sg in (('L', 1), ('R', -1)):
        out['Luva' + side] = ([side_faces(g, 'KZS_%s%s' % (k, side), sg) for g, k in zip(gl, gm)], [M(k) for k in gm], 'Luva')
        out['Calca' + side] = ([dd('Pants' + side, 'KZS_Pants' + side, DECIMATE['Pants'])], [M('Pants' + side)], 'Calca')
        out['Bota' + side] = ([side_faces(bo, 'KZS_Bota' + side, sg)], [M('Boots')], 'Bota')
    for o in gl + [bo]:
        bpy.data.objects.remove(o, do_unlink=True)
    return out


def pivots():
    g = lambda n: tuple(bpy.data.objects[n].matrix_world.translation)
    return {'Armadura': (0.0, 0.0, 0.0), 'Elmo': g(HEAD), 'LuvaL': g('LB_ArmL'), 'LuvaR': g('LB_ArmR'),
            'CalcaL': g('LB_LegL'), 'CalcaR': g('LB_LegR'), 'BotaL': g('LB_LegL'), 'BotaR': g('LB_LegR')}


def fit_trama(K, pitch=-12.0, helm_margin=0.012, debug=False):
    K['decode_body'](bpy.data.objects[BODY], WB)
    objs = prep()
    helm = [o for o in objs if kind_of(o) == 'Helm'][0]
    repose([o for o in objs if o is not helm], debug=debug)
    kh = fit_helm(helm, margin=helm_margin, pitch=pitch)
    P = categorize(K, objs)
    settle(P)
    for o in objs:
        o.hide_set(True)
    globals()['P'] = P
    return P, kh


def run_trama(K, export=True, **fit):
    P, kh = fit_trama(K, **fit)
    K['ALLOWED'].update({'Armadura': ('Body', 'Chest', 'LegL', 'LegR'),
                         'CalcaL': ('LegL', 'ShinL', 'FootL', 'Body'), 'CalcaR': ('LegR', 'ShinR', 'FootR', 'Body')})
    K['WEIGHT_MIX'].update({'Armadura': skirt_mix, 'CalcaL': pants_mix('L'), 'CalcaR': pants_mix('R')})
    K['HIDE_ZMIN'].update({'Armadura': 0.5})
    parts = build_parts_trama(K, P)
    if not export:
        return kh, parts
    wb = bpy.data.objects[WB]
    ex = K['export_parts'](SET, parts, wb, pivots(), textures=True)
    hide = K['write_hide'](SET, MODEL, parts, [bpy.data.objects[BODY], bpy.data.objects[HEAD]])
    hide['Armadura'] = merge_hide(K, 'Armadura', enclosed_hide(K, parts, 'Armadura', 0.55, 1.42))
    for side in 'LR':
        hide['Luva' + side] = merge_hide(K, 'Luva' + side, enclosed_hide(
            K, parts, 'Luva' + side, 0.55, 1.2, bones=('ForeArm' + side, 'Hand' + side), min_hits=5))
    return kh, ex, hide
