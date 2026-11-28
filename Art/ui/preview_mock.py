"""
Prévia da interface "clássica premium" fora da Unity: monta uma página HTML com as mesmas peças
(Assets/Resources/UI/Premium), os mesmos 9-slices (CSS border-image = -unity-slice-* com slice-scale 0.5) e as
medidas da USS, e fotografa com o Chromium do Playwright.

    python Art/ui/preview_mock.py [fundo.png]

Gera Art/previews/ui_premium_hud.jpg, ui_premium_barra.jpg e ui_premium_lojas.jpg. Serve só para revisar a arte:
o jogo de verdade usa a USS (KezorPremium.uss).
"""
import os
import sys
import json
import subprocess

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
P = 'file://' + os.path.join(ROOT, 'Assets/Resources/UI/Premium')
ART = 'file://' + os.path.join(ROOT, 'Assets/Resources/UI/Art')
ICONS = os.path.join(ROOT, 'Assets/Resources/UI/Icons')
FONTS = 'file://' + os.path.join(ROOT, 'Assets/Resources/Fonts')
OUT = os.path.join(ROOT, 'Art', 'previews')

# mapa do atlas de ícones (coordenadas da Unity: y de baixo para cima)
ATLAS_H = 1824
IMAP = {}
for name in ('items', 'skills'):
    for ln in open(os.path.join(ICONS, name + '_map.txt')):
        p = ln.split()
        if len(p) == 5:
            IMAP[p[0]] = (name, int(p[1]), int(p[2]), int(p[3]), int(p[4]))


def icon(key, w, h, extra=''):
    """div com o ícone do atlas recortado e escalado para w x h (mantém proporção)."""
    atlas, x, y, iw, ih = IMAP[key]
    H = ATLAS_H if atlas == 'items' else 256
    sc = min(w / iw, h / ih)
    top = H - y - ih
    ox = (w - iw * sc) / 2
    oy = (h - ih * sc) / 2
    url = 'file://' + os.path.join(ICONS, atlas + '.png')
    aw = 2048 * sc
    ah = H * sc
    return (f'<div class="ic" style="width:{w}px;height:{h}px;{extra}">'
            f'<div style="position:absolute;left:{ox:.1f}px;top:{oy:.1f}px;width:{iw*sc:.1f}px;height:{ih*sc:.1f}px;'
            f'background:url({url}) {-x*sc:.1f}px {-top*sc:.1f}px/{aw:.1f}px {ah:.1f}px no-repeat"></div></div>')


def slice9(img, sl, extra=''):
    """CSS equivalente a background-image + -unity-slice-* (px de textura) + -unity-slice-scale: 0.5."""
    if isinstance(sl, int):
        sl = (sl, sl, sl, sl)
    t, r, b, l = sl
    return (f'border-style:solid;border-width:{t/2}px {r/2}px {b/2}px {l/2}px;'
            f'border-image:url({P}/{img}.png) {t} {r} {b} {l} fill / {t/2}px {r/2}px {b/2}px {l/2}px stretch;{extra}')


