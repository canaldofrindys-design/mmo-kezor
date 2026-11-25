# Kezor — 4 classes novas (Lâmina Arcana, Comandante do Estandarte, Invocadora de Ecos, Punho de Brasa).
# Requer kezor_lib, kezor_humans, kezor_monsters, kezor_env e kezor_equip executados antes (usa base_body, piece, SETS,
# helm/armor/pants/gloves/boots, sword_model, HAND_R/HAND_L, PV_ARM e EQUIP_ITEMS).

C2_ITEMS = []


def _c2(name):
    if name in EQUIP_ITEMS:
        EQUIP_ITEMS.remove(name)
    C2_ITEMS.append(name)


# ================================================================ corpos base
def bases2():
    def lam_extra(P):
        b = P['Body']
        b.box((0, -0.122, 1.24), (0.05, 0.012, 0.28), mat('LamRune', '#8fd0ff', emit=2.0))   # runa no peito
        b.box((0, 0.13, 1.1), (0.34, 0.03, 0.5), mat('LamCape', '#2a4f5c'), top=(0.8, 1))     # meia capa
    base_body('LaminaArcanaBase', mat('LamTunic', '#3d6f7a'), mat('LamPants', '#2a2f3a'), LEATHER, HAIR_D, lam_extra); _c2('LaminaArcanaBase')

    def com_extra(P):
        b = P['Body']
        red = mat('ComCrimson', '#8a2a24'); gold = mat('ComGold', '#d4a94a', rough=0.4, metal=0.6)
        b.box((0, -0.124, 0.91), (0.22, 0.015, 0.3), red, top=(1.1, 1))                      # tabardo
        # estandarte nas costas (mastro + bandeira) — marca do Comandante
        b.seg((0.08, 0.17, 0.8), (0.08, 0.2, 2.45), 0.035, WOOD)
        b.box((0.08, 0.2, 2.48), (0.06, 0.06, 0.06), gold)
        b.prism([(0.1, 2.4), (0.55, 2.4), (0.55, 1.95), (0.33, 1.82), (0.1, 1.95)], 0.185, 0.215, red)
        b.prism([(0.25, 2.3), (0.4, 2.3), (0.4, 2.05), (0.25, 2.05)], 0.18, 0.181, gold)
    base_body('ComandanteEstandarteBase', mat('ComTunic', '#5a3a2a'), mat('ComPants', '#2e2a28'), LEATHER, HAIR_B, com_extra); _c2('ComandanteEstandarteBase')

    def inv_extra(P):
        b = P['Body']
        robe = mat('InvRobe', '#2f2f5a')
        b.lathe((0, 0, 0.02), [(0.34, 0.0), (0.31, 0.45), (0.22, 0.98)], 8, robe, sy=0.8)   # saia longa
        b.box((0, -0.122, 1.07), (0.07, 0.012, 0.05), mat('InvSilver', '#c8ccd8', rough=0.4, metal=0.6))
        h = P['Head']
        h.seg((0, 0.12, 1.66), (0, 0.16, 1.2), 0.2, mat('HairSilverBlue', '#a8b4d0'), w2=0.12)   # cabelo longo
    base_body('InvocadoraEcosBase', mat('InvTop', '#3a3a6a'), mat('InvRobe', '#2f2f5a'), LEATHER, mat('HairSilverBlue', '#a8b4d0'), inv_extra); _c2('InvocadoraEcosBase')

    def pun_extra(P):
        wrap = mat('PunWrap', '#d8cdb4'); band = mat('PunBand', '#c4421a')
        for side, part in ((1, 'ArmL'), (-1, 'ArmR')):
            x = 0.31 * side
            P[part].box((x, 0, 0.82), (0.125, 0.135, 0.12), wrap)                             # faixas nos antebraços
        P['Head'].box((0, 0, 1.68), (0.24, 0.26, 0.04), band)                                  # faixa na testa
        P['Head'].seg((0.0, 0.13, 1.68), (0.02, 0.24, 1.5), 0.04, band, w2=0.02)
    base_body('PunhoBrasaBase', mat('PunVest', '#6a3a22'), mat('PunPants', '#3a2a22'), LEATHER, HAIR_D, pun_extra); _c2('PunhoBrasaBase')


