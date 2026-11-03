import bpy,sys,mathutils
a=sys.argv[sys.argv.index('--')+1:]
glb=a[0]; fbxs=a[1:]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=glb)
o=[x for x in bpy.context.scene.objects if x.type=='MESH'][0]; me=o.data; me.transform(o.matrix_world)
zs=[v.co.z for v in me.vertices]; zmin,zmax=min(zs),max(zs); f=1.7/(zmax-zmin)
def cnt(m,zc,ysel=None):
    n=0
    for p in m.polygons:
        c=p.center
        if c.z*f_>=zc and (ysel is None or c.y<ysel): n+=sum(0 for _ in [0])+len(p.vertices)-2
    return n
f_=f
print('HEAD orig  tris z>=1.45: %d  (cara y<0: %d)  total %d' % (cnt(me,1.45+zmin*f), cnt(me,1.45+zmin*f,0.0), sum(len(p.vertices)-2 for p in me.polygons)))
for fb in fbxs:
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=fb)
    o=[x for x in bpy.context.scene.objects if x.type=='MESH'][0]; me=o.data; me.transform(o.matrix_world)
    f_=1.0
    print('HEAD %s tris z>=1.45: %d  (cara y<0: %d)  total %d' % (fb.split('/')[-1], cnt(me,1.45), cnt(me,1.45,0.0), sum(len(p.vertices)-2 for p in me.polygons)))