CSS = f"""
@font-face {{ font-family: Cinzel; src: url({FONTS}/Cinzel.ttf); font-weight: 400 900; }}
@font-face {{ font-family: Inter; src: url({FONTS}/Inter.ttf); font-weight: 100 900; }}
* {{ box-sizing: border-box; }}
body {{ margin:0; width:1600px; height:900px; overflow:hidden; font-family: Inter; font-size:14px; color:#efe6d4; background:#111; position:relative; }}
.bg {{ position:absolute; inset:0; background-size:cover; background-position:center; filter: brightness(0.92); }}
.abs {{ position:absolute; }}
.ic {{ position:relative; }}
.title {{ font-family:Cinzel; font-weight:700; color:#f2d48c; text-shadow:0 1px 2px #000, 0 0 1px #2a0e00; }}
.h2 {{ font-family:Cinzel; font-weight:700; color:#e8c27a; font-size:15px; letter-spacing:1px; text-shadow:0 1px 1px #000; }}
.t2 {{ color:#b9ad97; font-size:12px; }} .t3 {{ color:#7d7366; font-size:11px; }}
.amber {{ color:#ffb347; }} .gold {{ color:#e8c27a; }}
.out {{ text-shadow: 1px 1px 1px #000, -1px 0 1px #000, 0 -1px 1px #000; }}

.window {{ position:absolute; padding:0 0 24px 0; z-index:1; }}
.window::before {{ content:''; position:absolute; inset:0; {slice9('window', 128)} z-index:0; }}
.window > * {{ position:relative; z-index:1; }}
.window > .crown {{ position:absolute; top:-54px; left:50%; width:240px; height:96px; margin-left:-120px; background:url({P}/crown.png) 0 0/100% 100%; z-index:2; }}
.hudcrest {{ position:absolute !important; top:-60px; left:50%; width:300px; height:84px; margin-left:-150px; background:url({P}/hud_crest.png) 0 0/100% 100%; }}
.whead {{ height:38px; margin:34px 30px 0 30px; padding:0 10px 0 34px; display:flex; align-items:center; position:relative; }}
.whead::before {{ content:''; position:absolute; inset:0; {slice9('header', (32, 112, 32, 112))} }}
.whead .title {{ flex:1; text-align:center; font-size:17px; letter-spacing:2px; position:relative; }}
.close {{ width:26px; height:26px; background:url({P}/close.png) center/contain no-repeat; position:relative; }}
.crest {{ height:22px; margin:-4px 0 4px 0; background:url({P}/crest.png) center/contain no-repeat; }}
.wbody {{ padding:0 20px; display:flex; flex-direction:column; }}
.divider {{ height:12px; margin:6px 0; background:url({P}/divider.png) center/contain no-repeat; }}

.panel {{ position:absolute; padding:16px 20px; }}
.panel::before {{ content:''; position:absolute; inset:0; {slice9('panel', 72)} z-index:0; }}
.panel > * {{ position:relative; z-index:1; }}
.pdark {{ position:relative; {slice9('panel_dark', 24)} }}

.btn {{ display:inline-flex; align-items:center; justify-content:center; min-height:30px; padding:0 16px; margin:2px; font-weight:700; font-size:13px; color:#f0e2c4;
        text-shadow:0 1px 1px #000; position:relative; }}
.btn::before {{ content:''; position:absolute; inset:0; {slice9('btn', 28)} z-index:0; }}
.btn span {{ position:relative; z-index:1; }}
.btn.small {{ min-height:26px; font-size:12px; padding:0 12px; }}
.btn.hover::before {{ {slice9('btn_hover', 28)} }}
.btn.primary::before {{ {slice9('btn_ember', 28)} }}
.btn.on::before {{ {slice9('btn_down', 28)} }} .btn.on {{ color:#ffcf7a; }}
.btn.off::before {{ {slice9('btn_off', 28)} }} .btn.off {{ color:#55504a; }}

.tab {{ display:inline-flex; align-items:center; height:26px; padding:0 12px; margin-right:2px; font-weight:700; font-size:12px; color:#b9ad97; position:relative; }}
.tab::before {{ content:''; position:absolute; inset:0; {slice9('tab', (18, 20, 6, 20))} }}
.tab.on {{ color:#ffd58a; }} .tab.on::before {{ {slice9('tab_on', (18, 20, 6, 20))} }}
.tab span {{ position:relative; }}
.field {{ position:relative; height:28px; padding:0 10px; display:flex; align-items:center; color:#7d7366; font-style:italic; }}
.field::before {{ content:''; position:absolute; inset:0; {slice9('field', 16)} }}
.field span {{ position:relative; }}

.slot {{ position:absolute; }}
.slot::before {{ content:''; position:absolute; inset:0; {slice9('slot', 32)} }}
.slot.hover::before {{ {slice9('slot_hover', 32)} }}
.slot.sel::before {{ {slice9('slot_sel', 32)} }}
.slot.ember::before {{ {slice9('slot_ember', 32)} }}
.slot > * {{ position:absolute; }}
.slot .ic {{ left:6px; top:6px; }}
.key {{ left:5px; top:3px; font-size:12px; font-weight:700; color:#ffd58a; }}
.cnt {{ right:5px; bottom:2px; font-size:13px; font-weight:700; color:#fff3d6; }}
.slotname {{ left:0; right:0; bottom:4px; text-align:center; font-family:Cinzel; font-size:9px; color:#8a7a60; }}
.guide {{ opacity:.18; background-size:contain; background-repeat:no-repeat; background-position:center; }}
.rel {{ position:relative; }}

.grid {{ position:relative; }}
.grid::before {{ content:''; position:absolute; inset:0; {slice9('panel_dark', 24)} }}
.cell {{ position:absolute; width:40px; height:40px; background:url({P}/cell.png) 0 0/40px 40px; }}
.item {{ position:absolute; }}
.item::before {{ content:''; position:absolute; inset:0; {slice9('item_bg', 12)} }}
.item .ic {{ position:absolute; left:2px; top:2px; }}
.item .lvl {{ position:absolute; right:3px; top:1px; font-size:11px; font-weight:700; color:#ffb347; }}
.item .cnt {{ position:absolute; left:3px; top:-1px; font-size:15px; font-weight:800; color:#f5e2b0; }}
.rar {{ position:absolute; inset:1px; border:1px solid; }}

.bar {{ position:relative; height:16px; padding:4px 6px; }}
.bar::before {{ content:''; position:absolute; inset:0; {slice9('bar_frame', (12, 18, 12, 18))} }}
.fill {{ position:relative; height:100%; background:url({P}/bar_fill.png) 0 0/100% 100%; background-blend-mode:multiply; }}
.bartext {{ position:absolute; inset:0; display:flex; align-items:center; justify-content:center; font-size:11px; font-weight:700; }}

.tip {{ position:absolute; z-index:20; padding:12px 16px; max-width:360px; }}
.tip::before {{ content:''; position:absolute; inset:0; {slice9('tooltip', 28)} }}
.tip > * {{ position:relative; }}
.toast {{ position:absolute; height:42px; padding:0 48px; display:flex; align-items:center; justify-content:center; }}
.toast::before {{ content:''; position:absolute; inset:0; {slice9('toast', (20, 72, 20, 72))} }}
.toast span {{ position:relative; }}
.ibtn {{ position:relative; width:46px; height:40px; margin:0 2px; display:flex; align-items:center; justify-content:center; font-size:26px; font-weight:700; color:#ffd58a; }}
.ibtn::before {{ content:''; position:absolute; inset:0; {slice9('ibtn', 24)} }}
.ibtn.ember::before {{ {slice9('ibtn_on', 24)} }} .ibtn.ember {{ color:#fff3e0; }}
.ibtn > * {{ position:relative; }}
.glyph {{ background-size:contain; background-repeat:no-repeat; background-position:center; }}
.mbtn {{ position:relative; width:42px; height:50px; margin:0 1px; display:flex; align-items:center; justify-content:center; }}
.mbtn::before {{ content:''; position:absolute; inset:0; {slice9('menu_btn', 24)} }}
.mbtn.on::before {{ {slice9('menu_btn_on', 24)} }}
.mbtn.hover::before {{ {slice9('menu_btn_hover', 24)} }}
.mbtn > * {{ position:relative; }}
.hud4bar {{ position:relative; display:flex; align-items:center; padding:18px 30px 6px 30px; }}
.hud4bar::before {{ content:''; position:absolute; inset:0; {slice9('hudbar', (72, 160, 24, 160))} }}
.hud4bar > * {{ position:relative; }}
.crystal {{ position:relative; width:80px; height:88px; margin:0 4px; }}
.socket {{ position:absolute !important; left:-14px; top:-16px; width:108px; height:124px; background:url({P}/socket.png) 0 0/100% 100%; }}
.vbar {{ position:relative; width:14px; height:70px; margin:-6px 7px 0 7px; background:rgba(5,5,5,.9); }}
.vbar .vf {{ position:absolute; left:2px; right:2px; bottom:2px; }}
.vframe {{ position:absolute; inset:-5px; }}
.vframe::before {{ content:''; position:absolute; inset:0; {slice9('vbar_frame', (20, 12, 20, 12))} }}
.vtext {{ position:absolute; bottom:-16px; left:-22px; right:-22px; text-align:center; font-size:11px; font-weight:700; }}
.wing {{ position:absolute !important; bottom:-2px; width:120px; height:110px; }}
.chatlog {{ position:relative; height:150px; padding:8px 12px; display:flex; flex-direction:column; justify-content:flex-end; font-size:13px; }}
.chatlog::before {{ content:''; position:absolute; inset:0; {slice9('chat_bg', 20)} }}
.chatlog div {{ position:relative; text-shadow:1px 1px 1px #000; }}
.goldbox {{ position:relative; height:42px; display:flex; align-items:center; }}
.goldbox::before {{ content:''; position:absolute; inset:0; {slice9('goldbox', 24)} }}
.goldbox > * {{ position:relative; }}
.row {{ display:flex; align-items:center; }}
.listrow {{ display:flex; align-items:center; padding:3px 4px; border-bottom:1px solid rgba(138,106,62,.18); }}
.check {{ width:18px; height:18px; background:url({P}/check_on.png) 0 0/100% 100%; margin-right:8px; }}
.check.offc {{ background-image:url({P}/check.png); }}
"""


