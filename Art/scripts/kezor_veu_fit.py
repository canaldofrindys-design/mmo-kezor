# Kezor — veste o "Monarch's Gamble" (Sketchfab, CC BY) no corpo da Arcanista do Véu.
# Uso (Blender, cena com VB_Body/VB_Head/VB_ArmL... e o modelo importado como Object_4 (cima) e Object_5 (baixo)):
#   exec(open(r'...\kezor_veu_fit.py').read(), G); G['fit_all']()
import bpy, math, bmesh
from mathutils import Vector, Matrix

SRC_TOP, SRC_BOT = 'Object_4', 'Object_5'
BODY = 'VB_Body.001'


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


def components(me):
    """Peças soltas da malha: lista de conjuntos de índices de vértice."""
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    seen, comps = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        st, comp = [v], []
        seen.add(v.index)
        while st:
            x = st.pop(); comp.append(x.index)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen:
                    seen.add(y.index); st.append(y)
        comps.append(set(comp))
    bm.free()
    return comps


def split(src, name, groups):
    """Uma cópia por grupo, cada uma só com as faces cujos vértices estão no grupo."""
    out = {}
    for g, idxs in groups.items():
        o = src.copy(); o.data = src.data.copy()
        bpy.context.scene.collection.objects.link(o)
        old = bpy.data.objects.get(name + g)
        if old:
            bpy.data.objects.remove(old, do_unlink=True)
        o.name = name + g
        bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
        kill = [v for v in bm.verts if v.index not in idxs]
        bmesh.ops.delete(bm, geom=kill, context='VERTS')
        bm.to_mesh(o.data); bm.free()
        out[g] = o
    return out


def classify_top(me):
    g = {'Collar': set(), 'Bracers': set(), 'Sleeves': set(), 'Yoke': set(), 'Torso': set()}
    for c in components(me):
        cs = [me.vertices[i].co for i in c]
        zmin = min(p.z for p in cs); axmin = min(abs(p.x) for p in cs)
        cx = sum(p.x for p in cs) / len(cs)
        if zmin > 1.80:
            g['Collar'] |= c
        elif axmin > 0.6:
            g['Bracers'] |= c
        elif axmin > 0.2 and abs(cx) > 0.3:
            g['Sleeves'] |= c
        elif max(abs(p.x) for p in cs) > 0.28:
            g['Yoke'] |= c                      # faixas sobre os ombros que seguram as mangas
        else:
            g['Torso'] |= c
    return g


def classify_bot(me):
    g = {'Tails': set(), 'Tunic': set()}
    for c in components(me):
        zmin = min(me.vertices[i].co.z for i in c)
        (g['Tails'] if zmin < 0.8 else g['Tunic']).update(c)
    return g


