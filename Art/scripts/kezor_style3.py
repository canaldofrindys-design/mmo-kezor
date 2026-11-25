# Kezor — estilo heroico (v3): conjuntos com silhueta mais pesada, ombreiras grandes em camadas, capas e tabardos,
# elmos marcantes, frisos/filigrana low poly e motivos próprios do Kezor (brasa, cinza, runas). Mais contraste de
# materiais na cor de vértice (metal polido brilha mais no alto; couro e tecido ficam foscos).
# Requer kezor_heroes2.py executado antes (usa cbox, tone, _rivets, SETS, STYLE e o piece com chanfro).
import math


def flame(b, c, w, h, m, y0=-0.012, y1=0.012, lean=0.0):
    """Chama de brasa estilizada (prisma em zigue-zague), motivo do Kezor."""
    cx, cz = c[0], c[2]
    pts = [(cx - w / 2, cz), (cx - w * 0.28 + lean * 0.3, cz + h * 0.55), (cx - w * 0.1 + lean * 0.6, cz + h * 0.35),
           (cx + lean, cz + h), (cx + w * 0.12 + lean * 0.6, cz + h * 0.42), (cx + w * 0.3 + lean * 0.3, cz + h * 0.62), (cx + w / 2, cz)]
    b.prism(pts, c[1] + y0, c[1] + y1, m)


def rune_stud(b, c, m, glow=None, r=0.02):
    """Rebite de runa: losango facetado com centro brilhante."""
    b.lathe(c, [(0, -0.002), (r, 0.004), (r * 0.6, 0.012), (0, 0.014)], 4, m, rot0=0)
    if glow is not None:
        b.lathe((c[0], c[1] - 0.002, c[2]), [(0, 0.012), (r * 0.35, 0.014), (0, 0.018)], 4, glow, rot0=0)


def filigree(b, x0, x1, z, y, m, n=5, h=0.012):
    """Friso com filigrana simples: barra fina + pequenos losangos."""
    cbox(b, ((x0 + x1) / 2, y, z), (abs(x1 - x0), 0.01, h), m, bev=0.003)
    for i in range(n):
        x = x0 + (x1 - x0) * (i + 0.5) / n
        b.tf(loc=(x, y - 0.006, z), rot=(0, 45, 0))
        cbox(b, (0, 0, 0), (0.016, 0.008, 0.016), m, bev=0.002)
        b.reset()