def window(x, y, w, title, body, h=None):
    hh = f'height:{h}px;' if h else ''
    return (f'<div class="window" style="left:{x}px;top:{y}px;width:{w}px;{hh}">'
            f'<div class="whead"><div class="title">{title}</div><div class="close"></div></div>'
            f'<div class="crest"></div><div class="wbody">{body}</div><div class="crown"></div></div>')


def slot(x, y, w, h, inner='', cls='', key='', cnt=''):
    k = f'<div class="key out">{key}</div>' if key else ''
    c = f'<div class="cnt out">{cnt}</div>' if cnt else ''
    return f'<div class="slot {cls}" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px">{inner}{k}{c}</div>'


def btn(t, cls=''):
    return f'<div class="btn {cls}"><span>{t}</span></div>'


def bar(w, frac, color, text='', h=16):
    return (f'<div class="bar" style="width:{w}px;height:{h}px"><div class="fill" style="width:{frac*100:.0f}%;background-color:{color}"></div>'
            f'<div class="bartext out">{text}</div></div>')


def crystal_svg(color_deep, color_mid, color_surf, frac):
    # mesmo formato do HudCrystal (hexágono alongado) com líquido
    w, h = 68, 76
    cx = w / 2
    top, bot = 6, h - 6
    H = bot - top
    pts = [(cx, top), (w - 4, top + H * .26), (w - 4, top + H * .7), (cx, bot), (4, top + H * .7), (4, top + H * .26)]
    ps = ' '.join(f'{a:.1f},{b:.1f}' for a, b in pts)
    lvl = bot - H * frac
    return f'''<svg width="{w}" height="{h}" style="position:absolute;left:6px;top:6px">
<defs><clipPath id="c{color_mid[1:]}"><polygon points="{ps}"/></clipPath>
<linearGradient id="g{color_mid[1:]}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="{color_surf}"/><stop offset=".45" stop-color="{color_mid}"/><stop offset="1" stop-color="{color_deep}"/></linearGradient></defs>
<polygon points="{ps}" fill="#0a0606"/>
<g clip-path="url(#c{color_mid[1:]})"><rect x="0" y="{lvl:.1f}" width="{w}" height="{h}" fill="url(#g{color_mid[1:]})"/>
<rect x="0" y="{lvl:.1f}" width="{w}" height="3" fill="{color_surf}" opacity=".9"/>
<polygon points="{cx},{top} {w-4},{top+H*.26} {cx},{top+H*.42} 4,{top+H*.26}" fill="#fff" opacity=".13"/>
<polygon points="{cx},{top+H*.42} {cx},{bot} 4,{top+H*.7} 4,{top+H*.26}" fill="#000" opacity=".12"/></g>
<polygon points="{ps}" fill="none" stroke="#c9a058" stroke-width="1.6"/></svg>'''


