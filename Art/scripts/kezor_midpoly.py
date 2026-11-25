# Kezor — passe "mid poly estilizado" (roda no namespace G do Blender, depois de kezor_heroes2/kezor_town2).
# 1) subdivisão (Catmull-Clark) para arredondar caixas chanfradas, 2) sombreamento suave com normais ponderadas
#    (bordas duras só acima de ~55°), 3) id do tipo de material na 2ª UV (o shader Kezor/LowPoly aplica a textura
#    de detalhe: pele, cabelo, metal, couro, tecido, madeira, pedra, reboco, telha, folhagem), 4) LODs opcionais.
import bpy, bmesh, math, re

# ordem importa: a primeira regra que casa vence
MAT_RULES = [
    (0, ('eye', 'iris', 'glow', 'gem', 'rune', 'ember', 'lava', 'window', 'win', 'lamp', 'lant', 'potion', 'glass',
         'crystal', 'void', 'core', 'wisp', 'portalrune', 'keyglow', 'water', 'gel', 'slime', 'coal')),
    (9, ('tile', 'roof')),
    (8, ('plaster',)),
    (10, ('leaf', 'pine', 'moss', 'reed', 'seaweed', 'flower', 'grass', 'hay', 'mush', 'fatuo')),
    (7, ('stone', 'basalt', 'wall', 'slab', 'rock', 'crypt', 'portal', 'bone', 'fang', 'skull', 'soil', 'ash')),
    (6, ('wood', 'timber', 'plank', 'bark', 'trunk', 'bow', 'shaft', 'staff', 'chest', 'wand', 'string')),
    (1, ('skin', 'mouth', 'head')),
    (2, ('hair', 'beard', 'fur', 'feather', 'fth', 'belly', 'membrane', 'wing')),
    (3, ('steel', 'iron', 'gold', 'bronze', 'brass', 'silver', 'blade', 'plate', 'metal', 'guard', 'rust', 'bell',
         'lock', 'socket', 'saber', 'scale', 'shell', 'bastion', 'grate', 'astral', 'mask', 'horn')),
    (4, ('leather', 'hide', 'saddle', 'book', 'cover', 'tome', 'grip', 'grimoire', 'pages')),
    (5, ('cloth', 'robe', 'cape', 'cloak', 'dress', 'tunic', 'pants', 'shirt', 'apron', 'scarf', 'hood', 'banner',
         'veil', 'weave', 'rag', 'wrap', 'band', 'vest', 'coat', 'trim', 'top', 'blue', 'red', 'green', 'crimson',
         'purple', 'echo', 'cap', 'dark', 'arc', 'com', 'inv', 'pun', 'lam')),
]


def mat_id(m):
    """Tipo de material (id da textura de detalhe) a partir do nome; emissivos (alfa < 1) ficam sem detalhe."""
    if m is None:
        return 0
    try:
        if _mat_vcol(m)[3] < 1.0:
            return 0
    except Exception:
        pass
    n = re.sub(r'^(K_)+', '', m.name).lower()
    n = re.sub(r'_[ld]\d+.*$', '', n)
    for mid, keys in MAT_RULES:
        if any(k in n for k in keys):
            return mid
    return 0


def write_matid(obj):
    """UV0 = vazia (o FBX precisa de uma), UV1 = (id do material, 0) por canto de face."""
    me = obj.data
    while len(me.uv_layers) < 2:
        me.uv_layers.new(name='UVMap' if len(me.uv_layers) == 0 else 'MatId')
    ids = [mat_id(m) for m in me.materials] or [0]
    uv = me.uv_layers[1].data
    for p in me.polygons:
        v = float(ids[min(p.material_index, len(ids) - 1)])
        for li in p.loop_indices:
            uv[li].uv = (v, 0.0)


