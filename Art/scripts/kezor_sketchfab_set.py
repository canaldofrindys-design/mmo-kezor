# -*- coding: utf-8 -*-
"""Set de armadura vindo do Sketchfab (CC-BY) vestido num corpo de classe MPFB e exportado no formato do jogo.

Fluxo (Blender, com o corpo da classe importado de Models/Classes/<Classe>Base.fbx e a armadura importada):
  1. fit_*        : a armadura (que vem em pose A) desce os braços e se ajusta às medidas do corpo (PA_*).
  2. build_parts  : divide nas peças do jogo (Elmo, Armadura, LuvaL/R, CalcaL/R, BotaL/R), junta as texturas
                    de cada peça num atlas e converte o metal/rugosidade do glTF na máscara do Kezor/LowPoly.
  3. export_parts : pesos do corpo (UV2..UV5), malha "Piece" com o pivô da articulação do modelo e FBX em Equip/.
  4. write_hide   : lista dos vértices do corpo cobertos por cada peça (Resources/Equip/Hide), que o jogo esconde.
"""
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

ROOT = r'C:\MuProtoUnity\Assets\Resources'
# Blender (Z para cima, frente -Y) -> FBX Y para cima: (x, y, z) -> (x, z, -y)
C_YUP = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))
BONE_ORDER = ['Body', 'Chest', 'Head', 'ArmL', 'ForeArmL', 'HandL', 'ArmR', 'ForeArmR', 'HandR',
              'LegL', 'ShinL', 'FootL', 'LegR', 'ShinR', 'FootR']                       # = KezorAnim.BoneNames
ALLOWED = {
    'Armadura': ('Body', 'Chest'), 'Elmo': ('Head',),
    'LuvaL': ('ArmL', 'ForeArmL', 'HandL'), 'LuvaR': ('ArmR', 'ForeArmR', 'HandR'),
    'CalcaL': ('LegL', 'ShinL', 'FootL'), 'CalcaR': ('LegR', 'ShinR', 'FootR'),
    'BotaL': ('LegL', 'ShinL', 'FootL'), 'BotaR': ('LegR', 'ShinR', 'FootR'),
}


