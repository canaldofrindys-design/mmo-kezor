# Kezor — heróis v2: corpos base com mais detalhe (low poly estilizado, ~2-4k tris por herói), peças de equipamento
# com chanfro em todas as arestas e oclusão de ambiente (AO) assada nas cores de vértice na exportação.
# Mantém os pivôs (PIV) e as partes (Body, Head, ArmL, ArmR, LegL, LegR): o equipamento visível e as animações
# procedurais continuam funcionando. Requer kezor_lib, kezor_humans, kezor_monsters, kezor_env, kezor_equip,
# kezor_classes2 e kezor_export executados antes no mesmo namespace.
import bmesh, bpy, math, random
from mathutils import Vector


# ================================================================ tons e formas
def tone(m, f, suffix=None):
    """Material no mesmo tom, mais claro (f > 1) ou mais escuro (f < 1). Guarda kezor_hex para a cor de vértice."""
    hx = (m.get('kezor_hex') or '#888888').lstrip('#')
    rgb = [min(255, max(0, int(int(hx[i:i + 2], 16) * f))) for i in (0, 2, 4)]
    nh = '#%02x%02x%02x' % tuple(rgb)
    name = m.name + (suffix or ('_L%d' % int(f * 100) if f > 1 else '_D%d' % int(f * 100)))
    ex = bpy.data.materials.get(name)
    if ex is not None:
        return ex
    return mat(name, nh)


def cbox(b, c, size, m, bev=0.018, top=(1, 1), shift=(0, 0), mtop=None):
    """Caixa com todas as arestas chanfradas (8 lados por anel, 4 anéis). top escala o topo; shift desloca o topo."""
    cx, cy, cz = c
    sx, sy, sz = [v / 2 for v in size]
    bev = min(bev, sx * 0.45, sy * 0.45, sz * 0.45)
    tx, ty = top
    ox, oy = shift

    def ring(z, kx, ky, inset, dx=0.0, dy=0.0):
        ax, ay = sx * kx - inset, sy * ky - inset
        e = bev
        pts = [(-ax + e, -ay), (ax - e, -ay), (ax, -ay + e), (ax, ay - e), (ax - e, ay), (-ax + e, ay), (-ax, ay - e), (-ax, -ay + e)]
        return [(cx + dx + x, cy + dy + y, z) for x, y in pts]

    z0, z1 = cz - sz, cz + sz
    rings = [ring(z0, 1, 1, bev * 0.7), ring(z0 + bev, 1, 1, 0),
             ring(z1 - bev, tx, ty, 0, ox, oy), ring(z1, tx, ty, bev * 0.7, ox, oy)]
    pts = [p for r in rings for p in r]
    faces, ms = [], []
    for k in range(3):
        for i in range(8):
            j = (i + 1) % 8
            faces.append((k * 8 + i, k * 8 + j, (k + 1) * 8 + j, (k + 1) * 8 + i)); ms.append(m)
    faces.append(tuple(reversed(range(8)))); ms.append(m)
    faces.append(tuple(range(24, 32))); ms.append(mtop or m)
    return b.poly(pts, faces, ms)


def wedge(b, c, w, d, h, m):
    """Cunha (nariz): base retangular atrás, aresta na frente."""
    cx, cy, cz = c
    pts = [(cx - w / 2, cy, cz - h / 2), (cx + w / 2, cy, cz - h / 2), (cx + w / 2, cy, cz + h / 2), (cx - w / 2, cy, cz + h / 2),
           (cx, cy - d, cz - h / 2 + 0.004), (cx, cy - d * 0.4, cz + h / 2)]
    faces = [(0, 3, 2, 1), (0, 1, 4), (1, 2, 5, 4), (2, 3, 5), (3, 0, 4, 5)]
    return b.poly(pts, faces, m)


# ================================================================ partes do corpo
EYE_W = mat('EyeWhite', '#e9e4d8')
EYE_I = mat('EyeIris', '#2a2018')
MOUTH = mat('Mouth', '#6a3a2c')


