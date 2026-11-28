# Kezor — fluxo genérico para vestir uma armadura do Sketchfab (pose A ou T, uma ou várias malhas) num corpo de classe.
# 1) prep: cópias no mundo, giro para olhar -Y, no chão e na altura do corpo.
# 2) auto_joints: juntas da roupa (ombro/cotovelo/pulso/mão, quadril/joelho/tornozelo/ponta do pé) estimadas da malha;
#    juntas do corpo lidas dos pesos do jogo. Dá para corrigir à mão (SJ_FIX).
# 3) repose: skinning por distância às juntas (do kezor_trama_fit) e osso dominante de cada vértice.
# 4) split_parts: cada face vai para Elmo / Armadura / Luva / Calça / Bota pelo osso dominante.
# 5) settle, build_parts, export, pele escondida (write_hide + enclosed_hide + poke).
# Uso (Blender): E = {}; for f in ('kezor_veu_fit.py','kezor_bastiao_fit.py','kezor_escamas_fit.py','kezor_trama_fit.py',
#                  'kezor_hide_poke.py','kezor_auto_set.py'): exec(open(D + f).read(), E)
#   K = {}; exec(open(D + 'kezor_sketchfab_set.py').read(), K)
#   E['setup']('GR_', 'GuardiaoRunicoBase', 'Guarda'); E['run_auto'](K, objs=[...], rot_z=180)
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

PRE, MODEL, SET = 'GR_', 'GuardiaoRunicoBase', 'Novo'
BODY, HEAD, WB = PRE + 'Body', PRE + 'Head', 'KZB_PesosAuto'
SJ, TJ, BONES, TWIST = {}, {}, {}, {}
SPINE_R = 0.16
SPINE = {'sx': 1.0, 'sy': 1.0}
SJ_FIX = {}
DECIMATE = {'Elmo': 1.0, 'Armadura': 1.0, 'Luva': 1.0, 'Calca': 1.0, 'Bota': 1.0}
MAX_FACES = {'Elmo': 2500, 'Armadura': 9000, 'Luva': 3000, 'Calca': 2500, 'Bota': 2500}


def setup(pre, model, set_name):
    G = globals()
    G['PRE'], G['MODEL'], G['SET'] = pre, model, set_name
    G['BODY'], G['HEAD'] = pre + 'Body', pre + 'Head'
    G['SJ_FIX'] = {}


def kind_of(o):
    return 'Torso'


def spine_map(p):
    q = p.copy()
    q[:, 0] *= SPINE['sx']
    q[:, 1] = -0.027 + (p[:, 1] + 0.027) * SPINE['sy']
    return q


# ---------------------------------------------------------------- 1) cópias
def prep(objs, rot_z=0.0, height=None, keep=None):
    col = bpy.data.collections.get('TR_work') or bpy.data.collections.new('TR_work')
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    dg = bpy.context.evaluated_depsgraph_get()
    out = []
    for i, o in enumerate(objs):
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
        me.transform(o.matrix_world)
        c = bpy.data.objects.new('TR_s%02d' % i, me)
        col.objects.link(c)
        c['src'] = o.name
        o.hide_set(True)
        out.append(c)
    R = Matrix.Rotation(math.radians(rot_z), 4, 'Z')
    for c in out:
        c.data.transform(R)
    allp = np.vstack([_np(c) for c in out])
    zmin, zmax = allp[:, 2].min(), allp[:, 2].max()
    cx = (allp[:, 0].min() + allp[:, 0].max()) / 2
    body = np.array([tuple(p) for p in body_pts((BODY, HEAD))])
    h = height or (body[:, 2].max() - body[:, 2].min() + 0.04)
    s = h / (zmax - zmin)
    M = Matrix.Scale(s, 4) @ Matrix.Translation((-cx, 0, -zmin))
    for c in out:
        c.data.transform(M)
    # alinha o tronco em y com o do corpo
    allp = np.vstack([_np(c) for c in out])
    tor = allp[(allp[:, 2] > 1.1) & (allp[:, 2] < 1.35) & (abs(allp[:, 0]) < 0.12)]
    bt = body[(body[:, 2] > 1.1) & (body[:, 2] < 1.35) & (abs(body[:, 0]) < 0.12)]
    dy = (bt[:, 1].min() + bt[:, 1].max()) / 2 - (tor[:, 1].min() + tor[:, 1].max()) / 2
    for c in out:
        c.data.transform(Matrix.Translation((0, dy, 0)))
    return out, s


