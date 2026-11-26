# Kezor — armaduras v6: placas DURAS modeladas por parâmetros (não mais "corpo inflado").
# Cada placa é uma superfície (u, v) com silhueta própria (pontas, recortes, cava do braço), espessura real,
# chanfro arredondado nas quinas com normais endurecidas, friso dourado em tubo na borda, filete gravado por
# dentro da borda, emblemas e runas em relevo. Usa o namespace KZ (kezor_hero4 + kezor_armor5 já executados).
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

D2R = math.pi / 180.0
RES = 0.5           # densidade da grade (1 = máxima); o jogo pede ~35 mil triângulos para o conjunto

SETS6 = {
    # nome: (aço claro, aço escuro, ouro, runa, capa clara, capa escura, couro)
    'Runica': ('#c9d2db', '#161a20', '#e0ad4f', '#5fe0ff', '#8c1626', '#2a060b', '#3b2a20'),
}


def _g(x, s):
    return math.exp(-(x / s) ** 2)


def _sm(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def _lerp(a, b, t):
    return a + (b - a) * t


def ring(cx, cy, rx, ry, a_deg):
    """Ponto numa elipse em volta de um eixo vertical. 0° = frente (-Y), +90° = lado de fora (+X)."""
    a = a_deg * D2R
    return cx + rx * math.sin(a), cy - ry * math.cos(a)


# ================================================================ superfície paramétrica
class Shell:
    """Superfície em grade (u, v) ∈ [0,1]²; pos(u, v) -> Vector. u anda na horizontal, v sobe."""

    def __init__(self, name, pos, nu, nv, sgn=1, flip=False, thick=0.006, kind='steel'):
        nu, nv = max(3, int(round(nu * RES))), max(2, int(round(nv * RES)))
        self.name, self.pos, self.nu, self.nv = name, pos, nu, nv
        self.sgn, self.flip, self.thick, self.kind = sgn, flip, thick, kind
        self.tubes = []          # (lista de pontos, raio, fechado, tipo) — tipo: 'gold' | 'rune' | 'steel'

    def P(self, u, v):
        p = Vector(self.pos(u, v))
        p.x *= self.sgn
        return p

    def N(self, u, v):
        e = 1e-3
        du = self.P(min(1, u + e), v) - self.P(max(0, u - e), v)
        dv = self.P(u, min(1, v + e)) - self.P(u, max(0, v - e))
        n = du.cross(dv)
        if (self.sgn < 0) != self.flip:
            n = -n
        return n.normalized() if n.length > 1e-12 else Vector((0, 0, 1))

    # --- tubos decorativos
    def border(self, inset=0.0, r=None, kind='gold', sides='lrbt', steps=None):
        """Laço pela borda (inset em fração da grade). r=None: friso grosso no meio da espessura."""
        a, b = inset, 1 - inset
        pts = []
        nu, nv = steps or (self.nu, self.nv)
        seg = {
            'b': [(_lerp(a, b, i / nu), a) for i in range(nu)],
            'r': [(b, _lerp(a, b, j / nv)) for j in range(nv)],
            't': [(_lerp(b, a, i / nu), b) for i in range(nu)],
            'l': [(a, _lerp(b, a, j / nv)) for j in range(nv)],
        }
        order = 'brtl'
        if sides == 'lrbt':
            for s in order:
                pts += seg[s]
            closed = True
        else:
            # trechos abertos (ex.: só a borda de baixo)
            for s in order:
                if s in sides:
                    self.add_uv_tube(seg[s] + ([self._end(s, a, b)]), r, kind, False, inset == 0.0)
            return
        self.add_uv_tube(pts, r, kind, closed, inset == 0.0)

    @staticmethod
    def _end(s, a, b):
        return {'b': (b, a), 'r': (b, b), 't': (a, b), 'l': (a, a)}[s]

    def add_uv_tube(self, uvs, r, kind, closed, rim):
        out = []
        for (u, v) in uvs:
            p, n = self.P(u, v), self.N(u, v)
            if rim:
                out.append(p - n * (self.thick * 0.5))       # friso: no meio da espessura, cobre a quina
            else:
                out.append(p + n * ((r or 0.0016) * 0.45))   # filete/emblema: deitado sobre a placa
        rr = r if r is not None else (self.thick * 0.5 + 0.0021 if rim else 0.0016)
        self.tubes.append((out, rr, closed, kind))

    # --- malha
    def build(self):
        G = bpy.app.driver_namespace['KZ']
        G['_kill'](self.name)
        bm = bmesh.new()
        uvl = bm.loops.layers.uv.new('UVMap')
        grid = [[bm.verts.new(self.P(i / self.nu, j / self.nv)) for j in range(self.nv + 1)] for i in range(self.nu + 1)]
        for i in range(self.nu):
            for j in range(self.nv):
                q = [grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]]
                uv = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
                if (self.sgn < 0) != self.flip:
                    q.reverse()
                    uv.reverse()
                if len(set(q)) < 3:
                    continue
                try:
                    f = bm.faces.new(q)
                except ValueError:
                    continue
                for l, (a, b) in zip(f.loops, uv):
                    l[uvl].uv = (a / self.nu, b / self.nv)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        o = G['_link'](bpy.data.objects.new(self.name, me))
        o['kind'] = self.kind
        o['wu'] = (self.P(0, 0.5) - self.P(1, 0.5)).length + 1e-6
        o['wv'] = (self.P(0.5, 0) - self.P(0.5, 1)).length + 1e-6
        for gname in ('in', 'rim'):
            o.vertex_groups.new(name=gname)
        if self.thick > 0:
            m = o.modifiers.new('Esp', 'SOLIDIFY')
            m.thickness = self.thick
            m.offset = -1 if self.kind != 'cloth' else 0
            m.use_rim = True
            m.use_even_offset = True
            m.shell_vertex_group = 'in'
            m.rim_vertex_group = 'rim'
            G['_apply'](o, 'Esp')
            b = o.modifiers.new('Chanfro', 'BEVEL')
            b.width = min(0.0016, self.thick * 0.3)
            b.segments = 1
            b.limit_method = 'ANGLE'
            b.angle_limit = 40 * D2R
            b.harden_normals = False
            G['_apply'](o, 'Chanfro')
        for p in o.data.polygons:
            p.use_smooth = True
        return o


