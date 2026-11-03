"""Extrae las texturas de un GLB de material único (Meshy y similares) directamente de sus bytes originales.
Uso: python -I extract_textures_single.py <glb> <carpeta destino/Textures> <id> [parte=body]
Escribe {id}_{parte}_albedo.*, _normal.*, _metalsmooth.png (R = metalico, A = suavidad = 1 - rugosidad, el _MetallicGlossMap de URP/Lit)
y _emission.* solo si el mapa emisivo aporta luz (no es casi negro). NO usa las carpetas textures/ de AVATARS2 (estan volteadas)."""
import io
import json
import os
import struct
import sys

from PIL import Image

glb, dst, pid = sys.argv[1], sys.argv[2], sys.argv[3]
part = sys.argv[4] if len(sys.argv) > 4 else 'body'
d = open(glb, 'rb').read()
l = struct.unpack('<I', d[12:16])[0]
j = json.loads(d[20:20 + l])
bs = 20 + l + 8


def image(tex):
    t = j['textures'][tex['index']]
    im = j['images'][t['source']]
    bv = j['bufferViews'][im['bufferView']]
    o = bs + bv.get('byteOffset', 0)
    raw = d[o:o + bv['byteLength']]
    return Image.open(io.BytesIO(raw)), im.get('mimeType', 'image/png'), raw


os.makedirs(dst, exist_ok=True)
assert len(j['materials']) == 1, 'este script es para GLB de un solo material'
m = j['materials'][0]
pb = m['pbrMetallicRoughness']


def write_raw(raw, mime, suffix):
    ext = '.jpeg' if 'jpeg' in mime else '.png'
    path = os.path.join(dst, f'{pid}_{part}_{suffix}{ext}')
    open(path, 'wb').write(raw)
    return path


a, mime, raw = image(pb['baseColorTexture'])
write_raw(raw, mime, 'albedo')
print(f'albedo {a.size} {a.mode} {mime}')
n, mime, raw = image(m['normalTexture'])
write_raw(raw, mime, 'normal')
print(f'normal {n.size} {n.mode} {mime}')

mrf = pb.get('metallicFactor', 1.0)
rf = pb.get('roughnessFactor', 1.0)
if 'metallicRoughnessTexture' in pb:
    mr, _, _ = image(pb['metallicRoughnessTexture'])
    mr = mr.convert('RGB')
    _, g, b = mr.split()
    metal = b.point(lambda v: int(v * mrf))
    smooth = g.point(lambda v: 255 - int(v * rf))
else:
    size = a.size
    metal = Image.new('L', size, int(255 * mrf))
    smooth = Image.new('L', size, 255 - int(255 * rf))
    mr = None
Image.merge('RGBA', (metal, metal, metal, smooth)).save(os.path.join(dst, f'{pid}_{part}_metalsmooth.png'))
print(f'metalsmooth {smooth.size} (metallicFactor={mrf}, roughnessFactor={rf}) metal max={metal.getextrema()[1]} smooth range={smooth.getextrema()}')

if 'emissiveTexture' in m:
    e, mime, raw = image(m['emissiveTexture'])
    ef = m.get('emissiveFactor', [1, 1, 1])
    ex = e.convert('RGB').getextrema()
    mx = max(c[1] for c in ex)
    print(f'emissive {e.size} {mime} emissiveFactor={ef} maximo por canal={[c[1] for c in ex]}')
    if mx > 12:
        write_raw(raw, mime, 'emission')
        print('  -> emisivo con contenido: se extrae')
    else:
        print('  -> emisivo casi negro: NO se extrae (no aporta luz)')
print('doubleSided', m.get('doubleSided'), 'alphaMode', m.get('alphaMode'))
