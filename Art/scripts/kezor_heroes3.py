# Kezor — heróis v3: anatomia orgânica mid poly (fim do visual "de bloquinhos").
# Cada parte (Body, Head, ArmL, ArmR, LegL, LegR) vira uma superfície contínua feita por "loft" de seções
# superelípticas: tronco com peito, cintura e quadril; braço com deltoide, bíceps, cotovelo e antebraço; perna com
# coxa, joelho e panturrilha; cabeça com crânio, maçãs, queixo, nariz, orelhas e olhos; cabelo como casca.
# Mantém pivôs e nomes das partes (animações procedurais e equipamentos continuam valendo) e as mesmas medidas
# gerais do v2 (as peças de armadura continuam servindo). Roda depois de kezor_heroes2 e kezor_midpoly.
import bmesh, bpy, math, random
from mathutils import Vector

SEG = 14   # lados por seção (o passe mid poly ainda suaviza as normais)


def _se(a, p):
    """Superelipse: p = 2 elipse, p > 2 mais quadrada."""
    c, s = math.cos(a), math.sin(a)
    return math.copysign(abs(c) ** (2.0 / p), c), math.copysign(abs(s) ** (2.0 / p), s)


def loft(b, rings, m, n=SEG, caps=True, arc=None):
    """rings = [(cx, cy, z, rx, ry[, p]), ...] de baixo para cima; m = material ou lista (uma por faixa entre seções).
    raio 0 vira ponta. Fecha as pontas com leque. arc = (a0, a1) em graus (0 = frente, 90 = +X) faz uma superfície
    aberta (capuz, viseira), sem tampas."""
    if arc is not None:
        return _loft_arc(b, rings, m, n, arc)
    pts, idx = [], []
    for r in rings:
        cx, cy, z, rx, ry = r[:5]
        p = r[5] if len(r) > 5 else 2.4
        if rx <= 1e-5 and ry <= 1e-5:
            idx.append([len(pts)]); pts.append((cx, cy, z)); continue
        ring = []
        for i in range(n):
            a = -math.pi / 2 + 2 * math.pi * i / n          # começa na frente (-Y)
            ux, uy = _se(a, p)
            ring.append(len(pts)); pts.append((cx + ux * rx, cy + uy * ry, z))
        idx.append(ring)
    faces, fm = [], []
    for k in range(len(idx) - 1):
        A, Bv = idx[k], idx[k + 1]
        mm = m[k] if isinstance(m, (list, tuple)) else m
        for i in range(n):
            j = (i + 1) % n
            if len(A) == 1 and len(Bv) == 1:
                continue
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
        if len(idx[0]) > 1:
            c = len(pts); r0 = rings[0]; pts.append((r0[0], r0[1], r0[2]))
            for i in range(n):
                faces.append((c, idx[0][(i + 1) % n], idx[0][i])); fm.append(m0)
        if len(idx[-1]) > 1:
            c = len(pts); r1 = rings[-1]; pts.append((r1[0], r1[1], r1[2]))
            for i in range(n):
                faces.append((c, idx[-1][i], idx[-1][(i + 1) % n])); fm.append(m1)
    return b.poly(pts, faces, fm)


def _loft_arc(b, rings, m, n, arc):
    a0, a1 = [math.radians(v) for v in arc]
    pts, idx = [], []
    for r in rings:
        cx, cy, z, rx, ry = r[:5]
        p = r[5] if len(r) > 5 else 2.4
        ring = []
        for i in range(n + 1):
            a = -math.pi / 2 + a0 + (a1 - a0) * i / n
            ux, uy = _se(a, p)
            ring.append(len(pts)); pts.append((cx + ux * rx, cy + uy * ry, z))
        idx.append(ring)
    faces, fm = [], []
    for k in range(len(idx) - 1):
        A, Bv = idx[k], idx[k + 1]
        mm = m[k] if isinstance(m, (list, tuple)) else m
        for i in range(n):
            faces.append((A[i], A[i + 1], Bv[i + 1], Bv[i])); fm.append(mm)
    # espessura: segunda camada um pouco para dentro, faces invertidas (o tecido não fica "de papel")
    off = len(pts)
    for (x, y, z), r in zip(list(pts), [rr for rr in rings for _ in range(n + 1)]):
        cx, cy = r[0], r[1]
        pts.append((cx + (x - cx) * 0.93, cy + (y - cy) * 0.93, z))
    for k in range(len(idx) - 1):
        mm = m[k] if isinstance(m, (list, tuple)) else m
        for i in range(n):
            A, Bv = idx[k], idx[k + 1]
            faces.append((off + Bv[i], off + Bv[i + 1], off + A[i + 1], off + A[i])); fm.append(mm)
    for k in range(len(idx) - 1):   # bordas laterais
        A, Bv = idx[k], idx[k + 1]
        mm = m[k] if isinstance(m, (list, tuple)) else m
        faces.append((A[0], Bv[0], off + Bv[0], off + A[0])); fm.append(mm)
        faces.append((Bv[n], A[n], off + A[n], off + Bv[n])); fm.append(mm)
    for i in range(n):              # borda de baixo e de cima
        A = idx[0]; Z = idx[-1]
        mm0 = m[0] if isinstance(m, (list, tuple)) else m
        mm1 = m[-1] if isinstance(m, (list, tuple)) else m
        faces.append((A[i + 1], A[i], off + A[i], off + A[i + 1])); fm.append(mm0)
        faces.append((Z[i], Z[i + 1], off + Z[i + 1], off + Z[i])); fm.append(mm1)
    return b.poly(pts, faces, fm)


