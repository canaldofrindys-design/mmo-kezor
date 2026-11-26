# Kezor — esqueleto e animações dos heróis (estilo MMO clássico), feitos no Blender e exportados para o jogo.
#
# O esqueleto usa os mesmos nomes e pivôs do herói no Unity (Body, Head, ArmL, LegL...) e acrescenta as juntas que
# dão a pele que dobra: Chest (cintura), ForeArm (cotovelo), Hand (punho), Shin (joelho) e Foot (tornozelo).
# As poses são montadas em espaço do personagem (cinemática direta no tronco e nos braços, IK analítico de duas
# juntas nas pernas com o pé plantado no chão), gravadas como Actions no Blender (uma por clipe) e exportadas em
# texto para Assets/Resources/Anim/Heroi.txt: para cada quadro, o deslocamento do quadril e a rotação de cada osso
# em relação à pose de repouso, no espaço do personagem (Blender: Z para cima, frente -Y, esquerda +X).
#
# Uso (MCP do Blender, namespace KZ): exec(open(r'...\kezor_anim.py').read(), G); G['anim_build_all']()
import bpy, math, os
from mathutils import Vector, Quaternion, Matrix, Euler

FPS = 30
# cabeça e cauda de cada osso (Blender) e o pai
BONES = [
    ('Body', (0, 0, 0.95), (0, 0, 1.05), None),
    ('Chest', (0, 0, 1.05), (0, 0, 1.40), 'Body'),
    ('Head', (0, 0, 1.47), (0, 0, 1.75), 'Chest'),
    ('ArmL', (0.31, 0, 1.40), (0.31, 0, 1.08), 'Chest'),
    ('ForeArmL', (0.31, 0, 1.08), (0.31, 0, 0.80), 'ArmL'),
    ('HandL', (0.31, 0, 0.80), (0.31, 0, 0.70), 'ForeArmL'),
    ('ArmR', (-0.31, 0, 1.40), (-0.31, 0, 1.08), 'Chest'),
    ('ForeArmR', (-0.31, 0, 1.08), (-0.31, 0, 0.80), 'ArmR'),
    ('HandR', (-0.31, 0, 0.80), (-0.31, 0, 0.70), 'ForeArmR'),
    ('LegL', (0.12, 0, 0.92), (0.12, 0, 0.53), 'Body'),
    ('ShinL', (0.12, 0, 0.53), (0.12, 0, 0.10), 'LegL'),
    ('FootL', (0.12, 0, 0.10), (0.12, -0.15, 0.03), 'ShinL'),
    ('LegR', (-0.12, 0, 0.92), (-0.12, 0, 0.53), 'Body'),
    ('ShinR', (-0.12, 0, 0.53), (-0.12, 0, 0.10), 'LegR'),
    ('FootR', (-0.12, 0, 0.10), (-0.12, -0.15, 0.03), 'ShinR'),
]
BONE_NAMES = [b[0] for b in BONES]
PARENT = {b[0]: b[3] for b in BONES}
HEAD = {b[0]: Vector(b[1]) for b in BONES}
L_THIGH = 0.39
L_SHIN = 0.43
ANKLE_Z = 0.10
DOWN = Vector((0, 0, -1))
FWD = Vector((0, -1, 0))


# ================================================================ rotações no espaço do personagem
# Convenções (frente -Y, esquerda +X): braço/coxa para a frente = rx(-a); cotovelo dobra = rx(-a);
# joelho dobra = rx(+a); tronco/cabeça inclinam para a frente = rx(+a); giro para a esquerda = rz(+a);
# braço esquerdo abre para o lado = ry(-a), o direito = ry(+a); ponta do pé para baixo = rx(+a).
def rx(a): return Quaternion((1, 0, 0), math.radians(a))
def ry(a): return Quaternion((0, 1, 0), math.radians(a))
def rz(a): return Quaternion((0, 0, 1), math.radians(a))
def eul(x=0.0, y=0.0, z=0.0):
    return rz(z) @ ry(y) @ rx(x)