def _np(o):
    a = np.empty(len(o.data.vertices) * 3); o.data.vertices.foreach_get('co', a); return a.reshape(-1, 3)


def _set(o, a):
    o.data.vertices.foreach_set('co', a.reshape(-1)); o.data.update()


# ---------------------------------------------------------------- 2) juntas
def body_joints(K):
    K['decode_body'](bpy.data.objects[BODY], WB)
    wb = bpy.data.objects[WB]; wb.hide_set(True)
    mw = wb.matrix_world
    gid = {g.name: g.index for g in wb.vertex_groups}
    co = np.array([tuple(mw @ v.co) for v in wb.data.vertices])
    W = {}
    for v in wb.data.vertices:
        for g in v.groups:
            W.setdefault(g.group, {})[v.index] = g.weight

    def joint(a, b):
        A = W.get(gid[a], {}); B = W.get(gid[b], {})
        ids = [i for i in A if i in B and A[i] > 0.2 and B[i] > 0.2]
        return co[ids].mean(0)

    def grp(a, th=0.6):
        A = W.get(gid[a], {}); return co[[i for i, w in A.items() if w > th]]
    T = {}
    for s, sg in (('L', 1), ('R', -1)):
        arm = tuple(bpy.data.objects[PRE + 'Arm' + s].matrix_world.translation)
        leg = tuple(bpy.data.objects[PRE + 'Leg' + s].matrix_world.translation)
        hand, foot = grp('Hand' + s), grp('Foot' + s)
        T['S' + s] = arm
        T['E' + s] = tuple(joint('Arm' + s, 'ForeArm' + s))
        T['W' + s] = tuple(joint('ForeArm' + s, 'Hand' + s))
        T['T' + s] = (hand[:, 0].mean(), hand[:, 1].mean(), hand[:, 2].min() + 0.03)
        T['H' + s] = leg
        T['K' + s] = tuple(joint('Leg' + s, 'Shin' + s))
        T['A' + s] = tuple(joint('Shin' + s, 'Foot' + s))
        T['F' + s] = (foot[:, 0].mean(), foot[:, 1].min() + 0.01, 0.01)
    return T


def auto_joints(objs):
    """Juntas da roupa estimadas da malha (pose A/T simétrica, frente -Y)."""
    p = np.vstack([_np(o) for o in objs])
    body = np.array([tuple(q) for q in body_pts((BODY,))])
    J = {}
    zsh = TJ['SL'][2]
    for s, sg in (('L', 1), ('R', -1)):
        q = p[p[:, 0] * sg > 0]
        # braço: tudo além do ombro do corpo, acima da cintura
        xs = abs(TJ['S' + s][0]) + 0.02
        arm = q[(abs(q[:, 0]) > xs) & (q[:, 2] > 0.75)]
        c = arm.mean(0); d = np.linalg.svd(arm - c, full_matrices=False)[2][0]
        if d[0] * sg < 0:
            d = -d
        t = (arm - c) @ d
        tip = c + d * t.max()
        sh = np.array([sg * (abs(TJ['S' + s][0])), TJ['S' + s][1], zsh])
        # ombro na reta do braço, na largura do ombro do corpo
        if abs(d[2]) < 0.6:                                  # pose T/A aberta: ombro na reta do braço
            k = (sh[0] - c[0]) / d[0] if abs(d[0]) > 1e-3 else 0
            sh = c + d * k
        else:                                                # braços caídos: ombro do corpo
            sh = np.array(TJ['S' + s]) + np.array([sg * 0.005, 0, 0.01])
        L = np.linalg.norm(tip - sh)
        u = (tip - sh) / L
        J['S' + s] = tuple(sh)
        J['E' + s] = tuple(sh + u * L * 0.42)
        J['W' + s] = tuple(sh + u * L * 0.80)
        J['T' + s] = tuple(sh + u * L * 0.97)
        # perna: centro da fatia em cada altura
        def cen(z):
            r = q[(abs(q[:, 2] - z) < 0.02) & (abs(q[:, 0]) < 0.32) & (abs(q[:, 0]) > 0.02)]
            if not len(r):
                return np.array([sg * 0.1, 0, z])
            return np.array([(r[:, 0].min() + r[:, 0].max()) / 2, (r[:, 1].min() + r[:, 1].max()) / 2, z])
        J['H' + s] = tuple(np.array([sg * abs(TJ['H' + s][0]), TJ['H' + s][1], TJ['H' + s][2]]))
        J['K' + s] = tuple(cen(TJ['K' + s][2]))
        a = cen(TJ['A' + s][2]); J['A' + s] = tuple(a)
        f = q[(q[:, 2] < 0.05) & (abs(q[:, 0] - a[0]) < 0.12)]
        J['F' + s] = (a[0], f[:, 1].min() + 0.01 if len(f) else a[1] - 0.2, 0.01)
    J.update(SJ_FIX)
    return J


