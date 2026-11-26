# Kezor — heróis a partir do MPFB (MakeHuman para Blender, assets CC0): corpo, rosto e pele realistas com o rig
# "game_engine" do MPFB, encaixados no esqueleto do jogo (15 ossos, braços pendurados, juntas nas mesmas alturas)
# e com os pesos do MPFB somados nos 15 ossos. A pele vira cor de vértice (amostrada da textura), como o resto do jogo.
# Requer no namespace KZ: kezor_hero4 (clothes, eyes, paint, _join, _kill, _link, decimate, export_hero4),
# kezor_armor6 (Shell), kezor_body_cls (hair_cls, beard, encode_uv, transfer_weights, export_class).
import bpy, bmesh, glob, math, os
from mathutils import Vector, Matrix

KZG = bpy.app.driver_namespace['KZ']
MP_DATA = os.path.join(os.environ.get('APPDATA', ''), r'Blender Foundation\Blender\5.2\extensions\.user\user_default\mpfb\data')

# alturas do esqueleto do jogo (kezor_anim.BONES)
J = dict(top=1.805, shoulder=1.40, elbow=1.08, wrist=0.80, hip=0.92, knee=0.53, ankle=0.10, arm_x=0.31, arm_y=-0.004, leg_x=0.12)

# osso do MPFB -> osso do jogo (pesos somados)
BONEMAP = {
    'Root': 'Body', 'pelvis': 'Body', 'spine_01': 'Body', 'spine_02': 'Chest', 'spine_03': 'Chest',
    'neck_01': 'Head', 'head': 'Head',
    'clavicle_l': 'Chest', 'upperarm_l': 'ArmL', 'lowerarm_l': 'ForeArmL', 'hand_l': 'HandL',
    'clavicle_r': 'Chest', 'upperarm_r': 'ArmR', 'lowerarm_r': 'ForeArmR', 'hand_r': 'HandR',
    'thigh_l': 'LegL', 'calf_l': 'ShinL', 'foot_l': 'FootL', 'ball_l': 'FootL',
    'thigh_r': 'LegR', 'calf_r': 'ShinR', 'foot_r': 'FootR', 'ball_r': 'FootR',
}
for _s in ('l', 'r'):
    for _f in ('index', 'middle', 'pinky', 'ring', 'thumb'):
        for _k in ('01', '02', '03'):
            BONEMAP['%s_%s_%s' % (_f, _k, _s)] = 'Hand' + _s.upper()


def _rel(sub, name, ext):
    f = glob.glob(os.path.join(MP_DATA, sub, name, '*' + ext))
    return os.path.relpath(f[0], os.path.join(MP_DATA, sub)).replace('\\', '/') if f else ''


def make_human(ph, targets=(), skin='young_caucasian_male', proxy='male_muscle_13290', brows='eyebrow001', lashes='eyelashes01'):
    from bl_ext.user_default.mpfb.services.humanservice import HumanService as H
    hi = {"clothes": [], "color_adjustments": {}, "expressions": [], "eyebrows": _rel('eyebrows', brows, '.mhclo'),
          "eyelashes": _rel('eyelashes', lashes, '.mhclo'), "eyes": _rel('eyes', 'low-poly', '.mhclo'),
          "eyes_material_settings": {}, "eyes_material_type": "MAKESKIN", "hair": "", "makeup": [],
          "phenotype": ph, "proxy": _rel('proxymeshes', proxy, '.proxy') if proxy else "", "rig": "game_engine",
          "skin_material_settings": {}, "skin_material_type": "MAKESKIN", "skin_mhmat": 'skins/' + _rel('skins', skin, '.mhmat'),
          "targets": [dict(target=t, value=v) for t, v in targets], "teeth": "", "tongue": ""}
    s = H.get_default_deserialization_settings()
    s['subdiv_levels'] = 0
    base = H.deserialize_from_dict(hi, s)
    return base, base.parent


def pose_hanging(arm):
    """Braços e pernas retos para baixo (a pose de repouso do jogo)."""
    bpy.context.view_layer.objects.active = arm
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='POSE')
    down = Vector((0, 0, -1))
    for chain in (('upperarm_l', 'lowerarm_l', 'hand_l'), ('upperarm_r', 'lowerarm_r', 'hand_r'),
                  ('thigh_l', 'calf_l'), ('thigh_r', 'calf_r')):
        for bn in chain:
            pb = arm.pose.bones[bn]
            bpy.context.view_layer.update()
            M = pb.matrix.copy()
            h, t = M.translation.copy(), (M @ Vector((0, pb.bone.length, 0)))
            q = (t - h).rotation_difference(down)
            pb.matrix = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ M
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode='OBJECT')


