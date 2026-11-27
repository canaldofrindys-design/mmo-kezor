# Kezor — veste o "Ancient indian warrior" (Sketchfab, RustyGREINDER, CC BY) no corpo do Punho de Brasa (set Ígneo).
# O modelo vem com rig Mixamo: a pose de descanso (T) dá as juntas exatas da roupa, e o mesmo skinning por
# distância do kezor_trama_fit leva cada peça para as juntas do corpo (braços descem, pernas alinham).
# Uso (Blender, cena com FB_Body/FB_Head/FB_ArmL... da PunhoBrasaBase e o modelo importado):
#   E = {}; for f in ('kezor_veu_fit.py', 'kezor_bastiao_fit.py', 'kezor_escamas_fit.py', 'kezor_trama_fit.py',
#                     'kezor_igneo_fit.py'): exec(open(D + f).read(), E)
#   K = {}; exec(open(D + 'kezor_sketchfab_set.py').read(), K); E['run_igneo'](K)
import bpy, math, os
import numpy as np
from mathutils import Vector, Matrix

BODY, HEAD = 'FB_Body', 'FB_Head'
WB = 'KZB_PesosIgneo'
SET, MODEL = 'Igneo', 'PunhoBrasaBase'
RIG = 'Object_4'
SHOULDER_Z = 1.384                      # altura do ombro do corpo: escala do modelo
PIECES = {'Shoesmat': 'Shoes', 'Glovemat': 'Gloves', 'Hatmat': 'Hat', 'Topmat': 'Top', 'Bottommat': 'Bottom'}
DECIMATE = {'Helm': 1.0, 'Armor': 1.0, 'Gloves': 0.8, 'Pants': 1.0, 'Boots': 0.8}
TWIST = {}
BONES = {}
for s_ in 'LR':
    BONES.update({'UpArm' + s_: ('S' + s_, 'E' + s_, 0.075, 1.08), 'ForeArm' + s_: ('E' + s_, 'W' + s_, 0.065, 1.08),
                  'Hand' + s_: ('W' + s_, 'T' + s_, 0.05, 1.05),
                  'Thigh' + s_: ('H' + s_, 'K' + s_, 0.10, 1.05), 'Shin' + s_: ('K' + s_, 'A' + s_, 0.075, 1.05),
                  'Foot' + s_: ('A' + s_, 'F' + s_, 0.06, 1.1)})

TJ = {'SL': (0.24, 0.0, 1.384), 'EL': (0.242, -0.032, 1.097), 'WL': (0.243, -0.025, 0.801), 'TL': (0.225, -0.015, 0.60),
      'HL': (0.098, 0.0, 0.91), 'KL': (0.11, 0.007, 0.541), 'AL': (0.114, 0.0, 0.124), 'FL': (0.11, -0.22, 0.01)}
for k, v in list(TJ.items()):
    TJ[k[0] + 'R'] = (-v[0], v[1], v[2])
SJ = {}


def rig_joints():
    """Juntas da roupa na pose T (cabeças dos ossos Mixamo), já na escala do corpo."""
    arm = bpy.data.objects[RIG]
    s = SHOULDER_Z / (arm.matrix_world @ arm.data.bones[[b.name for b in arm.data.bones if b.name.split(':')[-1].startswith('LeftArm')][0]].head_local).z
    S = Matrix.Scale(s, 4) @ arm.matrix_world
    B = {b.name.split(':')[-1].rsplit('_', 1)[0]: b for b in arm.data.bones}
    h = lambda n: tuple(S @ B[n].head_local)
    t = lambda n: tuple(S @ B[n].tail_local)
    for side, nm in (('L', 'Left'), ('R', 'Right')):
        SJ.update({'S' + side: h(nm + 'Arm'), 'E' + side: h(nm + 'ForeArm'), 'W' + side: h(nm + 'Hand'),
                   'T' + side: h(nm + 'HandMiddle3'), 'H' + side: h(nm + 'UpLeg'), 'K' + side: h(nm + 'Leg'),
                   'A' + side: h(nm + 'Foot'), 'F' + side: t(nm + 'ToeBase')})
    return s


def spine_scale(z):
    return SPINE_K


SPINE_K = 1.08