def grow(rings, k=1.0, add=0.0, cy=0.0):
    """Mesma silhueta, maior (casca de armadura por cima do corpo)."""
    out = []
    for r in rings:
        rr = list(r)
        if rr[3] > 1e-5:
            rr[3] = rr[3] * k + add
            rr[4] = rr[4] * k + add
        rr[1] += cy
        out.append(tuple(rr))
    return out


def blob(b, c, r, m, n=10, rings=6, sq=(1, 1, 1)):
    """Elipsoide (olhos, maçãs, nós dos dedos)."""
    cx, cy, cz = c
    rs = []
    for k in range(rings + 1):
        t = -math.pi / 2 + math.pi * k / rings
        rr = math.cos(t)
        rs.append((cx, cy, cz + math.sin(t) * r * sq[2], rr * r * sq[0], rr * r * sq[1], 2.0))
    return loft(b, rs, m, n=n, caps=False)


def _smooth(f):
    return [p.use_smooth for p in f]


# ================================================================ tronco
def body3(b, shirt, belt, hips, buckle, w=1.0, skin=None):
    shD, shL = tone(shirt, 0.72), tone(shirt, 1.15)
    beltD = tone(belt, 0.7)
    W = w
    rings = [   # (cx, cy, z, rx, ry, p)
        (0, 0.005, 0.90, 0.150 * W, 0.100, 2.6),   # virilha
        (0, 0.000, 0.96, 0.178 * W, 0.115, 2.6),   # quadril
        (0, 0.000, 1.03, 0.172 * W, 0.112, 2.5),
        (0, 0.000, 1.10, 0.158 * W, 0.104, 2.4),   # cintura
        (0, -0.004, 1.18, 0.172 * W, 0.112, 2.4),
        (0, -0.010, 1.26, 0.198 * W, 0.124, 2.5),  # peito
        (0, -0.006, 1.33, 0.205 * W, 0.120, 2.7),
        (0, 0.000, 1.385, 0.190 * W, 0.104, 3.0),  # ombros
        (0, 0.006, 1.42, 0.120 * W, 0.080, 2.4),   # trapézio
        (0, 0.010, 1.445, 0.062, 0.055, 2.0),      # base do pescoço
    ]
    mats = [hips, hips, belt, shD, shirt, shirt, shirt, shirt, shD]
    loft(b, rings, mats)
    # cinto: anel um pouco saliente, fivela e bolsa
    loft(b, [(0, 0, 1.045, 0.177 * W, 0.116, 2.5), (0, 0, 1.05, 0.183 * W, 0.121, 2.5),
             (0, 0, 1.09, 0.176 * W, 0.115, 2.4), (0, 0, 1.095, 0.168 * W, 0.109, 2.4)], belt)
    cbox(b, (0, -0.118, 1.07), (0.064, 0.016, 0.046), buckle, bev=0.008)
    cbox(b, (0.15 * W, -0.075, 1.02), (0.07, 0.06, 0.08), beltD, bev=0.02)
    # peitoral e gola: volume suave por cima da camisa
    for xs in (-1, 1):
        blob(b, (xs * 0.082 * W, -0.104, 1.29), 0.07, shirt, sq=(1.25, 0.22, 0.8))
    loft(b, [(0, 0.008, 1.40, 0.088, 0.072, 2.2), (0, 0.008, 1.43, 0.082, 0.066, 2.2), (0, 0.01, 1.455, 0.07, 0.058, 2.0)], shD, caps=False)


