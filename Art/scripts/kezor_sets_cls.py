# Kezor — sets das outras 5 classes no método v6 (placas paramétricas + frisos, rebites e costuras).
# Depende de kezor_armor6.py e kezor_sets6.py já executados no mesmo namespace.
# Véu (Arcanista), Trama (Lâmina Arcana), Bastião (Comandante), Ecos (Invocadora), Ígneo (Punho de Brasa).
import bpy, math
from mathutils import Vector, Matrix

#                 aço claro   aço escuro  ouro       runa       pano claro pano escuro couro
SETS6['Veu'] = ('#c7ccd8', '#1c2030', '#d9b45a', '#8fd0ff', '#3f5fb0', '#141b3a', '#2a2238')
SETS6['Trama'] = ('#a3283a', '#1e0609', '#e2b04e', '#ff5cd6', '#3a2244', '#0d070f', '#2a1c18')
SETS6['Bastiao'] = ('#34343e', '#060609', '#e6b450', '#b07bff', '#5a2a86', '#1a0a2a', '#241a14')
SETS6['Ecos'] = ('#ece6f4', '#4a4060', '#d8b866', '#c08cff', '#c4b2e2', '#3b2e52', '#3a2a48')
SETS6['Igneo'] = ('#8f8984', '#1e1a17', '#c98a3a', '#ff7a2a', '#b0301c', '#3a0a06', '#6a2a18')

HEAD = Vector((0.0, 0.016, 1.655))
ARM = (0.305, -0.004)
GRP = {}                     # nome da casca -> peça (Armadura, LuvaL, ...)


def _add(out, s, grp):
    s.grp = grp
    out.append(s)
    return s


def _hip_r(z, add=0.0):
    if z > 0.95:
        rx, ry = _torso_r(z, -0.02)
    else:
        rx, ry = 0.205, 0.142
    return rx + add, ry + add


def _sph(az, el, rx, ry, rz, c=HEAD):
    return tuple(c + Vector((rx * math.cos(el) * math.sin(az * D2R), -ry * math.cos(el) * math.cos(az * D2R), rz * math.sin(el))))


def _sigil(s, cu, cv, su, sv, kind='rune', r=0.0022):
    """Losango de runa com risco central (emblema aceso)."""
    s.add_uv_tube([(cu, cv - sv), (cu + su, cv), (cu, cv + sv), (cu - su, cv)], r, kind, True, False)
    s.add_uv_tube([(cu, cv - sv * 0.55), (cu, cv + sv * 0.55)], r * 0.8, kind, False, False)