def fit_all(scale=0.86, shoulder_top=1.47, wrist_z=0.90, sleeve_min_x=0.215):
    G = globals()
    body = [bpy.data.objects[BODY].matrix_world @ v.co for v in bpy.data.objects[BODY].data.vertices]
    top = wcopy(bpy.data.objects[SRC_TOP], 'VE_TopAll')
    bot = wcopy(bpy.data.objects[SRC_BOT], 'VE_BotAll')
    P = {}
    P.update(split(top, 'VE_', classify_top(top.data)))
    P.update(split(bot, 'VE_', classify_bot(bot.data)))
    for o in (top, bot):
        bpy.data.objects.remove(o, do_unlink=True)
    # 1) escala uniforme: ombro do modelo (topo das mangas, z 1.78) no ombro do corpo; centro do tronco em y
    zoff = shoulder_top - 1.78 * scale
    tor = [p for p in body if 1.15 < p.z < 1.4 and abs(p.x) < 0.13]
    by = (min(p.y for p in tor) + max(p.y for p in tor)) / 2
    tv = [v.co for v in P['Torso'].data.vertices if 1.45 < v.co.z < 1.66 and abs(v.co.x) < 0.17]
    ay = (min(c.y for c in tv) + max(c.y for c in tv)) / 2 * scale
    M = Matrix.Translation((0, by - ay, zoff)) @ Matrix.Scale(scale, 4)
    for o in P.values():
        o.data.transform(M)
    # 2) braços: o modelo tem os braços a ~45°; gira em torno do ombro até o braço do corpo (pendurado) e ajusta o comprimento
    piv = bpy.data.objects['VB_ArmL.001'].matrix_world.translation
    br = [v.co for v in P['Bracers'].data.vertices if v.co.x > 0]
    wrist = Vector((max(c.x for c in br), 0, sum(c.z for c in br) / len(br)))
    sv = [v.co for v in P['Sleeves'].data.vertices if v.co.x > 0]
    shoulder = Vector((min(c.x for c in sv) - 0.01, 0, max(c.z for c in sv) - 0.07))
    a0 = math.atan2(shoulder.z - wrist.z, wrist.x - shoulder.x)
    hand = [p for p in body if p.x > 0.14 and abs(p.z - wrist_z) < 0.01]
    bw = Vector((sum(p.x for p in hand) / len(hand), 0, wrist_z))
    a1 = math.atan2(shoulder.z - bw.z, bw.x - shoulder.x)
    s = (bw - shoulder).length / (wrist - shoulder).length
    for k in ('Sleeves', 'Bracers', 'Yoke'):
        me = P[k].data
        for v in me.vertices:
            c = v.co
            sg = 1 if c.x >= 0 else -1
            w = sm(shoulder.x - 0.05, shoulder.x + 0.04, abs(c.x))
            if w <= 0:
                continue
            x, z = abs(c.x) - shoulder.x, c.z - shoulder.z
            r = math.hypot(x, z); th = math.atan2(z, x)
            r2 = r * (1 + (s - 1) * w)
            th2 = th - (a1 - a0) * w
            nx = shoulder.x + r2 * math.cos(th2)
            # manga larga: o pano que cairia dentro do tronco fica encostado do lado de fora
            if k == 'Sleeves' and nx < sleeve_min_x:
                nx = sleeve_min_x + (nx - sleeve_min_x) * 0.15
            c.x = sg * nx
            c.z = shoulder.z + r2 * math.sin(th2)
        me.update()
    # 3) nada da roupa por dentro do corpo: empurra para fora da pele (com folga)
    for k, gap in (('Torso', 0.012), ('Tunic', 0.014), ('Yoke', 0.012), ('Sleeves', 0.012), ('Bracers', 0.008), ('Collar', 0.006)):
        push_out(P[k], gap)
    # 4) bojo do peito e faixas dos ombros: o modelo foi feito para outro corpo; aproxima da pele
    pull_in(P['Torso'], 0.014, 0.09, 0.3)
    pull_in(P['Yoke'], 0.014, 0.12, 0.25)
    G['P'] = P
    return {'zoff': zoff, 'by-ay': by - ay, 'shoulder': tuple(shoulder), 'wrist': tuple(wrist), 'body_wrist': tuple(bw),
            'a0': math.degrees(a0), 'a1': math.degrees(a1), 's': s, 'arm_pivot': tuple(piv)}


def push_out(o, gap):
    from mathutils.bvhtree import BVHTree
    import bmesh as _bm
    G = globals()
    if 'BVH' not in G:
        b = bpy.data.objects[BODY]
        bm = _bm.new(); bm.from_mesh(b.data); bm.transform(b.matrix_world)
        _bm.ops.triangulate(bm, faces=bm.faces[:])
        G['BVH'] = BVHTree.FromBMesh(bm); bm.free()
    t = G['BVH']
    n = 0
    for v in o.data.vertices:
        hit = t.find_nearest(v.co, 0.08)
        if hit[0] is None:
            continue
        loc, nor = hit[0], hit[1]
        d = (v.co - loc).dot(nor)
        if d < gap:
            v.co = v.co + nor * (gap - d); n += 1
    o.data.update()
    return n