def make_bones():
    B = {}
    for s in 'LR':
        B.update({'UpArm' + s: ('S' + s, 'E' + s, 0.07, 1.0), 'ForeArm' + s: ('E' + s, 'W' + s, 0.06, 1.0),
                  'Hand' + s: ('W' + s, 'T' + s, 0.05, 1.0),
                  'Thigh' + s: ('H' + s, 'K' + s, 0.09, 1.0), 'Shin' + s: ('K' + s, 'A' + s, 0.07, 1.0),
                  'Foot' + s: ('A' + s, 'F' + s, 0.06, 1.0)})
    return B


# ---------------------------------------------------------------- 3) repose com osso dominante
LABEL = {'UpArm': 1, 'ForeArm': 2, 'Hand': 3, 'Thigh': 4, 'Shin': 5, 'Foot': 6, 'Spine': 0}


def repose_auto(objs, sharp=2.0):
    XF = {b: bone_xf(b) for b in BONES}
    for o in objs:
        p = _np(o)
        names, outs, ws = [], [], []
        for b in BONES:
            a0, d0, L0, a1, Rm, st = XF[b]
            rel = p - a0
            t = rel @ d0
            perp = rel - t[:, None] * d0
            outs.append(a1 + (t[:, None] * d0 * st + perp * BONES[b][3]) @ Rm.T)
            ws.append(np.exp(-sharp * (seg_dist(p, a0, d0, L0) / BONES[b][2]) ** 2))
            names.append(b)
        outs.append(spine_map(p))
        zc = np.clip(p[:, 2], 0.95, 1.5)
        d = np.sqrt(p[:, 0] ** 2 + (p[:, 1] + 0.025) ** 2 + (p[:, 2] - zc) ** 2)
        ws.append(np.exp(-sharp * (d / SPINE_R) ** 2) + 1e-6)
        names.append('Spine')
        W = np.array(ws); W /= W.sum(0) + 1e-12
        _set(o, sum(W[i][:, None] * outs[i] for i in range(len(outs))))
        dom = W.argmax(0)
        lab = np.array([LABEL[names[i].rstrip('LR')] * (1 if names[i].endswith('L') or names[i] == 'Spine' else -1) for i in dom],
                       dtype=np.int32)
        at = o.data.attributes.get('kz_bone') or o.data.attributes.new('kz_bone', 'INT', 'POINT')
        at.data.foreach_set('value', lab)


