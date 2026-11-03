"""Conversion reproducible de un avatar de AVATARS2 (GLB de una sola malla) a FBX estatico para Unity.

Uso: blender -b --python convert_avatar.py -- <glb> <fbx_salida> <id> [opciones JSON]
Opciones (JSON): {"height":1.7, "target_tris":45000, "pose_deg":0, "pivot":[0.16,0,1.27], "x0":0.13, "bw":0.08, "zlow":1.17, "zfade":0.04,
                  "head_z":1.43, "probe":false, "out_dir_render":null}
Pasos: importar GLB -> hornear transformaciones -> escalar a 'height' con los pies en z=0 y el cuerpo centrado en X/Y ->
       soldar vertices por posicion (las UV/normales viven en los bucles, no se pierden) -> decimar (colapso) protegiendo la cabeza ->
       transferir las normales personalizadas de la malla original -> (opcional) pose A -> exportar FBX sin armadura.
No usa las texturas de AVATARS2/*/textures (estan volteadas): las texturas salen del propio GLB con extract_textures_single.py.
"""
import json
import math
import sys

import bmesh
import bpy
import mathutils

argv = sys.argv[sys.argv.index('--') + 1:]
glb, out_fbx, pid = argv[0], argv[1], argv[2]
opt = {'height': 1.7, 'target_tris': 45000, 'pose_deg': 0.0, 'pivot': [0.16, 0.0, 1.27], 'x0': 0.13, 'bw': 0.08, 'zlow': 1.17, 'zfade': 0.04, 'zhi': 1.30, 'zhifade': 0.04,
       'head_z': 1.43, 'w_head': 1.0, 'w_body': 0.25, 'weld': True, 'seam_protect': False, 'w_face': None, 'face_y': 0.0, 'probe': False}
if len(argv) > 3:
    opt.update(json.loads(argv[3]))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 1, 'se esperaba una sola malla, hay %d' % len(meshes)
src = meshes[0]
for o in bpy.context.scene.objects:
    if o.type != 'MESH':
        print('NO-MESH', o.name, o.type)

# 1) hornear la transformacion del nodo (p. ej. la traslacion de Tom) en la malla
mw = src.matrix_world.copy()
src.parent = None
src.data.transform(mw)
src.matrix_world = mathutils.Matrix.Identity(4)
me = src.data
tris_before = sum(len(p.vertices) - 2 for p in me.polygons)

# 2) escala y centrado: altura = height, pies en z=0, X/Y centrados en el cuerpo (banda 0.2..1.0 de la altura relativa)
zs = [v.co.z for v in me.vertices]
zmin, zmax = min(zs), max(zs)
f = opt['height'] / (zmax - zmin)
band = [v.co for v in me.vertices if zmin + 0.12 * (zmax - zmin) <= v.co.z <= zmin + 0.55 * (zmax - zmin)]
cx = sum(p.x for p in band) / len(band)
cy = sum(p.y for p in band) / len(band)
me.transform(mathutils.Matrix.Scale(f, 4) @ mathutils.Matrix.Translation((-cx, -cy, -zmin)))
print('ESCALA factor', round(f, 4), 'centro cuerpo', round(cx, 3), round(cy, 3), 'tris originales', tris_before)


def bbox(m):
    xs = [v.co.x for v in m.vertices]; ys = [v.co.y for v in m.vertices]; zz = [v.co.z for v in m.vertices]
    return (min(xs), min(ys), min(zz)), (max(xs), max(ys), max(zz))