def bag_items():
    """itens de exemplo na mochila 8x8: (id, coluna, linha, w, h, raridade, nível/contagem)."""
    return [
        ('item_22', 0, 0, 2, 4, '#ffd36a', '+9'), ('item_131', 2, 0, 2, 3, '#6dff9e', '+7'), ('item_301', 4, 0, 3, 2, '#ff8a3a', '+11'),
        ('item_2001', 7, 0, 1, 1, None, '35'), ('item_2002', 7, 1, 1, 1, None, '28'), ('item_133', 4, 2, 2, 2, None, '+3'),
        ('item_143', 6, 2, 2, 2, '#6dff9e', '+5'), ('item_600', 2, 3, 1, 1, None, ''), ('item_604', 3, 3, 1, 1, '#ffd36a', ''),
        ('item_1000', 0, 4, 1, 1, None, ''), ('item_1001', 1, 4, 1, 1, None, ''), ('item_1003', 2, 4, 1, 1, None, ''),
        ('item_44', 4, 4, 1, 4, None, '+2'), ('item_400', 5, 4, 2, 2, '#bfe3ff', '+4'), ('item_2000', 7, 4, 1, 1, None, '12'),
        ('item_501', 0, 5, 1, 1, None, ''), ('item_1010', 1, 5, 1, 1, None, '3'),
    ]


def inventory(x, y, sel_item=None):
    C, G = 40, 2
    C1, C2, C3 = C, C * 2 + G, C * 3 + G * 2
    layout = [('Mascote', 's_pet', 0, 0, C2, C2, 'item_401'), ('Amuleto', 's_amulet', 92, 42, C1, C1, 'item_603'),
              ('Elmo', 's_helm', 141, 0, C2, C2, 'item_130'), ('Asas', 's_wings', 236, 0, C3, C2, 'item_302'),
              ('Arma', 's_weapon', 0, 90, C2, C3, 'item_42'), ('Montaria', 's_mount', 92, 132, C1, C1, None),
              ('Armadura', 's_armor', 141, 90, C2, C3, 'item_141'), ('Escudo', 's_shield', 278, 90, C2, C3, 'item_46'),
              ('Luvas', 's_gloves', 0, 222, C2, C2, 'item_132'), ('Anel', 's_ring', 92, 243, C1, C1, 'item_601'),
              ('Calças', 's_pants', 141, 222, C2, C2, 'item_134'), ('Anel', 's_ring', 232, 243, C1, C1, None),
              ('Botas', 's_boots', 278, 222, C2, C2, None)]
    rar = {'item_302': '#ff8a3a', 'item_42': '#ffd36a', 'item_141': '#ffd36a', 'item_130': '#ffd36a'}
    eq = ''
    for name, guide, sx, sy, w, h, it in layout:
        if it:
            inner = icon(it, w - 12, h - 12)
            bc = rar.get(it, '#8a6a3e')
            eq += f'<div style="position:absolute;left:{sx}px;top:{sy}px;width:{w}px;height:{h}px;border:1px solid {bc};border-radius:4px">' \
                  f'{slot(0, 0, w - 2, h - 2, inner)}</div>'
        else:
            gd = f'<div class="guide" style="left:18%;top:18%;right:18%;bottom:18%;background-image:url({P}/../Art/g_{guide}.png)"></div>'
            nm = f'<div class="slotname">{name}</div>' if w > C1 else ''
            eq += slot(sx, sy, w, h, gd + nm)
    equip = f'<div class="rel" style="width:360px;height:304px;margin:0 auto 6px auto">{eq}<div class="t3" style="position:absolute;left:92px;right:92px;top:214px;text-align:center;font-size:10px;color:#8fe08f">Conjunto Áureo 3/5</div></div>'
    cells = ''.join(f'<div class="cell" style="left:{c*(C+G)}px;top:{r*(C+G)}px"></div>' for r in range(8) for c in range(8))
    items = ''
    for key, c, r, w, h, rc, lv in bag_items():
        W, H = w * C + (w - 1) * G, h * C + (h - 1) * G
        rr = f'<div class="rar" style="border-color:{rc}"></div>' if rc else ''
        sel = ' style="outline:2px solid #ffb347;outline-offset:-2px"' if key == sel_item else ''
        lab = (f'<div class="cnt out">{lv}</div>' if lv.isdigit() else f'<div class="lvl out">{lv}</div>') if lv else ''
        items += f'<div class="item" style="left:{c*(C+G)}px;top:{r*(C+G)}px;width:{W}px;height:{H}px"><div{sel} style="position:absolute;inset:0"></div>{rr}{icon(key, W - 4, H - 4)}{lab}</div>'
    gw = 8 * C + 7 * G
    grid = f'<div class="grid" style="width:{gw}px;height:{gw}px;margin:0 auto">{cells}{items}</div>'
    head = '<div class="row" style="width:340px;margin:4px auto 6px auto;justify-content:space-between"><span class="t2">Duplo clique equipa ou usa</span><span class="t3">23 casas livres</span></div>'
    actions = f'<div class="row" style="justify-content:center;margin-top:6px">{btn("Equipar", "small")}{btn("Usar", "small hover")}{btn("Aplicar runa", "small primary")}{btn("Vender", "small")}</div>'
    gold = f'<div class="goldbox" style="width:352px;margin:8px auto 0 auto"><div class="glyph" style="width:30px;height:30px;margin:0 8px 0 10px;background-image:url({P}/g_coin.png)"></div><b style="color:#ffd58a;font-size:16px" class="out">12.480.350</b></div>'
    foot = (f'<div class="row" style="width:352px;margin:6px auto 0 auto"><div class="ibtn"><div class="glyph" style="width:26px;height:26px;background-image:url({P}/g_close.png)"></div></div>'
            f'<div style="flex:1"></div><div class="ibtn"><div class="glyph" style="width:26px;height:26px;background-image:url({P}/g_options.png)"></div></div>'
            f'<div class="ibtn"><div class="glyph" style="width:26px;height:26px;background-image:url({P}/g_a_mag.png)"></div></div><div class="ibtn ember"><span>−</span></div></div>')
    return window(x, y, 384, 'Equipamento', equip + head + grid + actions + gold + foot)


