"""Hoja comparativa original (arriba) vs convertido (abajo). Uso: python cmp_sheet.py <id> <prefijo_orig> <prefijo_conv> <salida.png>"""
import sys
from PIL import Image, ImageDraw

pid, po, pc, out = sys.argv[1:5]
views = ['front', 'left', 'right', 'back', 'threeq', 'face']
W, H = 330, 470
S = Image.new('RGB', (W * len(views), 2 * (H + 22)), (30, 30, 30))
d = ImageDraw.Draw(S)
for c, v in enumerate(views):
    for r, (pre, lab) in enumerate(((po, 'GLB original'), (pc, 'FBX convertido'))):
        im = Image.open(f'{pre}_{v}.png').convert('RGB').resize((W, H))
        S.paste(im, (c * W, r * (H + 22) + 22))
        d.text((c * W + 6, r * (H + 22) + 5), f'{pid} {v} - {lab}', fill=(255, 255, 255))
S.save(out)