# ================================================================ braço
def arm3(b, x, sleeve, fore, hand, w=1.0, bracer=None):
    s = 1 if x > 0 else -1
    in_ = -s * 0.018   # o ombro encosta no tronco
    k = w
    rings = [
        (x + in_ * 1.4, 0.0, 1.300, 0.070 * k, 0.074 * k),   # axila
        (x + in_, 0.0, 1.250, 0.062 * k, 0.066 * k),         # bíceps
        (x + in_ * 0.6, 0.004, 1.170, 0.058 * k, 0.062 * k),
        (x + in_ * 0.3, 0.006, 1.075, 0.048 * k, 0.052 * k), # cotovelo
        (x, 0.000, 1.020, 0.052 * k, 0.056 * k),
        (x, -0.004, 0.960, 0.056 * k, 0.058 * k),            # antebraço
        (x, -0.004, 0.880, 0.044 * k, 0.047 * k),
        (x, -0.004, 0.815, 0.036 * k, 0.040 * k),            # pulso
    ]
    loft(b, rings, [sleeve, sleeve, sleeve, fore, fore, fore, fore])
    # deltoide: cúpula do ombro
    loft(b, [(x + in_ * 1.4, 0, 1.285, 0.074 * k, 0.078 * k, 2.2), (x + in_ * 1.2, 0, 1.36, 0.080 * k, 0.082 * k, 2.2),
             (x + in_, 0, 1.415, 0.058 * k, 0.062 * k, 2.2), (x + in_ * 0.8, 0, 1.44, 0.0, 0.0)], tone(sleeve, 1.1), caps=True)
    if bracer is not None:
        loft(b, [(x, -0.004, 0.83, 0.042 * k, 0.046 * k), (x, -0.004, 0.835, 0.047 * k, 0.05 * k),
                 (x, -0.004, 0.905, 0.053 * k, 0.056 * k), (x, -0.004, 0.91, 0.048 * k, 0.052 * k)], bracer)
    hand3(b, x, hand, k)


def hand3(b, x, skin, k=1.0):
    s = 1 if x > 0 else -1
    hd = tone(skin, 0.86)
    # palma e dedos: mão fechada levemente (mitene com nós)
    loft(b, [(x, -0.004, 0.815, 0.032 * k, 0.038 * k, 2.4), (x, -0.006, 0.785, 0.034 * k, 0.046 * k, 2.8),
             (x, -0.010, 0.745, 0.033 * k, 0.050 * k, 3.0), (x, -0.014, 0.712, 0.030 * k, 0.046 * k, 3.0),
             (x, -0.016, 0.69, 0.022 * k, 0.034 * k, 2.6), (x, -0.016, 0.68, 0.0, 0.0)], [skin, skin, skin, hd, hd])
    for i in range(4):   # nós dos dedos
        blob(b, (x + s * 0.004, -0.036 * k + i * 0.022 * k, 0.712), 0.012 * k, hd, n=8, rings=4)
    loft(b, [(x - s * 0.03 * k, -0.03, 0.775, 0.013 * k, 0.014 * k), (x - s * 0.038 * k, -0.045, 0.742, 0.012 * k, 0.012 * k),
             (x - s * 0.04 * k, -0.055, 0.72, 0.009 * k, 0.009 * k), (x - s * 0.04 * k, -0.058, 0.712, 0.0, 0.0)], skin, n=8)


# ================================================================ perna
def leg3(b, x, pants, shin, boot, cuff=None):
    pd = tone(pants, 0.8)
    bootL, bootD = tone(boot, 1.18), tone(boot, 0.62)
    s = 1 if x > 0 else -1
    rings = [
        (x - s * 0.01, 0.000, 0.975, 0.098, 0.110),   # topo da coxa (dentro do quadril)
        (x, 0.000, 0.880, 0.094, 0.104),
        (x, -0.004, 0.760, 0.082, 0.090),
        (x, -0.010, 0.620, 0.064, 0.072),             # acima do joelho
        (x, -0.016, 0.545, 0.058, 0.066),             # joelho
        (x, 0.002, 0.470, 0.062, 0.074),              # panturrilha
        (x, 0.004, 0.390, 0.058, 0.066),
        (x, 0.000, 0.300, 0.046, 0.052),
    ]
    loft(b, rings, [pants, pants, pants, pd, shin, shin, shin])
    blob(b, (x, -0.058, 0.55), 0.034, pd, sq=(1.2, 0.6, 1.0))   # joelho
    # bota: cano, dobra, pé com bico arredondado e sola
    loft(b, [(x, 0.0, 0.07, 0.056, 0.062, 2.6), (x, 0.0, 0.16, 0.054, 0.060, 2.6), (x, 0.0, 0.25, 0.058, 0.064, 2.6),
             (x, 0.0, 0.32, 0.062, 0.068, 2.6)], boot)
    loft(b, [(x, 0.0, 0.27, 0.064, 0.07, 2.6), (x, 0.0, 0.275, 0.07, 0.076, 2.6), (x, 0.0, 0.32, 0.072, 0.078, 2.6),
             (x, 0.0, 0.325, 0.064, 0.07, 2.6)], cuff or bootL)
    b.tf(loc=(x, 0.03, 0.055), rot=(90, 0, 0))    # pé ao longo de -Y (frente)
    loft(b, [(0, 0.0, -0.02, 0.05, 0.048, 2.6), (0, 0.0, 0.04, 0.058, 0.052, 2.8), (0, -0.004, 0.11, 0.06, 0.044, 3.0),
             (0, -0.012, 0.17, 0.052, 0.032, 2.8), (0, -0.016, 0.2, 0.034, 0.02, 2.4), (0, -0.018, 0.21, 0.0, 0.0)],
         [boot, boot, boot, bootL, bootL])
    b.reset()
    cbox(b, (x, -0.055, 0.006), (0.118, 0.27, 0.014), bootD, bev=0.006)   # sola


