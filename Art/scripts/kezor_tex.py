# Kezor — texturas "pintadas" assadas no Blender (Cycles) para as peças de equipamento.
# Mapas por peça (Resources/Textures/Equip/<Set>_<Peça>_*.png), lidos pelo Kezor/LowPoly quando _UseMaps = 1:
#   _Base   cor pintada: cor do vértice × sujeira nos cantos (AO) + quinas gastas (Bevel) + arranhões + variação
#   _Normal relevo (tangente): amassados de martelo e arranhões no metal, grão no couro, trama no pano
#   _Mask   R = metal, G = oclusão, B = lisura (brilho)
# Usa o namespace KZ (kezor_hero4/armor5/armor6/sets6 executados antes).
import bpy, bmesh, math, os
from mathutils import Vector

KIND_ID = {'steel': 1, 'goldplate': 2, 'gold': 2, 'leather': 3, 'strap': 3, 'stitch': 3, 'cloth': 4, 'rune': 5}
TEX_OUT = r'C:\MuProtoUnity\Assets\Resources\Textures\Equip'


def tx_join(objs, name):
    """Copia e junta as peças numa malha só, guardando o tipo de material de cada face (atributo 'kid')."""
    G = bpy.app.driver_namespace['KZ']
    G['_kill'](name)
    cps = []
    for o in objs:
        c = o.copy()
        c.data = o.data.copy()
        G['_link'](c)
        c.data.transform(c.matrix_world)
        c.matrix_world.identity()
        me = c.data
        kid = KIND_ID.get(o.get('kind', 'steel'), 1)
        at = me.attributes.get('kid') or me.attributes.new('kid', 'FLOAT', 'FACE')
        at.data.foreach_set('value', [float(kid)] * len(me.polygons))
        # faces do lado de dentro e da espessura dividem a grade com o lado de fora: ganham UV próprio
        gi = [g.index for g in c.vertex_groups if g.name in ('in', 'rim')]
        inner = [False] * len(me.polygons)
        if gi:
            vin = [any(gg.group in gi and gg.weight > 0.5 for gg in v.groups) for v in me.vertices]
            for p in me.polygons:
                inner[p.index] = all(vin[i] for i in p.vertices)
        for g in list(c.vertex_groups):
            c.vertex_groups.remove(g)
        # placas: mantêm a grade (u, v) como UV (uma ilha limpa por placa); tubos e rebites sem UV ganham depois
        has = len(me.uv_layers) > 0
        while len(me.uv_layers) > 1:
            me.uv_layers.remove(me.uv_layers[-1])
        if has:
            me.uv_layers[0].name = 'Bake'
        else:
            me.uv_layers.new(name='Bake')
        tb = me.attributes.get('tube') or me.attributes.new('tube', 'BOOLEAN', 'FACE')
        tb.data.foreach_set('value', [(not has) or inner[i] for i in range(len(me.polygons))])
        cps.append(c)
    m = G['_join'](cps, name)
    m['kind'] = 'steel'
    return m


