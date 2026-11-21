# The methanol vapour canister: the hydrogen canister with its red band recoloured amber.
# usage: python draw_meoh_canister.py H2_PNG OUT_PNG
import sys
from PIL import Image

SWAP = {
    (0xc2, 0x4a, 0x3d): (0xc8, 0x8a, 0x2e),  # band
    (0xe0, 0x7a, 0x66): (0xe8, 0xb8, 0x5c),  # band highlight
    (0x64, 0x34, 0x3b): (0x6e, 0x48, 0x1e),  # band shade
}
im = Image.open(sys.argv[1]).convert('RGBA')
px = im.load()
for y in range(im.size[1]):
    for x in range(im.size[0]):
        r, g, b, a = px[x, y]
        if (r, g, b) in SWAP:
            px[x, y] = SWAP[(r, g, b)] + (a,)
im.save(sys.argv[2])
print(sys.argv[2], im.size)
