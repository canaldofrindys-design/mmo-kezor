# Kezor — Cidade de Brasalume v4: kit completo da cidade murada (arte original do Kezor).
# Muralha de cantaria com ameias, torres redondas, portão fortificado, casas de enxaimel de 2 e 3 andares,
# taverna, ferraria, capela, salão da guilda, loja, estábulo, fonte monumental, piso da praça e props.
#
# Roda sozinho (não depende dos outros scripts):
#   python kezor_city4.py --out <pasta Models>            (módulo bpy do pip)   ou
#   blender -b -P kezor_city4.py -- --out <pasta Models>
# Opções: --only Nome1,Nome2   --no-ao (pula a oclusão assada)   --blend arquivo.blend (salva a cena)
#
# Convenções do projeto (iguais às do kezor_export.py / kezor_midpoly.py):
#  - Blender: Z para cima, frente do modelo = -Y, origem no chão. FBX sai Y para cima, frente +Z no Unity, escala 1.
#  - Cor no vértice 'Col' (sRGB do material × variação por peça × oclusão assada); alfa < 0,5 = emissivo.
#  - 2ª UV 'MatId' = (id da textura de detalhe, 0): 3 metal, 5 tecido, 6 madeira, 7 pedra, 8 reboco, 9 telha, 10 folhagem.
#  - Um objeto de malha por modelo, com o nome do modelo (a Fonte4 tem o filho 'Gem', que gira no jogo).
import bpy, bmesh, math, random, os, sys
from mathutils import Vector, Matrix

# ---------------------------------------------------------------------------------------------------- materiais
# nome: (cor sRGB, id da textura de detalhe, emissão)
MATS = {
    'stone':    ('#a39883', 7, 0), 'stone2':  ('#8f8572', 7, 0), 'stone_lt': ('#b9ae98', 7, 0),
    'stone_dk': ('#6c665c', 7, 0), 'basalt':  ('#4c4945', 7, 0), 'mortar':   ('#4f4840', 7, 0),
    'cobble':   ('#7d766b', 7, 0), 'slab':    ('#9a917f', 7, 0), 'slab2':    ('#857c6c', 7, 0), 'slab_rd': ('#8a6f5c', 7, 0),
    'plaster':  ('#d6c9a8', 8, 0), 'plaster2': ('#cdb894', 8, 0), 'plaster3': ('#c2a98a', 8, 0),
    'timber':   ('#45301f', 6, 0), 'wood':    ('#6b4a30', 6, 0), 'plank':    ('#7d5a3a', 6, 0), 'wood_lt': ('#94704a', 6, 0),
    'tile':     ('#8e3e2b', 9, 0), 'tile2':   ('#7a3324', 9, 0), 'tile3':    ('#9c4a33', 9, 0),
    'slate':    ('#4a515b', 7, 0), 'slate2':  ('#3f454e', 7, 0), 'slate3':   ('#565e69', 7, 0),
    'iron':     ('#36373b', 3, 0), 'gold':    ('#b8923c', 3, 0), 'bronze':   ('#8a6232', 3, 0), 'steel': ('#7d8288', 3, 0),
    'cloth_r':  ('#8c2620', 5, 0), 'cloth_b': ('#2c4676', 5, 0), 'cloth_y':  ('#b98a2a', 5, 0), 'cloth_g': ('#3d5a2c', 5, 0),
    'cloth_w':  ('#d8ccb0', 5, 0), 'cloth_dk': ('#3a2a22', 5, 0), 'rope': ('#8a7350', 5, 0),
    'leaf':     ('#4c6a2e', 10, 0), 'leaf2':  ('#5d7d35', 10, 0), 'hay': ('#c2a457', 10, 0),
    'flower_r': ('#b8332c', 10, 0), 'flower_y': ('#d9b234', 10, 0), 'flower_v': ('#7a4aa0', 10, 0),
    'soil':     ('#4a3a2a', 7, 0), 'water':   ('#2f5d77', 0, 0), 'water_lt': ('#4a86a3', 0, 0),
    'glass':    ('#ffb766', 0, 1.6), 'glass_dim': ('#e09a52', 0, 0.9), 'fire': ('#ff8a2a', 0, 4.0), 'ember': ('#ff5a1a', 0, 3.0),
    'rose':     ('#d8783a', 0, 1.8), 'rune':    ('#ffb04a', 0, 3.0),
}


def _hex(h):
    h = h.lstrip('#')
    return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]


def mat_vcol(name):
    hx, mid, emit = MATS[name]
    a = 0.5 * (1 - min(1.0, emit / 4.0)) if emit > 0 else 1.0
    return (*_hex(hx), a)


def get_material(name):
    m = bpy.data.materials.get('K_' + name)
    if m is None:
        m = bpy.data.materials.new('K_' + name)
        m.use_nodes = True
        bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        c = _hex(MATS[name][0])
        lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
        bsdf.inputs['Base Color'].default_value = (*lin, 1)
        m['kezor_hex'] = MATS[name][0]
    return m


# ---------------------------------------------------------------------------------------------------- construtor
class Kit:
    """Malha em construção (bmesh) com pilha de transformações, material e tom por face."""

    def __init__(self, name, seed=1):
        self.name = name
        self.bm = bmesh.new()
        self.lvar = self.bm.faces.layers.float.new('var')
        self.mats = []
        self.M = Matrix.Identity(4)
        self.stack = []
        self.rnd = random.Random(seed)
        self.children = []   # (nome, Kit, posição)

    # -- transformações
    def push(self, loc=(0, 0, 0), rz=0.0, rx=0.0, ry=0.0, s=1.0):
        self.stack.append(self.M.copy())
        S = Matrix.Diagonal((s, s, s, 1)) if not isinstance(s, (tuple, list)) else Matrix.Diagonal((*s, 1))
        self.M = self.M @ Matrix.Translation(loc) @ Matrix.Rotation(math.radians(rz), 4, 'Z') @ \
            Matrix.Rotation(math.radians(ry), 4, 'Y') @ Matrix.Rotation(math.radians(rx), 4, 'X') @ S
        return self

    def pop(self):
        self.M = self.stack.pop()

    def mi(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def _paint(self, faces, m, var):
        i = self.mi(m)
        if var is None:
            var = 1.0 + self.rnd.uniform(-0.035, 0.035)
        for f in faces:
            f.material_index = i
            f[self.lvar] = var

    def tone(self, spread=0.08):
        return 1.0 + self.rnd.uniform(-spread, spread)

    # -- primitivas
    def box(self, c, size, m, bev=0.02, var=None, rz=0.0, rx=0.0, ry=0.0, top=(1.0, 1.0), shift=(0.0, 0.0)):
        """Caixa chanfrada. c = centro, size = (x, y, z). top encolhe o topo (talude/pirâmide), shift desloca o topo."""
        sx, sy, sz = size
        if sx <= 0.001 or sy <= 0.001 or sz <= 0.001:
            return
        M = self.M @ Matrix.Translation(c) @ Matrix.Rotation(math.radians(rz), 4, 'Z') @ \
            Matrix.Rotation(math.radians(ry), 4, 'Y') @ Matrix.Rotation(math.radians(rx), 4, 'X')
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        vs = res['verts']
        for v in vs:
            x, y, z = v.co
            if z > 0:
                x = x * top[0] + shift[0] / sx
                y = y * top[1] + shift[1] / sy
            v.co = M @ Vector((x * sx, y * sy, z * sz))
        faces = list({f for v in vs for f in v.link_faces})
        self._paint(faces, m, var)
        tv = faces[0][self.lvar]
        b = min(bev, sx * 0.3, sy * 0.3, sz * 0.3) if min(sx, sy, sz) >= 0.07 else 0.0
        if b > 0.002:
            edges = list({e for v in vs for e in v.link_edges})
            r = bmesh.ops.bevel(self.bm, geom=vs + edges, offset=b, segments=1, affect='EDGES', profile=0.5, clamp_overlap=True)
            self._paint(r['faces'], m, tv)

    def block(self, c, size, m, bev=0.035, var=None):
        """Bloco de cantaria leve: face da frente (-Y) chanfrada e lados, sem face de trás (fica no rejunte). 18 tri."""
        sx, sy, sz = size
        if sx <= 0.01 or sz <= 0.01:
            return
        b = min(bev, sx * 0.25, sz * 0.25)
        cx, cy, cz = c
        yf, yb = cy - sy / 2, cy + sy / 2
        M = self.M
        def V(x, y, z):
            return self.bm.verts.new(M @ Vector((x, y, z)))
        x0, x1, z0, z1 = cx - sx / 2, cx + sx / 2, cz - sz / 2, cz + sz / 2
        f = [V(x0 + b, yf, z0 + b), V(x1 - b, yf, z0 + b), V(x1 - b, yf, z1 - b), V(x0 + b, yf, z1 - b)]
        mm = [V(x0, yf + b, z0), V(x1, yf + b, z0), V(x1, yf + b, z1), V(x0, yf + b, z1)]
        bk = [V(x0, yb, z0), V(x1, yb, z0), V(x1, yb, z1), V(x0, yb, z1)]
        fs = [self.bm.faces.new((f[0], f[1], f[2], f[3]))]
        for i in range(4):
            j = (i + 1) % 4
            fs.append(self.bm.faces.new((mm[i], mm[j], f[j], f[i])))
            fs.append(self.bm.faces.new((bk[i], bk[j], mm[j], mm[i])))
        self._paint(fs, m, var)

    def seg(self, p0, p1, w, d, m, bev=0.015, var=None, roll=0.0):
        """Viga de seção w × d entre dois pontos (locais)."""
        p0, p1 = Vector(p0), Vector(p1)
        v = p1 - p0
        L = v.length
        if L < 1e-4:
            return
        q = v.normalized().to_track_quat('Z', 'Y')
        R = q.to_matrix().to_4x4() @ Matrix.Rotation(math.radians(roll), 4, 'Z')
        self.stack.append(self.M.copy())
        self.M = self.M @ Matrix.Translation((p0 + p1) / 2) @ R
        self.box((0, 0, 0), (w, d, L), m, bev=bev, var=var)
        self.pop()

    def cyl(self, c, r, h, m, n=12, r2=None, var=None, rx=0.0, ry=0.0, caps=True):
        """Cilindro (ou tronco de cone) com base em c (eixo Z local)."""
        M = self.M @ Matrix.Translation(c) @ Matrix.Rotation(math.radians(ry), 4, 'Y') @ \
            Matrix.Rotation(math.radians(rx), 4, 'X') @ Matrix.Translation((0, 0, h / 2))
        res = bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=n, radius1=r,
                                    radius2=r if r2 is None else r2, depth=h, matrix=M)
        faces = list({f for v in res['verts'] for f in v.link_faces})
        self._paint(faces, m, var)
        return faces

    def lathe(self, c, prof, m, n=16, var=None, cap_top=True, cap_bot=False, ring_var=0.0):
        """Torneado: prof = [(raio, z), ...] de baixo para cima. ring_var > 0 dá um tom diferente por anel."""
        M = self.M @ Matrix.Translation(c)
        rings = []
        for (r, z) in prof:
            ring = []
            for i in range(n):
                a = 2 * math.pi * i / n
                ring.append(self.bm.verts.new(M @ Vector((math.cos(a) * r, math.sin(a) * r, z))))
            rings.append(ring)
        base = var if var is not None else self.tone(0.03)
        for k in range(len(rings) - 1):
            fv = []
            for i in range(n):
                a, b = rings[k][i], rings[k][(i + 1) % n]
                cc, d = rings[k + 1][(i + 1) % n], rings[k + 1][i]
                try:
                    fv.append(self.bm.faces.new((a, b, cc, d)))
                except ValueError:
                    pass
            self._paint(fv, m, base * (1 + self.rnd.uniform(-ring_var, ring_var)) if ring_var else base)
        if cap_top and prof[-1][0] > 1e-4:
            self._paint([self.bm.faces.new(rings[-1])], m, base)
        if cap_bot and prof[0][0] > 1e-4:
            self._paint([self.bm.faces.new(list(reversed(rings[0])))], m, base)

    def poly(self, pts, m, var=None):
        """Face plana a partir de pontos (locais)."""
        vs = [self.bm.verts.new(self.M @ Vector(p)) for p in pts]
        f = self.bm.faces.new(vs)
        self._paint([f], m, var)
        return f

    def prism(self, pts2d, z0, z1, m, var=None, axis='Y', at=0.0):
        """Polígono 2D (no plano XZ, extrudado em Y de at-z0..at+... ) — usado em empenas. pts2d = [(x, z)], espessura y0..y1."""
        y0, y1 = z0, z1
        front = [self.bm.verts.new(self.M @ Vector((x, y0, z))) for (x, z) in pts2d]
        back = [self.bm.verts.new(self.M @ Vector((x, y1, z))) for (x, z) in pts2d]
        fs = [self.bm.faces.new(front), self.bm.faces.new(list(reversed(back)))]
        n = len(pts2d)
        for i in range(n):
            j = (i + 1) % n
            fs.append(self.bm.faces.new((front[j], front[i], back[i], back[j])))
        self._paint(fs, m, var)
        return fs

    def child(self, name, kit, loc):
        self.children.append((name, kit, Vector(loc)))

    # -- finalização
    def to_object(self, coll):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(get_material(m))
        ob = bpy.data.objects.new(self.name, me)
        coll.objects.link(ob)
        ob['kmats'] = list(self.mats)
        return ob


# ---------------------------------------------------------------------------------------------------- peças compostas
def stone_face(k, x0, x1, z0, z1, m='stone', mortar='mortar', depth=0.32, ch=(0.42, 0.62), bw=(0.7, 1.35),
               gap=0.035, openings=(), protrude=0.025, spread=0.09, alt=None, bev=0.035, back=True):
    """Parede de cantaria no plano y = 0 (frente para -Y, entra em +Y): fiadas de blocos desencontrados com rejunte.
    openings = [(x0, x1, z0, z1)] ficam vazias (janelas, portas, seteiras). alt = material alternado (blocos mais escuros)."""
    rnd = k.rnd
    if back:
        k.box(((x0 + x1) / 2, depth / 2 + 0.03, (z0 + z1) / 2), (x1 - x0, depth, z1 - z0), mortar, bev=0, var=1.0)
    z = z0
    row = 0
    while z < z1 - 0.05:
        h = rnd.uniform(*ch)
        if z1 - (z + h) < ch[0] * 0.6:
            h = z1 - z
        zc0, zc1 = z, z + h
        # intervalos livres desta fiada
        cuts = sorted([(o[0], o[1]) for o in openings if o[2] < zc1 - 0.01 and o[3] > zc0 + 0.01])
        free, cur = [], x0
        for (a, b) in cuts:
            if a > cur:
                free.append((cur, min(a, x1)))
            cur = max(cur, b)
        if cur < x1:
            free.append((cur, x1))
        for (a, b) in free:
            x = a
            first = True
            while x < b - 0.04:
                w = rnd.uniform(*bw)
                if first and row % 2 == 1:
                    w *= 0.55
                first = False
                if b - (x + w) < bw[0] * 0.45:
                    w = b - x
                mat = alt if (alt and rnd.random() < 0.18) else m
                pr = rnd.uniform(-protrude, protrude)
                k.block((x + w / 2, -pr / 2 + 0.005, zc0 + h / 2), (w - gap, 0.06 + pr + 0.05, h - gap), mat,
                        bev=bev, var=k.tone(spread))
                x += w
        z += h
        row += 1