def body2(b, shirt, belt, hips, buckle, w=1.0, skin=None):
    shirtL, shirtD = tone(shirt, 1.18), tone(shirt, 0.72)
    beltD = tone(belt, 0.7)
    cbox(b, (0, 0, 0.99), (0.36 * w, 0.22, 0.17), hips, bev=0.02)                           # quadril
    cbox(b, (0, 0, 1.12), (0.35 * w, 0.205, 0.12), shirtD, bev=0.02, top=(1.08, 1.05))     # cintura
    cbox(b, (0, 0, 1.27), (0.42 * w, 0.235, 0.22), shirt, bev=0.03, top=(1.1, 1.0))        # peito
    cbox(b, (0, 0.005, 1.39), (0.36 * w, 0.2, 0.05), shirt, bev=0.015, top=(0.7, 0.85))    # ombros/trapézio
    for x in (-0.095, 0.095):                                                               # placas do peito
        cbox(b, (x * w, -0.112, 1.28), (0.15 * w, 0.03, 0.15), shirtL, bev=0.012, top=(1.0, 0.8))
    cbox(b, (0, -0.112, 1.15), (0.08, 0.02, 0.1), shirtD, bev=0.008)                        # abdome
    cbox(b, (0, 0.115, 1.25), (0.3 * w, 0.025, 0.24), shirtD, bev=0.01)                     # costas
    b.cyl((0, 0, 1.385), 0.085, 0.07, 0.04, 8, shirtD)                                      # gola
    # cinto com fivela, bolsa e alça
    cbox(b, (0, 0, 1.07), (0.39 * w, 0.24, 0.06), belt, bev=0.012)
    cbox(b, (0, -0.124, 1.07), (0.07, 0.016, 0.05), buckle, bev=0.006)
    cbox(b, (0, -0.131, 1.07), (0.035, 0.008, 0.024), beltD, bev=0.003)
    cbox(b, (0.16 * w, -0.06, 1.02), (0.07, 0.07, 0.08), beltD, bev=0.01)                   # bolsa
    cbox(b, (0.16 * w, -0.098, 1.045), (0.06, 0.012, 0.03), belt, bev=0.004)
    cbox(b, (-0.17 * w, 0.04, 1.03), (0.03, 0.06, 0.1), belt, bev=0.006)                    # fivela lateral


def arm2(b, x, sleeve, fore, hand, w=1.0, bracer=None):
    sl, sd = tone(sleeve, 1.15), tone(sleeve, 0.75)
    s = 1 if x > 0 else -1
    b.lathe((x, 0, 1.31), [(0.0, 0.11), (0.06, 0.1), (0.085, 0.06), (0.085, 0.0)], 8, sl)   # ombro
    cbox(b, (x, 0, 1.2), (0.13 * w, 0.14 * w, 0.28), sleeve, bev=0.02, top=(1.12, 1.1))     # braço
    b.cyl((x, 0, 1.055), 0.058 * w, 0.058 * w, 0.03, 8, sd)                                 # cotovelo
    cbox(b, (x, 0, 0.94), (0.115 * w, 0.125 * w, 0.2), fore, bev=0.018, top=(1.16, 1.1))     # antebraço
    if bracer is not None:
        cbox(b, (x, 0, 0.84), (0.122 * w, 0.132 * w, 0.07), bracer, bev=0.01)               # braçadeira
        cbox(b, (x, -0.066, 0.84), (0.05, 0.008, 0.035), tone(bracer, 1.3), bev=0.004)
    hd = tone(hand, 0.82)
    cbox(b, (x, -0.003, 0.752), (0.094 * w, 0.104 * w, 0.085), hand, bev=0.014)             # palma
    cbox(b, (x, -0.016, 0.69), (0.088 * w, 0.09 * w, 0.055), hd, bev=0.012, top=(0.92, 0.85), shift=(0, 0.01))  # dedos
    b.seg((x - s * 0.036, -0.04, 0.755), (x - s * 0.046, -0.06, 0.705), 0.026, hand)       # polegar


def leg2(b, x, pants, shin, boot, cuff=None):
    pd = tone(pants, 0.78)
    bootL, bootD = tone(boot, 1.2), tone(boot, 0.65)
    cbox(b, (x, 0, 0.72), (0.17, 0.19, 0.36), pants, bev=0.022, top=(1.1, 1.06))            # coxa
    cbox(b, (x, -0.075, 0.52), (0.11, 0.05, 0.1), pd, bev=0.012)                            # joelho
    cbox(b, (x, 0, 0.35), (0.14, 0.16, 0.26), shin, bev=0.018, top=(1.15, 1.1))             # canela
    cbox(b, (x, 0, 0.19), (0.152, 0.172, 0.14), boot, bev=0.016)                            # cano da bota
    cbox(b, (x, 0, 0.275), (0.168, 0.188, 0.045), cuff or bootL, bev=0.012)                 # dobra
    cbox(b, (x, -0.045, 0.065), (0.148, 0.26, 0.11), boot, bev=0.02, top=(0.94, 0.82), shift=(0, 0.025))  # pé
    cbox(b, (x, -0.165, 0.05), (0.13, 0.04, 0.07), bootL, bev=0.01)                         # biqueira
    cbox(b, (x, -0.04, 0.008), (0.156, 0.29, 0.018), bootD, bev=0.005)                      # sola


