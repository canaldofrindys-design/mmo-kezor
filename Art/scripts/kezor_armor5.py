# Kezor — armaduras v5: placas ajustadas ao corpo contínuo do herói v4 (estilo MMO clássico: peitoral com relevo,
# ombreiras em camadas, saiote de placas, braçais, manoplas, coxotes, joelheiras, grevas e elmo com crista), frisos
# dourados e runas emissivas (alfa < 0,5 na cor do vértice). Usa as funções de kezor_hero4 (mesmo namespace KZ).
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

STEEL = '#a7b0ba'
STEEL_D = '#5f6873'
GOLD = '#d1a445'
RUNE = '#5fd4ff'
LEATHER_A = '#4a3020'

SETS5 = {
    # nome: (aço, aço escuro, friso, runa)
    'Runica': ('#8b96a3', '#3f4751', '#c99a3c', '#5fd4ff'),
}


def _ang(p):
    return math.degrees(math.atan2(p.x, -p.y))          # 0 = frente, +90 = esquerda


def _g(x, s):
    return math.exp(-(x / s) ** 2)


def hi_body():
    """Corpo em alta resolução (remesh de voxel) só para recortar as placas com bordas limpas."""
    G = bpy.app.driver_namespace['KZ']
    keep = bpy.data.objects.get('KZ_HeroiNovo')
    if keep is not None:
        keep.name = 'KZ_HeroiCorpo4'                     # protege o corpo já pronto
    hi = G['assemble'](None)
    hi.name = 'KZ_CorpoHi'
    hi.hide_render = True
    return hi


