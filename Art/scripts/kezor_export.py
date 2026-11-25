# Kezor — exporta cada modelo (empty raiz com kezor_root) para Assets/Models/<Categoria>/<Nome>.fbx
import bpy, os
from mathutils import Vector, Matrix

# Blender (Z para cima, frente -Y) -> FBX Y para cima (Forward -Z, Up Y): (x, y, z) -> (x, z, -y)
C = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))
CI = C.inverted()


def prebake(kids):
    """Converte malhas e pivôs para Y-up à mão, para que no FBX todas as partes tenham rotação zero
    (o 'Apply Transform' do exportador erra a rotação dos filhos em hierarquias)."""
    saved = {}
    for k in kids:
        saved[k] = (k.matrix_basis.copy(), k.matrix_parent_inverse.copy())
    for k in kids:
        if k.type == 'MESH':
            k.data.transform(C)
    for k in kids:
        if k.parent in kids:
            loc = Vector(k['pivot']) - Vector(k.parent['pivot'])
            k.matrix_parent_inverse = Matrix.Identity(4)
            k.matrix_basis = Matrix.Translation(C @ loc)
    for k in kids:
        if k.parent not in kids:
            k.matrix_world = CI
    return saved


def unbake(kids, saved):
    for k in kids:
        if k.type == 'MESH':
            k.data.transform(CI)
    for k, (mb, mpi) in saved.items():
        k.matrix_parent_inverse = mpi
        k.matrix_basis = mb

OUT = r'C:\MuProtoUnity\Assets\Resources\Models'


def _mat_vcol(m):
    """Cor do vértice para o shader Kezor/LowPoly: RGB = cor sRGB do material; alfa < 0,5 = emissivo (0 = brilho total)."""
    hx = (m.get('kezor_hex') if m else None) or '#ffffff'
    hx = hx.lstrip('#')
    rgb = [int(hx[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    emit = 0.0
    if m and m.node_tree:
        bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if bsdf:
            for sck in bsdf.inputs:
                if sck.identifier == 'Emission Strength':
                    emit = float(sck.default_value)
    a = 0.5 * (1 - min(1.0, emit / 4.0)) if emit > 0 else 1.0
    return (*rgb, a)


def bake_vertex_colors(obj):
    me = obj.data
    attr = me.color_attributes.get('Col')
    if attr is None:
        attr = me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    cols = [_mat_vcol(m) for m in me.materials] or [(1, 1, 1, 1)]
    for poly in me.polygons:
        c = cols[min(poly.material_index, len(cols) - 1)]
        for li in poly.loop_indices:
            attr.data[li].color_srgb = c
    me.color_attributes.active_color = attr
    try:
        me.color_attributes.render_color_index = me.color_attributes.find('Col')
    except Exception:
        pass


def export_all(only=None):
    report = []
    roots = [o for o in bpy.data.objects if o.get('kezor_root')]
    for r in roots:
        if only and r.name not in only:
            continue
        cat = r['category']
        name = r.name
        os.makedirs(os.path.join(OUT, cat), exist_ok=True)
        path = os.path.join(OUT, cat, name + '.fbx')
        kids = list(r.children_recursive)
        old_loc = r.location.copy()
        old_names = {k: k.name for k in kids}
        r.name = name + '__root'
        r.location = (0, 0, 0)
        for k in kids:
            k.name = '__tmp_' + k.name
        for k in kids:
            k.name = k['part']
        for k in kids:
            if k.type == 'MESH':
                bake_vertex_colors(k)
        bpy.context.view_layer.update()
        saved = prebake(kids)
        bpy.context.view_layer.update()
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        for k in kids:
            k.select_set(True)
        bpy.context.view_layer.objects.active = kids[0]
        try:
            bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
                                     global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                     bake_space_transform=False, axis_forward='-Z', axis_up='Y',
                                     mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False,
                                     bake_anim=False, path_mode='COPY', embed_textures=True, use_custom_props=False, colors_type='SRGB')
            bad = [k.name for k in kids if k.name != k['part']]
            report.append((cat, name, os.path.getsize(path), bad))
        finally:
            unbake(kids, saved)
            for k in kids:
                k.name = '__tmp2_' + k.name
            for k, n in old_names.items():
                k.name = n
            r.name = name
            r.location = old_loc
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    return report


print(export_all())