def _joints(arm):
    W = arm.matrix_world
    P = lambda n, tail=False: W @ (arm.pose.bones[n].tail if tail else arm.pose.bones[n].head)
    return dict(shoulder=P('upperarm_l'), elbow=P('lowerarm_l'), wrist=P('hand_l'), finger=P('middle_03_l', True),
                hip=P('thigh_l'), knee=P('calf_l'), ankle=P('foot_l'), neck=P('neck_01'), head=P('head'),
                chest=P('spine_03'))


def bake(o, name):
    """Malha avaliada (forma + pose + máscara de ajudantes) num objeto novo, com os grupos de vértices."""
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    me.transform(o.matrix_world)
    KZG['_kill'](name)
    n = KZG['_link'](bpy.data.objects.new(name, me))
    for g in o.vertex_groups:
        n.vertex_groups.new(name=g.name)
    return n


def _pw(xs, ys, x):
    """Interpolação linear por trechos (xs crescente)."""
    if x <= xs[0]:
        return ys[0] + (x - xs[0]) * ((ys[1] - ys[0]) / (xs[1] - xs[0]))
    for i in range(1, len(xs)):
        if x <= xs[i]:
            t = (x - xs[i - 1]) / (xs[i] - xs[i - 1])
            return ys[i - 1] + t * (ys[i] - ys[i - 1])
    return ys[-1] + (x - xs[-1]) * ((ys[-1] - ys[-2]) / (xs[-1] - xs[-2]))


def fit_objects(objs, body, jt, top):
    """Leva o corpo do MPFB para o esqueleto do jogo sem deformar a anatomia:
    escala uniforme pela altura (1,805 m), pernas nas alturas do jogo (quadril 0,92, joelho 0,53, tornozelo 0,10),
    tronco e cabeça descem junto com o quadril (proporção natural), braços na largura natural do corpo e com
    cotovelo/punho nas alturas do jogo (1,08 e 0,80). Devolve os pivôs do modelo (cabeça, braços, pernas)."""
    k = J['top'] / top
    S = {n: v * k for n, v in jt.items()}
    d = S['hip'].z - J['hip']                    # o tronco desce isso
    sh = S['shoulder'].z - d
    az = ([S['finger'].z - d, S['wrist'].z - d, S['elbow'].z - d, sh],
          [J['wrist'] - (S['wrist'].z - S['finger'].z), J['wrist'], J['elbow'], sh])
    lz = ([0.0, S['ankle'].z, S['knee'].z, S['hip'].z], [0.0, J['ankle'], J['knee'], J['hip']])
    for o in objs:
        gi = {g.index: g.name for g in o.vertex_groups}
        me = o.data
        for v in me.vertices:
            p = v.co * k
            wa = wl = 0.0
            for g in v.groups:
                b = BONEMAP.get(gi.get(g.group, ''), '')
                if b.startswith(('Arm', 'ForeArm', 'Hand')):
                    wa += g.weight
                elif b.startswith(('Leg', 'Shin', 'Foot')):
                    wl += g.weight
            wa, wl = min(1.0, wa), min(1.0, wl)
            q = Vector((p.x, p.y, p.z - d))
            if wa > 0:
                q = q.lerp(Vector((p.x, p.y, _pw(az[0], az[1], p.z - d))), wa)
            if wl > 0:
                q = q.lerp(Vector((p.x, p.y, _pw(lz[0], lz[1], p.z))), wl)
            v.co = q
        me.update()
    ax, ay = S['shoulder'].x, S['shoulder'].y
    return dict(Head=(0, 0, round(1.47 - d, 4)), ArmL=(round(ax, 4), 0, round(sh, 4)), ArmR=(round(-ax, 4), 0, round(sh, 4)),
                LegL=(round(S['hip'].x, 4), 0, J['hip']), LegR=(round(-S['hip'].x, 4), 0, J['hip']), drop=d)


def merge_weights(o):
    """Soma os pesos do rig do MPFB nos 15 ossos do jogo (grupos com os nomes do kezor_anim)."""
    gi = {g.index: g.name for g in o.vertex_groups}
    acc = []
    for v in o.data.vertices:
        w = {}
        for g in v.groups:
            b = BONEMAP.get(gi.get(g.group, ''))
            if b:
                w[b] = w.get(b, 0.0) + g.weight
        acc.append(w)
    for g in list(o.vertex_groups):
        o.vertex_groups.remove(g)
    groups = {}
    for i, w in enumerate(acc):
        tot = sum(w.values())
        if tot <= 1e-6:
            w, tot = {'Body': 1.0}, 1.0
        for b, x in sorted(w.items(), key=lambda t: -t[1])[:4]:
            g = groups.get(b) or o.vertex_groups.new(name=b)
            groups[b] = g
            g.add([i], x / tot, 'REPLACE')