def _apply_mod(obj, mod):
    with bpy.context.temp_override(object=obj, active_object=obj):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def smooth_obj(obj, level=1, sharp_deg=55.0, weld=0.0005, grow=1.35, small=500):
    """Arredonda e suaviza uma malha (destrutivo). Retorna triângulos finais."""
    me = obj.data
    orig = sum(len(p.vertices) - 2 for p in me.polygons)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=weld)
    bm.to_mesh(me); bm.free()
    if level > 0:
        m = obj.modifiers.new('mp_sub', 'SUBSURF')
        m.levels = level; m.render_levels = level
        try:
            m.boundary_smooth = 'PRESERVE_CORNERS'
        except Exception:
            pass
        m.use_limit_surface = False
        _apply_mod(obj, m)
        # orçamento: peças pequenas ficam com a subdivisão inteira; as grandes voltam para ~grow× os triângulos originais
        now = sum(len(p.vertices) - 2 for p in me.polygons)
        want = max(small * 4, int(orig * grow))
        if now > want:
            d = obj.modifiers.new('mp_dec', 'DECIMATE')
            d.decimate_type = 'COLLAPSE'; d.ratio = want / now; d.use_collapse_triangulate = True
            _apply_mod(obj, d)
    for p in me.polygons:
        p.use_smooth = True
    # arestas duras: só onde a dobra passa de sharp_deg ou na troca de material
    bm = bmesh.new(); bm.from_mesh(me)
    lim = math.radians(sharp_deg)
    for e in bm.edges:
        sharp = False
        if len(e.link_faces) == 2:
            f0, f1 = e.link_faces
            ang = f0.normal.angle(f1.normal, 0)
            if ang > lim or (f0.material_index != f1.material_index and ang > math.radians(32)):
                sharp = True
        e.smooth = not sharp
    bm.to_mesh(me); bm.free()
    wn = obj.modifiers.new('mp_wn', 'WEIGHTED_NORMAL')
    wn.keep_sharp = True; wn.weight = 50; wn.mode = 'FACE_AREA'
    _apply_mod(obj, wn)
    return sum(len(p.vertices) - 2 for p in me.polygons)


def midpoly(root, level=1, sharp_deg=55.0, skip=(), grow=1.35):
    """Passe mid poly em todas as malhas do modelo. Partes minúsculas (olhos, gemas) não são subdivididas."""
    tris = 0
    for k in [c for c in root.children_recursive if c.type == 'MESH' and len(c.data.polygons)]:
        if k.get('midpoly') or k.get('part') in skip:
            tris += sum(len(p.vertices) - 2 for p in k.data.polygons)
            continue
        dims = max(k.dimensions) if k.dimensions else 0
        lv = level if dims > 0.03 else 0
        tris += smooth_obj(k, lv, sharp_deg, grow=grow)
        k['midpoly'] = 1
    root['midpoly'] = 1
    return tris


def tri_count(root):
    return sum(sum(len(p.vertices) - 2 for p in k.data.polygons) for k in root.children_recursive if k.type == 'MESH')


# bake_vertex_colors com variação por face mais leve (com malha suave, a variação forte vira "sarampo")
if '_bvc_base' not in globals():
    _bvc_base = bake_vertex_colors


def bake_vertex_colors(obj):
    me = obj.data
    if not obj.get('midpoly'):
        _bvc_base(obj)
        write_matid(obj)
        return
    attr = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    ao = me.color_attributes.get('AO')
    cols = [_mat_vcol(m) for m in me.materials] or [(1, 1, 1, 1)]
    try:
        cn = [v.vector.z for v in me.corner_normals]       # normal suave por canto: o tom não "quebra" face a face
    except Exception:
        cn = None
    for poly in me.polygons:
        c = cols[min(poly.material_index, len(cols) - 1)]
        for li in poly.loop_indices:
            if c[3] < 1.0:
                attr.data[li].color_srgb = c
                continue
            up = cn[li] if cn is not None else poly.normal.z
            var = 1.0 + 0.05 * up
            o = ao.data[li].color[0] if ao is not None else 1.0
            k = var * (0.62 + 0.38 * o)
            attr.data[li].color_srgb = (min(1, c[0] * k), min(1, c[1] * k), min(1, c[2] * k), 1.0)
    if ao is not None:
        me.color_attributes.remove(ao)
        attr = me.color_attributes.get('Col')
    me.color_attributes.active_color = attr
    try:
        me.color_attributes.render_color_index = me.color_attributes.find('Col')
    except Exception:
        pass
    write_matid(obj)


def show_only(names):
    """Esconde tudo e mostra só estes modelos (para capturas). Retorna o estado para restore_view."""
    saved = {o.name: (o.hide_get(), o.hide_viewport) for o in bpy.context.view_layer.objects}
    keep = set()
    for n in names:
        r = bpy.data.objects.get(n)
        if r:
            keep.add(r.name); keep.update(c.name for c in r.children_recursive)
    for o in bpy.context.view_layer.objects:
        v = o.name in keep
        o.hide_viewport = not v
        o.hide_set(not v)
    return saved


