"""
kzpaint: pequeno "motor" de pintura procedural para a arte da interface do Kezor.

Tudo é desenhado com campos de distância (SDF) e mapas de altura em numpy: cada peça vira relevo, o relevo vira
normal, e a normal é iluminada como metal (ouro velho, ferro, bronze), pedra ou esmalte. Assim o acabamento
(chanfros, frisos, rebites, gemas, filigrana) sai consistente em todas as peças e pode ser refeito mudando a paleta.

Sem dependências além de numpy e Pillow.
"""
from __future__ import annotations

import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

F = np.float32

# ----------------------------------------------------------------------------------------------- cores e rampas

def hexc(h: str, a: float = 1.0):
    h = h.lstrip('#')
    return np.array([int(h[0:2], 16) / 255, int(h[2:4], 16) / 255, int(h[4:6], 16) / 255], F)


def ramp(stops):
    """stops = [(t, '#rrggbb'), ...] -> função que mapeia array 0..1 em RGB."""
    ts = np.array([s[0] for s in stops], F)
    cs = np.stack([hexc(s[1]) for s in stops])

    def f(v):
        v = np.clip(v, 0, 1)
        return np.stack([np.interp(v, ts, cs[:, i]) for i in range(3)], -1).astype(F)
    return f


GOLD = ramp([(0.0, '#120a04'), (0.28, '#4a2f12'), (0.52, '#8e6630'), (0.72, '#c9a058'), (0.88, '#ecd08e'), (1.0, '#fff4d0')])
GOLD_PALE = ramp([(0.0, '#16100a'), (0.3, '#54401f'), (0.55, '#9a7c45'), (0.78, '#d6bb7f'), (1.0, '#fff8e2')])
BRONZE = ramp([(0.0, '#0d0805'), (0.3, '#3a2614'), (0.55, '#6f4f2c'), (0.8, '#a98552'), (1.0, '#e8d0a0')])
IRON = ramp([(0.0, '#050506'), (0.3, '#17151a'), (0.55, '#2e2a2c'), (0.78, '#57504c'), (1.0, '#a39a90')])
IRON_WARM = ramp([(0.0, '#060504'), (0.32, '#1c1612'), (0.58, '#352a22'), (0.8, '#5e4f42'), (1.0, '#a8957e')])
STEEL = ramp([(0.0, '#06070a'), (0.35, '#1d2126'), (0.6, '#3c434b'), (0.82, '#6f7882'), (1.0, '#d6dee6')])
EMBER_ENAMEL = ramp([(0.0, '#140302'), (0.35, '#4a0c06'), (0.65, '#8c1e0c'), (0.85, '#d24a1c'), (1.0, '#ffb070')])
BLUE_ENAMEL = ramp([(0.0, '#020512'), (0.35, '#0a1a4a'), (0.65, '#1c3f9a'), (0.85, '#4a7ee0'), (1.0, '#bfe0ff')])
STONE = ramp([(0.0, '#050404'), (0.4, '#120e0c'), (0.7, '#211a16'), (1.0, '#3a2f27')])

# ----------------------------------------------------------------------------------------------- grade e SDFs

