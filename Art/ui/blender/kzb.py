"""
kzb: utilitários para modelar e renderizar a arte da interface do Kezor no Blender (bpy 5.x, Cycles).

Unidade da cena = 1 px de interface na tela (referência 1600x900). As imagens saem em 2x (RES = 2 px por unidade),
casando com -unity-slice-scale: 0.5 na USS. Câmera ortográfica olhando para baixo (-Z): X para a direita, Y para cima.
"""
import math
import os

import bpy
import bmesh
from mathutils import Vector

RES = 2


# ----------------------------------------------------------------------------------------------- cena

def reset(samples=96):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 8
    sc.cycles.transmission_bounces = 8
    sc.cycles.glossy_bounces = 6
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.image_settings.color_depth = '8'
    sc.view_settings.view_transform = os.environ.get('KZ_VIEW', 'Standard')
    try:
        sc.view_settings.look = 'None'
    except Exception:
        pass
    sc.view_settings.exposure = 0.0
    world()
    lights()
    return sc


def world(strength=0.35):
    w = bpy.data.worlds.new('W')
    bpy.context.scene.world = w
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes['Background']
    # céu em gradiente quente em cima, escuro embaixo: dá reflexo de "sala iluminada por tochas" no metal
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    rmp = nt.nodes.new('ShaderNodeValToRGB')
    rmp.color_ramp.elements[0].position = 0.35
    rmp.color_ramp.elements[0].color = (0.02, 0.015, 0.012, 1)
    rmp.color_ramp.elements[1].position = 1.0
    rmp.color_ramp.elements[1].color = (1.0, 0.82, 0.6, 1)
    mr = nt.nodes.new('ShaderNodeMapRange')
    mr.inputs['From Min'].default_value = -1
    mr.inputs['From Max'].default_value = 1
    nt.links.new(tc.outputs['Generated'], sep.inputs[0])
    nt.links.new(tc.outputs['Window'] if False else tc.outputs['Generated'], sep.inputs[0])
    # usa a direção do raio (Normal no fundo = direção)
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    sep2 = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(geo.outputs['Incoming'], sep2.inputs[0])
    # Incoming aponta para a câmera; inverte z para "para cima"
    neg = nt.nodes.new('ShaderNodeMath')
    neg.operation = 'MULTIPLY'
    neg.inputs[1].default_value = -1
    nt.links.new(sep2.outputs['Z'], neg.inputs[0])
    nt.links.new(neg.outputs[0], mr.inputs['Value'])
    nt.links.new(mr.outputs['Result'], rmp.inputs['Fac'])
    nt.links.new(rmp.outputs['Color'], bg.inputs['Color'])
    bg.inputs['Strength'].default_value = strength


def add_area(name, loc, rot, size, energy, color=(1, 1, 1), shape='DISK'):
    l = bpy.data.lights.new(name, 'AREA')
    l.energy = energy
    l.size = size
    l.shape = shape
    l.color = color
    o = bpy.data.objects.new(name, l)
    bpy.context.scene.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = rot
    return o


def lights(scale=1.0, cx=0, cy=0):
    """luz principal de cima-esquerda (quente), preenchimento frio fraco, contraluz de brasa embaixo-direita."""
    for o in [o for o in bpy.data.objects if o.type == 'LIGHT']:
        bpy.data.objects.remove(o)
    s = scale
    key = add_area('key', (cx - 600 * s, cy + 700 * s, 900 * s), (0, 0, 0), 500 * s, 1.6e7 * s * s, (1.0, 0.88, 0.7))
    point_at(key, (cx, cy, 0))
    fill = add_area('fill', (cx + 700 * s, cy + 300 * s, 700 * s), (0, 0, 0), 900 * s, 1.2e6 * s * s, (0.7, 0.8, 1.0))
    point_at(fill, (cx, cy, 0))
    rim = add_area('rim', (cx + 500 * s, cy - 700 * s, 260 * s), (0, 0, 0), 500 * s, 3.0e6 * s * s, (1.0, 0.45, 0.15))
    point_at(rim, (cx, cy, 0))
    top = add_area('top', (cx, cy, 1200 * s), (0, 0, 0), 1600 * s, 1.2e6 * s * s, (1.0, 0.95, 0.88), 'SQUARE')
    top.visible_glossy = False      # não espelha nas faces planas (a mesa das gemas ficava rosada)


