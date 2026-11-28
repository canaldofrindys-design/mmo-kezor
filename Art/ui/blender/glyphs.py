"""
Kezor — ícones dos botões (g_*.png) em relevo de ouro, renderizados no Blender (Cycles).

O desenho de cada ícone vem das funções _g_* de Art/ui/kezor_ui_premium.py (máscara 2D). Aqui a máscara vira
uma chapa de metal em relevo de verdade: malha deslocada pela altura (máscara desfocada = bordas arredondadas),
ouro com a mesma luz do resto do kit e um contorno escuro por baixo para ler em qualquer fundo.

    python Art/ui/blender/glyphs.py            # todos
    python Art/ui/blender/glyphs.py g_bag      # alguns
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(__file__)
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..'))
import bpy  # noqa: E402
import bmesh  # noqa: E402
from kzb import *  # noqa: E402,F401,F403
import kezor_ui_premium as k2  # noqa: E402
from kzpaint import Ink, blur  # noqa: E402

OUT = k2.OUT
SAMPLES = int(os.environ.get('KZ_SAMPLES', '128'))
TMP = os.environ.get('KZ_TMP', '/tmp/kz_glyph_masks')
S = 128          # tamanho do desenho original (coordenadas das funções _g_*)
UI = 64          # tamanho na tela (textura 128 em 2x)
MS = 4           # escala da máscara: 512x512


def mask_of(fn):
    ink = Ink(S * MS, S * MS, 3)
    fn(ink, MS, S)
    return ink.mask()


def save_l(a, path):
    Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), 'L').save(path)


def image_node(nt, path):
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = 'Non-Color'
    t = nt.nodes.new('ShaderNodeTexImage')
    t.image = im
    t.interpolation = 'Cubic'
    t.extension = 'CLIP'
    return t


def alpha_wrap(mat, path):
    """mistura o shader do material com transparente usando a máscara (UV do plano)."""
    nt = mat.node_tree
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    surf = out.inputs['Surface'].links[0].from_socket
    tex = image_node(nt, path)
    uv = nt.nodes.new('ShaderNodeTexCoord')
    nt.links.new(uv.outputs['UV'], tex.inputs['Vector'])
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(tex.outputs['Color'], mix.inputs['Fac'])
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(surf, mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs['Surface'])
    return mat


def height_grid(h, size, n, mat, z0=0.0, amp=1.0, name='relief'):
    """grade n x n cobrindo [-size/2, size/2]², z = altura amostrada de h (array quadrado, linha 0 = topo)."""
    hs = h.shape[0]
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new()
    vs = []
    for j in range(n + 1):
        row = []
        for i in range(n + 1):
            u, v = i / n, j / n
            px = min(hs - 1, int(u * (hs - 1)))
            py = min(hs - 1, int((1 - v) * (hs - 1)))
            row.append(bm.verts.new(((u - 0.5) * size, (v - 0.5) * size, z0 + h[py, px] * amp)))
        vs.append(row)
    for j in range(n):
        for i in range(n):
            f = bm.faces.new((vs[j][i], vs[j][i + 1], vs[j + 1][i + 1], vs[j + 1][i]))
            for lp, (a, b) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                lp[uvl].uv = (a / n, b / n)
    return obj_from_bmesh(bm, name, mat, smooth=True)


def glyph(name, fn):
    reset(SAMPLES)
    os.makedirs(TMP, exist_ok=True)
    m = mask_of(fn)                                    # 512², 1 = desenho
    px = m.shape[0] / UI                               # pixels de máscara por unidade da cena (8)
    # altura: platô com bordas arredondadas + leve abaulado no meio das formas largas
    h = 0.65 * np.clip(blur(m, 1.1 * px) * 1.35, 0, 1) ** 0.8 + 0.35 * blur(m, 3.0 * px)
    # filete gravado acompanhando o contorno por dentro (dá o acabamento de peça cinzelada)
    e = blur(m, 2.2 * px)
    groove = np.exp(-((e - 0.80) / 0.07) ** 2) * (blur(m, 4.5 * px) > 0.45)
    h = h * m ** 0.25 - 0.22 * groove
    cut = np.clip((m - 0.35) / 0.3, 0, 1)              # recorte nítido do metal
    edge = np.clip(blur(m, 1.6 * px) * 4.0, 0, 1)      # contorno escuro por baixo
    pm, pe = os.path.join(TMP, name + '_m.png'), os.path.join(TMP, name + '_e.png')
    save_l(cut, pm)
    save_l(edge, pe)
    gm = gold_pale() if name == 'g_close' else gold()
    height_grid(h, UI, 320, alpha_wrap(gm, pm), 0.6, 6.0, 'relief')
    dark = alpha_wrap(flat((0.012, 0.007, 0.005), 'outline', 0.7), pe)
    floor_plane(-UI / 2, -UI / 2, UI / 2, UI / 2, dark, 0.0, 'outline')
    # UV do plano de contorno
    o = bpy.data.objects['outline']
    uvl = o.data.uv_layers.new()
    for poly in o.data.polygons:
        for li in poly.loop_indices:
            co = o.data.vertices[o.data.loops[li].vertex_index].co
            uvl.data[li].uv = (co.x / UI + 0.5, co.y / UI + 0.5)
    camera(-UI / 2, -UI / 2, UI / 2, UI / 2)
    render(os.path.join(OUT, name + '.png'))


if __name__ == '__main__':
    names = sys.argv[1:] or list(k2.GLYPHS)
    for n in names:
        print('->', n, flush=True)
        glyph(n, k2.GLYPHS[n])
