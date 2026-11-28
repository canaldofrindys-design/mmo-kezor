"""
Kezor — ícones dos itens renderizados no Blender (Cycles), a partir dos MESMOS modelos FBX que o jogo usa.

Cada item com modelo (armas, escudos, peças dos sets, asas, montarias, mascotes) vira um PNG no tamanho exato da grade
do inventário (96 px por casa) em Art/icons/items/item_<id>.png. O IconBaker do Unity (Kezor ▸ Gerar ícones) usa esses
PNGs quando existem e só renderiza no Unity o que faltar; poções e runas continuam desenhadas em 2D.

Material: a cor vem da cor de vértice 'Col' do modelo (a mesma que o jogo pinta), metal/aspereza vêm do material da
peça e do id de material 'MatId' (3 = metal); 'Col' com alfa < 0,5 brilha (gemas, brasas). Luz: a mesma do kit da
interface (kzb.py), girada para a câmera em 3/4.

    python Art/ui/blender/icons.py              # todos
    python Art/ui/blender/icons.py 22 701 300   # alguns ids
"""
import math
import os
import re
import sys

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(__file__)
sys.path.insert(0, HERE)
import bpy  # noqa: E402
from mathutils import Vector, Matrix, Euler  # noqa: E402
import kzb  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
MODELS = os.path.join(ROOT, 'Assets', 'Resources', 'Models')
OUT = os.path.join(ROOT, 'Art', 'icons', 'items')
SAMPLES = int(os.environ.get('KZ_SAMPLES', '96'))
CELL = 96
SIZE = 200.0          # maior medida do modelo na cena (unidades), para a luz do kit valer igual


# ----------------------------------------------------------------------------------------------- dados do jogo (lidos do C#)

def read(p):
    return open(os.path.join(ROOT, p), encoding='utf-8').read()


def items():
    """[{id, kind, slot, maxdmg, epic}] a partir do ItemDatabase.cs."""
    out = []
    for line in read('Assets/Scripts/Core/Items/ItemDatabase.cs').splitlines():
        if 'new ItemDefinition' not in line:
            continue
        m = re.search(r'\bId\s*=\s*(\d+)', line)
        if not m:
            continue
        g = lambda pat, d=None: (re.search(pat, line).group(1) if re.search(pat, line) else d)  # noqa: E731
        out.append({'id': int(m.group(1)), 'kind': g(r'Kind\s*=\s*ItemKind\.(\w+)', 'None'),
                    'slot': g(r'Slot\s*=\s*EquipSlot\.(\w+)', 'None'), 'maxdmg': int(g(r'MaxDamage\s*=\s*(\d+)', '0')),
                    'epic': bool(re.search(r'Epic\s*=\s*true', line)),
                    'potion': bool(re.search(r'Potion\s*=\s*PotionType\.(?!None)', line)),
                    'rune': bool(re.search(r'Rune\s*=\s*RuneType\.(?!None)', line))})
    return out


def gear_tables():
    src = read('Assets/Scripts/Client/Gear.cs')

    def dict_block(name):
        a = src.index(name)
        b = src.index('};', a)
        return {int(k): v for k, v in re.findall(r'\{\s*(\d+),\s*"([^"]+)"\s*\}', src[a:b])}
    weapons = dict_block('WeaponModels')
    extra = dict_block('ExtraModels')
    sets = [(int(a), int(b), s) for a, b, s in re.findall(r'd\.Id >= (\d+) && d\.Id <= (\d+)\) return "(\w+)"', src)]
    tints = {int(k): (float(r), float(g), float(b)) for k, r, g, b in
             re.findall(r'\{\s*(\d+),\s*new Color\(([\d.]+)f,\s*([\d.]+)f,\s*([\d.]+)f\)\s*\}', src[src.index('WeaponTints'):])}
    return weapons, extra, sets, tints


def grid_size(d):
    if d['potion'] or d['rune']:
        return 1, 1
    if d['slot'] == 'Wings':
        return 3, 2
    if d['slot'] == 'Mount':
        return 2, 2
    if d['slot'] == 'Pet':
        return 1, 1
    k = d['kind']
    return {'Sword': (1, 4) if d['maxdmg'] >= 40 or d['epic'] else (1, 3), 'Axe': (2, 3), 'Staff': (1, 4), 'Lance': (1, 4),
            'Bow': (2, 4), 'Gauntlet': (2, 2), 'Tome': (2, 2), 'Shield': (2, 2), 'Helm': (2, 2), 'Armor': (2, 3),
            'Pants': (2, 2), 'Gloves': (2, 2), 'Boots': (2, 2)}.get(k, (1, 1))


