# Kezor — passe extra nas listas de pele escondida de um set já exportado.
# Pele que fura o pano (o vértice fica do lado de fora, mas logo para dentro dele há pano da peça) também some.
# Uso (Blender, na cena do set com as peças KZS_* ainda lá):
#   P = {}; exec(open(D + 'kezor_hide_poke.py').read(), P)
#   P['poke_set']('Trama', 'LaminaArcanaBase', 'LB_', {'Armadura': ['KZS_...'], ...})
import bpy, bmesh, os
from mathutils.bvhtree import BVHTree

ROOT = r'C:\MuProtoUnity\Assets\Resources'


def world_mesh_bm(o):
    bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world); bm.normal_update()
    return bm


def poke_points(body_objs, piece_objs, reach=0.06, zmin=0.05):
    tb = bmesh.new()
    for x in piece_objs:
        t = world_mesh_bm(x)
        me = bpy.data.meshes.new('kz_tmp'); t.to_mesh(me); t.free(); tb.from_mesh(me); bpy.data.meshes.remove(me)
    tree = BVHTree.FromBMesh(tb); tb.free()
    pts = []
    for b in body_objs:
        bm = world_mesh_bm(b)
        for v in bm.verts:
            if v.co.z < zmin:
                continue
            if tree.ray_cast(v.co - v.normal * 0.0005, -v.normal, reach)[0] is not None:
                pts.append(tuple(int(round(c * 1000)) for c in v.co))
        bm.free()
    return pts


def merge(set_name, piece, model, pts):
    path = os.path.join(ROOT, 'Equip', 'Hide', '%s_%s_%s.txt' % (set_name, piece, model))
    old = set()
    if os.path.exists(path):
        old = set(tuple(int(x) for x in l.split()) for l in open(path, encoding='utf-8').read().split('\n') if l.strip())
    allp = sorted(old | set(pts))
    with open(path, 'w', encoding='utf-8') as f:
        f.write('\n'.join('%d %d %d' % p for p in allp))
    return len(old), len(allp)


def poke_set(set_name, model, pre, pieces, reach=0.06, skip=('Elmo',)):
    body = [bpy.data.objects[pre + 'Body']]
    out = {}
    for piece, names in pieces.items():
        if piece in skip:
            continue
        out[piece] = merge(set_name, piece, model, poke_points(body, [bpy.data.objects[n] for n in names], reach))
    return out