def sample_texture_to_vcol(o, gain=1.0, tint=None):
    """Cor de vértice a partir da textura base do material (amostra pela UV de cada canto)."""
    me = o.data
    img = None
    for m in me.materials:
        if m and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == 'TEX_IMAGE' and n.image and ('diffuse' in n.image.name.lower() or 'diff' in n.label.lower() or img is None):
                    if img is None or 'diffuse' in n.image.name.lower():
                        img = n.image
    attr = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    me.color_attributes.active_color = attr
    nl = len(me.loops)
    if img is None or not me.uv_layers:
        return False
    w, h = img.size
    px = list(img.pixels[:])
    uv = [0.0] * (nl * 2)
    me.uv_layers[0].data.foreach_get('uv', uv)
    out = [0.0] * (nl * 4)
    for i in range(nl):
        u, vv = uv[2 * i] % 1.0, uv[2 * i + 1] % 1.0
        x, y = min(w - 1, int(u * w)), min(h - 1, int(vv * h))
        j = (y * w + x) * 4
        c = [px[j], px[j + 1], px[j + 2]]
        c = [max(0.0, min(1.0, cc * gain)) for cc in c]          # pixels já vêm em sRGB (imagem de 8 bits)
        if tint:
            c = [c[t] * tint[t] for t in range(3)]
        out[4 * i:4 * i + 4] = (c[0], c[1], c[2], 1.0)
    attr.data.foreach_set('color_srgb', out)
    return True


def cleanup_mpfb(base, arm):
    for c in list(arm.children) + [arm]:
        try:
            bpy.data.objects.remove(c, do_unlink=True)
        except Exception:
            pass


# ---------------------------------------------------------------- classes
CLASSES_MP = {
    'GuardiaoRunico': dict(ph=dict(gender=1.0, muscle=0.9, weight=0.6, height=0.8), skin='young_caucasian_male', proxy='male_muscle_13290',
                           tg=[('torso-vshape-incr', 0.6), ('measure-shoulder-dist-incr', 0.7)], race=(0.1, 0.1, 0.8)),
    'ArcanistaDoVeu': dict(ph=dict(gender=0.0, muscle=0.4, weight=0.45, height=0.7, cupsize=0.55), skin='young_caucasian_female', proxy='female_generic',
                           tg=[], race=(0.05, 0.15, 0.8)),
    'LaminaArcana': dict(ph=dict(gender=1.0, muscle=0.75, weight=0.4, height=0.85), skin='young_asian_male', proxy='male_generic',
                         tg=[('torso-vshape-incr', 0.4)], race=(0.05, 0.75, 0.2)),
    'ComandanteEstandarte': dict(ph=dict(gender=1.0, muscle=1.0, weight=0.75, height=0.9, age=0.62), skin='middleage_caucasian_male', proxy='male_muscle_13290',
                                 tg=[('torso-vshape-incr', 0.8), ('measure-shoulder-dist-incr', 1.0)], race=(0.05, 0.05, 0.9)),
    'InvocadoraEcos': dict(ph=dict(gender=0.0, muscle=0.35, weight=0.4, height=0.6, cupsize=0.6), skin='young_caucasian_female2', proxy='female_generic',
                           tg=[], race=(0.05, 0.25, 0.7)),
    'PunhoBrasa': dict(ph=dict(gender=1.0, muscle=1.0, weight=0.6, height=0.75), skin='young_african_male', proxy='male_muscle_13290',
                       tg=[('torso-vshape-incr', 1.0), ('measure-shoulder-dist-incr', 0.8)], race=(0.85, 0.05, 0.1)),
    'PatrulheiroSelvagem': dict(ph=dict(gender=1.0, muscle=0.7, weight=0.45, height=0.8), skin='young_caucasian_male2', proxy='male_generic',
                                tg=[], race=(0.1, 0.1, 0.8)),
}

V4_HEAD_MID = Vector((0.0, 0.013, 1.655))


def _phen(c):
    ph = dict(age=0.5, gender=0.5, muscle=0.5, weight=0.5, proportions=1.0, height=0.5, cupsize=0.5, firmness=0.5)
    ph.update(c['ph'])
    a, s, ca = c['race']
    ph['race'] = dict(african=a, asian=s, caucasian=ca)
    return ph