HAIRSTYLES = ('short', 'long', 'tail', 'band', 'spiky', 'crop')


def head2(b, skin, hair=None, style='short', eyes=EYE_I, beard=False):
    sd, sl = tone(skin, 0.84), tone(skin, 1.07)
    b.cyl((0, 0, 1.35), 0.056, 0.05, 0.13, 8, sd)                                           # pescoço
    cbox(b, (0, 0.004, 1.615), (0.222, 0.242, 0.21), skin, bev=0.035, top=(0.92, 0.93))     # crânio
    cbox(b, (0, -0.012, 1.5), (0.18, 0.2, 0.08), skin, bev=0.025, top=(1.18, 1.1))         # maxilar
    cbox(b, (0, -0.1, 1.478), (0.075, 0.035, 0.035), sl, bev=0.01)                          # queixo
    cbox(b, (0, -0.118, 1.648), (0.168, 0.02, 0.022), sd, bev=0.006)                        # sobrancelha (arco)
    wedge(b, (0, -0.119, 1.585), 0.034, 0.032, 0.06, sl)                                    # nariz
    for xs in (-1, 1):
        cbox(b, (xs * 0.05, -0.12, 1.622), (0.046, 0.01, 0.026), EYE_W, bev=0.003)          # olho
        cbox(b, (xs * 0.046, -0.126, 1.62), (0.018, 0.006, 0.02), eyes, bev=0.002)         # íris
        cbox(b, (xs * 0.115, 0.012, 1.6), (0.022, 0.05, 0.07), sd, bev=0.006)               # orelha
        cbox(b, (xs * 0.07, -0.106, 1.555), (0.03, 0.025, 0.03), sl, bev=0.006)             # maçã do rosto
        if hair is not None:
            b.tf(loc=(xs * 0.052, -0.124, 1.656), rot=(0, xs * -10, 0))
            cbox(b, (0, 0, 0), (0.05, 0.012, 0.014), tone(hair, 0.8), bev=0.003)            # sobrancelha
            b.reset()
    cbox(b, (0, -0.119, 1.532), (0.05, 0.006, 0.009), MOUTH, bev=0.002)                     # boca
    if beard and hair is not None:
        cbox(b, (0, -0.07, 1.47), (0.17, 0.1, 0.07), hair, bev=0.02, top=(0.7, 0.8))
    if hair is None:
        return
    hd, hl = tone(hair, 0.75), tone(hair, 1.2)
    if style in ('short', 'crop', 'band'):
        cbox(b, (0, 0.012, 1.712), (0.238, 0.258, 0.06), hair, bev=0.025, top=(0.85, 0.88))
        cbox(b, (0, 0.1, 1.62), (0.236, 0.07, 0.17), hair, bev=0.02)
        for xs in (-1, 1):
            cbox(b, (xs * 0.115, 0.04, 1.65), (0.02, 0.1, 0.09), hd, bev=0.006)
        cbox(b, (0.03, -0.108, 1.695), (0.15, 0.04, 0.035), hl, bev=0.012)                 # franja
    if style == 'long':
        cbox(b, (0, 0.012, 1.712), (0.244, 0.262, 0.07), hair, bev=0.025, top=(0.85, 0.88))
        cbox(b, (0, 0.11, 1.48), (0.25, 0.08, 0.42), hair, bev=0.03, top=(0.9, 1.0))
        for xs in (-1, 1):
            cbox(b, (xs * 0.118, 0.0, 1.55), (0.03, 0.15, 0.24), hd, bev=0.01, top=(1.0, 0.8))
        cbox(b, (0, -0.105, 1.7), (0.2, 0.04, 0.04), hl, bev=0.012)
    if style == 'tail':
        cbox(b, (0, 0.012, 1.712), (0.236, 0.256, 0.06), hair, bev=0.025, top=(0.85, 0.88))
        cbox(b, (0, 0.1, 1.63), (0.234, 0.07, 0.15), hair, bev=0.02)
        b.seg((0, 0.15, 1.66), (0, 0.22, 1.36), 0.07, hair, w2=0.03)                         # rabo de cavalo
        b.cyl((0, 0.14, 1.64), 0.03, 0.03, 0.03, 6, hd)
    if style == 'spiky':
        cbox(b, (0, 0.012, 1.705), (0.214, 0.236, 0.06), hair, bev=0.022, top=(0.9, 0.9))
        cbox(b, (0, 0.1, 1.62), (0.232, 0.07, 0.16), hair, bev=0.02)
        rnd = random.Random(7)
        for row, (yy, n) in enumerate(((-0.06, 4), (0.02, 5), (0.09, 4))):
            for i in range(n):
                xx = (i - (n - 1) / 2) * 0.05
                b.tf(loc=(xx, yy, 1.725), rot=(-25 + row * 25, xx * 160, 0))
                b.cone((0, 0, 0), 0.032, 0.07 + rnd.random() * 0.035, 4, hl if (i + row) % 2 else hair)
                b.reset()
    if style == 'band':
        cbox(b, (0, 0.0, 1.668), (0.244, 0.262, 0.03), tone(hair, 0.5, '_band'), bev=0.008)