def plates(hi):
    G = bpy.app.driver_namespace['KZ']
    rc, sm, is_arm = G['_region_copy'], G['_smooth'], G['_is_arm']
    P = {}
    # ---------------------------------------------------------------- peitoral + costas (Body)
    def chest_keep(p):
        return (not is_arm(p)) and 0.885 < p.z < 1.445

    def chest_extra(p):
        e = 0.0
        if p.y < 0:
            e += 0.010 * _g(p.x, 0.016) * sm(0.95, 1.05, p.z)           # crista central
            e += 0.008 * _g(abs(p.x) - 0.075, 0.05) * _g(p.z - 1.30, 0.06)  # relevo dos peitorais
            e += 0.004 * _g(abs(p.x) - 0.04, 0.025) * _g(p.z - 1.13, 0.05)  # abdome em placas
        e -= 0.006 * _g(p.z - 1.215, 0.006) * (1 if p.y < 0 else 0)        # sulco abaixo do peito
        return e + 0.01 * sm(1.0, 0.885, p.z)                                # abre embaixo, sobre o saiote
    P['peito'] = rc(hi, 'KZ5_Peito', chest_keep, 0.036, chest_extra, solid=0.008, cuts=(0.885, 1.445))
    # gorjal
    P['gorjal'] = rc(hi, 'KZ5_Gorjal', lambda p: (not is_arm(p)) and 1.40 < p.z < 1.50, 0.03,
                     lambda p: 0.006 * sm(1.48, 1.42, p.z), solid=0.008, cuts=(1.40, 1.50))
    # ombreiras: três lâminas sobrepostas por lado, cada uma mais alta e mais aberta
    for side, sgn in (('L', 1), ('R', -1)):
        for i, (z0, off, x0) in enumerate(((1.25, 0.05, 0.15), (1.31, 0.068, 0.17), (1.36, 0.086, 0.19))):
            def keep(p, z0=z0, x0=x0, sgn=sgn):
                return p.x * sgn > x0 and p.z > z0 and p.z < 1.50 and abs(p.y) < 0.13
            def extra(p, i=i, x0=x0, sgn=sgn):
                lift = (p.x * sgn - x0) * (0.10 + 0.05 * i)                     # aba abre para fora
                return lift + 0.006 * _g(p.y, 0.02) * (1 if i == 2 else 0)       # nervura na lâmina de cima
            P['omb%s%d' % (side, i)] = rc(hi, 'KZ5_Ombro%s%d' % (side, i), keep, off, extra, solid=0.007,
                                          cuts=(z0,))
    # saiote: 9 placas com vão entre elas
    def tasset_keep(p):
        if is_arm(p) or not (0.70 < p.z < 0.93):
            return False
        a = (_ang(p) + 180 + 20) % 40
        return 3 < a < 37
    P['saiote'] = rc(hi, 'KZ5_Saiote', tasset_keep, 0.05, lambda p: 0.045 * sm(0.92, 0.70, p.z), solid=0.007,
                     cuts=(0.70, 0.93))
    # ---------------------------------------------------------------- braços (ArmL/ArmR)
    for side, sgn in (('L', 1), ('R', -1)):
        P['bracal' + side] = rc(hi, 'KZ5_Bracal' + side, lambda p, s=sgn: is_arm(p) and p.x * s > 0 and 0.80 < p.z < 1.05,
                                0.026, lambda p: 0.008 * sm(0.98, 1.05, p.z), solid=0.006, cuts=(0.80, 1.05))
        P['manopla' + side] = rc(hi, 'KZ5_Manopla' + side, lambda p, s=sgn: is_arm(p) and p.x * s > 0 and p.z <= 0.815,
                                 0.012, lambda p: 0.004 * _g(p.z - 0.78, 0.01), solid=0.004)
        P['cotovelo' + side] = rc(hi, 'KZ5_Cotovelo' + side,
                                  lambda p, s=sgn: is_arm(p) and p.x * s > 0 and 1.03 < p.z < 1.14 and p.y > 0.0,
                                  0.032, lambda p: 0.012 * _g(p.z - 1.085, 0.03), solid=0.006, cuts=(1.03, 1.14))
        P['braco' + side] = rc(hi, 'KZ5_Braco' + side, lambda p, s=sgn: is_arm(p) and p.x * s > 0 and 1.14 < p.z < 1.26,
                               0.022, None, solid=0.006, cuts=(1.14, 1.26))
    # ---------------------------------------------------------------- pernas (LegL/LegR)
    for side, sgn in (('L', 1), ('R', -1)):
        P['coxote' + side] = rc(hi, 'KZ5_Coxote' + side,
                                lambda p, s=sgn: (not is_arm(p)) and p.x * s > 0.01 and 0.58 < p.z < 0.84 and p.y < 0.03,
                                0.022, lambda p: 0.006 * _g(p.x - 0.11 * (1 if p.x > 0 else -1), 0.02), solid=0.006,
                                cuts=(0.58, 0.84))
        P['joelho' + side] = rc(hi, 'KZ5_Joelho' + side,
                                lambda p, s=sgn: (not is_arm(p)) and p.x * s > 0.01 and 0.47 < p.z < 0.61 and p.y < 0.02,
                                0.034, lambda p: 0.016 * _g(p.z - 0.54, 0.035) * _g(p.y + 0.06, 0.04), solid=0.007,
                                cuts=(0.47, 0.61))
        P['greva' + side] = rc(hi, 'KZ5_Greva' + side,
                               lambda p, s=sgn: (not is_arm(p)) and p.x * s > 0.01 and 0.11 < p.z < 0.47,
                               0.03, lambda p: 0.006 * _g(p.x - 0.12 * (1 if p.x > 0 else -1), 0.012) * (1 if p.y < 0 else 0),
                               solid=0.006, cuts=(0.11, 0.47))
        P['sapato' + side] = rc(hi, 'KZ5_Sapato' + side,
                                lambda p, s=sgn: (not is_arm(p)) and p.x * s > 0.01 and p.z <= 0.12,
                                0.02, lambda p: 0.004 * sm(0.02, 0.0, p.z), solid=0.005)
    # ---------------------------------------------------------------- elmo (Head): rosto aberto em T, crista
    def helm_keep(p):
        if p.z < 1.575:
            return False
        face = p.y < -0.025 and 1.585 < p.z < 1.735 and abs(p.x) < 0.058
        brow = p.y < -0.025 and 1.69 < p.z < 1.735 and abs(p.x) < 0.07
        return not (face or brow)

    def helm_extra(p):
        crest = 0.03 * _g(p.x, 0.010) * sm(1.70, 1.80, p.z)
        cheek = 0.008 * _g(abs(p.x) - 0.06, 0.02) * _g(p.z - 1.63, 0.04) * (1 if p.y < 0 else 0)
        return crest + cheek + 0.006 * sm(1.74, 1.80, p.z)
    P['elmo'] = rc(hi, 'KZ5_Elmo', helm_keep, 0.026, helm_extra, solid=0.008, cuts=(1.575,))
    return P