# ---------------------------------------------------------------- 4) peças por osso dominante
def split_parts(objs, head_z=None, sleeve_to='Luva', boot_z=None):
    """Elmo: acima do pescoço; Luva: braço (manga junto, ombreira fica na armadura); Calça: coxa; Bota: canela/pé."""
    hz = head_z or (bpy.data.objects[HEAD].matrix_world.translation.z + 0.0)
    out = {k: [] for k in ('Elmo', 'Armadura', 'LuvaL', 'LuvaR', 'CalcaL', 'CalcaR', 'BotaL', 'BotaR')}
    for o in objs:
        lab = np.empty(len(o.data.vertices), np.int32); o.data.attributes['kz_bone'].data.foreach_get('value', lab)
        co = _np(o)
        part = {}
        for f in o.data.polygons:
            vs = list(f.vertices)
            l = np.bincount(np.abs(lab[vs]) * 2 + (lab[vs] < 0)).argmax()
            b, neg = l // 2, l % 2
            c = co[vs].mean(0)
            side = 'R' if neg or (b == 0 and c[0] < 0) else 'L'
            if b == 0:
                k = 'Elmo' if c[2] > hz and abs(c[0]) < 0.16 else 'Armadura'
            elif b == 1:
                k = 'Armadura' if c[2] > TJ['S' + side][2] - 0.06 else sleeve_to + side if sleeve_to != 'Armadura' else 'Armadura'
            elif b in (2, 3):
                k = 'Luva' + side
            elif b == 4:
                k = 'Calca' + side
            else:
                k = 'Bota' + side
            if boot_z is not None and k.startswith('Calca') and c[2] < boot_z:
                k = 'Bota' + side
            part.setdefault(k, []).append(f.index)
        for k, idx in part.items():
            c = o.copy(); c.data = o.data.copy(); bpy.data.collections['TR_work'].objects.link(c)
            c.name = 'TA_%s_%s' % (k, o.name)
            keep = set(idx)
            bm = bmesh.new(); bm.from_mesh(c.data); bm.faces.ensure_lookup_table()
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context='FACES')
            bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
            bm.to_mesh(c.data); bm.free()
            out[k].append(c)
        o.hide_set(True)
    return out


def head_pts(th=0.5):
    """Pele da cabeça: vértices do corpo com peso de Head (o objeto Head do corpo MPFB é só olhos)."""
    wb = bpy.data.objects[WB]; mw = wb.matrix_world
    gi = wb.vertex_groups['Head'].index
    p = [tuple(mw @ v.co) for v in wb.data.vertices if any(g.group == gi and g.weight > th for g in v.groups)]
    p += [tuple(q) for q in body_pts((HEAD,))]
    return np.array(p)


def fit_helm_auto(parts, margin=0.015, depth_margin=0.02):
    """Elmo cobre a cabeça: só cresce (nunca encolhe), topo e centro na cabeça."""
    hs = parts.get('Elmo') or []
    if not hs:
        return 1.0
    head = head_pts()
    hp = np.vstack([_np(o) for o in hs])
    top = head[:, 2].max()
    hd = head[head[:, 2] > top - 0.2]                      # parte de cima da cabeça (sem pescoço)
    k = max(1.0, (np.ptp(hd[:, 0]) + 2 * margin) / np.ptp(hp[:, 0]),
            (np.ptp(hd[:, 1]) + 2 * depth_margin) / np.ptp(hp[:, 1]))
    hc = Vector(((hp[:, 0].min() + hp[:, 0].max()) / 2, (hp[:, 1].min() + hp[:, 1].max()) / 2, hp[:, 2].max()))
    tc = Vector(((hd[:, 0].min() + hd[:, 0].max()) / 2, (hd[:, 1].min() + hd[:, 1].max()) / 2,
                 max(hp[:, 2].max(), top + margin)))
    M = Matrix.Translation(tc) @ Matrix.Scale(k, 4) @ Matrix.Translation(-hc)
    for o in hs:
        o.data.transform(M)
    return k


def settle_auto(parts):
    G = globals()
    G['BVH'] = body_tree((BODY,), no_arms=True)
    for k in ('Armadura', 'CalcaL', 'CalcaR'):
        for o in parts[k]:
            push_out(o, 0.012 if k == 'Armadura' else 0.010)
    G['BVH'] = body_tree((BODY,))
    for k in ('LuvaL', 'LuvaR', 'BotaL', 'BotaR'):
        for o in parts[k]:
            push_out(o, 0.007)
    G['BVH'] = body_tree((BODY, HEAD))
    for o in parts['Elmo']:
        push_out(o, 0.008)


