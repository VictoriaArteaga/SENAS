"""Render PBR del GLB ORIGINAL (nodos del importador glTF de Blender, Cycles CPU) para comprobar si el brillo que se ve en Unity viene del propio modelo.
Uso: blender -b --python pbr_check.py -- <glb> <salida.png> <z_centro> [alto_cuadro]"""
import math
import sys

import bpy
import mathutils

glb, out, zc = sys.argv[sys.argv.index('--') + 1:][:3]
fh = float(sys.argv[sys.argv.index('--') + 4]) if len(sys.argv) > sys.argv.index('--') + 4 else 0.45
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
sc = bpy.context.scene
meshes = [o for o in sc.objects if o.type == 'MESH']
pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
zmin = min(p.z for p in pts); zmax = max(p.z for p in pts)
k = 1.7 / (zmax - zmin)
zc = float(zc)   # altura relativa del centro (en metros, ya a escala 1.7)
sc.render.engine = 'CYCLES'; sc.cycles.samples = 24; sc.cycles.device = 'CPU'
sc.render.resolution_x = 700; sc.render.resolution_y = 700
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.55, 0.65, 0.9, 1); w.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.0
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sc.collection.objects.link(sun)
sun.data.energy = 3.0; sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = fh / k
cx = sum(p.x for p in pts) / len(pts)
target = mathutils.Vector((cx, 0, zmin + zc / k))
cam.location = target + mathutils.Vector((0, -10, 0)); cam.rotation_euler = (math.pi / 2, 0, 0)
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
print('PBR ok', out)
