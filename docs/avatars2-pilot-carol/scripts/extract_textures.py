import json,struct,io,os,sys
from PIL import Image
glb,dst,pid=sys.argv[1],sys.argv[2],sys.argv[3]
d=open(glb,'rb').read(); l=struct.unpack('<I',d[12:16])[0]; j=json.loads(d[20:20+l]); bs=20+l+8
def img(texinfo):
    t=j['textures'][texinfo['index']]; im=j['images'][t['source']]; bv=j['bufferViews'][im['bufferView']]
    o=bs+bv.get('byteOffset',0); raw=d[o:o+bv['byteLength']]; return Image.open(io.BytesIO(raw)), im.get('mimeType'), raw
# parte de cada material: nombre del nodo que lo usa
part={}
for n in j['nodes']:
    if 'mesh' in n:
        for p in j['meshes'][n['mesh']]['primitives']: part[p['material']]=n['name']
os.makedirs(dst,exist_ok=True)
for mi,m in enumerate(j['materials']):
    p=part[mi]; pb=m['pbrMetallicRoughness']
    a,mime,araw=img(pb['baseColorTexture']); ext='.jpeg' if 'jpeg' in mime else '.png'
    open(os.path.join(dst,f'{pid}_{p}_albedo{ext}'),'wb').write(araw)
    nrm,mime,nraw=img(m['normalTexture']); ext='.jpeg' if 'jpeg' in mime else '.png'
    open(os.path.join(dst,f'{pid}_{p}_normal{ext}'),'wb').write(nraw)
    mr,_,_r=img(pb['metallicRoughnessTexture']); mr=mr.convert('RGB'); r,g,b=mr.split()
    metal=b if pb.get('metallicFactor',1)!=0 else Image.new('L',mr.size,0)
    smooth=g.point(lambda v:255-v)
    Image.merge('RGBA',(metal,metal,metal,smooth)).save(os.path.join(dst,f'{pid}_{p}_metalsmooth.png'))
    print(p,mi,'albedo',a.size,'normal',nrm.size,'mr',mr.size,'metalFactor',pb.get('metallicFactor',1),'doubleSided',m.get('doubleSided'))
