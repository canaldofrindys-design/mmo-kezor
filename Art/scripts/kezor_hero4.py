# Kezor — herói v4: corpo contínuo esculpido (metaballs anatômicos + cabeça esculpida por campos), unido por
# remesh de voxel, roupa base ajustada ao corpo (túnica, calça, cinto, braçadeiras e botas), olhos, cabelo e pintura
# por cor de vértice com luz/oclusão. Requer no namespace KZ: MB, body_mb, make_head2 (definidos na sessão do MCP)
# ou este próprio arquivo (que redefine tudo o que usa).
import bpy, bmesh, math, random
from mathutils import Vector, Euler, Matrix

K = 0.57


def _kill(n):
    o = bpy.data.objects.get(n)
    if o is not None:
        bpy.data.objects.remove(o, do_unlink=True)


def _link(o):
    bpy.context.scene.collection.objects.link(o)
    return o


def _hex(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


PALETTE = dict(skin='#d9a77f', tunic='#2c4a7c', tunic2='#22385e', trim='#c9a24a', pants='#3a2b22', boots='#5b3c27',
               boots2='#3d281a', belt='#3a2517', buckle='#d8b25a', bracer='#6a4630', hair='#3a2416', lips='#b87262',
               brow='#3a2416', eye='#f2ede4', iris='#5a3a22', pupil='#120c08')


# ================================================================ peças de roupa a partir do corpo
def _region_copy(src, name, keep, offset, extra=None, solid=0.0, cuts=()):
    """Copia as faces do corpo onde keep(p) é verdadeiro e infla pela normal (roupa justa)."""
    _kill(name)
    bm = bmesh.new()
    bm.from_mesh(src.data)
    bm.verts.ensure_lookup_table()
    bm.normal_update()
    dead = [f for f in bm.faces if not all(keep(v.co) for v in f.verts)]
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.normal_update()
    # borda limpa: encaixa a borda na altura do corte e alisa ao longo dela (sem dentes de serra)
    border = [v for v in bm.verts if v.is_boundary]
    for v in border:
        best = None
        for c in cuts:
            if abs(v.co.z - c) < 0.03 and (best is None or abs(v.co.z - c) < abs(v.co.z - best)):
                best = c
        if best is not None:
            v.co.z = best
    for _ in range(4):
        new = {}
        for v in border:
            nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
            if len(nb) == 2:
                new[v] = v.co * 0.5 + (nb[0].co + nb[1].co) * 0.25
        for v, c in new.items():
            v.co = c
    bm.normal_update()
    for v in bm.verts:
        d = offset + (extra(v.co) if extra else 0.0)
        v.co = v.co + v.normal * d
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = _link(bpy.data.objects.new(name, me))
    if solid > 0:
        m = o.modifiers.new('Esp', 'SOLIDIFY')
        m.thickness = solid
        m.offset = -1
        m.use_rim = True
        _apply(o, 'Esp')
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def _apply(o, mod):
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod)


def _is_arm(p):
    return abs(p.x) > 0.232 and p.z > 0.62


def _smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def clothes(body):
    out = {}
    # túnica: tronco de 0,78 m até a base do pescoço, mangas até acima do cotovelo; barra abre como saia
    def tunic_keep(p):
        if p.z > 1.47:
            return False
        if _is_arm(p):
            return p.z > 1.13
        return p.z > 0.76

    def tunic_extra(p):
        if _is_arm(p):
            return 0.004
        return 0.004 + 0.03 * _smooth(0.97, 0.76, p.z)
    out['tunic'] = _region_copy(body, 'KZ_Tunica', tunic_keep, 0.006, tunic_extra, solid=0.006, cuts=(0.76, 1.13, 1.47))
    # calça
    out['pants'] = _region_copy(body, 'KZ_Calca', lambda p: (not _is_arm(p)) and 0.30 < p.z < 0.99, 0.0035, cuts=(0.30, 0.99))
    # botas com cano dobrado
    out['boots'] = _region_copy(body, 'KZ_Botas', lambda p: p.z < 0.37, 0.011,
                                lambda p: 0.008 * _smooth(0.31, 0.35, p.z) + 0.004 * _smooth(0.06, 0.0, p.z), solid=0.004, cuts=(0.37,))
    # cinto
    out['belt'] = _region_copy(body, 'KZ_Cinto', lambda p: (not _is_arm(p)) and 0.925 < p.z < 0.985, 0.026, solid=0.012, cuts=(0.925, 0.985))
    # braçadeiras de couro
    out['bracer'] = _region_copy(body, 'KZ_Bracadeira', lambda p: _is_arm(p) and 0.80 < p.z < 0.98, 0.009, solid=0.006, cuts=(0.80, 0.98))
    return out