def stone_course_ring(k, cx, cy, r, z0, z1, m='stone', n=None, ch=(0.45, 0.6), gap=0.04, thick=0.12, spread=0.08, mortar='mortar', alt=None):
    """Torre redonda: fiadas de blocos curvos (caixas giradas em volta do eixo) sobre um cilindro de rejunte."""
    rnd = k.rnd
    segs = max(16, int(2 * math.pi * r / 0.25))
    k.cyl((cx, cy, z0), r - 0.02, z1 - z0, mortar, n=segs, caps=False, var=1.0)
    z = z0
    row = 0
    while z < z1 - 0.05:
        h = rnd.uniform(*ch)
        if z1 - (z + h) < ch[0] * 0.6:
            h = z1 - z
        cnt = n or max(8, int(2 * math.pi * r / rnd.uniform(0.9, 1.2)))
        off = (row % 2) * 0.5
        for i in range(cnt):
            a = (i + off) / cnt * 360
            w = 2 * math.pi * r / cnt
            mat = alt if (alt and rnd.random() < 0.18) else m
            k.push((cx, cy, 0), rz=a + 90)
            k.block((0, -(r + thick / 2 - 0.03), z + h / 2), (w * 1.04 - gap, thick, h - gap), mat, bev=0.03, var=k.tone(spread))
            k.pop()
        z += h
        row += 1


def cone_roof(k, cx, cy, zb, rr, height, rows=16, n=40, mats=('slate', 'slate2', 'slate3'), curve=1.08):
    """Telhado cônico em fiadas de lousa: cada fiada é um anel com a borda de baixo recortada (lousas alternadas)
    e um tom próprio; a de cima cobre a de baixo."""
    M = k.M @ Matrix.Translation((cx, cy, 0))
    for i in range(rows):
        f0, f1 = i / rows, (i + 1.35) / rows
        r0 = rr * (1 - f0) ** curve + 0.04
        r1 = max(rr * (1 - min(f1, 1)) ** curve, 0.0) + 0.04
        z0, z1 = zb + height * f0, zb + height * min(f1, 1.0)
        top, bot, lip = [], [], []
        for j in range(n):
            a = 2 * math.pi * (j + (i % 2) * 0.5) / n
            ca, sa = math.cos(a), math.sin(a)
            dz = -0.07 if j % 2 == 0 else 0.0
            lip.append(k.bm.verts.new(M @ Vector((ca * (r0 - 0.06), sa * (r0 - 0.06), z0 + dz - 0.04))))
            bot.append(k.bm.verts.new(M @ Vector((ca * (r0 + 0.02), sa * (r0 + 0.02), z0 + dz))))
            top.append(k.bm.verts.new(M @ Vector((ca * r1, sa * r1, z1))))
        fs = []
        for j in range(n):
            jn = (j + 1) % n
            fs.append(k.bm.faces.new((bot[j], bot[jn], top[jn], top[j])))
            fs.append(k.bm.faces.new((lip[j], lip[jn], bot[jn], bot[j])))
        k._paint(fs, mats[i % len(mats)] if k.rnd.random() < 0.5 else mats[0], k.tone(0.07))


def merlons(k, x0, x1, z, y, m='stone', mw=0.9, mh=1.0, gap=0.65, depth=0.55, slit=True):
    """Ameias ao longo de X (no plano y), com seteira em cada merlão."""
    n = max(1, int((x1 - x0 + gap) / (mw + gap)))
    tot = n * mw + (n - 1) * gap
    x = (x0 + x1) / 2 - tot / 2
    for i in range(n):
        k.box((x + mw / 2, y, z + mh / 2), (mw, depth, mh), m, bev=0.04, var=k.tone(0.07))
        k.box((x + mw / 2, y, z + mh + 0.06), (mw + 0.08, depth + 0.08, 0.12), m, bev=0.03, var=k.tone(0.05))
        x += mw + gap


def window(k, x, z, w=0.9, h=1.3, frame='timber', shutters=True, arch=False, glow='glass', sill='stone_lt', cross=True, depth=0.12, pane_y=0.02):
    """Janela no plano y = 0 (frente -Y): moldura, vidro aceso recuado, cruz de madeira, peitoril e venezianas abertas."""
    k.box((x, pane_y, z + h / 2), (w, 0.02, h), glow, bev=0.0)
    t = 0.09
    k.box((x - w / 2 - t / 2, -depth / 2, z + h / 2), (t, depth, h + t * 2), frame, bev=0.015)
    k.box((x + w / 2 + t / 2, -depth / 2, z + h / 2), (t, depth, h + t * 2), frame, bev=0.015)
    k.box((x, -depth / 2, z + h + t / 2), (w + 2 * t, depth, t), frame, bev=0.015)
    if arch:
        for i in range(7):
            a = math.pi * (i + 0.5) / 7
            k.push((x + math.cos(a) * (w / 2 + 0.06), -depth / 2, z + h + t + math.sin(a) * (w / 2) * 0.5), ry=-math.degrees(a) + 90)
            k.box((0, 0, 0), (0.1, depth, w * 0.24), frame, bev=0.01)
            k.pop()
    if cross:
        k.box((x, -0.02, z + h / 2), (0.05, 0.05, h), frame, bev=0.008)
        k.box((x, -0.02, z + h * 0.6), (w, 0.05, 0.05), frame, bev=0.008)
    k.box((x, -depth / 2 - 0.05, z - 0.05), (w + 0.35, depth + 0.12, 0.1), sill, bev=0.02)
    if shutters:
        for s in (-1, 1):
            k.push((x + s * (w / 2 + 0.06), -depth - 0.02, z + h / 2), rz=s * 62)
            k.box((s * w / 4, 0, 0), (w / 2, 0.04, h * 0.98), 'plank', bev=0.01, var=k.tone(0.06))
            for zz in (-h * 0.3, h * 0.3):
                k.box((s * w / 4, -0.025, zz), (w / 2 * 0.95, 0.02, 0.07), 'iron', bev=0.005)
            k.pop()


def door(k, x, w=1.3, h=2.3, frame='stone_lt', arch=True, planks='plank', lantern=True, z0=0.0):
    """Porta de tábuas com ferragens no plano y = 0, moldura de pedra em arco, degrau e lampião."""
    k.box((x, 0.03, z0 + h / 2), (w, 0.08, h), planks, bev=0.01, var=k.tone(0.05))
    nb = max(3, int(w / 0.22))
    for i in range(nb):
        xx = x - w / 2 + (i + 0.5) * w / nb
        k.box((xx, -0.02, z0 + h / 2), (w / nb - 0.02, 0.04, h - 0.04), planks, bev=0.006, var=k.tone(0.08))
    for zz in (0.35, h - 0.45):
        k.box((x, -0.05, z0 + zz), (w * 0.92, 0.025, 0.08), 'iron', bev=0.006)
    k.cyl((x + w * 0.32, -0.08, z0 + h * 0.48), 0.06, 0.03, 'iron', n=8, rx=90)
    ft = 0.22
    for s in (-1, 1):
        zz = z0
        while zz < z0 + h - 0.05:
            hh = min(0.45, z0 + h - zz)
            ww = ft + (0.08 if int((zz - z0) / 0.45) % 2 else 0)
            k.box((x + s * (w / 2 + ww / 2 - 0.02), -0.08, zz + hh / 2), (ww, 0.24, hh - 0.025), frame, bev=0.025, var=k.tone(0.07))
            zz += 0.45
    if arch:
        n = 7
        for i in range(n):
            a = math.pi * (i + 0.5) / n
            rr = w / 2 + ft / 2
            k.push((x + math.cos(a) * rr, -0.08, z0 + h + math.sin(a) * rr * 0.55), ry=90 - math.degrees(a))
            k.box((0, 0, 0), (ft + 0.05, 0.26, rr * math.pi / n * 0.92), frame, bev=0.025, var=k.tone(0.07))
            k.pop()
        k.box((x, 0.02, z0 + h + 0.25), (w, 0.1, 0.5), 'mortar', bev=0)
    else:
        k.box((x, -0.08, z0 + h + 0.12), (w + ft * 2 + 0.1, 0.26, 0.24), frame, bev=0.03)
    k.box((x, -0.35, z0 + 0.07), (w + 0.6, 0.55, 0.14), 'stone_dk', bev=0.03)
    if lantern:
        lx = x + w / 2 + 0.45
        k.seg((lx, 0.0, z0 + h + 0.15), (lx, -0.45, z0 + h + 0.15), 0.04, 0.04, 'iron')
        k.box((lx, -0.45, z0 + h - 0.12), (0.2, 0.2, 0.3), 'glass', bev=0.0)
        for sx in (-1, 1):
            for sy in (-1, 1):
                k.box((lx + sx * 0.1, -0.45 + sy * 0.1, z0 + h - 0.12), (0.03, 0.03, 0.34), 'iron', bev=0)
        k.box((lx, -0.45, z0 + h + 0.06), (0.26, 0.26, 0.06), 'iron', bev=0.01, top=(0.4, 0.4))


def tile_roof_side(k, x0, x1, y_eave, z_eave, y_ridge, z_ridge, m=('tile', 'tile2', 'tile3'), kind='clay', row=0.36, var_spread=0.07):
    """Uma água de telhado entre a beira (y_eave, z_eave) e a cumeeira (y_ridge, z_ridge), de x0 a x1.
    clay: fileiras de telhas onduladas (canal); slate: fiadas de lousas em placas desencontradas."""
    rnd = k.rnd
    dy, dz = y_ridge - y_eave, z_ridge - z_eave
    L = math.hypot(dy, dz)
    ang = math.degrees(math.atan2(dz, abs(dy)))
    sgn = 1 if dy > 0 else -1
    n = max(2, int(L / row))
    step = L / n
    # base (deck) para não ver através
    k.push(((x0 + x1) / 2, (y_eave + y_ridge) / 2, (z_eave + z_ridge) / 2 - 0.08), rz=0 if sgn > 0 else 180, rx=ang)
    k.box((0, 0, 0), (x1 - x0 - 0.02, L, 0.08), 'timber', bev=0, var=0.7)
    k.pop()
    for i in range(n):
        t0 = i * step
        yc = y_eave + dy * (t0 + step * 0.65) / L
        zc = z_eave + dz * (t0 + step * 0.65) / L
        mat = m[rnd.randrange(len(m))] if kind == 'slate' else m[i % len(m)] if rnd.random() < 0.3 else m[0]
        k.push(((x0 + x1) / 2, yc, zc), rz=0 if sgn > 0 else 180, rx=ang + 4.5)
        if kind == 'clay':
            _clay_row(k, x1 - x0, step * 1.35, mat)
        else:
            _slate_row(k, x1 - x0, step * 1.35, mat, i)
        k.pop()


def _slate_row(k, width, depth, m, row):
    """Fiada de lousas: tira única com a borda de baixo recortada (lousa sim, lousa não um pouco mais comprida)."""
    rnd = k.rnd
    xs = [-width / 2]
    x = -width / 2 + (0.17 if row % 2 else 0.0)
    while x < width / 2 - 0.15:
        x += rnd.uniform(0.26, 0.4)
        xs.append(min(x, width / 2))
    if xs[-1] < width / 2:
        xs.append(width / 2)
    top, bot, lip = [], [], []
    for i, xx in enumerate(xs):
        dl = 0.0 if i % 2 == 0 else -0.06
        top.append(k.bm.verts.new(k.M @ Vector((xx, depth / 2, 0.0))))
        bot.append(k.bm.verts.new(k.M @ Vector((xx, -depth / 2 + dl, 0.03))))
        lip.append(k.bm.verts.new(k.M @ Vector((xx, -depth / 2 + dl, -0.02))))
    fs = []
    for i in range(len(xs) - 1):
        fs.append(k.bm.faces.new((bot[i], bot[i + 1], top[i + 1], top[i])))
        fs.append(k.bm.faces.new((lip[i], lip[i + 1], bot[i + 1], bot[i])))
    k._paint(fs, m, k.tone(0.06))