def _faces(objs):
    return sum(len(o.data.polygons) for o in objs)


def build_parts_auto(K, parts):
    """Junta por material dentro de cada peça (até 4 materiais por atlas) e reduz até o limite de faces."""
    for x in [x for x in bpy.data.objects if x.name.startswith('KZS_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    out = {}
    for piece, objs in parts.items():
        if not objs:
            continue
        bymat = {}
        for o in objs:
            for i, m in enumerate(o.data.materials):
                if m is None:
                    continue
                c = K['_dup'](o, 'KZS_tmp')
                bm = bmesh.new(); bm.from_mesh(c.data)
                bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index != i], context='FACES')
                bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
                bm.to_mesh(c.data); bm.free()
                if not len(c.data.polygons):
                    bpy.data.objects.remove(c, do_unlink=True); continue
                c.data.materials.clear(); c.data.materials.append(m)
                bymat.setdefault(m.name, []).append(c)
        mats = sorted(bymat, key=lambda n: -_faces(bymat[n]))
        if len(mats) > 4:                                    # materiais pequenos vão para o maior
            for n in mats[4:]:
                for c in bymat[n]:
                    c.data.materials.clear(); c.data.materials.append(bpy.data.materials[mats[0]])
                    bymat[mats[0]].append(c)
            mats = mats[:4]
        ms = []
        for n in mats:
            j = K['_join'](bymat[n], 'KZS_%s_%d' % (piece, len(ms))) if len(bymat[n]) > 1 else bymat[n][0]
            j.name = 'KZS_%s_%d' % (piece, len(ms))
            ms.append(j)
        tot = _faces(ms)
        lim = MAX_FACES[piece.rstrip('LR')] * (0.5 if piece[-1] in 'LR' else 1.0)
        if tot > lim:
            for j in ms:
                decimate(j, max(0.1, lim / tot))
        out[piece] = (ms, [j.data.materials[0] for j in ms], piece.rstrip('LR'))
    return out


def ensure_maps(mats):
    """Materiais sem metal/rugosidade ou normal ganham imagens neutras; diffuse vira baseColor."""
    for m in mats:
        nt = m.node_tree
        if not nt:
            continue
        for nd in nt.nodes:
            if nd.type == 'TEX_IMAGE' and nd.image and 'diffuse' in nd.image.name.lower() and 'baseColor' not in nd.image.name:
                nd.image.name = nd.image.name.replace('diffuse', 'baseColor').replace('Diffuse', 'baseColor')
        have = lambda key: any(nd.type == 'TEX_IMAGE' and nd.image and key in nd.image.name for nd in nt.nodes)
        if not have('baseColor'):
            imgs = [nd.image for nd in nt.nodes if nd.type == 'TEX_IMAGE' and nd.image]
            col = None
            for nd in nt.nodes:
                if nd.type == 'BSDF_PRINCIPLED':
                    col = tuple(nd.inputs['Base Color'].default_value)
            if imgs:
                imgs[0].name = 'KZ_%s_baseColor' % m.name
            else:
                img = bpy.data.images.new('KZ_%s_baseColor' % m.name, 4, 4, alpha=False)
                img.pixels.foreach_set(list(col or (0.5, 0.5, 0.5, 1)) * 16)
                nt.nodes.new('ShaderNodeTexImage').image = img
        for key, rgba in (('normal', (0.5, 0.5, 1.0, 1.0)), ('metallicRoughness', (1.0, 0.5, 0.6, 1.0))):
            if not have(key):
                img = bpy.data.images.new('KZ_%s_%s' % (m.name, key), 4, 4, alpha=False)
                img.pixels.foreach_set(list(rgba) * 16)
                nt.nodes.new('ShaderNodeTexImage').image = img
        try:
            m.blend_method = 'OPAQUE'
        except Exception:
            pass


def pivots_auto():
    g = lambda n: tuple(bpy.data.objects[PRE + n].matrix_world.translation)
    return {'Armadura': (0.0, 0.0, 0.0), 'Elmo': g('Head'), 'LuvaL': g('ArmL'), 'LuvaR': g('ArmR'),
            'CalcaL': g('LegL'), 'CalcaR': g('LegR'), 'BotaL': g('LegL'), 'BotaR': g('LegR')}