class Canvas:
    """Imagem RGBA em float (0..1), pré-multiplicada não; composição 'over' simples."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.rgb = np.zeros((h, w, 3), F)
        self.a = np.zeros((h, w), F)
        yy, xx = np.mgrid[0:h, 0:w].astype(F)
        self.x = xx + 0.5
        self.y = yy + 0.5

    def over(self, rgb, a):
        a = np.clip(a, 0, 1).astype(F)
        if rgb.ndim == 1:
            rgb = np.broadcast_to(rgb, (self.h, self.w, 3))
        out_a = a + self.a * (1 - a)
        safe = np.maximum(out_a, 1e-6)[..., None]
        self.rgb = (rgb * a[..., None] + self.rgb * (self.a * (1 - a))[..., None]) / safe
        self.a = out_a

    def add(self, rgb, k):
        """luz aditiva (brilho) — soma cor onde já há pixel e também cria alfa."""
        k = np.clip(k, 0, None).astype(F)
        if rgb.ndim == 1:
            rgb = np.broadcast_to(rgb, (self.h, self.w, 3))
        self.rgb = self.rgb * self.a[..., None] + rgb * k[..., None]
        self.a = np.clip(self.a + k * 0.9, 0, 1)
        self.rgb = self.rgb / np.maximum(self.a, 1e-6)[..., None]
        self.rgb = np.clip(self.rgb, 0, 1)

    def mul(self, k):
        self.rgb *= np.clip(k, 0, 4)[..., None]
        self.rgb = np.clip(self.rgb, 0, 1)

    def image(self, down=1):
        rgb = np.clip(self.rgb, 0, 1)
        a = np.clip(self.a, 0, 1)
        # pré-multiplica para reduzir sem halo, depois desfaz
        pm = np.concatenate([rgb * a[..., None], a[..., None]], -1)
        im = Image.fromarray((pm * 255 + 0.5).astype(np.uint8), 'RGBA')
        if down > 1:
            im = im.resize((self.w // down, self.h // down), Image.LANCZOS)
        arr = np.asarray(im).astype(F) / 255
        al = arr[..., 3:4]
        rgb = np.where(al > 0.002, arr[..., :3] / np.maximum(al, 1e-6), 0)
        out = np.concatenate([np.clip(rgb, 0, 1), al], -1)
        return Image.fromarray((out * 255 + 0.5).astype(np.uint8), 'RGBA')


def sd_box(x, y, cx, cy, hw, hh, r=0.0, ch=0.0):
    """Retângulo (centro, meia-largura/altura) com raio r e/ou chanfro ch. Negativo dentro."""
    qx = np.abs(x - cx) - (hw - r)
    qy = np.abs(y - cy) - (hh - r)
    d = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r
    if ch > 0:
        dc = ((np.abs(x - cx) + np.abs(y - cy)) - (hw + hh - ch)) / math.sqrt(2)
        d = np.maximum(d, dc)
    return d.astype(F)


def sd_rect(x, y, x0, y0, x1, y1, r=0.0, ch=0.0):
    return sd_box(x, y, (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2, r, ch)


def sd_circle(x, y, cx, cy, R):
    return (np.hypot(x - cx, y - cy) - R).astype(F)


def sd_ellipse(x, y, cx, cy, rx, ry):
    k = np.hypot((x - cx) / rx, (y - cy) / ry)
    return ((k - 1) * min(rx, ry)).astype(F)


def sd_poly(x, y, pts):
    """Polígono convexo ou não (distância exata por segmentos + sinal por paridade)."""
    pts = [(float(a), float(b)) for a, b in pts]
    d = np.full(x.shape, 1e9, F)
    s = np.ones(x.shape, F)
    n = len(pts)
    for i in range(n):
        ax, ay = pts[i]
        bx, by = pts[(i + 1) % n]
        ex, ey = bx - ax, by - ay
        wx, wy = x - ax, y - ay
        t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey + 1e-9), 0, 1)
        dx, dy = wx - ex * t, wy - ey * t
        d = np.minimum(d, dx * dx + dy * dy)
        c1 = y >= ay
        c2 = y < by
        c3 = ex * wy > ey * wx
        flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
        s = np.where(flip, -s, s)
    return (s * np.sqrt(d)).astype(F)


def cover(d, aa=1.0):
    """cobertura anti-serrilhada a partir da distância."""
    return np.clip(0.5 - d / aa, 0, 1).astype(F)


def smooth(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)

# ----------------------------------------------------------------------------------------------- ruído

_rng = np.random.default_rng(7)


def noise(w, h, scale, seed=0, octaves=4, persistence=0.5):
    """fBm de ruído de valor (interpolado bicúbico pelo Pillow). Retorna 0..1."""
    rng = np.random.default_rng(seed)
    acc = np.zeros((h, w), F)
    amp, tot = 1.0, 0.0
    s = scale
    for _ in range(octaves):
        gw, gh = max(2, int(w / s) + 3), max(2, int(h / s) + 3)
        g = rng.random((gh, gw)).astype(F)
        im = Image.fromarray((g * 255).astype(np.uint8), 'L').resize((int(gw * s), int(gh * s)), Image.BICUBIC)
        a = np.asarray(im).astype(F)[:h, :w] / 255
        acc += a * amp
        tot += amp
        amp *= persistence
        s = max(1.0, s / 2)
    return acc / tot


def grain(w, h, seed=1):
    return np.random.default_rng(seed).random((h, w)).astype(F)

# ----------------------------------------------------------------------------------------------- relevo e luz

LIGHT = np.array([-0.48, -0.62, 0.62], F)
LIGHT /= np.linalg.norm(LIGHT)


def _box(a, r, axis):
    if r < 1:
        return a
    pad = [(0, 0), (0, 0)]
    pad[axis] = (r + 1, r)
    p = np.pad(a, pad, mode='edge')
    c = np.cumsum(p, axis=axis, dtype=np.float64)
    if axis == 0:
        out = (c[2 * r + 1:, :] - c[:-2 * r - 1, :]) / (2 * r + 1)
    else:
        out = (c[:, 2 * r + 1:] - c[:, :-2 * r - 1]) / (2 * r + 1)
    return out.astype(F)


def blur(a, r):
    """desfoque quase gaussiano (3 caixas)."""
    if r <= 0.3:
        return a.astype(F)
    # largura de caixa para 3 passes aproximar sigma=r
    w = math.sqrt(12 * r * r / 3 + 1)
    rb = max(1, int(round((w - 1) / 2)))
    out = a.astype(F)
    for _ in range(3):
        out = _box(out, rb, 0)
        out = _box(out, rb, 1)
    return out


def normals(h, strength=1.0):
    gy, gx = np.gradient(h * strength)
    n = np.stack([-gx, -gy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n


def light(h, strength=1.0, shin=24.0, light_dir=LIGHT):
    n = normals(h, strength)
    ndl = np.clip((n * light_dir).sum(-1), 0, 1)
    half = light_dir + np.array([0, 0, 1], F)
    half /= np.linalg.norm(half)
    spec = np.clip((n * half).sum(-1), 0, 1) ** shin
    # luz de recorte vinda de baixo-direita (bounce quente) dá volume às peças de metal
    rim_dir = np.array([0.55, 0.65, 0.35], F)
    rim_dir /= np.linalg.norm(rim_dir)
    rim = np.clip((n * rim_dir).sum(-1), 0, 1) ** 3
    return ndl.astype(F), spec.astype(F), rim.astype(F), n


def metal(h, mat=GOLD, strength=1.0, shin=28.0, spec_k=0.55, amb=0.18, rim_k=0.18, rim_col='#ff9a4a'):
    ndl, spec, rim, n = light(h, strength, shin)
    v = amb + (1 - amb) * ndl
    col = mat(v * 0.92)
    col = col + spec[..., None] * spec_k * hexc('#fff3d6')
    col = col + rim[..., None] * rim_k * hexc(rim_col)
    return np.clip(col, 0, 1).astype(F)


def profile_bevel(d, width, round_=True):
    """altura 0..1 subindo da borda (d=0) para dentro (d=-width)."""
    t = np.clip(-d / width, 0, 1)
    if round_:
        return np.sqrt(1 - (1 - t) ** 2).astype(F)
    return t.astype(F)


def bead(d, center, width):
    """friso arredondado (meia-cana) centrado na distância 'center' (negativa = dentro)."""
    t = np.clip(1 - np.abs(d - center) / width, 0, 1)
    return np.sqrt(np.clip(1 - (1 - t) ** 2, 0, 1)).astype(F)

# ----------------------------------------------------------------------------------------------- traço vetorial (filigrana)

class Ink:
    """Máscara desenhada com Pillow em supersample, para filigrana, arabescos e glifos."""

    def __init__(self, w, h, ss=4):
        self.w, self.h, self.ss = w, h, ss
        self.im = Image.new('L', (w * ss, h * ss), 0)
        self.d = ImageDraw.Draw(self.im)

    def _p(self, p):
        return (p[0] * self.ss, p[1] * self.ss)

    def line(self, pts, w0, w1=None, v=255):
        """polilinha com espessura variando de w0 a w1 (pincel redondo)."""
        if w1 is None:
            w1 = w0
        n = len(pts)
        for i in range(n - 1):
            a, b = pts[i], pts[i + 1]
            seg = max(1, int(math.hypot(b[0] - a[0], b[1] - a[1]) * self.ss / 1.5))
            for k in range(seg + 1):
                t = (i + k / seg) / max(1, n - 1)
                r = (w0 + (w1 - w0) * t) * 0.5 * self.ss
                px = (a[0] + (b[0] - a[0]) * k / seg) * self.ss
                py = (a[1] + (b[1] - a[1]) * k / seg) * self.ss
                self.d.ellipse([px - r, py - r, px + r, py + r], fill=v)

    def poly(self, pts, v=255):
        self.d.polygon([self._p(p) for p in pts], fill=v)

    def circle(self, cx, cy, r, v=255):
        s = self.ss
        self.d.ellipse([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], fill=v)

    def ring(self, cx, cy, r, w, v=255):
        s = self.ss
        self.d.ellipse([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], outline=v, width=max(1, int(w * s)))

    def text(self, xy, txt, font, v=255, anchor='mm'):
        self.d.text(self._p(xy), txt, fill=v, font=font, anchor=anchor)

    def mask(self):
        im = self.im.resize((self.w, self.h), Image.LANCZOS)
        return np.asarray(im).astype(F) / 255


def spiral(cx, cy, r0, r1, a0, turns, cw=True, steps=80):
    pts = []
    for i in range(steps + 1):
        t = i / steps
        a = a0 + (1 if cw else -1) * turns * 2 * math.pi * t
        r = r0 + (r1 - r0) * t
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def bezier(p0, p1, p2, p3, steps=40):
    out = []
    for i in range(steps + 1):
        t = i / steps
        u = 1 - t
        out.append((u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
                    u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1]))
    return out


def scroll_arm(ink, x0, y0, x1, y1, curl_r, w0, w1, bend=0.25, cw=True, turns=1.1):
    """braço de arabesco: curva de (x0,y0) até (x1,y1) que termina enrolando numa espiral."""
    dx, dy = x1 - x0, y1 - y0
    nx, ny = -dy * bend, dx * bend
    if not cw:
        nx, ny = -nx, -ny
    pts = bezier((x0, y0), (x0 + dx * 0.33 + nx, y0 + dy * 0.33 + ny), (x0 + dx * 0.66 + nx, y0 + dy * 0.66 + ny), (x1, y1))
    ink.line(pts, w0, (w0 + w1) / 2)
    # espiral: começa tangente ao fim da curva
    ang = math.atan2(y1 - pts[-2][1], x1 - pts[-2][0])
    side = 1 if cw else -1
    cxs = x1 + math.cos(ang + side * math.pi / 2) * curl_r
    cys = y1 + math.sin(ang + side * math.pi / 2) * curl_r
    a0 = math.atan2(y1 - cys, x1 - cxs)
    sp = spiral(cxs, cys, curl_r, curl_r * 0.18, a0, turns, cw=cw)
    ink.line(sp, (w0 + w1) / 2, w1)
    ink.circle(sp[-1][0], sp[-1][1], w1 * 0.9)


def leaf(ink, x, y, ang, length, width):
    """folha pontuda (acanto estilizado)."""
    ca, sa = math.cos(ang), math.sin(ang)
    pts = []
    for i in range(21):
        t = i / 20
        wv = math.sin(t * math.pi) ** 0.8 * width * (1 - t * 0.35)
        pts.append((t * length, wv))
    for i in range(20, -1, -1):
        t = i / 20
        wv = math.sin(t * math.pi) ** 0.8 * width * (1 - t * 0.35)
        pts.append((t * length, -wv * 0.6))
    ink.poly([(x + px * ca - py * sa, y + px * sa + py * ca) for px, py in pts])


def relief(mask, r=1.6, gain=1.0):
    """máscara 0..1 -> altura arredondada (domo) para iluminar como peça fundida."""
    h = blur(mask, r)
    h = np.minimum(h, mask * 1.0 + 0.0)
    return (np.sqrt(np.clip(h, 0, 1)) * gain).astype(F)


def shadow(alpha, r, k=0.8, dx=0, dy=0):
    a = blur(alpha, r) * k
    dx, dy = int(dx), int(dy)
    if dx or dy:
        out = np.zeros_like(a)
        H, W = a.shape
        out[max(0, dy):H + min(0, dy), max(0, dx):W + min(0, dx)] = a[max(0, -dy):H - max(0, dy), max(0, -dx):W - max(0, dx)]
        a = out
    return a

# ----------------------------------------------------------------------------------------------- gemas

def gem(cv: Canvas, cx, cy, rx, ry, mat=EMBER_ENAMEL, facets=8, glow=0.0, glow_col='#ff6a1a', bezel=GOLD, bezel_w=None,
        shape='oval', rot=0.0):
    """gema lapidada em engaste de ouro: facetas, reflexo, brilho interno."""
    x, y = cv.x, cv.y
    if bezel_w is None:
        bezel_w = max(1.5, min(rx, ry) * 0.28)
    if shape == 'oval':
        d_out = sd_ellipse(x, y, cx, cy, rx + bezel_w, ry + bezel_w)
        d_in = sd_ellipse(x, y, cx, cy, rx, ry)
    else:  # losango
        def dia(rx_, ry_):
            return sd_poly(x, y, [(cx, cy - ry_), (cx + rx_, cy), (cx, cy + ry_), (cx - rx_, cy)])
        d_out = dia(rx + bezel_w * 1.4, ry + bezel_w * 1.4)
        d_in = dia(rx, ry)
    if glow > 0:
        g = np.clip(1 - (np.hypot((x - cx) / (rx * 3.2), (y - cy) / (ry * 3.2))), 0, 1) ** 2 * glow
        cv.add(hexc(glow_col), g * 0.55)
    # sombra do engaste
    cv.over(np.zeros(3, F), shadow(cover(d_out), 2.0, 0.6, 1, 2))
    # engaste
    h_b = profile_bevel(d_out, bezel_w * 1.1) * (1 - 0.5 * profile_bevel(d_in + bezel_w * 0.15, bezel_w * 0.5))
    col = metal(h_b * bezel_w * 0.9, bezel, 1.0, 30, 0.7)
    cv.over(col, cover(d_out))
    # pedra: facetas por setor angular + cúpula
    ang = np.arctan2((y - cy) / ry, (x - cx) / rx) + rot
    sector = np.floor((ang + math.pi) / (2 * math.pi) * facets)
    fac = (np.sin(sector * 2.39996) * 0.5 + 0.5) * 0.22
    rr = np.clip(np.hypot((x - cx) / rx, (y - cy) / ry), 0, 1)
    table = (rr < 0.5).astype(F)
    v = 0.25 + 0.55 * (1 - rr) ** 0.7 + fac * (1 - table) + 0.12 * table
    # luz vinda de cima-esquerda dentro da pedra (refração: mais claro embaixo-direita)
    v += 0.25 * np.clip(((x - cx) / rx * 0.5 + (y - cy) / ry * 0.6), -1, 1)
    col = mat(np.clip(v, 0, 1))
    cv.over(col, cover(d_in))
    # reflexos
    hl = np.clip(1 - np.hypot((x - (cx - rx * 0.35)) / (rx * 0.32), (y - (cy - ry * 0.4)) / (ry * 0.22)), 0, 1) ** 1.5
    cv.add(hexc('#ffffff'), hl * cover(d_in) * 0.85)
    sp = np.clip(1 - np.hypot((x - (cx + rx * 0.3)) / (rx * 0.12), (y - (cy + ry * 0.35)) / (ry * 0.1)), 0, 1)
    cv.add(hexc('#ffe0c0'), sp * cover(d_in) * 0.5)


def rivet(cv: Canvas, cx, cy, r, mat=GOLD):
    x, y = cv.x, cv.y
    d = sd_circle(x, y, cx, cy, r)
    cv.over(np.zeros(3, F), shadow(cover(d), r * 0.5, 0.7, r * 0.2, r * 0.35))
    h = profile_bevel(d, r) * r * 0.9
    cv.over(metal(h, mat, 1.0, 40, 0.8), cover(d))


def save(im: Image.Image, path):
    im.save(path, optimize=True)
