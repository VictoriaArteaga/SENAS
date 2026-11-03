import bpy,sys,mathutils,collections
src=sys.argv[sys.argv.index('--')+1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
for o in bpy.context.scene.objects:
    if o.type!='MESH': continue
    vs=[o.matrix_world@v.co for v in o.data.vertices]
    xs=[v.x for v in vs]; ys=[v.y for v in vs]; zs=[v.z for v in vs]
    print(f"{o.name:14} z[{min(zs):.3f},{max(zs):.3f}] x[{min(xs):.3f},{max(xs):.3f}] y[{min(ys):.3f},{max(ys):.3f}]")
# perfil de z para head/body/shirt: anchura x por bandas de z entre 1.25 y 1.50
print('--- bandas z (n vértices con |x|<0.09) por malla')
for o in bpy.context.scene.objects:
    if o.type!='MESH': continue
    c=collections.Counter()
    for v in o.data.vertices:
        w=o.matrix_world@v.co
        if abs(w.x)<0.09 and 1.20<=w.z<1.60: c[round(w.z*20)/20]+=1
    print(o.name, sorted(c.items()))