def tubes_obj(name, tubes, res=1):
    """Converte a lista de tubos numa malha (uma por tipo de cor guardada no atributo 'tk')."""
    G = bpy.app.driver_namespace['KZ']
    objs = []
    for kind in ('gold', 'rune', 'steel'):
        sel = [t for t in tubes if t[3] == kind]
        if not sel:
            continue
        nm = '%s_%s' % (name, kind)
        G['_kill'](nm)
        made = []
        for k, (pts, r, closed, _) in enumerate(sel):
            if len(pts) < 2:
                continue
            cu = bpy.data.curves.new(nm + str(k), 'CURVE')
            cu.dimensions = '3D'
            cu.bevel_depth = r
            cu.bevel_resolution = 0
            cu.resolution_u = 1
            cu.use_fill_caps = True
            sp = cu.splines.new('POLY')
            sp.points.add(len(pts) - 1)
            for i, p in enumerate(pts):
                sp.points[i].co = (p.x, p.y, p.z, 1.0)
            sp.use_cyclic_u = closed
            tmp = G['_link'](bpy.data.objects.new(nm + str(k) + '_c', cu))
            dg = bpy.context.evaluated_depsgraph_get()
            me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
            bpy.data.objects.remove(tmp, do_unlink=True)
            bpy.data.curves.remove(cu)
            ob = G['_link'](bpy.data.objects.new(nm + str(k), me))
            made.append(ob)
        if not made:
            continue
        ob = G['_join'](made, nm) if len(made) > 1 else made[0]
        ob.name = nm
        ob['kind'] = kind
        for p in ob.data.polygons:
            p.use_smooth = True
        objs.append(ob)
    return objs


# ================================================================ peças
def cuirass():
    """Peitoral (frente) com quilha central e peitorais em relevo; costas separadas; cava do braço recortada."""
    def rx(z):
        return 0.183 + 0.075 * _sm(1.04, 1.32, z) - 0.09 * _sm(1.38, 1.46, z)

    def ry(z):
        return 0.112 + 0.022 * _sm(1.04, 1.28, z) - 0.03 * _sm(1.38, 1.46, z)

    def front(u, v):
        a = _lerp(-104, 104, u)
        aa = abs(a)
        zb = 0.985 - 0.035 * _g(a, 22)                                  # ponta embaixo, no meio
        zt = 1.445 - 0.055 * _g(a, 30) - 0.17 * _sm(52, 90, aa)        # gola em V e cava do braço
        z = _lerp(zb, zt, v)
        r_add = 0.02 * _g(a, 5) * _sm(1.0, 1.08, z)                     # quilha
        r_add += 0.034 * _g(aa - 33, 19) * _g(z - 1.31, 0.07)           # peitorais
        r_add -= 0.008 * _g(aa - 33, 26) * _g(z - 1.215, 0.012)         # sulco sob o peito
        r_add += 0.01 * _g(aa - 17, 10) * (_g(z - 1.09, 0.025) + _g(z - 1.15, 0.025))   # gomos do abdome
        x, y = ring(0, 0.012, rx(z) + r_add * abs(math.sin(a * D2R)) * 0.4, ry(z) + r_add, a)
        return x, y, z

    def back(u, v):
        a = _lerp(76, 284, u)
        aa = abs(a - 180)
        zb = 0.985
        zt = 1.455 - 0.04 * _g(aa, 30) - 0.17 * _sm(52, 104, 180 - abs(a - 180) if False else (104 - aa))
        zt = 1.455 - 0.03 * _g(aa, 30) - 0.17 * _sm(60, 100, aa)
        z = _lerp(zb, zt, v)
        r_add = 0.012 * _g(aa - 40, 22) * _g(z - 1.33, 0.08)             # omoplatas
        x, y = ring(0, 0.012, rx(z) - 0.004, ry(z) + 0.004 + r_add, a)
        return x, y, z

    f = Shell('KZ6_Peito', front, 64, 34, thick=0.007)
    f.border()
    f.border(inset=0.055, r=0.0017)
    # emblema: losango com asas e runa central acesa
    emb = []
    for t in range(0, 37):
        k = t / 36 * 2 * math.pi
        emb.append((0.5 + 0.055 * math.sin(k) ** 3, 0.70 + 0.10 * math.cos(k)))
    f.add_uv_tube(emb, 0.0022, 'gold', True, False)
    for s in (-1, 1):
        wing = [(0.5 + s * (0.06 + 0.17 * t), 0.74 + 0.10 * math.sin(t * 2.6) - 0.10 * t) for t in [i / 14 for i in range(15)]]
        f.add_uv_tube(wing, 0.0019, 'gold', False, False)
        wing2 = [(0.5 + s * (0.06 + 0.13 * t), 0.69 + 0.06 * math.sin(t * 2.8) - 0.10 * t) for t in [i / 12 for i in range(13)]]
        f.add_uv_tube(wing2, 0.0016, 'gold', False, False)
    f.add_uv_tube([(0.5, 0.62), (0.5, 0.78)], 0.0024, 'rune', False, False)
    f.add_uv_tube([(0.5, 0.18), (0.5, 0.55)], 0.0018, 'rune', False, False)
    def gambeson(u, v):                                  # gibão de couro por baixo (cobre a axila)
        a = _lerp(-180, 180, u)
        z = _lerp(0.98, 1.43, v)
        x, y = ring(0, 0.012, rx(z) - 0.008, ry(z) - 0.008, a)
        return x, y, z
    gb = Shell('KZ6_Gibao', gambeson, 40, 10, thick=0.004, kind='leather')
    b = Shell('KZ6_Costas', back, 56, 30, thick=0.007)
    b.border()
    b.border(inset=0.06, r=0.0016)
    return [f, b, gb]