def _clay_row(k, width, depth, m):
    """Fileira de telhas de canal: perfil ondulado em X, uma peça só (vértices compartilhados)."""
    pitch = 0.24
    nw = max(4, int(width / pitch))
    seg = 2
    pts = []
    for i in range(nw * seg + 1):
        x = -width / 2 + width * i / (nw * seg)
        ph = (i % seg) / seg
        z = 0.05 * math.sin(ph * math.pi) if (i // seg) % 2 == 0 else -0.015 * math.sin(ph * math.pi)
        pts.append((x, z))
    vf, vb = [], []
    for (x, z) in pts:
        vf.append(k.bm.verts.new(k.M @ Vector((x, -depth / 2, z + 0.02))))
        vb.append(k.bm.verts.new(k.M @ Vector((x, depth / 2, z))))
    lip = [k.bm.verts.new(k.M @ Vector((x, -depth / 2, z - 0.035))) for (x, z) in pts]
    fs = []
    for i in range(len(pts) - 1):
        fs.append(k.bm.faces.new((vf[i], vf[i + 1], vb[i + 1], vb[i])))
        fs.append(k.bm.faces.new((lip[i], lip[i + 1], vf[i + 1], vf[i])))
    k._paint(fs, m, k.tone(0.06))


def gable_roof(k, w, d, z_eave, pitch_h, over=0.45, gable_over=0.35, kind='clay', mats=None, ridge='tile2', end_m='plaster',
               timber_gable=True, gable_wall=True):
    """Telhado de duas águas (cumeeira em X) sobre um corpo w × d. Empenas fechadas de reboco com enxaimel."""
    mats = mats or (('tile', 'tile2', 'tile3') if kind == 'clay' else ('slate', 'slate2', 'slate3'))
    x0, x1 = -w / 2 - gable_over, w / 2 + gable_over
    zr = z_eave + pitch_h
    for s in (-1, 1):
        ye = s * (d / 2 + over)
        ze = z_eave - over * pitch_h / (d / 2)
        tile_roof_side(k, x0, x1, ye, ze, 0.0, zr + 0.05, mats, kind)
    # cumeeira
    if kind == 'clay':
        k.cyl((x0 - 0.05, 0, zr + 0.13), 0.13, x1 - x0 + 0.1, ridge, n=8, ry=90)
    else:
        k.box(((x0 + x1) / 2, 0, zr + 0.12), (x1 - x0 + 0.1, 0.32, 0.12), 'slate2', bev=0.03, rx=0)
    # empenas
    if gable_wall:
        for s in (-1, 1):
            xs = s * (w / 2 - 0.02)
            k.push((xs, 0, 0), rz=90 * s)
            k.prism([(-d / 2, z_eave), (d / 2, z_eave), (0, zr)], -0.12, 0.05, end_m)
            if timber_gable:
                k.box((0, -0.15, z_eave + 0.08), (d + 0.1, 0.14, 0.16), 'timber')
                k.seg((-d / 2, -0.15, z_eave + 0.1), (0, -0.15, zr - 0.05), 0.16, 0.14, 'timber')
                k.seg((d / 2, -0.15, z_eave + 0.1), (0, -0.15, zr - 0.05), 0.16, 0.14, 'timber')
                k.box((0, -0.15, (z_eave + zr) / 2), (0.16, 0.14, zr - z_eave - 0.1), 'timber')
                k.seg((-d / 4, -0.15, z_eave + 0.1), (0, -0.15, (z_eave + zr) / 2), 0.13, 0.12, 'timber')
                k.seg((d / 4, -0.15, z_eave + 0.1), (0, -0.15, (z_eave + zr) / 2), 0.13, 0.12, 'timber')
                # janelinha do sótão
                window(k, 0, z_eave + pitch_h * 0.32, w=0.5, h=0.55, shutters=False, cross=True, depth=0.1) if pitch_h > 2.2 else None
            # tábua de beiral (barge board)
            L = math.hypot(d / 2 + over, pitch_h + over * pitch_h / (d / 2))
            k.pop()
            for t in (-1, 1):
                k.seg((s * (w / 2 + gable_over + 0.02), t * (d / 2 + over), z_eave - over * pitch_h / (d / 2) + 0.05),
                      (s * (w / 2 + gable_over + 0.02), 0, zr + 0.1), 0.06, 0.24, 'timber')
    return zr


def timber_wall(k, x0, x1, z0, z1, m='plaster', posts=1.15, braces=True, rails=True, beam='timber', seed_off=0, window_slots=()):
    """Parede de enxaimel no plano y = 0 (frente -Y): painel de reboco com postes, travessas e mãos-francesas."""
    k.box(((x0 + x1) / 2, 0.06, (z0 + z1) / 2), (x1 - x0, 0.12, z1 - z0), m, bev=0, var=k.tone(0.04))
    t, dd = 0.17, 0.08
    k.box(((x0 + x1) / 2, -dd / 2 + 0.01, z0 + t / 2), (x1 - x0 + 0.04, dd + 0.02, t), beam, bev=0.015)
    k.box(((x0 + x1) / 2, -dd / 2 + 0.01, z1 - t / 2), (x1 - x0 + 0.04, dd + 0.02, t), beam, bev=0.015)
    n = max(1, round((x1 - x0) / posts))
    xs = [x0 + (x1 - x0) * i / n for i in range(n + 1)]
    for i, x in enumerate(xs):
        xx = min(max(x, x0 + t / 2), x1 - t / 2)
        k.box((xx, -dd / 2, (z0 + z1) / 2), (t, dd, z1 - z0 - 0.02), beam, bev=0.015, var=k.tone(0.06))
    zm = z0 + (z1 - z0) * 0.48
    for i in range(n):
        a, b = xs[i] + t / 2, xs[i + 1] - t / 2
        slot = any(sx0 < (a + b) / 2 < sx1 for (sx0, sx1) in window_slots)
        if rails and not slot:
            k.box(((a + b) / 2, -dd / 2 + 0.005, zm), (b - a, dd - 0.01, 0.13), beam, bev=0.012)
        if braces and not slot and (i + seed_off) % 2 == 0:
            k.seg((a, -dd / 2 + 0.008, z0 + t), (b, -dd / 2 + 0.008, zm), 0.12, dd - 0.016, beam, bev=0.01)
            k.seg((b, -dd / 2 + 0.008, zm), (a, -dd / 2 + 0.008, z1 - t), 0.12, dd - 0.016, beam, bev=0.01)
        elif braces and not slot:
            k.seg((a, -dd / 2 + 0.008, zm), (b, -dd / 2 + 0.008, z1 - t), 0.12, dd - 0.016, beam, bev=0.01)
            k.seg((b, -dd / 2 + 0.008, z0 + t), (a, -dd / 2 + 0.008, zm), 0.12, dd - 0.016, beam, bev=0.01)
    return xs


def chimney(k, x, y, z0, z1, m='stone2'):
    """Chaminé de pedra em fiadas, com coroa e capelo."""
    z = z0
    i = 0
    while z < z1:
        h = 0.32
        k.box((x, y, z + h / 2), (0.75 + (0.04 if i % 2 else 0), 0.75 + (0 if i % 2 else 0.04), h - 0.02), m, bev=0.025, var=k.tone(0.08))
        z += h
        i += 1
    k.box((x, y, z + 0.08), (0.95, 0.95, 0.16), 'stone_dk', bev=0.03)
    k.box((x, y, z + 0.22), (0.5, 0.5, 0.12), 'basalt', bev=0.02)
    k.box((x, y, z + 0.4), (0.7, 0.7, 0.06), 'stone_dk', bev=0.02)
    for sx in (-1, 1):
        k.box((x + sx * 0.2, y, z + 0.3), (0.08, 0.5, 0.16), 'stone_dk', bev=0.01)
    return z + 0.45


def lantern_post(k, x, y, h=3.6, arm=0.8, m='iron'):
    """Poste de ferro com braço e lampião de vidro aceso."""
    k.box((x, y, 0.15), (0.5, 0.5, 0.3), 'stone_dk', bev=0.04)
    k.box((x, y, 0.36), (0.36, 0.36, 0.12), 'stone', bev=0.03)
    k.cyl((x, y, 0.42), 0.075, h - 0.42, m, n=8, r2=0.055)
    for zz in (0.9, h - 0.4):
        k.cyl((x, y, zz), 0.1, 0.07, m, n=8)
    k.seg((x, y, h - 0.1), (x, y - arm, h - 0.1), 0.05, 0.05, m)
    k.seg((x, y - 0.05, h - 0.55), (x, y - arm * 0.6, h - 0.12), 0.035, 0.035, m)
    ly = y - arm
    k.box((x, ly, h - 0.5), (0.3, 0.3, 0.42), 'glass', bev=0)
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.box((x + sx * 0.15, ly + sy * 0.15, h - 0.5), (0.04, 0.04, 0.46), m, bev=0)
    k.box((x, ly, h - 0.25), (0.4, 0.4, 0.08), m, bev=0.01, top=(0.3, 0.3))
    k.box((x, ly, h - 0.73), (0.34, 0.34, 0.05), m, bev=0.01)
    k.cyl((x, ly, h - 0.16), 0.03, 0.12, m, n=6)


def banner(k, x, y, z_top, w=0.9, h=2.4, cloth='cloth_r', emblem='gold', rod='iron', rz=0.0):
    """Estandarte pendurado (pano com ponta em V, barra de ferro, emblema de brasa)."""
    k.push((x, y, z_top), rz=rz)
    k.cyl((-w / 2 - 0.1, 0, 0), 0.03, w + 0.2, rod, n=6, ry=90)
    for s in (-1, 1):
        k.box((s * (w / 2 + 0.12), 0, 0), (0.06, 0.06, 0.06), 'gold', bev=0.01)
    pts = [(-w / 2, -0.02), (w / 2, -0.02), (w / 2, -h), (0, -h - 0.35), (-w / 2, -h)]
    k.prism([(x2, z2) for (x2, z2) in pts], -0.02, 0.02, cloth)
    # barra e emblema (losango de brasa com chama)
    k.prism([(-w / 2, -0.12), (w / 2, -0.12), (w / 2, -0.24), (-w / 2, -0.24)], -0.035, -0.02, emblem)
    k.prism([(-w / 2, -h + 0.12), (w / 2, -h + 0.12), (w / 2, -h + 0.02), (0, -h - 0.25), (-w / 2, -h + 0.02)], -0.035, -0.02, emblem)
    zc = -h * 0.5
    k.prism([(0, zc + 0.42), (0.3, zc), (0, zc - 0.42), (-0.3, zc)], -0.04, -0.02, emblem)
    k.prism([(0, zc + 0.26), (0.13, zc - 0.02), (0, zc - 0.22), (-0.13, zc - 0.02)], -0.05, -0.04, cloth)
    k.pop()


def brazier(k, x, y, z=0.0, s=1.0, base=True):
    """Braseiro de ferro sobre pedestal de pedra com fogo (emissivo)."""
    k.push((x, y, z), s=s)
    if base:
        k.box((0, 0, 0.2), (0.9, 0.9, 0.4), 'stone_dk', bev=0.05)
        k.box((0, 0, 0.6), (0.62, 0.62, 0.4), 'stone', bev=0.04, top=(0.85, 0.85))
        k.box((0, 0, 0.84), (0.8, 0.8, 0.1), 'stone_lt', bev=0.03)
        zb = 0.9
    else:
        zb = 0.0
    k.lathe((0, 0, zb), [(0.12, 0), (0.18, 0.12), (0.5, 0.42), (0.58, 0.52), (0.55, 0.56)], 'iron', n=12)
    for i in range(6):
        a = i * 60
        k.push((0, 0, zb), rz=a)
        k.box((0.55, 0, 0.5), (0.06, 0.06, 0.24), 'iron', bev=0.01, ry=-15)
        k.pop()
    k.lathe((0, 0, zb + 0.44), [(0.46, 0), (0.3, 0.25), (0.1, 0.55), (0.0, 0.7)], 'fire', n=10)
    k.lathe((0, 0, zb + 0.44), [(0.48, 0), (0.48, 0.06)], 'ember', n=10)
    k.pop()


# ---------------------------------------------------------------------------------------------------- muralha
WALL_H = 7.2      # topo do adarve
WALL_T = 2.6      # espessura
SEG_LEN = 8.0     # comprimento de um segmento


def muralha4(name='Muralha4', seed=11):
    """Segmento reto da muralha (8 m em X, lado de fora = -Y): base em talude de basalto, cantaria, cordão,
    adarve com ameias e seteiras do lado de fora e parapeito baixo do lado de dentro, contrafortes por dentro."""
    k = Kit(name, seed)
    L, T, H = SEG_LEN, WALL_T, WALL_H
    x0, x1 = -L / 2, L / 2
    # núcleo
    k.box((0, 0, H / 2), (L, T - 0.5, H), 'mortar', bev=0, var=0.9)
    # talude externo (base inclinada) em blocos de basalto
    k.push((0, -T / 2, 0))
    k.push((0, 0, 0), rx=-8)
    stone_face(k, x0, x1, 0.0, 1.6, m='basalt', alt='stone_dk', ch=(0.4, 0.55), bw=(0.9, 1.6), depth=0.4)
    k.pop()
    # face externa
    stone_face(k, x0, x1, 1.55, H - 0.35, m='stone', alt='stone2', ch=(0.45, 0.62), bw=(0.8, 1.5),
               openings=[(-2.05, -1.95, 3.6, 4.6), (1.95, 2.05, 3.6, 4.6)])
    # seteiras (fundo escuro e moldura)
    for sx in (-2.0, 2.0):
        k.box((sx, 0.1, 4.1), (0.12, 0.2, 1.0), 'basalt', bev=0)
        k.box((sx, -0.02, 4.65), (0.5, 0.2, 0.14), 'stone_lt', bev=0.02)
        k.box((sx, -0.02, 3.55), (0.5, 0.2, 0.14), 'stone_lt', bev=0.02)
    # cordão (moldura saliente) e mata-cães estilizados
    k.box((0, -0.12, H - 0.25), (L + 0.02, 0.36, 0.22), 'stone_lt', bev=0.04)
    for i in range(8):
        xx = x0 + 0.5 + i
        k.box((xx, -0.25, H - 0.5), (0.3, 0.4, 0.32), 'stone2', bev=0.03, top=(1, 1.6), shift=(0, -0.12))
    k.pop()
    # face interna (blocos maiores, menos detalhe) e contraforte central
    k.push((0, T / 2, 0), rz=180)
    stone_face(k, x0, x1, 0.0, H - 0.3, m='stone2', alt='stone_dk', ch=(0.55, 0.75), bw=(1.0, 1.8), spread=0.07)
    k.pop()
    for bx in (0.0,):
        k.push((bx, T / 2 + 0.55, 0))
        k.box((0, 0, 2.6), (1.3, 1.1, 5.2), 'stone2', bev=0.06, top=(0.85, 0.55), shift=(0, -0.25))
        for z in (1.0, 2.2, 3.4):
            k.box((0, 0.0, z), (1.36, 1.16 - z * 0.08, 0.06), 'mortar', bev=0)
        k.pop()
    # adarve (lajes) e ameias
    zt = H
    k.box((0, 0, zt - 0.1), (L, T, 0.2), 'stone_dk', bev=0.03)
    x = x0
    while x < x1 - 0.05:
        w = min(k.rnd.uniform(0.7, 1.1), x1 - x)
        k.box((x + w / 2, 0.1, zt + 0.03), (w - 0.04, T - 1.2, 0.08), 'slab', bev=0.02, var=k.tone(0.09))
        x += w
    merlons(k, x0 + 0.1, x1 - 0.1, zt, -T / 2 + 0.3, m='stone', mw=1.0, mh=1.25, gap=0.62, depth=0.62)
    k.box((0, -T / 2 + 0.3, zt + 0.12), (L, 0.62, 0.24), 'stone', bev=0.03)
    # parapeito interno baixo
    k.box((0, T / 2 - 0.22, zt + 0.35), (L, 0.44, 0.7), 'stone2', bev=0.04)
    k.box((0, T / 2 - 0.22, zt + 0.74), (L + 0.02, 0.52, 0.1), 'stone_lt', bev=0.03)
    return k


def muralha4_hera(name='Muralha4Hera', seed=12):
    """Mesmo segmento com hera subindo pela face externa e musgo no talude."""
    k = muralha4(name, seed)
    rnd = k.rnd
    T = WALL_T
    for v in range(5):
        x = rnd.uniform(-3.6, 3.6)
        z = 0.2
        top = rnd.uniform(3.0, 6.3)
        while z < top:
            nx = x + rnd.uniform(-0.25, 0.25)
            nz = z + rnd.uniform(0.25, 0.45)
            k.seg((x, -T / 2 - 0.08, z), (nx, -T / 2 - 0.08, nz), 0.05, 0.04, 'wood')
            for _ in range(3):
                lx, lz = nx + rnd.uniform(-0.35, 0.35), nz + rnd.uniform(-0.25, 0.25)
                k.box((lx, -T / 2 - 0.12, lz), (0.22, 0.05, 0.18), rnd.choice(['leaf', 'leaf2']), bev=0.01, rz=rnd.uniform(-20, 20), rx=rnd.uniform(-15, 15))
            x, z = nx, nz
    for i in range(14):
        k.box((rnd.uniform(-3.9, 3.9), -T / 2 - 0.2, rnd.uniform(0.05, 0.6)), (rnd.uniform(0.4, 0.9), 0.1, 0.25), 'leaf', bev=0.02, rx=-8)
    return k


def torre4(name='Torre4', seed=21, r=3.6, h=11.0, roof=True):
    """Torre redonda de canto: base em talude, fiadas de cantaria, seteiras, cordão, ameias e telhado cônico de lousa
    com coruchéu e bandeira. Porta voltada para dentro (+Y)."""
    k = Kit(name, seed)
    rnd = k.rnd
    # talude
    k.lathe((0, 0, 0), [(r + 0.8, 0), (r + 0.8, 0.25), (r + 0.75, 0.3)], 'stone_dk', n=32)
    for i in range(4):
        z0, z1 = 0.3 + i * 0.4, 0.3 + (i + 1) * 0.4
        f0, f1 = i / 4, (i + 1) / 4
        stone_course_ring(k, 0, 0, r + 0.6 - 0.6 * f0, z0, z1, m='stone_dk', alt='basalt', ch=(0.4, 0.41), thick=0.16)
    stone_course_ring(k, 0, 0, r - 0.1, 1.9, h - 0.6, m='stone', alt='stone2')
    # seteiras em espiral
    for i in range(6):
        a = 25 + i * 62
        z = 3.2 + (i % 3) * 2.2
        k.push((0, 0, 0), rz=a)
        k.box((r + 0.02, 0, z), (0.3, 0.16, 1.0), 'basalt', bev=0)
        k.box((r + 0.08, 0, z + 0.58), (0.25, 0.45, 0.14), 'stone_lt', bev=0.02)
        k.pop()
    # porta para dentro (+Y)
    k.push((0, r - 0.05, 0), rz=180)
    door(k, 0, w=1.2, h=2.2, lantern=False)
    k.pop()
    # cordão + mísulas + ameias
    k.lathe((0, 0, h - 0.6), [(r + 0.05, 0), (r + 0.35, 0.15), (r + 0.35, 0.35), (r + 0.05, 0.45)], 'stone_lt', n=32)
    for i in range(16):
        k.push((0, 0, 0), rz=i * 22.5)
        k.box((r + 0.3, 0, h - 0.95), (0.4, 0.3, 0.5), 'stone2', bev=0.03, top=(1.6, 1))
        k.pop()
    k.lathe((0, 0, h - 0.2), [(r + 0.5, 0), (r + 0.5, 0.2)], 'stone_dk', n=32)
    nm = 12
    for i in range(nm):
        k.push((0, 0, 0), rz=i * 360 / nm)
        k.box((r + 0.25, 0, h + 0.65), (0.55, 1.0, 1.3), 'stone', bev=0.04, var=k.tone(0.07))
        k.box((r + 0.25, 0, h + 1.35), (0.62, 1.08, 0.12), 'stone_lt', bev=0.03)
        k.pop()
    k.lathe((0, 0, h), [(r + 0.5, 0), (r + 0.5, 0.25), (r - 0.05, 0.25)], 'stone', n=32)
    if roof:
        # telhado cônico em fiadas de lousa (anéis em degrau) com beiral
        zb = h + 1.2
        rr = r + 0.55
        top = 6.8
        k.lathe((0, 0, 0), [(r - 0.2, zb - 0.35), (rr - 0.1, zb - 0.12), (rr - 0.1, zb - 0.02)], 'timber', n=32)
        cone_roof(k, 0, 0, zb, rr, top, rows=16, n=40)
        k.cyl((0, 0, zb + top - 0.2), 0.08, 2.0, 'iron', n=8)
        k.lathe((0, 0, zb + top), [(0.18, 0), (0.22, 0.12), (0.0, 0.5)], 'gold', n=10)
        # bandeira
        k.box((0.55, 0, zb + top + 1.45), (0.9, 0.03, 0.55), 'cloth_r', bev=0)
        k.box((0.55, -0.02, zb + top + 1.45), (0.25, 0.03, 0.25), 'gold', bev=0, ry=45)
    return k


def portao4(name='Portao4', seed=31):
    """Portão fortificado (atravessa a muralha, vão de 6 m em X): duas torres quadradas com telhado de pirâmide,
    arco de aduelas, grade levadiça erguida, folhas de madeira abertas para dentro, passadiço com ameias,
    brasão do Kezor e estandartes. Lado de fora = -Y."""
    k = Kit(name, seed)
    gw, T = 6.4, WALL_T + 1.4          # vão e profundidade
    tw, td, th = 5.4, 6.4, 12.5        # torres
    xo = gw / 2 + tw / 2
    for s in (-1, 1):
        cx = s * xo
        k.box((cx, 0, th / 2), (tw - 0.5, td - 0.5, th), 'mortar', bev=0)
        # talude da torre
        for (fy, rot) in ((-td / 2, 0), (td / 2, 180)):
            k.push((cx, fy, 0), rz=rot)
            k.push((0, 0, 0), rx=-7)
            stone_face(k, -tw / 2, tw / 2, 0, 1.7, m='basalt', alt='stone_dk', bw=(0.8, 1.4))
            k.pop()
            stone_face(k, -tw / 2, tw / 2, 1.65, th - 0.5, m='stone', alt='stone2',
                       openings=[(-0.1, 0.1, 4.4, 5.4), (-0.55, 0.55, 8.0, 9.4)])
            k.box((0, 0.12, 4.9), (0.16, 0.2, 1.0), 'basalt', bev=0)
            window(k, 0, 8.0, w=0.9, h=1.4, frame='stone_lt', shutters=False, arch=True, depth=0.2)
            k.pop()
        for (fx, rot) in ((cx + s * tw / 2, 90 * s),):
            k.push((fx, 0, 0), rz=rot)
            k.push((0, 0, 0), rx=-7)
            stone_face(k, -td / 2, td / 2, 0, 1.7, m='basalt', alt='stone_dk', bw=(0.8, 1.4))
            k.pop()
            stone_face(k, -td / 2, td / 2, 1.65, th - 0.5, m='stone', alt='stone2', openings=[(-0.1, 0.1, 6.2, 7.2)])
            k.box((0, 0.12, 6.7), (0.16, 0.2, 1.0), 'basalt', bev=0)
            k.pop()
        # face do lado do vão (dentro da passagem)
        k.push((cx - s * tw / 2, 0, 0), rz=-90 * s)
        stone_face(k, -td / 2, td / 2, 0, th - 0.5, m='stone2', alt='stone_dk', bw=(0.9, 1.6))
        k.pop()
        # cordão, mísulas, ameias e telhado piramidal de lousa
        k.box((cx, 0, th - 0.3), (tw + 0.5, td + 0.5, 0.3), 'stone_lt', bev=0.04)
        for (dx, dy, ww, dd) in ((0, -td / 2 - 0.1, tw + 0.6, 0.6), (0, td / 2 + 0.1, tw + 0.6, 0.6), (-tw / 2 - 0.1, 0, 0.6, td + 0.6), (tw / 2 + 0.1, 0, 0.6, td + 0.6)):
            k.box((cx + dx, dy, th + 0.1), (ww, dd, 0.5), 'stone', bev=0.03)
        for (fy, rot, ln) in ((-td / 2 - 0.1, 0, tw), (td / 2 + 0.1, 180, tw), (cx + s * (tw / 2 + 0.1), None, td)):
            if rot is None:
                k.push((fy, 0, 0), rz=-90 * s)
            else:
                k.push((cx, fy, 0), rz=rot)
            merlons(k, -ln / 2 + 0.1, ln / 2 - 0.1, th + 0.35, 0, m='stone', mw=0.9, mh=1.1, gap=0.6, depth=0.55)
            k.pop()
        # pirâmide em fiadas (anéis quadrados em degrau)
        zb = th + 0.6
        rows = 12
        H = 6.0
        hw0, hd0 = tw / 2 + 0.2, td / 2 + 0.2
        for i in range(rows):
            f0 = i / rows
            f1 = (i + 1) / rows
            a0, b0 = hw0 * (1 - f0), hd0 * (1 - f0)
            a1, b1 = hw0 * (1 - f1), hd0 * (1 - f1)
            z0, z1 = zb + H * f0, zb + H * f1
            k.box((cx, 0, (z0 + z1) / 2 + 0.03), (2 * a0, 2 * b0, z1 - z0 + 0.06), k.rnd.choice(['slate', 'slate2', 'slate3']), bev=0.03,
                  top=(a1 / a0 if a0 > 0 else 0, b1 / b0 if b0 > 0 else 0))
        k.cyl((cx, 0, zb + H - 0.1), 0.07, 1.6, 'iron', n=8)
        k.lathe((cx, 0, zb + H + 0.2), [(0.16, 0), (0.2, 0.1), (0.0, 0.42)], 'gold', n=10)
        # estandarte na face de fora e na de dentro
        banner(k, cx, -td / 2 - 0.25, th - 1.1, w=1.5, h=3.6)
        banner(k, cx, td / 2 + 0.25, th - 1.1, w=1.5, h=3.6, rz=180)
        # braseiros nas quinas de fora
        brazier(k, cx + s * 1.4, -td / 2 - 1.6, s=0.9)
    # arco do vão (aduelas) dos dois lados e passadiço
    za = 5.6
    for (fy, rot) in ((-T / 2 - 0.1, 0), (T / 2 + 0.1, 180)):
        k.push((0, fy, 0), rz=rot)
        n = 13
        rr = gw / 2
        for i in range(n):
            a = math.pi * (i + 0.5) / n
            k.push((math.cos(a) * (rr + 0.35), 0, za + math.sin(a) * (rr + 0.35)), ry=90 - math.degrees(a))
            k.box((0, 0, 0), (0.75, 0.5, rr * math.pi / n * 0.94), 'stone_lt' if i != n // 2 else 'stone', bev=0.04, var=k.tone(0.05))
            k.pop()
        # tímpano/paramento acima do arco
        stone_face(k, -gw / 2 - 0.02, gw / 2 + 0.02, za + rr + 0.6, th - 2.4, m='stone', alt='stone2', bw=(0.7, 1.2))
        pts = []
        for i in range(17):
            a = math.pi * i / 16
            pts.append((math.cos(a) * (rr + 0.7), za + math.sin(a) * (rr + 0.7)))
        pts = [(gw / 2 + 0.02, za)] + pts[1:-1] + [(-gw / 2 - 0.02, za), (-gw / 2 - 0.02, za + rr + 0.7), (gw / 2 + 0.02, za + rr + 0.7)]
        k.prism([(-gw / 2 - 0.02, za), (-rr - 0.7 + 0.01, za), (-gw / 2 - 0.02, za + rr + 0.7)], -0.02, 0.4, 'stone2')
        k.prism([(gw / 2 + 0.02, za), (gw / 2 + 0.02, za + rr + 0.7), (rr + 0.7 - 0.01, za)], -0.02, 0.4, 'stone2')
        for i in range(8):
            a0 = math.pi * i / 8
            a1 = math.pi * (i + 1) / 8
            xa, za0 = math.cos(a0) * (rr + 0.7), za + math.sin(a0) * (rr + 0.7)
            xb, zb0 = math.cos(a1) * (rr + 0.7), za + math.sin(a1) * (rr + 0.7)
            xc = (xa + xb) / 2
            k.prism([(xa, za0), (xb, zb0), (xb if xb < 0 else xb, za + rr + 0.72), (xa, za + rr + 0.72)], 0.0, 0.38, 'stone2')
        # cordão + ameias do passadiço
        k.box((0, -0.1, th - 2.3), (gw + 0.1, 0.5, 0.25), 'stone_lt', bev=0.03)
        merlons(k, -gw / 2, gw / 2, th - 2.15, -0.05, m='stone', mw=0.85, mh=1.1, gap=0.55, depth=0.5)
        # brasão do Kezor (escudo com chama de brasa)
        k.push((0, -0.35, za + rr + 1.55))
        k.prism([(-0.75, 0.75), (0.75, 0.75), (0.75, -0.1), (0, -0.95), (-0.75, -0.1)], -0.08, 0.12, 'stone_lt')
        k.prism([(-0.6, 0.62), (0.6, 0.62), (0.6, -0.05), (0, -0.78), (-0.6, -0.05)], -0.12, -0.08, 'cloth_r')
        k.prism([(0, 0.48), (0.25, 0.05), (0.12, -0.35), (0, -0.55), (-0.12, -0.35), (-0.25, 0.05)], -0.15, -0.12, 'gold')
        k.pop()
        k.pop()
    # teto da passagem e paredes internas
    k.box((0, 0, th - 2.4 + 0.2), (gw + 0.2, T + 0.2, 0.4), 'stone_dk', bev=0.03)
    k.box((0, 0, za + gw / 2 + 0.3), (gw + 0.1, T, 0.5), 'stone_dk', bev=0)
    for i in range(9):
        a = math.pi * (i + 0.5) / 9
        k.push((math.cos(a) * gw / 2, 0, za + math.sin(a) * gw / 2), ry=90 - math.degrees(a))
        k.box((0, 0, 0), (0.25, T, gw / 2 * math.pi / 9 + 0.02), 'stone2', bev=0.02)
        k.pop()
    # grade levadiça erguida (só a ponta aparece sob o arco de fora)
    for i in range(9):
        x = -gw / 2 + 0.4 + i * (gw - 0.8) / 8
        k.box((x, -T / 2 + 0.5, za + 1.6), (0.1, 0.1, 2.2), 'iron', bev=0.01)
        k.box((x, -T / 2 + 0.5, za + 0.4), (0.06, 0.06, 0.3), 'iron', bev=0.0, top=(0.2, 0.2))
    for z in (za + 1.0, za + 2.0):
        k.box((0, -T / 2 + 0.5, z), (gw - 0.6, 0.12, 0.1), 'iron', bev=0.01)
    # folhas do portão abertas, encostadas nas paredes da passagem (lado de dentro)
    for s in (-1, 1):
        k.push((s * (gw / 2 - 0.25), T / 2 - 0.2, 0), rz=-s * 90)
        k.box((s * -1.55, 0, 2.9), (3.1, 0.18, 5.6), 'plank', bev=0.02)
        for j in range(8):
            k.box((s * -1.55 + (j - 3.5) * 0.38, -0.1, 2.9), (0.34, 0.04, 5.5), 'plank', bev=0.008, var=k.tone(0.08))
        for z in (0.8, 2.9, 5.0):
            k.box((s * -1.55, -0.13, z), (3.0, 0.03, 0.14), 'iron', bev=0.008)
            for j in range(6):
                k.cyl((s * -1.55 + (j - 2.5) * 0.5, -0.15, z), 0.04, 0.04, 'iron', n=6, rx=90)
        k.pop()
    # piso da passagem (lajes)
    x = -gw / 2
    while x < gw / 2 - 0.05:
        w = min(k.rnd.uniform(0.6, 1.0), gw / 2 - x)
        y = -T / 2 - 0.4
        while y < T / 2 + 0.4 - 0.05:
            d = min(k.rnd.uniform(0.6, 1.1), T / 2 + 0.4 - y)
            k.box((x + w / 2, y + d / 2, 0.025), (w - 0.04, d - 0.04, 0.06), k.rnd.choice(['slab', 'slab2']), bev=0.015, var=k.tone(0.1))
            y += d
        x += w
    return k


# ---------------------------------------------------------------------------------------------------- acabamento e exportação
C_YUP = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))   # (x, y, z) Blender -> (x, z, -y) FBX