def point_at(o, target):
    d = Vector(target) - o.location
    o.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def camera(x0, y0, x1, y1, res=RES):
    sc = bpy.context.scene
    cam = bpy.data.cameras.new('cam')
    cam.type = 'ORTHO'
    w, h = x1 - x0, y1 - y0
    cam.ortho_scale = max(w, h)
    cam.clip_start = 1
    cam.clip_end = 5000
    co = bpy.data.objects.new('cam', cam)
    sc.collection.objects.link(co)
    co.location = ((x0 + x1) / 2, (y0 + y1) / 2, 2000)
    sc.camera = co
    sc.render.resolution_x = int(round(w * res))
    sc.render.resolution_y = int(round(h * res))
    sc.render.resolution_percentage = 100
    cam.sensor_fit = 'HORIZONTAL' if w >= h else 'VERTICAL'
    return co


def render(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc = bpy.context.scene
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


# ----------------------------------------------------------------------------------------------- materiais

def _nodes(mat):
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    return nt, bsdf, out


def _noise(nt, scale, detail=6, rough=0.6, coord='Object'):
    tc = nt.nodes.new('ShaderNodeTexCoord')
    n = nt.nodes.new('ShaderNodeTexNoise')
    n.inputs['Scale'].default_value = scale
    n.inputs['Detail'].default_value = detail
    n.inputs['Roughness'].default_value = rough
    nt.links.new(tc.outputs[coord], n.inputs['Vector'])
    return n


def _ao_dirt(nt, color_socket_from, dirt=0.18, dist=6.0):
    """multiplica a cor por AO (sujeira nas reentrâncias): o segredo do acabamento 'pintado à mão'."""
    ao = nt.nodes.new('ShaderNodeAmbientOcclusion')
    ao.inputs['Distance'].default_value = dist
    ao.samples = 16
    rmp = nt.nodes.new('ShaderNodeMapRange')
    rmp.inputs['From Min'].default_value = 0.0
    rmp.inputs['From Max'].default_value = 1.0
    rmp.inputs['To Min'].default_value = dirt
    rmp.inputs['To Max'].default_value = 1.0
    nt.links.new(ao.outputs['AO'], rmp.inputs['Value'])
    mul = nt.nodes.new('ShaderNodeMix')
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs['Factor'].default_value = 1.0
    nt.links.new(color_socket_from, mul.inputs['A'])
    nt.links.new(rmp.outputs['Result'], mul.inputs['B'])
    return mul.outputs['Result'], ao


def metal(name, base, rough=(0.22, 0.42), dirt=0.15, bump=0.15, noise_scale=0.08, ao_dist=6.0, hammer=0.0):
    mat = bpy.data.materials.new(name)
    nt, bsdf, out = _nodes(mat)
    bsdf.inputs['Metallic'].default_value = 1.0
    n = _noise(nt, noise_scale, 8, 0.6)
    # cor com variação leve
    cr = nt.nodes.new('ShaderNodeValToRGB')
    cr.color_ramp.elements[0].color = tuple(c * 0.78 for c in base) + (1,)
    cr.color_ramp.elements[1].color = tuple(min(1, c * 1.08) for c in base) + (1,)
    nt.links.new(n.outputs['Fac'], cr.inputs['Fac'])
    col, ao = _ao_dirt(nt, cr.outputs['Color'], dirt, ao_dist)
    nt.links.new(col, bsdf.inputs['Base Color'])
    rr = nt.nodes.new('ShaderNodeMapRange')
    rr.inputs['To Min'].default_value = rough[0]
    rr.inputs['To Max'].default_value = rough[1]
    nt.links.new(n.outputs['Fac'], rr.inputs['Value'])
    # reentrâncias mais ásperas (sem polimento)
    rm = nt.nodes.new('ShaderNodeMath')
    rm.operation = 'ADD'
    inv = nt.nodes.new('ShaderNodeMath')
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    nt.links.new(ao.outputs['AO'], inv.inputs[1])
    mul = nt.nodes.new('ShaderNodeMath')
    mul.operation = 'MULTIPLY'
    mul.inputs[1].default_value = 0.35
    nt.links.new(inv.outputs[0], mul.inputs[0])
    nt.links.new(rr.outputs['Result'], rm.inputs[0])
    nt.links.new(mul.outputs[0], rm.inputs[1])
    nt.links.new(rm.outputs[0], bsdf.inputs['Roughness'])
    if bump > 0 or hammer > 0:
        b = nt.nodes.new('ShaderNodeBump')
        b.inputs['Strength'].default_value = max(bump, hammer)
        b.inputs['Distance'].default_value = 1.0
        src = n
        if hammer > 0:
            v = nt.nodes.new('ShaderNodeTexVoronoi')
            v.inputs['Scale'].default_value = 0.35
            tc = nt.nodes.new('ShaderNodeTexCoord')
            nt.links.new(tc.outputs['Object'], v.inputs['Vector'])
            nt.links.new(v.outputs['Distance'], b.inputs['Height'])
        else:
            nt.links.new(n.outputs['Fac'], b.inputs['Height'])
        nt.links.new(b.outputs['Normal'], bsdf.inputs['Normal'])
    return mat


def gold():
    return metal('gold', (0.93, 0.66, 0.30), rough=(0.18, 0.38), dirt=0.10, bump=0.05, noise_scale=0.06, ao_dist=4.0)


def gold_pale():
    return metal('gold_pale', (0.96, 0.80, 0.52), rough=(0.16, 0.32), dirt=0.12, bump=0.05, noise_scale=0.06, ao_dist=4.0)


def bronze():
    return metal('bronze', (0.62, 0.40, 0.20), rough=(0.3, 0.5), dirt=0.12, bump=0.08, noise_scale=0.07, ao_dist=5.0)


def iron():
    return metal('iron', (0.13, 0.11, 0.10), rough=(0.38, 0.62), dirt=0.25, bump=0.0, noise_scale=0.05, ao_dist=8.0, hammer=0.25)


def steel_dark():
    return metal('steel', (0.30, 0.29, 0.30), rough=(0.25, 0.45), dirt=0.2, bump=0.08, noise_scale=0.05, ao_dist=6.0)


def leather(base=(0.045, 0.032, 0.026), name='leather', grain=1.6, rough=0.85):
    mat = bpy.data.materials.new(name)
    nt, bsdf, out = _nodes(mat)
    n = _noise(nt, 0.015, 6, 0.55)
    v = nt.nodes.new('ShaderNodeTexVoronoi')
    v.inputs['Scale'].default_value = grain
    tc = nt.nodes.new('ShaderNodeTexCoord')
    nt.links.new(tc.outputs['Object'], v.inputs['Vector'])
    cr = nt.nodes.new('ShaderNodeValToRGB')
    cr.color_ramp.elements[0].color = tuple(c * 0.55 for c in base) + (1,)
    cr.color_ramp.elements[1].color = tuple(c * 1.6 for c in base) + (1,)
    nt.links.new(n.outputs['Fac'], cr.inputs['Fac'])
    col, ao = _ao_dirt(nt, cr.outputs['Color'], 0.35, 10)
    nt.links.new(col, bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = rough
    try:
        bsdf.inputs['Specular IOR Level'].default_value = 0.12
    except KeyError:
        pass
    b = nt.nodes.new('ShaderNodeBump')
    b.inputs['Strength'].default_value = 0.25
    nt.links.new(v.outputs['Distance'], b.inputs['Height'])
    nt.links.new(b.outputs['Normal'], bsdf.inputs['Normal'])
    return mat


def stone(base=(0.06, 0.05, 0.045), name='stone'):
    mat = bpy.data.materials.new(name)
    nt, bsdf, out = _nodes(mat)
    n = _noise(nt, 0.02, 10, 0.65)
    n2 = _noise(nt, 0.3, 4, 0.5)
    cr = nt.nodes.new('ShaderNodeValToRGB')
    cr.color_ramp.elements[0].color = tuple(c * 0.5 for c in base) + (1,)
    cr.color_ramp.elements[1].color = tuple(c * 1.7 for c in base) + (1,)
    nt.links.new(n.outputs['Fac'], cr.inputs['Fac'])
    col, ao = _ao_dirt(nt, cr.outputs['Color'], 0.3, 10)
    nt.links.new(col, bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.8
    b = nt.nodes.new('ShaderNodeBump')
    b.inputs['Strength'].default_value = 0.35
    nt.links.new(n2.outputs['Fac'], b.inputs['Height'])
    nt.links.new(b.outputs['Normal'], bsdf.inputs['Normal'])
    return mat


def lacquer(base=(0.16, 0.012, 0.006), name='lacquer', rough=0.18):
    mat = bpy.data.materials.new(name)
    nt, bsdf, out = _nodes(mat)
    n = _noise(nt, 0.03, 6, 0.5)
    cr = nt.nodes.new('ShaderNodeValToRGB')
    cr.color_ramp.elements[0].color = tuple(c * 0.6 for c in base) + (1,)
    cr.color_ramp.elements[1].color = tuple(c * 1.3 for c in base) + (1,)
    nt.links.new(n.outputs['Fac'], cr.inputs['Fac'])
    col, ao = _ao_dirt(nt, cr.outputs['Color'], 0.3, 8)
    nt.links.new(col, bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = rough
    try:
        bsdf.inputs['Coat Weight'].default_value = 0.6
        bsdf.inputs['Coat Roughness'].default_value = 0.08
    except KeyError:
        pass
    return mat


def gem_mat(color=(1.0, 0.12, 0.03), glow=6.0, name='gem'):
    """pedra lapidada opaca e muito especular: as facetas planas pegam a luz e cintilam."""
    mat = bpy.data.materials.new(name)
    nt, bsdf, out = _nodes(mat)
    bsdf.inputs['Base Color'].default_value = color + (1,)
    bsdf.inputs['Roughness'].default_value = 0.04
    bsdf.inputs['IOR'].default_value = 2.4
    try:
        bsdf.inputs['Specular IOR Level'].default_value = 0.18
        bsdf.inputs['Coat Weight'].default_value = 0.0
        bsdf.inputs['Coat Roughness'].default_value = 0.02
    except KeyError:
        pass
    # brilho interno: mais forte no centro da pedra (fresnel invertido)
    lw = nt.nodes.new('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.4
    inv = nt.nodes.new('ShaderNodeMath')
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    nt.links.new(lw.outputs['Facing'], inv.inputs[1])
    mul = nt.nodes.new('ShaderNodeMath')
    mul.operation = 'MULTIPLY'
    mul.inputs[1].default_value = glow * 0.05
    nt.links.new(inv.outputs[0], mul.inputs[0])
    bsdf.inputs['Emission Color'].default_value = tuple(min(1, c * 1.1) for c in color) + (1,)
    nt.links.new(mul.outputs[0], bsdf.inputs['Emission Strength'])
    return mat


def emissive(color, strength, name='emit'):
    mat = bpy.data.materials.new(name)
    nt, bsdf, out = _nodes(mat)
    bsdf.inputs['Base Color'].default_value = (0, 0, 0, 1)
    bsdf.inputs['Emission Color'].default_value = color + (1,)
    bsdf.inputs['Emission Strength'].default_value = strength
    return mat


def flat(color, name='flat', rough=0.5):
    mat = bpy.data.materials.new(name)
    nt, bsdf, out = _nodes(mat)
    bsdf.inputs['Base Color'].default_value = color + (1,)
    bsdf.inputs['Roughness'].default_value = rough
    return mat


# ----------------------------------------------------------------------------------------------- geometria

def obj_from_bmesh(bm, name, mat=None, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    if mat:
        me.materials.append(mat)
    return o


def sweep(path, profile, mat, name='sweep', closed=True, z0=0.0, flip=False):
    """
    Varre um perfil ao longo de um caminho poligonal no plano XY com cantos em meia-esquadria.
    path: [(x,y)] no sentido anti-horário (o "dentro" fica à esquerda); profile: [(d, h)] com d = distância para
    dentro a partir do caminho e h = altura. Retorna o objeto.
    """
    n = len(path)
    pts = [Vector((p[0], p[1], 0)) for p in path]
    rings = []
    rng = range(n) if closed else range(n)
    for i in rng:
        p = pts[i]
        if closed:
            a = pts[i - 1]
            b = pts[(i + 1) % n]
        else:
            a = pts[i - 1] if i > 0 else p - (pts[1] - p)
            b = pts[i + 1] if i < n - 1 else p + (p - pts[i - 1])
        t1 = (p - a).normalized()
        t2 = (b - p).normalized()
        n1 = Vector((-t1.y, t1.x, 0))
        n2 = Vector((-t2.y, t2.x, 0))
        m = (n1 + n2)
        if m.length < 1e-6:
            m = n1
        m.normalize()
        sc = 1.0 / max(0.2, m.dot(n1))
        ring = []
        for d, h in profile:
            v = p + m * (d * sc)
            ring.append(Vector((v.x, v.y, z0 + h)))
        rings.append(ring)
    bm = bmesh.new()
    vs = [[bm.verts.new(v) for v in r] for r in rings]
    m = len(profile)
    segs = n if closed else n - 1
    for i in range(segs):
        r0, r1 = vs[i], vs[(i + 1) % n]
        for j in range(m - 1):
            f = (r0[j], r1[j], r1[j + 1], r0[j + 1])
            try:
                face = bm.faces.new(f if not flip else f[::-1])
            except ValueError:
                pass
    bm.normal_update()
    o = obj_from_bmesh(bm, name, mat)
    # normais para cima
    me = o.data
    up = sum((p.normal.z for p in me.polygons))
    if up < 0:
        for p in me.polygons:
            p.flip()
    return o


def round_profile(d0, d1, h0, h1, segs=10, base=0.0):
    """meia-cana (cordão) de d0 a d1, de altura h1 sobre a base h0."""
    out = [(d0, base)]
    for i in range(segs + 1):
        t = i / segs
        a = math.pi * t
        out.append((d0 + (d1 - d0) * t, h0 + (h1 - h0) * math.sin(a)))
    out.append((d1, base))
    return out


def tube(points, radii, mat, name='tube', flatten=0.65, res=6, z=0.0):
    """tubo arredondado por pontos (filigrana): curva poly com bevel e raio por ponto; achatado em Z."""
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'
    cu.bevel_depth = 1.0
    cu.bevel_resolution = res
    cu.use_fill_caps = True
    sp = cu.splines.new('POLY')
    sp.points.add(len(points) - 1)
    for i, (p, r) in enumerate(zip(points, radii)):
        zz = p[2] if len(p) > 2 else z
        sp.points[i].co = (p[0], p[1], zz, 1)
        sp.points[i].radius = r
    o = bpy.data.objects.new(name, cu)
    bpy.context.scene.collection.objects.link(o)
    o.scale = (1, 1, flatten)
    cu.materials.append(mat)
    return o


def spiral_pts(cx, cy, r0, r1, a0, turns, cw=True, steps=60, z=0.0):
    out = []
    for i in range(steps + 1):
        t = i / steps
        a = a0 + (-1 if cw else 1) * turns * 2 * math.pi * t
        r = r0 + (r1 - r0) * t
        out.append((cx + math.cos(a) * r, cy + math.sin(a) * r, z))
    return out


def bez(p0, p1, p2, p3, steps=30, z=0.0):
    out = []
    for i in range(steps + 1):
        t = i / steps
        u = 1 - t
        out.append((u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
                    u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1], z))
    return out


def scroll(mat, x0, y0, x1, y1, curl, w0, w1, bend=0.2, cw=True, turns=1.2, z=0.0, name='scroll', flatten=0.65):
    """arabesco: curva até (x1,y1) que termina numa voluta (espiral) — tubo afinando de w0 para w1."""
    dx, dy = x1 - x0, y1 - y0
    nx, ny = -dy * bend, dx * bend
    if not cw:
        nx, ny = -nx, -ny
    a = bez((x0, y0), (x0 + dx * .33 + nx, y0 + dy * .33 + ny), (x0 + dx * .66 + nx, y0 + dy * .66 + ny), (x1, y1), 30, z)
    ang = math.atan2(y1 - a[-2][1], x1 - a[-2][0])
    side = -1 if cw else 1
    cx = x1 + math.cos(ang + side * math.pi / 2) * curl
    cy = y1 + math.sin(ang + side * math.pi / 2) * curl
    a0 = math.atan2(y1 - cy, x1 - cx)
    s = spiral_pts(cx, cy, curl, curl * 0.12, a0, turns, cw=cw, steps=50, z=z)
    pts = a + s[1:]
    nn = len(pts)
    radii = [w0 + (w1 - w0) * (i / (nn - 1)) ** 0.8 for i in range(nn)]
    o = tube(pts, radii, mat, name, flatten)
    ball(mat, s[-1][0], s[-1][1], w1 * 1.5, z, name + '_ball', flatten)
    return o


def ball(mat, x, y, r, z=0.0, name='ball', flatten=0.6):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=r)
    bmesh.ops.scale(bm, vec=(1, 1, flatten), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(x, y, z), verts=bm.verts)
    return obj_from_bmesh(bm, name, mat)


def leaf(mat, x, y, ang, length, width, z=0.0, name='leaf', h=None, curl=0.0):
    """folha de acanto: lâmina abaulada com nervura central, levemente curvada."""
    bm = bmesh.new()
    N, M = 16, 6
    h = h if h is not None else width * 0.6
    grid = []
    ca, sa = math.cos(ang), math.sin(ang)
    for i in range(N + 1):
        t = i / N
        w = math.sin(t * math.pi) ** 0.75 * width * (1 - 0.3 * t)
        row = []
        for j in range(M + 1):
            u = j / M * 2 - 1
            px = t * length
            py = u * w + curl * t * t * length
            pz = h * (1 - u * u) * math.sin(t * math.pi) ** 0.5 - abs(u) * h * 0.1
            row.append(bm.verts.new((x + px * ca - py * sa, y + px * sa + py * ca, z + pz)))
        grid.append(row)
    for i in range(N):
        for j in range(M):
            bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    bm.normal_update()
    o = obj_from_bmesh(bm, name, mat)
    if sum(p.normal.z for p in o.data.polygons) < 0:
        for p in o.data.polygons:
            p.flip()
    return o


def gem(mat, x, y, rx, ry=None, z=0.0, facets=12, crown=0.6, name='gem', table=0.42):
    """gema lapidada (brilhante oval): mesa, coroa e pavilhão."""
    ry = ry or rx
    bm = bmesh.new()
    gird = []
    tab = []
    for i in range(facets):
        a = 2 * math.pi * i / facets
        gird.append(bm.verts.new((x + math.cos(a) * rx, y + math.sin(a) * ry, z)))
        a2 = a + math.pi / facets
        tab.append(bm.verts.new((x + math.cos(a2) * rx * table, y + math.sin(a2) * ry * table, z + min(rx, ry) * crown)))
    bot = bm.verts.new((x, y, z - min(rx, ry) * 0.9))
    for i in range(facets):
        j = (i + 1) % facets
        bm.faces.new((gird[i], gird[j], tab[i]))
        bm.faces.new((tab[i], gird[j], tab[j]))
        bm.faces.new((gird[j], gird[i], bot))
    bm.faces.new(tab)
    bm.normal_update()
    return obj_from_bmesh(bm, name, mat, smooth=False)


def bezel(mat, x, y, rx, ry, w, h, z=0.0, name='bezel', segs=48):
    """engaste: anel arredondado em volta da gema."""
    path = [(x + math.cos(2 * math.pi * i / segs) * (rx + w), y + math.sin(2 * math.pi * i / segs) * (ry + w)) for i in range(segs)]
    prof = round_profile(0, w, 0, h, 8)
    return sweep(path, prof, mat, name, z0=z)


def set_gem(gmat, bmat, x, y, r, z=0.0, ry=None, name='g', claws=4, facets=12):
    ry = ry or r
    bezel(bmat, x, y, r * 0.98, ry * 0.98, r * 0.32, r * 0.42, z, name + '_bz')
    gem(gmat, x, y, r, ry, z + r * 0.18, facets, name=name)
    for i in range(claws):
        a = 2 * math.pi * (i + 0.5) / claws + math.pi / 4
        ball(bmat, x + math.cos(a) * r * 0.98, y + math.sin(a) * ry * 0.98, r * 0.2, z + r * 0.38, name + f'_c{i}', 0.9)


def plate(path, h, mat, name='plate', bevel=2.0, z0=0.0, bsegs=4):
    """placa plana com borda arredondada (contorno anti-horário)."""
    prof = []
    for i in range(bsegs + 1):
        a = math.pi / 2 * i / bsegs
        prof.append((bevel * (1 - math.cos(a)), (h - bevel) + bevel * math.sin(a)))
    prof = [(0, 0)] + prof
    o = sweep(path, prof, mat, name + '_edge', z0=z0)
    # tampa
    bm = bmesh.new()
    inset = bevel
    # tampa = polígono deslocado para dentro: usa o último anel do sweep
    me = o.data
    n = len(path)
    m = len(prof)
    verts = [me.vertices[i * m + (m - 1)].co.copy() for i in range(n)]
    vv = [bm.verts.new(v) for v in verts]
    try:
        f = bm.faces.new(vv)
    except ValueError:
        f = None
    if f is not None:
        bmesh.ops.triangulate(bm, faces=[f])
    bm.normal_update()
    cap = obj_from_bmesh(bm, name + '_cap', mat, smooth=False)
    if sum(p.normal.z for p in cap.data.polygons) < 0:
        for p in cap.data.polygons:
            p.flip()
    return o, cap


def chamfer_rect(x0, y0, x1, y1, c):
    """retângulo com cantos chanfrados, anti-horário (começa embaixo à esquerda)."""
    if c <= 0:
        return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


def round_rect(x0, y0, x1, y1, r, seg=6):
    pts = []
    for (cx, cy, a0) in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def mirror4(fn, cx, cy):
    """chama fn(sx, sy) para os 4 quadrantes (enfeites dos cantos)."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            fn(sx, sy)


def floor_plane(x0, y0, x1, y1, mat, z=0.0, name='floor'):
    bm = bmesh.new()
    v = [bm.verts.new((x0, y0, z)), bm.verts.new((x1, y0, z)), bm.verts.new((x1, y1, z)), bm.verts.new((x0, y1, z))]
    bm.faces.new(v)
    return obj_from_bmesh(bm, name, mat, smooth=False)


def shadow_catcher(x0, y0, x1, y1, z=-0.5):
    """plano que só recebe sombra (mantém fundo transparente com a sombra projetada)."""
    o = floor_plane(x0, y0, x1, y1, flat((0.5, 0.5, 0.5)), z, 'catcher')
    o.is_shadow_catcher = True
    return o