# ================================================================ cabeça
def head3(b, skin, hair=None, style='short', eyes=None, beard=False):
    eyes = eyes or EYE_I
    sd, sl = tone(skin, 0.86), tone(skin, 1.06)
    loft(b, [(0, 0.012, 1.40, 0.054, 0.05, 2.0), (0, 0.010, 1.47, 0.05, 0.048, 2.0), (0, 0.004, 1.52, 0.056, 0.054, 2.0)], skin, caps=False)   # pescoço
    rings = [   # crânio e rosto contínuos (frente = -Y)
        (0, -0.020, 1.468, 0.000, 0.000),
        (0, -0.030, 1.475, 0.040, 0.034, 2.2),   # queixo
        (0, -0.018, 1.500, 0.074, 0.078, 2.4),   # maxilar
        (0, -0.008, 1.540, 0.094, 0.102, 2.5),
        (0, -0.004, 1.585, 0.104, 0.114, 2.5),   # maçãs
        (0, 0.000, 1.630, 0.110, 0.120, 2.4),    # olhos/têmporas
        (0, 0.004, 1.680, 0.108, 0.120, 2.3),    # testa
        (0, 0.008, 1.725, 0.090, 0.104, 2.2),
        (0, 0.010, 1.755, 0.056, 0.068, 2.1),
        (0, 0.010, 1.768, 0.000, 0.000),
    ]
    loft(b, rings, skin, n=16)
    # nariz, maçãs, sobrancelha, boca, orelhas
    loft(b, [(0, -0.112, 1.575, 0.016, 0.012, 2.0), (0, -0.124, 1.585, 0.017, 0.014, 2.0),
             (0, -0.122, 1.615, 0.010, 0.010, 2.0), (0, -0.114, 1.64, 0.0, 0.0)], sl, n=8)
    for xs in (-1, 1):
        blob(b, (xs * 0.058, -0.092, 1.572), 0.028, skin, sq=(1.1, 0.5, 0.8))
        blob(b, (xs * 0.05, -0.112, 1.622), 0.017, EYE_W, n=10, rings=5, sq=(1.25, 0.45, 0.75))
        blob(b, (xs * 0.048, -0.119, 1.622), 0.009, eyes, n=8, rings=4, sq=(1.0, 0.5, 1.0))
        blob(b, (xs * 0.05, -0.114, 1.647), 0.022, sd, n=8, rings=4, sq=(1.4, 0.45, 0.32))   # arco da sobrancelha
        loft(b, [(xs * 0.108, 0.012, 1.575, 0.008, 0.02, 2.2), (xs * 0.116, 0.014, 1.60, 0.012, 0.03, 2.2),
                 (xs * 0.116, 0.016, 1.635, 0.010, 0.026, 2.2), (xs * 0.11, 0.016, 1.65, 0.0, 0.0)], sd, n=8)   # orelha
        if hair is not None:
            blob(b, (xs * 0.05, -0.117, 1.656), 0.02, tone(hair, 0.8), n=8, rings=4, sq=(1.5, 0.35, 0.35))   # sobrancelha
    blob(b, (0, -0.104, 1.535), 0.018, MOUTH, n=8, rings=4, sq=(1.5, 0.3, 0.3))
    if beard and hair is not None:
        loft(b, [(0, -0.05, 1.455, 0.03, 0.03), (0, -0.05, 1.48, 0.07, 0.068), (0, -0.03, 1.53, 0.098, 0.09),
                 (0, -0.02, 1.565, 0.104, 0.1), (0, -0.01, 1.58, 0.0, 0.0)], hair, n=14)
    if hair is None:
        return
    hair3(b, hair, style)


