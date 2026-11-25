# Kezor — marco 2: baú da cidade (Ilsa, a Guardiã + baú de ferro), portais, Pântano de Turfa Negra e Cripta de Vidro Negro.
# Requer kezor_lib.py, kezor_humans.py, kezor_monsters.py e kezor_env.py executados antes, no mesmo namespace.
# Convenções iguais às dos outros scripts (frente -Y, origem nos pés, Body -> partes com pivô na articulação).

W2_ITEMS = []


def _done(r):
    W2_ITEMS.append(r.name)
    return r


# ================================================================ baú da cidade
def ilsa(coll, name='IlsaGuardia', cat='Npcs'):
    cloak = mat('IlsaCloak', '#2e5552')
    dress = mat('IlsaDress', '#3c3a48')
    trim = mat('IlsaTrim', '#c8a24a', rough=0.4, metal=0.6)
    hair = mat('HairSilver', '#cfcac2')
    key = mat('KeyGlow', '#ffd36a', emit=3.0)
    P = hparts(0.97)
    b = P['Body']
    std_body(b, dress, LEATHER, dress, w=0.92)
    b.lathe((0, 0, 0), [(0.34, 0.03), (0.31, 0.5), (0.21, 1.02)], 8, dress, sy=0.8)
    b.box((0, 0.13, 0.86), (0.46, 0.04, 1.02), cloak, top=(0.8, 1))                       # capa longa
    b.lathe((0, 0, 1.30), [(0.27, 0), (0.2, 0.08), (0.12, 0.13)], 8, cloak, sy=0.78)        # gola da capa
    b.box((0, -0.124, 1.07), (0.08, 0.012, 0.05), trim)
    # argola de chaves no cinto (as chaves brilham de leve)
    b.lathe((0.2, -0.12, 0.98), [(0.06, 0), (0.06, 0.012)], 8, trim, sy=0.3)
    for i, dx in enumerate((-0.03, 0.0, 0.03)):
        b.seg((0.2 + dx, -0.125, 0.96), (0.2 + dx * 1.5, -0.13, 0.86 - i * 0.02), 0.014, key if i == 1 else trim, d=0.01)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.29 * side
        std_arm(a, x, cloak, SKIN, SKIN, w=0.92)
        a.box((x, 0, 1.05), (0.13, 0.14, 0.05), trim)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        std_leg(P[part], 0.11 * side, dress, dress, LEATHER)
    h = P['Head']
    std_head(h, SKIN, hair=hair)
    h.seg((0, 0.12, 1.66), (0, 0.18, 1.38), 0.09, hair, w2=0.05)                           # coque e mecha
    h.box((0, 0.13, 1.69), (0.1, 0.08, 0.08), hair)
    h.box((0, -0.12, 1.715), (0.2, 0.01, 0.02), trim)                                      # diadema
    # lamparina na mão direita
    a = P['ArmR']
    a.seg((-0.29, -0.06, 0.72), (-0.29, -0.06, 0.60), 0.01, trim)
    a.box((-0.29, -0.06, 0.55), (0.08, 0.08, 0.1), trim, top=(0.8, 0.8))
    a.box((-0.29, -0.06, 0.55), (0.06, 0.06, 0.07), mat('LampGlow', '#ffcf7a', emit=4.0))
    piv = {k: v for k, v in PIV.items()}
    piv['ArmL'] = (0.29, 0, 1.40); piv['ArmR'] = (-0.29, 0, 1.40); piv['LegL'] = (0.11, 0, 0.92); piv['LegR'] = (-0.11, 0, 0.92)
    return _done(finish(name, cat, coll, P, piv=piv))


def bau(coll, name='BauGuardia'):
    wood = mat('ChestWood', '#5a3a22')
    iron = mat('ChestIron', '#3e4148', rough=0.45, metal=0.7)
    lock = mat('ChestLock', '#ffd36a', emit=3.0)
    b = B()
    b.box((0, 0, 0.3), (1.0, 0.62, 0.6), wood)
    b.lathe((0, 0, 0.6), [(0.31, 0), (0.25, 0.14), (0.0, 0.2)], 8, wood, rot0=0, sx=1.62, sy=1.0)  # tampa arredondada
    for x in (-0.38, 0.0, 0.38):
        b.box((x, 0, 0.42), (0.07, 0.64, 0.84), iron)
    b.box((0, 0, 0.04), (1.04, 0.66, 0.08), iron)
    b.box((0, -0.325, 0.5), (0.14, 0.03, 0.16), iron)
    b.box((0, -0.34, 0.5), (0.06, 0.02, 0.07), lock)
    for sx in (-1, 1):
        b.box((sx * 0.52, 0, 0.38), (0.04, 0.2, 0.06), iron)                                 # alças
    return _done(env_single(name, coll, b))