def prep():
    """Cópias no mundo (pose de descanso, escala do corpo) das peças de roupa: TR_Top, TR_Bottom..."""
    arm = bpy.data.objects[RIG]
    arm.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    s = rig_joints()
    dg = bpy.context.evaluated_depsgraph_get()
    col = bpy.data.collections.get('TR_work') or bpy.data.collections.new('TR_work')
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    out = []
    for o in [o for o in bpy.data.objects if o.type == 'MESH' and o.data.materials and o.parent is not None]:
        m = o.data.materials[0].name
        o.hide_set(True)
        if m not in PIECES and m != 'Bodymat_1':
            continue
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
        me.transform(Matrix.Scale(s, 4) @ o.matrix_world)
        if m == 'Bodymat_1':            # pele do modelo: só serve para medir a cabeça
            a = np.empty(len(me.vertices) * 3); me.vertices.foreach_get('co', a); a = a.reshape(-1, 3)
            hd = a[a[:, 2] > SJ['SL'][2] + 0.09]
            globals()['SRC_HEAD'] = (hd.min(0), hd.max(0))
            bpy.data.meshes.remove(me)
            continue
        c = bpy.data.objects.new('TR_' + PIECES[m], me)
        col.objects.link(c)
        c['src'] = o.name
        c['kind'] = {'Hat': 'Helm', 'Gloves': 'Hand', 'Shoes': 'Boots'}.get(PIECES[m], 'Torso')
        out.append(c)
    return out


def tex_alias(mat, metal=0.0, rough=0.8):
    """As texturas vêm como diffuse/normal/specularGlossiness: dá os nomes que o write_maps procura."""
    nt = mat.node_tree
    for nd in nt.nodes:
        if nd.type == 'TEX_IMAGE' and nd.image:
            n = nd.image.name
            if 'diffuse' in n and 'baseColor' not in n:
                nd.image.name = n.replace('diffuse', 'baseColor')
    name = 'KZ_%s_metallicRoughness' % mat.name
    if not any(nd.type == 'TEX_IMAGE' and nd.image and 'metallicRoughness' in nd.image.name for nd in nt.nodes):
        img = bpy.data.images.get(name) or bpy.data.images.new(name, 4, 4, alpha=False)
        img.pixels.foreach_set([1.0, rough, metal, 1.0] * 16)
        nd = nt.nodes.new('ShaderNodeTexImage'); nd.image = img


def cls_bottom(mn, mx):
    """Calça (pernas abaixo do quadril, um lado só) x saiote/faixa (tronco)."""
    one = mn[0] * mx[0] > 0 or min(abs(mn[0]), abs(mx[0])) < 0.01
    if mx[2] < 0.93 and one and mn[2] < 0.7:
        return 'PantsL' if mn[0] + mx[0] > 0 else 'PantsR'
    return 'Rest'


def fit_hat(helm, margin=0.0):
    """Leva o elmo junto com a cabeça do modelo para a cabeça do corpo (mesma relação, escala pela largura)."""
    wb = bpy.data.objects[WB]; gi = wb.vertex_groups['Head'].index; mw = wb.matrix_world
    head = np.array([tuple(mw @ v.co) for v in wb.data.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)])
    dmn, dmx = head.min(0), head.max(0)
    smn, smx = SRC_HEAD
    k = (dmx[0] - dmn[0]) / (smx[0] - smn[0]) * (1 + margin)
    sc = Vector(((smn[0] + smx[0]) / 2, (smn[1] + smx[1]) / 2, smx[2]))
    tc = Vector(((dmn[0] + dmx[0]) / 2, (dmn[1] + dmx[1]) / 2, dmx[2]))
    helm.data.transform(Matrix.Translation(tc) @ Matrix.Scale(k, 4) @ Matrix.Translation(-sc))
    return k