def restore_view(saved):
    for n, (h, hv) in saved.items():
        o = bpy.data.objects.get(n)
        if o and o.name in bpy.context.view_layer.objects:
            o.hide_viewport = hv; o.hide_set(h)


# ================================================================ caixa arredondada (substitui a cbox chanfrada)
if '_cbox_base' not in globals():
    _cbox_base = cbox
ROUND = True
MAX_R = 0.12      # raio máximo do arredondado (m)
ORGANIC = False   # heróis: cantos bem mais redondos e meio estufado (silhueta orgânica)
BULGE = 0.05
BIG_SIZE = 0.11   # acima disso a caixa ganha 2 segmentos por canto (cidade usa ~0.9 para poupar triângulos)


def _rrect(cx, cy, z, ax, ay, rc, k):
    """Retângulo arredondado (anti-horário visto de cima), k segmentos por canto."""
    rc = max(1e-4, min(rc, ax * 0.98, ay * 0.98))
    pts = []
    for q, (sx, sy, a0) in enumerate(((1, -1, -90), (1, 1, 0), (-1, 1, 90), (-1, -1, 180))):
        ccx, ccy = cx + sx * (ax - rc), cy + sy * (ay - rc)
        for i in range(k + 1):
            a = math.radians(a0 + 90.0 * i / k)
            pts.append((ccx + math.cos(a) * rc, ccy + math.sin(a) * rc, z))
    return pts


def _rbox_geom(c, size, bev=0.0, top=(1, 1), shift=(0, 0)):
    """Pontos e faces de uma caixa arredondada. Retorna (pts, faces, idx_topo)."""
    cx, cy, cz = c
    sx, sy, sz = [max(v, 1e-4) / 2 for v in size]
    mn = min(sx, sy, sz)
    big = max(size) > BIG_SIZE and mn > 0.012
    k = 2 if big else 1
    ps = 2 if big else 1
    f = (0.85 if big else 0.6) if ORGANIC else (0.46 if big else 0.36)
    cap = 0.92 if ORGANIC else 0.47
    rv = min(max(bev, f * mn), cap * mn)                         # raio do perfil vertical (topo/base)
    rc = min(max(bev, f * min(sx, sy)), cap * min(sx, sy))       # raio dos cantos vistos de cima
    mr = MAX_R * (2.0 if ORGANIC else 1.0)
    rv = min(rv, mr); rc = min(rc, mr * 1.2)                     # paredes grandes: canto macio, não bolha
    tx, ty = top
    ox, oy = shift
    z0, z1 = cz - sz, cz + sz
    angs = [25.0 + (90.0 - 25.0) * i / ps for i in range(ps + 1)]
    rings = []
    for a in angs:                                               # base: do centro para fora
        t = math.radians(a)
        ins = rv * (1 - math.sin(t))
        rings.append(_rrect(cx, cy, z0 + rv * (1 - math.cos(t)), sx - ins, sy - ins, max(rc - ins, rc * 0.3), k))
    if ORGANIC and big:                                          # barriga: o meio da peça estufa um pouco (forma orgânica)
        bz = (z0 + rv + z1 - rv) / 2
        mx, my = sx * (1 + tx) / 2, sy * (1 + ty) / 2
        rings.append(_rrect(cx + ox / 2, cy + oy / 2, bz, mx * (1 + BULGE), my * (1 + BULGE), rc * (1 + BULGE), k))
    for a in reversed(angs):                                     # topo: de fora para o centro
        t = math.radians(a)
        ins = rv * (1 - math.sin(t))
        rings.append(_rrect(cx + ox, cy + oy, z1 - rv * (1 - math.cos(t)), max(sx * tx - ins, 1e-4), max(sy * ty - ins, 1e-4), max(rc - ins, rc * 0.3), k))
    n = len(rings[0])
    pts = [p for r in rings for p in r]
    faces = []
    for kk in range(len(rings) - 1):
        for i in range(n):
            j = (i + 1) % n
            faces.append((kk * n + i, kk * n + j, (kk + 1) * n + j, (kk + 1) * n + i))
    faces.append(tuple(reversed(range(n))))
    last = (len(rings) - 1) * n
    faces.append(tuple(range(last, last + n)))
    return pts, faces, len(faces) - 1


