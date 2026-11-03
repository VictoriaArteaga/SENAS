"""Analisis de componentes conectados (tras soldar por posicion) de un FBX ya convertido, para decidir si cabeza/pelo/accesorios se pueden separar.
Uso: blender -b --python comp_analysis.py -- <fbx>"""
import sys

import bmesh
import bpy
import mathutils

src = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
o = [x for x in bpy.context.scene.objects if x.type == 'MESH'][0]
bm = bmesh.new(); bm.from_mesh(o.data)
mw = o.matrix_world
for v in bm.verts:
    v.co = mw @ v.co
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
seen = set(); comps = []
for v in bm.verts:
    if v.index in seen or not v.link_faces:
        continue
    st = [v]; seen.add(v.index); c = [v]
    while st:
        a = st.pop()
        for e in a.link_edges:
            w = e.other_vert(a)
            if w.index not in seen:
                seen.add(w.index); st.append(w); c.append(w)
    comps.append(c)
rows = []
for c in comps:
    fs = set(f.index for v in c for f in v.link_faces)
    zs = [v.co.z for v in c]; xs = [v.co.x for v in c]; ys = [v.co.y for v in c]
    rows.append((len(fs), min(zs), max(zs), min(xs), max(xs), min(ys), max(ys)))
rows.sort(reverse=True)
tot = sum(r[0] for r in rows)
print('COMP', src.split('/')[-1], 'componentes', len(rows), 'caras', tot)
for r in rows[:8]:
    print('COMP   caras=%6d (%.1f%%) z[%.2f,%.2f] x[%.2f,%.2f] y[%.2f,%.2f]' % (r[0], 100.0 * r[0] / tot, r[1], r[2], r[3], r[4], r[5], r[6]))
# perfil del cuello: area aproximada (ancho x profundo) de vertices por banda z en la zona del cuello, para localizar la cintura del cuello
print('COMP perfil por banda z (n vertices, ancho max|x|*2, prof y) 1.30-1.62')
for z0 in [x / 100 for x in range(130, 164, 2)]:
    b = [v.co for v in bm.verts if z0 <= v.co.z < z0 + 0.02 and abs(v.co.x) < 0.25]
    if b:
        print('COMP   z%.2f n=%5d  ancho=%.3f  y[%.3f,%.3f]' % (z0, len(b), 2 * max(abs(p.x) for p in b), min(p.y for p in b), max(p.y for p in b)))