def categorize(K, objs):
    for x in [x for x in bpy.data.objects if x.name.startswith('TA_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    P = {}
    for o in objs:
        k = o.name[3:]
        if k == 'Bottom':
            for g, c in split_comps(o, 'TA_', cls_bottom).items():
                c.select_set(False)
                P['Skirt' if g == 'Rest' else g] = c
                if g == 'Rest':
                    c.name = 'TA_Skirt'
            continue
        c = K['_dup'](o, 'TA_' + {'Hat': 'Helm', 'Top': 'Top', 'Gloves': 'Gloves', 'Shoes': 'Boots'}[k])
        P[c.name[3:]] = c
    for o in P.values():
        for cc in o.users_collection:
            cc.objects.unlink(o)
        bpy.data.collections['TR_work'].objects.link(o)
    return P


def settle(P, gaps=None):
    G = globals()
    G['BVH'] = body_tree((BODY,), no_arms=True)
    for k in ('Top', 'Skirt', 'PantsL', 'PantsR'):
        if k in P:
            push_out(P[k], 0.010 if k == 'Top' else 0.012)
    G['BVH'] = body_tree((BODY,))
    for k in ('Gloves', 'Boots'):
        push_out(P[k], 0.006)
    G['BVH'] = body_tree((HEAD,))
    push_out(P['Helm'], 0.008)


def build_parts_igneo(K, P):
    for x in [x for x in bpy.data.objects if x.name.startswith('KZS_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    M = lambda k: P[k].data.materials[0]
    for k, o in P.items():
        tex_alias(M(k), *((0.8, 0.35) if k == 'Helm' else (0.0, 0.8)))

    def dd(k, name, r):
        o = K['_dup'](P[k], name); decimate(o, r); return o
    out = {'Elmo': ([dd('Helm', 'KZS_Helm', DECIMATE['Helm'])], [M('Helm')], 'Elmo'),
           'Armadura': ([dd('Top', 'KZS_Top', DECIMATE['Armor']), dd('Skirt', 'KZS_Skirt', DECIMATE['Armor'])],
                        [M('Top'), M('Skirt')], 'Armadura')}
    gl = dd('Gloves', 'KZS_GlovesAll', DECIMATE['Gloves'])
    bo = dd('Boots', 'KZS_BootsAll', DECIMATE['Boots'])
    for side, sg in (('L', 1), ('R', -1)):
        out['Luva' + side] = ([side_faces(gl, 'KZS_Glove' + side, sg)], [M('Gloves')], 'Luva')
        if 'Pants' + side in P:
            out['Calca' + side] = ([dd('Pants' + side, 'KZS_Pants' + side, DECIMATE['Pants'])], [M('Pants' + side)], 'Calca')
        out['Bota' + side] = ([side_faces(bo, 'KZS_Bota' + side, sg)], [M('Boots')], 'Bota')
    for o in (gl, bo):
        bpy.data.objects.remove(o, do_unlink=True)
    return out


def pivots():
    g = lambda n: tuple(bpy.data.objects[n].matrix_world.translation)
    return {'Armadura': (0.0, 0.0, 0.0), 'Elmo': g(HEAD), 'LuvaL': g('FB_ArmL'), 'LuvaR': g('FB_ArmR'),
            'CalcaL': g('FB_LegL'), 'CalcaR': g('FB_LegR'), 'BotaL': g('FB_LegL'), 'BotaR': g('FB_LegR')}


def fit_igneo(K, helm_margin=0.08, debug=False):
    K['decode_body'](bpy.data.objects[BODY], WB)
    bpy.data.objects[WB].hide_set(True)
    objs = prep()
    helm = [o for o in objs if kind_of(o) == 'Helm'][0]
    repose([o for o in objs if o is not helm], debug=debug)
    kh = fit_hat(helm, margin=helm_margin)
    P = categorize(K, objs)
    settle(P)
    for o in objs:
        o.hide_set(True)
    globals()['P'] = P
    return P, kh


def run_igneo(K, export=True, **fit):
    P, kh = fit_igneo(K, **fit)
    K['ALLOWED'].update({'Armadura': ('Body', 'Chest', 'LegL', 'LegR'),
                         'CalcaL': ('LegL', 'ShinL', 'FootL', 'Body'), 'CalcaR': ('LegR', 'ShinR', 'FootR', 'Body')})
    K['WEIGHT_MIX'].update({'Armadura': skirt_mix, 'CalcaL': pants_mix('L'), 'CalcaR': pants_mix('R')})
    K['HIDE_ZMIN'].update({'Armadura': 0.5})
    parts = build_parts_igneo(K, P)
    if not export:
        return kh, parts
    wb = bpy.data.objects[WB]
    ex = K['export_parts'](SET, parts, wb, pivots(), textures=True)
    hide = K['write_hide'](SET, MODEL, parts, [bpy.data.objects[BODY], bpy.data.objects[HEAD]])
    hide['Armadura'] = merge_hide(K, 'Armadura', enclosed_hide(K, parts, 'Armadura', 0.55, 1.42))
    return kh, ex, hide