# ================================================================ conjuntos
def sets2():
    SETS['Trama'] = (mat('GearWeave', '#3e7f8a', rough=0.45, metal=0.5), mat('GearWeaveD', '#25484f'), mat('GearSilver', '#c8ccd8', rough=0.35, metal=0.8), None)
    SETS['Bastiao'] = (mat('GearBastion', '#9aa3ad', rough=0.35, metal=0.8), mat('GearBastionD', '#7a2a24'), GOLD, mat('BastionGlow', '#ffd36a', emit=3.0))
    SETS['Ecos'] = (mat('GearEcho', '#2f3a6a'), mat('GearEchoD', '#1a1f3d'), mat('GearSilver', '#c8ccd8', rough=0.35, metal=0.8), mat('EchoGlow', '#8fe8ff', emit=4.0))
    SETS['Igneo'] = (mat('GearEmberHide', '#7a3e22'), mat('GearEmberHideD', '#3a2216'), mat('GearEmberBrass', '#e08a3a', rough=0.4, metal=0.6), None)
    for s in ('Trama', 'Bastiao', 'Ecos', 'Igneo'):
        helm(s); _c2(f'{s}_Elmo')
        armor(s); _c2(f'{s}_Armadura')
        for side in ('L', 'R'):
            pants(s, side); _c2(f'{s}_Calca{side}')
            gloves(s, side); _c2(f'{s}_Luva{side}')
            boots(s, side); _c2(f'{s}_Bota{side}')


# ================================================================ armas
def lance_model(name, shaft, head, ring, glow=None, length=2.0, wide=1.0):
    cx, cy = -0.31, -0.07

    def build(b):
        b.cyl((cx, cy, 0.0), 0.028, 0.028, length, 6, shaft)
        b.cyl((cx, cy, length * 0.55), 0.04, 0.04, 0.06, 6, ring)
        top = length
        b.cyl((cx, cy, top), 0.045, 0.035, 0.06, 6, ring)
        b.prism([(cx - 0.06 * wide, top + 0.06), (cx + 0.06 * wide, top + 0.06), (cx, top + 0.5)], cy - 0.012, cy + 0.012, head)
        if wide > 1.1:   # alabarda: lâmina lateral
            b.prism([(cx, top - 0.05), (cx + 0.25, top + 0.02), (cx + 0.22, top - 0.22), (cx, top - 0.18)], cy - 0.012, cy + 0.012, head)
        if glow is not None:
            b.prism([(cx - 0.02, top + 0.12), (cx + 0.02, top + 0.12), (cx, top + 0.42)], cy - 0.016, cy + 0.016, glow)
            for z in (0.4, 1.0, 1.6):
                b.cyl((cx, cy, z), 0.035, 0.035, 0.025, 6, glow)
    piece(name, PV_ARM['R'], build); _c2(name)


def tome_model(name, cover, pages, clasp, glow=None, bell=False):
    hx, hy, hz = HAND_R

    def build(b):
        if bell:   # sino: cabo + campânula + badalo
            b.seg((hx, hy - 0.02, hz - 0.02), (hx, hy - 0.02, hz - 0.2), 0.03, WOOD)
            b.lathe((hx, hy - 0.02, hz - 0.48), [(0.0, 0.28), (0.07, 0.26), (0.11, 0.12), (0.15, 0.0)], 8, cover)
            b.lathe((hx, hy - 0.02, hz - 0.48), [(0.155, 0.0), (0.155, 0.02)], 8, clasp)
            b.gem((hx, hy - 0.02, hz - 0.52), 0.035, 0.08, glow or clasp)
        else:      # tomo aberto preso à mão
            b.tf(loc=(hx - 0.02, hy - 0.16, hz - 0.05), rot=(-20, 0, 0))
            b.box((-0.11, 0, 0), (0.2, 0.03, 0.28), cover)
            b.box((0.11, 0, 0), (0.2, 0.03, 0.28), cover)
            b.box((0, -0.02, 0.0), (0.4, 0.02, 0.25), pages)
            b.box((0, 0.02, 0), (0.04, 0.03, 0.29), clasp)
            if glow is not None:
                b.gem((0, -0.12, 0.05), 0.05, 0.12, glow)
            b.reset()
    piece(name, PV_ARM['R'], build); _c2(name)