# ================================================================ pintura (aço, friso dourado, runas emissivas)
def paint5(o, fn):
    G = bpy.app.driver_namespace['KZ']
    sm = G['_smooth']
    me = o.data
    attr = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    me.color_attributes.active_color = attr
    vc = []
    # bordas: vértices perto da borda original ficam dourados (distância até a borda por propagação)
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    dist = [9.0] * len(bm.verts)
    front = []
    for v in bm.verts:
        if v.is_boundary or any((not e.is_manifold) or e.calc_face_angle(0.0) > 1.25 for e in v.link_edges):   # borda ou quina da espessura
            dist[v.index] = 0.0
            front.append(v)
    for _ in range(1):
        nxt = []
        for v in front:
            for e in v.link_edges:
                w = e.other_vert(v)
                d = dist[v.index] + e.calc_length()
                if d < dist[w.index]:
                    dist[w.index] = d
                    nxt.append(w)
        front = nxt
    bm.free()
    for v in me.vertices:
        c, a = fn(v.co, v.normal, dist[v.index])
        k = 0.82 + 0.18 * max(0.0, v.normal.z) + 0.08 * sm(0.0, 1.8, v.co.z)
        if a < 0.5:
            k = 1.0
        vc.append((min(1, c[0] * k), min(1, c[1] * k), min(1, c[2] * k), a))
    idx = [0] * len(me.loops)
    me.loops.foreach_get('vertex_index', idx)
    flat = []
    for i in idx:
        flat.extend(vc[i])
    attr.data.foreach_set('color_srgb', flat)


def paint_set(P, set_name='Runica'):
    G = bpy.app.driver_namespace['KZ']
    hx, mix, sm = G['_hex'], G['_mix'], G['_smooth']
    st, sd, gd, rn = [hx(c) for c in SETS5[set_name]]

    def plate(p, n, d, rune_fn=None):
        if d < 0.004:
            return gd, 1.0                                   # friso dourado na borda
        if rune_fn and rune_fn(p):
            return rn, 0.0                                   # runa acesa
        c = mix(sd, st, 0.35 + 0.65 * max(0.0, n.z * 0.5 + 0.5) * sm(0.0, 0.03, d + 0.01))   # aço: claro em cima, escuro embaixo e perto do friso
        return c, 1.0

    chest_rune = lambda p: p.y < 0 and abs(p.x) < 0.006 and 1.0 < p.z < 1.36 or (p.y < 0 and abs(math.hypot(p.x, p.z - 1.255) - 0.028) < 0.004)
    for k, o in P.items():
        if k == 'peito':
            paint5(o, lambda p, n, d: plate(p, n, d, chest_rune))
        elif k.startswith('omb'):
            paint5(o, lambda p, n, d: plate(p, n, d, lambda q: abs(q.y) < 0.004 and k.endswith('2')))
        elif k == 'elmo':
            paint5(o, lambda p, n, d: plate(p, n, d, lambda q: abs(q.x) < 0.004 and q.y < -0.03 and q.z > 1.74))
        elif k.startswith('greva') or k.startswith('bracal'):
            paint5(o, lambda p, n, d: plate(p, n, d, lambda q: abs(abs(q.x) - (0.12 if k.startswith('greva') else 0.31)) < 0.004 and q.y < -0.02))
        elif k.startswith('manopla'):
            paint5(o, lambda p, n, d: (mix(hx(LEATHER_A), sd, 0.3), 1.0) if p.z < 0.74 else plate(p, n, d))
        else:
            paint5(o, lambda p, n, d: plate(p, n, d))


