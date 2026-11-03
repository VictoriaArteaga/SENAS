import bpy,sys
fs=sys.argv[sys.argv.index('--')+1:]
for f in fs:
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f)
    o=[x for x in bpy.context.scene.objects if x.type=='MESH'][0]; me=o.data; me.transform(o.matrix_world)
    t=sum(len(p.vertices)-2 for p in me.polygons); h=sum(len(p.vertices)-2 for p in me.polygons if p.center.z>=1.42)
    print('HEADN',f.split('/')[-1],'total',t,'cabeza(z>=1.42)',h)