def pull_in(o, gap, maxd, k):
    t = globals()['BVH']
    for v in o.data.vertices:
        hit = t.find_nearest(v.co, maxd)
        if hit[0] is None:
            continue
        d = (v.co - hit[0]).dot(hit[1])
        if d > gap:
            v.co = hit[0] + hit[1] * (gap + (d - gap) * k) + (v.co - hit[0] - hit[1] * d)
    o.data.update()


# ================================================================ peças do jogo (usa as funções do kezor_sketchfab_set, namespace K)
DECIMATE = {'Tails': 0.5, 'Tunic': 0.6, 'Torso': 0.8, 'Sleeves': 0.8}


def decimate(o, ratio):
    if not ratio or ratio >= 1:
        return
    m = o.modifiers.new('dec', 'DECIMATE')
    m.ratio = ratio
    with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
        bpy.ops.object.modifier_apply(modifier='dec')


def keep_side(src, name, sg):
    o = src.copy(); o.data = src.data.copy()
    bpy.context.scene.collection.objects.link(o)
    o.name = name
    bm = bmesh.new(); bm.from_mesh(o.data)
    kill = [f for f in bm.faces if sum(v.co.x for v in f.verts) * sg <= 0]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(o.data); bm.free()
    return o


def build_parts_veu(K):
    P = globals()['P']
    for x in [x for x in bpy.data.objects if x.name.startswith('KZS_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    mt, mb = P['Torso'].data.materials[0], P['Tunic'].data.materials[0]
    out = {}
    out['Elmo'] = ([K['_dup'](P['Collar'], 'KZS_Collar')], [mt], 'Elmo')
    to = K['_join']([K['_dup'](P['Torso'], 'KZS_Torso0'), K['_dup'](P['Yoke'], 'KZS_Yoke')], 'KZS_Torso'); decimate(to, DECIMATE['Torso'])
    tu = K['_dup'](P['Tunic'], 'KZS_Tunic'); decimate(tu, DECIMATE['Tunic'])
    out['Armadura'] = ([to, tu], [mt, mb], 'Armadura')
    sl = K['_dup'](P['Sleeves'], 'KZS_SleevesAll'); decimate(sl, DECIMATE['Sleeves'])
    ta = K['_dup'](P['Tails'], 'KZS_TailsAll'); decimate(ta, DECIMATE['Tails'])
    for side, sg in (('L', 1), ('R', -1)):
        s = keep_side(sl, 'KZS_Sleeve' + side, sg)
        b = keep_side(P['Bracers'], 'KZS_Bracer' + side, sg)
        out['Luva' + side] = ([K['_join']([s, b], 'KZS_Luva' + side)], [mt], 'Luva')
        out['Calca' + side] = ([keep_side(ta, 'KZS_Tail' + side, sg)], [mb], 'Calca')
    bpy.data.objects.remove(sl, do_unlink=True); bpy.data.objects.remove(ta, do_unlink=True)
    return out


def tail_mix(co, w):
    """Caudas do casaco: no quadril seguem o tronco; descendo passam a seguir a coxa do mesmo lado."""
    f = 0.8 * sm(0.95, 0.55, co.z)
    side = 'LegL' if co.x >= 0 else 'LegR'
    base = [(b, x * (1 - f)) for b, x in w if b in ('Body', 'Chest')] or [('Body', 1 - f)]
    return base + [(side, f)]


def pivots_veu():
    g = lambda n: tuple(bpy.data.objects[n].matrix_world.translation)
    return {'Armadura': (0.0, 0.0, 0.0), 'Elmo': g('VB_Head.001'), 'LuvaL': g('VB_ArmL.001'), 'LuvaR': g('VB_ArmR.001'),
            'CalcaL': g('VB_LegL.001'), 'CalcaR': g('VB_LegR.001')}