def _scene_for_bake():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 24
    if sc.world is None:
        sc.world = bpy.data.worlds.new('W')
    sc.world.light_settings.distance = 1.6
    return sc


def finish(ob, ao=True, sharp_deg=38.0, ao_floor=True):
    """Oclusão assada (Cycles) + cor 'Col' (material × tom da peça × oclusão) + 'MatId' + normais com quinas duras."""
    me = ob.data
    for p in me.polygons:
        p.use_smooth = True
    try:
        me.set_sharp_from_angle(angle=math.radians(sharp_deg))
    except Exception:
        pass
    aod = None
    if ao:
        _scene_for_bake()
        floor = None
        if ao_floor:   # chão para escurecer o pé das paredes
            bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.005))
            floor = bpy.context.active_object
        at = me.color_attributes.new('AO', 'FLOAT_COLOR', 'CORNER')
        me.color_attributes.active_color = at
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.bake(type='AO', target='VERTEX_COLORS')
        aod = [0.0] * (len(me.loops) * 4)
        me.color_attributes['AO'].data.foreach_get('color', aod)
        me.color_attributes.remove(me.color_attributes['AO'])
        if floor is not None:
            bpy.data.objects.remove(floor, do_unlink=True)
    mats = list(ob['kmats'])
    cols = [mat_vcol(m) for m in mats]
    ids = [MATS[m][1] if MATS[m][2] == 0 else 0 for m in mats]
    var = me.attributes.get('var')
    vv = [1.0] * len(me.polygons)
    if var is not None:
        var.data.foreach_get('value', vv)
    attr = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    while len(me.uv_layers) < 2:
        me.uv_layers.new(name='UVMap' if len(me.uv_layers) == 0 else 'MatId')
    uv = me.uv_layers[1].data
    out = [0.0] * (len(me.loops) * 4)
    uvo = [0.0] * (len(me.loops) * 2)
    for p in me.polygons:
        c = cols[p.material_index]
        mid = float(ids[p.material_index])
        up = p.normal.z
        for li in p.loop_indices:
            uvo[li * 2] = mid
            if c[3] < 1.0:
                r, g, b, a = c
            else:
                o = aod[li * 4] if aod else 1.0
                kk = vv[p.index] * (1.0 + 0.04 * up) * (0.58 + 0.42 * o)
                r, g, b, a = min(1, c[0] * kk), min(1, c[1] * kk), min(1, c[2] * kk), 1.0
            out[li * 4:li * 4 + 4] = (r, g, b, a)
    # color_srgb em lote: converte para linear porque foreach_set grava 'color' (linear)
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in out]
    for i in range(3, len(lin), 4):
        lin[i] = out[i]
    attr.data.foreach_set('color', lin)
    uv.foreach_set('uv', uvo)
    me.color_attributes.active_color = attr
    try:
        me.color_attributes.render_color_index = me.color_attributes.find('Col')
    except Exception:
        pass
    if var is not None:
        me.attributes.remove(var)
    return ob


