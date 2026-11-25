# Kezor — biblioteca de modelagem low poly para o Blender (bpy + bmesh).
# Convenção: Z para cima, frente do modelo = -Y (vista Front do Blender), esquerda do personagem = +X.
# Exportando com Forward -Z / Up Y + Apply Transform, a frente vira +Z no Unity.
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Euler


def _lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hexc(h):
    h = h.lstrip('#')
    if len(h) == 3:
        h = ''.join(ch * 2 for ch in h)
    return tuple(_lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4))


def mat(name, hexs, emit=0.0, rough=0.85, metal=0.0):
    key = 'K_' + name
    m = bpy.data.materials.get(key) or bpy.data.materials.new(key)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    bsdf = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None:
        bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
        out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None) or nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(bsdf.outputs[0], out.inputs[0])
    c = hexc(hexs)

    def setin(ident, val):
        for s in bsdf.inputs:
            if s.identifier == ident:
                s.default_value = val
                return

    setin('Base Color', (*c, 1.0))
    setin('Roughness', rough)
    setin('Metallic', metal)
    if emit > 0:
        setin('Emission Color', (*c, 1.0))
        setin('Emission Strength', emit)
    else:
        setin('Emission Strength', 0.0)
    m.diffuse_color = (*c, 1.0)
    m["kezor_hex"] = hexs
    return m