def hair3(b, hair, style):
    hd, hl = tone(hair, 0.75), tone(hair, 1.2)
    # casca do couro cabeludo: cobre do alto da testa para trás
    cap = [(0, 0.018, 1.60, 0.112, 0.112, 2.3), (0, 0.012, 1.66, 0.118, 0.126, 2.3), (0, 0.010, 1.71, 0.106, 0.118, 2.2),
           (0, 0.010, 1.75, 0.078, 0.09, 2.1), (0, 0.010, 1.778, 0.0, 0.0)]
    if style == 'spiky':
        cap = [(0, 0.02, 1.62, 0.108, 0.108, 2.3), (0, 0.012, 1.68, 0.116, 0.124, 2.3), (0, 0.01, 1.73, 0.09, 0.1, 2.2),
               (0, 0.01, 1.76, 0.0, 0.0)]
    loft(b, cap, hair, n=16)
    # franja
    loft(b, [(0.012, -0.096, 1.665, 0.07, 0.016, 2.2), (0.014, -0.106, 1.69, 0.088, 0.024, 2.2),
             (0.01, -0.09, 1.725, 0.07, 0.03, 2.2), (0.0, -0.07, 1.745, 0.0, 0.0)], hl, n=12)
    if style in ('short', 'crop', 'band'):
        loft(b, [(0, 0.075, 1.555, 0.088, 0.05, 2.4), (0, 0.082, 1.60, 0.106, 0.06, 2.4), (0, 0.07, 1.66, 0.11, 0.07, 2.4),
                 (0, 0.05, 1.70, 0.0, 0.0)], hd, n=12)
    if style == 'long':
        loft(b, [(0, 0.1, 1.33, 0.07, 0.03, 2.6), (0, 0.11, 1.42, 0.11, 0.05, 2.6), (0, 0.105, 1.52, 0.122, 0.07, 2.4),
                 (0, 0.085, 1.62, 0.12, 0.08, 2.3), (0, 0.06, 1.70, 0.0, 0.0)], hair, n=14)
        for xs in (-1, 1):
            loft(b, [(xs * 0.11, 0.0, 1.45, 0.018, 0.04, 2.4), (xs * 0.115, 0.0, 1.55, 0.024, 0.06, 2.4),
                     (xs * 0.112, 0.01, 1.65, 0.02, 0.07, 2.4), (xs * 0.1, 0.02, 1.69, 0.0, 0.0)], hd, n=10)
    if style == 'tail':
        loft(b, [(0, 0.1, 1.56, 0.08, 0.05, 2.4), (0, 0.095, 1.62, 0.1, 0.06, 2.4), (0, 0.07, 1.68, 0.0, 0.0)], hd, n=12)
        b.tf(loc=(0, 0.125, 1.64), rot=(-160, 0, 0))
        loft(b, [(0, 0, 0, 0.028, 0.028, 2), (0, 0, 0.03, 0.034, 0.034, 2), (0, 0.02, 0.14, 0.03, 0.03, 2),
                 (0, 0.05, 0.25, 0.016, 0.016, 2), (0, 0.06, 0.29, 0.0, 0.0)], [hd, hair, hair, hair], n=10)
        b.reset()
    if style == 'spiky':
        rnd = random.Random(7)
        for row, (yy, nn) in enumerate(((-0.06, 4), (0.0, 5), (0.06, 5), (0.1, 4))):
            for i in range(nn):
                xx = (i - (nn - 1) / 2) * 0.046
                b.tf(loc=(xx, yy, 1.72 - row * 0.012), rot=(-30 + row * 28, xx * 200, 0))
                h = 0.07 + rnd.random() * 0.04
                loft(b, [(0, 0, 0, 0.024, 0.02, 2), (0, 0.004, h * 0.5, 0.014, 0.012, 2), (0, 0.01, h, 0.0, 0.0)],
                     hl if (i + row) % 2 else hair, n=8)
                b.reset()
    if style == 'band':
        loft(b, [(0, 0.004, 1.655, 0.114, 0.124, 2.3), (0, 0.004, 1.685, 0.116, 0.126, 2.3)], tone(hair, 0.5, '_band'), caps=False)


# ================================================================ troca no construtor
def use_organic():
    """Substitui as partes do corpo do v2 (caixas) pelas orgânicas; base_body e os NPCs passam a usar estas."""
    g = globals()
    g['body2'], g['arm2'], g['leg2'], g['head2'] = body3, arm3, leg3, head3

    def std_body3(b, shirt, belt, hips, w=1.0):
        body3(b, shirt, belt, hips, GOLD, w=w)

    def std_arm3(b, x, sleeve, fore, hand, w=1.0):
        arm3(b, x, sleeve, fore, hand, w=w)

    def std_leg3(b, x, pants, shin, boot, cuff=None):
        leg3(b, x, pants, shin, boot, cuff)

    def std_head3(b, skin, hair=None, eyes=None):
        head3(b, skin, hair=hair, style='short', eyes=EYE_I if eyes is None or eyes is DARK else eyes)

    g['std_body'], g['std_arm'], g['std_leg'], g['std_head'] = std_body3, std_arm3, std_leg3, std_head3


def organic_preview(name='PreviewHero'):
    """Monta um corpo de teste (sem classe) para conferir a anatomia."""
    use_organic()
    shirt = mat('PrevShirt', '#2c5a66'); pants = mat('PrevPants', '#2a2e38'); boots = mat('PrevBoots', '#4a3226')
    hair = mat('PrevHair', '#2a1c14')
    P = hparts()
    body3(P['Body'], shirt, LEATHER, pants, GOLD)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        arm3(P[part], 0.31 * side, shirt, SKIN2, SKIN2, bracer=LEATHER_L)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        leg3(P[part], 0.12 * side, pants, pants, boots)
    head3(P['Head'], SKIN2, hair=hair, style='spiky')
    coll = collection('Preview3')
    return finish(name, 'Classes', coll, P)


