"""Renderiza el rostro con ENCUADRE IDENTICO (ortografico, centro fijo) para el GLB original (normalizado igual que el conversor) y para FBX convertidos.
Uso: blender -b --python face_compare.py -- <glb> <salida_dir> <id> <fbx1> [<fbx2> ...]
Salida: <dir>/face_<etiqueta>.png con etiqueta 'orig' y el nombre de cada FBX. Vista frontal (y 3/4) con textura plana (workbench)."""
import math
import os
import sys

import bpy
import mathutils

a = sys.argv[sys.argv.index('--') + 1:]
glb, outdir, pid, fbxs = a[0], a[1], a[2], a[3:]
os.makedirs(outdir, exist_ok=True)


def setup():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'FLAT'; sh.color_type = 'TEXTURE'
    sc.render.resolution_x = 800; sc.render.resolution_y = 800
    sc.world = bpy.data.worlds.new('w'); sc.world.color = (0.5, 0.5, 0.5)
    cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = float(os.environ.get('FACE_SCALE', '0.34'))
    return sc, cam


def shoot(sc, cam, path, yaw):
    c = mathutils.Vector((0.0, 0.0, float(os.environ.get('FACE_Z', '1.56'))))
    cam.location = c + mathutils.Vector((math.sin(yaw), -math.cos(yaw), 0)) * 10
    cam.rotation_euler = (math.pi / 2, 0, yaw)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def texturize(tdir):
    for m in bpy.data.materials:
        m.use_nodes = True
        for n in list(m.node_tree.nodes):
            m.node_tree.nodes.remove(n)
        f = [x for x in os.listdir(tdir) if x.startswith(m.name + '_albedo') and not x.endswith('.meta')][0]
        t = m.node_tree.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(os.path.join(tdir, f))
        e = m.node_tree.nodes.new('ShaderNodeEmission'); m.node_tree.links.new(t.outputs[0], e.inputs[0])
        o = m.node_tree.nodes.new('ShaderNodeOutputMaterial'); m.node_tree.links.new(e.outputs[0], o.inputs[0])


# GLB original normalizado como el conversor (escala a 1.7, pies z=0, cuerpo centrado)
sc, cam = setup()
bpy.ops.import_scene.gltf(filepath=glb)
for m in bpy.data.materials:
    if m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type == 'BSDF_PRINCIPLED' and n.inputs['Base Color'].links and n.inputs['Base Color'].links[0].from_node.type == 'TEX_IMAGE':
                m.node_tree.nodes.active = n.inputs['Base Color'].links[0].from_node
src = [o for o in sc.objects if o.type == 'MESH'][0]
src.data.transform(src.matrix_world); src.matrix_world = mathutils.Matrix.Identity(4)
zs = [v.co.z for v in src.data.vertices]; zmin, zmax = min(zs), max(zs)
f = 1.7 / (zmax - zmin)
band = [v.co for v in src.data.vertices if zmin + 0.12 * (zmax - zmin) <= v.co.z <= zmin + 0.55 * (zmax - zmin)]
cx = sum(p.x for p in band) / len(band); cy = sum(p.y for p in band) / len(band)
src.data.transform(mathutils.Matrix.Scale(f, 4) @ mathutils.Matrix.Translation((-cx, -cy, -zmin)))
shoot(sc, cam, os.path.join(outdir, 'face_orig.png'), 0.0)
shoot(sc, cam, os.path.join(outdir, 'face_orig_3q.png'), math.radians(30))

for fbx in fbxs:
    sc, cam = setup()
    bpy.ops.import_scene.fbx(filepath=fbx)
    texturize(os.path.join(os.path.dirname(fbx), 'Textures'))
    label = os.path.splitext(os.path.basename(fbx))[0]
    shoot(sc, cam, os.path.join(outdir, f'face_{label}.png'), 0.0)
    shoot(sc, cam, os.path.join(outdir, f'face_{label}_3q.png'), math.radians(30))
print('FACE ok')