BUDGET5 = dict(peito=5000, gorjal=600, saiote=2400, elmo=3200)


def finish(P):
    G = bpy.app.driver_namespace['KZ']
    for k, o in P.items():
        n = BUDGET5.get(k, 900 if (k.startswith('omb') or k.startswith('joelho') or k.startswith('cotovelo')) else 1300)
        G['decimate'](o, n)
        for p in o.data.polygons:
            p.use_smooth = True
        try:
            o.data.set_sharp_from_angle(angle=math.radians(50))
        except Exception:
            pass


def build_armor5(set_name='Runica'):
    hi = hi_body()
    P = plates(hi)
    finish(P)
    paint_set(P, set_name)
    return P


# ================================================================ exportação (Equip/<Set>_<Peça>.fbx)
GROUPS5 = {
    'Armadura': (('peito', 'gorjal', 'saiote', 'ombL0', 'ombL1', 'ombL2', 'ombR0', 'ombR1', 'ombR2'), (0, 0, 0)),
    'LuvaL': (('bracalL', 'manoplaL', 'cotoveloL', 'bracoL'), (0.31, 0, 1.40)),
    'LuvaR': (('bracalR', 'manoplaR', 'cotoveloR', 'bracoR'), (-0.31, 0, 1.40)),
    'CalcaL': (('coxoteL', 'joelhoL'), (0.12, 0, 0.92)),
    'CalcaR': (('coxoteR', 'joelhoR'), (-0.12, 0, 0.92)),
    'BotaL': (('grevaL', 'sapatoL'), (0.12, 0, 0.92)),
    'BotaR': (('grevaR', 'sapatoR'), (-0.12, 0, 0.92)),
    'Elmo': (('elmo',), (0, 0, 1.47)),
}


def export_set5(P, set_name='Runica'):
    G = bpy.app.driver_namespace['KZ']
    out = []
    for piece, (keys, pivot) in GROUPS5.items():
        name = '%s_%s' % (set_name, piece)
        for n in ('__a5_root', 'Piece'):
            G['_kill'](n)
        objs = [G['_copy'](P[k], 3) for k in keys]
        # runas (alfa < 0,5) sem textura de detalhe
        mesh = G['_join'](objs, 'Piece')
        me = mesh.data
        col = me.color_attributes.get('Col')
        n = len(me.loops)
        c = [0.0] * (n * 4)
        col.data.foreach_get('color_srgb', c)
        uv = [0.0] * (n * 2)
        me.uv_layers[1].data.foreach_get('uv', uv)
        for i in range(n):
            if c[i * 4 + 3] < 0.5:
                uv[i * 2] = 0.0
        me.uv_layers[1].data.foreach_set('uv', uv)
        G['_bake_ao']([mesh])
        mesh.data.transform(Matrix.Translation(-Vector(pivot)))
        root = G['_link'](bpy.data.objects.new('__a5_root', None))
        root['kezor_root'] = True
        root['category'] = 'Equip'
        mesh.parent = root
        mesh.location = pivot
        mesh['part'] = 'Piece'
        mesh['pivot'] = pivot
        bpy.context.view_layer.update()
        path = os.path.join(G['OUT'], 'Equip', name + '.fbx')
        saved = G['prebake']([mesh])
        bpy.context.view_layer.update()
        for x in bpy.context.view_layer.objects:
            x.select_set(False)
        mesh.select_set(True)
        bpy.context.view_layer.objects.active = mesh
        try:
            bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
                                     global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                     bake_space_transform=False, axis_forward='-Z', axis_up='Y',
                                     mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False,
                                     bake_anim=False, path_mode='COPY', embed_textures=True, use_custom_props=False,
                                     colors_type='SRGB')
        finally:
            G['unbake']([mesh], saved)
        out.append((name, os.path.getsize(path), sum(len(p.vertices) - 2 for p in me.polygons)))
        bpy.data.objects.remove(mesh, do_unlink=True)
        bpy.data.objects.remove(root, do_unlink=True)
    return out