# ================================================================ olhos, cabelo e fivela
def _sphere(name, c, r, seg=24, sc=(1, 1, 1)):
    _kill(name)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg * 3 // 4, radius=r)
    for v in bm.verts:
        v.co = Vector((v.co.x * sc[0], v.co.y * sc[1], v.co.z * sc[2])) + Vector(c)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    return _link(bpy.data.objects.new(name, me))


EYE_Y = -0.0525
EYE_Z = 1.702
EYE_X = 0.0335


def eyes():
    return [_sphere('KZ_Olho' + n, (s * EYE_X, EYE_Y, EYE_Z), 0.0118) for n, s in (('L', 1), ('R', -1))]


def hair(body, style='short'):
    """Cabelo: casca do topo da cabeça inflada, com mechas (ondulação na direção do penteado)."""
    def keep(p):
        if p.z < 1.62:
            return False
        front = p.y < -0.02
        if front:                                     # linha do cabelo na testa
            return p.z > 1.735 + 0.02 * (abs(p.x) / 0.08) ** 2
        if abs(p.x) > 0.06 and p.z < 1.69 and p.y < 0.03:   # costeletas e orelhas livres
            return p.z > 1.665 and abs(p.x) < 0.085 and p.y > -0.04
        return True

    def extra(p):
        ang = math.atan2(p.x, -p.y)
        strand = 0.0035 * math.sin(ang * 18 + p.z * 40) + 0.002 * math.sin(ang * 41 - p.z * 90)
        top = 0.008 * _smooth(1.70, 1.80, p.z)
        return strand + top
    o = _region_copy(body, 'KZ_Cabelo', keep, 0.009, extra, solid=0.008)
    return o


# ================================================================ pintura por cor de vértice
def paint(o, base, fn=None):
    """Cor por vértice (calculada uma vez por vértice e copiada para os cantos com foreach_set: rápido)."""
    me = o.data
    attr = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    me.color_attributes.active_color = attr
    c0 = _hex(base)
    vc = []
    for v in me.vertices:
        p, n = v.co, v.normal
        c = fn(p, n, c0) if fn else c0
        k = 0.86 + 0.14 * _smooth(0.0, 1.8, p.z) + 0.06 * max(0.0, n.z)
        vc.append((min(1, c[0] * k), min(1, c[1] * k), min(1, c[2] * k), 1.0))
    idx = [0] * len(me.loops)
    me.loops.foreach_get('vertex_index', idx)
    flat = []
    for i in idx:
        flat.extend(vc[i])
    attr.data.foreach_set('color_srgb', flat)


