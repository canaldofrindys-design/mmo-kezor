# Kezor — monstros não humanoides (lodo, lobo, aranha, golem). Requer kezor_lib.py + kezor_humans.py (finish/PARTS).


def finish_custom(name, cat, coll, body_b, parts):
    """parts: lista de (nome, builder, pivô)."""
    wipe(name)
    r = root(name, coll, cat)
    body = body_b.obj(name + '.Body', coll, r, (0, 0, 0), 'Body')
    for pname, bb, pv in parts:
        bb.obj(name + '.' + pname, coll, body, pv, pname)
    return r


def lodo(coll, name='LodoMusgoso', cat='Monsters'):
    gel = mat('SlimeGel', '#6fa83a', rough=0.35)
    gel2 = mat('SlimeGel2', '#5a9030', rough=0.35)
    moss = mat('Moss', '#3f6b2a')
    eye = mat('SlimeEye', '#f4ff9a', emit=4.0)
    mouth = mat('SlimeMouth', '#26401a')
    cap = mat('MushCap', '#b0502a')
    stem = mat('MushStem', '#e8dcc0')
    b = B()
    b.ico((0, 0, 0.36), (0.6, 0.58, 0.46), gel, jit=0.06, seed=3, sub=2, m2=gel2, zmin=0.0)
    for i, (x, y, s) in enumerate(((0.18, 0.12, 0.16), (-0.2, 0.2, 0.13), (0.02, 0.28, 0.12), (-0.3, -0.05, 0.09))):
        b.ico((x, y, 0.72 - abs(x) * 0.5), (s, s, s * 0.5), moss, jit=0.25, seed=10 + i)
    b.cyl((0.2, 0.1, 0.74), 0.025, 0.02, 0.12, 5, stem)
    b.lathe((0.2, 0.1, 0.84), [(0.075, 0), (0.06, 0.04), (0, 0.06)], 6, cap)
    b.cyl((0.27, 0.16, 0.7), 0.018, 0.015, 0.08, 5, stem)
    b.lathe((0.27, 0.16, 0.77), [(0.05, 0), (0.04, 0.025), (0, 0.04)], 6, cap)
    for x in (-0.17, 0.17):
        b.box((x, -0.5, 0.5), (0.11, 0.06, 0.14), eye, top=(0.8, 1))
    b.box((0, -0.53, 0.33), (0.2, 0.04, 0.04), mouth, top=(1.3, 1))
    return finish_custom(name, cat, coll, b, [])


def lobo(coll, name='LoboCinzento', cat='Monsters'):
    fur = mat('WolfFur', '#6b6966')
    dark = mat('WolfDark', '#45433f')
    belly = mat('WolfBelly', '#8a8580')
    eye = mat('WolfEye', '#ff4a2a', emit=4.0)
    nose = mat('Dark', '#1a1a1a')
    fang = mat('Fang', '#e8e2d0')
    b = B()
    b.box((0, 0.08, 0.76), (0.4, 0.95, 0.36), fur, top=(0.9, 0.95))                       # tronco
    b.box((0, 0.1, 0.6), (0.3, 0.7, 0.06), belly)
    b.box((0, -0.3, 0.82), (0.5, 0.45, 0.5), dark, top=(0.85, 0.8), shift=(0, -0.02))     # juba
    for i in range(5):
        y = -0.35 + i * 0.16
        b.seg((0, y, 1.0 - i * 0.03), (0, y + 0.14, 1.12 - i * 0.04), 0.1, dark, w2=0.01, d=0.05)
    b.seg((0, 0.53, 0.86), (0, 0.8, 0.68), 0.14, fur, w2=0.12)                             # cauda
    b.seg((0, 0.8, 0.68), (0, 0.98, 0.42), 0.12, dark, w2=0.02)
    h = B()
    h.box((0, -0.68, 1.0), (0.32, 0.3, 0.3), fur, top=(0.85, 0.9))
    h.box((0, -0.9, 0.95), (0.17, 0.22, 0.14), dark, top=(0.85, 0.9))                       # focinho
    h.box((0, -0.9, 0.86), (0.14, 0.2, 0.05), dark)                                        # mandíbula
    h.box((0, -1.015, 0.98), (0.06, 0.02, 0.05), nose)
    for x in (-0.04, 0.04):
        h.cone((x, -0.98, 0.89), 0.012, -0.05, 4, fang)
    for x in (-0.1, 0.1):
        h.box((x, -0.83, 1.05), (0.06, 0.02, 0.03), eye, top=(1, 1), shift=(x * 0.2, 0))
        h.lathe((x, -0.66, 1.13), [(0.07, 0), (0, 0.18)], 4, dark, sy=0.5)
    for x in (-0.17, 0.17):
        h.box((x, -0.64, 0.95), (0.04, 0.18, 0.2), dark)                                     # bochechas
    legs = []
    spots = [(0.15, -0.35), (-0.15, -0.35), (0.15, 0.42), (-0.15, 0.42)]
    for i, (x, y) in enumerate(spots):
        l = B()
        front = y < 0
        top = (x, y, 0.66)
        if front:
            knee = (x, y - 0.02, 0.33)
            l.seg(top, knee, 0.14, dark if front else fur, w2=0.1)
            l.seg(knee, (x, y - 0.02, 0.05), 0.09, dark, w2=0.08)
        else:
            knee = (x, y + 0.12, 0.38)
            l.seg(top, knee, 0.16, fur, w2=0.11, d=0.2)
            l.seg(knee, (x, y + 0.04, 0.05), 0.09, dark, w2=0.08)
        l.box((x, y - 0.06, 0.035), (0.11, 0.16, 0.07), dark, top=(0.9, 0.8))
        legs.append(('Leg%d' % i, l, top))
    return finish_custom(name, cat, coll, b, [('Head', h, (0, -0.52, 0.95))] + legs)


