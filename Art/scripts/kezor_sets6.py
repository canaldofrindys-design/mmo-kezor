# Kezor — sets do Guardião no método v6 (placas paramétricas): Couro (inicial) e Escamas (médio).
# Depende de kezor_armor6.py já executado no mesmo namespace (Shell, ring, tubes_obj, paint6, helpers).
import bpy, math
from mathutils import Vector, Matrix

SETS6['Couro'] = ('#b08458', '#3a2414', '#a7adb3', '#5fe0ff', '#b48a5a', '#4a2e18', '#4e321e')     # ferro no lugar do ouro; couro claro = 'capa'
SETS6['Escamas'] = ('#b3bcb4', '#1a1e1c', '#b8853e', '#5fe0ff', '#24384f', '#0b121b', '#4a3222')   # aço esverdeado, bronze


def _torso_r(z, wide=0.0):
    rx = 0.183 + (0.06 + wide) * _sm(1.04, 1.32, z) - 0.085 * _sm(1.38, 1.46, z)
    ry = 0.112 + 0.022 * _sm(1.04, 1.28, z) - 0.03 * _sm(1.38, 1.46, z)
    return rx, ry


def scale_rows(prefix, z_top, rows, h, step, radius_fn, a0, a1, n_scal, sgn=1, center=(0.0, 0.012), trim_last=True):
    """Fileiras de escamas: cada fileira tem a borda de baixo recortada em pontas redondas e cobre a de baixo."""
    out = []
    for j in range(rows):
        zt = z_top - step * j
        ph = 0.5 if j % 2 else 0.0

        def row(u, v, zt=zt, ph=ph):
            a = _lerp(a0, a1, u)
            tip = abs(math.sin(math.pi * (n_scal * u + ph))) ** 0.55
            zb = zt - h + 0.016 * (1 - tip)
            z = _lerp(zb, zt, v)
            rx, ry = radius_fn(z)
            fl = 0.007 * (1 - v)                         # a borda de baixo levanta por cima da próxima fileira
            x, y = ring(center[0], center[1], rx + fl, ry + fl, a)
            return x, y, z
        s = Shell('%s%d' % (prefix, j), row, int(n_scal * 8), 4, sgn=sgn, thick=0.004)
        if trim_last and j == rows - 1:
            s.border(sides='b', r=0.0022)
        out.append(s)
    return out