def model_for(d, weapons, extra, sets):
    """mesmo critério do Gear.ModelPathsFor + IconBaker (pares: só a última peça)."""
    i = d['id']
    if i in weapons:
        return 'Equip/' + weapons[i] + ('_L' if d['kind'] == 'Gauntlet' else '')
    if i in extra:
        return extra[i]
    for a, b, s in sets:
        if a <= i <= b:
            return {'Helm': f'Equip/{s}_Elmo', 'Armor': f'Equip/{s}_Armadura', 'Pants': f'Equip/{s}_CalcaR',
                    'Gloves': f'Equip/{s}_LuvaR', 'Boots': f'Equip/{s}_BotaR'}.get(d['slot'])
    return None


# ----------------------------------------------------------------------------------------------- material

TEX = os.path.join(ROOT, 'Assets', 'Resources', 'Textures', 'Equip')


def map_names(name):
    """igual ao Gear.MapNames: a peça usa a própria textura ou a do par (LuvaL -> Luva; calças e botas -> Pernas)."""
    yield name
    if name.endswith('L') or name.endswith('R'):
        yield name[:-1]
    u = name.find('_')
    if u > 0 and ('_Calca' in name or '_Bota' in name):
        yield name[:u] + '_Pernas'


def find_maps(path):
    name = path.split('/')[-1]
    for n in map_names(name):
        b = os.path.join(TEX, n + '_Base.png')
        if os.path.exists(b):
            nm, mk = os.path.join(TEX, n + '_Normal.png'), os.path.join(TEX, n + '_Mask.png')
            return b, nm if os.path.exists(nm) else None, mk if os.path.exists(mk) else None
    return None


def textured_material(maps, tint):
    """peça com texturas assadas (sets de classe): cor, relevo e máscara R metal / G oclusão / B lisura."""
    base, nrm, mask = maps
    m = bpy.data.materials.new('ic_maps')
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    uv = nt.nodes.new('ShaderNodeUVMap')
    uv.uv_map = 'UVMap'

    def img(path, color):
        t = nt.nodes.new('ShaderNodeTexImage')
        t.image = bpy.data.images.load(path, check_existing=True)
        t.image.colorspace_settings.name = 'sRGB' if color else 'Non-Color'
        nt.links.new(uv.outputs['UV'], t.inputs['Vector'])
        return t
    tb = img(base, True)
    mul = nt.nodes.new('ShaderNodeMix')
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs['Factor'].default_value = 1.0
    nt.links.new(tb.outputs['Color'], mul.inputs['A'])
    mul.inputs['B'].default_value = (*tint, 1.0)
    col_out = mul.outputs['Result']
    if mask:
        tm = img(mask, False)
        sp = nt.nodes.new('ShaderNodeSeparateColor')
        nt.links.new(tm.outputs['Color'], sp.inputs[0])
        k = nt.nodes.new('ShaderNodeMath')
        k.operation = 'MULTIPLY'
        k.inputs[1].default_value = 0.9
        nt.links.new(sp.outputs['Red'], k.inputs[0])
        nt.links.new(k.outputs[0], bsdf.inputs['Metallic'])
        inv = nt.nodes.new('ShaderNodeMath')
        inv.operation = 'SUBTRACT'
        inv.inputs[0].default_value = 1.0
        nt.links.new(sp.outputs['Blue'], inv.inputs[1])
        nt.links.new(inv.outputs[0], bsdf.inputs['Roughness'])
        ao = nt.nodes.new('ShaderNodeMix')
        ao.data_type = 'RGBA'
        ao.blend_type = 'MULTIPLY'
        ao.inputs['Factor'].default_value = 0.7
        nt.links.new(col_out, ao.inputs['A'])
        nt.links.new(sp.outputs['Green'], ao.inputs['B'])
        col_out = ao.outputs['Result']
    nt.links.new(col_out, bsdf.inputs['Base Color'])
    if nrm:
        tn = img(nrm, False)
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nm.uv_map = 'UVMap'
        nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    # brilho das gemas/runas: alfa da cor de vértice < 0,5
    col = nt.nodes.new('ShaderNodeAttribute')
    col.attribute_name = 'Col'
    em = nt.nodes.new('ShaderNodeMapRange')
    em.inputs['From Min'].default_value = 0.5
    em.inputs['From Max'].default_value = 0.0
    em.inputs['To Max'].default_value = 4.0
    em.clamp = True
    nt.links.new(col.outputs['Alpha'], em.inputs['Value'])
    nt.links.new(col.outputs['Color'], bsdf.inputs['Emission Color'])
    nt.links.new(em.outputs['Result'], bsdf.inputs['Emission Strength'])
    return m