def _mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def paint_all(parts):
    P = {k: _hex(v) for k, v in PALETTE.items()}

    def skin(p, n, c):
        c = _mix(c, P['lips'], _smooth(0.006, 0.0, abs(p.z - 1.611) - 0.004) * _smooth(0.026, 0.012, abs(p.x)) * (1 if p.y < -0.07 else 0))
        brow = _smooth(0.006, 0.0, abs(p.z - (1.721 + 0.1 * abs(p.x) - 0.002))) * _smooth(0.056, 0.042, abs(p.x)) * _smooth(0.007, 0.016, abs(p.x))
        if p.y < -0.06:
            c = _mix(c, P['brow'], brow * 0.9)
        c = _mix(c, _mix(c, (0.85, 0.45, 0.4), 0.5), 0.18 * _smooth(0.03, 0.0, abs(p.z - 1.668)) * _smooth(0.02, 0.05, abs(p.x)) * (1 if p.y < -0.05 else 0))
        return c
    paint(parts['body'], PALETTE['skin'], skin)

    def tunic(p, n, c):
        hem = _smooth(0.80, 0.77, p.z)
        cuff = _smooth(1.155, 1.135, p.z) if _is_arm(p) else 0
        collar = _smooth(1.44, 1.465, p.z)
        t = max(hem, cuff, collar)
        c = _mix(c, P['tunic2'], 0.5 * _smooth(1.2, 0.85, p.z))
        return _mix(c, P['trim'], t)
    paint(parts['tunic'], PALETTE['tunic'], tunic)
    paint(parts['pants'], PALETTE['pants'])
    paint(parts['boots'], PALETTE['boots'], lambda p, n, c: _mix(c, P['boots2'], max(_smooth(0.03, 0.0, p.z), _smooth(0.31, 0.34, p.z))))
    paint(parts['belt'], PALETTE['belt'], lambda p, n, c: _mix(c, P['buckle'], _smooth(0.022, 0.012, abs(p.x)) * (1 if p.y < -0.05 else 0)))
    paint(parts['bracer'], PALETTE['bracer'])
    paint(parts['hair'], PALETTE['hair'], lambda p, n, c: _mix(c, _mix(c, (1, 0.85, 0.6), 0.25), max(0.0, n.z) * 0.5))
    for e in parts['eyes']:
        cx = e.location.x if e.location.x else None

        def eye(p, n, c, e=e):
            ctr = Vector((EYE_X if p.x > 0 else -EYE_X, EYE_Y, EYE_Z))
            d = (p - ctr).normalized()
            a = d.dot(Vector((0, -1, 0)))
            if d.z > 0.62:
                return P['brow']                 # pálpebra/cílios
            if a > 0.92:
                return P['pupil']
            if a > 0.72:
                return P['iris']
            return P['eye']
        paint(e, PALETTE['eye'], eye)


# ================================================================ montagem
def assemble(tris=18000, voxel=0.0032):
    sc = bpy.context.scene
    B = bpy.data.objects['KZ_CorpoMB']
    B.data.resolution = 0.006
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    m = bpy.data.meshes.new_from_object(B.evaluated_get(dg))
    _kill('KZ_HeroiNovo')
    o = _link(bpy.data.objects.new('KZ_HeroiNovo', m))
    parts = [o]
    for n in ('KZ_Cabeca', 'KZ_OrelhaL', 'KZ_OrelhaR'):
        s = bpy.data.objects[n]
        c = s.copy()
        c.data = s.data.copy()
        _link(c)
        c.data.transform(c.matrix_world)
        c.matrix_world = Matrix.Identity(4)
        parts.append(c)
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    for x in parts:
        x.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.join()
    r = o.modifiers.new('Remesh', 'REMESH')
    r.mode = 'VOXEL'
    r.voxel_size = voxel
    _apply(o, 'Remesh')
    s = o.modifiers.new('Smooth', 'CORRECTIVE_SMOOTH')
    s.iterations = 3
    s.smooth_type = 'SIMPLE'
    s.use_only_smooth = True
    _apply(o, 'Smooth')
    if tris:
        decimate(o, tris)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def decimate(o, tris):
    d = o.modifiers.new('Dec', 'DECIMATE')
    d.ratio = min(1.0, tris / max(1, len(o.data.polygons)))
    _apply(o, 'Dec')
    for p in o.data.polygons:
        p.use_smooth = True


BUDGET = dict(body=12000, tunic=4000, pants=2000, boots=1600, belt=500, bracer=600, hair=2500)


def build_hero4():
    body = assemble(None)                       # alta resolução: roupa e cabelo saem com bordas limpas
    cl = clothes(body)
    parts = dict(body=body, eyes=eyes(), hair=hair(body), **cl)
    for k, n in BUDGET.items():
        decimate(parts[k], n)
    paint_all(parts)
    return parts