def detail_piece(name, b):
    s, part = _set_of(name)
    if s is None:
        return
    c1, c2, c3, glow = SETS[s]
    st = STYLE.get(s, s)
    heavy = st == 'Runica'
    scale = st == 'Escamas'
    cloth = st == 'Veu'
    leather = st == 'Couro'
    emb = glow or c3
    c1L, c2D = tone(c1, 1.22), tone(c2, 0.7)
    if part == 'Armadura':
        # ---------------- ombreiras grandes em camadas (largas, sobrepostas, com friso e motivo de brasa)
        for x in (-1, 1):
            px = x * 0.27
            if cloth:
                for k in range(3):
                    cbox(b, (px * (1 + k * 0.06), 0.0, 1.42 - k * 0.045), (0.2 - k * 0.012, 0.28, 0.05), c1 if k % 2 == 0 else c2, bev=0.014, top=(0.8, 0.9))
                cbox(b, (px * 1.18, -0.02, 1.33), (0.04, 0.22, 0.03), c3, bev=0.006)
                if glow is not None:
                    rune_stud(b, (px * 1.05, -0.15, 1.43), c3, glow)
                continue
            n = 4 if heavy else 3
            for k in range(n):
                w = (0.26 if heavy else 0.22) - k * 0.016
                d = (0.31 if heavy else 0.27) - k * 0.01
                b.tf(loc=(px + x * (0.01 + k * 0.03), 0, 1.47 - k * 0.058), rot=(0, x * (14 + k * 9), 0))
                cbox(b, (0, 0, 0), (w, d, 0.055), c1L if k == 0 else (c1 if k % 2 == 0 else c2), bev=0.016, top=(0.88, 0.94))
                cbox(b, (x * (w / 2 - 0.012), 0, -0.012), (0.018, d * 0.96, 0.03), c3, bev=0.005)   # aro de metal
                b.reset()
            b.tf(loc=(px + x * 0.02, 0, 1.505), rot=(0, x * 14, 0))
            filigree(b, -0.09, 0.09, 0.0, -0.13, c3, n=4)
            b.reset()
            rune_stud(b, (px + x * 0.04, -0.158, 1.45), c3, glow, r=0.024)
            if heavy or st == 'Escamas':                                                          # chama de brasa erguida
                b.tf(loc=(px + x * 0.07, 0.0, 1.5), rot=(0, x * 20, 0))
                flame(b, (0, 0, 0), 0.1, 0.13 if heavy else 0.09, emb if heavy else c3, lean=x * 0.02)
                b.reset()
        # ---------------- gorjal alto e peitoral com filigrana e emblema de runa
        cbox(b, (0, 0, 1.445), (0.26, 0.24, 0.07), c2, bev=0.014, top=(0.82, 0.88))
        filigree(b, -0.12, 0.12, 1.41, -0.128, c3, n=6)
        b.lathe((0, -0.15, 1.27), [(0, 0), (0.05, 0.0), (0.058, 0.014), (0.04, 0.026), (0, 0.028)], 8, c3, rot0=0, sx=1, sy=0.5)
        b.gem((0, -0.162, 1.28), 0.03, 0.065, emb)
        for x in (-1, 1):
            filigree(b, x * 0.05, x * 0.17, 1.17, -0.132, c3, n=3, h=0.01)
        # ---------------- capa (pesados, escamas, couro) ou manto longo (tecido): tecido em 3 painéis com dobras
        cape = c2D if not cloth else c2
        top_z, bot_z = 1.43, (0.32 if cloth else 0.42)
        for i, (x0, x1, yy, rz) in enumerate(((-0.2, -0.07, 0.15, -6), (-0.075, 0.075, 0.165, 0), (0.07, 0.2, 0.15, 6))):
            b.tf(loc=(0, 0, 0), rot=(0, 0, 0))
            b.prism([(x0, top_z), (x1, top_z), (x1 * 1.25, bot_z + (0.04 if i != 1 else 0)), (x0 * 1.25, bot_z + (0.04 if i != 1 else 0))],
                    yy, yy + 0.022, cape if i != 1 else tone(cape, 1.12, '_capeL'))
        cbox(b, (0, 0.17, 1.42), (0.42, 0.04, 0.05), c3, bev=0.01)                               # barra/fecho da capa
        flame(b, (0, 0.188, 0.95), 0.14, 0.2, emb, y0=0.0, y1=0.006)                           # brasão de brasa na capa
        if heavy or scale:                                                                      # tabardo frontal
            b.prism([(-0.1, 1.04), (0.1, 1.04), (0.085, 0.6), (0, 0.54), (-0.085, 0.6)], -0.142, -0.13, c2)
            b.prism([(-0.06, 0.95), (0.06, 0.95), (0, 0.8)], -0.146, -0.142, emb if heavy else c3)
        if scale:
            for row in range(3):
                for i in range(4):
                    cbox(b, (-0.12 + i * 0.08 + (row % 2) * 0.04, -0.13, 1.21 - row * 0.05), (0.06, 0.012, 0.05), c2 if (i + row) % 2 else c1, bev=0.006, top=(0.6, 1))
        if leather:
            for y in (-0.125, 0.125):                                                           # correias cruzadas
                b.seg((-0.18, y, 1.38), (0.16, y, 1.0), 0.03, c2, d=0.012)
            rune_stud(b, (-0.01, -0.14, 1.19), c3, None, r=0.022)
    elif part == 'Elmo':
        if heavy:   # elmo com crista em lâmina e asas varridas de metal
            b.prism([(-0.01, 1.74), (0.01, 1.74), (0.02, 1.98), (-0.06, 1.9)], -0.06, 0.12, c3)
            for x in (-1, 1):
                b.prism([(x * 0.13, 1.68), (x * 0.135, 1.62), (x * 0.3, 1.86), (x * 0.24, 1.88)], -0.02, 0.02, c1L)
                b.prism([(x * 0.14, 1.66), (x * 0.25, 1.82), (x * 0.22, 1.83)], -0.025, -0.02, c3)
                cbox(b, (x * 0.128, -0.06, 1.53), (0.028, 0.11, 0.12), c2, bev=0.008)
            if glow is not None:
                cbox(b, (0, -0.152, 1.63), (0.16, 0.01, 0.016), glow, bev=0.002)
            filigree(b, -0.1, 0.1, 1.72, -0.13, c3, n=4)
        elif scale:  # protetor nasal, abas e penacho de escamas
            cbox(b, (0, -0.152, 1.58), (0.03, 0.022, 0.12), c3, bev=0.006)
            for x in (-1, 1):
                b.prism([(x * 0.13, 1.66), (x * 0.14, 1.5), (x * 0.2, 1.62)], -0.03, 0.06, c2)
            for i in range(4):
                b.tf(loc=(0, 0.02 + i * 0.045, 1.79 - i * 0.02), rot=(-20 - i * 12, 0, 0))
                cbox(b, (0, 0, 0), (0.05, 0.03, 0.08), c1 if i % 2 == 0 else c2, bev=0.006, top=(0.5, 1))
                b.reset()
        elif cloth:  # capuz alto com ponta, máscara de tecido e gema
            b.seg((0, 0.06, 1.74), (0, 0.24, 1.98), 0.16, c1, w2=0.03)
            cbox(b, (0, -0.142, 1.53), (0.16, 0.02, 0.07), c2, bev=0.008)
            if glow is not None:
                b.gem((0, -0.15, 1.69), 0.024, 0.05, glow)
            filigree(b, -0.09, 0.09, 1.66, -0.142, c3, n=3)
        else:        # couro: elmo de caçador com viseira e penas
            cbox(b, (0, -0.002, 1.645), (0.27, 0.29, 0.025), c2, bev=0.006)
            cbox(b, (0, -0.14, 1.66), (0.2, 0.04, 0.03), c2D, bev=0.008)
            for i in range(3):
                b.seg((0.1, 0.1, 1.7), (0.14 + i * 0.02, 0.2, 1.9 - i * 0.04), 0.03, tone(c3, 1.0 - i * 0.15, f'_fth{i}'), w2=0.008)
        _rivets(b, [(-0.1, -0.14, 1.64), (0.1, -0.14, 1.64)], c3, 0.009)
    elif part.startswith('Luva'):
        x = 0.31 * (1 if part.endswith('L') else -1)
        cbox(b, (x, -0.075, 0.72), (0.11, 0.025, 0.04), c3, bev=0.005)                         # nós dos dedos
        b.lathe((x, 0, 0.8), [(0.085, 0.0), (0.1, 0.06), (0.095, 0.09)], 8, c2)                 # punho em sino
        cbox(b, (x, -0.096, 0.85), (0.06, 0.01, 0.03), c3, bev=0.003)
        if heavy or scale:
            b.tf(loc=(x, 0.06, 0.86), rot=(-30, 0, 0))
            flame(b, (0, 0, 0), 0.05, 0.07, emb if heavy else c3)
            b.reset()
    elif part.startswith('Bota'):
        x = 0.12 * (1 if part.endswith('L') else -1)
        cbox(b, (x, -0.172, 0.062), (0.15, 0.06, 0.09), c3, bev=0.01)                           # biqueira de metal
        cbox(b, (x, 0, 0.225), (0.18, 0.2, 0.03), c2, bev=0.006)
        b.lathe((x, 0, 0.27), [(0.095, 0.0), (0.11, 0.05)], 8, c2D)                             # cano largo
        rune_stud(b, (x, -0.112, 0.235), c3, glow, r=0.016)
    elif part.startswith('Calca'):
        x = 0.12 * (1 if part.endswith('L') else -1)
        b.tf(loc=(x, -0.06, 0.83), rot=(-8, 0, 0))
        cbox(b, (0, 0, 0), (0.18, 0.04, 0.17), c2, bev=0.01, top=(1.12, 1.0))                   # coxote
        cbox(b, (0, -0.022, -0.07), (0.16, 0.01, 0.014), c3, bev=0.003)
        b.reset()
        cbox(b, (x, -0.1, 0.52), (0.13, 0.055, 0.12), c1L if not cloth else c2, bev=0.014)      # joelheira
        rune_stud(b, (x, -0.128, 0.52), c3, glow, r=0.016)