# ================================================================ conjuntos de armadura orgânicos
# Cascas que seguem a anatomia do corpo v3 (um pouco maiores), com faixas de material por região. Os adornos de cada
# estilo (ombreiras em camadas, capas, filigranas, cristas) continuam vindo do detail_piece (kezor_style3).
TORSO = [(0, 0.005, 0.90, 0.150, 0.100, 2.6), (0, 0.000, 0.96, 0.178, 0.115, 2.6), (0, 0.000, 1.03, 0.172, 0.112, 2.5),
         (0, 0.000, 1.10, 0.158, 0.104, 2.4), (0, -0.004, 1.18, 0.172, 0.112, 2.4), (0, -0.010, 1.26, 0.198, 0.124, 2.5),
         (0, -0.006, 1.33, 0.205, 0.120, 2.7), (0, 0.000, 1.385, 0.190, 0.104, 3.0), (0, 0.006, 1.42, 0.120, 0.080, 2.4),
         (0, 0.010, 1.445, 0.075, 0.065, 2.0)]


def armor3(set_name):
    c1, c2, c3, glow = SETS[set_name]
    st = STYLE.get(set_name, set_name)

    def build(b):
        if st == 'Veu':   # túnica longa até as canelas, com barra e faixa
            robe = [(0, 0.01, 0.05, 0.30, 0.24, 2.3), (0, 0.01, 0.10, 0.295, 0.235, 2.3), (0, 0.0, 0.45, 0.25, 0.19, 2.4),
                    (0, 0.0, 0.80, 0.205, 0.145, 2.5), (0, 0.0, 0.98, 0.19, 0.128, 2.5)] + grow(TORSO[3:], 1.07, 0.012)
            loft(b, robe, [c3, c1, c1, c1, c1, c1, c1, c1, c1, c2, c2, c2], n=18)
            loft(b, [(0, 0.0, 1.05, 0.186, 0.127, 2.5), (0, 0, 1.06, 0.192, 0.132, 2.5), (0, 0, 1.11, 0.184, 0.124, 2.4),
                     (0, 0, 1.12, 0.176, 0.118, 2.4)], c3, n=16)
            loft(b, [(0, -0.2, 0.08, 0.05, 0.01), (0, -0.2, 0.09, 0.05, 0.012), (0, -0.14, 1.0, 0.04, 0.012), (0, -0.14, 1.01, 0.04, 0.01)], c3, n=8)
            if glow is not None:
                b.gem((0, -0.15, 1.3), 0.04, 0.09, glow)
            return
        k = 1.06 if st == 'Couro' else 1.09
        shell = grow(TORSO[1:], k, 0.012)
        loft(b, shell, [c2, c3, c1, c1, c1, c1, c1, c2], n=18)
        # faldas abertas em leque abaixo do cinto
        loft(b, [(0, 0.0, 0.84, 0.215 * k, 0.15, 2.6), (0, 0.0, 0.88, 0.212 * k, 0.146, 2.6), (0, 0.0, 0.98, 0.196 * k, 0.132, 2.6),
                 (0, 0.0, 1.04, 0.19 * k, 0.128, 2.5)], [tone(c2, 0.85), c2, c2], n=18, caps=False)
        loft(b, [(0, 0, 1.04, 0.19 * k, 0.128, 2.5), (0, 0, 1.045, 0.198 * k, 0.135, 2.5), (0, 0, 1.095, 0.19 * k, 0.127, 2.4),
                 (0, 0, 1.10, 0.182 * k, 0.12, 2.4)], c3, n=18)
        if st == 'Escamas':   # fileiras de escamas no peito
            for row in range(4):
                z = 1.33 - row * 0.055
                for i in range(7):
                    x = (i - 3) * 0.05 + (row % 2) * 0.025
                    blob(b, (x, -0.128 - abs(x) * 0.12, z), 0.03, c2 if (i + row) % 2 else c1, n=8, rings=4, sq=(0.9, 0.35, 1.0))
        if st == 'Runica' and glow is not None:
            loft(b, [(0, -0.138, 1.16, 0.02, 0.006), (0, -0.14, 1.17, 0.03, 0.008), (0, -0.142, 1.33, 0.03, 0.008), (0, -0.14, 1.34, 0.02, 0.006)], glow, n=6)
    return piece(f'{set_name}_Armadura', PV_BODY, build)


def pants3(set_name, side):
    c1, c2, c3, glow = SETS[set_name]
    x = 0.12 * SIDE[side]
    s = SIDE[side]

    def build(b):
        rings = [(x - s * 0.01, 0.0, 0.975, 0.098, 0.110), (x, 0.0, 0.88, 0.094, 0.104), (x, -0.004, 0.76, 0.082, 0.090),
                 (x, -0.010, 0.62, 0.064, 0.072), (x, -0.016, 0.545, 0.058, 0.066), (x, 0.002, 0.47, 0.062, 0.074),
                 (x, 0.004, 0.39, 0.058, 0.066), (x, 0.0, 0.30, 0.046, 0.052)]
        loft(b, grow(rings, 1.1, 0.008), [c1, c1, c1, c2, c2, c2, c2])
        blob(b, (x, -0.074, 0.55), 0.045, c3, sq=(1.15, 0.55, 1.1))   # joelheira
        if glow is not None:
            loft(b, [(x, -0.112, 0.66, 0.012, 0.005), (x, -0.113, 0.67, 0.016, 0.006), (x, -0.104, 0.84, 0.016, 0.006), (x, -0.102, 0.85, 0.012, 0.005)], glow, n=6)
    return piece(f'{set_name}_Calca{side}', PV_LEG[side], build)