def aranha(coll, name='AranhaTecela', cat='Monsters'):
    shell = mat('SpiderShell', '#2e2433')
    mark = mat('SpiderMark', '#7a3b8f')
    eye = mat('SpiderEye', '#8dff6a', emit=4.0)
    fang = mat('SpiderFang', '#c9b9a0')
    b = B()
    b.ico((0, 0.42, 0.68), (0.46, 0.56, 0.4), shell, jit=0.06, seed=7, sub=1, m2=mark, share=0.25)   # abdômen
    b.prism([(0, 0.16), (0.1, 0.0), (0, -0.2), (-0.1, 0.0)], 0, 0.02, mark)
    b.ico((0, -0.2, 0.55), (0.28, 0.3, 0.2), shell, jit=0.05, seed=11)                                # cefalotórax
    b.seg((0, 0.1, 0.62), (0, -0.05, 0.58), 0.12, shell)
    b.seg((0, 0.96, 0.66), (0, 1.04, 0.6), 0.08, mark, w2=0.03)                                       # fiandeira
    for x, z, s in ((-0.06, 0.63, 0.04), (0.06, 0.63, 0.04), (-0.12, 0.6, 0.03), (0.12, 0.6, 0.03), (-0.03, 0.68, 0.025), (0.03, 0.68, 0.025)):
        b.box((x, -0.475 + abs(x) * 0.4, z), (s, 0.02, s), eye)
    for x in (-0.07, 0.07):
        b.seg((x, -0.42, 0.52), (x * 1.2, -0.5, 0.38), 0.07, shell, w2=0.05)
        b.seg((x * 1.2, -0.5, 0.38), (x * 0.6, -0.46, 0.28), 0.04, fang, w2=0.008)
    for i in range(6):
        an = i * math.pi / 3
        b.seg((math.cos(an) * 0.3, 0.42 + math.sin(an) * 0.36, 0.9), (math.cos(an) * 0.36, 0.42 + math.sin(an) * 0.44, 1.02), 0.05, mark, w2=0.005)
    parts = []
    yaw = [-55, -20, 15, 50]
    for i in range(8):
        side = 1 if i < 4 else -1     # 0-3 esquerda (+X), 4-7 direita
        k = i % 4
        a = math.radians(yaw[k])
        p = Vector((side * 0.18, -0.32 + 0.09 * k, 0.55))
        o = Vector((side * math.cos(a), math.sin(a), 0))
        knee = p + o * 0.45 + Vector((0, 0, 0.36))
        foot = p + o * 0.95 + Vector((0, 0, -0.55))
        l = B()
        l.seg(p, knee, 0.075, shell, w2=0.06)
        l.box(tuple(knee), (0.08, 0.08, 0.08), mark)
        l.seg(knee, foot, 0.06, shell, w2=0.015)
        parts.append(('Leg%d' % i, l, tuple(p)))
    return finish_custom(name, cat, coll, b, parts)


