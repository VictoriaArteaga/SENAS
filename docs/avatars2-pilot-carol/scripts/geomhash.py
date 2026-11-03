import bpy,sys,hashlib
fs=sys.argv[sys.argv.index('--')+1:]
for f in fs:
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f)
    o=[x for x in bpy.context.scene.objects if x.type=='MESH'][0]; me=o.data
    pts=sorted((round(v.co.x,5),round(v.co.y,5),round(v.co.z,5)) for v in me.vertices)
    uv=me.uv_layers[0].data; uvs=sorted((round(u.uv[0],5),round(u.uv[1],5)) for u in uv)
    print('GEOM',f.split('/')[-3]+'/'+f.split('/')[-1],'verts',len(me.vertices),'tris',sum(len(p.vertices)-2 for p in me.polygons),'hash_pos',hashlib.md5(str(pts).encode()).hexdigest()[:12],'hash_uv',hashlib.md5(str(uvs).encode()).hexdigest()[:12])