# ================================================================ pesos (iguais ao SkinRig "Full" no Unity)
def weights_full(p):
    """Pesos de um corpo contínuo pela posição (Blender): braços, cabeça, pernas e tronco com transições suaves."""
    x, z = p.x, p.z
    ax = abs(x)
    s = 'L' if x > 0 else 'R'
    w = {}

    def add(b, v):
        if v > 1e-4:
            w[b] = w.get(b, 0.0) + v
    a = 0.0
    if 0.60 < z < 1.48:
        a = _smooth(0.20, 0.28, ax) if z > 1.30 else (_smooth(0.215, 0.25, ax) if z > 1.05 else _smooth(0.232, 0.25, ax))
        a *= _smooth(0.60, 0.66, z)
    if a > 0:
        f = _smooth(1.13, 1.03, z)
        h = _smooth(0.83, 0.77, z)
        add('Arm' + s, a * (1 - f))
        add('ForeArm' + s, a * f * (1 - h))
        add('Hand' + s, a * f * h)
    rest = 1 - a
    hd = _smooth(1.465, 1.53, z) * rest
    add('Head', hd)
    rem = rest - hd
    leg = (_smooth(0.93, 0.83, z) if z < 0.95 else 0.0) * rem
    if leg > 0:
        k = _smooth(0.59, 0.47, z)
        an = _smooth(0.15, 0.08, z)
        sl = max(0.0, min(1.0, 0.5 + 0.5 * x / 0.03))          # entre as coxas, divide entre as duas pernas
        for side, f in (('L', sl), ('R', 1 - sl)):
            add('Leg' + side, leg * f * (1 - k))
            add('Shin' + side, leg * f * k * (1 - an))
            add('Foot' + side, leg * f * k * an)
    tor = rem - leg
    c = _smooth(0.98, 1.14, z)
    add('Body', tor * (1 - c))
    add('Chest', tor * c)
    top = sorted(w.items(), key=lambda kv: -kv[1])[:4]
    tot = sum(v for _, v in top) or 1.0
    return {b: v / tot for b, v in top}


def skin_preview(o, arm):
    """Grupos de vértices pelos pesos_full e modificador de esqueleto (para conferir as animações no Blender)."""
    for g in list(o.vertex_groups):
        o.vertex_groups.remove(g)
    groups = {}
    for v in o.data.vertices:
        for b, val in weights_full(v.co).items():
            g = groups.get(b) or o.vertex_groups.new(name=b)
            groups[b] = g
            g.add([v.index], val, 'REPLACE')
    m = o.modifiers.get('Esqueleto') or o.modifiers.new('Esqueleto', 'ARMATURE')
    m.object = arm


# ================================================================ exportação para o jogo
MATID = dict(body=1, tunic=5, pants=5, boots=4, belt=4, bracer=4, hair=2, eyes=0)
OUT = r'C:\MuProtoUnity\Assets\Resources\Models'


def _copy(o, matid):
    c = o.copy()
    c.data = o.data.copy()
    _link(c)
    c.data.transform(c.matrix_world)
    c.matrix_world = Matrix.Identity(4)
    me = c.data
    while len(me.uv_layers) < 2:
        me.uv_layers.new(name='UVMap' if len(me.uv_layers) == 0 else 'MatId')
    uv = me.uv_layers[1].data
    flat = [float(matid), 0.0] * len(me.loops)
    uv.foreach_set('uv', flat)
    return c


def _join(objs, name):
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    for x in objs:
        x.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    o.data.name = name
    return o


