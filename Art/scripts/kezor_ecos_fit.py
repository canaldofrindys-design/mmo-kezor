# Kezor — veste o "Medieval Dress" (Sketchfab, A9908244, CC BY) no corpo da Invocadora dos Ecos.
# Uso (Blender, cena com EB_Body/EB_Head/EB_ArmL... da InvocadoraEcosBase e o vestido importado):
#   E = {}; exec(open(r'...\kezor_veu_fit.py').read(), E); exec(open(r'...\kezor_ecos_fit.py').read(), E)
#   K = {}; exec(open(r'...\kezor_sketchfab_set.py').read(), K); E['run_ecos'](K)
# Reaproveita do kezor_veu_fit: sm, wcopy, components, push_out, pull_in, decimate, keep_side.
import bpy, math, bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

BODY = 'EB_Body'
HEAD = 'EB_Head'
SRC_UNDER, SRC_OVER, SRC_TRIM, SRC_VEIL = 'Object_10', 'Object_12', 'Object_13', 'Object_8'
SRC_HAT = ('Object_4', 'Object_5', 'Object_6', 'Object_15')
SHOULDER_SRC = 1.36          # topo do ombro do vestido (importado com 1.75 m de altura)
HEM_Z = 0.06                 # barra da saia depois do ajuste
WAIST_Z = 1.05               # acima disso não estica
DECIMATE = {'ODress': 0.35, 'UPanel': 0.4, 'OSleeve': 0.5, 'USleeve': 0.6, 'Trim': 0.7,
            'Escof_top': 0.25, 'Escoff_main': 0.6, 'Escoff_bottom': 0.5, 'Veil': 0.6}


def _bbox(pts):
    return ([min(p[k] for p in pts) for k in range(3)], [max(p[k] for p in pts) for k in range(3)])


def split_by(src, prefix, classify):
    """Separa as peças soltas da malha (já no mundo) em grupos pela função classify(min, max)."""
    co = [v.co.copy() for v in src.data.vertices]
    groups = {}
    for comp in components(src.data):
        mn, mx = _bbox([co[i] for i in comp])
        groups.setdefault(classify(mn, mx), set()).update(comp)
    out = {}
    for g, keep in groups.items():
        o = src.copy(); o.data = src.data.copy()
        bpy.context.scene.collection.objects.link(o)
        old = bpy.data.objects.get(prefix + g)
        if old:
            bpy.data.objects.remove(old, do_unlink=True)
        o.name = prefix + g
        bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
        bm.to_mesh(o.data); bm.free()
        out[g] = o
    return out


def cls_under(mn, mx):
    return 'USleeve' if min(abs(mn[0]), abs(mx[0])) > 0.12 and mn[0] * mx[0] > 0 and mn[2] > 0.8 else 'UPanel'


def cls_over(mn, mx):
    return 'OSleeve' if min(abs(mn[0]), abs(mx[0])) > 0.13 and mn[0] * mx[0] > 0 and mn[2] > 0.4 and mx[2] > 1.3 else 'ODress'


def cls_trim(mn, mx):
    return 'TSleeve' if min(abs(mn[0]), abs(mx[0])) > 0.2 and mn[0] * mx[0] > 0 and mn[2] > 0.4 and mx[2] < 0.75 else 'Trim'


def body_tree(no_arms):
    """BVH do corpo; no_arms=True tira os braços (os braços pendurados não podem empurrar a saia)."""
    b = bpy.data.objects[BODY]
    bm = bmesh.new(); bm.from_mesh(b.data); bm.transform(b.matrix_world)
    if no_arms:
        wb = bpy.data.objects['KZB_PesosEcos']
        arm = {g.index for g in wb.vertex_groups if g.name.startswith(('Arm', 'ForeArm', 'Hand'))}
        bad = set()
        for v in wb.data.vertices:
            if sum(g.weight for g in v.groups if g.group in arm) > 0.5:
                bad.add(v.index)
        bm.faces.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if any(v.index in bad for v in f.verts)], context='FACES')
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    t = BVHTree.FromBMesh(bm); bm.free()
    return t