# ================================================================ portal
def portal(coll, name='PortalArco'):
    stone = mat('PortalStone', '#5e5a55')
    stone2 = mat('PortalStoneD', '#45423e')
    rune = mat('PortalRune', '#ffffff', emit=4.0)        # cor do vórtice vem do _Tint no cliente
    b = B()
    b.box((0, 0, 0.1), (2.6, 1.2, 0.2), stone2)
    for sx in (-1, 1):
        b.box((sx * 1.05, 0, 1.3), (0.42, 0.5, 2.4), stone, top=(0.85, 0.85))
        for z in (0.6, 1.3, 2.0):
            b.box((sx * 1.05, -0.255, z), (0.12, 0.02, 0.2), rune)
    b.box((0, 0, 2.62), (2.7, 0.6, 0.36), stone, top=(0.9, 0.9))
    b.gem((0, -0.31, 2.62), 0.12, 0.3, rune)
    vortex = B()
    vortex.lathe((0, 0, 1.3), [(0, -1.05), (0.55, -0.7), (0.8, 0.0), (0.55, 0.7), (0, 1.05)], 10, rune, sy=0.08)
    return _done(env_single(name, coll, b, [('Vortex', vortex, (0, 0, 1.3))]))


# ================================================================ guildas
def hadrik(coll, name='HadrikGuildas', cat='Npcs'):
    coat = mat('HadrikCoat', '#6a2f2a'); trim = mat('HadrikTrim', '#d4a94a', rough=0.4, metal=0.6)
    shirt = mat('HadrikShirt', '#d8cdb4'); beard = mat('BeardGrey', '#9a948a'); banner = mat('GuildBanner', '#2f4f7a')
    P = hparts(1.02)
    b = P['Body']
    std_body(b, coat, LEATHER, mat('HadrikPants', '#2e2a28'), w=1.05)
    b.box((0, -0.124, 1.24), (0.16, 0.012, 0.3), shirt)                                    # camisa à mostra
    b.box((0, 0.0, 0.88), (0.42, 0.25, 0.5), coat, top=(0.9, 0.95))                        # casaco longo
    b.box((0, -0.13, 0.9), (0.03, 0.012, 0.5), trim)
    for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
        a = P[part]; x = 0.31 * side
        std_arm(a, x, coat, SKIN, SKIN)
        a.box((x, 0, 1.0), (0.14, 0.15, 0.05), trim)
    for side, part in ((1, 'LegL'), (-1, 'LegR')):
        std_leg(P[part], 0.12 * side, mat('HadrikPants', '#2e2a28'), mat('HadrikPants', '#2e2a28'), LEATHER)
    h = P['Head']
    std_head(h, SKIN, hair=beard)
    h.box((0, -0.07, 1.47), (0.2, 0.12, 0.14), beard, top=(1.1, 1.1))
    h.box((0, 0.0, 1.74), (0.27, 0.29, 0.05), coat)                                        # chapéu de aba
    h.box((0, 0.0, 1.8), (0.2, 0.22, 0.1), coat, top=(0.9, 0.9))
    h.box((0, -0.12, 1.77), (0.21, 0.01, 0.03), trim)
    # estandarte da guilda na mão esquerda
    a = P['ArmL']
    a.seg((0.31, -0.06, 0.2), (0.31, -0.06, 2.3), 0.04, WOOD)
    a.box((0.31, -0.06, 2.33), (0.08, 0.08, 0.08), trim)
    a.prism([(0.33, 2.25), (0.85, 2.25), (0.85, 1.65), (0.59, 1.5), (0.33, 1.65)], -0.075, -0.045, banner)
    a.prism([(0.53, 2.05), (0.65, 2.05), (0.65, 1.8), (0.53, 1.8)], -0.08, -0.07, trim)
    return _done(finish(name, cat, coll, P))


def build_city():
    W2_ITEMS.clear()
    c_npc = collection('Npcs'); c_env = collection('Environment')
    ilsa(c_npc); bau(c_env); portal(c_env)
    for i, n in enumerate(W2_ITEMS):
        bpy.data.objects[n].location = (40 + i * 2.5, 6, 0)
    return list(W2_ITEMS)