def _bake_ao(objs, samples=24, distance=0.12):
    sc = bpy.context.scene
    old = sc.render.engine
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = samples
    try:
        sc.cycles.device = 'CPU'
    except Exception:
        pass
    if sc.world is not None and hasattr(sc.world, 'light_settings'):
        sc.world.light_settings.distance = distance
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    for o in objs:
        me = o.data
        a = me.color_attributes.get('AO') or me.color_attributes.new('AO', 'BYTE_COLOR', 'CORNER')
        me.color_attributes.active_color = a
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    try:
        bpy.ops.object.bake(type='AO', target='VERTEX_COLORS', margin=0)
    finally:
        sc.render.engine = old
    for o in objs:
        me = o.data
        ao = me.color_attributes.get('AO')
        col = me.color_attributes.get('Col')
        n = len(me.loops)
        a = [0.0] * (n * 4)
        c = [0.0] * (n * 4)
        ao.data.foreach_get('color', a)
        col.data.foreach_get('color_srgb', c)
        for i in range(n):
            k = 0.62 + 0.38 * a[i * 4]
            c[i * 4] *= k
            c[i * 4 + 1] *= k
            c[i * 4 + 2] *= k
        col.data.foreach_set('color_srgb', c)
        me.color_attributes.remove(ao)
        col = me.color_attributes.get('Col')
        me.color_attributes.active_color = col
        try:
            me.color_attributes.render_color_index = me.color_attributes.find('Col')
        except Exception:
            pass


PIVOTS = dict(Head=(0, 0, 1.47), ArmL=(0.31, 0, 1.40), ArmR=(-0.31, 0, 1.40), LegL=(0.12, 0, 0.92), LegR=(-0.12, 0, 0.92))


def export_hero4(parts, name='GuardiaoRunicoBase', cat='Classes'):
    """Body = corpo contínuo com a roupa (pele que dobra); Head = olhos e cabelo (rígidos na cabeça);
    ArmL/ArmR/LegL/LegR = pivôs vazios (o equipamento e o esqueleto do jogo usam esses nomes)."""
    import os
    for n in ('__h4_root', 'Body', 'Head', 'ArmL', 'ArmR', 'LegL', 'LegR'):
        _kill(n)
    body_parts = [_copy(parts[k], MATID[k]) for k in ('body', 'tunic', 'pants', 'boots', 'belt', 'bracer')]
    body = _join(body_parts, 'Body')
    head_parts = [_copy(parts['hair'], 2)] + [_copy(e, 0) for e in parts['eyes']]
    head = _join(head_parts, 'Head')
    _bake_ao([body, head])
    head.data.transform(Matrix.Translation(-Vector(PIVOTS['Head'])))
    root = _link(bpy.data.objects.new('__h4_root', None))
    root['kezor_root'] = True
    root['category'] = cat
    body.parent = root
    body['part'] = 'Body'
    body['pivot'] = (0, 0, 0)
    head.parent = body
    head.location = PIVOTS['Head']
    head['part'] = 'Head'
    head['pivot'] = PIVOTS['Head']
    kids = [body, head]
    for n in ('ArmL', 'ArmR', 'LegL', 'LegR'):
        e = _link(bpy.data.objects.new(n, None))
        e.parent = body
        e.location = PIVOTS[n]
        e['part'] = n
        e['pivot'] = PIVOTS[n]
        kids.append(e)
    bpy.context.view_layer.update()
    os.makedirs(os.path.join(OUT, cat), exist_ok=True)
    path = os.path.join(OUT, cat, name + '.fbx')
    G = bpy.app.driver_namespace['KZ']
    saved = G['prebake'](kids)
    bpy.context.view_layer.update()
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    for k in kids:
        k.select_set(True)
    bpy.context.view_layer.objects.active = body
    try:
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
                                 global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 bake_space_transform=False, axis_forward='-Z', axis_up='Y',
                                 mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False,
                                 bake_anim=False, path_mode='COPY', embed_textures=True, use_custom_props=False,
                                 colors_type='SRGB')
    finally:
        G['unbake'](kids, saved)
    names = [k.name for k in kids]
    for k in kids + [root]:
        bpy.data.objects.remove(k, do_unlink=True)
    return path, os.path.getsize(path), names, sum(len(p.vertices) - 2 for p in parts['body'].data.polygons)