def tx_unwrap(o, margin=0.003, tube_scale=0.5):
    """Placas: grade (u, v) já pronta. Tubos: projeção automática. Depois iguala a densidade e empacota tudo."""
    me = o.data
    uv = me.uv_layers['Bake']
    me.uv_layers.active = uv
    tb = me.attributes['tube'].data
    sel = [bool(x.value) for x in tb]
    me.polygons.foreach_set('select', sel)
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.context.tool_settings.mesh_select_mode = (False, False, True)
    if any(sel):
        bpy.ops.uv.smart_project(angle_limit=math.radians(70), island_margin=0.0, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.context.scene.tool_settings.use_uv_select_sync = True
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.average_islands_scale()
    bpy.ops.object.mode_set(mode='OBJECT')
    # tubos e rebites ocupam menos espaço (são finos e quase de uma cor só)
    if any(sel) and tube_scale != 1.0:
        n = len(me.loops)
        uvs = [0.0] * (n * 2)
        uv.data.foreach_get('uv', uvs)
        for p in me.polygons:
            if sel[p.index]:
                for li in p.loop_indices:
                    uvs[li * 2] *= tube_scale
                    uvs[li * 2 + 1] *= tube_scale
        uv.data.foreach_set('uv', uvs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=margin, shape_method='CONCAVE')
    bpy.ops.object.mode_set(mode='OBJECT')
    return uv


def _n(nt, typ, loc, **kw):
    n = nt.nodes.new(typ)
    n.location = loc
    for k, v in kw.items():
        setattr(n, k, v)
    return n


def tx_material(o, img_name, res):
    """Material de assar: monta máscara por tipo, relevo procedural e as três saídas (cor, máscara, normal)."""
    mat = bpy.data.materials.get('KZ_TexBake') or bpy.data.materials.new('KZ_TexBake')
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    L = nt.links.new
    out = _n(nt, 'ShaderNodeOutputMaterial', (1400, 0))
    col = _n(nt, 'ShaderNodeAttribute', (-1200, 300), attribute_name='Col')
    kid = _n(nt, 'ShaderNodeAttribute', (-1200, 0), attribute_name='kid')
    tc = _n(nt, 'ShaderNodeTexCoord', (-1400, -300))

    def mask(v):                                         # 1 onde kid == v
        c = _n(nt, 'ShaderNodeMath', (-1000, -100 * v), operation='COMPARE')
        c.inputs[1].default_value = v
        c.inputs[2].default_value = 0.5
        L(kid.outputs['Fac'], c.inputs[0])
        return c.outputs[0]
    m_steel, m_gold, m_leather, m_cloth = mask(1), mask(2), mask(3), mask(4)
    metal = _n(nt, 'ShaderNodeMath', (-800, -100), operation='ADD')
    L(m_steel, metal.inputs[0]); L(m_gold, metal.inputs[1])

    # ---------------- relevo
    obj = tc.outputs['Object']
    dent = _n(nt, 'ShaderNodeTexNoise', (-900, -500), noise_dimensions='3D')
    dent.inputs['Scale'].default_value = 16.0
    dent.inputs['Detail'].default_value = 1.5
    L(obj, dent.inputs['Vector'])
    vor = _n(nt, 'ShaderNodeTexVoronoi', (-900, -750), feature='SMOOTH_F1')
    vor.inputs['Scale'].default_value = 20.0
    vor.inputs['Smoothness'].default_value = 1.0
    L(obj, vor.inputs['Vector'])
    # arranhões: ruído esticado num eixo e afinado por limiar
    mp = _n(nt, 'ShaderNodeMapping', (-1100, -1000))
    mp.inputs['Scale'].default_value = (9.0, 9.0, 200.0)
    mp.inputs['Rotation'].default_value = (0.5, 0.9, 0.3)
    L(obj, mp.inputs['Vector'])
    scr = _n(nt, 'ShaderNodeTexNoise', (-900, -1000))
    scr.inputs['Scale'].default_value = 2.2
    scr.inputs['Detail'].default_value = 0.0
    L(mp.outputs['Vector'], scr.inputs['Vector'])
    scr_r = _n(nt, 'ShaderNodeMapRange', (-700, -1000))
    scr_r.inputs['From Min'].default_value = 0.494
    scr_r.inputs['From Max'].default_value = 0.5
    scr_r.inputs['To Min'].default_value = 0.0
    scr_r.inputs['To Max'].default_value = 1.0
    scr_r.clamp = True
    L(scr.outputs['Fac'], scr_r.inputs['Value'])
    scr_d = _n(nt, 'ShaderNodeMapRange', (-700, -1200))
    scr_d.inputs['From Min'].default_value = 0.5
    scr_d.inputs['From Max'].default_value = 0.506
    scr_d.inputs['To Min'].default_value = 1.0
    scr_d.inputs['To Max'].default_value = 0.0
    scr_d.clamp = True
    L(scr.outputs['Fac'], scr_d.inputs['Value'])
    scratch = _n(nt, 'ShaderNodeMath', (-500, -1100), operation='MULTIPLY')
    L(scr_r.outputs['Result'], scratch.inputs[0]); L(scr_d.outputs['Result'], scratch.inputs[1])
    # grão do couro e trama do pano
    grain = _n(nt, 'ShaderNodeTexVoronoi', (-900, -1400), feature='F1')
    grain.inputs['Scale'].default_value = 140.0
    L(obj, grain.inputs['Vector'])
    weave = _n(nt, 'ShaderNodeTexWave', (-900, -1650), wave_type='BANDS')
    weave.inputs['Scale'].default_value = 45.0
    L(obj, weave.inputs['Vector'])

    def mul(a, b, loc):
        m = _n(nt, 'ShaderNodeMath', loc, operation='MULTIPLY')
        L(a, m.inputs[0])
        if isinstance(b, float):
            m.inputs[1].default_value = b
        else:
            L(b, m.inputs[1])
        return m.outputs[0]

    def add(a, b, loc):
        m = _n(nt, 'ShaderNodeMath', loc, operation='ADD')
        L(a, m.inputs[0]); L(b, m.inputs[1])
        return m.outputs[0]
    h_metal = add(mul(dent.outputs['Fac'], 0.6, (-500, -500)), mul(vor.outputs['Distance'], 0.5, (-500, -700)), (-300, -600))
    h_metal = add(h_metal, mul(scratch.outputs[0], -0.35, (-300, -1100)), (-150, -700))
    h_metal = mul(h_metal, metal.outputs[0], (0, -700))
    h_leath = mul(mul(grain.outputs['Distance'], 0.7, (-500, -1400)), m_leather, (-300, -1400))
    h_cloth = mul(mul(weave.outputs['Fac'], 0.5, (-500, -1650)), m_cloth, (-300, -1650))
    height = add(add(h_metal, h_leath, (150, -1000)), h_cloth, (300, -1000))
    bump = _n(nt, 'ShaderNodeBump', (500, -900))
    bump.inputs['Strength'].default_value = 0.7
    bump.inputs['Distance'].default_value = 0.003
    L(height, bump.inputs['Height'])

    # ---------------- sujeira (AO) e quinas gastas (Bevel)
    ao = _n(nt, 'ShaderNodeAmbientOcclusion', (-600, 600), samples=16)
    ao.inputs['Distance'].default_value = 0.06
    bev = _n(nt, 'ShaderNodeBevel', (-800, 900), samples=16)
    bev.inputs['Radius'].default_value = 0.004
    geo = _n(nt, 'ShaderNodeNewGeometry', (-800, 1100))
    dp = _n(nt, 'ShaderNodeVectorMath', (-600, 1000), operation='DOT_PRODUCT')
    L(bev.outputs['Normal'], dp.inputs[0]); L(geo.outputs['Normal'], dp.inputs[1])
    edge = _n(nt, 'ShaderNodeMapRange', (-400, 1000))
    edge.inputs['From Min'].default_value = 0.975
    edge.inputs['From Max'].default_value = 0.86
    edge.clamp = True
    L(dp.outputs['Value'], edge.inputs['Value'])
    grime_n = _n(nt, 'ShaderNodeTexNoise', (-800, 1300))
    grime_n.inputs['Scale'].default_value = 9.0
    grime_n.inputs['Detail'].default_value = 6.0
    L(obj, grime_n.inputs['Vector'])
    dirt = _n(nt, 'ShaderNodeMapRange', (-400, 700))          # quanto menor o AO, mais sujeira
    dirt.inputs['From Min'].default_value = 1.0
    dirt.inputs['From Max'].default_value = 0.35
    dirt.clamp = True
    L(ao.outputs['AO'], dirt.inputs['Value'])
    dirt2 = mul(dirt.outputs['Result'], grime_n.outputs['Fac'], (-200, 700))
    dirt2 = mul(dirt2, 1.6, (-50, 700))

    # cor: vértice → escurece com sujeira → clareia nas quinas e nos arranhões (metal)
    c1 = _n(nt, 'ShaderNodeMix', (100, 500), data_type='RGBA', blend_type='MULTIPLY')
    c1.inputs['Factor'].default_value = 1.0
    L(col.outputs['Color'], c1.inputs[6])
    grime_col = _n(nt, 'ShaderNodeMix', (-150, 450), data_type='RGBA')
    grime_col.inputs[6].default_value = (1, 1, 1, 1)
    grime_col.inputs[7].default_value = (0.32, 0.26, 0.2, 1)
    L(dirt2, grime_col.inputs['Factor'])
    L(grime_col.outputs[2], c1.inputs[7])
    wear = add(mul(edge.outputs['Result'], 0.55, (100, 900)), mul(scratch.outputs[0], 0.18, (100, 800)), (250, 850))
    wear = mul(wear, metal.outputs[0], (400, 850))
    c2 = _n(nt, 'ShaderNodeMix', (550, 500), data_type='RGBA', blend_type='SCREEN')
    L(wear, c2.inputs['Factor'])
    L(c1.outputs[2], c2.inputs[6])
    c2.inputs[7].default_value = (0.85, 0.86, 0.88, 1)
    # couro: quinas mais claras (gasto)
    lw = mul(edge.outputs['Result'], m_leather, (400, 1100))
    c3 = _n(nt, 'ShaderNodeMix', (750, 500), data_type='RGBA', blend_type='SCREEN')
    L(mul(lw, 0.35, (550, 1100)), c3.inputs['Factor'])
    L(c2.outputs[2], c3.inputs[6])
    c3.inputs[7].default_value = (0.75, 0.6, 0.45, 1)

    # máscara: R metal, G oclusão, B lisura
    smooth_m = _n(nt, 'ShaderNodeMapRange', (300, 200))
    smooth_m.inputs['To Min'].default_value = 0.72
    smooth_m.inputs['To Max'].default_value = 0.35
    L(add(scratch.outputs[0], dirt2, (150, 200)), smooth_m.inputs['Value'])
    sm_metal = mul(smooth_m.outputs['Result'], metal.outputs[0], (500, 200))
    sm_other = add(mul(m_leather, 0.32, (300, 50)), mul(m_cloth, 0.14, (300, -50)), (500, 50))
    smooth = add(sm_metal, sm_other, (650, 150))
    comb = _n(nt, 'ShaderNodeCombineColor', (850, 150))
    L(metal.outputs[0], comb.inputs[0]); L(ao.outputs['AO'], comb.inputs[1]); L(smooth, comb.inputs[2])

    emis = _n(nt, 'ShaderNodeEmission', (1050, 300))
    sw = _n(nt, 'ShaderNodeMix', (950, 450), data_type='RGBA')           # 0 = cor, 1 = máscara
    sw.name = 'KZ_Switch'
    L(c3.outputs[2], sw.inputs[6]); L(comb.outputs[0], sw.inputs[7])
    L(sw.outputs[2], emis.inputs['Color'])
    diff = _n(nt, 'ShaderNodeBsdfDiffuse', (1050, -300))
    L(bump.outputs['Normal'], diff.inputs['Normal'])
    add_s = _n(nt, 'ShaderNodeAddShader', (1250, 0))
    L(emis.outputs[0], add_s.inputs[0]); L(diff.outputs[0], add_s.inputs[1])
    L(add_s.outputs[0], out.inputs['Surface'])

    img = bpy.data.images.get(img_name)
    if img is None or img.size[0] != res:
        if img:
            bpy.data.images.remove(img)
        img = bpy.data.images.new(img_name, res, res, alpha=False, float_buffer=False)
    tn = _n(nt, 'ShaderNodeTexImage', (1250, 400))
    tn.name = 'KZ_Target'
    tn.image = img
    nt.nodes.active = tn
    o.data.materials.clear()
    o.data.materials.append(mat)
    return mat, img


def tx_bake(o, kind, res, path):
    """kind: 'base' | 'mask' | 'normal'. Assa no UV 'Bake' e salva PNG."""
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    mat = o.data.materials[0]
    nt = mat.node_tree
    tgt = nt.nodes['KZ_Target']
    img = bpy.data.images.new('KZ_Bake_' + kind, res, res, alpha=False, float_buffer=(kind == 'normal'))
    if kind != 'base':
        img.colorspace_settings.name = 'Non-Color'
    tgt.image = img
    nt.nodes.active = tgt
    nt.nodes['KZ_Switch'].inputs['Factor'].default_value = 1.0 if kind == 'mask' else 0.0
    o.data.uv_layers.active = o.data.uv_layers['Bake']
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    sc.render.bake.margin = 6
    sc.render.bake.use_selected_to_active = False
    if kind == 'normal':
        sc.cycles.samples = 8
        sc.render.bake.normal_space = 'TANGENT'
        bpy.ops.object.bake(type='NORMAL', uv_layer='Bake')
    else:
        sc.cycles.samples = 4
        bpy.ops.object.bake(type='EMIT', uv_layer='Bake')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
    return path


def tx_preview(o, base, normal, mask, path, cam_from=None, res=(560, 760)):
    """Render EEVEE com material PBR (cor, relevo, metal, brilho) para conferir o resultado."""
    mat = bpy.data.materials.get('KZ_TexPrev') or bpy.data.materials.new('KZ_TexPrev')
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    L = nt.links.new
    out = _n(nt, 'ShaderNodeOutputMaterial', (900, 0))
    bs = _n(nt, 'ShaderNodeBsdfPrincipled', (600, 0))
    uvn = _n(nt, 'ShaderNodeUVMap', (-600, 0), uv_map='Bake')

    def img(p, nc, loc):
        n = _n(nt, 'ShaderNodeTexImage', loc)
        n.image = bpy.data.images.load(p, check_existing=False)
        if nc:
            n.image.colorspace_settings.name = 'Non-Color'
        L(uvn.outputs['UV'], n.inputs['Vector'])
        return n
    b = img(base, False, (-300, 300))
    nm = img(normal, True, (-300, -300))
    mk = img(mask, True, (-300, 0))
    sep = _n(nt, 'ShaderNodeSeparateColor', (0, 0))
    L(mk.outputs['Color'], sep.inputs[0])
    inv = _n(nt, 'ShaderNodeMath', (200, -100), operation='SUBTRACT')
    inv.inputs[0].default_value = 1.0
    L(sep.outputs[2], inv.inputs[1])
    nmap = _n(nt, 'ShaderNodeNormalMap', (200, -300), uv_map='Bake')
    L(nm.outputs['Color'], nmap.inputs['Color'])
    L(b.outputs['Color'], bs.inputs['Base Color'])
    L(sep.outputs[0], bs.inputs['Metallic'])
    L(inv.outputs[0], bs.inputs['Roughness'])
    L(nmap.outputs['Normal'], bs.inputs['Normal'])
    L(bs.outputs[0], out.inputs['Surface'])
    o.data.materials.clear()
    o.data.materials.append(mat)
    return mat