# ================================================================ COURO (inicial)
def set_couro():
    P = 'KC_'
    out = []

    def jerkin(u, v):                                    # gibão justo até o quadril
        a = _lerp(-180, 180, u)
        z = _lerp(0.84, 1.44, v)
        rx, ry = _torso_r(z, -0.03)
        fl = 0.022 * _sm(0.95, 0.84, z)
        x, y = ring(0, 0.012, rx + 0.01 + fl, ry + 0.014 + fl, a)
        return x, y, z
    s = Shell(P + 'Gibao', jerkin, 64, 22, thick=0.005, kind='leather')
    s.border(sides='b', r=0.0024)
    s.add_uv_tube([(0.5, 0.15), (0.5, 0.98)], 0.0018, 'gold', False, False)        # costura/fecho de ferro
    for k in range(5):                                                                  # fivelas do fecho
        vv = 0.3 + 0.13 * k
        s.add_uv_tube([(0.49, vv), (0.51, vv)], 0.0035, 'gold', False, False)
    out.append(s)

    def chestpiece(u, v):                                # peitoral de couro fervido por cima
        a = _lerp(-62, 62, u)
        zt = 1.40 - 0.05 * _g(a, 25)
        z = _lerp(1.08 - 0.03 * _g(a, 30), zt, v)
        rx, ry = _torso_r(z, -0.03)
        bump = 0.016 * _g(abs(a) - 30, 18) * _g(z - 1.3, 0.07)
        x, y = ring(0, 0.012, rx + 0.016, ry + 0.016 + bump, a)
        return x, y, z
    s = Shell(P + 'Peito', chestpiece, 30, 16, thick=0.006, kind='cloth')
    s.border(r=0.0022)
    out.append(s)

    def belt(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(0.885, 0.935, v)
        x, y = ring(0, 0.012, 0.21, 0.142, a)
        return x, y, z
    s = Shell(P + 'Cinto', belt, 72, 3, thick=0.008, kind='leather')
    s.add_uv_tube([(0.48, 0.5), (0.52, 0.5)], 0.012, 'gold', False, False)
    out.append(s)
    for side in (1, -1):                                 # abas de couro (frente e lado)
        for k, (ac, w) in enumerate(((22, 20), (70, 18), (140, 30))):
            def flap(u, v, ac=ac, w=w):
                a = ac + _lerp(-w, w, u)
                z = _lerp(0.66, 0.89, v)
                x, y = ring(0, 0.012, 0.218 + 0.03 * (1 - v), 0.15 + 0.03 * (1 - v), a)
                return x, y, z
            s = Shell(P + 'Aba%s%d' % ('L' if side > 0 else 'R', k), flap, 10, 8, sgn=side, thick=0.005, kind='leather')
            s.border(r=0.0018)
            out.append(s)
    for side in ('L', 'R'):                              # ombreiras de couro em duas camadas
        sgn = 1 if side == 'L' else -1
        S = Vector((0.29, 0.004, 1.40))
        tilt = Matrix.Rotation(-20 * D2R, 3, 'Y')
        for j, (R, zoff) in enumerate(((0.14, 0.0), (0.13, -0.055))):
            def pad(u, v, R=R, zoff=zoff, j=j):
                a = _lerp(-60, 240, u)
                th = _lerp(1.25, 0.0 if j == 0 else 0.7, v)
                r = R * math.sin(th)
                x, y = ring(0, 0, r, r * 0.92, a)
                return tuple(S + tilt @ Vector((x, y, R * 0.9 * math.cos(th) - 0.035 + zoff)))
            s = Shell(P + 'Ombro%s%d' % (side, j), pad, 36, 10, sgn=sgn, thick=0.006, kind='cloth')
            s.border(sides='b', r=0.0022)
            out.append(s)
        cx, cy = 0.305, -0.004

        def bracer(u, v):
            a = _lerp(-180, 180, u)
            z = _lerp(0.80, 1.0, v)
            r = 0.06 + 0.008 * v + 0.012 * _sm(0.15, 0.0, v)
            x, y = ring(cx, cy, r, r, a)
            return x, y, z
        s = Shell(P + 'Bracal' + side, bracer, 36, 10, sgn=sgn, thick=0.005, kind='cloth')
        s.border(sides='bt', r=0.0018)
        for k in range(4):                               # amarras
            s.add_uv_tube([(0.22, 0.2 + 0.18 * k), (0.28, 0.29 + 0.18 * k)], 0.0016, 'gold', False, False)
        out.append(s)

        def glove(u, v):
            a = _lerp(-180, 180, u)
            z = _lerp(0.655, 0.81, v)
            rx = 0.04 + 0.006 * _g(v - 0.45, 0.3)
            ry = 0.064 * (0.75 + 0.25 * _sm(0.0, 0.5, v))
            x, y = ring(0.307, -0.022 + 0.01 * v, rx, ry, a)
            return x, y, z
        out.append(Shell(P + 'Luva' + side, glove, 28, 8, sgn=sgn, thick=0.004, kind='leather'))

        def thigh(u, v):
            a = _lerp(-180, 180, u)
            z = _lerp(0.5, 0.9, v)
            r = 0.09 + 0.014 * _sm(0.0, 0.5, v)
            x, y = ring(0.112, 0.0, r, r * 1.02, a)
            return x, y, z
        out.append(Shell(P + 'Calca' + side, thigh, 32, 8, sgn=sgn, thick=0.004, kind='leather'))

        def knee(u, v):
            a = _lerp(-60, 60, u)
            z = _lerp(0.47, 0.6, v)
            r = 0.1 + 0.02 * math.sin(v * math.pi) * _g(a, 40)
            x, y = ring(0.116, -0.004, r, r, a)
            return x, y, z
        s = Shell(P + 'Joelho' + side, knee, 16, 8, sgn=sgn, thick=0.006, kind='cloth')
        s.border(r=0.0018)
        out.append(s)

        def boot(u, v):                                  # bota alta com dobra no topo
            a = _lerp(-180, 180, u)
            z = _lerp(0.004, 0.47, v)
            r = 0.06 + 0.014 * _g(z - 0.36, 0.09) + 0.016 * _sm(0.86, 0.9, v)
            x, y = ring(0.12, 0.004, r, r + 0.03 * _g(abs(a) - 180, 70) * _g(z - 0.36, 0.11) + 0.006 + 0.022 * _sm(0.14, 0.0, z), a)
            return x, y, z
        s = Shell(P + 'Bota' + side, boot, 40, 14, sgn=sgn, thick=0.005, kind='leather')
        s.border(sides='t', r=0.0026)
        out.append(s)

        def foot(u, v):
            a = _lerp(-100, 100, u)
            y = _lerp(0.09, -0.2, v)
            t = max(0.0, min(1.0, (0.06 - y) / 0.26))
            w = 0.064 * (1 - _sm(0.7, 1.0, t) * 0.55)
            h = 0.14 - 0.075 * _sm(0.15, 0.6, t) - 0.03 * _sm(0.75, 1.0, t)
            x = 0.12 + w * math.sin(a * D2R)
            z = 0.004 + h * max(0.0, math.cos(a * D2R)) ** 0.8
            return x, y, z
        s = Shell(P + 'Pe' + side, foot, 22, 20, sgn=sgn, flip=True, thick=0.005, kind='leather')
        s.border(sides='l', r=0.002)
        out.append(s)

    C = Vector((0.0, 0.016, 1.655))

    def capc(u, v):                                      # gorro de couro com abas nas orelhas
        az = _lerp(-180, 180, u)
        el = _lerp(0.25, 1.5707, v)
        x = 0.12 * math.cos(el) * math.sin(az * D2R)
        y = -0.136 * math.cos(el) * math.cos(az * D2R)
        z = 0.15 * math.sin(el)
        return tuple(C + Vector((x, y, z)))
    s = Shell(P + 'Gorro', capc, 48, 12, thick=0.006, kind='cloth')
    s.border(sides='b', r=0.0028)
    s.add_uv_tube([(0.5, 0.0), (0.5, 0.98)], 0.0022, 'gold', False, False)
    out.append(s)
    def nape(u, v):
        az = _lerp(70, 290, u)
        el = _lerp(-0.5, 0.3, v)
        fl = 0.02 * (1 - v)
        x = (0.124 + fl) * math.cos(el) * math.sin(az * D2R)
        y = -(0.14 + fl) * math.cos(el) * math.cos(az * D2R)
        z = 0.152 * math.sin(el)
        return tuple(C + Vector((x, y, z)))
    s = Shell(P + 'Nuca', nape, 30, 8, thick=0.005, kind='leather')
    s.border(r=0.0022)
    out.append(s)
    # rebites de ferro no peitoral e nas ombreiras
    for k in range(9):
        a = -50 + 100 * k / 8
        z = 1.385 - 0.05 * _g(a, 25)
        rx, ry = _torso_r(z, -0.03)
        x, y = ring(0, 0.012, rx + 0.024, ry + 0.024, a)
        n = Vector((math.sin(a * D2R) * 0.6, -math.cos(a * D2R), 0.1)).normalized()
        out.append(spike(P + 'Rebite%d' % k, Vector((x, y, z)) - n * 0.002, n, 0.0055, 0.005, kind='goldplate'))
    return out


# ================================================================ ESCAMAS (médio)
def set_escamas():
    P = 'KE_'
    out = []
    # cota de escamas do tronco: 8 fileiras do peito até o quadril
    out += scale_rows(P + 'Cota', 1.43, 8, 0.085, 0.066,
                      lambda z: tuple(r + 0.008 for r in _torso_r(z, -0.01)), -180, 180, 26)
    # saia de escamas
    out += scale_rows(P + 'Saia', 0.90, 3, 0.09, 0.07,
                      lambda z: (0.212 + 0.25 * (0.9 - z), 0.144 + 0.25 * (0.9 - z)), -180, 180, 24)

    def belt(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(0.885, 0.94, v)
        x, y = ring(0, 0.012, 0.213, 0.146, a)
        return x, y, z
    s = Shell(P + 'Cinto', belt, 72, 3, thick=0.008, kind='leather')
    s.border(sides='bt', r=0.0022)
    out.append(s)

    def buckle(u, v):
        a = _lerp(-11, 11, u)
        z = _lerp(0.875, 0.95, v)
        x, y = ring(0, 0.012, 0.22, 0.155 + 0.01 * math.sin(u * math.pi) * math.sin(v * math.pi), a)
        return x, y, z
    out.append(Shell(P + 'Fivela', buckle, 10, 8, thick=0.006, kind='goldplate'))

    def plastron(u, v):                                  # placa de peito por cima das escamas
        a = _lerp(-48, 48, u)
        z = _lerp(1.16 - 0.03 * _g(a, 25), 1.40 - 0.04 * _g(a, 25), v)
        rx, ry = _torso_r(z, -0.01)
        r_add = 0.03 + 0.012 * _g(a, 5) + 0.012 * _g(abs(a) - 26, 15) * _g(z - 1.3, 0.06)
        x, y = ring(0, 0.012, rx + r_add * 0.5, ry + r_add, a)
        return x, y, z
    s = Shell(P + 'Placa', plastron, 32, 14, thick=0.007)
    s.border()
    s.border(inset=0.1, r=0.0015)
    out.append(s)

    def gorget(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(1.41, 1.5, v) - 0.012 * _g(a, 35)
        r = _lerp(0.2, 0.13, v)
        x, y = ring(0, 0.018, r, r * 0.86, a)
        return x, y, z
    s = Shell(P + 'Gorjal', gorget, 56, 4, thick=0.006)
    s.border(sides='bt')
    out.append(s)

    for side in ('L', 'R'):
        sgn = 1 if side == 'L' else -1
        S = Vector((0.295, 0.004, 1.40))
        tilt = Matrix.Rotation(-20 * D2R, 3, 'Y')
        SC = 1.12

        def place(l):
            return tuple(S + tilt @ (Vector(l) * SC))

        def dome(u, v):
            a = _lerp(-60, 240, u)
            th = _lerp(1.3, 0.0, v)
            R = 0.15
            r = R * math.sin(th) + 0.012 * _g(a - 90, 8) * math.sin(th) * _sm(0.2, 0.9, v)
            x, y = ring(0, 0, r, r * 0.92, a)
            return place((x, y, R * 0.85 * math.cos(th) - 0.035))
        s = Shell(P + 'Ombro%s_Cupula' % side, dome, 44, 16, sgn=sgn, thick=0.007)
        s.border()
        out.append(s)
        for j in range(2):
            def lame(u, v, j=j):
                a = _lerp(-55, 235, u)
                zt = -0.005 - 0.05 * j
                tip = abs(math.sin(math.pi * 5 * u)) ** 0.6
                z = _lerp(zt - 0.07 + 0.012 * (1 - tip), zt, v)
                r = 0.124 + 0.012 * j + 0.026 * (1 - v)
                x, y = ring(0, 0, r, r * 0.9, a)
                return place((x, y, z))
            s = Shell(P + 'Ombro%s_Lamina%d' % (side, j), lame, 60, 5, sgn=sgn, thick=0.006)
            s.border()
            out.append(s)
        cx, cy = 0.305, -0.004
        out += scale_rows(P + 'Manga%s' % side, 1.30, 3, 0.08, 0.06,
                          lambda z: (0.078, 0.078), -180, 180, 12, sgn=sgn, center=(cx, cy))

        def vamb(u, v):
            a = _lerp(-80, 260, u)
            z = _lerp(0.815, 1.04, v)
            r = 0.062 + 0.01 * v + 0.022 * _sm(0.2, 0.0, v)
            r += 0.008 * _g(a - 60, 9)
            x, y = ring(cx, cy, r, r * 0.97, a)
            return x, y, z
        s = Shell(P + 'Bracal' + side, vamb, 40, 12, sgn=sgn, thick=0.006)
        s.border(sides='bt')
        out.append(s)

        def couter(u, v):
            a = _lerp(80, 240, u)
            z = _lerp(1.03, 1.14, v)
            r = 0.074 + 0.024 * math.sin(v * math.pi) * _g(a - 165, 40)
            x, y = ring(cx, cy, r, r, a)
            return x, y, z
        s = Shell(P + 'Cotovelo' + side, couter, 24, 10, sgn=sgn, thick=0.006)
        s.border()
        out.append(s)

        def glove(u, v):
            a = _lerp(-180, 180, u)
            z = _lerp(0.655, 0.80, v)
            rx = 0.042 + 0.007 * _g(v - 0.45, 0.3)
            ry = 0.066 * (0.75 + 0.25 * _sm(0.0, 0.5, v))
            x, y = ring(0.307, -0.022 + 0.01 * v, rx, ry, a)
            return x, y, z
        out.append(Shell(P + 'Luva' + side, glove, 28, 8, sgn=sgn, thick=0.004, kind='leather'))

        def under(u, v):
            a = _lerp(-180, 180, u)
            z = _lerp(0.5, 0.9, v)
            r = 0.088 + 0.014 * _sm(0.0, 0.5, v)
            x, y = ring(0.112, 0.0, r, r * 1.02, a)
            return x, y, z
        out.append(Shell(P + 'Malha' + side, under, 32, 6, sgn=sgn, thick=0.004, kind='leather'))
        out += scale_rows(P + 'Coxa%s' % side, 0.86, 4, 0.08, 0.062,
                          lambda z: (0.11, 0.114), -110, 120, 9, sgn=sgn, center=(0.112, 0.0))

        def knee(u, v):
            a = _lerp(-75, 115, u)
            z = _lerp(0.455, 0.6, v)
            r = 0.07 + 0.028 * math.sin(v * math.pi) * _g(a - 5, 45) + 0.03 * _g(a - 95, 18) * math.sin(v * math.pi)
            x, y = ring(0.118, -0.01, r, r, a)
            return x, y, z
        s = Shell(P + 'Joelho' + side, knee, 30, 10, sgn=sgn, thick=0.006)
        s.border()
        out.append(s)

        def greave(u, v):
            a = _lerp(-180, 180, u)
            z = _lerp(0.115, 0.47, v)
            r = 0.062 + 0.016 * _g(z - 0.36, 0.09) + 0.018 * _sm(0.85, 1.0, v)
            ry = r + 0.008 * _g(a, 50) + 0.03 * _g(abs(a) - 180, 70) * _g(z - 0.36, 0.11)
            x, y = ring(0.12, 0.004, r + 0.005 * _g(a, 5), ry + 0.006 * _g(a, 5), a)
            return x, y, z
        s = Shell(P + 'Greva' + side, greave, 44, 16, sgn=sgn, thick=0.006)
        s.border(sides='bt')
        out.append(s)

        def ankle(u, v):
            a = _lerp(-180, 180, u)
            z = _lerp(0.004, 0.14, v)
            x, y = ring(0.12, 0.004, 0.064 - 0.004 * v, 0.086 - 0.012 * v, a)
            return x, y, z
        out.append(Shell(P + 'Tornozelo' + side, ankle, 40, 4, sgn=sgn, thick=0.005, kind='leather'))

        def foot(u, v):
            a = _lerp(-100, 100, u)
            y = _lerp(0.09, -0.21, v)
            t = max(0.0, min(1.0, (0.06 - y) / 0.27))
            w = 0.065 * (1 - _sm(0.66, 1.0, t) * 0.7)
            h = 0.15 - 0.075 * _sm(0.15, 0.6, t) - 0.04 * _sm(0.72, 1.0, t)
            x = 0.12 + w * math.sin(a * D2R)
            z = 0.004 + h * max(0.0, math.cos(a * D2R)) ** 0.8
            return x, y, z
        s = Shell(P + 'Pe' + side, foot, 22, 22, sgn=sgn, flip=True, thick=0.005)
        s.border()
        out.append(s)

    # elmo de nasal com nervuras e cortina de escamas na nuca
    C = Vector((0.0, 0.016, 1.655))

    def sph(az, el, rx=0.12, ry=0.136, rz=0.152):
        return tuple(C + Vector((rx * math.cos(el) * math.sin(az * D2R), -ry * math.cos(el) * math.cos(az * D2R), rz * math.sin(el))))

    def capk(u, v):
        az = _lerp(-180, 180, u)
        el = _lerp(0.3, 1.5707, v)
        pk = 0.012 * _sm(0.7, 1.0, v)                    # topo levemente pontudo
        return sph(az, el, 0.12, 0.136, 0.152 + pk)
    s = Shell(P + 'Elmo', capk, 48, 12, thick=0.007)
    s.border(sides='b')
    for k in range(4):                                   # nervuras de bronze
        uu = k / 4
        s.add_uv_tube([(uu, 0.0), (uu, 0.97)], 0.0024, 'gold', False, False)
    out.append(s)

    def nasal(u, v):
        az = _lerp(-7, 7, u)
        el = _lerp(-0.25, 0.32, v)
        return sph(az, el, 0.128, 0.146 + 0.008 * math.sin(u * math.pi), 0.152)
    s = Shell(P + 'Nasal', nasal, 4, 8, thick=0.006, kind='goldplate')
    out.append(s)

    def aventail_r(z):
        t = (1.70 - z) / 0.2
        return (0.125 + 0.03 * t, 0.142 + 0.03 * t)
    out += scale_rows(P + 'Cortina', 1.70, 3, 0.075, 0.058, aventail_r, 55, 305, 16, center=(0.0, 0.016))
    return out


def build_set(set_name, maker):
    objs = {}
    for s in maker():
        objs[s.name] = s.build()
        for t in tubes_obj(s.name + '_T', s.tubes):
            objs[t.name] = t
    paint6(objs, set_name)
    return objs


def groups_set(objs, P):
    def pick(*pref):
        return tuple(k for k in objs if any(k.startswith(P + p) for p in pref))
    if P == 'KC_':
        return {
            'Armadura': (pick('Gibao', 'Peito', 'Cinto', 'Aba', 'OmbroL', 'OmbroR', 'Rebite'), (0, 0, 0)),
            'LuvaL': (pick('BracalL', 'LuvaL'), (0.31, 0, 1.40)), 'LuvaR': (pick('BracalR', 'LuvaR'), (-0.31, 0, 1.40)),
            'CalcaL': (pick('CalcaL', 'JoelhoL'), (0.12, 0, 0.92)), 'CalcaR': (pick('CalcaR', 'JoelhoR'), (-0.12, 0, 0.92)),
            'BotaL': (pick('BotaL', 'PeL'), (0.12, 0, 0.92)), 'BotaR': (pick('BotaR', 'PeR'), (-0.12, 0, 0.92)),
            'Elmo': (pick('Gorro', 'Nuca'), (0, 0, 1.47)),
        }
    return {
        'Armadura': (pick('Cota', 'Saia', 'Cinto', 'Fivela', 'Placa', 'Gorjal', 'OmbroL', 'OmbroR'), (0, 0, 0)),
        'LuvaL': (pick('MangaL', 'BracalL', 'CotoveloL', 'LuvaL'), (0.31, 0, 1.40)),
        'LuvaR': (pick('MangaR', 'BracalR', 'CotoveloR', 'LuvaR'), (-0.31, 0, 1.40)),
        'CalcaL': (pick('MalhaL', 'CoxaL', 'JoelhoL'), (0.12, 0, 0.92)),
        'CalcaR': (pick('MalhaR', 'CoxaR', 'JoelhoR'), (-0.12, 0, 0.92)),
        'BotaL': (pick('GrevaL', 'TornozeloL', 'PeL'), (0.12, 0, 0.92)),
        'BotaR': (pick('GrevaR', 'TornozeloR', 'PeR'), (-0.12, 0, 0.92)),
        'Elmo': (pick('Elmo', 'Nasal', 'Cortina'), (0, 0, 1.47)),
    }


def export_named(objs, groups, set_name):
    G = bpy.app.driver_namespace['KZ']
    old5 = dict(G['GROUPS5'])
    oldcopy = G['_copy']

    def mcopy(o, matid):
        k = o.get('kind', '')
        return oldcopy(o, 4 if k == 'leather' else (5 if k == 'cloth' else 3))
    G['GROUPS5'].clear()
    G['GROUPS5'].update(groups)
    G['_copy'] = mcopy
    try:
        return G['export_set5'](objs, set_name)
    finally:
        G['GROUPS5'].clear()
        G['GROUPS5'].update(old5)
        G['_copy'] = oldcopy