def cbox(b, c, size, m, bev=0.018, top=(1, 1), shift=(0, 0), mtop=None):
    """Caixa de cantos e arestas arredondados (mid poly). Peças grandes ganham mais segmentos; minúsculas ficam com 1."""
    if not ROUND:
        return _cbox_base(b, c, size, m, bev, top, shift, mtop)
    pts, faces, ti = _rbox_geom(c, size, bev, top, shift)
    ms = [m] * len(faces)
    ms[ti] = mtop or m
    return b.poly(pts, faces, ms)


if '_box_base' not in globals():
    _box_base = B.box
    _seg_base = B.seg


def _box_round(s, c, size, m, top=(1, 1), shift=(0, 0)):
    if not ROUND:
        return _box_base(s, c, size, m, top, shift)
    return cbox(s, c, size, m, 0.0, top, shift)


def _seg_round(s, p0, p1, w, m, w2=None, d=None, d2=None, up=(0, 0, 1)):
    """Segmento (membros, cabos) com seção arredondada, afunilando de (w, d) para (w2, d2)."""
    if not ROUND:
        return _seg_base(s, p0, p1, w, m, w2, d, d2, up)
    p0, p1 = Vector(p0), Vector(p1)
    w2 = w if w2 is None else w2
    d = w if d is None else d
    d2 = (d * w2 / w) if d2 is None else d2
    L = (p1 - p0).length
    if L < 1e-5:
        return []
    ax = (p1 - p0).normalized()
    u = Vector(up).cross(ax)
    if u.length < 1e-4:
        u = Vector((1, 0, 0)).cross(ax)
    u.normalize()
    v = ax.cross(u).normalized()
    pts, faces, _ = _rbox_geom((0, 0, L / 2), (w, d, L), 0.0, (w2 / max(w, 1e-5), d2 / max(d, 1e-5)))
    wp = [tuple(p0 + u * x + v * y + ax * z) for x, y, z in pts]
    return s.poly(wp, faces, m)


B.box = _box_round
B.seg = _seg_round

if '_lathe_base' not in globals():
    _lathe_base = B.lathe


def _lathe_round(s, c, prof, n, m, rot0=None, sx=1.0, sy=1.0, caps=True):
    """Peças de revolução (mantos, saias, golas, cabos): o dobro de lados nos heróis — sai a saia octogonal."""
    if ORGANIC and n < 16:
        n = max(12, n * 2)
    return _lathe_base(s, c, prof, n, m, rot0, sx, sy, caps)


B.lathe = _lathe_round


# ================================================================ pipeline dos heróis
HERO_CATS = ('Classes', 'Equip', 'Wings')


def rebuild_midpoly_city():
    """Cidade e natureza: formas arredondadas, telhas curvas; pinheiros e pedras ganham uma subdivisão."""
    global BIG_SIZE
    old = BIG_SIZE
    BIG_SIZE = 0.9
    try:
        build_city_props()
    finally:
        BIG_SIZE = old
    for n in CITY_PROPS:
        r = bpy.data.objects.get(n)
        if r is None:
            continue
        lvl = 1 if n.startswith(('Pinheiro2', 'Pedra2', 'Arvore2')) else 0
        midpoly(r, level=lvl, sharp_deg=70)
    return list(CITY_PROPS)


def rebuild_midpoly_heroes():
    """Refaz corpos, conjuntos, armas e asas com as formas arredondadas e aplica o passe mid poly."""
    global ORGANIC
    ORGANIC = True
    try:
        rebuild_heroes()
        rebuild_sets3()
        weapons(); weapons2(); all_wings()
    finally:
        ORGANIC = False
    roots = [o for o in bpy.data.objects if o.get('kezor_root') and o['category'] in HERO_CATS]
    for r in roots:
        shape_pass(r)
        midpoly(r, level=0, sharp_deg=70)
    return sorted(r.name for r in roots if not (r['category'] == 'Classes' and not r.name.endswith('Base')))


def export_batch(names, start, count):
    """Exporta (com AO) uma fatia da lista — o MCP tem limite de tempo por chamada."""
    import io, contextlib
    f = io.StringIO()
    with contextlib.redirect_stdout(f):
        b, rep = export_v2(names[start:start + count])
    return b, [r for r in rep if r[3]]