def hud(selected_menu='bag'):
    quick = ''.join(f'<div class="rel" style="width:52px;height:52px;margin:0 2px">{slot(0, 0, 52, 52, icon(k, 40, 40), key=q, cnt=n)}</div>'
                    for q, k, n in (('Q', 'item_2001', '35'), ('W', 'item_2002', '28'), ('E', 'item_2000', '12'), ('R', 'item_1010', '3')))
    skills = ''.join(f'<div class="rel" style="width:52px;height:52px;margin:0 2px">{slot(0, 0, 52, 52, icon(k, 40, 40) + (cd or ""), key=str(i + 1))}</div>'
                     for i, (k, cd) in enumerate((('skill_1', None), ('skill_2', '<div style="position:absolute;left:5px;top:5px;right:5px;bottom:5px;background:conic-gradient(rgba(0,0,0,0) 0 62%, rgba(0,0,0,.62) 62% 100%)"></div><div style="left:0;right:0;top:15px;text-align:center;font-size:16px;font-weight:800" class="out">3.2</div>'), ('skill_3', None), ('skill_4', None), ('skill_5', None))))
    active = f'<div class="rel" style="width:60px;height:60px;margin:0 4px">{slot(0, 0, 60, 60, icon("skill_1", 48, 48), "sel")}<div class="glyph" style="position:absolute;right:-8px;top:-10px;width:22px;height:22px;background-image:url({P}/g_rmb.png)"></div></div>'
    menu = ''.join(f'<div class="mbtn {"on" if g == selected_menu else ""}"><div class="glyph" style="width:26px;height:26px;background-image:url({P}/g_{g}.png)"></div></div>'
                   for g in ('bag', 'char', 'guild', 'a_mag', 'options'))
    life = f'<div class="crystal"><div class="socket"></div>{crystal_svg("#4a0805", "#b01c10", "#ff7a4a", 0.78)}<div class="out" style="position:absolute;left:0;right:0;top:32px;text-align:center;font-weight:800;font-size:15px">1.842</div></div>'
    mana = f'<div class="crystal"><div class="socket"></div>{crystal_svg("#081650", "#1c48c0", "#7ab8ff", 0.55)}<div class="out" style="position:absolute;left:0;right:0;top:32px;text-align:center;font-weight:800;font-size:15px">964</div></div>'

    def vbar(frac, col, top, text):
        return (f'<div class="vbar"><div class="vf" style="height:{frac*66:.0f}px;background:linear-gradient({top} 0 3px,{col} 3px)"></div>'
                f'<div class="vframe"></div><div class="vtext out">{text}</div></div>')
    wings = (f'<div class="wing" style="left:-102px;background:url({P}/hud_wing_l.png) 0 0/100% 100%"></div>'
             f'<div class="wing" style="right:-102px;background:url({P}/hud_wing_r.png) 0 0/100% 100%"></div>')
    bar_html = f'<div class="hud4bar">{wings}<div class="hudcrest"></div><div class="row">{quick}</div>{life}{vbar(0.8, "#c9a23a", "#ffe08a", "64")}<div class="row" style="padding:0 3px">{skills}</div>{active}{vbar(0.45, "#3aa8c8", "#a8f0ff", "210")}{mana}<div class="row" style="margin-left:6px">{menu}</div></div>'
    segs = ''.join(f'<div class="bar" style="flex:1;height:12px;padding:2px 3px;margin-right:1px"><div class="fill" style="width:{100 if i < 13 else (40 if i == 13 else 0)}%;background-color:#d4a94a"></div></div>' for i in range(20))
    exp = f'<div class="row" style="width:1010px;height:12px;margin-bottom:2px;position:relative">{segs}<div class="out" style="position:absolute;right:-46px;font-size:10px;color:#e8c27a">65,4%</div></div>'
    return f'<div class="abs" style="left:0;right:0;bottom:0;height:150px;display:flex;flex-direction:column;align-items:center;justify-content:flex-end">{bar_html}{exp}</div>'


