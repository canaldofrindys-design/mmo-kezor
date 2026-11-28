# Kezor — reexporta um set já montado (objetos KZS_* na cena) depois de mexer nas peças.
# Uso: R = {}; exec(open(D + 'kezor_reexport.py', encoding='utf-8').read(), R)
#      R['reexport'](E, K, 'AV_', 'ArcanistaDoVeuBase', 'Sabio')
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree

PIECES = ('Elmo', 'Armadura', 'LuvaL', 'LuvaR', 'CalcaL', 'CalcaR', 'BotaL', 'BotaR')


def built_from_scene():
    out = {}
    for piece in PIECES:
        ms = sorted([o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith('KZS_%s_' % piece)
                     and o.name[len('KZS_%s_' % piece):].isdigit()], key=lambda o: o.name)
        if ms:
            out[piece] = (ms, [o.data.materials[0] for o in ms], piece.rstrip('LR'))
    return out


def uv_labels(src, label_of_comp):
    """KD-tree dos centros de UV das faces da malha de origem -> rótulo do componente conectado."""
    bm = bmesh.new(); bm.from_mesh(src.data); bm.faces.ensure_lookup_table()
    uvl = bm.loops.layers.uv.active
    seen, comp = set(), {}
    cid = 0
    comps = []
    for f in bm.faces:
        if f.index in seen:
            continue
        st, grp = [f], []
        seen.add(f.index)
        while st:
            g = st.pop(); grp.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index); st.append(h)
        comps.append(grp)
    kd = KDTree(len(bm.faces))
    lab = {}
    for ci, grp in enumerate(comps):
        l = label_of_comp(ci, grp, bm, uvl)
        for g in grp:
            u = sum((lp[uvl].uv for lp in g.loops), Vector((0, 0))) / len(g.loops)
            kd.insert((u.x, u.y, 0.0), g.index)
            lab[g.index] = l
    kd.balance()
    bm.free()
    return kd, lab


def regroup(objs, face_piece):
    """Junta as faces de objs (mesmo material) e redistribui: face_piece(obj, poly) -> nome da peça."""
    res = {}
    for o in objs:
        for p in o.data.polygons:
            res.setdefault(face_piece(o, p), {}).setdefault(o.name, []).append(p.index)
    new = {}
    for piece, by in res.items():
        parts = []
        for oname, idx in by.items():
            o = bpy.data.objects[oname]
            c = o.copy(); c.data = o.data.copy()
            for col in o.users_collection:
                col.objects.link(c)
            keep = set(idx)
            bm = bmesh.new(); bm.from_mesh(c.data); bm.faces.ensure_lookup_table()
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context='FACES')
            bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
            bm.to_mesh(c.data); bm.free()
            parts.append(c)
        if len(parts) > 1:
            for x in bpy.context.view_layer.objects:
                x.select_set(False)
            for c in parts:
                c.hide_set(False); c.select_set(True)
            bpy.context.view_layer.objects.active = parts[0]
            bpy.ops.object.join()
        new[piece] = parts[0]
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    for piece, o in new.items():
        old = bpy.data.objects.get('KZS_%s_0' % piece)
        if old:
            bpy.data.objects.remove(old, do_unlink=True)
        o.name = 'KZS_%s_0' % piece
    return new


def reexport(E, K, pre, model, set_name):
    E['setup'](pre, model, set_name)
    G = E
    built = built_from_scene()
    K['ALLOWED'].update({'Armadura': ('Body', 'Chest', 'LegL', 'LegR'),
                         'CalcaL': ('LegL', 'ShinL', 'FootL', 'Body'), 'CalcaR': ('LegR', 'ShinR', 'FootR', 'Body')})
    K['WEIGHT_MIX'].update({'Armadura': E['skirt_mix'], 'CalcaL': E['pants_mix']('L'), 'CalcaR': E['pants_mix']('R')})
    K['HIDE_ZMIN'].update({'Armadura': 0.5})
    WB, BODY, HEAD, SET, MODEL = G['WB'], G['BODY'], G['HEAD'], G['SET'], G['MODEL']
    ex = K['export_parts'](SET, built, bpy.data.objects[WB], E['pivots_auto'](), textures=True)
    hide = K['write_hide'](SET, MODEL, built, [bpy.data.objects[BODY], bpy.data.objects[HEAD]])
    if 'Armadura' in built:
        hide['Armadura'] = E['merge_hide'](K, 'Armadura', E['enclosed_hide'](K, built, 'Armadura', 0.55, 1.42))
    for piece, (ms, _, _) in built.items():
        if piece == 'Elmo':
            E['merge'](SET, piece, MODEL, E['poke_points']([bpy.data.objects[BODY], bpy.data.objects[HEAD]], ms, 0.08, zmin=1.3))
        else:
            E['merge'](SET, piece, MODEL, E['poke_points']([bpy.data.objects[BODY]], ms, 0.10))
        body = [bpy.data.objects[BODY], bpy.data.objects[HEAD]] if piece == 'Elmo' else [bpy.data.objects[BODY]]
        E['merge'](SET, piece, MODEL, E['near_points'](body, ms))
    E['clean_hide'](SET, MODEL)
    E['fill_gaps'](SET, MODEL)
    E['clean_hide'](SET, MODEL)
    return ex, hide