def gloves3(set_name, side):
    c1, c2, c3, glow = SETS[set_name]
    x = 0.31 * SIDE[side]

    def build(b):
        loft(b, [(x, -0.004, 0.80, 0.044, 0.048), (x, -0.004, 0.85, 0.054, 0.058), (x, -0.004, 0.95, 0.066, 0.068),
                 (x, 0.0, 1.02, 0.066, 0.07), (x, 0.0, 1.03, 0.06, 0.064)], c1)
        loft(b, [(x, 0, 1.005, 0.068, 0.072), (x, 0, 1.01, 0.072, 0.076), (x, 0, 1.035, 0.07, 0.074), (x, 0, 1.04, 0.064, 0.068)], c3)
        k = 1.14
        loft(b, [(x, -0.004, 0.815, 0.032 * k, 0.038 * k, 2.4), (x, -0.006, 0.785, 0.034 * k, 0.046 * k, 2.8),
                 (x, -0.010, 0.745, 0.033 * k, 0.050 * k, 3.0), (x, -0.014, 0.712, 0.030 * k, 0.046 * k, 3.0),
                 (x, -0.016, 0.69, 0.022 * k, 0.034 * k, 2.6), (x, -0.016, 0.675, 0.0, 0.0)], c2)
        if glow is not None:
            loft(b, [(x, -0.066, 0.86, 0.012, 0.005), (x, -0.068, 0.87, 0.015, 0.006), (x, -0.07, 0.96, 0.015, 0.006), (x, -0.068, 0.97, 0.012, 0.005)], glow, n=6)
    return piece(f'{set_name}_Luva{side}', PV_ARM[side], build)


def boots3(set_name, side):
    c1, c2, c3, glow = SETS[set_name]
    x = 0.12 * SIDE[side]
    st = STYLE.get(set_name, set_name)

    def build(b):
        if st == 'Veu':   # sandálias: sola e tiras
            cbox(b, (x, -0.05, 0.012), (0.13, 0.28, 0.024), c2, bev=0.008)
            for z in (0.07, 0.15, 0.22):
                loft(b, [(x, 0, z, 0.06, 0.066), (x, 0, z + 0.022, 0.06, 0.066)], c3, caps=False)
            return
        loft(b, [(x, 0.0, 0.07, 0.064, 0.07, 2.6), (x, 0.0, 0.16, 0.062, 0.068, 2.6), (x, 0.0, 0.26, 0.066, 0.072, 2.6),
                 (x, 0.0, 0.31, 0.072, 0.078, 2.6)], c2)
        loft(b, [(x, 0, 0.29, 0.076, 0.082, 2.6), (x, 0, 0.295, 0.082, 0.088, 2.6), (x, 0, 0.335, 0.084, 0.09, 2.6),
                 (x, 0, 0.34, 0.076, 0.082, 2.6)], c3)
        b.tf(loc=(x, 0.035, 0.058), rot=(90, 0, 0))
        loft(b, [(0, 0.0, -0.025, 0.058, 0.056, 2.6), (0, 0.0, 0.04, 0.066, 0.06, 2.8), (0, -0.004, 0.115, 0.067, 0.05, 3.0),
                 (0, -0.012, 0.175, 0.058, 0.036, 2.8), (0, -0.016, 0.205, 0.038, 0.022, 2.4), (0, -0.018, 0.215, 0.0, 0.0)], c1)
        b.reset()
        cbox(b, (x, -0.055, 0.008), (0.13, 0.285, 0.016), tone(c2, 0.6), bev=0.006)
    return piece(f'{set_name}_Bota{side}', PV_LEG[side], build)


SKULL = [(0, -0.030, 1.475, 0.040, 0.034, 2.2), (0, -0.018, 1.50, 0.074, 0.078, 2.4), (0, -0.008, 1.54, 0.094, 0.102, 2.5),
         (0, -0.004, 1.585, 0.104, 0.114, 2.5), (0, 0.0, 1.63, 0.110, 0.120, 2.4), (0, 0.004, 1.68, 0.108, 0.120, 2.3),
         (0, 0.008, 1.725, 0.090, 0.104, 2.2), (0, 0.010, 1.755, 0.056, 0.068, 2.1), (0, 0.010, 1.768, 0.0, 0.0)]


