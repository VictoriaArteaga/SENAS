import bpy,sys,mathutils
args=sys.argv[sys.argv.index('--')+1:]
src,out,height=args[0],args[1],float(args[2])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in bpy.context.scene.objects:
    if o.type!='MESH': print('NON-MESH',o.name,o.type)
# Raíz única para escalar y centrar todo junto.
root=bpy.data.objects.new('Carol',None); bpy.context.scene.collection.objects.link(root)
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
    mw=o.matrix_world.copy(); o.parent=None; o.matrix_world=mw
    o.name='Carol_'+o.name
    o.data.materials[0].name='carol_'+o.name.split('_',1)[1]
    o.select_set(True)
bpy.context.view_layer.objects.active=meshes[0]
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)   # hornea la rotación/escala (p. ej. el pelo)
def bbox():
    pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    return (mathutils.Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts))),
            mathutils.Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts))))
mn,mx=bbox(); f=height/(mx.z-mn.z)
off=mathutils.Vector(((mn.x+mx.x)/2,(mn.y+mx.y)/2,mn.z))     # pies en el origen, centrado en X/Y
for o in meshes:
    o.data.transform(mathutils.Matrix.Scale(f,4)@mathutils.Matrix.Translation(-off))
    o.parent=root
mn,mx=bbox()
print('FINAL bbox min',tuple(round(v,3) for v in mn),'max',tuple(round(v,3) for v in mx),'factor',round(f,4))
tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes); print('TRIS',tris)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,object_types={'MESH','EMPTY'},path_mode='STRIP',embed_textures=False,
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',
    mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,bake_anim=False)
print('EXPORTED',out)