def fit_auto(K, objs, rot_z=0.0, height=None, debug=False):
    G = globals()
    G['TJ'] = body_joints(K)
    work, s = prep(objs, rot_z, height)
    G['SJ'] = auto_joints(work)
    G['BONES'] = make_bones()
    repose_auto(work)
    parts = split_parts(work)
    G['HELM_K'] = fit_helm_auto(parts)
    settle_auto(parts)
    G['PARTS'] = parts
    return parts, s


def run_auto(K, objs, rot_z=0.0, height=None, export=True, closed_helm=False):
    parts, s = fit_auto(K, objs, rot_z, height)
    K['ALLOWED'].update({'Armadura': ('Body', 'Chest', 'LegL', 'LegR'),
                         'CalcaL': ('LegL', 'ShinL', 'FootL', 'Body'), 'CalcaR': ('LegR', 'ShinR', 'FootR', 'Body')})
    K['WEIGHT_MIX'].update({'Armadura': skirt_mix, 'CalcaL': pants_mix('L'), 'CalcaR': pants_mix('R')})
    K['HIDE_ZMIN'].update({'Armadura': 0.5})
    ensure_maps({m for objs_ in parts.values() for o in objs_ for m in o.data.materials if m})
    built = build_parts_auto(K, parts)
    if not export:
        return built
    ex = K['export_parts'](SET, built, bpy.data.objects[WB], pivots_auto(), textures=True)
    hide = K['write_hide'](SET, MODEL, built, [bpy.data.objects[BODY], bpy.data.objects[HEAD]])
    G = globals(); G['WB'] = WB
    if 'Armadura' in built:
        hide['Armadura'] = merge_hide(K, 'Armadura', enclosed_hide(K, built, 'Armadura', 0.55, 1.42))
    for piece, (ms, _, _) in built.items():
        if piece == 'Elmo':
            merge(SET, piece, MODEL, poke_points([bpy.data.objects[BODY], bpy.data.objects[HEAD]], ms, 0.08, zmin=1.3))
        else:
            merge(SET, piece, MODEL, poke_points([bpy.data.objects[BODY]], ms, 0.10))
        body = [bpy.data.objects[BODY], bpy.data.objects[HEAD]] if piece == 'Elmo' else [bpy.data.objects[BODY]]
        merge(SET, piece, MODEL, near_points(body, ms))
    clean_hide(SET, MODEL)
    fill_gaps(SET, MODEL)
    clean_hide(SET, MODEL)
    return s, ex, hide


def near_points(body_objs, piece_objs, dist=0.016, dot=0.3):
    """Pele quase colada na peça (mesma direção da superfície) briga com ela na tela: esconde."""
    tb = bmesh.new()
    for x in piece_objs:
        t = bmesh.new(); t.from_mesh(x.data); t.transform(x.matrix_world)
        me = bpy.data.meshes.new('kz_tmp'); t.to_mesh(me); t.free(); tb.from_mesh(me); bpy.data.meshes.remove(me)
    tree = BVHTree.FromBMesh(tb); tb.free()
    pts = []
    for b in body_objs:
        bm = bmesh.new(); bm.from_mesh(b.data); bm.transform(b.matrix_world); bm.normal_update()
        for v in bm.verts:
            loc, nor, _, d = tree.find_nearest(v.co, dist)
            if loc is not None and abs(nor.dot(v.normal)) > dot:
                pts.append(tuple(int(round(c * 1000)) for c in v.co))
        bm.free()
    return pts


# ---------------------------------------------------------------- 6) limpeza das listas: nada de mão na bota
FORBID = {'Bota': ('Arm', 'ForeArm', 'Hand', 'Head'), 'Calca': ('Arm', 'ForeArm', 'Hand', 'Head'),
          'Luva': ('Leg', 'Shin', 'Foot', 'Head'), 'Armadura': ('Hand', 'Shin', 'Foot'), 'Elmo': ('Hand', 'Leg', 'Shin', 'Foot')}