def frame_top():
    player = (f'<div class="panel" style="left:14px;top:14px;display:flex;align-items:center;padding:10px 18px 10px 12px">'
              f'<div class="rel" style="width:64px;height:64px;margin-right:12px;background:url({P}/portrait.png) 0 0/100% 100%;display:flex;align-items:center;justify-content:center">'
              f'<div class="glyph" style="width:40px;height:40px;background-image:url({P}/g_char.png)"></div>'
              f'<div class="out" style="position:absolute;left:8px;right:8px;bottom:-8px;height:20px;{slice9("lvl_badge", 12)}display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:800;color:#fff3e0;line-height:0">'
              f'<span style="margin-top:-8px">152</span></div></div>'
              f'<div><div class="title" style="font-size:17px;color:#f2e6cc">Heroi817</div><div class="gold" style="font-size:12px">Lâmina Arcana</div>'
              f'<div class="t2" style="font-size:12px;margin-top:2px"><span style="color:#8fe08f">Vale de Brasalume</span> · área segura</div>'
              f'<div class="row" style="margin-top:2px"><div class="glyph" style="width:16px;height:16px;margin-right:4px;background-image:url({P}/g_coin.png)"></div><span class="gold t2">12.480.350</span><b class="amber" style="font-size:12px;margin-left:10px">+45 pontos (C)</b></div></div></div>')
    buffs = ''.join(f'<div class="rel" style="width:34px;height:34px;margin-right:4px">{slot(0, 0, 34, 34, icon(k, 22, 22))}<div class="out" style="position:absolute;left:-4px;right:-4px;bottom:-14px;text-align:center;font-size:10px;font-weight:700">{t}</div></div>'
                    for k, t in (('skill_17', '2m'), ('skill_12', '48'), ('skill_20', '9.5')))
    buffs = f'<div class="abs row" style="left:18px;top:122px">{buffs}</div>'
    target = (f'<div class="panel" style="left:620px;top:12px;width:360px;padding:12px 22px 14px 22px;display:flex;flex-direction:column;align-items:center">'
              f'<div class="title" style="font-size:16px">Lobo das Cinzas</div><div class="t2" style="margin-bottom:4px">Nível 64 · Covil de Ferrugem</div>{bar(316, 0.62, "#d8341f", "6.230 / 10.000", 20)}</div>')
    toast = f'<div class="toast" style="left:560px;top:110px;width:480px"><span class="title" style="font-size:15px;color:#ffb347">Invasão de goblins em Vale de Brasalume!</span></div>'
    mm = (f'<div class="abs" style="right:14px;top:14px;width:196px;height:196px">'
          f'<div class="abs" style="left:14px;top:14px;width:168px;height:168px;border-radius:84px;overflow:hidden;background:radial-gradient(circle at 45% 40%,#4a5a34,#26301c 60%,#141a10);"></div>'
          f'<div class="abs" style="left:94px;top:94px;width:6px;height:6px;border-radius:3px;background:#ffe08a;box-shadow:0 0 4px #ffb347"></div>'
          f'<div class="abs" style="left:60px;top:70px;width:4px;height:4px;border-radius:2px;background:#ff5a4a"></div><div class="abs" style="left:120px;top:110px;width:4px;height:4px;border-radius:2px;background:#ff5a4a"></div>'
          f'<div class="abs" style="left:0;top:0;width:196px;height:196px;background:url({P}/minimap_frame.png) 0 0/100% 100%"></div>'
          f'<div class="title abs" style="top:198px;left:-30px;right:-30px;text-align:center;font-size:13px">Vale de Brasalume</div><div class="t3 abs" style="top:216px;left:0;right:0;text-align:center;font-size:10px">X 128 · Y 214</div></div>')
    return player + buffs + target + toast + mm


def chat():
    tabs = ''.join(f'<div class="tab {"on" if i == 0 else ""}"><span>{t}</span></div>' for i, t in enumerate(('Geral', 'Grupo', 'Guilda', 'Sistema')))
    lines = ['<span style="color:#8fd0ff">[Sistema] Conectado ao servidor Kezor.</span>', '<span class="amber">Ilsa, a Guardiã:</span> Seu baú está seguro comigo.',
             '<span style="color:#9fe39f">[Grupo] Brasa:</span> bora no Covil de Ferrugem?', '<span style="color:#e8c27a">[Guilda] Aurora:</span> guerra às 21h, todos online!',
             '<b>Heroi817:</b> vou só reparar o equipamento', '<span style="color:#ffd36a">Você recebeu Gema da Aurora.</span>']
    log = ''.join(f'<div>{l}</div>' for l in lines)
    return (f'<div class="abs" style="left:14px;bottom:158px;width:420px"><div class="row" style="padding-left:8px">{tabs}</div>'
            f'<div class="chatlog">{log}</div><div class="field" style="margin-top:3px"><span>Enter para conversar · /comando</span></div></div>')