def export_fbx(ob, path):
    """Exporta o objeto (e filhos) com malha já em Y para cima e rotação zero, como o kezor_export.py."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    kids = [ob] + list(ob.children_recursive)
    saved = {k: (k.matrix_basis.copy(), k.matrix_parent_inverse.copy()) for k in kids}
    for k in kids:
        k.data.transform(C_YUP)
    for k in kids[1:]:
        loc = k.matrix_basis.to_translation()
        k.matrix_parent_inverse = Matrix.Identity(4)
        k.matrix_basis = Matrix.Translation(C_YUP @ loc)
    ob.matrix_world = C_YUP.inverted()
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for k in kids:
        k.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
                             global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=False, axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False,
                             bake_anim=False, path_mode='AUTO', embed_textures=False, use_custom_props=False, colors_type='SRGB')
    for k in kids:
        k.data.transform(C_YUP.inverted())
    for k, (mb, mpi) in saved.items():
        k.matrix_parent_inverse = mpi
        k.matrix_basis = mb


def build_object(fn, coll, ao=True):
    k = fn()
    ob = k.to_object(coll)
    finish(ob, ao=ao)
    for (cname, ck, loc) in k.children:
        co = ck.to_object(coll)
        finish(co, ao=False)
        co.parent = ob
        co.matrix_parent_inverse = Matrix.Identity(4)
        co.location = loc
    return ob


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons) + sum(tri_count(c) for c in ob.children)


# ---------------------------------------------------------------------------------------------------- casas
def _face(k, w, d, side):
    """Entra no referencial de uma fachada: 0 frente (-Y), 1 direita (+X), 2 fundos (+Y), 3 esquerda (-X).
    Retorna a largura da fachada. Plano y = 0, frente para fora."""
    if side == 0:
        k.push((0, -d / 2, 0))
        return w
    if side == 1:
        k.push((w / 2, 0, 0), rz=90)
        return d
    if side == 2:
        k.push((0, d / 2, 0), rz=180)
        return w
    k.push((-w / 2, 0, 0), rz=-90)
    return d


def quoins(k, w, d, z0, z1, m='stone_lt'):
    """Cunhais: blocos alternados longos/curtos nas quinas."""
    for (sx, sy) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        z = z0
        i = 0
        while z < z1 - 0.1:
            h = min(0.42, z1 - z)
            a, b = (0.62, 0.34) if i % 2 == 0 else (0.34, 0.62)
            k.box((sx * (w / 2 - a / 2 + 0.06), sy * (d / 2 + 0.03), z + h / 2), (a, 0.14, h - 0.03), m, bev=0.025, var=k.tone(0.06))
            k.box((sx * (w / 2 + 0.03), sy * (d / 2 - b / 2 + 0.06), z + h / 2), (0.14, b, h - 0.03), m, bev=0.025, var=k.tone(0.06))
            z += h
            i += 1


def stone_storey(k, w, d, z0, h, sides_open, m='stone', alt='stone2'):
    """Andar de cantaria: para cada fachada, lista de aberturas [(tipo, x, ...)]. tipo 'w' janela, 'd' porta, 's' vitrine."""
    k.box((0, 0, z0 + h / 2), (w - 0.5, d - 0.5, h), 'mortar', bev=0, var=0.9)
    for side in range(4):
        L = _face(k, w, d, side)
        ops = sides_open.get(side, [])
        holes = []
        for o in ops:
            if o[0] == 'w':
                _, x, zz, ww, hh = o
                holes.append((x - ww / 2 - 0.12, x + ww / 2 + 0.12, z0 + zz - 0.12, z0 + zz + hh + 0.12))
            elif o[0] == 'd':
                _, x, ww, hh = o[:4]
                holes.append((x - ww / 2 - 0.3, x + ww / 2 + 0.3, z0, z0 + hh + ww * 0.3 + 0.35))
            elif o[0] == 's':
                _, x, ww, hh = o
                holes.append((x - ww / 2, x + ww / 2, z0 + 0.6, z0 + 0.6 + hh))
        stone_face(k, -L / 2, L / 2, z0, z0 + h, m=m, alt=alt, openings=holes, ch=(0.38, 0.5), bw=(0.55, 1.0), depth=0.25)
        for o in ops:
            if o[0] == 'w':
                _, x, zz, ww, hh = o
                window(k, x, z0 + zz, w=ww, h=hh, frame='timber', sill='stone_lt', depth=0.16)
            elif o[0] == 'd':
                _, x, ww, hh = o[:4]
                door(k, x, w=ww, h=hh, z0=z0, lantern=len(o) < 5)
            elif o[0] == 's':
                _, x, ww, hh = o
                shop_window(k, x, z0 + 0.6, ww, hh)
        k.pop()
    quoins(k, w, d, z0, z0 + h)


def shop_window(k, x, z, w, h):
    """Vitrine de loja: balcão de madeira, vidraça em grade acesa e verga."""
    k.box((x, 0.02, z + h / 2), (w, 0.02, h), 'glass_dim', bev=0)
    n = max(2, int(w / 0.5))
    for i in range(n + 1):
        k.box((x - w / 2 + i * w / n, -0.04, z + h / 2), (0.06, 0.08, h), 'timber', bev=0.01)
    for zz in (0.0, h * 0.5, h):
        k.box((x, -0.04, z + zz), (w + 0.1, 0.08, 0.07), 'timber', bev=0.01)
    k.box((x, -0.25, z - 0.08), (w + 0.4, 0.55, 0.12), 'wood', bev=0.02)
    k.box((x, -0.2, z - 0.4), (w + 0.2, 0.3, 0.6), 'plank', bev=0.02)
    k.box((x, -0.1, z + h + 0.15), (w + 0.6, 0.3, 0.3), 'stone_lt', bev=0.03)


def timber_storey(k, w, d, z0, h, wins, m='plaster', seed_off=0, beam='timber'):
    """Andar de enxaimel com janelas: wins = {lado: [(x, larg, alt)]}."""
    k.box((0, 0, z0 + h / 2), (w - 0.3, d - 0.3, h), m, bev=0, var=0.92)
    for side in range(4):
        L = _face(k, w, d, side)
        ws = wins.get(side, [])
        slots = [(x - ww / 2 - 0.15, x + ww / 2 + 0.15) for (x, ww, hh) in ws]
        timber_wall(k, -L / 2, L / 2, z0, z0 + h, m=m, posts=1.2, beam=beam, seed_off=seed_off + side, window_slots=slots)
        for (x, ww, hh) in ws:
            window(k, x, z0 + 0.85, w=ww, h=hh, frame='timber', sill='timber', depth=0.14, pane_y=-0.015)
            if (side + int(x * 3)) % 3 == 0:
                flower_box(k, x, z0 + 0.8, ww)
        k.pop()
    # vigas do soalho (cabeças de barrote) na frente
    n = int(w / 0.6)
    for i in range(n):
        x = -w / 2 + (i + 0.5) * w / n
        k.box((x, -d / 2 - 0.08, z0 - 0.06), (0.14, 0.3, 0.16), beam, bev=0.015)
        k.box((x, d / 2 + 0.08, z0 - 0.06), (0.14, 0.3, 0.16), beam, bev=0.015)
    k.box((0, 0, z0 - 0.17), (w + 0.12, d + 0.12, 0.12), beam, bev=0.015)


def flower_box(k, x, z, w):
    k.box((x, -0.3, z - 0.1), (w + 0.1, 0.26, 0.22), 'plank', bev=0.015)
    for i in range(5):
        xx = x - w / 2 + (i + 0.5) * w / 5
        k.box((xx, -0.3, z + 0.08), (0.2, 0.2, 0.18), k.rnd.choice(['leaf', 'leaf2']), bev=0.04, rz=k.rnd.uniform(0, 45))
        if k.rnd.random() < 0.7:
            k.box((xx + k.rnd.uniform(-0.05, 0.05), -0.36, z + 0.2), (0.09, 0.09, 0.08), k.rnd.choice(['flower_r', 'flower_y', 'flower_v']), bev=0.02)


def corbels(k, w, z, d):
    """Mãos-francesas sob o balanço (jetty) da fachada da frente."""
    for x in (-w / 2 + 0.3, w / 2 - 0.3):
        k.seg((x, -d / 2 - 0.02, z - 0.9), (x, -d / 2 - 0.4, z - 0.1), 0.14, 0.14, 'timber')


def townhouse(name, seed, w, d, floors_stone=1, floors_timber=1, ridge='x', roof='clay', jetty=0.35, chimney_x=None,
              door_x=0.0, plaster='plaster', stone='stone', storey=3.0, pitch=None, dormers=0, extras=None, front_wins=None):
    k = Kit(name, seed)
    rnd = k.rnd
    z = 0.0
    # soco/embasamento
    k.box((0, 0, 0.12), (w + 0.25, d + 0.25, 0.24), 'stone_dk', bev=0.04)
    z = 0.2
    for f in range(floors_stone):
        ops = {}
        fw = front_wins if front_wins is not None else [x for x in (-w / 2 + 1.2, w / 2 - 1.2) if abs(x - door_x) > 1.4]
        if f == 0:
            ops[0] = [('d', door_x, 1.3, 2.25)] + [('w', x, 1.0, 0.9, 1.2) for x in fw]
        else:
            ops[0] = [('w', x, 0.8, 0.9, 1.35) for x in ([door_x] + fw)]
        ops[1] = [('w', 0.0, 1.0, 0.8, 1.15)] if d > 5 else []
        ops[3] = [('w', 0.0, 1.0, 0.8, 1.15)] if d > 5 and f > 0 else []
        ops[2] = [('w', x, 1.0, 0.8, 1.15) for x in (-w / 4, w / 4)]
        stone_storey(k, w, d, z, storey, ops, m=stone)
        k.box((0, 0, z + storey - 0.08), (w + 0.18, d + 0.18, 0.16), 'stone_lt', bev=0.03)
        z += storey
    ww, dd = w, d
    for f in range(floors_timber):
        dd = dd + 2 * jetty
        wins = {0: [(x, 0.85, 1.25) for x in ([-ww / 2 + 1.1, 0.0, ww / 2 - 1.1] if ww > 6 else [-ww / 4 - 0.1, ww / 4 + 0.1])],
                2: [(x, 0.8, 1.1) for x in (-ww / 4, ww / 4)],
                1: [(0.0, 0.8, 1.1)], 3: [(0.0, 0.8, 1.1)] if dd > 6 else []}
        if jetty > 0:
            corbels(k, ww, z, dd - 2 * jetty)
        timber_storey(k, ww, dd, z, storey - 0.2, wins, m=plaster, seed_off=f + seed)
        z += storey - 0.2
    # telhado
    if ridge == 'x':
        ph = pitch or dd * 0.62
        zr = gable_roof(k, ww, dd, z, ph, kind=roof, end_m=plaster)
    else:
        ph = pitch or ww * 0.72
        k.push((0, 0, 0), rz=90)
        zr = gable_roof(k, dd, ww, z, ph, kind=roof, end_m=plaster)
        k.pop()
    # trapeiras (dormers) na água da frente
    if dormers and ridge == 'x':
        for i in range(dormers):
            x = -ww / 2 + (i + 1) * ww / (dormers + 1)
            dormer(k, x, -dd / 2 + dd * 0.22, z + ph * 0.22, roof)
    if chimney_x is not None:
        cy = dd * 0.18 if ridge == 'x' else 0.0
        cx = chimney_x
        chimney(k, cx, cy, z - 0.5, zr + 0.9)
        k.meta_smoke = (cx, cy, zr + 1.5)
    k.meta_top = zr
    k.meta_size = (w, d)
    if extras:
        extras(k, w, d, z)
    return k


def dormer(k, x, y, z, roof):
    """Trapeira: caixinha com janela e telhadinho de duas águas, apoiada na água da frente."""
    k.push((x, y, z))
    k.box((0, 0.2, 0.6), (1.3, 1.4, 1.2), 'plaster', bev=0.02)
    k.push((0, -0.5, 0))
    window(k, 0, 0.25, w=0.6, h=0.75, shutters=False, depth=0.1, pane_y=-0.015)
    k.pop()
    k.push((0, 0.1, 0), rz=90)
    gable_roof(k, 1.6, 1.3, 1.2, 0.75, over=0.15, gable_over=0.15, kind=roof, timber_gable=False)
    k.pop()
    k.pop()


def _props_door(k, w, d, z):
    """Barris e caixote ao lado da porta."""
    k.cyl((-w / 2 + 0.7, -d / 2 - 0.6, 0), 0.32, 0.85, 'wood', n=10, r2=0.3)
    for zz in (0.12, 0.72):
        k.cyl((-w / 2 + 0.7, -d / 2 - 0.6, zz), 0.335, 0.05, 'iron', n=10)
    k.box((-w / 2 + 1.45, -d / 2 - 0.55, 0.3), (0.6, 0.6, 0.6), 'plank', bev=0.03, rz=12)


def casa4a():
    return townhouse('Casa4A', 401, 7.0, 6.0, 1, 1, ridge='x', roof='clay', chimney_x=2.2, door_x=-0.8, extras=_props_door)


def casa4b():
    return townhouse('Casa4B', 402, 5.6, 6.6, 1, 2, ridge='y', roof='slate', chimney_x=None, door_x=0.0, plaster='plaster2')


def casa4c():
    k = townhouse('Casa4C', 403, 8.0, 6.4, 2, 0, ridge='x', roof='slate', chimney_x=-2.6, door_x=0.0, dormers=2, stone='stone2',
                  pitch=3.4)
    return k


def casa4d():
    def ex(k, w, d, z):
        # puxado lateral (galpão) com telhado de uma água
        k.push((w / 2 + 1.2, 0.4, 0))
        k.box((0, 0, 1.2), (2.2, 4.0, 2.4), 'plank', bev=0.02)
        for i in range(6):
            k.box((-1.12, -1.6 + i * 0.64, 1.2), (0.05, 0.58, 2.38), 'plank', bev=0.008, var=k.tone(0.1))
        tile_roof_side(k, -1.35, 1.35, -2.3, 2.45, 2.3, 3.4, kind='clay') if False else None
        k.push((0, 0, 0), rz=90)
        tile_roof_side(k, -2.3, 2.3, -1.45, 2.35, 1.2, 3.25, ('tile', 'tile2', 'tile3'), 'clay')
        k.pop()
        k.box((-0.2, -2.1, 0.5), (1.6, 0.25, 1.0), 'wood', bev=0.02)
        k.pop()
        # lenha empilhada
        for i in range(10):
            k.cyl((w / 2 + 0.2 + (i % 5) * 0.3, -d / 2 - 0.2, 0.15 + (i // 5) * 0.26), 0.12, 1.0, 'wood_lt', n=7, rx=90)
    return townhouse('Casa4D', 404, 6.4, 6.0, 1, 1, ridge='y', roof='clay', chimney_x=None, door_x=1.2, plaster='plaster3',
                     extras=ex, front_wins=[-1.5])


def hanging_sign(k, x, y, z, cloth='cloth_r', emblem='gold', w=0.8, h=0.6):
    """Placa pendurada num braço de ferro, saindo da fachada (plano y) para -Y."""
    k.seg((x, y, z), (x, y - 1.1, z), 0.06, 0.06, 'iron')
    k.seg((x, y - 0.05, z - 0.55), (x, y - 0.75, z - 0.03), 0.04, 0.04, 'iron')
    for s in (-1, 1):
        k.seg((x, y - 0.6 + s * 0.28, z), (x, y - 0.6 + s * 0.28, z - 0.2), 0.02, 0.02, 'iron')
    k.box((x, y - 0.6, z - 0.2 - h / 2), (0.08, w, h), 'plank', bev=0.02)
    k.box((x, y - 0.6, z - 0.2 - h / 2), (0.1, w * 0.86, h * 0.8), cloth, bev=0.015)
    k.box((x, y - 0.6, z - 0.2 - h / 2), (0.12, w * 0.32, h * 0.42), emblem, bev=0.01, rx=45)


def taverna4():
    """Taverna do Lume Velho: corpo de pedra, dois andares de enxaimel, frontão central, varanda com mesas, placa e barris."""
    def ex(k, w, d, z):
        # frontão (cross gable) no meio da fachada
        k.push((0, -d / 2 - 0.35, 0))
        k.push((0, 0.9, z - 0.2))
        k.box((0, 0, 1.4), (3.6, 1.8, 2.8), 'plaster', bev=0)
        k.push((0, -0.9, 0))
        timber_wall(k, -1.8, 1.8, 0, 2.8, posts=0.9, window_slots=[(-0.6, 0.6)])
        window(k, 0, 0.8, w=0.9, h=1.3, depth=0.14, pane_y=-0.015)
        k.pop()
        k.push((0, 0.0, 0), rz=90)
        gable_roof(k, 2.2, 3.6, 2.8, 2.0, over=0.3, gable_over=0.3, kind='clay')
        k.pop()
        k.pop()
        k.pop()
        # varanda coberta na frente
        pz = 2.7
        for x in (-w / 2 + 0.4, -1.6, 1.6, w / 2 - 0.4):
            k.box((x, -d / 2 - 1.9, pz / 2), (0.2, 0.2, pz), 'timber', bev=0.02)
            k.box((x, -d / 2 - 1.9, 0.1), (0.36, 0.36, 0.2), 'stone_dk', bev=0.03)
            k.seg((x, -d / 2 - 1.9, pz - 0.6), (x + (0.5 if x < 0 else -0.5), -d / 2 - 1.9, pz - 0.05), 0.12, 0.12, 'timber')
        k.box((0, -d / 2 - 1.9, pz + 0.05), (w - 0.4, 0.22, 0.2), 'timber', bev=0.02)
        tile_roof_side(k, -w / 2, w / 2, -d / 2 - 2.3, pz - 0.05, -d / 2 - 0.05, pz + 0.9, ('tile', 'tile2', 'tile3'), 'clay')
        k.box((0, -d / 2 - 1.0, 0.08), (w - 0.2, 2.2, 0.16), 'plank', bev=0.015)
        for x in (-3.4, 3.3):
            k.box((x, -d / 2 - 1.2, 0.8), (1.4, 0.8, 0.08), 'wood', bev=0.02)
            k.box((x, -d / 2 - 1.2, 0.4), (0.12, 0.12, 0.78), 'wood', bev=0.01)
            for sy in (-1, 1):
                k.box((x, -d / 2 - 1.2 + sy * 0.65, 0.45), (1.3, 0.3, 0.06), 'wood', bev=0.01)
                k.box((x, -d / 2 - 1.2 + sy * 0.65, 0.22), (0.1, 0.1, 0.44), 'wood', bev=0.01)
            k.cyl((x + 0.3, -d / 2 - 1.2, 0.84), 0.06, 0.16, 'wood_lt', n=8)
            k.cyl((x - 0.25, -d / 2 - 1.1, 0.84), 0.05, 0.12, 'bronze', n=8)
        hanging_sign(k, 1.5, -d / 2 - 0.05, 3.4 + 0.2, cloth='cloth_g')
        # barris empilhados ao lado
        for (bx, by, bz) in ((w / 2 + 0.55, -d / 2 + 0.6, 0), (w / 2 + 0.55, -d / 2 + 1.3, 0), (w / 2 + 0.55, -d / 2 + 0.95, 0.62)):
            k.cyl((bx - 0.45, by, bz + 0.32), 0.32, 0.9, 'wood', n=10, ry=90)
            for xx in (-0.3, 0.3):
                k.cyl((bx - 0.45 + xx, by, bz + 0.32), 0.335, 0.05, 'iron', n=10, ry=90)
    return townhouse('Taverna4', 405, 11.0, 7.6, 1, 2, ridge='x', roof='clay', chimney_x=4.2, door_x=0.0, extras=ex,
                     front_wins=[-3.6, 3.6], pitch=4.4)


def loja4():
    """Loja da Mercadora: térreo com vitrine larga e toldo listrado, andar de enxaimel, placa."""
    def ex(k, w, d, z):
        # toldo listrado sobre a vitrine
        k.push((1.1, -d / 2 - 0.1, 2.85))
        n = 8
        for i in range(n):
            x = -1.6 + (i + 0.5) * 3.2 / n
            k.box((x, -0.75, -0.3), (3.2 / n + 0.005, 1.6, 0.04), 'cloth_r' if i % 2 == 0 else 'cloth_w', bev=0, rx=-22)
        for i in range(n):
            x = -1.6 + (i + 0.5) * 3.2 / n
            k.box((x, -1.48, -0.72), (3.2 / n, 0.04, 0.3), 'cloth_r' if i % 2 == 0 else 'cloth_w', bev=0)
        for s in (-1, 1):
            k.seg((s * 1.6, 0, 0.1), (s * 1.6, -1.5, -0.55), 0.04, 0.04, 'iron')
        k.pop()
        # mercadorias na frente
        for i, m in enumerate(('wood', 'plank', 'wood_lt')):
            k.box((1.1 - 1.0 + i * 0.75, -d / 2 - 0.65, 0.95), (0.5, 0.4, 0.25), m, bev=0.02, rz=k.rnd.uniform(-15, 15))
        k.cyl((-w / 2 + 0.6, -d / 2 - 0.55, 0), 0.3, 0.8, 'wood', n=10, r2=0.27)
        k.box((-w / 2 + 0.6, -d / 2 - 0.55, 0.85), (0.5, 0.5, 0.1), 'hay', bev=0.04)
        hanging_sign(k, -w / 2 + 0.6, -d / 2 - 0.05, 3.2 + 0.2, cloth='cloth_y', emblem='bronze')
    return townhouse('Loja4', 406, 7.0, 6.4, 1, 1, ridge='y', roof='clay', chimney_x=None, door_x=-1.7, extras=ex,
                     front_wins=[], plaster='plaster2')


def _shop_front(k, w, d):
    pass


def ferraria4():
    """Ferraria do Bram: casa de pedra com sótão, galpão aberto ao lado com forja de tijolo acesa, chaminé alta,
    bigorna, tina d'água, armas no suporte e pilha de carvão. A forja fica em (+3,9; -0,4) local."""
    def ex(k, w, d, z):
        # galpão aberto (+X)
        gx = w / 2 + 2.3
        for (x, y) in ((gx - 2.2, -d / 2 + 0.3), (gx + 2.1, -d / 2 + 0.3), (gx + 2.1, d / 2 - 0.3), (gx - 2.2, d / 2 - 0.3)):
            k.box((x, y, 1.5), (0.26, 0.26, 3.0), 'timber', bev=0.02)
            k.box((x, y, 0.12), (0.44, 0.44, 0.24), 'stone_dk', bev=0.03)
        k.box((gx, -d / 2 + 0.3, 3.0), (4.6, 0.24, 0.24), 'timber', bev=0.02)
        k.box((gx, d / 2 - 0.3, 3.0), (4.6, 0.24, 0.24), 'timber', bev=0.02)
        k.push((gx, 0, 0), rz=90)
        tile_roof_side(k, -d / 2 - 0.2, d / 2 + 0.2, -2.6, 2.95, 2.25, 4.2, ('slate', 'slate2', 'slate3'), 'slate')
        k.pop()
        # piso de lajes do galpão
        k.box((gx, 0, 0.04), (4.4, d - 0.4, 0.08), 'slab2', bev=0.02)
        # forja de tijolo com coifa e chaminé
        fx, fy = gx + 1.0, 0.8
        k.box((fx, fy, 0.5), (1.8, 1.4, 1.0), 'stone_dk', bev=0.04)
        k.box((fx, fy, 1.05), (1.6, 1.2, 0.12), 'basalt', bev=0.02)
        k.box((fx, fy - 0.1, 1.15), (1.0, 0.7, 0.12), 'ember', bev=0.02)
        k.lathe((fx, fy - 0.1, 1.15), [(0.35, 0), (0.22, 0.2), (0.0, 0.35)], 'fire', n=8)
        k.box((fx, fy + 0.2, 2.0), (1.7, 1.0, 0.2), 'stone2', bev=0.03, top=(0.55, 0.6))
        chimney(k, fx, fy + 0.35, 2.1, 6.3, m='stone_dk')
        # fole
        k.box((fx - 1.2, fy, 0.7), (0.5, 0.9, 0.25), 'wood', bev=0.03, rx=8)
        k.box((fx - 1.2, fy, 0.82), (0.45, 0.85, 0.1), 'cloth_dk', bev=0.02, rx=8)
        # bigorna no tronco
        ax, ay = gx - 0.6, -0.6
        k.cyl((ax, ay, 0), 0.38, 0.6, 'wood', n=10, r2=0.34)
        k.box((ax, ay, 0.68), (0.32, 0.22, 0.16), 'iron', bev=0.02)
        k.box((ax, ay, 0.82), (0.7, 0.26, 0.14), 'iron', bev=0.02, top=(1.0, 1.0))
        k.box((ax + 0.45, ay, 0.84), (0.25, 0.14, 0.08), 'iron', bev=0.01, top=(0.2, 0.6))
        # tina d'água
        k.cyl((gx - 1.6, 0.9, 0), 0.45, 0.55, 'wood', n=12)
        k.cyl((gx - 1.6, 0.9, 0.48), 0.4, 0.05, 'water', n=12)
        # suporte de armas e escudo
        k.box((gx + 1.9, -1.3, 0.9), (0.12, 1.4, 0.1), 'wood', bev=0.01)
        for i in range(4):
            yy = -1.85 + i * 0.36
            k.box((gx + 1.85, yy, 0.95), (0.05, 0.08, 1.5), 'steel', bev=0.01, rx=8)
            k.box((gx + 1.85, yy, 0.45), (0.06, 0.3, 0.06), 'gold', bev=0.01)
        k.cyl((gx + 2.0, 0.2, 1.3), 0.45, 0.08, 'bronze', n=12, ry=90)
        k.cyl((gx + 2.05, 0.2, 1.3), 0.12, 0.06, 'iron', n=8, ry=90)
        # carvão
        for i in range(10):
            k.box((gx - 1.4 + k.rnd.uniform(-0.4, 0.4), 1.9 + k.rnd.uniform(-0.2, 0.2), 0.1 + k.rnd.uniform(0, 0.2)), (0.25, 0.22, 0.18), 'basalt', bev=0.05, rz=k.rnd.uniform(0, 90))
        hanging_sign(k, -w / 2 + 0.7, -d / 2 - 0.05, 3.4, cloth='cloth_dk', emblem='steel')
        k.meta_forge = (fx, fy)
    return townhouse('Ferraria4', 407, 7.0, 6.4, 1, 1, ridge='x', roof='slate', chimney_x=-2.2, door_x=0.6, extras=ex,
                     front_wins=[-1.9], jetty=0.0, plaster='plaster3')