def clean_hide(set_name, model, wb_name=None):
    from mathutils.kdtree import KDTree
    wb = bpy.data.objects[wb_name or WB]; mw = wb.matrix_world
    names = {g.index: g.name for g in wb.vertex_groups}
    kd = KDTree(len(wb.data.vertices)); dom = []
    for v in wb.data.vertices:
        kd.insert(mw @ v.co, v.index)
        dom.append(names[max(v.groups, key=lambda g: g.weight).group].rstrip('LR') if v.groups else '')
    kd.balance()
    out = {}
    for path in [os.path.join(K_ROOT, 'Equip', 'Hide', f) for f in os.listdir(os.path.join(K_ROOT, 'Equip', 'Hide'))
                 if f.startswith(set_name + '_') and f.endswith('_%s.txt' % model)]:
        piece = os.path.basename(path)[len(set_name) + 1:-len(model) - 5].rstrip('LR')
        bad = FORBID.get(piece, ())
        pts = [tuple(int(x) for x in l.split()) for l in open(path, encoding='utf-8').read().split('\n') if l.strip()]
        keep = [p for p in pts if dom[kd.find(Vector(p) / 1000.0)[1]] not in bad]
        with open(path, 'w', encoding='utf-8') as f:
            f.write('\n'.join('%d %d %d' % p for p in keep))
        out[os.path.basename(path)] = (len(pts), len(keep))
    return out


def fill_gaps(set_name, model, frac=0.6, rounds=2):
    """Pele solta no meio de pele escondida (sobra de triângulo que aparece como risco branco) vai para a peça vizinha."""
    import collections
    d = os.path.join(K_ROOT, 'Equip', 'Hide')
    lists = {}
    for f in os.listdir(d):
        if f.startswith(set_name + '_') and f.endswith('_%s.txt' % model):
            piece = f[len(set_name) + 1:-len(model) - 5]
            lists[piece] = set(tuple(int(x) for x in l.split()) for l in open(os.path.join(d, f), encoding='utf-8').read().split('\n') if l.strip())
    owner = {}
    for piece, pts in lists.items():
        for q in pts:
            owner.setdefault(q, piece)
    added = collections.Counter()
    for name in (BODY, HEAD):
        o = bpy.data.objects[name]
        bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
        key = {v: tuple(int(round(c * 1000)) for c in v.co) for v in bm.verts}
        for _ in range(rounds):
            new = {}
            for v in bm.verts:
                if key[v] in owner:
                    continue
                nb = [key[e.other_vert(v)] for e in v.link_edges]
                own = [owner[k] for k in nb if k in owner]
                if nb and len(own) >= frac * len(nb):
                    new[key[v]] = collections.Counter(own).most_common(1)[0][0]
            owner.update(new)
            for k, piece in new.items():
                lists[piece].add(k); added[piece] += 1
        bm.free()
    for piece, pts in lists.items():
        with open(os.path.join(d, '%s_%s_%s.txt' % (set_name, piece, model)), 'w', encoding='utf-8') as f:
            f.write('\n'.join('%d %d %d' % q for q in sorted(pts)))
    return dict(added)


K_ROOT = r'C:\MuProtoUnity\Assets\Resources'


# ---------------------------------------------------------------- 7) atalhos de cena
def hide_preview(set_name=None):
    """Cópias PV_ do corpo/cabeça sem a pele que o jogo esconde com o set inteiro vestido."""
    import glob
    set_name = set_name or SET
    pts = set()
    for f in glob.glob(os.path.join(K_ROOT, 'Equip', 'Hide', '%s_*_%s.txt' % (set_name, MODEL))):
        pts |= set(tuple(int(x) for x in l.split()) for l in open(f, encoding='utf-8').read().split('\n') if l.strip())
    out = []
    for n in (BODY, HEAD):
        src = bpy.data.objects[n]
        if 'PV_' + n in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects['PV_' + n])
        me = src.data.copy(); o = bpy.data.objects.new('PV_' + n, me); bpy.context.scene.collection.objects.link(o)
        o.matrix_world = src.matrix_world
        bm = bmesh.new(); bm.from_mesh(me); mw = src.matrix_world
        hid = {v for v in bm.verts if tuple(int(round(c * 1000)) for c in (mw @ v.co)) in pts}
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if all(v in hid for v in f.verts)], context='FACES')
        bm.to_mesh(me); bm.free(); src.hide_set(True); out.append(o)
    return out