class B:
    """Acumula geometria flat-shaded com material por face."""

    def __init__(s, scale=1.0):
        s.bm = bmesh.new()
        s.mats = []
        s.M = Matrix.Identity(4)
        s.k = scale

    def mi(s, m):
        if m not in s.mats:
            s.mats.append(m)
        return s.mats.index(m)

    def tf(s, loc=(0, 0, 0), rot=(0, 0, 0), scl=(1, 1, 1)):
        s.M = Matrix.LocRotScale(Vector(loc), Euler(tuple(math.radians(a) for a in rot), 'XYZ'), Vector(scl))
        return s

    def reset(s):
        s.M = Matrix.Identity(4)
        return s

    def poly(s, pts, faces, m):
        vs = [s.bm.verts.new((s.M @ Vector(p)) * s.k) for p in pts]
        out = []
        for j, f in enumerate(faces):
            mm = m[j] if isinstance(m, (list, tuple)) else m
            try:
                fa = s.bm.faces.new([vs[i] for i in f])
            except ValueError:
                continue
            fa.material_index = s.mi(mm)
            fa.smooth = False
            out.append(fa)
        return vs

    # caixa com topo escalado (top) e deslocado (shift) — serve para formas afuniladas
    def box(s, c, size, m, top=(1, 1), shift=(0, 0)):
        cx, cy, cz = c
        sx, sy, sz = [v / 2 for v in size]
        tx, ty = top
        ox, oy = shift
        pts = [(cx - sx, cy - sy, cz - sz), (cx + sx, cy - sy, cz - sz), (cx + sx, cy + sy, cz - sz), (cx - sx, cy + sy, cz - sz),
               (cx - sx * tx + ox, cy - sy * ty + oy, cz + sz), (cx + sx * tx + ox, cy - sy * ty + oy, cz + sz),
               (cx + sx * tx + ox, cy + sy * ty + oy, cz + sz), (cx - sx * tx + ox, cy + sy * ty + oy, cz + sz)]
        faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        return s.poly(pts, faces, m)

    # perfil de revolução: prof = [(raio, z), ...]; raio 0 = ponta
    def lathe(s, c, prof, n, m, rot0=None, sx=1.0, sy=1.0, caps=True):
        cx, cy, cz = c
        a0 = math.pi / n if rot0 is None else rot0
        rings, pts = [], []
        for (r, z) in prof:
            if r <= 1e-6:
                rings.append([len(pts)])
                pts.append((cx, cy, cz + z))
            else:
                ring = []
                for i in range(n):
                    a = a0 + 2 * math.pi * i / n
                    ring.append(len(pts))
                    pts.append((cx + math.cos(a) * r * sx, cy + math.sin(a) * r * sy, cz + z))
                rings.append(ring)
        faces, fm = [], []
        for k in range(len(rings) - 1):
            A, Bv = rings[k], rings[k + 1]
            mm = m[k] if isinstance(m, (list, tuple)) else m
            if len(A) == 1 and len(Bv) == 1:
                continue
            for i in range(n):
                j = (i + 1) % n
                if len(A) == 1:
                    faces.append((A[0], Bv[j], Bv[i]))
                elif len(Bv) == 1:
                    faces.append((A[i], A[j], Bv[0]))
                else:
                    faces.append((A[i], A[j], Bv[j], Bv[i]))
                fm.append(mm)
        if caps:
            m0 = m[0] if isinstance(m, (list, tuple)) else m
            m1 = m[-1] if isinstance(m, (list, tuple)) else m
            if len(rings[0]) > 1:
                faces.append(tuple(reversed(rings[0]))); fm.append(m0)
            if len(rings[-1]) > 1:
                faces.append(tuple(rings[-1])); fm.append(m1)
        return s.poly(pts, faces, fm)

    def cyl(s, c, r1, r2, h, n, m, **kw):
        return s.lathe(c, [(r1, 0), (r2, h)], n, m, **kw)

    def cone(s, c, r, h, n, m, **kw):
        return s.lathe(c, [(r, 0), (0, h)], n, m, **kw)

    def gem(s, c, r, h, m, n=6):
        return s.lathe((c[0], c[1], c[2] - h / 2), [(0, 0), (r, h * 0.42), (0, h)], n, m)

    def ico(s, c, scl, m, jit=0.0, seed=0, sub=1, m2=None, zmin=None, share=0.3):
        tmp = bmesh.new()
        bmesh.ops.create_icosphere(tmp, subdivisions=sub, radius=1.0)
        rnd = random.Random(seed)
        idx, pts = {}, []
        for i, v in enumerate(tmp.verts):
            p = v.co.copy()
            if jit:
                p *= (1 + rnd.uniform(-jit, jit))
            q = [c[0] + p.x * scl[0], c[1] + p.y * scl[1], c[2] + p.z * scl[2]]
            if zmin is not None and q[2] < zmin:
                q[2] = zmin
            pts.append(tuple(q))
            idx[v] = i
        faces = [tuple(idx[v] for v in f.verts) for f in tmp.faces]
        ms = [m2 if (m2 is not None and rnd.random() < share) else m for _ in faces]
        tmp.free()
        return s.poly(pts, faces, ms)

    # caixa estendida entre dois pontos (ossos, galhos, pernas). w = largura, w2 = largura na ponta
    def seg(s, p0, p1, w, m, w2=None, d=None, d2=None, up=(0, 0, 1)):
        p0, p1 = Vector(p0), Vector(p1)
        w2 = w if w2 is None else w2
        d = w if d is None else d
        d2 = (d * w2 / w) if d2 is None else d2
        ax = (p1 - p0).normalized()
        u = Vector(up).cross(ax)
        if u.length < 1e-4:
            u = Vector((1, 0, 0)).cross(ax)
        u.normalize()
        v = ax.cross(u).normalized()
        pts = []
        for p, ww, dd in ((p0, w, d), (p1, w2, d2)):
            for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                pts.append(tuple(p + u * sx * ww / 2 + v * sy * dd / 2))
        faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        return s.poly(pts, faces, m)

    # polígono 2D no plano XZ extrudado em Y (escudos, lâminas, placas)
    def prism(s, pts2, y0, y1, m, mside=None):
        n = len(pts2)
        pts = [(x, y0, z) for x, z in pts2] + [(x, y1, z) for x, z in pts2]
        faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))]
        ms = [m, m]
        for i in range(n):
            j = (i + 1) % n
            faces.append((i, j, n + j, n + i))
            ms.append(mside or m)
        return s.poly(pts, faces, ms)

    # telhado de duas águas, cumeeira ao longo de X
    def roof(s, c, w, d, h, m, mg=None, t=0.18):
        cx, cy, cz = c
        mg = mg or m
        pts = [(cx - w / 2, cy - d / 2, cz), (cx + w / 2, cy - d / 2, cz), (cx + w / 2, cy + d / 2, cz), (cx - w / 2, cy + d / 2, cz),
               (cx - w / 2, cy, cz + h), (cx + w / 2, cy, cz + h)]
        faces = [(0, 1, 5, 4), (2, 3, 4, 5), (3, 0, 4), (1, 2, 5), (0, 3, 2, 1)]
        return s.poly(pts, faces, [m, m, mg, mg, m])

    def tris(s):
        return sum(len(f.verts) - 2 for f in s.bm.faces)

    def obj(s, name, coll, parent=None, pivot=(0, 0, 0), part=None):
        bmesh.ops.recalc_face_normals(s.bm, faces=s.bm.faces[:])
        pv = Vector(pivot) * s.k
        for v in s.bm.verts:
            v.co -= pv
        me = bpy.data.meshes.new(name)
        s.bm.to_mesh(me)
        s.bm.free()
        for m in s.mats:
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = False
        o = bpy.data.objects.new(name, me)
        coll.objects.link(o)
        o["pivot"] = tuple(pv)
        o["part"] = part or name
        if parent is not None:
            o.parent = parent
            o.location = pv - Vector(parent["pivot"])
        else:
            o.location = pv
        return o


def collection(name, parent=None):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(c)
    return c


def root(name, coll, category):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'ARROWS'
    e.empty_display_size = 0.4
    coll.objects.link(e)
    e["pivot"] = (0.0, 0.0, 0.0)
    e["kezor_root"] = True
    e["category"] = category
    return e


def wipe(name):
    """Remove um modelo anterior com o mesmo nome (só objetos criados por este script)."""
    o = bpy.data.objects.get(name)
    if o is None:
        return
    for ch in list(o.children_recursive) + [o]:
        me = ch.data if ch.type == 'MESH' else None
        bpy.data.objects.remove(ch, do_unlink=True)
        if me is not None and me.users == 0:
            bpy.data.meshes.remove(me)


def model_tris(rootobj):
    t = 0
    for ch in rootobj.children_recursive:
        if ch.type == 'MESH':
            t += sum(len(p.vertices) - 2 for p in ch.data.polygons)
    return t