def character(x, y):
    rows = ''
    for lab, v in (('Força', '842'), ('Agilidade', '1.204'), ('Vitalidade', '615'), ('Energia', '310')):
        rows += f'<div class="row" style="margin:3px 0"><span style="width:92px">{lab}</span><b class="amber" style="width:54px;font-size:15px">{v}</b>{btn("+1", "small")}{btn("+10", "small")}{btn("+100", "small")}</div>'
    skills = ''.join(f'<div class="row" style="align-items:flex-start;margin:2px 0"><div class="rel" style="width:40px;height:40px;margin-right:8px;flex-shrink:0">{slot(0, 0, 40, 40, icon(k, 28, 28))}</div>'
                     f'<div><div><b>{n}</b> <span style="color:#8fa8ff">{mp} MP</span> <span class="t3">nível {lv}</span></div><div class="t2">{d}</div></div></div>'
                     for k, n, mp, lv, d in (('skill_1', 'Corte Rúnico', 8, 1, 'Golpe rápido que marca o alvo com uma runa.'),
                                             ('skill_2', 'Lâmina Fantasma', 22, 30, 'Atravessa o alvo e acerta quem estiver atrás.'),
                                             ('skill_3', 'Tempestade de Aço', 40, 80, 'Gira as lâminas e fere tudo em volta.')))
    body = (f'<div class="h2">Heroi817</div><div class="t2">Lâmina Arcana · Nível 152 · Renascimentos 1</div><div class="divider"></div>'
            f'<div>Pontos livres: <b class="amber">45</b></div>{rows}<div class="divider"></div>'
            f'<div style="line-height:1.5">Vida <b>1.842/2.360</b> &nbsp;&nbsp; Mana <b>964/1.750</b><br>Dano físico <b>412~588</b> &nbsp;&nbsp; Defesa <b>366</b><br>Taxa de ataque <b>2.140</b> &nbsp;&nbsp; Taxa de defesa <b>701</b></div>'
            f'<div class="divider"></div><div>Skills <span class="t3">(1–6 escolhem · botão direito usa)</span></div>{skills}'
            f'<div class="row" style="margin-top:6px">{btn("Renascer (nível 400, 10.000.000 ouro)", "off")}</div>')
    return window(x, y, 360, 'Personagem', body)


def item_tip(x, y):
    return (f'<div class="tip" style="left:{x}px;top:{y}px;width:300px">'
            f'<div class="title" style="font-size:16px;color:#ffd36a">Arco Rúnico do Ocaso +9</div><div class="t2">Arco · duas mãos · Lâmina Arcana, Patrulheiro</div>'
            f'<div class="divider" style="margin:4px 0"></div><div>Dano <b>118 ~ 164</b> &nbsp; Velocidade <b>25</b></div><div>Durabilidade <b>62/70</b></div>'
            f'<div style="color:#bfe3ff;margin-top:4px">Runa da Aurora: +12% de dano crítico</div><div style="color:#6dff9e">Ascendente: +8% de taxa de ataque</div>'
            f'<div class="divider" style="margin:4px 0"></div><div style="color:#8fe08f">▲ +38 de dano em relação ao equipado</div><div class="t3" style="margin-top:4px">Venda: 184.000 ouro</div></div>')


def shop(x, y):
    rows = ''
    for k, n, p, pot in (('item_2001', 'Poção de Vida Grande', '1.200', True), ('item_2002', 'Poção de Mana Grande', '1.400', True), ('item_2000', 'Antídoto', '300', True),
                         ('item_101', 'Armadura de Couro', '500', False), ('item_20', 'Arco de Caça', '800', False), ('item_10', 'Espada Curta', '650', False),
                         ('item_45', 'Escudo Redondo', '720', False), ('item_1000', 'Pergaminho de Retorno', '2.000', False)):
        b = btn('×1', 'small') + (btn('×10', 'small') if pot else '')
        rows += f'<div class="listrow"><div class="rel" style="width:40px;height:40px;flex-shrink:0">{slot(0, 0, 40, 40, icon(k, 28, 28))}</div><div style="flex:1;margin:0 8px">{n}<br><span class="gold">{p} ouro</span></div>{b}</div>'
    body = f'<div class="t2" style="margin-bottom:4px">Compre com ouro. Para vender, selecione um item na mochila.</div><div style="height:430px;overflow:hidden;position:relative">{rows}</div><div class="divider"></div><div class="row" style="justify-content:center">{btn("Vender Arco Rúnico do Ocaso +9 por 184.000", "primary")}</div>'
    return window(x, y, 400, 'Mercadora Tália — Loja', body)