# ================================================================ peças genéricas
def g_torso(P, name, z0, z1, add, kind='leather', flare=0.0, nu=48, nv=12, thick=0.005, wide=-0.02, top=0.0):
    def f(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(z0, z1, v)
        rx, ry = _torso_r(z, wide)
        fl = flare * _sm(z0 + 0.12, z0, z)
        x, y = ring(0, 0.012, rx + add + fl + top * _sm(1.3, z1, z), ry + add + fl, a)
        return x, y, z
    return Shell(P + name, f, nu, nv, thick=thick, kind=kind)


def g_cuirass(P, add=0.012, kind='steel', keel=0.018, pecs=0.03, zb=0.97):
    """Peitoral (frente) e costas com cava do braço; quilha e peitorais em relevo."""
    def front(u, v):
        a = _lerp(-102, 102, u)
        aa = abs(a)
        zbb = zb - 0.035 * _g(a, 22)
        zt = 1.445 - 0.055 * _g(a, 30) - 0.17 * _sm(52, 90, aa)
        z = _lerp(zbb, zt, v)
        rx, ry = _torso_r(z, 0.0)
        r_add = keel * _g(a, 5) * _sm(1.0, 1.08, z) + pecs * _g(aa - 33, 19) * _g(z - 1.31, 0.07)
        r_add += 0.008 * _g(aa - 17, 10) * (_g(z - 1.09, 0.025) + _g(z - 1.15, 0.025))
        x, y = ring(0, 0.012, rx + add + r_add * abs(math.sin(a * D2R)) * 0.4, ry + add + r_add, a)
        return x, y, z

    def back(u, v):
        a = _lerp(76, 284, u)
        aa = abs(a - 180)
        zt = 1.455 - 0.03 * _g(aa, 30) - 0.17 * _sm(60, 100, aa)
        z = _lerp(zb, zt, v)
        rx, ry = _torso_r(z, 0.0)
        r_add = 0.012 * _g(aa - 40, 22) * _g(z - 1.33, 0.08)
        x, y = ring(0, 0.012, rx + add - 0.004, ry + add + 0.004 + r_add, a)
        return x, y, z
    f = Shell(P + 'Peito', front, 56, 28, thick=0.007, kind=kind)
    b = Shell(P + 'Costas', back, 48, 24, thick=0.007, kind=kind)
    return f, b


def g_belt(P, z0=0.885, z1=0.935, rx=0.212, ry=0.146, kind='leather', name='Cinto'):
    def f(u, v):
        a = _lerp(-180, 180, u)
        x, y = ring(0, 0.012, rx, ry, a)
        return x, y, _lerp(z0, z1, v)
    return Shell(P + name, f, 72, 3, thick=0.008, kind=kind)


def g_buckle(P, z0=0.872, z1=0.948, w=12, r=0.22, ry=0.156, kind='goldplate', name='Fivela'):
    def f(u, v):
        a = _lerp(-w, w, u)
        x, y = ring(0, 0.012, r, ry + 0.012 * math.sin(u * math.pi) * math.sin(v * math.pi), a)
        return x, y, _lerp(z0, z1, v)
    return Shell(P + name, f, 10, 8, thick=0.006, kind=kind)


def g_panel(P, name, ac, w, z_top, z_bot, push=0.012, flare=0.03, point=0.0, kind='cloth', sgn=1, folds=0.0, nu=14, nv=18):
    """Painel pendurado (tabardo, aba, saiote) em volta do quadril."""
    def f(u, v):
        a = ac + _lerp(-w, w, u)
        zb = z_bot - point * (1 - abs(2 * u - 1))
        z = _lerp(zb, z_top, v)
        rx, ry = _hip_r(z)
        p = push + flare * (1 - v) ** 1.3 + folds * (1 - v) * math.sin(u * math.pi * 5)
        x, y = ring(0, 0.012, rx + p, ry + p, a)
        return x, y, z
    return Shell(P + name, f, nu, nv, sgn=sgn, thick=0.005, kind=kind)


def g_gorget(P, kind='steel', z0=1.41, z1=1.5, r0=0.2, r1=0.13, name='Gorjal'):
    def f(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(z0, z1, v) - 0.012 * _g(a, 35)
        r = _lerp(r0, r1, v)
        x, y = ring(0, 0.018, r, r * 0.86, a)
        return x, y, z
    return Shell(P + name, f, 56, 4, thick=0.006, kind=kind)


def g_pauldron(P, side, R=0.15, SC=1.12, lames=2, kind='steel', tilt=-20, fin=False, spikes=(), crown=0, rune=False, grp='Armadura', out=None):
    sgn = 1 if side == 'L' else -1
    S = Vector((0.295, 0.004, 1.40))
    T = Matrix.Rotation(tilt * D2R, 3, 'Y')

    def place(l):
        return tuple(S + T @ (Vector(l) * SC))

    def dome(u, v):
        a = _lerp(-60, 240, u)
        th = _lerp(1.3, 0.0, v)
        r = R * math.sin(th) + 0.012 * _g(a - 90, 8) * math.sin(th) * _sm(0.2, 0.9, v)
        x, y = ring(0, 0, r, r * 0.92, a)
        return place((x, y, R * 0.85 * math.cos(th) - 0.035))
    d = _add(out, Shell('%sOmbro%s_Cupula' % (P, side), dome, 44, 16, sgn=sgn, thick=0.007, kind=kind), grp)
    d.border()
    d.border(inset=0.1, r=0.0016, sides='b')
    d.rivet_row('b', 0.12, 8, 0.005, (0.08, 0.92))
    if rune:
        d.add_uv_tube([(0.5, 0.25), (0.5, 0.9)], 0.0022, 'rune', False, False)
    for j in range(lames):
        def lame(u, v, j=j):
            a = _lerp(-55, 235, u)
            zt = -0.005 - 0.048 * j
            tip = abs(math.sin(math.pi * 5 * u)) ** 0.6 if kind == 'steel' and j == lames - 1 else 1.0
            z = _lerp(zt - 0.068 + 0.012 * (1 - tip), zt, v)
            r = R * 0.82 + 0.012 * j + 0.026 * (1 - v)
            x, y = ring(0, 0, r, r * 0.9, a)
            return place((x, y, z))
        s = _add(out, Shell('%sOmbro%s_Lamina%d' % (P, side, j), lame, 52, 5, sgn=sgn, thick=0.006, kind=kind), grp)
        s.border()
        s.rivet_row('t', 0.3, 6, 0.0042, (0.1, 0.9))
    if fin:
        def fn(u, v):
            th = _lerp(1.05, 0.05, u)
            base = Vector((R * math.sin(th), 0.0, R * 0.85 * math.cos(th) - 0.035))
            nrm = Vector((math.sin(th), 0.0, math.cos(th) * 0.9)).normalized()
            h = 0.034 * math.sin(math.pi * u) ** 0.6 + 0.022 * _g(u - 0.78, 0.09)
            p = base + nrm * (h * v + 0.004)
            return place((p.x, p.y, p.z))
        _add(out, Shell('%sOmbro%s_Crista' % (P, side), fn, 22, 4, sgn=sgn, thick=0.007, kind='goldplate'), grp)
    sp = list(spikes)
    for k in range(crown):                                # coroa de pontas no alto da cúpula
        sp.append((40 + 100 * k / max(1, crown - 1), 0.55, 0.06, 0.013))
    for k, (a, th, L, r0) in enumerate(sp):
        sa, ca = math.sin(a * D2R), math.cos(a * D2R)
        loc = Vector((R * math.sin(th) * sa, -R * 0.92 * math.sin(th) * ca, R * 0.85 * math.cos(th) - 0.035))
        nrm = Vector((math.sin(th) * sa, -math.sin(th) * ca, math.cos(th))).normalized()
        axis = (nrm + Vector((0.25, 0, 0.9))).normalized()
        _add(out, spike('%sOmbro%s_Ponta%d' % (P, side, k), S + T @ (loc * SC), T @ axis, r0, L, sgn), grp)
    return out


def g_sleeve(P, side, z0=1.0, z1=1.38, r0=0.07, r1=0.078, kind='leather', name='Manga', bell=0.0, drop=0.0, folds=0.0, nu=28, nv=8, thick=0.004):
    """Manga justa (bell=0) ou manga de sino que abre e cai atrás do punho."""
    sgn = 1 if side == 'L' else -1
    cx, cy = ARM

    def f(u, v):
        a = _lerp(-180, 180, u)
        zb = z0 - drop * (0.5 - 0.5 * math.cos(a * D2R))
        z = _lerp(zb, z1, v)
        r = _lerp(r0, r1, v) + bell * (1 - v) ** 1.6 + folds * (1 - v) * math.sin(a * D2R * 5)
        x, y = ring(cx, cy, r, r, a)
        return x, y, z
    return Shell(P + name + side, f, nu, nv, sgn=sgn, thick=thick, kind=kind)


def g_bracer(P, side, z0=0.815, z1=1.04, flare=0.022, kind='steel', name='Bracal', a0=-80, a1=260, keel=0.008, r0=0.062):
    sgn = 1 if side == 'L' else -1
    cx, cy = ARM

    def f(u, v):
        a = _lerp(a0, a1, u)
        z = _lerp(z0, z1, v)
        r = r0 + 0.01 * v + flare * _sm(0.2, 0.0, v) + keel * _g(a - 60, 9)
        x, y = ring(cx, cy, r, r * 0.97, a)
        return x, y, z
    return Shell(P + name + side, f, 40, 12, sgn=sgn, thick=0.006, kind=kind)


def g_couter(P, side, kind='steel', wing=0.03):
    sgn = 1 if side == 'L' else -1
    cx, cy = ARM

    def f(u, v):
        a = _lerp(75, 245, u)
        z = _lerp(1.025, 1.145, v)
        r = 0.075 + 0.026 * math.sin(v * math.pi) * _g(a - 165, 40) + wing * _g(a - 95, 22) * math.sin(v * math.pi)
        x, y = ring(cx, cy, r, r, a)
        return x, y, z
    return Shell(P + 'Cotovelo' + side, f, 26, 10, sgn=sgn, thick=0.006, kind=kind)


def g_glove(P, side, kind='leather'):
    sgn = 1 if side == 'L' else -1

    def f(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(0.655, 0.81, v)
        rx = 0.042 + 0.007 * _g(v - 0.45, 0.3)
        ry = 0.066 * (0.75 + 0.25 * _sm(0.0, 0.5, v))
        x, y = ring(0.307, -0.022 + 0.01 * v, rx, ry, a)
        return x, y, z
    return Shell(P + 'Luva' + side, f, 28, 8, sgn=sgn, thick=0.004, kind=kind)


def g_handlames(P, side, n=3, kind='steel', out=None, grp=None, knuckles=0):
    sgn = 1 if side == 'L' else -1
    for j in range(n):
        def hl(u, v, j=j):
            a = _lerp(25, 155, u)
            z = _lerp(0.735 - 0.032 * j, 0.775 - 0.032 * j, v)
            x, y = ring(0.307, -0.017, 0.055 - 0.002 * j, 0.077, a)
            return x, y, z
        s = _add(out, Shell('%sLamMao%s%d' % (P, side, j), hl, 14, 3, sgn=sgn, thick=0.004, kind=kind), grp)
        s.border(sides='b')
    for k in range(knuckles):                             # cravos nos nós dos dedos (soco)
        a = _lerp(45, 135, k / max(1, knuckles - 1))
        x, y = ring(0.307, -0.017, 0.058, 0.078, a)
        n = Vector((math.sin(a * D2R), -math.cos(a * D2R) * 0.8, -0.35)).normalized()
        _add(out, spike('%sCravo%s%d' % (P, side, k), Vector((x, y, 0.69)) - n * 0.003, n, 0.0085, 0.024, sgn, 'steel'), grp)


def g_cuisse(P, side, kind='steel', z0=0.605, a0=-95, a1=115, r0=0.122):
    sgn = 1 if side == 'L' else -1

    def f(u, v):
        a = _lerp(a0, a1, u)
        zt = 0.86 - 0.04 * _g(a + 40, 40)
        z = _lerp(z0, zt, v)
        r = r0 + 0.004 * v + 0.006 * _g(a - 10, 7)
        x, y = ring(0.11, -0.002, r, r * 1.04, a)
        return x, y, z
    return Shell(P + 'Coxote' + side, f, 36, 12, sgn=sgn, thick=0.006, kind=kind)


def g_under(P, side, kind='leather', name='Malha', z0=0.5, z1=0.9):
    sgn = 1 if side == 'L' else -1

    def f(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(z0, z1, v)
        r = 0.088 + 0.014 * _sm(0.0, 0.5, v)
        x, y = ring(0.112, 0.0, r, r * 1.02, a)
        return x, y, z
    return Shell(P + name + side, f, 32, 6, sgn=sgn, thick=0.004, kind=kind)


def g_knee(P, side, kind='steel', wing=0.03):
    sgn = 1 if side == 'L' else -1

    def f(u, v):
        a = _lerp(-75, 115, u)
        z = _lerp(0.455, 0.6, v)
        r = 0.07 + 0.028 * math.sin(v * math.pi) * _g(a - 5, 45) + wing * _g(a - 95, 18) * math.sin(v * math.pi)
        x, y = ring(0.118, -0.01, r, r, a)
        return x, y, z
    return Shell(P + 'Joelho' + side, f, 30, 10, sgn=sgn, thick=0.006, kind=kind)


def g_greave(P, side, kind='steel', z1=0.47, flare=0.018, name='Greva'):
    sgn = 1 if side == 'L' else -1

    def f(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(0.115, z1, v)
        r = 0.062 + 0.016 * _g(z - 0.36, 0.09) + flare * _sm(0.85, 1.0, v)
        ry = r + 0.008 * _g(a, 50) + 0.03 * _g(abs(a) - 180, 70) * _g(z - 0.36, 0.11)
        x, y = ring(0.12, 0.004, r + 0.005 * _g(a, 5), ry + 0.006 * _g(a, 5), a)
        return x, y, z
    return Shell(P + name + side, f, 44, 16, sgn=sgn, thick=0.006, kind=kind)


def g_ankle(P, side, kind='leather'):
    sgn = 1 if side == 'L' else -1

    def f(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(0.004, 0.14, v)
        x, y = ring(0.12, 0.004, 0.064 - 0.004 * v, 0.086 - 0.012 * v, a)
        return x, y, z
    return Shell(P + 'Tornozelo' + side, f, 40, 4, sgn=sgn, thick=0.005, kind=kind)


def g_foot(P, side, kind='steel', point=0.7, length=0.21, name='Pe', lift=0.0):
    sgn = 1 if side == 'L' else -1

    def f(u, v):
        a = _lerp(-100, 100, u)
        y = _lerp(0.09, -length, v)
        t = max(0.0, min(1.0, (0.06 - y) / (length + 0.06)))
        w = 0.065 * (1 - _sm(0.66, 1.0, t) * point)
        h = 0.15 - 0.075 * _sm(0.15, 0.6, t) - 0.04 * _sm(0.72, 1.0, t)
        x = 0.12 + w * math.sin(a * D2R)
        z = 0.004 + h * max(0.0, math.cos(a * D2R)) ** 0.8 + lift * _sm(0.85, 1.0, t)
        return x, y, z
    return Shell(P + name + side, f, 22, 22, sgn=sgn, flip=True, thick=0.005, kind=kind)


def g_boot(P, side, kind='leather', z1=0.47, cuff=0.016):
    sgn = 1 if side == 'L' else -1

    def f(u, v):
        a = _lerp(-180, 180, u)
        z = _lerp(0.004, z1, v)
        r = 0.06 + 0.014 * _g(z - 0.36, 0.09) + cuff * _sm(0.86, 0.9, v)
        ry = r + 0.03 * _g(abs(a) - 180, 70) * _g(z - 0.36, 0.11) + 0.006 + 0.022 * _sm(0.14, 0.0, z)
        x, y = ring(0.12, 0.004, r, ry, a)
        return x, y, z
    return Shell(P + 'Bota' + side, f, 40, 14, sgn=sgn, thick=0.005, kind=kind)


def g_robe_leg(P, side, z0=0.13, z1=0.94, flare=0.06, kind='cloth', name='Saia', folds=0.006, back=0.0):
    """Metade da túnica longa presa na perna (anda junto com a perna, sem atravessar)."""
    sgn = 1 if side == 'L' else -1

    def f(u, v):
        a = _lerp(-180, 180, u)
        t = 1 - v
        zb = z0 - back * _g(abs(a) - 180, 60)
        z = _lerp(zb, z1, v)
        r = 0.118 + flare * t ** 1.2 + folds * t * math.sin(a * D2R * 6)
        w = _sm(0.0, -0.5, math.sin(a * D2R))           # lado de dentro (perto da outra perna)
        rx = r * (1 - w) + min(r, 0.106) * w
        x, y = ring(0.11, 0.0, rx, r * 1.06, a)
        return x, y, z
    return Shell(P + name + side, f, 40, 18, sgn=sgn, thick=0.005, kind=kind)


def g_hood(P, name='Capuz', kind='cloth', peak=0.05, gap=38, rx=0.142, ry=0.158, rz=0.17, drape=0.06):
    def f(u, v):
        az = _lerp(gap, 360 - gap, u)
        el = _lerp(-0.9, 1.5707, v)
        dr = drape * _sm(-0.35, -0.9, el)
        p = Vector(_sph(az, el, rx + dr, ry + dr, rz))
        k = _sm(0.95, 1.5707, el)
        p += Vector((0.0, 0.7 * peak * k, peak * k))
        return p.x, p.y, p.z
    return Shell(P + name, f, 52, 20, thick=0.006, kind=kind)


def g_band(P, name, z0, z1, rx=0.132, ry=0.15, kind='goldplate', point=0.0, nu=56, rz=0.17, tilt=0.025):
    """Faixa na testa que segue a forma da cabeça (sobe um pouco atrás)."""
    def f(u, v):
        az = _lerp(-180, 180, u)
        back = 0.5 - 0.5 * math.cos(az * D2R)
        z = _lerp(z0, z1 + point * _g(az, 9), v) + tilt * back
        k = math.sqrt(max(0.05, 1 - ((z - HEAD.z) / rz) ** 2))
        x, y = ring(HEAD.x, HEAD.y, rx * k + 0.003, ry * k + 0.003, az)
        return x, y, z
    return Shell(P + name, f, nu, 4, thick=0.005, kind=kind)


def g_yoke(P, name='Mantelete', kind='cloth', r_out=(0.31, 0.19), z_in=1.52, drop=0.075, tips=0, tip=0.0):
    """Mantelete sobre os ombros (cobre a pele entre a gola e as ombreiras)."""
    def f(u, v):
        a = _lerp(-180, 180, u)
        t = 1 - v
        tp = (abs(math.sin(math.pi * tips * u)) ** 0.6) if tips else 1.0
        z = z_in - drop * t ** 2.5 - tip * t * (1 - tp)
        rx = _lerp(0.115, r_out[0], t)
        ry = _lerp(0.1, r_out[1], t)
        x, y = ring(0, 0.016, rx, ry, a)
        return x, y, z
    return Shell(P + name, f, 64, 8, thick=0.005, kind=kind)


# ================================================================ VÉU (Arcanista do Véu) — túnica longa de mago
def set_veu():
    P = 'KV_'
    out = []
    s = _add(out, g_torso(P, 'Tunica', 0.86, 1.47, 0.006, 'cloth', flare=0.03, nu=56, nv=16), 'Armadura')
    s.border(sides='b', r=0.0026)
    s.stitches('b', 0.08)
    # tabardo da frente (até a canela) e das costas (até o chão)
    s = _add(out, g_panel(P, 'TabardoF', 0, 24, 1.30, 0.34, push=0.018, flare=0.05, point=0.05, nu=14, nv=26), 'Armadura')
    s.border()
    s.border(inset=0.1, r=0.0015)
    _sigil(s, 0.5, 0.58, 0.16, 0.06)
    s.add_uv_tube([(0.5, 0.12), (0.5, 0.5)], 0.0018, 'rune', False, False)
    s.add_uv_tube([(0.5, 0.66), (0.5, 0.95)], 0.0018, 'rune', False, False)
    s = _add(out, g_panel(P, 'TabardoC', 180, 40, 1.38, 0.2, push=0.02, flare=0.07, point=0.04, nu=20, nv=26), 'Armadura')
    s.border()
    s.border(inset=0.08, r=0.0015)
    _sigil(s, 0.5, 0.62, 0.1, 0.05)
    # faixa larga com nó e pontas caindo do lado
    s = _add(out, g_belt(P, 0.885, 0.965, 0.216, 0.15, 'cloth', 'Faixa'), 'Armadura')
    s.border(sides='bt', r=0.0018)
    for k, (ac, L) in enumerate(((30, 0.5), (40, 0.42))):
        s = _add(out, g_panel(P, 'Ponta%d' % k, ac, 5, 0.9, 0.9 - L, push=0.03, flare=0.01, point=0.03, nu=4, nv=12), 'Armadura')
        s.border(sides='b', r=0.0016)
    s = _add(out, g_buckle(P, w=9, name='Medalhao'), 'Armadura')
    _sigil(s, 0.5, 0.5, 0.25, 0.28)
    # gola alta de prata e ouro aberta na frente
    def collar(u, v):
        a = _lerp(-150, 150, u) + 180
        z = _lerp(1.43, 1.62 - 0.05 * _g(a - 180, 120) + 0.03 * _g(a - 180, 40), v)
        r = _lerp(0.19, 0.16, v) + 0.03 * v * v
        x, y = ring(0, 0.03, r, r * 0.9, a)
        return x, y, z
    s = _add(out, g_yoke(P), 'Armadura')
    s.border(sides='b', r=0.0024)
    s.stitches('b', 0.15)
    s = _add(out, Shell(P + 'Gola', collar, 44, 8, thick=0.006, kind='steel'), 'Armadura')
    s.border()
    s.rivet_row('b', 0.15, 10, 0.004, (0.05, 0.95))
    # medalhão do peito (broche com runa)
    def brooch(u, v):
        a = _lerp(-14, 14, u)
        z = _lerp(1.27, 1.35, v)
        rx, ry = _torso_r(z, -0.02)
        x, y = ring(0, 0.012, rx + 0.02, ry + 0.022 + 0.01 * math.sin(u * math.pi) * math.sin(v * math.pi), a)
        return x, y, z
    s = _add(out, Shell(P + 'Broche', brooch, 10, 8, thick=0.006, kind='goldplate'), 'Armadura')
    s.border(r=0.0016)
    _sigil(s, 0.5, 0.5, 0.25, 0.3)
    for side in ('L', 'R'):
        sgn = 1 if side == 'L' else -1
        g_pauldron(P, side, R=0.115, SC=1.1, lames=1, kind='steel', fin=False, rune=True,
                   spikes=((90, 0.5, 0.05, 0.012),), out=out)
        L = 'Luva' + side
        s = _add(out, g_sleeve(P, side, 0.84, 1.46, 0.078, 0.086, 'cloth', 'Sino', bell=0.07, drop=0.08, folds=0.004, nu=36, nv=14, thick=0.005), L)
        s.border(sides='b', r=0.0026)
        s.border(inset=0.12, r=0.0014, sides='b')
        s = _add(out, g_bracer(P, side, 0.8, 0.95, 0.012, 'steel', 'Bracal', keel=0.004, r0=0.058), L)
        s.border(sides='bt')
        s.add_uv_tube([(0.41, 0.25), (0.41, 0.75)], 0.0016, 'rune', False, False)
        _add(out, g_glove(P, side, 'leather'), L)
        C = 'Calca' + side
        s = _add(out, g_robe_leg(P, side, 0.13, 0.94, 0.06, 'cloth', 'Saia', back=0.05), C)
        s.border(sides='b', r=0.0026)
        s.border(inset=0.08, r=0.0014, sides='b')
        s.stitches('b', 0.16)
        B = 'Bota' + side
        s = _add(out, g_foot(P, side, 'leather', point=0.75, length=0.215, name='Sapato', lift=0.012), B)
        s.border(sides='l', r=0.002)
        s = _add(out, g_ankle(P, side, 'leather'), B)
        s.border(sides='t', r=0.002)
        for k in range(3):
            s.add_uv_tube([(i / 16, 0.15 + 0.25 * k + 0.12 * math.sin(i / 16 * 2 * math.pi)) for i in range(17)], 0.0026, 'strap', False, False)
    # capuz com borda dourada e diadema com gema
    s = _add(out, g_hood(P, 'Capuz', 'cloth', peak=0.06), 'Elmo')
    s.border(sides='lrb', r=0.0028)
    s.border(inset=0.05, r=0.0014, sides='lr')
    s = _add(out, g_band(P, 'Diadema', 1.735, 1.75, 0.13, 0.15, 'goldplate', point=0.025), 'Elmo')
    s.border(sides='t', r=0.0014)
    _sigil(s, 0.5, 0.6, 0.012, 0.35)
    return out


# ================================================================ TRAMA (Lâmina Arcana) — meia armadura assimétrica laqueada
def set_trama():
    P = 'KT_'
    out = []
    _add(out, g_torso(P, 'Gibao', 0.9, 1.48, -0.004, 'leather', nu=40, nv=10, thick=0.004, top=0.07), 'Armadura')
    f, b = g_cuirass(P, add=0.008, keel=0.02, pecs=0.032)
    for sh in (f, b):
        _add(out, sh, 'Armadura')
        sh.border()
        sh.border(inset=0.06, r=0.0016)
    f.rivet_row('l', 0.03, 7, 0.0048, (0.12, 0.85))
    f.rivet_row('r', 0.03, 7, 0.0048, (0.12, 0.85))
    # runa magenta em V no peito, filigrana dourada
    f.add_uv_tube([(0.36, 0.78), (0.5, 0.52), (0.64, 0.78)], 0.0022, 'rune', False, False)
    f.add_uv_tube([(0.5, 0.18), (0.5, 0.45)], 0.0018, 'rune', False, False)
    for sg in (-1, 1):
        f.add_uv_tube([(0.5 + sg * (0.04 + 0.12 * t), 0.4 + 0.25 * t + 0.05 * math.sin(t * 6)) for t in [i / 14 for i in range(15)]], 0.0016, 'gold', False, False)
    b.add_uv_tube([(0.5, 0.25), (0.5, 0.8)], 0.0018, 'rune', False, False)
    s = _add(out, g_gorget(P, 'steel', 1.41, 1.5, 0.205, 0.135), 'Armadura')
    s.border(sides='bt')
    s = _add(out, g_belt(P), 'Armadura')
    s.border(sides='bt', r=0.0022)
    s.rivet_row('b', 0.5, 20, 0.0042, (0.02, 0.98))
    s = _add(out, g_buckle(P), 'Armadura')
    _sigil(s, 0.5, 0.5, 0.2, 0.3)
    # abas de tabardo (pano escuro) na frente e atrás, com escarcelas laqueadas nos lados
    for nm, ac, w, zb in (('AbaF', 0, 15, 0.42), ('AbaC', 180, 22, 0.36)):
        s = _add(out, g_panel(P, nm, ac, w, 0.9, zb, push=0.02, flare=0.04, point=0.06, nu=10, nv=20), 'Armadura')
        s.border()
        _sigil(s, 0.5, 0.55, 0.15, 0.06)
    for side in (1, -1):
        for j in range(2):
            zt = 0.895 - 0.07 * j
            s = _add(out, g_panel(P, 'Escarcela%s%d' % ('L' if side > 0 else 'R', j), 60, 26, zt, zt - 0.1,
                                  push=0.012 + 0.006 * j, flare=0.05, point=0.02 + 0.015 * j, kind='steel', sgn=side, nu=14, nv=6), 'Armadura')
            s.border()
            s.rivet_row('t', 0.15, 3, 0.0042, (0.2, 0.8))
    # assimetria: ombreira grande no ombro esquerdo (escudo), pequena no direito
    g_pauldron(P, 'L', R=0.158, SC=1.32, lames=3, kind='steel', fin=True, rune=True,
               spikes=((90, 0.42, 0.11, 0.022), (90, 0.82, 0.075, 0.017), (35, 0.7, 0.05, 0.013), (145, 0.7, 0.05, 0.013)), out=out)
    g_pauldron(P, 'R', R=0.11, SC=1.1, lames=1, kind='steel', out=out)
    for side in ('L', 'R'):
        L = 'Luva' + side
        _add(out, g_sleeve(P, side, 1.0, 1.46, 0.07, 0.084, kind='leather'), L)
        s = _add(out, g_couter(P, side), L)
        s.border()
        s.rivets([(0.5, 0.5)], 0.0075)
        s = _add(out, g_bracer(P, side, flare=0.03), L)
        s.border(sides='bt')
        s.border(inset=0.14, r=0.0014, sides='bt')
        s.add_uv_tube([(0.41, 0.25), (0.41, 0.8)], 0.0018, 'rune', False, False)
        s.rivet_row('t', 0.08, 6, 0.004, (0.05, 0.95))
        _add(out, g_glove(P, side), L)
        g_handlames(P, side, 3, 'steel', out, L)
        C = 'Calca' + side
        _add(out, g_under(P, side), C)
        s = _add(out, g_cuisse(P, side), C)
        s.border()
        s.border(inset=0.09, r=0.0014)
        s.rivet_row('t', 0.1, 6, 0.0042, (0.1, 0.9))
        s = _add(out, g_knee(P, side), C)
        s.border()
        s.add_uv_tube([(0.41, 0.3), (0.41, 0.7)], 0.0018, 'rune', False, False)
        B = 'Bota' + side
        s = _add(out, g_greave(P, side, flare=0.03), B)
        s.border(sides='bt')
        s.border(inset=0.08, r=0.0014, sides='bt')
        s.add_uv_tube([(0.5, 0.2), (0.5, 0.75)], 0.0018, 'rune', False, False)
        s.rivet_row('t', 0.06, 10, 0.004, (0.02, 0.98))
        s = _add(out, g_ankle(P, side), B)
        s.border(sides='t')
        s = _add(out, g_foot(P, side, 'steel', point=0.8, length=0.22), B)
        s.border()
    # tiara dourada com ponta e gema (o Lâmina luta de rosto descoberto)
    s = _add(out, g_band(P, 'Tiara', 1.728, 1.752, 0.132, 0.15, 'goldplate', point=0.04), 'Elmo')
    s.border(sides='bt', r=0.0014)
    _sigil(s, 0.5, 0.55, 0.014, 0.3)
    for sg in (1, -1):                                     # asinhas laterais
        def wing(u, v, sg=sg):
            az = sg * _lerp(80, 125, u)
            z = _lerp(1.735, 1.755 + 0.05 * u, v)
            x, y = ring(HEAD.x, HEAD.y, 0.118 + 0.012 * u, 0.13 + 0.012 * u, az)
            return x, y, z + 0.02 * u * u
        _add(out, Shell(P + 'TiaraAsa' + ('L' if sg > 0 else 'R'), wing, 8, 3, thick=0.004, kind='goldplate'), 'Elmo')
    return out


# ================================================================ BASTIÃO (Comandante do Estandarte) — placa pesada negra e ouro
def set_bastiao():
    P = 'KB_'
    out = []
    _add(out, g_torso(P, 'Gibao', 0.9, 1.48, -0.004, 'leather', nu=40, nv=10, thick=0.004, top=0.07), 'Armadura')
    f, b = g_cuirass(P, add=0.014, keel=0.024, pecs=0.036, zb=0.96)
    for sh in (f, b):
        _add(out, sh, 'Armadura')
        sh.border()
        sh.border(inset=0.05, r=0.0018)
        sh.border(inset=0.1, r=0.0013)
    f.rivet_row('l', 0.03, 8, 0.0052, (0.1, 0.88))
    f.rivet_row('r', 0.03, 8, 0.0052, (0.1, 0.88))
    # brasão: coroa dourada sobre losango aceso
    f.add_uv_tube([(0.4, 0.72), (0.42, 0.8), (0.46, 0.75), (0.5, 0.83), (0.54, 0.75), (0.58, 0.8), (0.6, 0.72), (0.4, 0.72)], 0.0026, 'gold', True, False)
    _sigil(f, 0.5, 0.6, 0.06, 0.08)
    for sg in (-1, 1):
        f.add_uv_tube([(0.5 + sg * (0.07 + 0.16 * t), 0.64 + 0.08 * math.sin(t * 2.6) - 0.1 * t) for t in [i / 14 for i in range(15)]], 0.002, 'gold', False, False)
    b.rivet_row('b', 0.07, 10, 0.0048)
    s = _add(out, g_gorget(P, 'steel', 1.4, 1.5, 0.215, 0.14, 'Gorjal0'), 'Armadura')
    s.border(sides='b')
    s = _add(out, g_gorget(P, 'goldplate', 1.47, 1.535, 0.15, 0.105, 'Gorjal1'), 'Armadura')
    for i, (z0, z1) in enumerate(((0.955, 1.02), (0.915, 0.975))):   # faixas do abdome
        def lame(u, v, z0=z0, z1=z1, i=i):
            a = _lerp(-118, 118, u)
            z = _lerp(z0 - 0.012 * _g(a, 30), z1, v)
            x, y = ring(0, 0.012, 0.2 + 0.004 * i + 0.01 * (1 - v), 0.134 + 0.004 * i + 0.01 * (1 - v), a)
            return x, y, z
        s = _add(out, Shell(P + 'Faixa%d' % i, lame, 56, 5, thick=0.006), 'Armadura')
        s.border(sides='b')
    s = _add(out, g_belt(P, 0.88, 0.935, 0.215, 0.148), 'Armadura')
    s.border(sides='bt', r=0.0024)
    s.rivet_row('b', 0.5, 22, 0.0045, (0.02, 0.98))
    s = _add(out, g_buckle(P, 0.865, 0.955, 14, 0.224, 0.16), 'Armadura')
    _sigil(s, 0.5, 0.5, 0.2, 0.3)
    for side in (1, -1):                                    # escarcelas grandes pontudas (3 lâminas)
        for k, (ac, w, nl) in enumerate(((27, 25, 3), (78, 20, 2))):
            for j in range(nl):
                zt = 0.895 - 0.072 * j
                s = _add(out, g_panel(P, 'Escarcela%s%d%d' % ('L' if side > 0 else 'R', k, j), ac, w, zt, zt - 0.105,
                                      push=0.01 + 0.006 * j, flare=0.06, point=0.03 if j == nl - 1 else 0.012, kind='steel', sgn=side, nu=14, nv=6), 'Armadura')
                s.border()
                s.rivet_row('t', 0.15, 3, 0.0045, (0.2, 0.8))
    for side in ('L', 'R'):
        g_pauldron(P, side, R=0.16, SC=1.34, lames=3, kind='steel', fin=False, rune=True, crown=5,
                   spikes=((90, 0.42, 0.1, 0.02),), out=out)
    # capa longa roxa com friso dourado
    def cape(u, v):
        t = 1 - v
        z = 1.42 - 1.12 * t
        uu = u * 2 - 1
        half = 0.2 + 0.22 * t ** 0.8
        y = 0.135 + 0.05 * (1 - uu * uu) * (1 - t) + 0.17 * t ** 1.5 + 0.035 * abs(uu) * t
        y += 0.018 * t * math.sin(uu * 7.5 + 0.6) + 0.007 * t * math.sin(uu * 17)
        return uu * half, y, z
    s = _add(out, Shell(P + 'Capa', cape, 30, 36, thick=0.006, kind='cloth', flip=True), 'Armadura')
    s.border(sides='lrb', r=0.0045)
    s.border(inset=0.05, r=0.0018, sides='lrb')
    _sigil(s, 0.5, 0.55, 0.08, 0.06)
    for side in ('L', 'R'):
        L = 'Luva' + side
        _add(out, g_sleeve(P, side), L)

        def rere(u, v, side=side):
            a = _lerp(-80, 260, u)
            x, y = ring(ARM[0], ARM[1], 0.084 + 0.006 * v, 0.084 + 0.006 * v, a)
            return x, y, _lerp(1.115, 1.275, v)
        s = _add(out, Shell(P + 'Braco' + side, rere, 40, 8, sgn=1 if side == 'L' else -1, thick=0.005), L)
        s.border(sides='bt')
        s.rivet_row('b', 0.15, 6, 0.0042, (0.05, 0.95))
        s = _add(out, g_couter(P, side, wing=0.046), L)
        s.border()
        s.rivets([(0.5, 0.5)], 0.0085)
        s = _add(out, g_bracer(P, side, flare=0.036, keel=0.012), L)
        s.border(sides='bt')
        s.border(inset=0.12, r=0.0015, sides='bt')
        s.add_uv_tube([(0.41, 0.25), (0.41, 0.8)], 0.0018, 'rune', False, False)
        s.rivet_row('t', 0.08, 6, 0.0042, (0.05, 0.95))
        _add(out, g_glove(P, side), L)
        g_handlames(P, side, 3, 'steel', out, L)
        C = 'Calca' + side
        _add(out, g_under(P, side), C)
        s = _add(out, g_cuisse(P, side, r0=0.124), C)
        s.border()
        s.border(inset=0.09, r=0.0015)
        s.rivet_row('t', 0.1, 7, 0.0045, (0.1, 0.9))
        s = _add(out, g_knee(P, side, wing=0.05), C)
        s.border()
        s.rivets([(0.41, 0.5)], 0.0075)
        s.add_uv_tube([(0.62, 0.3), (0.62, 0.7)], 0.0018, 'rune', False, False)
        B = 'Bota' + side
        s = _add(out, g_greave(P, side, flare=0.034), B)
        s.border(sides='bt')
        s.border(inset=0.08, r=0.0015, sides='bt')
        s.add_uv_tube([(0.5, 0.2), (0.5, 0.75)], 0.0018, 'rune', False, False)
        s.rivet_row('t', 0.06, 10, 0.0042, (0.02, 0.98))
        s = _add(out, g_ankle(P, side), B)
        s.border(sides='t')
        s = _add(out, g_foot(P, side, 'steel', point=0.8, length=0.22), B)
        s.border()
        s.rivet_row('l', 0.5, 4, 0.004, (0.35, 0.95))
    # elmo fechado: viseira com fenda acesa, coroa de pontas douradas e penacho roxo
    def helm(u, v):
        az = _lerp(-180, 180, u)
        el = _lerp(-0.95, 1.5707, v)
        fl = 0.022 * _sm(-0.55, -0.95, el)
        keel = 0.012 * _g(az, 7) * _sm(-0.6, 0.2, el)
        return _sph(az, el, 0.134 + fl, 0.15 + fl + keel, 0.166)
    s = _add(out, Shell(P + 'Elmo', helm, 56, 22, thick=0.007), 'Elmo')
    s.border(sides='b')
    s.border(inset=0.06, r=0.0016, sides='b')
    s.add_uv_tube([(0.5 + 0.14 * (t - 0.5), 0.47 + 0.02 * math.cos((t - 0.5) * 3)) for t in [i / 16 for i in range(17)]], 0.0034, 'rune', False, False)
    s.add_uv_tube([(0.5, 0.15), (0.5, 0.4)], 0.0018, 'rune', False, False)
    for k in range(9):                                      # respiradouros
        s.rivets([(0.5 + (k - 4) * 0.012, 0.3)], 0.0028)
    s.rivet_row('b', 0.12, 16, 0.0042, (0.0, 1.0))
    def crown(u, v):
        az = _lerp(-180, 180, u)
        tip = abs(math.sin(math.pi * 7 * u)) ** 6
        z = _lerp(1.735, 1.77 + 0.045 * tip, v)
        x, y = ring(HEAD.x, HEAD.y, 0.104 + 0.004 * v, 0.118 + 0.004 * v, az)
        return x, y, z
    s = _add(out, Shell(P + 'Coroa', crown, 84, 5, thick=0.005, kind='goldplate'), 'Elmo')
    s.border(sides='b', r=0.0018)
    s.rivet_row('b', 0.4, 7, 0.006, (0.0, 1.0), kind='goldplate')
    def plume(u, v):
        el = _lerp(1.35, 3.6, u)
        h = 0.06 * math.sin(_lerp(0.25, math.pi, u)) ** 0.7 + 0.008
        rr = 1.0 + (h / 0.166) * v + 0.25 * _sm(0.6, 1.0, u)
        y = -0.15 * math.cos(el) * rr
        z = 0.166 * math.sin(el) * rr + 0.03 * v
        return (0.0, HEAD.y + y, HEAD.z + z)
    _add(out, Shell(P + 'Penacho', plume, 36, 6, thick=0.016, kind='cloth'), 'Elmo')
    return out


# ================================================================ ECOS (Invocadora dos Ecos) — vestido em camadas, espartilho e capuz
def set_ecos():
    P = 'KO_'
    out = []
    s = _add(out, g_torso(P, 'Corpete', 0.9, 1.47, 0.004, 'cloth', flare=0.02, nu=48, nv=14), 'Armadura')
    s.border(sides='b', r=0.0022)

    def corset(u, v):                                       # espartilho esmaltado com cintura marcada
        a = _lerp(-80, 80, u)
        z = _lerp(0.93, 1.27 - 0.05 * _g(a, 25) + 0.04 * _g(abs(a) - 45, 15), v)
        rx, ry = _torso_r(z, -0.02)
        pinch = -0.008 * _g(z - 1.03, 0.05)
        x, y = ring(0, 0.012, rx + 0.018 + pinch, ry + 0.02 + pinch, a)
        return x, y, z
    s = _add(out, Shell(P + 'Espartilho', corset, 34, 14, thick=0.006), 'Armadura')
    s.border()
    s.border(inset=0.08, r=0.0014)
    for k in range(6):                                      # amarração dourada em X no meio
        v0 = 0.12 + 0.13 * k
        s.add_uv_tube([(0.46, v0), (0.54, v0 + 0.13)], 0.0013, 'gold', False, False)
        s.add_uv_tube([(0.54, v0), (0.46, v0 + 0.13)], 0.0013, 'gold', False, False)
    s.rivet_row('l', 0.04, 5, 0.004, (0.1, 0.9))
    s.rivet_row('r', 0.04, 5, 0.004, (0.1, 0.9))
    # joia do peito
    def jewel(u, v):
        a = _lerp(-12, 12, u)
        z = _lerp(1.3, 1.38, v)
        rx, ry = _torso_r(z, -0.02)
        x, y = ring(0, 0.012, rx + 0.016, ry + 0.02 + 0.012 * math.sin(u * math.pi) * math.sin(v * math.pi), a)
        return x, y, z
    s = _add(out, Shell(P + 'Joia', jewel, 10, 8, thick=0.006, kind='goldplate'), 'Armadura')
    s.border(r=0.0015)
    _sigil(s, 0.5, 0.5, 0.25, 0.3)
    s = _add(out, g_belt(P, 0.89, 0.935, 0.214, 0.148, 'goldplate', 'Cinto'), 'Armadura')
    s.border(sides='bt', r=0.0015)
    # sobressaia em pétalas (4 painéis) por cima da saia longa
    for k, (ac, w) in enumerate(((0, 28), (60, 26), (120, 26), (180, 30))):
        for side in ((1,) if ac in (0, 180) else (1, -1)):
            nm = 'Petala%d%s' % (k, '' if ac in (0, 180) else ('L' if side > 0 else 'R'))
            s = _add(out, g_panel(P, nm, ac, w, 0.9, 0.56 if ac in (0, 180) else 0.6, push=0.03, flare=0.07, point=0.07,
                                  sgn=side, folds=0.004, nu=14, nv=14), 'Armadura')
            s.border(sides='lrb', r=0.0022)
            s.border(inset=0.1, r=0.0012, sides='b')
            if ac in (0, 180):
                _sigil(s, 0.5, 0.45, 0.1, 0.07)
    s = _add(out, g_yoke(P, tips=8, tip=0.03), 'Armadura')
    s.border(sides='b', r=0.002)
    # gola alta em leque atrás da cabeça
    for k in range(5):
        def petal(u, v, k=k):
            ac = 180 + (k - 2) * 30
            a = ac + _lerp(-16, 16, u)
            z = _lerp(1.43, 1.6 + 0.05 * math.sin(u * math.pi) - 0.025 * abs(k - 2), v)
            r = 0.17 + 0.07 * v * v
            x, y = ring(0, 0.035, r, r * 0.9, a)
            return x, y, z
        s = _add(out, Shell(P + 'Leque%d' % k, petal, 10, 8, thick=0.005, kind='steel'), 'Armadura')
        s.border(sides='lrt', r=0.0018)
    for side in ('L', 'R'):
        g_pauldron(P, side, R=0.12, SC=1.1, lames=1, kind='steel', rune=True, out=out)
        L = 'Luva' + side
        s = _add(out, g_sleeve(P, side, 0.92, 1.46, 0.076, 0.086, 'cloth', 'SinoA', bell=0.05, drop=0.05, nu=32, nv=12, thick=0.005), L)
        s.border(sides='b', r=0.0022)
        s = _add(out, g_sleeve(P, side, 0.78, 1.0, 0.07, 0.074, 'cloth', 'SinoB', bell=0.075, drop=0.12, folds=0.004, nu=36, nv=8, thick=0.005), L)
        s.border(sides='b', r=0.0024)
        s.border(inset=0.15, r=0.0012, sides='b')
        s = _add(out, g_bracer(P, side, 0.8, 0.86, 0.004, 'goldplate', 'Pulseira', a0=-180, a1=180, keel=0.0, r0=0.06), L)
        s.add_uv_tube([(0.62, 0.5), (0.68, 0.5)], 0.003, 'rune', False, False)
        _add(out, g_glove(P, side, 'leather'), L)
        C = 'Calca' + side
        s = _add(out, g_robe_leg(P, side, 0.12, 0.94, 0.065, 'cloth', 'Saia', folds=0.008, back=0.04), C)
        s.border(sides='b', r=0.0024)
        s.stitches('b', 0.1)
        B = 'Bota' + side
        s = _add(out, g_greave(P, side, kind='steel', z1=0.4, flare=0.01, name='Cano'), B)
        s.border(sides='bt', r=0.0018)
        s.add_uv_tube([(0.5, 0.25), (0.5, 0.8)], 0.0016, 'rune', False, False)
        s = _add(out, g_ankle(P, side, 'leather'), B)
        s = _add(out, g_foot(P, side, 'steel', point=0.85, length=0.225, lift=0.015), B)
        s.border(r=0.0016)
    # capuz claro com tiara de lua crescente
    s = _add(out, g_hood(P, 'Capuz', 'cloth', peak=0.03, gap=42, drape=0.07), 'Elmo')
    s.border(sides='lrb', r=0.0026)
    s.stitches('l', 0.06)
    s.stitches('r', 0.06)
    s = _add(out, g_band(P, 'Tiara', 1.735, 1.748, 0.13, 0.15, 'goldplate', point=0.012), 'Elmo')

    def crescent(u, v):
        t = _lerp(-1, 1, u)
        ang = t * 1.1
        rr = 0.034 - 0.012 * v * (1 - t * t)
        x = rr * math.sin(ang)
        z = 1.765 + rr * math.cos(ang) - 0.02
        y = HEAD.y - 0.132 - 0.004 * v
        return x, y, z
    s = _add(out, Shell(P + 'Lua', crescent, 16, 3, thick=0.004, kind='goldplate'), 'Elmo')
    s.add_uv_tube([(0.5, 0.0), (0.5, 0.02)], 0.004, 'rune', False, False)
    return out


# ================================================================ ÍGNEO (Punho de Brasa) — colete de couro aberto, faixa e punhos de ferro
def set_igneo():
    P = 'KI_'
    out = []
    for side in (1, -1):                                    # colete aberto na frente (duas bandas)
        def vest(u, v):
            a = _lerp(14, 180, u)
            zt = 1.45 - 0.14 * _sm(40, 14, a) - 0.17 * _sm(60, 95, a) * _sm(140, 100, a)
            z = _lerp(0.93, zt, v)
            rx, ry = _torso_r(z, 0.0)
            x, y = ring(0, 0.012, rx + 0.014, ry + 0.016, a)
            return x, y, z
        s = _add(out, Shell(P + 'Colete' + ('L' if side > 0 else 'R'), vest, 36, 16, sgn=side, thick=0.006, kind='leather'), 'Armadura')
        s.border(r=0.0022)
        s.stitches('l', 0.05)
        s.stitches('b', 0.08)
        s.rivet_row('l', 0.1, 5, 0.0045, (0.15, 0.8))
    # gola de couro grossa nos ombros
    s = _add(out, g_gorget(P, 'leather', 1.42, 1.5, 0.21, 0.15, 'Gola'), 'Armadura')
    s.border(sides='b', r=0.0024)
    # faixa larga de pano com nó e pontas na frente
    s = _add(out, g_belt(P, 0.86, 0.99, 0.22, 0.155, 'cloth', 'Faixa'), 'Armadura')
    s.border(sides='bt', r=0.0016)
    for k, (ac, L) in enumerate(((-18, 0.34), (-8, 0.28))):
        s = _add(out, g_panel(P, 'Ponta%d' % k, ac, 5, 0.9, 0.9 - L, push=0.034, flare=0.012, point=0.025, nu=4, nv=10), 'Armadura')
        s.border(sides='b', r=0.0014)
    s = _add(out, g_belt(P, 0.835, 0.875, 0.222, 0.156, 'leather', 'Cinto'), 'Armadura')
    s.rivet_row('b', 0.5, 18, 0.0042, (0.02, 0.98))
    s = _add(out, g_buckle(P, 0.825, 0.89, 14, 0.228, 0.165), 'Armadura')
    _sigil(s, 0.5, 0.5, 0.2, 0.3)
    s = _add(out, g_panel(P, 'Tanga', 0, 16, 0.84, 0.5, push=0.03, flare=0.03, point=0.04, nu=8, nv=12), 'Armadura')
    s.border(sides='lrb', r=0.002)
    _sigil(s, 0.5, 0.6, 0.15, 0.08)
    # ombreira de ferro só no ombro esquerdo (guarda) + alça de couro cruzando o peito
    g_pauldron(P, 'L', R=0.14, SC=1.18, lames=2, kind='steel', rune=True,
               spikes=((90, 0.42, 0.09, 0.02), (40, 0.72, 0.05, 0.013), (140, 0.72, 0.05, 0.013)), out=out)

    # alça diagonal: tubo de couro do ombro esquerdo até o quadril direito, pela frente e pelas costas
    tor = Shell(P + 'Alca', lambda u, v: (0.0, 0.0, 0.0), 3, 2, thick=0.0, kind='strap')
    tor.pos_mirror = False
    for back in (False, True):
        pts = []
        for i in range(25):
            t = i / 24
            z = _lerp(1.43, 0.95, t)
            a = _lerp(45 if not back else 135, -60 if not back else -120 + 360, t)
            rx, ry = _torso_r(z, 0.0)
            x, y = ring(0, 0.012, rx + 0.022, ry + 0.024, a)
            pts.append(Vector((x, y, z)))
        tor.tubes.append((pts, 0.007, False, 'strap'))
    tor.tubes_only = True
    _add(out, tor, 'Armadura')
    for side in ('L', 'R'):
        L = 'Luva' + side
        s = _add(out, g_bracer(P, side, 0.8, 1.02, 0.01, 'leather', 'Faixas', a0=-180, a1=180, keel=0.0, r0=0.06), L)
        for k in range(6):                                  # ataduras em diagonal
            v0 = 0.04 + 0.15 * k
            s.add_uv_tube([(i / 18, v0 + 0.1 * i / 18) for i in range(19)], 0.0034, 'strap', False, False)
        _add(out, g_glove(P, side, 'leather'), L)
        g_handlames(P, side, 2, 'steel', out, L, knuckles=4)

        def knuckle(u, v, side=side):                       # soqueira de ferro nos nós dos dedos
            a = _lerp(20, 160, u)
            z = _lerp(0.672, 0.712, v)
            x, y = ring(0.307, -0.017, 0.058, 0.08, a)
            return x, y, z
        s = _add(out, Shell(P + 'Soqueira' + side, knuckle, 16, 3, sgn=1 if side == 'L' else -1, thick=0.007), L)
        s.border()
        C = 'Calca' + side
        sg = 1 if side == 'L' else -1

        def baggy(u, v):                                    # calça larga que estufa no joelho e prende na canela
            a = _lerp(-180, 180, u)
            z = _lerp(0.3, 0.94, v)
            r = 0.1 + 0.03 * _g(z - 0.55, 0.16) - 0.02 * _sm(0.42, 0.3, z) + 0.004 * math.sin(a * D2R * 6) * _g(z - 0.5, 0.15)
            w = _sm(0.0, -0.5, math.sin(a * D2R))
            rx = r * (1 - w) + min(r, 0.106) * w
            x, y = ring(0.112, 0.0, rx, r * 1.04, a)
            return x, y, z
        s = _add(out, Shell(P + 'Calca' + side, baggy, 40, 16, sgn=sg, thick=0.005, kind='cloth'), C)
        s.stitches('l', 0.5, 0.03, 0.001, (0.05, 0.95))
        B = 'Bota' + side
        s = _add(out, g_boot(P, side, 'leather', z1=0.4, cuff=0.02), B)
        s.border(sides='t', r=0.0024)
        s.strap((0.25, 0.7), (0.75, 0.7), 0.005, 20, True)
        s.strap((0.25, 0.45), (0.75, 0.45), 0.005, 20, True)
        s = _add(out, g_foot(P, side, 'leather', point=0.55, length=0.2), B)
        s.border(sides='l', r=0.002)

        def toecap(u, v, side=side):                        # biqueira de ferro
            a = _lerp(-95, 95, u)
            y = _lerp(-0.12, -0.205, v)
            t = max(0.0, min(1.0, (0.06 - y) / 0.26))
            w = 0.065 * (1 - _sm(0.66, 1.0, t) * 0.55) + 0.006
            h = 0.15 - 0.075 * _sm(0.15, 0.6, t) - 0.04 * _sm(0.72, 1.0, t) + 0.005
            return 0.12 + w * math.sin(a * D2R), y, 0.004 + h * max(0.0, math.cos(a * D2R)) ** 0.8
        s = _add(out, Shell(P + 'Biqueira' + side, toecap, 16, 6, sgn=sg, flip=True, thick=0.005), B)
        s.border()
    # bandana com nó e pontas caindo atrás
    s = _add(out, g_band(P, 'Bandana', 1.715, 1.76, 0.134, 0.152, 'cloth'), 'Elmo')
    s.border(sides='bt', r=0.0016)
    _sigil(s, 0.5, 0.5, 0.012, 0.3)
    for k, (dx, L) in enumerate(((0.02, 0.17), (-0.015, 0.13))):
        def tail(u, v, dx=dx, L=L):
            t = 1 - v
            x = dx + _lerp(-0.012, 0.012, u) * (1 - 0.4 * t) + 0.03 * t * t * (1 if dx > 0 else -1)
            y = HEAD.y + 0.14 + 0.05 * t
            z = 1.76 - L * t
            return x, y, z
        _add(out, Shell(P + 'Laco%d' % k, tail, 3, 8, thick=0.004, kind='cloth'), 'Elmo')
    return out


# ================================================================ montagem e exportação
def build_cls(set_name, maker):
    shells = maker()
    GRP.clear()
    objs = {}
    for s in shells:
        GRP[s.name] = s.grp
        if not getattr(s, 'tubes_only', False):
            objs[s.name] = s.build()
        for t in tubes_obj(s.name + '_T', s.tubes):
            objs[t.name] = t
        for e in s.extra:
            GRP[e.name] = s.grp
            objs[e.name] = e.build()
    paint6(objs, set_name)
    return objs


PIVOTS_CLS = {'Armadura': (0, 0, 0), 'LuvaL': (0.31, 0, 1.40), 'LuvaR': (-0.31, 0, 1.40),
          'CalcaL': (0.12, 0, 0.92), 'CalcaR': (-0.12, 0, 0.92), 'BotaL': (0.12, 0, 0.92),
          'BotaR': (-0.12, 0, 0.92), 'Elmo': (0, 0, 1.47)}


def groups_cls(objs):
    keys = sorted(GRP, key=len, reverse=True)
    out = {g: [] for g in PIVOTS_CLS}
    for nm in objs:
        for k in keys:
            if nm.startswith(k):
                out[GRP[k]].append(nm)
                break
    return {g: (tuple(v), PIVOTS_CLS[g]) for g, v in out.items()}


CLS_MAKERS = {'Veu': set_veu, 'Trama': set_trama, 'Bastiao': set_bastiao, 'Ecos': set_ecos, 'Igneo': set_igneo}