# ================================================================ corpo base v2 (substitui base_body)
LOOKS = {   # estilo de cabelo, barba, tom de pele e braçadeira por modelo
    'GuardiaoRunicoBase': dict(style='short', beard=True),
    'ArcanistaDoVeuBase': dict(style='long'),
    'PatrulheiroSelvagemBase': dict(style='tail'),
    'LaminaArcanaBase': dict(style='spiky'),
    'ComandanteEstandarteBase': dict(style='crop', beard=True),
    'InvocadoraEcosBase': dict(style='long'),
    'PunhoBrasaBase': dict(style='band', bare=True),
}

SKIN2 = mat('Skin2', '#d4a27c')


def base_body(name, shirt, pants, boots, hair, extra=None):
    look = LOOKS.get(name, {})
    P = hparts()
    b = P['Body']
    body2(b, shirt, LEATHER, pants, GOLD)
    sleeve = SKIN2 if look.get('bare') else shirt
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        arm2(P[part], 0.31 * side, sleeve, SKIN2, SKIN2, bracer=LEATHER_L)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        leg2(P[part], 0.12 * side, pants, pants, boots)
    head2(P['Head'], SKIN2, hair=hair, style=look.get('style', 'short'), beard=look.get('beard', False))
    if extra:
        extra(P)
    coll = collection('Classes')
    r = finish(name, 'Classes', coll, P)
    if name not in EQUIP_ITEMS:
        EQUIP_ITEMS.append(name)
    return r


# ================================================================ peças de equipamento com chanfro
_piece_v1 = piece


def piece(name, pivot, build, cat='Equip', coll=None):
    """Igual à peça v1, mas chanfra todas as arestas vivas antes de criar a malha (brilho nas quinas)."""
    def build2(b):
        build(b)
        bm = b.bm
        bm.normal_update()
        edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(35)]
        if edges:
            bmesh.ops.bevel(bm, geom=edges, offset=0.0045, segments=1, affect='EDGES', clamp_overlap=True, profile=0.5)
            # faces do chanfro herdam o material da face vizinha; garante índice válido
            for f in bm.faces:
                if f.material_index >= len(b.mats):
                    f.material_index = 0
    return _piece_v1(name, pivot, build2, cat, coll)


# ================================================================ AO nas cores de vértice
def bake_ao(roots, samples=24, distance=0.22):
    """Assa a oclusão de ambiente (Cycles) num atributo 'AO' por canto de face de cada malha dos modelos."""
    sc = bpy.context.scene
    old_engine = sc.render.engine
    try:
        sc.render.engine = 'CYCLES'
    except TypeError:
        return 0
    sc.cycles.samples = samples
    try:
        sc.cycles.device = 'CPU'
    except Exception:
        pass
    world = sc.world
    if world is not None and hasattr(world, 'light_settings'):
        world.light_settings.distance = distance
    n = 0
    for r in roots:
        kids = [k for k in r.children_recursive if k.type == 'MESH' and len(k.data.polygons)]
        if not kids:
            continue
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        for k in kids:
            me = k.data
            a = me.color_attributes.get('AO') or me.color_attributes.new('AO', 'BYTE_COLOR', 'CORNER')
            me.color_attributes.active_color = a
            k.select_set(True)
        bpy.context.view_layer.objects.active = kids[0]
        try:
            bpy.ops.object.bake(type='AO', target='VERTEX_COLORS', margin=0)
            n += len(kids)
        except Exception as e:
            print('AO falhou em', r.name, e)
    sc.render.engine = old_engine
    return n