def fit_all(K, wrist_pad=0.0, sleeve_min_x=0.2, skirt_flare=0.16, bust_push=0.06):
    G = globals()
    wb = K['decode_body'](bpy.data.objects[BODY], 'KZB_PesosEcos')
    body = [bpy.data.objects[BODY].matrix_world @ v.co for v in bpy.data.objects[BODY].data.vertices]
    P = {}
    P.update(split_by(wcopy(bpy.data.objects[SRC_UNDER], 'EE_U'), 'EE_', cls_under))
    P.update(split_by(wcopy(bpy.data.objects[SRC_OVER], 'EE_O'), 'EE_', cls_over))
    P.update(split_by(wcopy(bpy.data.objects[SRC_TRIM], 'EE_T'), 'EE_', cls_trim))
    for n in ('EE_U', 'EE_O', 'EE_T'):
        bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    P['Veil'] = wcopy(bpy.data.objects[SRC_VEIL], 'EE_Veil')
    for n in SRC_HAT:
        o = bpy.data.objects[n]
        k = o.data.materials[0].name
        c = wcopy(o, 'EE_h_' + n)
        P[k] = K['_join']([P[k], c], 'EE_' + k) if k in P else c
        P[k].name = 'EE_' + k
    # 1) desloca: ombro do vestido no ombro do corpo; centro do tronco em y
    sh_body = max(p.z for p in body if 0.14 < abs(p.x) < 0.2)
    dz = sh_body - SHOULDER_SRC
    tor = [p for p in body if 1.15 < p.z < 1.4 and abs(p.x) < 0.13]
    by = (min(p.y for p in tor) + max(p.y for p in tor)) / 2
    dv = [v.co for k in ('ODress', 'UPanel') for v in P[k].data.vertices if 1.03 < v.co.z < 1.28 and abs(v.co.x) < 0.13]
    dy = by - (min(c.y for c in dv) + max(c.y for c in dv)) / 2
    for o in P.values():
        o.data.transform(Matrix.Translation((0, dy, dz)))
    # 2) saia: estica abaixo da cintura até a barra ficar em HEM_Z
    hem = min(v.co.z for k in ('ODress', 'UPanel') for v in P[k].data.vertices)
    ks = (WAIST_Z - HEM_Z) / (WAIST_Z - hem)
    for k in ('ODress', 'UPanel', 'Trim'):
        for v in P[k].data.vertices:
            if v.co.z < WAIST_Z:
                v.co.z = WAIST_Z - (WAIST_Z - v.co.z) * ks
        P[k].data.update()
    # 2b) a saia abre um pouco a partir do quadril (as mãos penduradas ficam por baixo) e o corpete
    #     avança no peito (o busto do corpo é maior que o do manequim do vestido)
    for k in ('ODress', 'UPanel', 'Trim'):
        for v in P[k].data.vertices:
            c = v.co
            f = 1 + skirt_flare * sm(1.24, 0.98, c.z)
            c.x *= f
            c.y = by + (c.y - by) * f
            if c.y < by:
                c.y -= bust_push * sm(1.10, 1.21, c.z) * sm(1.40, 1.31, c.z) * sm(0.19, 0.06, abs(c.x))
        P[k].data.update()
    # 3) mangas: o vestido tem os braços a ~45°. Acha o eixo da manga de baixo (centros das fatias) e leva
    #    esse eixo para o eixo do braço do corpo (ombro EB_ArmL -> pulso), girando, esticando e deslocando.
    us = [v.co.copy() for v in P['USleeve'].data.vertices if v.co.x > 0]
    top = max(c.z for c in us); far = max(c.x for c in us)
    a_ = Vector((sum(c.x for c in us if c.z > top - 0.04) / max(1, len([c for c in us if c.z > top - 0.04])), 0, top - 0.05))
    tip = [c for c in us if c.x > far - 0.04]
    w_ = Vector((sum(c.x for c in tip) / len(tip), 0, sum(c.z for c in tip) / len(tip)))
    d = (w_ - a_).normalized()
    bins = {}
    for c in us:
        t = (Vector((c.x, 0, c.z)) - a_).dot(d)
        bins.setdefault(int(t / 0.05), []).append(c)
    cen = {k: Vector((sum(c.x for c in v) / len(v), 0, sum(c.z for c in v) / len(v))) for k, v in bins.items() if len(v) > 8}
    ks_ = sorted(cen)
    S0, W0 = cen[ks_[1]], cen[ks_[-1]]
    d0 = (W0 - S0).normalized()
    S0 = S0 - d0 * 0.05 * 1.5                     # volta até o ombro (a fatia 1 começa a ~5 cm)
    hg = wb.vertex_groups['HandL'].index
    hand = [v.co for v in wb.data.vertices if any(g.group == hg and g.weight > 0.5 for g in v.groups)]
    hz = max(c.z for c in hand) + wrist_pad
    near = [c for c in hand if c.z > hz - wrist_pad - 0.03]
    B0 = bpy.data.objects['EB_ArmL'].matrix_world.translation.copy(); B0.y = 0
    Bw = Vector((sum(c.x for c in near) / len(near), 0, hz))
    a0 = math.atan2(-(W0 - S0).z, (W0 - S0).x)
    a1 = math.atan2(-(Bw - B0).z, (Bw - B0).x)
    s = (Bw - B0).length / (W0 - S0).length
    da = a1 - a0
    ca, sa = math.cos(-da), math.sin(-da)
    for k in ('USleeve', 'OSleeve', 'TSleeve'):
        me = P[k].data
        for v in me.vertices:
            c = v.co
            sg = 1 if c.x >= 0 else -1
            q = Vector((abs(c.x), 0, c.z)) - S0
            t = q.dot(d0)
            w = sm(-0.06, 0.04, t)
            if w <= 0:
                continue
            along = d0 * t; perp = q - along
            q2 = along * s + perp                   # estica só ao longo do braço
            r = Vector((q2.x * ca - q2.z * sa, 0, q2.x * sa + q2.z * ca))
            n = (S0 + q) * (1 - w) + (B0 + r) * w
            if k != 'USleeve' and n.z < B0.z - 0.12 and n.x < sleeve_min_x:
                n.x = sleeve_min_x + (n.x - sleeve_min_x) * 0.2
            c.x = sg * n.x
            c.z = n.z
        me.update()
    shoulder, wrist, bw = S0, W0, Bw
    # 4) nada por dentro do corpo (a saia ignora os braços)
    G['BVH'] = body_tree(True)
    pull_in(P['UPanel'], 0.010, 0.06, 0.4)
    G['BVH'] = body_tree(False)          # com os braços: a saia cobre as mãos penduradas
    for k, gap in (('UPanel', 0.010), ('ODress', 0.018), ('Trim', 0.020),
                   ('USleeve', 0.018), ('OSleeve', 0.028), ('TSleeve', 0.030), ('Veil', 0.02)):
        push_out(P[k], gap)
    G['P'] = P
    return {'dz': dz, 'dy': dy, 'ks': ks, 'shoulder': tuple(shoulder), 'wrist': tuple(wrist), 'body_wrist': tuple(bw),
            'a0': math.degrees(a0), 'a1': math.degrees(a1), 's': s}