# ================================================================ telhado de telhas curvas (capa e canal)
def wavy_row(b, x0, x1, y, z, dvec, nrm, wid, m, period=0.3, amp=0.05, thick=0.05):
    """Fileira de telhas onduladas ao longo de X: topo ondulado + aba da frente (borda de baixo)."""
    n = max(2, int(round((x1 - x0) / period)))
    steps = n * 4
    top_u, top_l, bot_l = [], [], []
    for i in range(steps + 1):
        x = x0 + (x1 - x0) * i / steps
        wave = abs(math.sin(math.pi * i / 4.0)) * amp
        for lst, du, dn in ((top_u, -wid / 2, wave), (top_l, wid / 2, wave + 0.012), (bot_l, wid / 2, -thick)):
            lst.append((x + dvec[0] * du + nrm[0] * dn, y + dvec[1] * du + nrm[1] * dn, z + dvec[2] * du + nrm[2] * dn))
    k = steps + 1
    pts = top_u + top_l + bot_l
    faces = []
    for i in range(steps):
        faces.append((i, i + 1, k + i + 1, k + i))                 # topo
        faces.append((k + i, k + i + 1, 2 * k + i + 1, 2 * k + i))  # aba
    # orienta as faces para fora (normal do topo deve concordar com nrm)
    p0, p1, p2 = Vector(pts[0]), Vector(pts[1]), Vector(pts[k + 1])
    if (p1 - p0).cross(p2 - p0).dot(Vector(nrm)) < 0:
        faces = [tuple(reversed(f)) for f in faces]
    return b.poly(pts, faces, m)


def tile_roof(b, c, w, d, h, rnd, tiles, overhang=0.35, ridge_m=None, gable_m=None, row=0.34):
    """Telhado de duas águas com fileiras de telhas curvas sobrepostas, cumeeira arredondada e beiral."""
    cx, cy, cz = c
    W, D = w + overhang * 2, d + overhang * 2
    b.roof((cx, cy, cz - 0.02), W - 0.1, D - 0.12, h - 0.05, T['timber'], gable_m or T['plaster'])
    half = D / 2
    L = math.hypot(half, h)
    rows = max(3, int(L / row))
    for s in (-1, 1):
        nrm = (0, s * h / L, half / L)
        dvec = (0, s * half / L, -h / L)
        for i in range(rows):
            t = (i + 0.5) / rows
            y = cy + s * half * (1 - t)
            z = cz + h * t + 0.05 + (0.015 if i % 2 else 0)
            m = tiles[rnd.randrange(len(tiles))]
            wavy_row(b, cx - W / 2, cx + W / 2, y, z, dvec, nrm, row * 1.25, m)
    b.seg((cx - W / 2 - 0.05, cy, cz + h + 0.06), (cx + W / 2 + 0.05, cy, cz + h + 0.06), 0.26, ridge_m or tiles[0], d=0.16)
    for s in (-1, 1):
        for k in range(int(W / 0.7) + 1):
            x = cx - W / 2 + 0.2 + k * 0.7
            if x > cx + W / 2 - 0.1:
                break
            b.seg((x, cy + s * (d / 2 - 0.05), cz - 0.1), (x, cy + s * (D / 2 - 0.05), cz - 0.22), 0.1, T['timber'], d=0.1)


# ================================================================ hera e musgo nas muralhas
def ivy(b, x0, x1, face_y, side, z_top, rnd, vines=4, leaf_mats=None, stem_m=None):
    """Ramos de hera descendo da borda (z_top) pela face (y = face_y, side = +1 fora / -1 dentro), com tufos de folhas."""
    t = T
    leaf_mats = leaf_mats or [t['leaf'], t['leaf3'], t['leaf2']]
    stem_m = stem_m or t['bark2']
    for v in range(vines):
        x = x0 + (x1 - x0) * (v + 0.3 + rnd.random() * 0.4) / vines
        z = z_top
        length = 1.6 + rnd.random() * 2.6
        y = face_y + side * 0.06
        prev = (x, y, z)
        steps = int(length / 0.35)
        for s in range(steps):
            x += (rnd.random() - 0.5) * 0.32
            z -= 0.35
            cur = (x, y + side * rnd.random() * 0.03, z)
            b.seg(prev, cur, 0.035, stem_m, d=0.03)
            for _ in range(2):
                c = (cur[0] + (rnd.random() - 0.5) * 0.3, y + side * 0.07, cur[2] + (rnd.random() - 0.5) * 0.25)
                r = 0.1 + rnd.random() * 0.08
                b.ico(c, (r, r * 0.45, r), leaf_mats[rnd.randrange(len(leaf_mats))], jit=0.25, seed=rnd.randrange(9999), sub=1)
            prev = cur
        # cortina de folhas no topo
        for _ in range(3):
            c = (x0 + (x1 - x0) * rnd.random(), face_y + side * 0.1, z_top - rnd.random() * 0.3)
            r = 0.16 + rnd.random() * 0.1
            b.ico(c, (r, r * 0.5, r * 0.8), leaf_mats[rnd.randrange(len(leaf_mats))], jit=0.25, seed=rnd.randrange(9999), sub=1)