def smooth(a, b, x):
    if b == a:
        return 0.0 if x < a else 1.0
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def cyc(keys, u):
    """Curva periódica (Catmull-Rom) por chaves [(u, valor)], u em [0,1)."""
    u %= 1.0
    n = len(keys)
    for i in range(n):
        u0, v0 = keys[i]
        u1, v1 = keys[(i + 1) % n]
        if i + 1 == n:
            u1 += 1.0
        uu = u if u >= u0 else u + 1.0
        if u0 <= uu < u1:
            t = (uu - u0) / (u1 - u0)
            vm = keys[(i - 1) % n][1]
            vp = keys[(i + 2) % n][1]
            t2, t3 = t * t, t * t * t
            return 0.5 * ((2 * v0) + (-vm + v1) * t + (2 * vm - 5 * v0 + 4 * v1 - vp) * t2 + (-vm + 3 * v0 - 3 * v1 + vp) * t3)
    return keys[0][1]


def ease(keys, t):
    """Curva não periódica com suavização (Hermite com tangente zero nas chaves: golpes com 'peso')."""
    if t <= keys[0][0]:
        return keys[0][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t0 <= t <= t1:
            k = (t - t0) / (t1 - t0) if t1 > t0 else 1.0
            k = k * k * (3 - 2 * k)
            return v0 + (v1 - v0) * k
    return keys[-1][1]


def ease_fast(keys, t):
    """Como ease, mas acelerando no fim do trecho (o corte do golpe chega rápido e pesado)."""
    if t <= keys[0][0]:
        return keys[0][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t0 <= t <= t1:
            k = (t - t0) / (t1 - t0) if t1 > t0 else 1.0
            k = 1 - (1 - k) ** 3 if v1 != v0 else k
            return v0 + (v1 - v0) * k
    return keys[-1][1]


# ================================================================ pose
class Pose:
    """Rotações de cada osso no espaço do personagem (W) e deslocamento do quadril."""
    def __init__(self):
        self.W = {n: Quaternion() for n in BONE_NAMES}
        self.hip = Vector((0, 0, 0))

    def fk(self, name, local):
        p = PARENT[name]
        self.W[name] = (self.W[p] if p else Quaternion()) @ local

    def joint(self, name):
        """Posição atual da cabeça do osso (cinemática direta a partir do quadril)."""
        chain = []
        n = name
        while n:
            chain.append(n)
            n = PARENT[n]
        chain.reverse()
        pos = HEAD['Body'] + self.hip
        for a, b in zip(chain, chain[1:]):
            pos = pos + self.W[a] @ (HEAD[b] - HEAD[a])
        return pos


def leg_ik(P, side, ankle, toe=0.0, pole=None, splay=6.0):
    """IK de duas juntas: coxa e canela até o tornozelo (posição no espaço do personagem); joelho para a frente."""
    leg, shin, foot = 'Leg' + side, 'Shin' + side, 'Foot' + side
    H = P.joint(leg)
    d = Vector(ankle) - H
    dist = d.length
    reach = (L_THIGH + L_SHIN) * 0.998
    if dist > reach:
        d = d.normalized() * reach
        dist = reach
    n = d.normalized()
    sx = 1 if side == 'L' else -1
    pl = (pole or FWD + Vector((sx * math.tan(math.radians(splay)), 0, 0))).normalized()
    p = (pl - n * pl.dot(n))
    if p.length < 1e-5:
        p = FWD.copy()
    p.normalize()
    ca = (L_THIGH ** 2 + dist ** 2 - L_SHIN ** 2) / (2 * L_THIGH * dist)
    a = math.acos(max(-1.0, min(1.0, ca)))
    t = n * math.cos(a) + p * math.sin(a)
    K = H + t * L_THIGH
    s = (H + d - K).normalized()
    Wt = DOWN.rotation_difference(t)
    P.W[leg] = Wt
    P.W[shin] = (Wt @ DOWN).rotation_difference(s) @ Wt
    P.W[foot] = rx(toe) @ rz(sx * splay * 0.5)


def arm(P, side, fwd, out, flex, twist=0.0, wrist=0.0):
    """Braço em relação ao peito: fwd > 0 leva à frente; out abre para o lado; flex dobra o cotovelo."""
    sx = 1 if side == 'L' else -1
    P.fk('Arm' + side, rz(sx * twist) @ ry(-sx * out) @ rx(-fwd))
    P.fk('ForeArm' + side, rx(-flex))
    P.fk('Hand' + side, rx(-wrist))


def torso(P, pitch=0.0, yaw=0.0, roll=0.0, cpitch=0.0, cyaw=0.0, croll=0.0, hpitch=0.0, hyaw=0.0, hroll=0.0):
    P.W['Body'] = eul(pitch, roll, yaw)
    P.fk('Chest', eul(cpitch, croll, cyaw))
    P.fk('Head', eul(hpitch, hroll, hyaw))


# ================================================================ clipes
# Cada clipe: (nome, duração em s, loop, função(t normalizado 0..1) -> Pose)
def idle(u):
    P = Pose()
    b = math.sin(u * 2 * math.pi)                         # respiração (1 ciclo)
    w = math.sin(u * 2 * math.pi + 0.6)                   # troca de peso suave
    P.hip = Vector((w * 0.012, 0.0, -0.035 + b * 0.006))
    torso(P, pitch=2, yaw=-6 + w * 1.5, roll=w * 1.2,
          cpitch=3 + b * 1.4, cyaw=4, croll=-w * 1.0,
          hpitch=-4 - b * 0.8, hyaw=3 + math.sin(u * 4 * math.pi + 1) * 2.5)
    arm(P, 'L', fwd=6 + b * 1.5, out=9 + b * 1.2, flex=16 + b * 3, twist=4)
    arm(P, 'R', fwd=14 + b * 1.5, out=10 + b * 1.0, flex=34 + b * 3, twist=-6, wrist=6)
    leg_ik(P, 'L', (0.15, -0.09, ANKLE_Z), splay=10)       # pé esquerdo um pouco à frente (base de combate)
    leg_ik(P, 'R', (-0.16, 0.08, ANKLE_Z), splay=14)
    return P


def _gait(u, run):
    """Locomoção. u = fase (0 = pé esquerdo tocando o chão; 0,5 = direito). run = 0 anda, 1 corre."""
    P = Pose()
    if run:
        duty, front, back = 0.2, 0.30, -0.32
        swing = [(0.0, (front, 0.0)), (duty, (back, 0.0)), (duty + 0.12, (-0.40, 0.30)), (0.62, (-0.02, 0.40)),
                 (0.82, (0.36, 0.22)), (0.94, (0.36, 0.06))]
        hipk = [(0.0, -0.075), (0.1, -0.095), (0.25, -0.03), (0.37, -0.012), (0.5, -0.075), (0.6, -0.095), (0.75, -0.03), (0.87, -0.012)]
    else:
        duty, front, back = 0.56, 0.30, -0.31          # caminhada firme (vila): passada longa, calcanhar primeiro
        swing = [(0.0, (front, 0.0)), (duty, (back, 0.06)), (0.68, (-0.14, 0.12)), (0.84, (0.18, 0.10)), (0.95, (0.31, 0.03))]
        hipk = [(0.0, -0.055), (0.25, -0.02), (0.5, -0.055), (0.75, -0.02)]

    def foot(ph):
        ph %= 1.0
        if ph < duty:                                     # apoio: o pé fica parado no chão (desliza para trás em relação ao quadril)
            k = ph / duty
            f = front + (back - front) * k
            lift = 0.0
            toe = smooth(0.65, 1.0, k) * (28 if run else 22)          # calcanhar sobe no fim do apoio
            heel = smooth(0.6, 1.0, k) * (0.06 if run else 0.05)
            return f, lift + heel, toe
        f = cyc([(a, v[0]) for a, v in swing], ph)
        z = cyc([(a, v[1]) for a, v in swing], ph)
        k = (ph - duty) / (1 - duty)
        toe = (1 - smooth(0.0, 0.35, k)) * (28 if run else 22) + math.sin(k * math.pi) * (10 if run else 4) - smooth(0.75, 1.0, k) * (12 if run else 14)
        return f, max(0.0, z), toe

    hy = cyc(hipk, u)
    s = math.sin(u * 2 * math.pi)                         # >0 quando a perna esquerda vai à frente (0..0,5)
    P.hip = Vector((s * (0.012 if run else 0.02), -0.03 if run else 0.0, hy))
    lean = 13 if run else 3
    torso(P, pitch=lean * 0.55, yaw=s * (9 if run else 6), roll=-math.cos(u * 4 * math.pi) * (2.0 if run else 1.5),
          cpitch=lean * 0.6 + (math.cos(u * 4 * math.pi) * 2 if run else 0), cyaw=-s * (17 if run else 10), croll=s * 1.5,
          hpitch=-lean * 0.9, hyaw=s * (7 if run else 4))
    sw = 40 if run else 22
    arm(P, 'L', fwd=-s * sw + (8 if run else 2), out=15 if run else 7, flex=(68 if run else 18) + max(0, -s) * (14 if run else 14), twist=-6 if run else 2)
    arm(P, 'R', fwd=s * sw + (10 if run else 6), out=15 if run else 8, flex=(66 if run else 26) + max(0, s) * (14 if run else 14), twist=6 if run else -4, wrist=8)
    for side, ph, x in (('L', u, 0.11), ('R', u + 0.5, -0.11)):
        f, z, toe = foot(ph)
        leg_ik(P, side, (x, -f, ANKLE_Z + z), toe=toe, splay=5)
    return P


def walk(u): return _gait(u, False)
def run(u): return _gait(u, True)


READY = dict(lf=(0.15, -0.16), rf=(-0.17, 0.14))


def _strike(t, keys):
    """Golpe: chaves [(t, dict)] interpoladas com ease; campos ausentes usam a pose de prontidão."""
    base = dict(yaw=-6, pitch=2, cyaw=4, cpitch=4, croll=0, hz=-0.04, hx=0.0, hy=0.0,
                rf=14, ro=10, rx=34, rt=-6, rw=6, lf=6, lo=9, lx=16, lt=4, hp=-4, hyw=3)
    fields = base.keys()
    vals = {}
    for f in fields:
        ks = [(kt, kd.get(f, base[f])) for kt, kd in keys]
        vals[f] = ease(ks, t)
    return vals


def _attack_pose(t, keys, step=0.0):
    v = _strike(t, keys)
    P = Pose()
    P.hip = Vector((v['hx'], -v['hy'], v['hz']))
    torso(P, pitch=v['pitch'], yaw=v['yaw'], cpitch=v['cpitch'], cyaw=v['cyaw'], croll=v['croll'], hpitch=v['hp'], hyaw=v['hyw'])
    arm(P, 'L', fwd=v['lf'], out=v['lo'], flex=v['lx'], twist=v['lt'])
    arm(P, 'R', fwd=v['rf'], out=v['ro'], flex=v['rx'], twist=v['rt'], wrist=v['rw'])
    lf = 0.15 + step * smooth(0.05, 0.25, t) * (1 - smooth(0.6, 1.0, t))      # passo à frente no golpe
    leg_ik(P, 'L', (0.15, -lf, ANKLE_Z), splay=10)
    leg_ik(P, 'R', (-0.17, 0.12 + step * 0.4 * smooth(0.05, 0.25, t) * (1 - smooth(0.6, 1.0, t)), ANKLE_Z), toe=6 * smooth(0.1, 0.3, t) * (1 - smooth(0.6, 1, t)), splay=14)
    return P


def slash1(u):
    """Golpe diagonal: levanta a arma por cima do ombro direito, gira o tronco e corta até o quadril esquerdo."""
    k = [(0.00, {}),
         (0.24, dict(yaw=-14, cyaw=-30, cpitch=-6, croll=6, hz=-0.06, rf=150, ro=30, rx=78, rt=-20, rw=-10, lf=34, lo=16, lx=62, hp=0, hyw=12)),
         (0.40, dict(yaw=6, cyaw=12, cpitch=12, croll=-4, hz=-0.10, hy=0.06, rf=80, ro=-12, rx=24, rt=-30, rw=10, lf=20, lo=24, lx=46, hp=-6, hyw=-6)),
         (0.52, dict(yaw=14, cyaw=30, cpitch=18, croll=-8, hz=-0.11, hy=0.08, rf=34, ro=-34, rx=14, rt=-40, rw=20, lf=-8, lo=30, lx=34, hp=-10, hyw=-14)),
         (0.70, dict(yaw=10, cyaw=22, cpitch=12, croll=-5, hz=-0.09, hy=0.06, rf=28, ro=-26, rx=18, rt=-30, rw=16, lf=-4, lo=24, lx=30, hp=-8, hyw=-10)),
         (1.00, {})]
    return _attack_pose(u, k, step=0.22)


def slash2(u):
    """Golpe horizontal: arma aberta para a direita, varre na altura do peito com o tronco girando inteiro."""
    k = [(0.00, {}),
         (0.26, dict(yaw=-18, cyaw=-36, cpitch=0, croll=-4, hz=-0.07, rf=70, ro=78, rx=40, rt=30, rw=-4, lf=40, lo=-6, lx=70, lt=20, hp=-2, hyw=20)),
         (0.42, dict(yaw=8, cyaw=18, cpitch=8, croll=2, hz=-0.10, hy=0.05, rf=88, ro=10, rx=6, rt=-10, rw=4, lf=10, lo=30, lx=40, hp=-6, hyw=-4)),
         (0.54, dict(yaw=18, cyaw=40, cpitch=10, croll=4, hz=-0.11, hy=0.07, rf=76, ro=-58, rx=10, rt=-50, rw=8, lf=-12, lo=38, lx=30, hp=-6, hyw=-18)),
         (0.72, dict(yaw=12, cyaw=28, cpitch=8, croll=2, hz=-0.09, hy=0.05, rf=60, ro=-44, rx=16, rt=-40, rw=8, lf=-6, lo=30, lx=28, hp=-6, hyw=-12)),
         (1.00, {})]
    return _attack_pose(u, k, step=0.18)


def cast(u):
    """Magia/disparo: recolhe as mãos ao peito, inclina para trás e lança as duas mãos para a frente."""
    k = [(0.00, {}),
         (0.28, dict(yaw=-4, cyaw=-6, cpitch=-8, hz=-0.05, rf=40, ro=-4, rx=110, rt=-30, lf=40, lo=-4, lx=110, lt=30, hp=4)),
         (0.44, dict(yaw=2, cyaw=4, cpitch=12, hz=-0.09, hy=0.06, rf=86, ro=6, rx=8, rt=0, rw=-14, lf=86, lo=6, lx=8, lt=0, hp=-10)),
         (0.66, dict(yaw=2, cyaw=4, cpitch=10, hz=-0.08, hy=0.05, rf=80, ro=8, rx=12, rw=-10, lf=78, lo=8, lx=14, hp=-8)),
         (1.00, {})]
    return _attack_pose(u, k, step=0.14)


def fly_idle(u):
    """Pairando: sobe ~0,7 m acompanhando a batida das asas; pernas soltas, braços abertos para o equilíbrio."""
    P = Pose()
    b = math.sin(u * 2 * math.pi)
    P.hip = Vector((0, 0, 0.70 + b * 0.06))
    torso(P, pitch=6 + b * 2, cpitch=2 - b * 2, hpitch=-6 + b * 1.5, croll=math.sin(u * 2 * math.pi + 1.2) * 1.5)
    arm(P, 'L', fwd=8 + b * 4, out=26 - b * 6, flex=24 + b * 6, twist=6)
    arm(P, 'R', fwd=14 + b * 4, out=24 - b * 6, flex=34 + b * 6, twist=-6, wrist=6)
    for side, ph, sx in (('L', 0.0, 1), ('R', 0.35, -1)):
        s = math.sin(u * 2 * math.pi + ph * 2 * math.pi)
        P.fk('Leg' + side, ry(-sx * 4) @ rx(-(14 + s * 7)))
        P.fk('Shin' + side, rx(30 + s * 10))
        P.fk('Foot' + side, rx(26 + s * 5))
    return P


def fly_move(u):
    """Voo em frente: o corpo deita ~38°, pernas esticadas para trás, braços varridos junto ao corpo."""
    P = Pose()
    b = math.sin(u * 2 * math.pi)
    P.hip = Vector((0, 0.08, 0.78 + b * 0.05))
    torso(P, pitch=36 + b * 2, roll=math.sin(u * 2 * math.pi + 0.8) * 3, cpitch=4 - b * 2, hpitch=-30 - b * 1.5)
    arm(P, 'L', fwd=-28 + b * 5, out=18 - b * 4, flex=16 + b * 4, twist=12)
    arm(P, 'R', fwd=-22 + b * 5, out=18 - b * 4, flex=26 + b * 4, twist=-12, wrist=6)
    for side, ph, sx in (('L', 0.0, 1), ('R', 0.3, -1)):
        s = math.sin(u * 2 * math.pi + ph * 2 * math.pi)
        P.fk('Leg' + side, ry(-sx * 3) @ rx(8 + s * 5))
        P.fk('Shin' + side, rx(22 + s * 9))
        P.fk('Foot' + side, rx(38 + s * 6))
    return P


def death(u):
    """Morte: o golpe empurra para trás, os joelhos cedem e o corpo cai de costas."""
    P = Pose()
    k1 = smooth(0.0, 0.25, u)
    k2 = smooth(0.2, 0.75, u)
    k3 = smooth(0.7, 1.0, u)
    P.hip = Vector((0, 0.10 * k1 + 0.35 * k2, -0.05 * k1 - 0.70 * k2 - 0.02 * k3))
    torso(P, pitch=-10 * k1 - 70 * k2 - 6 * k3, yaw=8 * k2, cpitch=-14 * k1 + 6 * k2, croll=6 * k2, hpitch=-20 * k1 + 10 * k2 + 4 * k3, hyaw=25 * k2)
    arm(P, 'L', fwd=30 * k1 - 10 * k2, out=20 * k1 + 40 * k2, flex=40 * k1 - 10 * k2, twist=10)
    arm(P, 'R', fwd=40 * k1 - 20 * k2, out=18 * k1 + 50 * k2, flex=50 * k1 - 20 * k2, twist=-10)
    for side, sx in (('L', 1), ('R', -1)):
        P.fk('Leg' + side, ry(-sx * (4 + 10 * k2)) @ rx(-(15 * k1 + 45 * k2 - 55 * k3)))
        P.fk('Shin' + side, rx(30 * k1 + 60 * k2 - 55 * k3))
        P.fk('Foot' + side, rx(10 * k2))
    return P


CLIPS = [
    ('Idle', 3.0, True, idle),
    ('Walk', 0.62, True, walk),
    ('Run', 0.62, True, run),
    ('Slash1', 0.6, False, slash1),
    ('Slash2', 0.6, False, slash2),
    ('Cast', 0.6, False, cast),
    ('FlyIdle', 1.5, True, fly_idle),
    ('FlyMove', 1.1, True, fly_move),
    ('Death', 1.2, False, death),
]
# passada (m por ciclo) dos clipes de locomoção, para o jogo sincronizar a fase com a velocidade (pé sem deslizar)
STRIDE = {'Walk': (0.30 + 0.31) / 0.56, 'Run': (0.30 + 0.32) / 0.2}   # percurso do pé no apoio / fração de apoio


# ================================================================ Blender: armature, actions e prévia
def build_armature(name='KZ_Esqueleto'):
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    arm_data = bpy.data.armatures.new(name)
    ob = bpy.data.objects.new(name, arm_data)
    bpy.context.scene.collection.objects.link(ob)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_data.edit_bones
    for n, h, t, p in BONES:
        b = eb.new(n)
        b.head, b.tail = Vector(h), Vector(t)
        b.roll = 0.0
        if p:
            b.parent = eb[p]
            b.use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    ob.show_in_front = True
    return ob


def _rest(ob):
    return {b.name: b.matrix_local.to_quaternion() for b in ob.data.bones}


def key_pose(ob, P, frame, R):
    for n in BONE_NAMES:
        pb = ob.pose.bones[n]
        pb.rotation_mode = 'QUATERNION'
        p = PARENT[n]
        Wp = P.W[p] if p else Quaternion()
        rel = Wp.inverted() @ P.W[n]
        q = R[n].inverted() @ rel @ R[n]
        pb.rotation_quaternion = q
        pb.keyframe_insert('rotation_quaternion', frame=frame)
        if n == 'Body':
            pb.location = R[n].inverted() @ P.hip
            pb.keyframe_insert('location', frame=frame)


def frames_of(dur, loop):
    n = max(2, int(round(dur * FPS)))
    return n if loop else n + 1


def bake_actions(ob):
    R = _rest(ob)
    ob.animation_data_create()
    acts = {}
    for name, dur, loop, fn in CLIPS:
        an = 'KZ_' + name
        old = bpy.data.actions.get(an)
        if old is not None:
            bpy.data.actions.remove(old)
        act = bpy.data.actions.new(an)
        act.use_fake_user = True
        ob.animation_data.action = act
        n = frames_of(dur, loop)
        for f in range(n):
            u = f / (n if loop else (n - 1))
            key_pose(ob, fn(u), f, R)
        acts[name] = (act, n, loop, dur)
    return acts


def read_pose(ob, R):
    """Lê a pose avaliada do Blender: rotação de cada osso no espaço do personagem e deslocamento do quadril."""
    W = {}
    for n in BONE_NAMES:
        pb = ob.pose.bones[n]
        W[n] = pb.matrix.to_quaternion() @ R[n].inverted()
    hip = ob.pose.bones['Body'].head - HEAD['Body']
    return W, hip


ANIM_OUT = r'C:\MuProtoUnity\Assets\Resources\Anim\Heroi.txt'


def export_anim(ob, acts, path=ANIM_OUT):
    R = _rest(ob)
    sc = bpy.context.scene
    lines = ['KZANIM 1', 'fps %d' % FPS, 'bones ' + ' '.join(BONE_NAMES)]
    for n in BONE_NAMES:
        h = HEAD[n]
        lines.append('rest %s %.4f %.4f %.4f %s' % (n, h.x, h.y, h.z, PARENT[n] or '-'))
    for k, v in STRIDE.items():
        lines.append('stride %s %.4f' % (k, v))
    for name, (act, n, loop, dur) in acts.items():
        ob.animation_data.action = act
        lines.append('clip %s %d %d %.4f' % (name, n, 1 if loop else 0, dur))
        for f in range(n):
            sc.frame_set(f)
            W, hip = read_pose(ob, R)
            vals = ['%.4f %.4f %.4f' % (hip.x, hip.y, hip.z)]
            for b in BONE_NAMES:
                q = W[b]
                if q.w < 0:
                    q = -q
                vals.append('%.5f %.5f %.5f %.5f' % (q.w, q.x, q.y, q.z))
            lines.append('f ' + ' '.join(vals))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('\n'.join(lines) + '\n')
    return path, len(lines)


# ---------------------------------------------------------------- pele de prévia (mesmos pesos que o jogo usa)
def skin_weights(part, p):
    """Pesos por vértice (posição no espaço do personagem, Blender), idênticos aos do SkinRig no Unity."""
    z = p.z
    if part == 'Head':
        return {'Head': 1.0}
    if part == 'Body':
        c = smooth(0.98, 1.14, z)
        return {'Body': 1 - c, 'Chest': c}
    if part in ('ArmL', 'ArmR'):
        s = part[-1]
        f = smooth(1.13, 1.03, z)
        h = smooth(0.83, 0.77, z)
        return {'Arm' + s: 1 - f, 'ForeArm' + s: f * (1 - h), 'Hand' + s: f * h}
    if part in ('LegL', 'LegR'):
        s = part[-1]
        k = smooth(0.59, 0.47, z)
        a = smooth(0.15, 0.08, z)
        return {'Leg' + s: 1 - k, 'Shin' + s: k * (1 - a), 'Foot' + s: k * a}
    return {'Body': 1.0}


def preview_skin(root_name='GuardiaoRunicoBase', ob=None, name='KZ_PeleTeste'):
    """Junta as partes do herói numa malha só com grupos de vértices (pesos do skin_weights) e o esqueleto."""
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    root = bpy.data.objects[root_name]
    copies = []
    for k in root.children_recursive:
        if k.type != 'MESH':
            continue
        part = k.get('part') or 'Body'
        c = k.copy()
        c.data = k.data.copy()
        c.data.transform(k.matrix_world)
        c.parent = None
        c.matrix_world = Matrix.Identity(4)
        c.modifiers.clear()
        bpy.context.scene.collection.objects.link(c)
        for v in c.data.vertices:
            for n, w in skin_weights(part, v.co).items():
                if w > 1e-4:
                    g = c.vertex_groups.get(n) or c.vertex_groups.new(name=n)
                    g.add([v.index], w, 'REPLACE')
        copies.append(c)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for c in copies:
        c.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    if ob is not None:
        mod = o.modifiers.new('Esqueleto', 'ARMATURE')
        mod.object = ob
    return o


def anim_build_all(export=True):
    ob = build_armature()
    acts = bake_actions(ob)
    out = export_anim(ob, acts) if export else None
    return ob, acts, out