def capela4():
    """Capela do Lume: nave de pedra com contrafortes e janelas ogivais acesas, rosácea sobre o portal,
    telhado alto de lousa e campanário na frente com flecha. Frente (portal) = -Y."""
    k = Kit('Capela4', 501)
    W, D, H = 8.4, 14.0, 7.2
    k.box((0, 0, 0.15), (W + 0.6, D + 0.6, 0.3), 'stone_dk', bev=0.04)
    z0 = 0.3
    # nave
    ops = {1: [], 3: [], 2: [], 0: []}
    for side in (1, 3):
        ops[side] = [('w', y, 2.0, 1.1, 3.4) for y in (-4.2, -1.4, 1.4, 4.2)]
    k.box((0, 0, z0 + H / 2), (W - 0.5, D - 0.5, H), 'mortar', bev=0)
    for side in range(4):
        L = _face(k, W, D, side)
        holes = []
        if side in (1, 3):
            for y in (-4.2, -1.4, 1.4, 4.2):
                holes.append((y - 0.75, y + 0.75, z0 + 1.8, z0 + 6.1))
        if side == 0:
            holes = [(-1.5, 1.5, z0, z0 + 4.4), (-1.4, 1.4, z0 + 4.7, z0 + 7.2)]
        stone_face(k, -L / 2, L / 2, z0, z0 + H, m='stone_lt', alt='stone', openings=holes, ch=(0.42, 0.55), bw=(0.6, 1.1))
        if side in (1, 3):
            for y in (-4.2, -1.4, 1.4, 4.2):
                gothic_window(k, y, z0 + 2.0, 1.1, 3.6)
            for y in (-5.6, -2.8, 0.0, 2.8, 5.6):
                buttress(k, y, z0, H + 0.2)
        if side == 2:
            gothic_window(k, 0, z0 + 2.4, 1.4, 3.6)
        if side == 0:
            k.box((0, 0.03, z0 + 2.0), (2.4, 0.1, 4.0), 'plank', bev=0.02)
            for i in range(6):
                k.box((-1.0 + i * 0.4, -0.03, z0 + 2.0), (0.36, 0.05, 3.9), 'plank', bev=0.01, var=k.tone(0.08))
            for zz in (0.7, 2.0, 3.3):
                k.box((0, -0.06, z0 + zz), (2.3, 0.03, 0.1), 'iron', bev=0.01)
            # arquivoltas do portal (três arcos em degrau)
            for j, rr in enumerate((1.35, 1.6, 1.85)):
                n = 11
                for i in range(n):
                    a = math.pi * (i + 0.5) / n
                    k.push((math.cos(a) * rr, -0.1 - j * 0.12, z0 + 3.6 + math.sin(a) * rr * 0.75), ry=90 - math.degrees(a))
                    k.box((0, 0, 0), (0.24, 0.3, rr * math.pi / n * 0.9), 'stone_lt', bev=0.03, var=k.tone(0.05))
                    k.pop()
                for s in (-1, 1):
                    k.box((s * rr, -0.1 - j * 0.12, z0 + 1.8), (0.24, 0.3, 3.6), 'stone_lt', bev=0.03)
            k.box((0, 0.0, z0 + 4.2), (2.6, 0.08, 1.0), 'mortar', bev=0)
            # rosácea
            k.push((0, -0.05, z0 + 5.95))
            k.cyl((0, 0.06, 0), 1.25, 0.1, 'rose', n=24, rx=90)
            k.lathe((0, 0, 0), [(1.45, 0), (1.25, 0)], 'stone_lt', n=24) if False else None
            for i in range(24):
                a = i * 15
                k.push((0, 0, 0), ry=a)
                k.box((1.35, -0.05, 0), (0.25, 0.3, 0.38), 'stone_lt', bev=0.02)
                k.pop()
            for i in range(8):
                k.push((0, 0, 0), ry=i * 22.5)
                k.box((0, -0.06, 0), (2.4, 0.08, 0.07), 'stone_lt', bev=0.01)
                k.pop()
            k.cyl((0, 0.0, 0), 0.25, 0.2, 'stone_lt', n=10, rx=90)
            k.pop()
        k.pop()
    quoins(k, W, D, z0, z0 + H)
    k.box((0, 0, z0 + H + 0.1), (W + 0.3, D + 0.3, 0.2), 'stone_lt', bev=0.03)
    # telhado alto de lousa (cumeeira em Y)
    k.push((0, 0, 0), rz=90)
    zr = gable_roof(k, D, W, z0 + H + 0.2, 5.0, over=0.5, gable_over=0.2, kind='slate', end_m='stone_lt', timber_gable=False)
    k.pop()
    k.cyl((0, D / 2 + 0.3, zr), 0.06, 1.4, 'gold', n=6)
    k.box((0, D / 2 + 0.3, zr + 1.0), (0.6, 0.06, 0.08), 'gold', bev=0)
    # campanário na frente (sobre o portal, recuado)
    tz = z0 + H + 0.2
    tw = 3.2
    k.push((0, -D / 2 + 1.7, tz))
    k.box((0, 0, 2.6), (tw - 0.3, tw - 0.3, 5.2), 'mortar', bev=0)
    for side in range(4):
        L = _face(k, tw, tw, side)
        stone_face(k, -L / 2, L / 2, 0, 5.2, m='stone_lt', alt='stone', openings=[(-0.65, 0.65, 2.3, 4.6)], ch=(0.38, 0.5), bw=(0.5, 0.9))
        gothic_window(k, 0, 2.4, 0.9, 2.1, glow='basalt', sill=False)
        k.pop()
    k.box((0, 0, 5.3), (tw + 0.4, tw + 0.4, 0.25), 'stone_lt', bev=0.03)
    k.box((0, 0, 3.4), (0.7, 0.7, 0.1), 'iron', bev=0)
    k.lathe((0, 0, 2.8), [(0.0, 0.0), (0.5, 0.05), (0.42, 0.4), (0.28, 0.75), (0.0, 0.8)], 'bronze', n=12)
    # flecha
    cone_roof(k, 0, 0, 5.4, tw / 2 * 1.32, 7.0, rows=18, n=4 * 4, curve=1.0)
    k.cyl((0, 0, 12.2), 0.05, 1.6, 'gold', n=6)
    k.box((0, 0, 13.4), (0.7, 0.06, 0.08), 'gold', bev=0)
    k.lathe((0, 0, 12.3), [(0.14, 0), (0.0, 0.3)], 'gold', n=8)
    k.pop()
    # degraus do portal e braseiros
    for i in range(3):
        k.box((0, -D / 2 - 0.6 - i * 0.45, 0.25 - i * 0.1), (3.6 + i * 0.6, 0.5, 0.2), 'stone_lt', bev=0.03)
    for s in (-1, 1):
        brazier(k, s * 2.6, -D / 2 - 1.4, s=0.85)
    k.meta_size = (W, D)
    return k