def item_material(src, tint):
    """material do ícone a partir do material original da peça + cor de vértice 'Col' + id 'MatId'."""
    b0 = next((n for n in src.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if src and src.node_tree else None
    metal0 = b0.inputs['Metallic'].default_value if b0 else 0.0
    rough0 = b0.inputs['Roughness'].default_value if b0 else 0.6
    emit0 = b0.inputs['Emission Strength'].default_value if b0 else 0.0
    m = bpy.data.materials.new('ic_' + (src.name if src else 'x'))
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    col = nt.nodes.new('ShaderNodeAttribute')
    col.attribute_name = 'Col'
    mid = nt.nodes.new('ShaderNodeAttribute')
    mid.attribute_name = 'MatId'
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(mid.outputs['Vector'], sep.inputs[0])
    # cor: Col × tinta (Geada Rubra) com um leve ganho de contraste
    mul = nt.nodes.new('ShaderNodeMix')
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs['Factor'].default_value = 1.0
    nt.links.new(col.outputs['Color'], mul.inputs['A'])
    mul.inputs['B'].default_value = (*tint, 1.0)
    dk = nt.nodes.new('ShaderNodeMix')
    dk.data_type = 'RGBA'
    dk.blend_type = 'MULTIPLY'
    nt.links.new(mul.outputs['Result'], dk.inputs['A'])
    dk.inputs['B'].default_value = (0.85, 0.84, 0.82, 1.0)
    nt.links.new(dk.outputs['Result'], bsdf.inputs['Base Color'])
    # metal: id 3 (detalhe "metal") ou material metálico
    is3 = nt.nodes.new('ShaderNodeMath')
    is3.operation = 'COMPARE'
    is3.inputs[1].default_value = 3.0
    is3.inputs[2].default_value = 0.5
    nt.links.new(sep.outputs['X'], is3.inputs[0])
    mx = nt.nodes.new('ShaderNodeMath')
    mx.operation = 'MAXIMUM'
    nt.links.new(is3.outputs[0], mx.inputs[0])
    mx.inputs[1].default_value = metal0
    k = nt.nodes.new('ShaderNodeMath')
    k.operation = 'MULTIPLY'
    k.inputs[1].default_value = 0.6
    nt.links.new(mx.outputs[0], k.inputs[0])
    nt.links.new(k.outputs[0], bsdf.inputs['Metallic'])
    nt.links.new(k.outputs[0], dk.inputs['Factor'])
    # aspereza: metal polido 0,28, resto o do material (no mínimo 0,45)
    rr = nt.nodes.new('ShaderNodeMapRange')
    rr.inputs['To Min'].default_value = max(0.45, rough0 if metal0 < 0.5 else 0.6)
    rr.inputs['To Max'].default_value = min(rough0, 0.3) if metal0 >= 0.5 else 0.28
    nt.links.new(mx.outputs[0], rr.inputs['Value'])
    nt.links.new(rr.outputs['Result'], bsdf.inputs['Roughness'])
    bsdf.inputs['Specular IOR Level'].default_value = 0.45
    # brilho: alfa de Col < 0,5 (convenção do shader do jogo) ou material emissivo
    em = nt.nodes.new('ShaderNodeMapRange')
    em.inputs['From Min'].default_value = 0.5
    em.inputs['From Max'].default_value = 0.0
    em.inputs['To Min'].default_value = 0.0
    em.inputs['To Max'].default_value = 1.0
    em.clamp = True
    nt.links.new(col.outputs['Alpha'], em.inputs['Value'])
    ea = nt.nodes.new('ShaderNodeMath')
    ea.operation = 'MAXIMUM'
    nt.links.new(em.outputs['Result'], ea.inputs[0])
    ea.inputs[1].default_value = 1.0 if emit0 > 0 else 0.0
    es = nt.nodes.new('ShaderNodeMath')
    es.operation = 'MULTIPLY'
    es.inputs[1].default_value = 4.0
    nt.links.new(ea.outputs[0], es.inputs[0])
    nt.links.new(mul.outputs['Result'], bsdf.inputs['Emission Color'])
    nt.links.new(es.outputs[0], bsdf.inputs['Emission Strength'])
    return m


# ----------------------------------------------------------------------------------------------- cena

def import_model(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, path + '.fbx'))
    objs = [o for o in bpy.data.objects if o not in before]
    for o in objs:
        if o.type == 'ARMATURE':
            o.hide_render = True
    return [o for o in objs if o.type == 'MESH' and not o.hide_render]


def world_verts(meshes):
    pts = []
    dg = bpy.context.evaluated_depsgraph_get()
    for o in meshes:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        mw = o.matrix_world
        pts += [mw @ v.co for v in me.vertices]
        ev.to_mesh_clear()
    return pts


def normalize(meshes, d):
    """une tudo num pai, aplica a orientação das armas/escudos e escala para SIZE unidades."""
    root = bpy.data.objects.new('root', None)
    bpy.context.scene.collection.objects.link(root)
    tops = {o for o in meshes}
    for o in list(tops):
        p = o
        while p.parent is not None:
            p = p.parent
        if p.parent is None and p is not root:
            p.parent = root
    bpy.context.view_layer.update()
    pts = world_verts(meshes)
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    sz = hi - lo
    weapon = d['kind'] in ('Sword', 'Axe', 'Staff', 'Lance', 'Bow', 'Gauntlet', 'Tome')
    if d['kind'] == 'Shield':
        # face mais fina para a câmera (câmera olha para +Y)
        if sz.x <= sz.y and sz.x <= sz.z:
            root.rotation_euler = (0, 0, math.radians(-90))
        elif sz.z <= sz.x and sz.z <= sz.y:
            root.rotation_euler = (math.radians(90), 0, 0)
    elif weapon:
        # eixo mais longo na vertical (Z)
        if sz.y >= sz.x and sz.y >= sz.z:
            root.rotation_euler = (math.radians(-90), 0, 0)
        elif sz.x >= sz.y and sz.x >= sz.z:
            root.rotation_euler = (0, math.radians(90), 0)
    bpy.context.view_layer.update()
    pts = world_verts(meshes)
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    k = SIZE / max(hi - lo)
    c = (lo + hi) / 2
    root.scale = (k, k, k)
    root.location = -c * k
    bpy.context.view_layer.update()
    return weapon


def camera_lights(meshes, d, weapon, W, H):
    sc = bpy.context.scene
    pitch = 18 if d['slot'] == 'Mount' else 6 if (weapon or d['kind'] == 'Shield') else 14
    yaw = 0 if d['slot'] == 'Wings' else 22 if (weapon or d['kind'] == 'Shield') else 28
    rot = Euler((math.radians(90 - pitch), 0, math.radians(-yaw)), 'XYZ')
    R = rot.to_matrix()
    right, up, back = R.col[0], R.col[1], R.col[2]
    pts = world_verts(meshes)
    xs = [p.dot(right) for p in pts]
    ys = [p.dot(up) for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    wx, wy = max(xs) - min(xs), max(ys) - min(ys)
    cam = bpy.data.cameras.new('cam')
    cam.type = 'ORTHO'
    asp = W / H
    if asp >= 1:
        cam.sensor_fit = 'HORIZONTAL'
        cam.ortho_scale = max(wx, wy * asp) * 1.1
    else:
        cam.sensor_fit = 'VERTICAL'
        cam.ortho_scale = max(wy, wx / asp) * 1.1
    cam.clip_start = 1
    cam.clip_end = 6000
    co = bpy.data.objects.new('cam', cam)
    sc.collection.objects.link(co)
    co.rotation_euler = rot
    co.location = right * cx + up * cy + back * 2000
    sc.camera = co
    sc.render.resolution_x = W * 2
    sc.render.resolution_y = H * 2
    # luz do kit, no referencial da câmera: chave quente de cima-esquerda-frente, contraluz de brasa atrás-direita
    for o in [o for o in bpy.data.objects if o.type == 'LIGHT']:
        bpy.data.objects.remove(o)
    tgt = right * cx + up * cy

    def area(name, r, u, b, size, energy, color):
        o = kzb.add_area(name, tgt + right * r + up * u + back * b, (0, 0, 0), size, energy, color)
        kzb.point_at(o, tgt)
        return o
    area('key', -600, 700, 900, 700, 8.0e6, (1.0, 0.88, 0.7))
    area('fill', 700, 200, 700, 900, 2.0e6, (0.7, 0.8, 1.0))
    area('rim', 650, 350, -700, 400, 6.0e6, (1.0, 0.55, 0.22))
    area('rim2', -650, 300, -650, 400, 3.0e6, (1.0, 0.8, 0.6))
    top = area('top', 0, 1300, 200, 1600, 1.0e6, (1.0, 0.95, 0.88))
    top.visible_glossy = False


def outline(im):
    """contorno escuro de 2 px por fora (lê em qualquer fundo), como o IconBaker fazia."""
    a = np.asarray(im.split()[3]).astype(np.float32) / 255
    big = Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5))
    halo = np.asarray(big.filter(ImageFilter.GaussianBlur(0.8))).astype(np.float32) / 255 * 0.85
    base = np.zeros((im.height, im.width, 4), np.float32)
    base[..., 0], base[..., 1], base[..., 2] = 0.02, 0.012, 0.01
    base[..., 3] = halo
    src = np.asarray(im).astype(np.float32) / 255
    sa = src[..., 3:4]
    outa = sa + base[..., 3:4] * (1 - sa)
    rgb = (src[..., :3] * sa + base[..., :3] * base[..., 3:4] * (1 - sa)) / np.maximum(outa, 1e-6)
    res = np.concatenate([rgb, outa], -1)
    return Image.fromarray((np.clip(res, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA')


def render_item(d, path, tint):
    kzb.reset(SAMPLES)
    sc = bpy.context.scene
    sc.world.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.1
    sc.cycles.max_bounces = 6
    sc.view_settings.exposure = 1.0          # peças low poly escuras: um pouco mais de luz para ler na grade
    for o in [o for o in bpy.data.objects if o.type == 'LIGHT']:
        bpy.data.objects.remove(o)
    meshes = import_model(path)
    if not meshes:
        print('   (sem malha)', path)
        return False
    maps = find_maps(path)
    textured = any(s.material and s.material.name.startswith('KZ_EquipMaps') for o in meshes for s in o.material_slots)
    if textured:
        if maps is None:
            print('   (sem as texturas assadas em Textures/Equip: fica para o Unity)', path)
            return False
        mt = textured_material(maps, tint)
        for o in meshes:
            for s in o.material_slots:
                s.material = mt
    cache = {}
    for o in meshes if not textured else []:
        for s in o.material_slots:
            if s.material is None or s.material.name not in cache:
                cache[s.material.name if s.material else None] = item_material(s.material, tint)
            s.material = cache[s.material.name if s.material else None]
    for o in meshes:   # sombreado facetado como no jogo: quinas acima de 38° ficam duras
        me = o.data
        if me.attributes.get('custom_normal') is not None:
            me.attributes.remove(me.attributes['custom_normal'])
        for p in me.polygons:
            p.use_smooth = True
        try:
            me.set_sharp_from_angle(angle=math.radians(38))
        except Exception:
            pass
    weapon = normalize(meshes, d)
    gw, gh = grid_size(d)
    W, H = gw * CELL, gh * CELL
    camera_lights(meshes, d, weapon, W, H)
    tmp = os.path.join(OUT, f'_tmp_{d["id"]}.png')
    kzb.render(tmp)
    im = Image.open(tmp).convert('RGBA').resize((W, H), Image.LANCZOS)
    os.remove(tmp)
    outline(im).save(os.path.join(OUT, f'item_{d["id"]}.png'))
    return True


def main(ids):
    os.makedirs(OUT, exist_ok=True)
    weapons, extra, sets, tints = gear_tables()
    done = 0
    for d in items():
        if ids and d['id'] not in ids:
            continue
        path = model_for(d, weapons, extra, sets)
        if not path or not os.path.exists(os.path.join(MODELS, path + '.fbx')):
            continue
        print('->', d['id'], path, flush=True)
        if render_item(d, path, tints.get(d['id'], (1.0, 1.0, 1.0))):
            done += 1
    print('ícones:', done)


if __name__ == '__main__':
    main({int(a) for a in sys.argv[1:]})