def gauntlet_models(name, c1, c2, glow=None, spikes=False):
    for side, suffix in (('R', ''), ('L', '_L')):
        x = 0.31 * (1 if side == 'L' else -1)

        def build(b, x=x):
            b.box((x, -0.005, 0.735), (0.15, 0.16, 0.15), c1, top=(1.05, 1.05))           # punho fechado
            b.box((x, -0.085, 0.76), (0.13, 0.04, 0.06), c2)                               # nós dos dedos
            b.box((x, 0, 0.88), (0.15, 0.16, 0.16), c2, top=(1.1, 1.1))                    # braçal
            if spikes:
                for dz in (0.72, 0.78):
                    b.cone((x, -0.105, dz), 0.02, -0.06, 4, c2)
            if glow is not None:
                b.box((x, -0.108, 0.74), (0.11, 0.01, 0.03), glow)
                b.box((x, 0, 0.96), (0.16, 0.17, 0.02), glow)
        piece(name + suffix, PV_ARM[side], build); _c2(name + suffix)


def weapons2():
    rune = mat('LamBlade', '#cfe6ee', rough=0.25, metal=0.85)
    sword_model('Arma_LaminaRunicaCurta', 0.55, rune, STEEL_D, LEATHER, glow=mat('LamRune', '#8fd0ff', emit=2.0)); _c2('Arma_LaminaRunicaCurta')
    sword_model('Arma_SabreCrepusculo', 0.72, mat('DuskSaber', '#5a6a8a', rough=0.3, metal=0.8), mat('ComGold', '#d4a94a', rough=0.4, metal=0.6), LEATHER, glow=VOID); _c2('Arma_SabreCrepusculo')
    sword_model('Arma_EspadaMarePrismatica', 0.85, mat('PrismBlade', '#9fe8e0', rough=0.2, metal=0.9), GOLD, LEATHER, glow=mat('PrismGlow', '#7af0ff', emit=3.0), wide=1.15); _c2('Arma_EspadaMarePrismatica')
    sword_model('Arma_LaminaAstralPartida', 0.95, mat('AstralBlade', '#1e2240', rough=0.2, metal=0.8), mat('AstralGuard', '#c8ccd8', rough=0.3, metal=0.8), LEATHER, glow=mat('AstralGlow', '#b98bff', emit=4.0), wide=1.3); _c2('Arma_LaminaAstralPartida')
    lance_model('Arma_LancaRecruta', WOOD, STEEL, STEEL_D)
    lance_model('Arma_LancaVanguarda', mat('VanWood', '#4a2e1c'), mat('VanHead', '#b8c2cc', metal=0.8), GOLD, length=2.1)
    lance_model('Arma_AlabardaBastiao', mat('BastionShaft', '#2e2a28'), mat('GearBastion', '#9aa3ad', rough=0.35, metal=0.8), GOLD, length=2.2, wide=1.3)
    lance_model('Arma_HasteJuramentoSolar', mat('SolarShaft', '#e8e2d0'), mat('SolarHead', '#ffd36a', metal=0.8), GOLD, glow=mat('SolarGlow', '#ffb347', emit=4.0), length=2.3, wide=1.4)
    tome_model('Arma_TomoEcoMenor', mat('TomeLeather', '#3a2a5a'), mat('Pages', '#e8e2d0'), mat('InvSilver', '#c8ccd8', rough=0.4, metal=0.6))
    tome_model('Arma_SinoPrataVelada', mat('BellSilver', '#c8ccd8', rough=0.3, metal=0.9), None, GOLD, glow=mat('EchoGlowSoft', '#8fe8ff', emit=2.0), bell=True)
    tome_model('Arma_GrimorioMilEcos', mat('GrimoireCover', '#1a1f3d'), mat('Pages', '#e8e2d0'), GOLD, glow=mat('EchoGlow', '#8fe8ff', emit=4.0))
    tome_model('Arma_SinoSilencioEterno', mat('SilenceBell', '#2a2440', rough=0.2, metal=0.8), None, mat('InvSilver', '#c8ccd8', rough=0.4, metal=0.6), glow=mat('SilenceGlow', '#e0b8ff', emit=4.0), bell=True)
    gauntlet_models('Arma_ManoplasCouroCru', mat('Rawhide', '#8a6a4a'), LEATHER)
    gauntlet_models('Arma_ManoplasFerroFundido', mat('CastIron', '#4a4e54', rough=0.5, metal=0.7), STEEL_D, spikes=True)
    gauntlet_models('Arma_PunhosMagma', BASALT, BASALT2, glow=EMB)
    gauntlet_models('Arma_NucleoBrasaViva', mat('CoreShell', '#2a1a14'), mat('CoreRim', '#e08a3a', rough=0.4, metal=0.6), glow=EMB2, spikes=True)