def rerun(K, set_name, src_names, rot_z=0.0, **kw):
    for o in list(bpy.data.objects):
        if o.name.startswith(('KZS_', 'TA_', 'TR_', 'PV_')):
            bpy.data.objects.remove(o, do_unlink=True)
    setup(PRE, MODEL, set_name)
    for n in (BODY, HEAD):
        bpy.data.objects[n].hide_set(False)
    srcs = [bpy.data.objects[n] for n in src_names]
    for o in srcs:
        o.hide_set(False)
    r = run_auto(K, srcs, rot_z, **kw)
    for o in bpy.data.objects:
        if o.name.startswith(('TA_', 'TR_')):
            o.hide_set(True)
    hide_preview(set_name)
    return r


# ---------------------------------------------------------------- 8) prévia como no jogo (FBX exportados + texturas + pele escondida)
PIECES_TEX = {'Elmo': 'Elmo', 'Armadura': 'Armadura', 'LuvaL': 'Luva', 'LuvaR': 'Luva', 'CalcaL': 'Calca', 'CalcaR': 'Calca',
              'BotaL': 'Bota', 'BotaR': 'Bota'}


def _tex_names(set_name, tex):
    # mesma ordem do Gear.MapNames: a própria, o par, e Pernas para calça/bota
    yield '%s_%s' % (set_name, tex)
    if tex in ('Calca', 'Bota'):
        yield '%s_Pernas' % set_name


def game_preview(set_name, dx=0.0, label=None):
    """Monta o set como o jogo monta: cada FBX no pivô da articulação, textura Base, corpo sem a pele escondida."""
    col = bpy.data.collections.get('PV_' + set_name) or bpy.data.collections.new('PV_' + set_name)
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    piv = pivots_auto()
    for piece, tex in PIECES_TEX.items():
        path = os.path.join(K_ROOT, 'Models', 'Equip', '%s_%s.fbx' % (set_name, piece))
        if not os.path.exists(path):
            continue
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=path)
        new = [o for o in bpy.data.objects if o not in before]
        img = None
        for n in _tex_names(set_name, tex):
            f = os.path.join(K_ROOT, 'Textures', 'Equip', n + '_Base.png')
            if os.path.exists(f):
                img = bpy.data.images.load(f, check_existing=True); break
        m = bpy.data.materials.get('PVM_%s_%s' % (set_name, tex))
        if m is None:
            m = bpy.data.materials.new('PVM_%s_%s' % (set_name, tex)); m.use_nodes = True
            if img:
                nt = m.node_tree; t = nt.nodes.new('ShaderNodeTexImage'); t.image = img
                bsdf = [nd for nd in nt.nodes if nd.type == 'BSDF_PRINCIPLED'][0]
                nt.links.new(t.outputs['Color'], bsdf.inputs['Base Color'])
                bsdf.inputs['Roughness'].default_value = 0.45
                bsdf.inputs['Metallic'].default_value = 0.35
        for o in new:
            for c in o.users_collection:
                c.objects.unlink(o)
            col.objects.link(o)
            if o.type == 'MESH':
                o.data.materials.clear(); o.data.materials.append(m)
                o.matrix_world = Matrix.Translation(Vector(piv[piece]) + Vector((dx, 0, 0))) @ o.matrix_world
            else:
                bpy.data.objects.remove(o, do_unlink=True)
    for o in hide_preview(set_name):
        o.name = 'PV_%s_%s' % (set_name, o.name[3:])
        for c in o.users_collection:
            c.objects.unlink(o)
        col.objects.link(o)
        o.matrix_world = Matrix.Translation((dx, 0, 0)) @ o.matrix_world
        o.hide_set(False)
    return col