def bake_vertex_colors(obj):
    """Cor do material × AO assada × variação leve por face (3-4 tons por material). Emissivos ficam inteiros."""
    me = obj.data
    attr = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    ao = me.color_attributes.get('AO')
    cols = [_mat_vcol(m) for m in me.materials] or [(1, 1, 1, 1)]
    for poly in me.polygons:
        c = cols[min(poly.material_index, len(cols) - 1)]
        h = ((poly.index * 2654435761) & 0xffff) / 65535.0
        var = 1.0 + (h - 0.5) * 0.09                                    # ±4,5% por face
        up = poly.normal.z
        var *= 1.0 + 0.06 * up                                           # topo um pouco mais claro
        for li in poly.loop_indices:
            if c[3] < 1.0:
                attr.data[li].color_srgb = c
                continue
            o = ao.data[li].color[0] if ao is not None else 1.0
            k = var * (0.7 + 0.3 * o)
            attr.data[li].color_srgb = (min(1, c[0] * k), min(1, c[1] * k), min(1, c[2] * k), 1.0)
    if ao is not None:
        me.color_attributes.remove(ao)        # o FBX leva só 'Col' (o Unity lê a primeira camada de cor)
        attr = me.color_attributes.get('Col')
    me.color_attributes.active_color = attr
    try:
        me.color_attributes.render_color_index = me.color_attributes.find('Col')
    except Exception:
        pass


def export_v2(names):
    roots = [bpy.data.objects[n] for n in names if bpy.data.objects.get(n)]
    baked = bake_ao(roots)
    return baked, export_all(only=names)


def rebuild_heroes():
    """Refaz os 7 corpos base com o construtor v2 (os extras de cada classe continuam os mesmos)."""
    bases()
    C2_ITEMS.clear()
    bases2()
    return ['GuardiaoRunicoBase', 'ArcanistaDoVeuBase', 'PatrulheiroSelvagemBase', 'LaminaArcanaBase',
            'ComandanteEstandarteBase', 'InvocadoraEcosBase', 'PunhoBrasaBase']


# ================================================================ detalhes dos conjuntos (v2)
def _set_of(name):
    for s in SETS:
        if name.startswith(s + '_'):
            return s, name[len(s) + 1:]
    return None, None


def _rivets(b, pts, m, r=0.011):
    for p in pts:
        b.lathe(p, [(0, 0), (r, 0.004), (r * 0.7, 0.009), (0, 0.011)], 6, m, rot0=0)