def bake_vertex_colors(obj):
    """Cor × AO × variação; metal polido ganha brilho no alto e nas faces voltadas para a luz, couro e tecido ficam foscos."""
    me = obj.data
    attr = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    ao = me.color_attributes.get('AO')
    cols, metal = [], []
    for m in me.materials:
        cols.append(_mat_vcol(m))
        mv = 0.0
        if m and m.node_tree:
            bs = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if bs:
                for sck in bs.inputs:
                    if sck.identifier == 'Metallic':
                        mv = float(sck.default_value)
        metal.append(mv)
    if not cols:
        cols, metal = [(1, 1, 1, 1)], [0.0]
    light = (-0.45, -0.55, 0.7)
    for poly in me.polygons:
        mi = min(poly.material_index, len(cols) - 1)
        c, mt = cols[mi], metal[mi]
        h = ((poly.index * 2654435761) & 0xffff) / 65535.0
        n = poly.normal
        lam = max(0.0, n.x * light[0] + n.y * light[1] + n.z * light[2])
        if mt > 0.5:   # metal polido: mais contraste
            var = (1.0 + (h - 0.5) * 0.06) * (0.82 + 0.38 * lam) * (1.0 + 0.12 * max(0.0, n.z))
        else:          # couro e tecido: foscos, pouca variação
            var = (1.0 + (h - 0.5) * 0.08) * (0.94 + 0.1 * lam) * (1.0 + 0.04 * n.z)
        for li in poly.loop_indices:
            if c[3] < 1.0:
                attr.data[li].color_srgb = c
                continue
            o = ao.data[li].color[0] if ao is not None else 1.0
            k = var * (0.7 + 0.3 * o)
            attr.data[li].color_srgb = (min(1, c[0] * k), min(1, c[1] * k), min(1, c[2] * k), 1.0)
    if ao is not None:
        me.color_attributes.remove(ao)
        attr = me.color_attributes.get('Col')
    me.color_attributes.active_color = attr
    try:
        me.color_attributes.render_color_index = me.color_attributes.find('Col')
    except Exception:
        pass


def rebuild_sets3():
    armor_sets()
    sets2()
    return sorted(o.name for o in bpy.data.objects if o.get('kezor_root') and o.get('category') == 'Equip'
                  and o.name.startswith(tuple(s + '_' for s in SETS)))