# ================================================================ peças do jogo
def cull_covered(inner, outers, reach=0.035):
    """Apaga as faces da camada de baixo que ficam cobertas pela de cima (evita o pano de baixo furar o de cima)."""
    bm = bmesh.new()
    for o in outers:
        bm.from_mesh(o.data)
    t = BVHTree.FromBMesh(bm); bm.free()
    bm = bmesh.new(); bm.from_mesh(inner.data); bm.normal_update()
    kill = [f for f in bm.faces
            if any(t.ray_cast(f.calc_center_median() + d * 0.0005, d, reach)[0] is not None
                   for d in (f.normal, -f.normal))]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bm.to_mesh(inner.data); bm.free()
    return len(kill)


def build_parts_ecos(K, calca_z=0.9):
    P = globals()['P']
    for x in [x for x in bpy.data.objects if x.name.startswith('KZS_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    M = lambda k: P[k].data.materials[0]

    def dd(k, name, r=None):
        o = K['_dup'](P[k], name); decimate(o, DECIMATE.get(r or k)); return o

    def cut(src, name, below):
        """Só as faces abaixo (below=True) ou acima de calca_z."""
        o = K['_dup'](src, name)
        bm = bmesh.new(); bm.from_mesh(o.data)
        kill = [f for f in bm.faces if (f.calc_center_median().z >= calca_z) == below]
        bmesh.ops.delete(bm, geom=kill, context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        bm.to_mesh(o.data); bm.free()
        return o
    out = {}
    out['Elmo'] = ([dd(k, 'KZS_' + k) for k in ('Escoff_main', 'Escoff_bottom', 'Escof_top', 'Veil')],
                   [M(k) for k in ('Escoff_main', 'Escoff_bottom', 'Escof_top', 'Veil')], 'Elmo')
    up = dd('UPanel', 'KZS_UPanelAll')
    od, tr = dd('ODress', 'KZS_ODress'), dd('Trim', 'KZS_Trim')
    cull_covered(up, [od, tr])
    out['Armadura'] = ([od, tr, cut(up, 'KZS_UTop', False)], [M('ODress'), M('Trim'), M('UPanel')], 'Armadura')
    osl, tsl, usl = dd('OSleeve', 'KZS_OSl'), dd('TSleeve', 'KZS_TSl', 'Trim'), dd('USleeve', 'KZS_USl')
    cull_covered(usl, [osl, tsl])
    for side, sg in (('L', 1), ('R', -1)):
        out['Luva' + side] = ([keep_side(osl, 'KZS_OSl' + side, sg), keep_side(tsl, 'KZS_TSl' + side, sg),
                               keep_side(usl, 'KZS_USl' + side, sg)], [M('OSleeve'), M('TSleeve'), M('USleeve')], 'Luva')
        out['Calca' + side] = ([keep_side(cut(up, 'KZS_UBot' + side + '0', True), 'KZS_UBot' + side, sg)],
                               [M('UPanel')], 'Calca')
    for o in (up, osl, tsl, usl):
        bpy.data.objects.remove(o, do_unlink=True)
    return out


def veil_mix(co, w):
    """Escoffion e alto do véu seguem a cabeça; o véu que desce pelas costas passa para o peito."""
    f = sm(1.42, 1.2, co.z)
    return [('Head', 1 - f), ('Chest', f)]


def skirt_mix(co, w):
    """Saia longa: abaixo do quadril segue cada vez mais as coxas (não abre buraco ao andar)."""
    f = 0.75 * sm(0.95, 0.6, co.z)
    if f <= 0:
        return w
    wl = sm(-0.08, 0.08, co.x)
    base = [(b, x * (1 - f)) for b, x in w if b in ('Body', 'Chest')] or [('Body', 1 - f)]
    return base + [('LegL', f * wl), ('LegR', f * (1 - wl))]


def panel_mix(side):
    def f(co, w):
        g = 0.85 * sm(0.95, 0.5, co.z)
        base = [(b, x * (1 - g)) for b, x in w if b in ('Body', 'Chest')] or [('Body', 1 - g)]
        return base + [(side, g)]
    return f


def pivots_ecos():
    g = lambda n: tuple(bpy.data.objects[n].matrix_world.translation)
    return {'Armadura': (0.0, 0.0, 0.0), 'Elmo': g(HEAD), 'LuvaL': g('EB_ArmL'), 'LuvaR': g('EB_ArmR'),
            'CalcaL': g('EB_LegL'), 'CalcaR': g('EB_LegR')}


def enclosed_hide(K, parts, piece='Armadura', z0=0.6, z1=1.42, bones=None, min_hits=4):
    """Corpo dentro da roupa: o corpo tem cinto, busto e braços que atravessam o pano; esconde o que a peça
    cerca (min_hits de 8 raios horizontais batem). Armadura: tronco (sem braços/cabeça, abaixo de 0.86 tudo,
    menos o decote). Luvas: só os ossos em bones."""
    bm = bmesh.new()
    for o in parts[piece][0]:
        bm.from_mesh(o.data)
    t = BVHTree.FromBMesh(bm); bm.free()
    wb = bpy.data.objects['KZB_PesosEcos']
    gname = {g.index: g.name for g in wb.vertex_groups}
    dirs = [Vector((math.cos(a), math.sin(a), 0)) for a in [i * math.pi / 4 for i in range(8)]]
    out = []
    o = K['world_copy'](bpy.data.objects[BODY], 'KZS_enc_tmp')
    for v, vw in zip(o.data.vertices, wb.data.vertices):
        c = v.co
        if not (z0 < c.z < z1):
            continue
        w = {gname[g.group]: g.weight for g in vw.groups}
        if bones:
            if sum(x for b_, x in w.items() if b_ in bones) < 0.5:
                continue
        else:
            if sum(x for b_, x in w.items() if b_.startswith(('Arm', 'ForeArm', 'Hand', 'Head'))) > 0.5:
                continue
            if c.z < 0.86:
                if abs(c.x) <= 0.2:
                    out.append(v.index)
                continue
            if c.z > 1.26 and abs(c.x) < 0.07 and c.y < -0.05:      # decote em V: a pele aparece
                continue
        if sum(t.ray_cast(c + d * 0.001, d, 0.4)[0] is not None for d in dirs) >= min_hits:
            out.append(v.index)
    pts = [tuple(int(round(x * 1000)) for x in o.data.vertices[i].co) for i in out]
    bpy.data.objects.remove(o, do_unlink=True)
    return out, pts


def merge_hide(K, piece, pts):
    import os
    path = os.path.join(K['ROOT'], 'Equip', 'Hide', 'Ecos_%s_InvocadoraEcosBase.txt' % piece)
    old = set(tuple(int(x) for x in l.split()) for l in open(path, encoding='utf-8').read().split('\n') if l.strip())
    allp = sorted(old | set(pts))
    with open(path, 'w', encoding='utf-8') as f:
        f.write('\n'.join('%d %d %d' % p for p in allp))
    return len(allp)


def run_ecos(K, export=True, **fit):
    """Tudo de uma vez (K = namespace do kezor_sketchfab_set). A bota dos Ecos continua a antiga."""
    rep = fit_all(K, **fit)
    K['ALLOWED'].update({'Elmo': ('Head', 'Chest'), 'Armadura': ('Body', 'Chest', 'LegL', 'LegR'),
                         'CalcaL': ('LegL', 'Body', 'Chest'), 'CalcaR': ('LegR', 'Body', 'Chest')})
    K['WEIGHT_MIX'].update({'Elmo': veil_mix, 'Armadura': skirt_mix,
                            'CalcaL': panel_mix('LegL'), 'CalcaR': panel_mix('LegR')})
    K['HIDE_ZMIN'].update({'LuvaL': 0.86, 'LuvaR': 0.86, 'Elmo': 1.3, 'CalcaL': 9.0, 'CalcaR': 9.0, 'Armadura': 0.95})
    parts = build_parts_ecos(K)
    if not export:
        return rep, parts
    wb = bpy.data.objects['KZB_PesosEcos']
    ex = K['export_parts']('Ecos', parts, wb, pivots_ecos(), textures=True)
    hide = K['write_hide']('Ecos', 'InvocadoraEcosBase', parts, [bpy.data.objects[BODY], bpy.data.objects[HEAD]])
    hide['Armadura'] = merge_hide(K, 'Armadura', enclosed_hide(K, parts)[1])
    for side in 'LR':
        hide['Luva' + side] = merge_hide(K, 'Luva' + side, enclosed_hide(
            K, parts, 'Luva' + side, 0.86, 1.5, bones=('Arm' + side, 'ForeArm' + side), min_hits=5)[1])
    return rep, ex, hide