def detail_piece(name, b):
    """Camada extra de detalhe por tipo de peça, no estilo do conjunto (formas v1 + ombreiras, cristas, rebites...)."""
    s, part = _set_of(name)
    if s is None:
        return
    c1, c2, c3, glow = SETS[s]
    st = STYLE.get(s, s)
    heavy = st in ('Runica',)
    scale = st in ('Escamas',)
    cloth = st in ('Veu',)
    if part == 'Armadura':
        for x in (-1, 1):
            px = x * 0.255
            if cloth:   # manto em camadas sobre os ombros
                cbox(b, (px, 0.0, 1.405), (0.17, 0.26, 0.05), c1, bev=0.015, top=(0.8, 0.9))
                cbox(b, (px * 1.05, 0.0, 1.37), (0.15, 0.27, 0.05), c2, bev=0.012, top=(0.85, 0.9))
                if glow is not None:
                    cbox(b, (px, -0.134, 1.39), (0.12, 0.012, 0.012), glow, bev=0.003)
            else:
                n = 3 if heavy else 2
                for k in range(n):                                            # placas sobrepostas
                    w = (0.2 if heavy else 0.17) - k * 0.012
                    b.tf(loc=(px + x * k * 0.022, 0, 1.43 - k * 0.055), rot=(0, x * (18 + k * 8), 0))
                    cbox(b, (0, 0, 0), (w, 0.27 if heavy else 0.24, 0.05), c1 if k == 0 else (c2 if k % 2 else c1), bev=0.014, top=(0.9, 0.95))
                    b.reset()
                b.tf(loc=(px, 0, 1.46), rot=(0, x * 18, 0))
                cbox(b, (0, 0, 0), (0.14, 0.2, 0.025), c3, bev=0.008)        # friso
                b.reset()
                _rivets(b, [(px + x * 0.07, -0.12, 1.42), (px + x * 0.07, 0.12, 1.42)], c3)
                if heavy and glow is not None:
                    cbox(b, (px + x * 0.02, -0.136, 1.43), (0.1, 0.01, 0.014), glow, bev=0.003)
        cbox(b, (0, 0, 1.43), (0.24, 0.22, 0.05), c2, bev=0.012, top=(0.85, 0.9))           # gorjal
        emb = glow or c3
        b.lathe((0, -0.145, 1.27), [(0, 0), (0.035, 0.0), (0.042, 0.012), (0.03, 0.02), (0, 0.022)], 6, c3, rot0=0, sx=1, sy=0.5)
        b.gem((0, -0.155, 1.28), 0.022, 0.05, emb)                                           # emblema
        cbox(b, (0, 0.14, 1.25), (0.32, 0.03, 0.26), c2, bev=0.012)                          # placa das costas
        if scale:
            for row in range(3):
                for i in range(4):
                    cbox(b, (-0.12 + i * 0.08 + (row % 2) * 0.04, -0.13, 1.2 - row * 0.05), (0.06, 0.012, 0.05), c2 if (i + row) % 2 else c1, bev=0.006, top=(0.6, 1))
    elif part == 'Elmo':
        if heavy:
            cbox(b, (0, 0.0, 1.79), (0.03, 0.26, 0.05), c3, bev=0.008)                        # crista
            for x in (-1, 1):
                cbox(b, (x * 0.125, -0.06, 1.53), (0.025, 0.1, 0.1), c2, bev=0.008)          # malar
            if glow is not None:
                cbox(b, (0, -0.15, 1.63), (0.14, 0.008, 0.014), glow, bev=0.002)
        elif scale:
            cbox(b, (0, -0.15, 1.58), (0.025, 0.02, 0.1), c3, bev=0.006)                      # protetor nasal
            cbox(b, (0, 0.0, 1.76), (0.05, 0.05, 0.05), c3, bev=0.01, top=(0.4, 0.4))
        elif cloth:
            b.seg((0, 0.12, 1.72), (0, 0.26, 1.48), 0.06, c1, w2=0.02)                       # ponta do capuz
            if glow is not None:
                b.gem((0, -0.142, 1.69), 0.018, 0.04, glow)
        else:
            cbox(b, (0, -0.002, 1.64), (0.27, 0.29, 0.025), c2, bev=0.006)                    # tira de couro
        _rivets(b, [(-0.1, -0.14, 1.64), (0.1, -0.14, 1.64)], c3, 0.009)
    elif part.startswith('Luva'):
        x = 0.31 * (1 if part.endswith('L') else -1)
        cbox(b, (x, -0.07, 0.72), (0.1, 0.02, 0.035), c3, bev=0.005)                          # nós dos dedos
        cbox(b, (x, 0, 0.83), (0.15, 0.16, 0.035), c2, bev=0.008, top=(1.1, 1.1))           # punho
        _rivets(b, [(x, -0.082, 0.86)], c3, 0.008)
    elif part.startswith('Bota'):
        x = 0.12 * (1 if part.endswith('L') else -1)
        cbox(b, (x, -0.17, 0.06), (0.14, 0.05, 0.08), c3, bev=0.01)                           # biqueira
        cbox(b, (x, 0, 0.22), (0.18, 0.2, 0.025), c2, bev=0.006)                              # tira
        _rivets(b, [(x, -0.1, 0.22)], c3, 0.008)
    elif part.startswith('Calca'):
        x = 0.12 * (1 if part.endswith('L') else -1)
        b.tf(loc=(x, -0.06, 0.83), rot=(-8, 0, 0))
        cbox(b, (0, 0, 0), (0.17, 0.04, 0.16), c2, bev=0.01, top=(1.1, 1.0))                  # coxote
        b.reset()
        _rivets(b, [(x, -0.085, 0.88)], c3, 0.008)
        cbox(b, (x, -0.1, 0.52), (0.12, 0.05, 0.11), c1 if not cloth else c2, bev=0.012)     # joelheira
        _rivets(b, [(x, -0.126, 0.52)], c3, 0.009)


_piece_v2 = piece


def piece(name, pivot, build, cat='Equip', coll=None):
    def build3(b):
        build(b)
        detail_piece(name, b)
    return _piece_v2(name, pivot, build3, cat, coll)


def rebuild_gear():
    """Refaz conjuntos e armas (v1 + v2) com os detalhes e o chanfro novos."""
    armor_sets()
    sets2()
    weapons()
    weapons2()
    return [n for n in EQUIP_ITEMS if n.startswith(tuple(s + '_' for s in SETS)) or n.startswith(('Arma_', 'Escudo_'))]