def helm3(set_name):
    c1, c2, c3, glow = SETS[set_name]
    st = STYLE.get(set_name, set_name)

    def build(b):
        top = grow(SKULL[3:], 1.16, 0.014, cy=0.004)
        if st == 'Couro':     # capuz de couro justo com abas nas orelhas e aro
            loft(b, [(0, 0.01, 1.585, 0.128, 0.138, 2.4)] + top[1:], c1, n=16)
            for xs in (-1, 1):
                loft(b, [(xs * 0.125, 0.0, 1.52, 0.016, 0.05, 2.4), (xs * 0.13, 0.0, 1.56, 0.02, 0.06, 2.4), (xs * 0.13, 0.0, 1.61, 0.02, 0.065, 2.4),
                         (xs * 0.126, 0.0, 1.63, 0.0, 0.0)], c2, n=10)
            loft(b, [(0, 0.004, 1.6, 0.13, 0.14, 2.4), (0, 0.004, 1.625, 0.134, 0.144, 2.4)], c3, n=16, caps=False)
        elif st == 'Escamas':  # elmo arredondado aberto no rosto, com protetor de nuca
            loft(b, top, c1, n=18)
            loft(b, [(0, 0.03, 1.50, 0.13, 0.13, 2.4), (0, 0.025, 1.56, 0.134, 0.136, 2.4), (0, 0.02, 1.62, 0.132, 0.138, 2.4)],
                 c2, n=14, arc=(70, 290))
            loft(b, [(0, 0.004, 1.615, 0.135, 0.145, 2.4), (0, 0.004, 1.64, 0.138, 0.148, 2.4)], c3, n=18, caps=False)
            loft(b, [(0, 0.01, 1.74, 0.012, 0.11, 2.0), (0, 0.01, 1.80, 0.014, 0.12, 2.0), (0, 0.01, 1.82, 0.0, 0.0)], c3, n=10)   # crista
        elif st == 'Veu':      # capuz de tecido aberto na frente, com ponta caída
            hood = [(0, 0.04, 1.44, 0.15, 0.13, 2.3), (0, 0.035, 1.52, 0.15, 0.15, 2.3), (0, 0.025, 1.62, 0.15, 0.16, 2.3),
                    (0, 0.025, 1.72, 0.13, 0.15, 2.2), (0, 0.03, 1.79, 0.08, 0.1, 2.1)]
            loft(b, hood, c1, n=16, arc=(55, 305))
            loft(b, [(0, 0.03, 1.79, 0.08, 0.1, 2.1), (0, 0.06, 1.83, 0.04, 0.05, 2.0), (0, 0.16, 1.86, 0.0, 0.0)], c1, n=12)
            loft(b, [(0, -0.01, 1.43, 0.15, 0.125, 2.4), (0, -0.01, 1.46, 0.152, 0.128, 2.4)], c3, n=16, caps=False)
            if glow is not None:
                b.gem((0, -0.14, 1.77), 0.026, 0.055, glow)
        else:                  # Rúnica: elmo fechado com viseira de runa e gorjal
            closed = [(0, 0.0, 1.46, 0.10, 0.10, 2.6), (0, -0.006, 1.50, 0.124, 0.134, 2.6)] + top
            loft(b, closed, c1, n=18)
            loft(b, [(0, 0.0, 1.44, 0.13, 0.13, 2.6), (0, 0.0, 1.45, 0.14, 0.14, 2.6), (0, 0.0, 1.50, 0.136, 0.142, 2.6), (0, 0.0, 1.51, 0.128, 0.134, 2.6)], c3, n=18)
            vis = glow or DARK
            loft(b, [(0, -0.14, 1.615, 0.085, 0.01, 3), (0, -0.142, 1.635, 0.085, 0.01, 3)], vis, n=12)
            loft(b, [(0, -0.142, 1.55, 0.012, 0.008), (0, -0.144, 1.615, 0.012, 0.008)], vis, n=8)
    return piece(f'{set_name}_Elmo', PV_HEAD, build)


def use_organic_gear():
    g = globals()
    g['helm'], g['armor'], g['pants'], g['gloves'], g['boots'] = helm3, armor3, pants3, gloves3, boots3


def use_organic_details():
    """Adornos do estilo 3 só no peitoral (ombreiras, capa, emblemas) e no elmo; calças, luvas e botas já têm
    joelheira, punho e dobra na própria casca. Caixas dos adornos ficam bem arredondadas (ORGANIC)."""
    g = globals()
    base = g.get('_detail_style3') or g['detail_piece']
    g['_detail_style3'] = base

    def detail3(name, b):
        s, part = _set_of(name)
        if part in ('Armadura', 'Elmo'):
            base(name, b)
    g['detail_piece'] = detail3
    g['ORGANIC'] = True


def build_organic_all():
    """Corpos base, NPCs e conjuntos com a anatomia e as cascas orgânicas; devolve os nomes para exportar."""
    use_organic(); use_organic_gear(); use_organic_details()
    names = list(rebuild_heroes())
    names += list(rebuild_sets3())
    return names