def head_offset(body):
    hi = body.vertex_groups['Head'].index
    pts = [v.co for v in body.data.vertices if any(g.group == hi and g.weight > 0.9 for g in v.groups)]
    mid = Vector([(min(p[i] for p in pts) + max(p[i] for p in pts)) * 0.5 for i in range(3)])
    return mid - V4_HEAD_MID


def arm_test_factory(body):
    """keep() dos recortes de roupa precisa saber o que é braço: usa os pesos do vértice mais próximo."""
    from mathutils.kdtree import KDTree
    me = body.data
    kd = KDTree(len(me.vertices))
    names = {g.index: g.name for g in body.vertex_groups}
    arm = []
    for v in me.vertices:
        kd.insert(v.co, v.index)
        arm.append(sum(g.weight for g in v.groups if names[g.group].startswith(('Arm', 'ForeArm', 'Hand'))))
    kd.balance()

    def is_arm(p):
        co, i, dist = kd.find(p)
        return arm[i] > 0.5 and p.z > 0.62
    return is_arm


def build_class_mp(cls, hair_style, beard=False, colors=None):
    """Monta a classe no MPFB e devolve as partes no formato do export_hero4 (+ pivôs do modelo)."""
    c = CLASSES_MP[cls]
    for x in [x for x in bpy.data.objects if x.name.startswith('Human')]:
        bpy.data.objects.remove(x, do_unlink=True)
    base, arm = make_human(_phen(c), c['tg'], skin=c['skin'], proxy=c['proxy'])
    pose_hanging(arm)
    src = {ch.name: ch for ch in arm.children}
    pick = lambda suf: next(v for k, v in src.items() if k.endswith(suf))
    M = {}
    for key, suf in (('body', c['proxy']), ('eyes', 'low-poly'), ('brows', 'eyebrow001'), ('lash', 'eyelashes01')):
        s = pick(suf)
        o = bake(s, 'MP_%s__%s' % (key, cls))
        o.data.materials.clear()
        for m in s.data.materials:
            o.data.materials.append(m)
        M[key] = o
    top = max(v.co.z for v in M['body'].data.vertices)
    piv = fit_objects(list(M.values()), M['body'], _joints(arm), top)
    merge_weights(M['body'])
    sample_texture_to_vcol(M['body'])
    sample_texture_to_vcol(M['eyes'])
    cleanup_mpfb(base, arm)
    body = M['body']
    off = head_offset(body)
    piv['Head'] = (0.0, round(off.y, 4), round(1.47 + off.z, 4))
    # cabelo e barba feitos no espaço da cabeça v4 (o corpo vai e volta pelo deslocamento da cabeça)
    T = Matrix.Translation(-off)
    body.data.transform(T)
    hp = KZG['hair_cls'](body, hair_style, 0.018)
    if beard:
        hp.append(KZG['beard'](body, 1.603, 1.582))
    body.data.transform(T.inverted())
    for h in hp:
        h.data.transform(T.inverted())
    hp += [M['brows'], M['lash']]
    for o in hp:
        for g in list(o.vertex_groups):
            o.vertex_groups.remove(g)
        o.data.materials.clear()
    hair = KZG['_join'](hp, 'MP_Cabelo__' + cls)
    # roupa base: o teste de braço usa os pesos do corpo
    old_is_arm = KZG['_is_arm']
    KZG['_is_arm'] = arm_test_factory(body)
    try:
        cl = KZG['clothes'](body)
    finally:
        KZG['_is_arm'] = old_is_arm
    for k, o in cl.items():
        o.name = o.name + '__' + cls
    for k, n in (('tunic', 4000), ('pants', 2000), ('boots', 1600), ('belt', 500), ('bracer', 600)):
        KZG['decimate'](cl[k], n)
    KZG['decimate'](hair, 5000)
    pal = KZG['PALETTE']
    old = dict(pal)
    colors = colors or {}
    pal.update(colors)
    try:
        P = {k: KZG['_hex'](v) for k, v in pal.items()}
        KZG['paint'](hair, pal['hair'], lambda p, n, cc: KZG['_mix'](cc, KZG['_mix'](cc, (1, 0.85, 0.6), 0.25), max(0.0, n.z) * 0.5))
        isa = arm_test_factory(body)

        def tunic(p, n, cc):
            hem = KZG['_smooth'](0.80, 0.77, p.z)
            cuff = KZG['_smooth'](1.155, 1.135, p.z) if isa(p) else 0
            collar = KZG['_smooth'](1.44, 1.465, p.z) if not isa(p) else 0
            cc = KZG['_mix'](cc, P['tunic2'], 0.5 * KZG['_smooth'](1.2, 0.85, p.z))
            return KZG['_mix'](cc, P['trim'], max(hem, cuff, collar))
        KZG['paint'](cl['tunic'], pal['tunic'], tunic)
        KZG['paint'](cl['pants'], pal['pants'])
        KZG['paint'](cl['boots'], pal['boots'], lambda p, n, cc: KZG['_mix'](cc, P['boots2'], max(KZG['_smooth'](0.03, 0.0, p.z), KZG['_smooth'](0.31, 0.34, p.z))))
        KZG['paint'](cl['belt'], pal['belt'], lambda p, n, cc: KZG['_mix'](cc, P['buckle'], KZG['_smooth'](0.022, 0.012, abs(p.x)) * (1 if p.y < -0.05 else 0)))
        KZG['paint'](cl['bracer'], pal['bracer'])
        KZG['paint'](M['eyes'], '#ffffff', lambda p, n, cc: cc) if False else None
    finally:
        pal.clear()
        pal.update(old)
    parts = dict(body=body, eyes=[M['eyes']], hair=hair, **cl)
    return parts, piv