if opt['probe']:
    mn, mx = bbox(me)
    print('PROBE bbox', [round(c, 3) for c in mn], [round(c, 3) for c in mx])
    arm = [v.co for v in me.vertices if abs(v.co.x) > 0.45]
    if arm:
        zz = sorted(p.z for p in arm)
        print('PROBE brazo |x|>0.45: z mediana %.3f rango [%.3f,%.3f]  y mediana %.3f' % (zz[len(zz) // 2], zz[0], zz[-1], sorted(p.y for p in arm)[len(arm) // 2]))
    print('PROBE semianchura (max|x|) por banda z:')
    for z0 in [x / 100 for x in range(90, 160, 4)]:
        b = [v.co for v in me.vertices if z0 <= v.co.z < z0 + 0.04 and abs(v.co.x) < 0.45]
        if b:
            print('PROBE   z%.2f  max|x|=%.3f  y[%.3f,%.3f]  n=%d' % (z0, max(abs(p.x) for p in b), min(p.y for p in b), max(p.y for p in b), len(b)))
    raise SystemExit

# 3) copia de la malla original (alta resolucion) para transferir normales
hi = src.copy(); hi.data = src.data.copy(); hi.name = 'HI'
bpy.context.scene.collection.objects.link(hi)

# 4) soldar por posicion y decimar protegiendo la cabeza
bm = bmesh.new(); bm.from_mesh(me)
nv0 = len(bm.verts)
if opt['weld']:
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
print('SOLDADURA vertices', nv0, '->', len(bm.verts))
seam = set()
if opt['seam_protect']:
    uvl = bm.loops.layers.uv.active
    for v in bm.verts:
        uv0 = None
        for l in v.link_loops:
            u = l[uvl].uv
            if uv0 is None:
                uv0 = u.copy()
            elif (u - uv0).length > 1e-5:
                seam.add(v.index); break
    print('COSTURAS UV: vertices de costura', len(seam), 'de', len(bm.verts))
bm.verts.ensure_lookup_table()
seam_idx = set(seam)
bm.to_mesh(me); bm.free()
vg = src.vertex_groups.new(name='keep')
for v in me.vertices:
    if v.index in seam_idx:
        w = 0.001
    elif v.co.z >= opt['head_z']:
        w = opt['w_face'] if (opt['w_face'] is not None and v.co.y < opt['face_y']) else opt['w_head']   # mitad frontal de la cabeza = rostro
    else:
        w = opt['w_body']
    vg.add([v.index], w, 'REPLACE')
bpy.context.view_layer.objects.active = src
mod = src.modifiers.new('dec', 'DECIMATE')
mod.decimate_type = 'COLLAPSE'
mod.ratio = min(1.0, opt['target_tris'] / float(tris_before))
mod.use_collapse_triangulate = True
mod.vertex_group = 'keep'
mod.vertex_group_factor = 1.0
bpy.ops.object.modifier_apply(modifier='dec')
src.vertex_groups.clear()
me = src.data
tris_after = sum(len(p.vertices) - 2 for p in me.polygons)
print('DECIMADO ratio %.4f tris %d -> %d' % (mod.ratio if False else opt['target_tris'] / float(tris_before), tris_before, tris_after))

# 5) normales personalizadas transferidas desde la malla original
dt = src.modifiers.new('nrm', 'DATA_TRANSFER')
dt.object = hi
dt.use_loop_data = True
dt.data_types_loops = {'CUSTOM_NORMAL'}
dt.loop_mapping = 'POLYINTERP_NEAREST'
bpy.ops.object.modifier_apply(modifier='nrm')
me = src.data
print('NORMALES personalizadas:', me.has_custom_normals)
bpy.data.objects.remove(hi)

# 6) pose A (rotacion rigida de cada brazo alrededor del hombro, mezcla suave; sin armadura)
if opt['pose_deg']:
    THETA = math.radians(opt['pose_deg']); PIV = mathutils.Vector(opt['pivot'])

    def smooth(t):
        t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)

    def weight(p):
        return smooth((abs(p.x) - opt['x0']) / opt['bw']) * smooth((p.z - opt['zlow']) / opt['zfade']) * (1.0 - smooth((p.z - opt['zhi']) / opt['zhifade']))

    def rotp(p, w):
        side = 1.0 if p.x >= 0 else -1.0
        a = side * THETA * w
        pv = mathutils.Vector((side * PIV.x, PIV.y, PIV.z))
        q = p - pv; c, s = math.cos(a), math.sin(a)
        return mathutils.Vector((q.x * c + q.z * s, q.y, -q.x * s + q.z * c)) + pv

    def rotn(n, w, x):
        a = (1.0 if x >= 0 else -1.0) * THETA * w; c, s = math.cos(a), math.sin(a)
        return mathutils.Vector((n.x * c + n.z * s, n.y, -n.x * s + n.z * c))

    custom = me.has_custom_normals
    ws = [weight(v.co) for v in me.vertices]
    if custom:
        cn = [mathutils.Vector(n.vector) for n in me.corner_normals]
        lv = [l.vertex_index for l in me.loops]
        newn = [rotn(cn[i], ws[lv[i]], me.vertices[lv[i]].co.x).normalized() for i in range(len(cn))]
    for v, w in zip(me.vertices, ws):
        if w > 0:
            v.co = rotp(v.co, w)
    me.update()
    if custom:
        me.normals_split_custom_set([tuple(n) for n in newn])
    print('POSE A', opt['pose_deg'], 'grados; vertices movidos', sum(1 for w in ws if w > 0))

# 7) renombrar, material, raiz y exportar
mn, mx = bbox(me)
print('FINAL bbox min', [round(c, 3) for c in mn], 'max', [round(c, 3) for c in mx], 'tamano', [round(mx[i] - mn[i], 3) for i in range(3)])
tris_final = sum(len(p.vertices) - 2 for p in me.polygons)
print('TRIS', tris_before, '->', tris_final)
uvs = me.uv_layers[0].data
print('UV rango u[%.3f,%.3f] v[%.3f,%.3f]' % (min(u.uv[0] for u in uvs), max(u.uv[0] for u in uvs), min(u.uv[1] for u in uvs), max(u.uv[1] for u in uvs)))
src.name = pid + '_body'; me.name = pid + '_body'
if me.materials:
    me.materials[0].name = pid + '_body'
root = bpy.data.objects.new(pid, None); bpy.context.scene.collection.objects.link(root)
src.parent = root
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'MESH', 'EMPTY'}, path_mode='STRIP', embed_textures=False,
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, axis_forward='-Z', axis_up='Y',
                         mesh_smooth_type='FACE', use_tspace=True, add_leaf_bones=False, bake_anim=False)
print('EXPORTED', out_fbx)