def muralha2_hera(name='Muralha2Hera'):
    """Segmento de muralha igual ao Muralha2, com hera descendo dos dois lados e musgo no pé."""
    _m(); t = T
    r = muralha2(name)
    rnd = random.Random(77)
    b = B()
    Dp = 1.8
    ivy(b, -1.9, 1.9, Dp / 2 + 0.05, 1, 5.0, rnd, vines=4)
    ivy(b, -1.8, 1.0, -Dp / 2 - 0.05, -1, 4.9, rnd, vines=2)
    moss = mat('T2Moss', '#3b4a26')
    for k in range(6):                                                           # musgo no pé da muralha
        x = -1.9 + k * 0.75 + rnd.random() * 0.3
        for side in (-1, 1):
            cbox(b, (x, side * (Dp / 2 + 0.03), 0.12 + rnd.random() * 0.1), (0.5 + rnd.random() * 0.4, 0.12, 0.25 + rnd.random() * 0.2), moss, bev=0.05)
    body = next(k for k in r.children if k.type == 'MESH')
    b.obj(name + '.Hera', _coll(), body, (0, 0, 0), 'Hera')
    return r


# ================================================================ silhueta: ombreiras arqueadas, capas com dobras
def _islands(bm):
    seen, out = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, isl = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop(); isl.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index); stack.append(h)
        out.append(isl)
    return out


def shape_pass(root):
    """Deforma ilhas por posição/material: ombreiras (|x| > 0,17, z > 1,28 na Armadura) ganham arco e caem para fora;
    capas e mantos (material 'cape'/'Cape'/'Veil'/'Robe') ganham dobras verticais; saias de placas se abrem."""
    for k in [c for c in root.children_recursive if c.type == 'MESH']:
        me = k.data
        mats = [m.name.lower() if m else '' for m in me.materials]
        bm = bmesh.new(); bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        part = k.get('part', '')
        armor = root.name.endswith('_Armadura')
        for isl in _islands(bm):
            vs = {v for f in isl for v in f.verts}
            if len(vs) < 8:
                continue
            xs = [v.co.x for v in vs]; ys = [v.co.y for v in vs]; zs = [v.co.z for v in vs]
            cx, cy, cz = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2
            hx, hy, hz = (max(xs) - min(xs)) / 2, (max(ys) - min(ys)) / 2, (max(zs) - min(zs)) / 2
            mname = mats[isl[0].material_index] if isl[0].material_index < len(mats) else ''
            wz = k.matrix_world.translation.z
            if armor and abs(cx) > 0.17 and cz + wz > 1.25 and hz < 0.12 and hx > 0.04:
                # ombreira: arco no sentido da frente para trás e caída para fora
                side = 1 if cx > 0 else -1
                for v in vs:
                    u = (v.co.y - cy) / max(hy, 1e-4)
                    w = (v.co.x - cx) / max(hx, 1e-4) * side
                    v.co.z += (1 - u * u) * hx * 0.28 - max(0.0, w) * hx * 0.18
            elif any(t in mname for t in ('cape', 'veil', 'robe', 'cloak')) and hz > 0.18:
                # capa/manto: dobras verticais que abrem para baixo
                for v in vs:
                    down = max(0.0, (cz + hz - v.co.z) / max(2 * hz, 1e-4))
                    v.co.y += math.sin((v.co.x - cx) / max(hx, 1e-4) * math.pi * 2.5) * 0.022 * (0.3 + down)
                    v.co.x += (v.co.x - cx) * 0.12 * down
        if root.name.endswith('_Elmo'):
            # o elmo arredondado encolhe nos cantos: alarga um pouco para a cabeça (também arredondada) não aparecer
            xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]; zs = [v.co.z for v in bm.verts]
            c = ((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2)
            for v in bm.verts:
                v.co.x = c[0] + (v.co.x - c[0]) * 1.09
                v.co.y = c[1] + (v.co.y - c[1]) * 1.09
                v.co.z = c[2] + (v.co.z - c[2]) * 1.04
        bm.to_mesh(me); bm.free()
