import bpy,sys,math,mathutils
args=sys.argv[sys.argv.index('--')+1:]
src,outdir,tag=args[0],args[1],args[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
if src.endswith('.glb'):
    bpy.ops.import_scene.gltf(filepath=src)
    for m in bpy.data.materials:
        if not m.use_nodes: continue
        for n in m.node_tree.nodes:
            if n.type=='BSDF_PRINCIPLED':
                l=n.inputs['Base Color'].links
                if l and l[0].from_node.type=='TEX_IMAGE': m.node_tree.nodes.active=l[0].from_node
else:
    bpy.ops.import_scene.fbx(filepath=src)
    import os
    tdir=os.path.join(os.path.dirname(src),'Textures')
    for o in bpy.context.scene.objects:
        if o.type=='MESH':
            for m in o.data.materials:
                m.use_nodes=True
                for n in list(m.node_tree.nodes): m.node_tree.nodes.remove(n)
                t=m.node_tree.nodes.new('ShaderNodeTexImage')
                f=[x for x in os.listdir(tdir) if x.startswith(m.name+'_albedo') and not x.endswith('.meta')][0]
                t.image=bpy.data.images.load(os.path.join(tdir,f))
                out=m.node_tree.nodes.new('ShaderNodeEmission'); m.node_tree.links.new(t.outputs[0],out.inputs[0])
                mo=m.node_tree.nodes.new('ShaderNodeOutputMaterial'); m.node_tree.links.new(out.outputs[0],mo.inputs[0])
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
pts=[o.matrix_world@mathutils.Vector(c) for o in meshes for c in o.bound_box]
mn=mathutils.Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts)))
mx=mathutils.Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts)))
print('BBOX min',tuple(round(v,3) for v in mn),'max',tuple(round(v,3) for v in mx),'size',tuple(round(v,3) for v in (mx-mn)))
sc=bpy.context.scene
sc.render.engine='BLENDER_WORKBENCH'
sh=sc.display.shading; sh.light='FLAT'; sh.color_type='TEXTURE'
sc.render.resolution_x=700; sc.render.resolution_y=1000
sc.world=bpy.data.worlds.new('w'); sc.world.color=(0.5,0.5,0.5)
cam=bpy.data.objects.new('c',bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'
ctr=(mn+mx)/2; h=max(mx.z-mn.z,mx.y-mn.y)
def shot(name,yaw,scale=1.15,center=None,fh=None):
    c=center or ctr
    d=mathutils.Vector((math.sin(yaw),-math.cos(yaw),0))*10
    cam.location=c+d
    cam.rotation_euler=(math.pi/2,0,yaw)
    cam.data.ortho_scale=(fh or h)*scale
    sc.render.filepath=f'{outdir}/{tag}_{name}.png'
    bpy.ops.render.render(write_still=True)
shot('front',0); shot('left',math.pi/2); shot('right',-math.pi/2); shot('back',math.pi); shot('threeq',math.radians(35))
# head closeup: top 20% of the body height
top=mx.z-0.12*(mx.z-mn.z)
shot('face',0,1.0,mathutils.Vector((ctr.x,ctr.y,top)),0.25*(mx.z-mn.z))