def sm(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def world_copy(src, name):
    """Cópia da malha já no espaço do mundo, sem pai."""
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    o = src.copy()
    o.data = src.data.copy()
    bpy.context.scene.collection.objects.link(o)
    mw = src.matrix_world.copy()
    o.parent = None
    o.data.transform(mw)
    o.matrix_world = Matrix.Identity(4)
    o.name = name
    o.hide_set(False)
    o.hide_render = False
    return o


# ---------------------------------------------------------------- pesos
def decode_body(body, name='KZB_Pesos'):
    """Corpo com os pesos do jogo (UV KZ_W0..3) convertidos de volta em grupos de vértices."""
    o = world_copy(body, name)
    me = o.data
    for g in list(o.vertex_groups):
        o.vertex_groups.remove(g)
    vg = [o.vertex_groups.new(name=b) for b in BONE_ORDER]
    idx = [0] * len(me.loops)
    me.loops.foreach_get('vertex_index', idx)
    acc = {}
    for k in range(4):
        lay = me.uv_layers['KZ_W%d' % k].data
        uv = [0.0] * (len(me.loops) * 2)
        lay.foreach_get('uv', uv)
        for li, vi in enumerate(idx):
            b, w = int(round(uv[li * 2])), uv[li * 2 + 1]
            if w > 1e-4:
                acc.setdefault(vi, {})[b] = w
    for vi, d in acc.items():
        for b, w in d.items():
            vg[b].add([vi], w, 'REPLACE')
    o.hide_set(True)
    o.hide_render = True
    return o


def transfer_weights(src, dst, allowed):
    for g in list(dst.vertex_groups):
        dst.vertex_groups.remove(g)
    for g in src.vertex_groups:
        dst.vertex_groups.new(name=g.name)
    m = dst.modifiers.new('KZ_Pesos', 'DATA_TRANSFER')
    m.object = src
    m.use_vert_data = True
    m.data_types_verts = {'VGROUP_WEIGHTS'}
    m.vert_mapping = 'POLYINTERP_NEAREST'
    m.layers_vgroup_select_src = 'ALL'
    m.layers_vgroup_select_dst = 'NAME'
    with bpy.context.temp_override(object=dst, active_object=dst, selected_objects=[dst]):
        bpy.ops.object.modifier_apply(modifier=m.name)
    encode_uv(dst, allowed)


def encode_uv(o, allowed):
    me = o.data
    gi = {g.index: g.name for g in o.vertex_groups}
    per_v = []
    for v in me.vertices:
        w = [(gi[g.group], g.weight) for g in v.groups if g.weight > 1e-4 and gi[g.group] in allowed]
        if not w:
            w = [(allowed[0], 1.0)]
        w.sort(key=lambda t: -t[1])
        w = w[:4]
        tot = sum(x for _, x in w) or 1.0
        w = [(BONE_ORDER.index(b), x / tot) for b, x in w]
        while len(w) < 4:
            w.append((0, 0.0))
        per_v.append(w)
    idx = [0] * len(me.loops)
    me.loops.foreach_get('vertex_index', idx)
    for k in range(4):
        lay = me.uv_layers.get('KZ_W%d' % k) or me.uv_layers.new(name='KZ_W%d' % k)
        flat = []
        for i in idx:
            flat += [float(per_v[i][k][0]), per_v[i][k][1]]
        lay.data.foreach_set('uv', flat)


# ---------------------------------------------------------------- texturas
def _px(img):
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


def _tex(mat, key):
    for nd in mat.node_tree.nodes:
        if nd.type == 'TEX_IMAGE' and nd.image and key in nd.image.name:
            return nd.image
    return None


def _resize(a, w, h):
    """Redimensiona (vizinho mais próximo com média 2x2 quando reduz pela metade)."""
    H, W = a.shape[:2]
    if (W, H) == (w, h):
        return a
    if W == 2 * w and H == 2 * h:
        return a.reshape(h, 2, w, 2, 4).mean(axis=(1, 3))
    ys = (np.arange(h) * H // h).clip(0, H - 1)
    xs = (np.arange(w) * W // w).clip(0, W - 1)
    return a[ys][:, xs]


def _save(arr, path, srgb):
    h, w = arr.shape[:2]
    name = os.path.basename(path)
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    img = bpy.data.images.new(name, w, h, alpha=False, float_buffer=False)
    img.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
    img.pixels.foreach_set(arr.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def write_maps(mats, out_name, size=2048):
    """Junta as texturas de 1 ou 2 materiais (um em cima do outro) e grava _Base, _Normal e _Mask."""
    n = len(mats)
    cell_h = size // n
    base = np.zeros((size, size, 4), np.float32)
    nor = np.zeros((size, size, 4), np.float32)
    mask = np.zeros((size, size, 4), np.float32)
    for i, m in enumerate(mats):
        y0 = (n - 1 - i) * cell_h          # material 0 em cima (v alto)
        b = _resize(_px(_tex(m, 'baseColor')), size, cell_h)
        nn = _resize(_px(_tex(m, 'normal')), size, cell_h)
        mr = _resize(_px(_tex(m, 'metallicRoughness')), size, cell_h)
        base[y0:y0 + cell_h] = b
        nor[y0:y0 + cell_h] = nn
        mk = np.empty_like(mr)
        mk[..., 0] = mr[..., 2]            # metal (glTF B)
        mk[..., 1] = mr[..., 0]            # oclusão (R)
        mk[..., 2] = 1.0 - mr[..., 1]      # lisura = 1 - rugosidade (G)
        mk[..., 3] = 1.0
        mask[y0:y0 + cell_h] = mk
    base[..., 3] = nor[..., 3] = 1.0
    d = os.path.join(ROOT, 'Textures', 'Equip')
    os.makedirs(d, exist_ok=True)
    # imagens do Blender: base é sRGB nos pixels? os pixels já vêm lineares para sRGB -> regravar como sRGB mantém
    _save(base, os.path.join(d, out_name + '_Base.png'), True)
    _save(nor, os.path.join(d, out_name + '_Normal.png'), False)
    _save(mask, os.path.join(d, out_name + '_Mask.png'), False)


def remap_uv(o, slot, n):
    """Leva a UV da peça para a faixa do atlas (traz ilhas deslocadas de volta para 0..1 por face)."""
    me = o.data
    uv = me.uv_layers[0].data
    for p in me.polygons:
        us = [uv[li].uv.x for li in p.loop_indices]
        vs = [uv[li].uv.y for li in p.loop_indices]
        du = math.floor(sum(us) / len(us))
        dv = math.floor(sum(vs) / len(vs))
        for li in p.loop_indices:
            u = min(1.0, max(0.0, uv[li].uv.x - du))
            v = min(1.0, max(0.0, uv[li].uv.y - dv))
            uv[li].uv = (u, (n - 1 - slot + v) / n)


# ---------------------------------------------------------------- peças
def _components(o):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table()
    seen, comps = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            x = stack.pop()
            comp.append(x.index)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen:
                    seen.add(y.index)
                    stack.append(y)
        comps.append(comp)
    bm.free()
    return comps


def _keep_verts(o, keep):
    """Apaga da malha os vértices fora de keep."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
    bm.to_mesh(o.data)
    bm.free()


def _split(src, name, pred_comp):
    o = src.copy()
    o.data = src.data.copy()
    bpy.context.scene.collection.objects.link(o)
    o.name = name
    co = [v.co.copy() for v in src.data.vertices]
    keep = set()
    for comp in _components(src):
        pts = [co[i] for i in comp]
        if pred_comp(pts):
            keep.update(comp)
    _keep_verts(o, keep)
    return o


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


def _dup(o, name):
    c = o.copy()
    c.data = o.data.copy()
    bpy.context.scene.collection.objects.link(c)
    c.name = name
    return c


def build_parts(set_name, P, decimate_gloves=0.4):
    """P: dict com PA_Helmet, PA_Breastplate, PA_Shoulders, PA_Arms, PA_Gloves, PA_Legs já vestidos no corpo."""
    for x in [x for x in bpy.data.objects if x.name.startswith('KZS_')]:
        bpy.data.objects.remove(x, do_unlink=True)
    out = {}
    # elmo
    h = _dup(P['Helmet'], 'KZS_Elmo')
    out['Elmo'] = ([h], [P['Helmet'].data.materials[0]], 'Elmo')
    # tronco: peitoral + ombreiras
    b = _dup(P['Breastplate'], 'KZS_ArmA')
    s = _dup(P['Shoulders'], 'KZS_ArmB')
    out['Armadura'] = ([b, s], [P['Breastplate'].data.materials[0], P['Shoulders'].data.materials[0]], 'Armadura')
    # braços + manoplas por lado
    gl = _dup(P['Gloves'], 'KZS_GlovesAll')
    if decimate_gloves and decimate_gloves < 1:
        m = gl.modifiers.new('dec', 'DECIMATE')
        m.ratio = decimate_gloves
        with bpy.context.temp_override(object=gl, active_object=gl, selected_objects=[gl]):
            bpy.ops.object.modifier_apply(modifier='dec')
    for side, sg in (('L', 1), ('R', -1)):
        a = _split(P['Arms'], 'KZS_Arm' + side, lambda pts: sum(p.x for p in pts) * sg > 0)
        g = _split(gl, 'KZS_Glove' + side, lambda pts: sum(p.x for p in pts) * sg > 0)
        out['Luva' + side] = ([a, g], [P['Arms'].data.materials[0], P['Gloves'].data.materials[0]], 'Luva')
    bpy.data.objects.remove(gl, do_unlink=True)
    # pernas: do joelho para cima = calça, abaixo = bota
    for side, sg in (('L', 1), ('R', -1)):
        c = _split(P['Legs'], 'KZS_Calca' + side, lambda pts: min(p.z for p in pts) >= 0.39 and sum(p.x for p in pts) * sg > 0)
        t = _split(P['Legs'], 'KZS_Bota' + side, lambda pts: min(p.z for p in pts) < 0.39 and sum(p.x for p in pts) * sg > 0)
        out['Calca' + side] = ([c], [P['Legs'].data.materials[0]], 'Pernas')
        out['Bota' + side] = ([t], [P['Legs'].data.materials[0]], 'Pernas')
    return out


def pivots_from_model():
    """Pivôs das articulações do modelo da classe importado (empties ArmL/LegL e malha Head)."""
    g = lambda n: tuple(bpy.data.objects[n].matrix_world.translation)
    return {'Armadura': (0.0, 0.0, 0.0), 'Elmo': g('Head'), 'LuvaL': g('ArmL'), 'LuvaR': g('ArmR'),
            'CalcaL': g('LegL'), 'CalcaR': g('LegR'), 'BotaL': g('LegL'), 'BotaR': g('LegR')}


def export_parts(set_name, parts, wbody, piv, textures=True):
    mat = bpy.data.materials.get('KZ_EquipMaps') or bpy.data.materials.new('KZ_EquipMaps')
    done_tex = set()
    report = []
    for piece, (objs, mats, tex_name) in parts.items():
        if textures and tex_name not in done_tex:
            write_maps(mats, '%s_%s' % (set_name, tex_name))
            done_tex.add(tex_name)
        work = []
        for i, o in enumerate(objs):
            c = _dup(o, 'KZS_w_%s_%d' % (piece, i))
            remap_uv(c, i, len(objs))
            c.data.materials.clear()
            c.data.materials.append(mat)
            work.append(c)
        mesh = _join(work, 'Piece') if len(work) > 1 else work[0]
        mesh.name = 'Piece'
        me = mesh.data
        while len(me.uv_layers) > 1:
            me.uv_layers.remove(me.uv_layers[-1])
        me.uv_layers[0].name = 'UVMap'
        mid = me.uv_layers.new(name='MatId')
        mid.data.foreach_set('uv', [3.0, 0.0] * len(me.loops))
        for a in list(me.color_attributes):
            me.color_attributes.remove(a)
        col = me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
        col.data.foreach_set('color', [1.0, 1.0, 1.0, 1.0] * len(me.loops))
        transfer_weights(wbody, mesh, ALLOWED[piece])
        for g in list(mesh.vertex_groups):
            mesh.vertex_groups.remove(g)
        # pivô na origem e malha já em Y-up (como o prebake do kezor_export): no FBX a "Piece" fica
        # sozinha, no topo, com rotação zero, e o Gear.Attach só a encaixa na articulação
        p = Vector(piv[piece])
        me.transform(Matrix.Translation(-p))
        me.transform(C_YUP)
        mesh.parent = None
        mesh.matrix_world = C_YUP.inverted()
        bpy.context.view_layer.update()
        path = os.path.join(ROOT, 'Models', 'Equip', '%s_%s.fbx' % (set_name, piece))
        for x in bpy.context.view_layer.objects:
            x.select_set(False)
        mesh.select_set(True)
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
                                 global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 bake_space_transform=False, axis_forward='-Z', axis_up='Y',
                                 mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False,
                                 bake_anim=False, path_mode='STRIP', embed_textures=False, use_custom_props=False,
                                 colors_type='SRGB')
        report.append((piece, len(me.polygons), os.path.getsize(path)))
        bpy.data.objects.remove(mesh, do_unlink=True)
    return report


# ---------------------------------------------------------------- corpo escondido sob as peças
def write_hide(set_name, model, parts, body_objs, reach=0.12):
    """Para cada peça, os vértices do corpo/cabeça que ficam por baixo dela (posição em mm no espaço do Blender).
    O jogo (Gear.HideCovered) apaga os triângulos com os três vértices na lista."""
    trees = {}
    for piece, (objs, _, _) in parts.items():
        bm = bmesh.new()
        for o in objs:
            bm.from_mesh(o.data)
        trees[piece] = BVHTree.FromBMesh(bm)
        bm.free()
    lists = {k: [] for k in parts}
    up = Vector((0, 0, 0.3))
    for src in body_objs:
        o = world_copy(src, 'KZS_hide_tmp')
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.normal_update()
        for v in bm.verts:
            best, bd = None, 9.0
            for piece, t in trees.items():
                hits = 0
                for d in (v.normal, (v.normal + up).normalized(), (v.normal - up).normalized()):
                    if t.ray_cast(v.co + d * 0.001, d, reach)[0] is not None:
                        hits += 1
                loc, _, _, dist = t.find_nearest(v.co)
                close = dist is not None and dist < 0.06 and (v.co.z < 0.16 or (v.co.z < 0.86 and abs(v.co.x) > 0.17))
                if hits >= 2 or close:
                    if dist is not None and dist < bd:
                        best, bd = piece, dist
            if best:
                lists[best].append(tuple(int(round(c * 1000)) for c in v.co))
        bm.free()
        bpy.data.objects.remove(o, do_unlink=True)
    d = os.path.join(ROOT, 'Equip', 'Hide')
    os.makedirs(d, exist_ok=True)
    out = {}
    for piece, pts in lists.items():
        path = os.path.join(d, '%s_%s_%s.txt' % (set_name, piece, model))
        with open(path, 'w', encoding='utf-8') as f:
            f.write('\n'.join('%d %d %d' % p for p in sorted(set(pts))))
        out[piece] = len(pts)
    return out