# ================================================================ ecos (invocações)
def echo(coll, name, robe_hex, glow_hex, scale=1.0):
    robe = mat(name + 'Robe', robe_hex); glow = mat(name + 'Glow', glow_hex, emit=4.0)
    b = B(scale)
    b.lathe((0, 0, 0.45), [(0.0, 0.0), (0.18, 0.2), (0.28, 0.7), (0.2, 1.15), (0.12, 1.3)], 7, robe)
    b.ico((0, 0, 1.9), (0.16, 0.16, 0.18), robe, seed=101)
    for x in (-0.05, 0.05):
        b.box((x, -0.15, 1.92), (0.04, 0.02, 0.03), glow)
    b.lathe((0, 0, 1.7), [(0.24, 0), (0.18, 0.28), (0.0, 0.4)], 7, robe)
    parts = []
    for side, pname in ((1, 'ArmL'), (-1, 'ArmR')):
        a = B(scale)
        a.seg((side * 0.22, 0, 1.6), (side * 0.4, -0.2, 1.25), 0.08, robe, w2=0.04)
        a.gem((side * 0.42, -0.24, 1.2), 0.05, 0.12, glow)
        parts.append((pname, a, (side * 0.22, 0, 1.6)))
    for i in range(3):
        o = B(scale); o.gem((0.9, 0, 1.4), 0.06, 0.14, glow)
        parts.append(('Orb%d' % i, o, (0.9, 0, 1.4)))
    r = finish_custom(name, 'Monsters', coll, b, parts)
    C2_ITEMS.append(name)
    return r


def build_classes2():
    C2_ITEMS.clear()
    bases2()
    sets2()
    weapons2()
    cm = collection('Monsters')
    echo(cm, 'EcoSombrio', '#2a2440', '#b98bff')
    echo(cm, 'EcoLuminoso', '#d8e0ff', '#8fe8ff', scale=1.1)
    # vitrine
    place_row(['LaminaArcanaBase', 'ComandanteEstandarteBase', 'InvocadoraEcosBase', 'PunhoBrasaBase'], 64, 1.6)
    place_row([n for n in C2_ITEMS if n.endswith(('_Elmo', '_Armadura'))], 67, 1.2)
    place_row([n for n in C2_ITEMS if n.startswith(('Trama_', 'Bastiao_', 'Ecos_', 'Igneo_')) and not n.endswith(('_Elmo', '_Armadura'))], 70, 0.9)
    place_row([n for n in C2_ITEMS if n.startswith('Arma_')], 73, 1.0)
    place_row(['EcoSombrio', 'EcoLuminoso'], 77, 2.5)
    return list(C2_ITEMS)