def export_class_mp(parts, piv, cls):
    P = KZG['PIVOTS']
    old = dict(P)
    P.clear()
    P.update({k: v for k, v in piv.items() if k != 'drop'})
    try:
        return KZG['export_class'](parts, cls)
    finally:
        P.clear()
        P.update(old)


# ---------------------------------------------------------------- sets reajustados ao corpo MPFB
def torso_fit(body):
    """Medidas do tronco do corpo novo comparadas com o corpo v4 (onde os sets foram modelados)."""
    names = {g.index: g.name for g in body.vertex_groups}
    pts = []
    for v in body.data.vertices:
        if abs(v.co.z - 1.28) < 0.02:
            arm = sum(g.weight for g in v.groups if names[g.group].startswith(('Arm', 'ForeArm', 'Hand')))
            if arm < 0.3:
                pts.append(v.co)
    w = max(abs(p.x) for p in pts)
    y0, y1 = min(p.y for p in pts), max(p.y for p in pts)
    # folga de 0.06 em x e sem afinar em y: evita a pele atravessar peito/busto
    return dict(sx=min(1.0, max(0.68, min(1.0, w / 0.235)) + 0.06), sy=1.0, yc=(y0 + y1) * 0.5)


def export_set_fit(set_name, objs, groups, body, piv):
    f = torso_fit(body)
    arm_x = piv['ArmL'][0]
    owner = {}
    for g, (names, _) in groups.items():
        for n in names:
            owner[n] = g
    oldcopy = KZG['_copy']

    def fitcopy(o, matid):
        c = oldcopy(o, matid)
        g = owner.get(o.name, 'Armadura')
        if g == 'Armadura':
            me = c.data
            shoulder = 'Ombro' in o.name or o.name.startswith(('KZ6_Ombro', 'KC_Ombro', 'KE_Ombro'))
            for v in me.vertices:
                x, y, z = v.co
                if shoulder:
                    x = x + (arm_x - 0.295) * (1 if x > 0 else -1)
                    y = y + (f['yc'] - 0.012) * 0.8
                else:
                    k = KZG['_smooth'](0.25, 0.6, z)            # capa/saia longa embaixo afina menos
                    x = x * (1 - (1 - f['sx']) * k)
                    y = (y - 0.012) * (1 - (1 - f['sy']) * k) + 0.012 + (f['yc'] - 0.012) * k
                v.co = (x, y, z)
            me.update()
        elif g.startswith('Bota'):
            # dedos do corpo MPFB sao mais longos: alonga a bota pra frente e alarga um pouco
            lx = abs(piv['LegL'][0])
            me = c.data
            for v in me.vertices:
                x, y, z = v.co
                cx = lx if x > 0 else -lx
                k = 1 - KZG['_smooth'](0.12, 0.30, z)
                x = cx + (x - cx) * (1 + 0.06 * k)
                y = y * (1 + 0.14 * k)
                v.co = (x, y, z)
            me.update()
        KZG['transfer_weights'](body, c, KZG['ALLOWED'].get(g))
        return c
    KZG['_copy'] = fitcopy
    try:
        return KZG['export_named'](objs, groups, set_name)
    finally:
        KZG['_copy'] = oldcopy