def gothic_window(k, x, z, w, h, glow='glass', sill=True):
    """Janela ogival (arco quebrado de aduelas) com vitral aceso e mainel."""
    k.box((x, 0.02, z + h / 2), (w, 0.02, h + w * 0.3), glow, bev=0)
    for s in (-1, 1):
        k.box((x + s * (w / 2 + 0.1), -0.08, z + (h - w / 2) / 2), (0.2, 0.24, h - w / 2), 'stone_lt', bev=0.02)
    rr = w / 2 + 0.1
    n = 5
    for s in (-1, 1):
        for i in range(n):
            a = math.pi / 2 * (i + 0.5) / n
            cx = x - s * 0.18
            px = cx + s * math.cos(a) * (rr + 0.18)
            pz = z + h - w / 2 + math.sin(a) * (rr + 0.2) * 1.15
            k.push((px, -0.08, pz), ry=(90 - math.degrees(a)) * s)
            k.box((0, 0, 0), (0.2, 0.24, 0.32), 'stone_lt', bev=0.02)
            k.pop()
    k.box((x, -0.02, z + h / 2), (0.07, 0.06, h), 'stone_lt', bev=0.01)
    if sill:
        k.box((x, -0.12, z - 0.06), (w + 0.4, 0.3, 0.12), 'stone_lt', bev=0.02)


def buttress(k, x, z0, h):
    """Contraforte em degraus com capa inclinada."""
    k.box((x, -0.4, z0 + h * 0.3), (0.7, 0.8, h * 0.6), 'stone', bev=0.04)
    k.box((x, -0.3, z0 + h * 0.75), (0.6, 0.6, h * 0.35), 'stone', bev=0.04)
    k.box((x, -0.3, z0 + h * 0.95), (0.62, 0.62, 0.3), 'stone_lt', bev=0.03, top=(1, 0.1), shift=(0, 0.25))
    k.box((x, -0.55, z0 + h * 0.62), (0.74, 0.5, 0.25), 'stone_lt', bev=0.03, top=(1, 0.1), shift=(0, 0.2))


def guilda4():
    """Salão das Guildas: pedra clara em dois andares, janelas em arco, pórtico com colunas e frontão, escadaria,
    estandartes e torreões de canto com telhado cônico."""
    k = Kit('Guilda4', 601)
    W, D = 13.0, 9.0
    k.box((0, 0, 0.3), (W + 0.6, D + 0.6, 0.6), 'stone_dk', bev=0.05)
    z0 = 0.6
    sh = 3.6
    for f in range(2):
        ops = {0: [], 1: [], 2: [], 3: []}
        xs = [-4.6, -2.4, 2.4, 4.6]
        ops[0] = [('w', x, 1.0, 1.0, 1.8 if f == 0 else 2.0) for x in xs]
        if f == 0:
            ops[0].append(('d', 0.0, 1.8, 2.8, 'nolantern'))
        else:
            ops[0].append(('w', 0.0, 0.8, 1.4, 2.2))
        ops[2] = [('w', x, 1.0, 1.0, 1.8) for x in xs]
        ops[1] = [('w', y, 1.0, 1.0, 1.8) for y in (-2, 2)]
        ops[3] = [('w', y, 1.0, 1.0, 1.8) for y in (-2, 2)]
        stone_storey(k, W, D, z0, sh, ops, m='stone_lt', alt='stone')
        k.box((0, 0, z0 + sh - 0.1), (W + 0.25, D + 0.25, 0.22), 'stone', bev=0.04)
        z0 += sh
    # telhado
    zr = gable_roof(k, W, D, z0, 4.2, over=0.5, gable_over=0.3, kind='slate', end_m='stone_lt', timber_gable=False)
    # pórtico: 4 colunas, entablamento e frontão triangular
    py = -D / 2 - 1.6
    for x in (-2.6, -0.9, 0.9, 2.6):
        k.box((x, py, 0.75), (0.75, 0.75, 0.3), 'stone_lt', bev=0.04)
        k.lathe((x, py, 0.9), [(0.3, 0), (0.28, 0.2), (0.26, 3.6), (0.34, 3.75), (0.34, 3.95)], 'stone_lt', n=12)
        k.box((x, py, 4.95), (0.8, 0.8, 0.2), 'stone_lt', bev=0.03)
    k.box((0, py + 0.6, 5.3), (6.6, 2.0, 0.5), 'stone_lt', bev=0.04)
    k.push((0, py - 0.35, 0))
    k.prism([(-3.4, 5.55), (3.4, 5.55), (0, 7.1)], -0.05, 2.0, 'stone_lt')
    k.prism([(-2.8, 5.75), (2.8, 5.75), (0, 6.85)], -0.1, -0.05, 'cloth_r')
    k.prism([(0, 6.7), (0.32, 6.15), (0, 5.85), (-0.32, 6.15)], -0.14, -0.1, 'gold')
    k.pop()
    k.push((0, py + 1.0, 0))
    tile_roof_side(k, -3.6, 3.6, -1.5, 5.6, -1.5 + 0.01, 5.6, kind='slate') if False else None
    k.pop()
    # escadaria
    for i in range(4):
        k.box((0, -D / 2 - 1.6 - i * 0.4 + 0.6, 0.6 - i * 0.15 - 0.07), (7.0 + i * 0.4, 2.4, 0.15), 'stone_lt', bev=0.03)
    # torreões nos cantos da frente
    for s in (-1, 1):
        cx, cy = s * (W / 2 + 0.2), -D / 2 - 0.2
        stone_course_ring(k, cx, cy, 1.3, 0.6, z0 + 1.0, m='stone_lt', alt='stone', ch=(0.4, 0.55))
        k.lathe((cx, cy, z0 + 0.9), [(1.35, 0), (1.6, 0.25), (1.6, 0.4)], 'stone', n=20)
        cone_roof(k, cx, cy, z0 + 1.35, 1.75, 4.0, rows=10, n=24)
        k.cyl((cx, cy, z0 + 5.2), 0.05, 1.2, 'gold', n=6)
        for i in range(3):
            k.push((cx, cy, 0), rz=-90 * s + (i - 1) * 50)
            k.box((1.32, 0, z0 - 1.8), (0.1, 0.35, 0.9), 'glass', bev=0)
            k.pop()
    # estandartes na fachada
    for x in (-3.5, 3.5):
        banner(k, x, -D / 2 - 0.2, z0 - 0.4, w=1.2, h=3.4, cloth='cloth_b' if x < 0 else 'cloth_r')
    for s in (-1, 1):
        brazier(k, s * 4.2, -D / 2 - 2.4, s=0.9)
    k.meta_size = (W, D)
    return k


def estabulo4():
    """Estábulo: celeiro de tábuas sobre base de pedra, baias abertas na frente, palheiro com feno e telhado de telha."""
    k = Kit('Estabulo4', 701)
    W, D, H = 11.0, 7.0, 3.4
    k.box((0, 0, 0.1), (W + 0.3, D + 0.3, 0.2), 'stone_dk', bev=0.03)
    # paredes de fundo e laterais de tábuas
    for side in (1, 2, 3):
        L = _face(k, W, D, side)
        stone_face(k, -L / 2, L / 2, 0.2, 0.9, m='stone2', alt='stone_dk', ch=(0.3, 0.4), bw=(0.5, 0.9), depth=0.25)
        k.box((0, 0.06, 0.9 + (H - 0.9) / 2), (L, 0.12, H - 0.9), 'plank', bev=0)
        n = int(L / 0.32)
        for i in range(n):
            k.box((-L / 2 + (i + 0.5) * L / n, -0.01, 0.9 + (H - 0.9) / 2), (L / n - 0.02, 0.05, H - 0.92), 'plank', bev=0.008, var=k.tone(0.1))
        for x in (-L / 2 + 0.1, 0.0, L / 2 - 0.1):
            k.box((x, -0.05, H / 2 + 0.2), (0.2, 0.12, H - 0.2), 'timber', bev=0.015)
        k.pop()
    # frente: postes, baias com meia-porta
    for i in range(5):
        x = -W / 2 + 0.15 + i * (W - 0.3) / 4
        k.box((x, -D / 2 + 0.1, H / 2 + 0.1), (0.24, 0.24, H), 'timber', bev=0.02)
    for i in range(4):
        x = -W / 2 + 0.15 + (i + 0.5) * (W - 0.3) / 4
        k.box((x, -D / 2 + 0.1, 0.65), (2.4, 0.1, 1.1), 'plank', bev=0.015, var=k.tone(0.08))
        k.seg((x - 1.15, -D / 2 + 0.05, 0.15), (x + 1.15, -D / 2 + 0.05, 1.15), 0.1, 0.06, 'timber')
        k.box((x, D / 2 - 0.9, 0.5), (0.9, 0.5, 0.35), 'wood', bev=0.02)   # cocho
        for j in range(6):
            k.box((x + k.rnd.uniform(-1, 1), k.rnd.uniform(-2.5, 1.5), 0.25), (0.6, 0.5, 0.1), 'hay', bev=0.03, rz=k.rnd.uniform(0, 90))
    k.box((0, -D / 2 + 0.1, H + 0.05), (W, 0.25, 0.25), 'timber', bev=0.02)
    k.box((0, 0, 0.22), (W - 0.2, D - 0.2, 0.04), 'soil', bev=0)
    # palheiro (sótão) e telhado
    zr = gable_roof(k, W, D, H + 0.2, 3.4, over=0.6, gable_over=0.35, kind='clay', end_m='plank', timber_gable=True)
    k.push((W / 2 + 0.01, 0, 0), rz=90)
    k.box((0, -0.1, H + 1.3), (1.4, 0.1, 1.3), 'plank', bev=0.02)
    k.box((0, -0.12, H + 1.3), (1.2, 0.08, 1.1), 'hay', bev=0.02)
    k.pop()
    # fardos de feno empilhados na lateral
    for (x, y, z) in ((-W / 2 - 0.9, -1.5, 0), (-W / 2 - 0.9, -0.3, 0), (-W / 2 - 0.9, 0.9, 0), (-W / 2 - 0.9, -0.9, 0.62), (-W / 2 - 0.9, 0.3, 0.62)):
        k.box((x, y, z + 0.31), (1.0, 1.15, 0.6), 'hay', bev=0.06, var=k.tone(0.08))
        k.box((x, y, z + 0.31), (1.02, 0.06, 0.62), 'rope', bev=0.01)
    k.meta_size = (W, D)
    return k


# ---------------------------------------------------------------------------------------------------- praça
def fonte4():
    """Fonte da Brasa: bacia octogonal de cantaria com degraus, água, bacia alta em pedestal com bicas,
    coluna com quatro grifos estilizados, braseiro no topo e a gema de brasa (filho 'Gem') flutuando acima.
    Raio externo 4,0 m (obstáculo do servidor)."""
    k = Kit('Fonte4', 801)
    R = 3.9
    n = 8
    def octo(r, z0, z1, m, bev=0.05, inset=0.0):
        for i in range(n):
            a = (i + 0.5) * 360 / n
            side = 2 * r * math.tan(math.pi / n)
            k.push((0, 0, 0), rz=a + 90)
            k.box((0, -(r - 0.22), (z0 + z1) / 2), (side + 0.02, 0.44, z1 - z0), m, bev=bev, var=k.tone(0.06))
            k.pop()
    # degrau e bacia
    octo(R + 0.45, 0, 0.18, 'stone_dk')
    k.cyl((0, 0, 0), R + 0.3, 0.18, 'stone_dk', n=8)
    octo(R, 0.18, 0.95, 'stone_lt')
    for i in range(n):
        a = (i + 0.5) * 360 / n
        side = 2 * R * math.tan(math.pi / n)
        k.push((0, 0, 0), rz=a + 90)
        k.box((0, -(R - 0.2), 1.0), (side + 0.1, 0.55, 0.12), 'stone', bev=0.03)
        k.box((0, -(R + 0.02), 0.55), (side * 0.55, 0.06, 0.4), 'stone', bev=0.02)   # painel em relevo
        k.box((side / 2, -(R - 0.2), 0.6), (0.4, 0.62, 1.2), 'stone_lt', bev=0.04)   # pilarete no canto
        k.box((side / 2, -(R - 0.2), 1.25), (0.48, 0.7, 0.12), 'stone', bev=0.03)
        k.pop()
    k.cyl((0, 0, 0.18), R - 0.3, 0.62, 'water', n=24)
    k.cyl((0, 0, 0.8), R - 0.35, 0.02, 'water_lt', n=24)
    # pedestal e bacia alta
    k.lathe((0, 0, 0.8), [(1.1, 0), (0.95, 0.25), (0.7, 0.4), (0.6, 1.4), (0.8, 1.55), (1.9, 1.75), (2.1, 1.95), (2.1, 2.1), (1.95, 2.12)], 'stone_lt', n=24)
    k.cyl((0, 0, 2.6), 1.9, 0.02, 'water_lt', n=24)
    k.lathe((0, 0, 0.8), [(1.85, 2.1 - 0.8 + 0.8), (1.85, 2.15)], 'water', n=24) if False else None
    k.cyl((0, 0, 2.55), 1.88, 0.06, 'water', n=24)
    for i in range(8):
        a = i * 45
        k.push((0, 0, 0), rz=a)
        k.box((2.05, 0, 2.62), (0.35, 0.2, 0.12), 'bronze', bev=0.02)
        # fio d'água caindo na bacia de baixo
        k.box((2.35, 0, 1.75), (0.08, 0.12, 1.65), 'water_lt', bev=0)
        k.pop()
    # coluna com grifos
    k.lathe((0, 0, 2.6), [(0.55, 0), (0.45, 0.2), (0.4, 2.4), (0.55, 2.6), (0.75, 2.75), (0.75, 2.95)], 'stone', n=16)
    for i in range(4):
        k.push((0, 0, 2.6), rz=45 + i * 90)
        k.box((0.55, 0, 1.0), (0.5, 0.35, 0.9), 'stone_lt', bev=0.05, top=(0.7, 0.7))
        k.box((0.85, 0, 1.25), (0.35, 0.25, 0.3), 'stone_lt', bev=0.04)          # cabeça
        k.box((1.05, 0, 1.2), (0.2, 0.12, 0.12), 'stone', bev=0.02, top=(0.4, 0.6))  # bico
        k.box((0.45, 0, 1.55), (0.6, 0.12, 0.6), 'stone', bev=0.03, ry=-30)     # asa
        k.pop()
    # braseiro de bronze no topo
    brazier(k, 0, 0, z=5.55, s=0.8, base=False)
    # gema (filho que gira)
    g = Kit('Gem', 802)
    g.box((0, 0, 0), (0.55, 0.55, 0.55), 'rune', bev=0.06, rz=45, rx=35)
    g.box((0, 0, 0), (0.36, 0.36, 0.9), 'ember', bev=0.04, rz=20)
    k.child('Gem', g, (0, 0, 7.0))
    return k


