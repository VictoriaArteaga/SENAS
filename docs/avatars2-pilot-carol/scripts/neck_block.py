
# ---- Separar el cuello de Carol_head (Carol_head = cara y craneo; Carol_neck = cuello y escote) ----
# La malla original son ~190 parches con vertices sin soldar en los bordes. El anillo de corte se halla en una copia SOLDADA por posicion
# (solo para localizarlo); las piezas se construyen desde la malla original, sin vertices nuevos en el corte, para conservar UV y normales.
import bmesh, collections as _c
ZC=1.365                                  # entre las bandas z 1.36 (cuello, y>=-0.072) y 1.37 (aparece el menton, y=-0.13)
hd=[o for o in meshes if o.name=='Carol_head'][0]; hm=hd.data
cnorm=[mathutils.Vector(n.vector) for n in hm.corner_normals]
poly_neck=[p.center.z<ZC for p in hm.polygons]
# 1) anillo de corte en la copia soldada
w=bmesh.new(); w.from_mesh(hm); bmesh.ops.remove_doubles(w,verts=w.verts,dist=1e-5); w.faces.ensure_lookup_table(); w.edges.ensure_lookup_table(); w.verts.ensure_lookup_table()
assert len(w.faces)==len(poly_neck)
cut=[e for e in w.edges if len(e.link_faces)==2 and poly_neck[e.link_faces[0].index]!=poly_neck[e.link_faces[1].index]]
adj=_c.defaultdict(list)
for e in cut: adj[e.verts[0].index].append(e.verts[1].index); adj[e.verts[1].index].append(e.verts[0].index)
assert all(len(v)==2 for v in adj.values()), 'el anillo de corte no es un bucle simple'
start=next(iter(adj)); ring=[start]; prev=None; cur=start
while True:
    nxt=[n for n in adj[cur] if n!=prev]
    n=nxt[0] if prev is not None else adj[cur][0]
    if n==start: break
    ring.append(n); prev,cur=cur,n
assert len(ring)==len(adj), 'el anillo no es un unico bucle (%d de %d)'%(len(ring),len(adj))
ringpos=[w.verts[i].co.copy() for i in ring]
w.free()
print('NECK anillo de corte: vertices',len(ringpos),'z',round(min(p.z for p in ringpos),3),round(max(p.z for p in ringpos),3))
# 2) piezas desde la malla original
bm=bmesh.new(); bm.from_mesh(hm); bm.faces.ensure_lookup_table()
fid=bm.faces.layers.int.new('orig')
for f in bm.faces: f[fid]=f.index
def piece(keep_neck):
    b=bm.copy(); b.faces.ensure_lookup_table()
    bmesh.ops.delete(b,geom=[f for f in b.faces if poly_neck[f[b.faces.layers.int['orig']]]!=keep_neck],context='FACES')
    return b
def to_mesh(b,name,cap):
    b.faces.ensure_lookup_table(); fl=b.faces.layers.int['orig']; uvl=b.loops.layers.uv.active
    ncap=0
    if cap:
        rset=[p for p in ringpos]
        uvs=[l[uvl].uv.copy() for f in b.faces for l in f.loops if any((l.vert.co-p).length<2e-5 for p in rset)]
        mean=sum(uvs,mathutils.Vector((0,0)))/len(uvs)
        center=sum(ringpos,mathutils.Vector())/len(ringpos)
        vs=[b.verts.new(p) for p in ringpos]; vc=b.verts.new(center)
        for i in range(len(vs)):
            f=b.faces.new((vc,vs[i],vs[(i+1)%len(vs)])); f.material_index=0; f[fl]=-1; ncap+=1
            f.normal_update()
            if f.normal.z<0: f.normal_flip(); f.normal_update()
            for l in f.loops: l[uvl].uv=mean
        print('NECK tapa: triangulos',ncap,'UV',tuple(round(c,3) for c in mean),'normal.z medio',round(sum(f.normal.z for f in b.faces if f[fl]==-1)/ncap,3))
    b.faces.ensure_lookup_table()
    order=[f[fl] for f in b.faces]
    me=bpy.data.meshes.new(name); b.to_mesh(me)
    norms=[]
    for f,o in zip(me.polygons,order):
        if o>=0:
            op=hm.polygons[o]; norms+=[cnorm[op.loop_start+k] for k in range(op.loop_total)]
        else: norms+=[mathutils.Vector((0,0,1))]*f.loop_total
    me.normals_split_custom_set(norms)
    return me
neck_me=to_mesh(piece(True),'Carol_neck',True)
head_me=to_mesh(piece(False),'Carol_head',False)
for m in (neck_me,head_me):
    m.materials.clear(); m.materials.append(hm.materials[0])
hd.data=head_me
neck=bpy.data.objects.new('Carol_neck',neck_me); bpy.context.scene.collection.objects.link(neck)
meshes.append(neck)
print('NECK piezas: cabeza',len(head_me.polygons),'caras | cuello',len(neck_me.polygons),'caras (incluye tapa); total',len(head_me.polygons)+len(neck_me.polygons)-len(ringpos),'= original',len(poly_neck))
# comprobaciones de normales y UV de la pieza de cuello
b=bmesh.new(); b.from_mesh(neck_me)
print('NECK check cuello: no-manifold',sum(1 for e in b.edges if not e.is_manifold and not e.is_boundary),'caras degeneradas',sum(1 for f in b.faces if f.calc_area()<1e-12))
b.free()
