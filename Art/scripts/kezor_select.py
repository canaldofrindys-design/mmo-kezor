# Kezor — cenário da tela de seleção (noite), v2: pátio de pedra largo, escadarias laterais, muralha de basalto com
# pontas, portão rúnico ao fundo (arco de basalto com grade e runas de brasa), braseiros em pilares altos, árvores mortas
# emoldurando a cena. Os heróis ficam em fila em X = -5.2..5.2 (passo 2,6), Z(Blender Y) = 0, olhando para a câmera (-Y).
# Peças marcadas: Fogo0..5 (malha da chama; o Unity anima e recoloca no braseiro), Lugar0..4 (pivô de cada herói).
import math, random

SPOTS = [-5.2 + i * 2.6 for i in range(5)]
# braseiros: (x, y, altura do pilar)
BRAZIERS = [(-7.6, -0.6, 1.2), (7.6, -0.6, 1.2), (-3.9, 2.6, 2.4), (3.9, 2.6, 2.4), (-1.6, 4.4, 3.4), (1.6, 4.4, 3.4)]


def select_scene(name='SelecaoPatio'):
    coll = collection('Environment')
    wipe(name)
    r = root(name, coll, 'Environment')
    rnd = random.Random(42)
    stone = mat('SelSlab', '#3e3a36'); stoneD = mat('SelSlabD', '#2c2926'); stoneL = mat('SelSlabL', '#4b4641')
    basalt = mat('SelBasalt', '#232120'); basaltL = mat('SelBasaltL', '#33302c'); iron = mat('SelIron', '#2e2924', rough=0.5, metal=0.6)
    bronze = mat('SelBronze', '#7a5e36', rough=0.45, metal=0.6); bark = mat('SelBark', '#241e19'); barkL = mat('SelBarkL', '#332a22')
    ember = mat('SelEmber', '#ff6a1a', emit=4.0); ember2 = mat('SelEmberCore', '#ffd36a', emit=4.0); runeM = mat('SelRune', '#ff7a22', emit=2.5)
    grate = mat('SelGrate', '#1a1816', rough=0.6, metal=0.5); sky = mat('SelGateDark', '#0a0c16')
    ground = B()
    # lajes irregulares (pátio 20 x 12)
    for ix in range(-10, 10):
        for iy in range(-7, 5):
            w = 0.96 + rnd.uniform(-0.05, 0.02); d = 0.96 + rnd.uniform(-0.05, 0.02)
            h = 0.08 + rnd.uniform(0, 0.05)
            m = [stone, stoneD, stoneL][rnd.randrange(3)]
            cbox(ground, (ix + 0.5 + rnd.uniform(-0.03, 0.03), iy + 0.5, h / 2 - 0.04), (w, d, h), m, bev=0.025)
    # discos de bronze sob cada lugar
    for x in SPOTS:
        ground.lathe((x, 0, 0.07), [(0.66, 0), (0.66, 0.012)], 8, bronze)
        ground.lathe((x, 0, 0.08), [(0.54, 0), (0.54, 0.008)], 8, basalt)
    # degraus atrás da fila (largos) e escadarias laterais
    for k in range(3):
        cbox(ground, (0, 2.0 + k * 0.5, 0.08 + k * 0.18), (15.0 - k * 1.2, 0.55, 0.16 + k * 0.36), [stoneD, stone, stoneD][k], bev=0.03)
    for sx in (-1, 1):
        for k in range(6):
            cbox(ground, (sx * (9.4 - k * 0.15), -2.4 + k * 0.7, 0.12 + k * 0.24), (2.4, 0.7, 0.24 + k * 0.48), [stoneD, stone][k % 2], bev=0.03)
    # muralha de basalto com pontas
    wall = B()
    cbox(wall, (0, 5.2, 1.8), (24.0, 1.0, 3.6), basalt, bev=0.06)
    cbox(wall, (0, 4.7, 3.55), (24.2, 0.2, 0.18), basaltL, bev=0.03)
    for i in range(-12, 13):
        x = i * 0.98
        if abs(x) < 2.2: continue                                                           # vão do portão
        cbox(wall, (x, 5.1, 3.8), (0.55, 0.8, 0.4), basaltL if i % 2 else basalt, bev=0.04)
        wall.cone((x, 5.1, 4.0), 0.12, 0.6 + (0.3 if i % 3 == 0 else 0), 4, iron)
    for x in (-8.6, 8.6):                                                                   # torres laterais
        cbox(wall, (x, 5.4, 3.0), (2.0, 1.8, 6.0), basaltL, bev=0.08, top=(0.9, 0.9))
        wall.cone((x, 5.4, 6.0), 1.3, 1.8, 6, basalt)
        cbox(wall, (x, 4.48, 3.6), (0.3, 0.06, 0.7), runeM, bev=0.02)
    # portão rúnico: dois pilares, arco em ponta, grade de ferro e runas de brasa
    for sx in (-1, 1):
        cbox(wall, (sx * 2.0, 5.0, 2.6), (0.9, 1.2, 5.2), basaltL, bev=0.06, top=(0.8, 0.8))
        wall.cone((sx * 2.0, 5.0, 5.2), 0.55, 1.4, 4, basalt)
        for k in range(3):
            cbox(wall, (sx * 2.0, 4.38, 1.6 + k * 1.2), (0.22, 0.04, 0.36), runeM, bev=0.01)
    cbox(wall, (0, 5.2, 2.2), (3.1, 0.2, 4.4), sky, bev=0.01)                               # escuro atrás da grade
    for k in range(7):
        cbox(wall, (-1.35 + k * 0.45, 5.0, 2.1), (0.07, 0.07, 4.2), grate, bev=0.0)
    for k in range(5):
        cbox(wall, (0, 5.0, 0.4 + k * 0.9), (3.0, 0.07, 0.07), grate, bev=0.0)
    wall.seg((-2.0, 5.0, 4.6), (0, 5.0, 6.2), 0.45, basaltL, w2=0.3)
    wall.seg((2.0, 5.0, 4.6), (0, 5.0, 6.2), 0.45, basaltL, w2=0.3)
    cbox(wall, (0, 4.6, 5.3), (0.42, 0.06, 0.42), runeM, bev=0.02)                         # runa do topo
    # braseiros em pilares
    for i, (x, y, hgt) in enumerate(BRAZIERS):
        bz = B()
        cbox(bz, (x, y, hgt * 0.5), (0.5, 0.5, hgt), basaltL, bev=0.04, top=(0.85, 0.85))
        bz.lathe((x, y, hgt), [(0.18, 0), (0.42, 0.22), (0.48, 0.3)], 8, iron)
        for k in range(4):
            a = k * math.pi / 2 + 0.4
            bz.cone((x + math.cos(a) * 0.44, y + math.sin(a) * 0.44, hgt + 0.26), 0.05, 0.18, 4, bronze)
        bz.ico((x, y, hgt + 0.28), (0.36, 0.36, 0.08), mat('SelCoal', '#1e1410'), seed=i)
        bz.obj(name + '.Braseiro%d' % i, coll, r, (0, 0, 0), 'Braseiro%d' % i)
        fl = B()
        z0 = hgt + 0.3
        for k, (sc, m) in enumerate(((1.0, ember), (0.62, ember2))):
            fl.lathe((x, y, z0), [(0.3 * sc, 0), (0.24 * sc, 0.26 * sc), (0.12 * sc, 0.56 * sc), (0.0, 0.9 * sc)], 6, m, rot0=k * 0.5)
        fl.obj(name + '.Fogo%d' % i, coll, r, (x, y, z0), 'Fogo%d' % i)
    # árvores mortas emoldurando (grandes nas laterais, menores ao fundo)
    for n, (x, y, s) in enumerate(((-10.2, -1.5, 1.7), (10.0, -2.0, 1.6), (-6.6, 3.6, 1.0), (6.8, 3.4, 1.1), (-11.0, 2.4, 1.2), (11.2, 2.0, 1.3))):
        tr = B()
        tr.seg((x, y, 0), (x + 0.15, y, 2.4 * s), 0.32 * s, bark, w2=0.15 * s)
        for k in range(7):
            a = rnd.uniform(0, math.tau); h = rnd.uniform(1.0, 2.3) * s; L = rnd.uniform(0.7, 1.4) * s
            p0 = (x + 0.05, y, h); p1 = (x + math.cos(a) * L, y + math.sin(a) * L * 0.5, h + L * 0.6)
            tr.seg(p0, p1, 0.11 * s, barkL if k % 2 else bark, w2=0.035)
            tr.seg(p1, (p1[0] + math.cos(a + 0.6) * 0.45 * s, p1[1], p1[2] + 0.4 * s), 0.045 * s, bark, w2=0.01)
        tr.obj(name + '.Arvore%d' % n, coll, r, (0, 0, 0), 'Arvore%d' % n)
    # lugares dos heróis (pivôs)
    for i, x in enumerate(SPOTS):
        m = B()
        m.box((x, 0, -0.5), (0.01, 0.01, 0.01), basalt)
        m.obj(name + '.Lugar%d' % i, coll, r, (x, 0, 0), 'Lugar%d' % i)
    ground.obj(name + '.Chao', coll, r, (0, 0, 0), 'Chao')
    wall.obj(name + '.Muralha', coll, r, (0, 0, 0), 'Muralha')
    return r