def fauld_and_tassets():
    """Faixas do abdome (lâminas sobrepostas), cinturão com fivela e escarcelas (saiote de placas) pontudas."""
    out = []
    for i, (z0, z1) in enumerate(((0.955, 1.02), (0.915, 0.975))):
        def lame(u, v, z0=z0, z1=z1, i=i):
            a = _lerp(-118, 118, u)
            z = _lerp(z0 - 0.012 * _g(a, 30), z1, v)
            x, y = ring(0, 0.012, 0.196 + 0.004 * i + 0.01 * (1 - v), 0.128 + 0.004 * i + 0.01 * (1 - v), a)
            return x, y, z
        s = Shell('KZ6_Faixa%d' % i, lame, 56, 5, thick=0.006)
        s.border(sides='b', r=None)
        out.append(s)

    def belt(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(0.885, 0.93, v)
        x, y = ring(0, 0.012, 0.205, 0.137, a)
        return x, y, z
    bt = Shell('KZ6_Cinto', belt, 72, 3, thick=0.008, kind='leather')
    bt.border(inset=0.0, r=0.0022, kind='gold', sides='bt')
    out.append(bt)

    def buckle(u, v):
        a = _lerp(-13, 13, u)
        z = _lerp(0.875, 0.94, v)
        bulge = 0.012 * math.sin(u * math.pi) * math.sin(v * math.pi)
        x, y = ring(0, 0.012, 0.21, 0.148 + bulge, a)
        return x, y, z
    bk = Shell('KZ6_Fivela', buckle, 12, 8, thick=0.006, kind='goldplate')
    bk.add_uv_tube([(0.5, 0.2), (0.5, 0.8)], 0.0026, 'rune', False, False)
    out.append(bk)

    # escarcelas: 2 frontais grandes (3 lâminas cada) + 2 laterais
    for side in (1, -1):
        for k, (ac, w, nl, z_top, flare) in enumerate(((27, 25, 3, 0.895, 0.055), (78, 20, 2, 0.89, 0.07))):
            for j in range(nl):
                zt = z_top - 0.075 * j
                zb = zt - 0.105

                def tas(u, v, ac=ac, w=w, zt=zt, zb=zb, flare=flare, j=j):
                    a = ac + _lerp(-w, w, u)
                    tip = 0.03 * (1 - abs(u * 2 - 1)) if j == nl - 1 else 0.012 * (1 - abs(u * 2 - 1))
                    z = _lerp(zb - tip, zt, v)
                    out_r = flare * (1 - v) + 0.006 * j
                    x, y = ring(0, 0.012, 0.212 + out_r, 0.142 + out_r, a)
                    return x, y, z
                s = Shell('KZ6_Escarcela%s%d%d' % ('L' if side > 0 else 'R', k, j), tas, 16, 7, sgn=side, thick=0.006)
                s.border()
                if j == nl - 1:
                    s.add_uv_tube([(0.5, 0.15), (0.5, 0.75)], 0.0018, 'rune', False, False)
                out.append(s)
    return out


def gorget():
    out = []
    for i, (z0, z1, r0, r1) in enumerate(((1.40, 1.49, 0.215, 0.15), (1.47, 1.53, 0.15, 0.1))):
        def g(u, v, z0=z0, z1=z1, r0=r0, r1=r1):
            a = _lerp(-180, 180, u)
            z = _lerp(z0, z1, v) - 0.012 * _g(a, 35)
            r = _lerp(r0, r1, v)
            x, y = ring(0, 0.018, r, r * 0.86, a)
            return x, y, z
        s = Shell('KZ6_Gorjal%d' % i, g, 56, 4, thick=0.006)
        s.border(sides='b' if i == 0 else 'bt')
        out.append(s)
    return out


def pauldrons(side):
    """Ombreira grande: cúpula com crista + 3 lâminas que abrem para fora, tudo inclinado para o lado."""
    sgn = 1 if side == 'L' else -1
    S = Vector((0.30, 0.004, 1.405))
    tilt = Matrix.Rotation(-22 * D2R, 3, 'Y')        # em coordenadas do lado esquerdo; o espelho vem pelo sgn
    SC = HERO_PAULDRON
    out = []

    def place(local):
        return tuple(S + tilt @ (Vector(local) * SC))

    def dome(u, v):
        a = _lerp(-62, 242, u)
        th = _lerp(1.32, 0.0, v)                        # v sobe até o topo
        R = 0.152
        crest = 0.022 * _g(a - 90, 9) * _sm(0.2, 0.9, v)    # crista no meridiano de fora
        r = R * math.sin(th) + crest * math.sin(th)
        x, y = ring(0, 0, r * 1.0, r * 0.92, a)
        return place((x, y, R * 0.9 * math.cos(th) + crest * 0.6 * math.cos(th) - 0.035))

    d = Shell('KZ6_Ombro%s_Cupula' % side, dome, 48, 18, sgn=sgn, thick=0.007)
    d.border()
    d.border(inset=0.08, r=0.0017, sides='b')
    d.add_uv_tube([(0.5 + (90 - 90) / 304, 0.25), (0.5, 0.95)], 0.0022, 'rune', False, False)
    out.append(d)

    def fin(u, v):                                       # crista dourada em lâmina sobre a cúpula
        th = _lerp(1.05, 0.05, u)
        R = 0.152
        base = Vector((R * math.sin(th), 0.0, R * 0.9 * math.cos(th) - 0.035))
        nrm = Vector((math.sin(th), 0.0, math.cos(th) * 0.9)).normalized()
        h = 0.034 * math.sin(math.pi * u) ** 0.6 + 0.022 * _g(u - 0.78, 0.09)
        p = base + nrm * (h * v + 0.004)
        return place((p.x, p.y, p.z))
    fs = Shell('KZ6_Ombro%s_Crista' % side, fin, 22, 4, sgn=sgn, thick=0.007, kind='goldplate')
    out.append(fs)
    for j in range(3):
        def lame(u, v, j=j):
            a = _lerp(-55, 235, u)
            ztop = -0.005 - 0.046 * j
            point = 0.03 * _g(a - 90, 45)               # ponta virada para fora/baixo
            z = _lerp(ztop - 0.062 - point, ztop, v)
            r = 0.122 + 0.012 * j + 0.03 * (1 - v)
            x, y = ring(0, 0, r, r * 0.9, a)
            return place((x, y, z))
        s = Shell('KZ6_Ombro%s_Lamina%d' % (side, j), lame, 44, 6, sgn=sgn, thick=0.006)
        s.border()
        out.append(s)
    # pontas douradas saindo da cúpula (silhueta reconhecível de longe)
    R = 0.152
    for k, (a, th, L, r0) in enumerate(((90, 0.42, 0.11, 0.022), (90, 0.82, 0.075, 0.017), (35, 0.7, 0.05, 0.013), (145, 0.7, 0.05, 0.013))):
        sa, ca = math.sin(a * D2R), math.cos(a * D2R)
        loc = Vector((R * math.sin(th) * sa, -R * 0.92 * math.sin(th) * ca, R * 0.9 * math.cos(th) - 0.035))
        nrm = Vector((math.sin(th) * sa, -math.sin(th) * ca, math.cos(th))).normalized()
        axis = (nrm + Vector((0.25, 0, 0.9))).normalized()
        out.append(spike('KZ6_Ombro%s_Ponta%d' % (side, k), S + tilt @ (loc * SC), tilt @ axis, r0, L, sgn))
    return out


HERO_PAULDRON = 1.3


def spike(name, base, axis, r0, L, sgn=1, kind='goldplate'):
    """Ponta cônica levemente curvada para cima (coordenadas do lado esquerdo)."""
    axis = Vector(axis).normalized()
    t1 = axis.cross(Vector((0, 1, 0)))
    if t1.length < 1e-3:
        t1 = axis.cross(Vector((1, 0, 0)))
    t1.normalize()
    t2 = axis.cross(t1).normalized()
    up = Vector((0, 0, 1))

    def pos(u, v):
        a = u * 2 * math.pi
        r = r0 * (1 - v) ** 1.25 + 0.0006
        p = Vector(base) + axis * (L * v) + up * (0.25 * L * v * v) + (t1 * math.cos(a) + t2 * math.sin(a)) * r
        return p.x, p.y, p.z
    sh = Shell(name, pos, 14, 8, sgn=sgn, thick=0.0, kind=kind)
    return sh


def arm(side):
    sgn = 1 if side == 'L' else -1
    cx, cy = 0.305, -0.004
    out = []

    def sleeve(u, v):                                    # manga de couro por baixo das placas
        a = _lerp(-180, 180, u)
        z = _lerp(1.0, 1.36, v)
        r = 0.068 + 0.008 * v
        x, y = ring(cx, cy, r, r, a)
        return x, y, z
    out.append(Shell('KZ6_Manga' + side, sleeve, 28, 8, sgn=sgn, thick=0.004, kind='leather'))

    def rere(u, v):                                      # braço (acima do cotovelo)
        a = _lerp(-80, 260, u)
        z = _lerp(1.115, 1.275, v)
        r = 0.082 + 0.006 * v
        x, y = ring(cx, cy, r, r, a)
        return x, y, z
    s = Shell('KZ6_Braco' + side, rere, 40, 8, sgn=sgn, thick=0.005)
    s.border(sides='bt')
    out.append(s)

    def couter(u, v):                                    # cotoveleira em concha + aba lateral
        a = _lerp(70, 250, u)
        z = _lerp(1.02, 1.15, v)
        bul = 0.028 * math.sin(v * math.pi) * _g(a - 165, 40)
        wing = 0.045 * _g(a - 95, 22) * math.sin(v * math.pi)
        r = 0.076 + bul + wing
        x, y = ring(cx, cy, r, r, a)
        return x, y, z
    s = Shell('KZ6_Cotovelo' + side, couter, 30, 12, sgn=sgn, thick=0.006)
    s.border()
    out.append(s)

    def vamb(u, v):                                      # braçal com quilha e boca larga no punho
        a = _lerp(-80, 260, u)
        z = _lerp(0.815, 1.04, v)
        r = 0.064 + 0.01 * v + 0.034 * _sm(0.22, 0.0, v)
        r += 0.012 * _g(a - 60, 9)
        x, y = ring(cx, cy, r, r * 0.97, a)
        return x, y, z
    s = Shell('KZ6_Bracal' + side, vamb, 44, 14, sgn=sgn, thick=0.006)
    s.border(sides='bt')
    s.border(inset=0.12, r=0.0015, sides='bt')
    s.add_uv_tube([((60 + 80) / 340, 0.25), ((60 + 80) / 340, 0.8)], 0.0018, 'rune', False, False)
    out.append(s)

    def hand(u, v):                                      # manopla: casca da mão
        a = _lerp(-180, 180, u)
        z = _lerp(0.655, 0.80, v)
        rx = 0.044 + 0.007 * _g(v - 0.45, 0.3)
        ry = 0.068 * (0.75 + 0.25 * _sm(0.0, 0.5, v)) + 0.008 * _sm(0.8, 1.0, v)
        x, y = ring(0.307, -0.022 + 0.01 * v, rx, ry, a)
        return x, y, z
    s = Shell('KZ6_Mao' + side, hand, 32, 10, sgn=sgn, thick=0.004, kind='leather')
    out.append(s)
    for j in range(3):                                   # lâminas no dorso da mão
        def hl(u, v, j=j):
            a = _lerp(25, 155, u)
            z = _lerp(0.735 - 0.032 * j, 0.775 - 0.032 * j, v)
            x, y = ring(0.307, -0.017, 0.055 - 0.002 * j, 0.077, a)
            return x, y, z
        s = Shell('KZ6_LamMao%s%d' % (side, j), hl, 14, 3, sgn=sgn, thick=0.004)
        s.border(sides='b')
        out.append(s)
    return out


def leg(side):
    sgn = 1 if side == 'L' else -1
    out = []

    def cuisse(u, v):                                    # coxote (frente e lados da coxa)
        a = _lerp(-95, 115, u)
        zt = 0.86 - 0.04 * _g(a + 40, 40)
        z = _lerp(0.605, zt, v)
        r = 0.122 + 0.004 * v
        r += 0.006 * _g(a - 10, 7)
        x, y = ring(0.11, -0.002, r, r * 1.04, a)
        return x, y, z
    s = Shell('KZ6_Coxote' + side, cuisse, 40, 14, sgn=sgn, thick=0.006)
    s.border()
    s.border(inset=0.09, r=0.0015)
    out.append(s)

    def poleyn(u, v):                                    # joelheira em concha com aba lateral em leque
        a = _lerp(-75, 120, u)
        z = _lerp(0.455, 0.6, v)
        cup = 0.03 * math.sin(v * math.pi) * _g(a - 5, 45)
        wing = 0.05 * _g(a - 95, 18) * math.sin(v * math.pi)
        r = 0.072 + cup + wing
        x, y = ring(0.118, -0.01, r, r, a)
        return x, y, z
    s = Shell('KZ6_Joelho' + side, poleyn, 32, 12, sgn=sgn, thick=0.006)
    s.border()
    s.add_uv_tube([(0.41, 0.3), (0.41, 0.7)], 0.0018, 'rune', False, False)
    out.append(s)

    def greave(u, v):                                    # greva com quilha, panturrilha e boca alargada
        a = _lerp(-180, 180, u)
        z = _lerp(0.115, 0.475, v)
        r = 0.062 + 0.016 * _g(z - 0.36, 0.09) + 0.032 * _sm(0.8, 1.0, v)
        ry = r + 0.01 * _g(a, 50) + 0.03 * _g(abs(a) - 180, 70) * _g(z - 0.36, 0.11)
        r += 0.006 * _g(a, 5)
        ry += 0.007 * _g(a, 5)
        x, y = ring(0.12, 0.004, r, ry, a)
        return x, y, z
    s = Shell('KZ6_Greva' + side, greave, 48, 18, sgn=sgn, thick=0.006)
    s.border(sides='bt')
    s.border(inset=0.08, r=0.0015, sides='bt')
    s.add_uv_tube([(0.5, 0.2), (0.5, 0.75)], 0.0018, 'rune', False, False)
    out.append(s)

    def ankle(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(0.004, 0.14, v)
        x, y = ring(0.12, 0.004, 0.064 - 0.004 * v, 0.086 - 0.012 * v, a)
        return x, y, z
    s = Shell('KZ6_Tornozelo' + side, ankle, 40, 4, sgn=sgn, thick=0.005)
    s.border(sides='t')
    out.append(s)
    def foot(u, v):                                      # casca do pé inteiro, bico pontudo
        a = _lerp(-100, 100, u)
        y = _lerp(0.09, -0.215, v)
        t = max(0.0, min(1.0, (0.06 - y) / 0.275))
        w = 0.066 * (1 - _sm(0.66, 1.0, t) * 0.8) + 0.004 * _g(t - 0.45, 0.2)
        h = 0.15 - 0.075 * _sm(0.15, 0.6, t) - 0.045 * _sm(0.72, 1.0, t)
        x = 0.12 + w * math.sin(a * D2R)
        z = 0.004 + h * max(0.0, math.cos(a * D2R)) ** 0.8
        return x, y, z
    s = Shell('KZ6_Sapato%s' % side, foot, 26, 26, sgn=sgn, flip=True, thick=0.005)
    s.border(sides='b' if False else 'lrbt')
    out.append(s)
    for j in range(3):                                   # lâminas articuladas sobre o peito do pé
        def sab(u, v, j=j):
            a = _lerp(-92, 92, u)
            y = _lerp(-0.02 - 0.042 * j, -0.068 - 0.042 * j, v)
            t = max(0.0, min(1.0, (0.06 - y) / 0.275))
            w = 0.066 * (1 - _sm(0.66, 1.0, t) * 0.8) + 0.0075
            h = 0.15 - 0.075 * _sm(0.15, 0.6, t) - 0.045 * _sm(0.72, 1.0, t) + 0.006
            x = 0.12 + w * math.sin(a * D2R)
            z = 0.004 + h * max(0.0, math.cos(a * D2R)) ** 0.8
            return x, y, z
        s = Shell('KZ6_SapLam%s%d' % (side, j), sab, 18, 4, sgn=sgn, flip=True, thick=0.004)
        s.border(sides='lrbt')
        out.append(s)
    # malha de couro por baixo (coxa inteira), para não aparecer pele atrás
    def under(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(0.50, 0.9, v)
        r = 0.088 + 0.014 * _sm(0.0, 0.5, v)
        x, y = ring(0.112, 0.0, r, r * 1.02, a)
        return x, y, z
    s = Shell('KZ6_Malha%s' % side, under, 32, 6, sgn=sgn, thick=0.004, kind='leather')
    out.append(s)
    return out


def helm():
    """Elmo de rosto aberto: calota com crista em lâmina, aba da testa, protetores de bochecha, nasal e nuca."""
    C = Vector((0.0, 0.016, 1.655))
    out = []

    def sph(az, el, rx=0.118, ry=0.134, rz=0.15):
        x = rx * math.cos(el) * math.sin(az * D2R)
        y = -ry * math.cos(el) * math.cos(az * D2R)
        z = rz * math.sin(el)
        return tuple(C + Vector((x, y, z)))

    def cap(u, v):
        az = _lerp(-180, 180, u)
        el = _lerp(0.40, 1.5707, v)
        return sph(az, el)
    s = Shell('KZ6_Elmo_Calota', cap, 56, 14, thick=0.007)
    s.border(sides='b')
    s.border(inset=0.12, r=0.0016, sides='b')
    out.append(s)

    def sides(u, v):                                     # laterais e nuca (abertas na frente)
        az = _lerp(38, 322, u)
        el = _lerp(-0.62, 0.43, v)
        flare = 0.018 * _sm(0.35, 0.0, v)
        return sph(az, el, 0.121 + flare, 0.137 + flare * 0.8, 0.152)
    s = Shell('KZ6_Elmo_Lados', sides, 52, 12, thick=0.007)
    s.border()
    out.append(s)

    def cheek(u, v, sg):
        az = sg * _lerp(26, 56, u)
        el = _lerp(-0.66, 0.30, v)
        jut = 0.008 * math.sin(u * math.pi)
        p = sph(az, el, 0.124 + jut, 0.142 + jut, 0.152)
        return p
    for sg, nm in ((1, 'L'), (-1, 'R')):
        s = Shell('KZ6_Elmo_Bochecha' + nm, (lambda u, v, sg=sg: cheek(u if sg > 0 else 1 - u, v, sg)),
                  12, 12, thick=0.006)
        s.border()
        out.append(s)

    def nasal(u, v):
        az = _lerp(-6, 6, u)
        el = _lerp(-0.18, 0.46, v)
        return sph(az, el, 0.128, 0.146 + 0.006 * math.sin(u * math.pi), 0.152)
    s = Shell('KZ6_Elmo_Nasal', nasal, 4, 10, thick=0.006)
    s.border()
    out.append(s)

    for sg, nm in ((1, 'L'), (-1, 'R')):                 # asas douradas: 3 penas por lado, varridas para trás
        for k, (L, wd, lift) in enumerate(((1.0, 0.05, 0.0), (0.8, 0.042, -0.03), (0.6, 0.034, -0.055))):
            def feather(u, v, L=L, wd=wd, lift=lift):
                s0 = Vector((0.112, 0.035 + 0.01 * k, 1.665 + lift * 0.4))
                ln = 0.2 * L
                spine = s0 + Vector((0.05 * u, ln * 0.75 * u, (ln * 0.75 + lift * 0.3) * u ** 1.3 + lift * u))
                w = wd * (1 - u) ** 0.65 * (0.6 + 0.4 * math.sin(math.pi * min(1.0, u * 1.6)))
                p = spine + Vector((0.004, -0.55, 0.85)).normalized() * (w * (v - 0.25))
                return p.x, p.y, p.z
            sh = Shell('KZ6_Elmo_Asa%s%d' % (nm, k), feather, 16, 3, sgn=sg, thick=0.005, kind='goldplate')
            out.append(sh)

    def crest(u, v):                                     # crista: lâmina fina no plano do meio
        el = _lerp(0.75, 2.85, u)                        # do alto da testa até a nuca
        h = 0.03 * math.sin(_lerp(0.0, math.pi, u)) ** 0.6 + 0.003
        rr = 1.0 + (h / 0.15) * v
        y = -0.140 * math.cos(el) * rr
        z = 0.150 * math.sin(el) * rr
        return (0.0, C.y + y, C.z + z)
    s = Shell('KZ6_Elmo_Crista', crest, 40, 6, thick=0.009, kind='goldplate')
    out.append(s)
    return out


# ================================================================ montagem e pintura
def build6(set_name='Runica'):
    G = bpy.app.driver_namespace['KZ']
    shells = {}
    for s in cuirass() + fauld_and_tassets() + gorget() + helm():
        shells[s.name] = s
    for side in ('L', 'R'):
        for s in pauldrons(side) + arm(side) + leg(side):
            shells[s.name] = s
    objs = {}
    for nm, s in shells.items():
        objs[nm] = s.build()
        for t in tubes_obj(nm + '_T', s.tubes):
            objs[t.name] = t
    # capa longa (até a canela)
    cp = long_cape()
    objs[cp.name] = cp
    for t in tubes_obj(cp.name + '_T', CAPE_TUBES):
        objs[t.name] = t
    paint6(objs, set_name)
    return objs


CAPE_TUBES = []


def long_cape():
    CAPE_TUBES.clear()

    def pos(u, v):
        t = 1 - v                                        # 0 = gola, 1 = barra
        z = 1.42 - 1.12 * t
        uu = u * 2 - 1
        half = 0.19 + 0.21 * t ** 0.8
        x = uu * half
        y = 0.13 + 0.05 * (1 - uu * uu) * (1 - t) + 0.17 * t ** 1.5 + 0.035 * abs(uu) * t
        y += 0.018 * t * math.sin(uu * 7.5 + 0.6) + 0.007 * t * math.sin(uu * 17)
        if v < 0.02:
            z += 0.014 * math.sin(uu * 7.5 + 0.6)
        return x, y, z
    sh = Shell('KZ6_Capa', pos, 30, 36, thick=0.006, kind='cloth', flip=True)
    sh.border(sides='lrb', r=0.0045)
    CAPE_TUBES.extend(sh.tubes)
    sh.tubes = []
    return sh.build()


def paint6(objs, set_name='Runica'):
    G = bpy.app.driver_namespace['KZ']
    hx, mix = G['_hex'], G['_mix']
    st, sd, gd, rn, c1, c2, lt = [hx(c) for c in SETS6[set_name]]
    gd_d = mix(gd, (0.18, 0.09, 0.02), 0.55)
    for nm, o in objs.items():
        me = o.data
        kind = o.get('kind', 'steel')
        attr = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
        me.color_attributes.active_color = attr
        nl = len(me.loops)
        vidx = [0] * nl
        me.loops.foreach_get('vertex_index', vidx)
        uv = [0.0] * (nl * 2)
        if me.uv_layers:
            me.uv_layers[0].data.foreach_get('uv', uv)
        gin = o.vertex_groups.get('in')
        grim = o.vertex_groups.get('rim')
        win = [0.0] * len(me.vertices)
        wrim = [0.0] * len(me.vertices)
        for v in me.vertices:
            for g in v.groups:
                if gin and g.group == gin.index:
                    win[v.index] = g.weight
                elif grim and g.group == grim.index:
                    wrim[v.index] = g.weight
        wu, wv = o.get('wu', 1.0), o.get('wv', 1.0)
        flat = [0.0] * (nl * 4)
        for li in range(nl):
            vi = vidx[li]
            co, n = me.vertices[vi].co, me.vertices[vi].normal
            u, v = uv[li * 2], uv[li * 2 + 1]
            a = 1.0
            top = max(0.0, n.z) * 0.5 + 0.5
            if kind in ('steel', 'goldplate'):
                base = (st, sd) if kind == 'steel' else (gd, gd_d)
                edge = min(u * wu, (1 - u) * wu, v * wv, (1 - v) * wv)
                k = 0.05 + 0.80 * top ** 2.2 + 0.15 * _sm(0.4, 1.8, co.z)
                k *= 0.82 + 0.18 * _sm(0.0, 0.03, edge)                  # escurece perto do friso (sujeira)
                c = mix(base[1], base[0], max(0.0, min(1.0, k)))
                # brilho do metal escovado: faixas suaves na vertical
                c = mix(c, base[0], 0.10 * (0.5 + 0.5 * math.sin(co.z * 210 + co.x * 40)))
                if wrim[vi] > 0.5:
                    c = mix(c, (1, 1, 1), 0.35)                            # quina gasta: aço vivo
                if win[vi] > 0.5:
                    c = mix(base[1], (0, 0, 0), 0.35)                      # lado de dentro
            elif kind == 'leather':
                c = mix(mix(lt, (0, 0, 0), 0.35), lt, top)
                if win[vi] > 0.5:
                    c = mix(lt, (0, 0, 0), 0.6)
            elif kind == 'gold':
                c = mix(gd_d, gd, 0.25 + 0.75 * top)
                c = mix(c, (1.0, 0.93, 0.7), 0.25 * max(0.0, n.z) ** 4)
            elif kind == 'cloth':
                c = mix(c2, c1, 0.2 + 0.8 * _sm(0.3, 1.45, co.z) * (0.55 + 0.45 * top))
                if win[vi] > 0.5:
                    c = mix(c2, (0, 0, 0), 0.3)
            elif kind == 'rune':
                c, a = rn, 0.0
            else:
                c = st
            flat[li * 4:li * 4 + 4] = (min(1, c[0]), min(1, c[1]), min(1, c[2]), a)
        attr.data.foreach_set('color_srgb', flat)


# ================================================================ grupos de exportação (mesmos ossos da v5)
def groups6(objs):
    def pick(*pref):
        return tuple(k for k in objs if any(k.startswith(p) for p in pref))
    return {
        'Armadura': (pick('KZ6_Peito', 'KZ6_Costas', 'KZ6_Faixa', 'KZ6_Cinto', 'KZ6_Fivela', 'KZ6_Escarcela',
                          'KZ6_Gorjal', 'KZ6_OmbroL', 'KZ6_OmbroR', 'KZ6_Gibao', 'KZ6_Capa'), (0, 0, 0)),
        'LuvaL': (pick('KZ6_MangaL', 'KZ6_BracoL', 'KZ6_CotoveloL', 'KZ6_BracalL', 'KZ6_MaoL', 'KZ6_LamMaoL'), (0.31, 0, 1.40)),
        'LuvaR': (pick('KZ6_MangaR', 'KZ6_BracoR', 'KZ6_CotoveloR', 'KZ6_BracalR', 'KZ6_MaoR', 'KZ6_LamMaoR'), (-0.31, 0, 1.40)),
        'CalcaL': (pick('KZ6_CoxoteL', 'KZ6_JoelhoL', 'KZ6_MalhaL'), (0.12, 0, 0.92)),
        'CalcaR': (pick('KZ6_CoxoteR', 'KZ6_JoelhoR', 'KZ6_MalhaR'), (-0.12, 0, 0.92)),
        'BotaL': (pick('KZ6_GrevaL', 'KZ6_TornozeloL', 'KZ6_SapatoL', 'KZ6_SapLamL'), (0.12, 0, 0.92)),
        'BotaR': (pick('KZ6_GrevaR', 'KZ6_TornozeloR', 'KZ6_SapatoR', 'KZ6_SapLamR'), (-0.12, 0, 0.92)),
        'Elmo': (pick('KZ6_Elmo'), (0, 0, 1.47)),
    }


def tri_count(objs):
    return sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)