def golem(coll, name='GolemDeBasalto', cat='Monsters'):
    rock = mat('Basalt', '#3d3a3a', rough=0.95)
    rock2 = mat('Basalt2', '#2c2a2a', rough=0.95)
    lava = mat('Lava', '#ff6a1a', emit=6.0)
    b = B()
    b.ico((0, 0, 1.55), (0.82, 0.6, 0.62), rock, jit=0.12, seed=21, m2=rock2)                         # peito
    b.ico((0, 0.02, 0.92), (0.55, 0.45, 0.38), rock2, jit=0.15, seed=22)                              # quadril
    b.seg((0, 0, 1.0), (0, 0, 1.4), 0.5, rock2, w2=0.6, d=0.4)
    for side in (-1, 1):
        b.ico((side * 0.62, 0.05, 2.0), (0.38, 0.36, 0.3), rock, jit=0.15, seed=30 + side, m2=rock2)  # ombros
        b.cone((side * 0.66, 0.1, 2.15), 0.12, 0.38, 4, rock2)
        b.cone((side * 0.5, 0.18, 2.12), 0.08, 0.26, 4, rock2)
    b.gem((0, -0.52, 1.6), 0.16, 0.42, lava)                                                         # núcleo
    cracks = [((0, -0.5, 1.82), (-0.28, -0.5, 2.0)), ((0, -0.5, 1.82), (0.3, -0.48, 1.95)), ((0.0, -0.52, 1.38), (-0.22, -0.48, 1.2)),
              ((-0.22, -0.48, 1.2), (-0.4, -0.38, 1.28)), ((0.05, -0.5, 1.38), (0.3, -0.42, 1.15)), ((0.3, -0.42, 1.15), (0.38, -0.3, 0.95)),
              ((-0.3, 0.45, 1.75), (0.1, 0.5, 1.5)), ((0.1, 0.5, 1.5), (0.35, 0.42, 1.65)), ((0.1, 0.5, 1.5), (0.0, 0.42, 1.2))]
    for p0, p1 in cracks:
        b.seg(p0, p1, 0.05, lava, d=0.06)
    h = B()
    h.ico((0, -0.18, 2.25), (0.28, 0.26, 0.24), rock, jit=0.12, seed=40)
    h.box((0, -0.38, 2.31), (0.36, 0.1, 0.08), rock2, top=(0.9, 1))                                   # sobrancelha
    for x in (-0.09, 0.09):
        h.box((x, -0.41, 2.24), (0.09, 0.03, 0.045), lava)
    h.seg((-0.08, -0.42, 2.12), (0.08, -0.42, 2.12), 0.025, lava, d=0.02)
    parts = [('Head', h, (0, -0.12, 2.1))]
    for side, pn in ((1, 'ArmL'), (-1, 'ArmR')):
        a = B(); x = 0.88 * side
        a.ico((x, 0, 1.6), (0.3, 0.3, 0.42), rock, jit=0.15, seed=50 + side, m2=rock2)
        a.ico((x + side * 0.03, -0.03, 1.0), (0.34, 0.34, 0.38), rock2, jit=0.15, seed=55 + side)
        a.ico((x + side * 0.04, -0.06, 0.55), (0.36, 0.36, 0.3), rock, jit=0.18, seed=60 + side, m2=rock2)   # punho
        a.seg((x + side * 0.3, -0.05, 1.15), (x + side * 0.26, -0.1, 0.85), 0.05, lava, d=0.05)
        a.seg((x + side * 0.1, -0.33, 1.1), (x - side * 0.05, -0.3, 0.88), 0.045, lava, d=0.05)
        for k in range(3):
            a.box((x + side * 0.04 + (k - 1) * 0.13, -0.38, 0.5), (0.1, 0.08, 0.12), rock2)
        parts.append((pn, a, (x, 0, 1.9)))
    for side, pn in ((1, 'LegL'), (-1, 'LegR')):
        l = B(); x = 0.32 * side
        l.ico((x, 0, 0.52), (0.26, 0.28, 0.3), rock2, jit=0.15, seed=70 + side)
        l.box((x, -0.06, 0.13), (0.4, 0.5, 0.26), rock, top=(0.8, 0.75))
        l.seg((x + side * 0.12, -0.24, 0.5), (x + side * 0.05, -0.25, 0.3), 0.04, lava, d=0.04)
        parts.append((pn, l, (x, 0, 0.8)))
    return finish_custom(name, cat, coll, b, parts)