def vault(x, y):
    C, G = 40, 2
    cells = ''.join(f'<div class="cell" style="left:{c*(C+G)}px;top:{r*(C+G)}px"></div>' for r in range(7) for c in range(8))
    its = [('item_21', 0, 0, 2, 4, '#ffd36a', '+7'), ('item_151', 2, 0, 2, 3, None, '+4'), ('item_300', 4, 0, 3, 2, None, '+2'), ('item_605', 7, 0, 1, 1, '#a77bff', ''),
           ('item_152', 4, 2, 2, 2, None, '+4'), ('item_1002', 6, 2, 1, 1, None, '9'), ('item_602', 7, 2, 1, 1, None, ''), ('item_500', 2, 3, 1, 1, None, ''),
           ('item_154', 4, 4, 2, 2, None, '+4'), ('item_53', 6, 3, 1, 4, '#6dff9e', '+6')]
    items = ''
    for key, c, r, w, h, rc, lv in its:
        W, H = w * C + (w - 1) * G, h * C + (h - 1) * G
        rr = f'<div class="rar" style="border-color:{rc}"></div>' if rc else ''
        lab = (f'<div class="cnt out">{lv}</div>' if lv.isdigit() else f'<div class="lvl out">{lv}</div>') if lv else ''
        items += f'<div class="item" style="left:{c*(C+G)}px;top:{r*(C+G)}px;width:{W}px;height:{H}px">{rr}{icon(key, W - 4, H - 4)}{lab}</div>'
    gw, gh = 8 * C + 7 * G, 7 * C + 6 * G
    grid = f'<div class="grid" style="width:{gw}px;height:{gh}px;margin:8px auto">{cells}{items}</div>'
    body = (f'<div class="t2">Arraste itens entre o baú e a mochila (ou duplo clique). O baú é o mesmo em qualquer cidade.</div>{grid}'
            f'<div class="gold">Ouro no baú: <b>3.250.000</b> &nbsp;·&nbsp; na mochila: 12.480.350</div>'
            f'<div class="row" style="margin-top:4px"><div class="field" style="width:130px"><span>quantia</span></div>{btn("Guardar ouro")}{btn("Retirar ouro", "hover")}</div>')
    return window(x, y, 400, 'Ilsa, a Guardiã — Baú', body)


def options(x, y):
    body = (f'<div class="h2">Interface</div><div>Escala da interface: <b>100%</b></div>'
            f'<div style="position:relative;height:20px"><div style="position:absolute;left:0;right:0;top:7px;height:6px;border-radius:3px;background:#070505;border:1px solid #8a6a3e"></div>'
            f'<div style="position:absolute;left:44%;top:1px;width:18px;height:18px;background:url({P}/check_on.png) 0 0/100% 100%"></div></div>'
            f'<div class="row" style="margin:4px 0"><div class="check"></div>Mostrar minimapa</div><div class="row" style="margin:4px 0"><div class="check"></div>Interface clássica (molduras de ouro)</div>'
            f'<div class="divider"></div><div class="h2">Gráficos</div><div class="row">{btn("Baixa", "small")}{btn("Média", "small")}{btn("Alta", "small on")}{btn("Ultra", "small")}</div>'
            f'<div class="row" style="margin:4px 0"><div class="check offc"></div>Sincronização vertical (VSync)</div><div class="row" style="margin:4px 0"><div class="check"></div>Tela cheia</div>')
    return window(x, y, 440, 'Opções', body)


def page(bg, content):
    return f'<!doctype html><html><head><meta charset="utf-8"><style>{CSS}</style></head><body><div class="bg" style="background-image:url(file://{bg})"></div>{content}</body></html>'


def shoot(html_path, out_jpg, scale=2):
    js = f"""
const {{ chromium }} = require('playwright');
(async () => {{
  const b = await chromium.launch();
  const p = await b.newPage({{ viewport: {{ width: 1600, height: 900 }}, deviceScaleFactor: {scale} }});
  await p.goto('file://{html_path}');
  await p.waitForTimeout(400);
  await p.screenshot({{ path: '{out_jpg}', type: 'jpeg', quality: 90 }});
  await b.close();
}})();
"""
    subprocess.run(['node', '-e', js], check=True)


def main():
    bg = sys.argv[1] if len(sys.argv) > 1 else '/mnt/project-files/kezor/capturas/cidade_v4_praca.png'
    bg2 = sys.argv[2] if len(sys.argv) > 2 else '/mnt/project-files/kezor/capturas/cidade_v4_mercado_noite.png'
    tmp = os.path.join(OUT, '_mock')
    os.makedirs(tmp, exist_ok=True)
    scenes = {
        'ui_premium_hud': page(bg, frame_top() + chat() + hud('bag') + character(744, 150) + inventory(1212, 46, 'item_22') + item_tip(890, 380)),
        'ui_premium_barra': page(bg, frame_top() + chat() + hud('char')),
        'ui_premium_lojas': page(bg2, frame_top() + chat() + hud('bag') + shop(20, 236) + vault(430, 150) + inventory(1212, 46) + options(842, 560 - 300)),
    }
    for name, html in scenes.items():
        hp = os.path.join(tmp, name + '.html')
        open(hp, 'w').write(html)
        shoot(hp, os.path.join(OUT, name + '.jpg'))
        print(os.path.join(OUT, name + '.jpg'))


if __name__ == '__main__':
    main()