def praca4():
    """Piso da praça da fonte: anéis concêntricos de lajes (raio 15), faixa de pedra escura, rosa-dos-ventos
    de pedra avermelhada e meio-fio. Fica 3 cm acima do terreno."""
    k = Kit('Praca4', 811)
    rnd = k.rnd
    rings = [(4.6, 5.6, 'slab_rd'), (5.6, 7.4, 'slab'), (7.4, 7.9, 'stone_dk'), (7.9, 10.8, 'slab'), (10.8, 11.3, 'slab_rd'),
             (11.3, 14.4, 'slab2'), (14.4, 15.0, 'stone_dk')]
    for (r0, r1, m) in rings:
        rows = max(1, round((r1 - r0) / 0.95))
        for j in range(rows):
            a0r = r0 + (r1 - r0) * j / rows
            a1r = r0 + (r1 - r0) * (j + 1) / rows
            rm = (a0r + a1r) / 2
            cnt = max(12, int(2 * math.pi * rm / rnd.uniform(0.85, 1.1)))
            off = rnd.random()
            for i in range(cnt):
                t0 = (i + off) / cnt * 2 * math.pi
                t1 = (i + 1 + off) / cnt * 2 * math.pi
                g = 0.022
                ta, tb = t0 + g / rm, t1 - g / rm
                ra, rb = a0r + g, a1r - g
                h = 0.05 + rnd.uniform(-0.008, 0.008)
                mat = m if not (m.startswith('slab') and rnd.random() < 0.12) else rnd.choice(['slab', 'slab2', 'stone2'])
                pts = [(math.cos(ta) * ra, math.sin(ta) * ra), (math.cos(tb) * ra, math.sin(tb) * ra),
                       (math.cos(tb) * rb, math.sin(tb) * rb), (math.cos(ta) * rb, math.sin(ta) * rb)]
                bot = [k.bm.verts.new(k.M @ Vector((x, y, 0.0))) for (x, y) in pts]
                top = [k.bm.verts.new(k.M @ Vector((x * 0.995, y * 0.995, h))) for (x, y) in pts]
                fs = [k.bm.faces.new(top)]
                for q in range(4):
                    qq = (q + 1) % 4
                    fs.append(k.bm.faces.new((bot[q], bot[qq], top[qq], top[q])))
                k._paint(fs, mat, k.tone(0.1))
    # rosa-dos-ventos (8 pontas) sob a fonte não aparece; desenho nas quatro avenidas, entre 5,6 e 7,4
    for i in range(4):
        a = i * 90
        k.push((0, 0, 0.055), rz=a)
        k.prism([(5.7, 0.0), (7.3, 0.0), (6.5, 0.0)], 0, 0, 'slab_rd') if False else None
        pts = [(5.75, 0), (6.5, 0.35), (7.35, 0), (6.5, -0.35)]
        f = k.poly([(x, y, 0.0) for (x, y) in pts], 'stone_dk')
        k.pop()
    # rejunte (base escura)
    k.cyl((0, 0, -0.02), 15.0, 0.04, 'mortar', n=48)
    return k


# ---------------------------------------------------------------------------------------------------- props
def barraca4(cloth='cloth_r', name='Barraca4R', seed=901):
    """Barraca de feira: estrutura de madeira, toldo listrado em duas águas, balcão com mercadorias e caixotes."""
    k = Kit(name, seed)
    W, D, H = 3.2, 2.2, 2.3
    for (x, y) in ((-W / 2, -D / 2), (W / 2, -D / 2), (-W / 2, D / 2), (W / 2, D / 2)):
        k.box((x, y, (H + (0.4 if y > 0 else 0)) / 2), (0.14, 0.14, H + (0.4 if y > 0 else 0)), 'timber', bev=0.015)
    for y in (-D / 2, D / 2):
        k.box((0, y, H + (0.4 if y > 0 else 0)), (W + 0.2, 0.12, 0.12), 'timber', bev=0.012)
    # toldo listrado (uma água, mais alto atrás) com franja
    n = 8
    for i in range(n):
        x = -W / 2 - 0.15 + (i + 0.5) * (W + 0.3) / n
        m = cloth if i % 2 == 0 else 'cloth_w'
        k.push((x, 0, H + 0.25), rx=-10)
        k.box((0, 0, 0), ((W + 0.3) / n + 0.005, D + 0.7, 0.04), m, bev=0)
        k.pop()
        k.box((x, -D / 2 - 0.35, H + 0.06), ((W + 0.3) / n, 0.04, 0.3), m, bev=0)
    # balcão
    k.box((0, -D / 2 + 0.3, 0.5), (W - 0.2, 0.6, 1.0), 'plank', bev=0.02)
    for i in range(8):
        k.box((-W / 2 + 0.25 + i * (W - 0.5) / 7, -D / 2 - 0.0, 0.5), (0.3, 0.04, 0.95), 'plank', bev=0.006, var=k.tone(0.1))
    k.box((0, -D / 2 + 0.3, 1.03), (W, 0.75, 0.07), 'wood', bev=0.015)
    # mercadorias: cestos de frutas, rolos de pano, potes
    goods = [('flower_r', 0.12), ('flower_y', 0.12), ('leaf2', 0.13), ('hay', 0.12)]
    for i in range(4):
        x = -1.2 + i * 0.8
        k.cyl((x, -D / 2 + 0.3, 1.06), 0.26, 0.16, 'rope', n=10, r2=0.3)
        m, r = goods[i]
        for j in range(5):
            k.box((x + k.rnd.uniform(-0.14, 0.14), -D / 2 + 0.3 + k.rnd.uniform(-0.12, 0.12), 1.26), (r, r, r), m, bev=0.04, rz=k.rnd.uniform(0, 90))
    for i in range(3):
        k.cyl((-1.1 + i * 0.5, D / 2 - 0.4, 0.0), 0.22, 0.5 + 0.1 * i, 'bronze' if i == 1 else 'wood_lt', n=10, r2=0.15)
    k.box((1.2, D / 2 - 0.3, 0.3), (0.6, 0.55, 0.6), 'plank', bev=0.03)
    k.box((1.2, D / 2 - 0.3, 0.85), (0.5, 0.45, 0.5), 'plank', bev=0.03, rz=10)
    k.cyl((-W / 2 - 0.35, -D / 2 + 0.2, 0), 0.25, 0.7, 'wood', n=10, r2=0.22)
    return k


def barraca4r():
    return barraca4('cloth_r', 'Barraca4R', 901)


def barraca4b():
    return barraca4('cloth_b', 'Barraca4B', 902)


def barraca4y():
    return barraca4('cloth_y', 'Barraca4Y', 903)


def poste4():
    """Poste de ferro com lampião (luz a 3,3 m, 0,8 m à frente)."""
    k = Kit('Poste4', 911)
    lantern_post(k, 0, 0, h=3.8, arm=0.8)
    return k


def braseiro4():
    k = Kit('Braseiro4', 912)
    brazier(k, 0, 0, s=1.0)
    return k


def estandarte4():
    """Mastro com estandarte duplo (sem base de pedra para caber na rua)."""
    k = Kit('Estandarte4', 913)
    k.box((0, 0, 0.2), (0.6, 0.6, 0.4), 'stone_dk', bev=0.04)
    k.cyl((0, 0, 0.4), 0.08, 5.2, 'timber', n=8, r2=0.06)
    k.box((0, 0, 4.9), (1.6, 0.08, 0.08), 'iron', bev=0)
    banner(k, 0, 0, 4.85, w=1.3, h=2.8, cloth='cloth_r')
    k.lathe((0, 0, 5.55), [(0.1, 0), (0.12, 0.08), (0.0, 0.35)], 'gold', n=8)
    return k


def poco4():
    """Poço com mureta de pedra, telhadinho de telha, sarilho e balde."""
    k = Kit('Poco4', 921)
    stone_course_ring(k, 0, 0, 0.95, 0.0, 0.9, m='stone', alt='stone2', ch=(0.28, 0.32), thick=0.18)
    k.lathe((0, 0, 0.9), [(1.15, 0), (1.15, 0.1), (0.7, 0.1)], 'stone_lt', n=16)
    k.cyl((0, 0, 0.3), 0.72, 0.5, 'water', n=16)
    for s in (-1, 1):
        k.box((s * 1.0, 0, 1.55), (0.16, 0.16, 1.3), 'timber', bev=0.015)
    k.cyl((-1.1, 0, 1.75), 0.1, 2.2, 'wood', n=8, ry=90)
    k.cyl((-0.25, 0, 1.75), 0.14, 0.5, 'rope', n=8, ry=90)
    k.box((1.25, 0.15, 1.75), (0.06, 0.4, 0.06), 'iron', bev=0)
    k.seg((0.0, 0, 1.7), (0.0, 0, 1.1), 0.02, 0.02, 'rope')
    k.cyl((0, 0, 0.95), 0.17, 0.25, 'wood', n=8, r2=0.2)
    k.push((0, 0, 0), rz=0)
    gable_roof(k, 2.4, 1.4, 2.2, 0.7, over=0.25, gable_over=0.1, kind='clay', gable_wall=False)
    k.pop()
    return k


def banco4():
    k = Kit('Banco4', 931)
    for s in (-1, 1):
        k.box((s * 0.75, 0, 0.22), (0.3, 0.45, 0.44), 'stone', bev=0.04)
    k.box((0, 0, 0.5), (1.9, 0.5, 0.12), 'stone_lt', bev=0.03)
    return k


def canteiro4():
    """Canteiro de pedra com arbusto e flores (2,4 × 1,2)."""
    k = Kit('Canteiro4', 932)
    for (x, y, sx, sy) in ((0, -0.55, 2.4, 0.18), (0, 0.55, 2.4, 0.18), (-1.12, 0, 0.18, 1.1), (1.12, 0, 0.18, 1.1)):
        k.box((x, y, 0.25), (sx, sy, 0.5), 'stone', bev=0.03)
        k.box((x, y, 0.52), (sx + 0.06, sy + 0.06, 0.06), 'stone_lt', bev=0.02)
    k.box((0, 0, 0.4), (2.1, 0.95, 0.1), 'soil', bev=0)
    for i in range(5):
        x = -0.85 + i * 0.42
        k.box((x, k.rnd.uniform(-0.15, 0.15), 0.62), (0.42, 0.42, 0.4), k.rnd.choice(['leaf', 'leaf2']), bev=0.1, rz=k.rnd.uniform(0, 90))
        for j in range(3):
            k.box((x + k.rnd.uniform(-0.2, 0.2), k.rnd.uniform(-0.35, 0.35), 0.85), (0.1, 0.1, 0.08), k.rnd.choice(['flower_r', 'flower_y', 'flower_v']), bev=0.02)
    return k


def caixotes4():
    """Pilha de caixotes, sacos e barril (carga de mercador)."""
    k = Kit('Caixotes4', 941)
    for (x, y, z, s, r) in ((0, 0, 0, 0.7, 5), (0.75, 0.05, 0, 0.65, -8), (0.35, 0.0, 0.7, 0.6, 20), (-0.6, 0.4, 0, 0.55, 12)):
        k.box((x, y, z + s / 2), (s, s, s), 'plank', bev=0.03, rz=r)
        k.push((x, y, z + s / 2), rz=r)
        for sx in (-1, 1):
            k.box((sx * (s / 2 + 0.005), 0, 0), (0.02, s * 0.9, 0.08), 'wood', bev=0)
            k.box((0, sx * (s / 2 + 0.005), 0), (s * 0.9, 0.02, 0.08), 'wood', bev=0)
        k.pop()
    for (x, y) in ((-0.5, -0.45), (-0.95, -0.1)):
        k.box((x, y, 0.25), (0.45, 0.35, 0.5), 'cloth_w', bev=0.12, top=(0.7, 0.8))
    k.cyl((1.0, -0.65, 0), 0.3, 0.85, 'wood', n=10, r2=0.28)
    for zz in (0.12, 0.7):
        k.cyl((1.0, -0.65, zz), 0.315, 0.05, 'iron', n=10)
    return k


def barris4():
    k = Kit('Barris4', 942)
    for (x, y, z) in ((0, 0, 0), (0.68, 0, 0), (0.34, 0.0, 0.62)):
        k.cyl((x - 0.45, y, z + 0.32), 0.32, 0.9, 'wood', n=10, ry=90)
        for xx in (-0.3, 0.3):
            k.cyl((x - 0.45 + xx + 0.45 - 0.45, y, z + 0.32), 0.335, 0.05, 'iron', n=10, ry=90) if False else None
    for (x, y) in ((-0.9, 0.7), (-0.3, 0.75)):
        k.cyl((x, y, 0), 0.3, 0.85, 'wood', n=10, r2=0.27)
        for zz in (0.12, 0.7):
            k.cyl((x, y, zz), 0.315, 0.05, 'iron', n=10)
    return k


def obelisco4():
    """Marco da Brasa: obelisco de pedra sobre plinto em degraus com runas acesas e chama no topo."""
    k = Kit('Obelisco4', 951)
    for i, (w, h) in enumerate(((2.6, 0.3), (2.1, 0.3), (1.6, 0.5))):
        z = sum(x[1] for x in ((2.6, 0.3), (2.1, 0.3), (1.6, 0.5))[:i])
        k.box((0, 0, z + h / 2), (w, w, h), 'stone_lt' if i < 2 else 'stone', bev=0.04)
    k.box((0, 0, 1.1 + 2.2), (0.9, 0.9, 4.4), 'stone_lt', bev=0.05, top=(0.62, 0.62))
    for i in range(4):
        k.push((0, 0, 0), rz=i * 90)
        for j in range(3):
            k.box((0, -0.44 + j * 0.035, 1.8 + j * 1.0), (0.18, 0.04, 0.5), 'rune', bev=0, ry=0)
        k.pop()
    k.box((0, 0, 5.6), (0.6, 0.6, 0.4), 'stone_lt', bev=0.03, top=(0.1, 0.1))
    k.lathe((0, 0, 5.75), [(0.18, 0), (0.1, 0.25), (0.0, 0.5)], 'fire', n=8)
    return k


# ---------------------------------------------------------------------------------------------------- registro e main
MODELS = [muralha4, muralha4_hera, torre4, portao4, casa4a, casa4b, casa4c, casa4d, taverna4, loja4, ferraria4, capela4,
          guilda4, estabulo4, fonte4, praca4, barraca4r, barraca4b, barraca4y, poste4, braseiro4, estandarte4, poco4,
          banco4, canteiro4, caixotes4, barris4, obelisco4]


def main(argv):
    out = None
    only = None
    ao = True
    blend = None
    if '--out' in argv:
        out = argv[argv.index('--out') + 1]
    if '--only' in argv:
        only = set(argv[argv.index('--only') + 1].lower().split(','))
    if '--no-ao' in argv:
        ao = False
    if '--blend' in argv:
        blend = argv[argv.index('--blend') + 1]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new('Cidade4')
    bpy.context.scene.collection.children.link(coll)
    report = []
    x = 0.0
    for fn in MODELS:
        if only and fn.__name__ not in only:
            continue
        ob = build_object(fn, coll, ao=ao)
        tris = tri_count(ob)
        if out:
            export_fbx(ob, os.path.join(out, 'Environment', ob.name + '.fbx'))
        ob.location.x = x
        x += ob.dimensions.x + 3
        report.append((ob.name, tris, tuple(round(v, 2) for v in ob.dimensions)))
        print(f'[city4] {ob.name}: {tris} tri, {report[-1][2]}', flush=True)
    if blend:
        bpy.ops.wm.save_as_mainfile(filepath=blend)
    print('[city4] total', sum(r[1] for r in report), 'tri em', len(report), 'modelos')
    return report


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:])
