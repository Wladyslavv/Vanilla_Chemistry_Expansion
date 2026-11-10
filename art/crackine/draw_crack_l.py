"""Large (16x32) vial of pale yellow powder, matching the small Crackine vial.
Usage: python draw_crack_l.py <out dir> <small vial png>"""
import os, sys
from PIL import Image
out, small_path = sys.argv[1], sys.argv[2]
rows = [
    "................",
    "................",
    "......bbbb......",
    ".....bccccb.....",
    "....bddccccb....",
    "....bedddceb....",
    "....bfeeeefb....",
    ".....bffffb.....",
    "....bcgggghb....",
    "...bhcggggxhb...",
    "...bhcgggggxb...",
    "...bhcgggggxb...",
    "...bhPPPPPPhb...",
    "...bcPPMMMMSb...",
    "...bcMMMMMMSb...",
    "...bcMMMMMSSb...",
    "...bcMMMMMSSb...",
    "...bcMMMMSSSb...",
    "...bcMMMMSSSb...",
    "...bcMMMSSSSb...",
    "...bcMMMSSSDb...",
    "...bcMMSSSSDb...",
    "...bcMMSSSDDb...",
    "...biMSSSSDDb...",
    "...biMSSSDDib...",
    "....biSSSDib....",
    ".....biiiib.....",
    "......bbbb......",
    "................",
    "................",
    "................",
    "................",
]
rows = ["." * 16] + rows[:-1]  # centre vertically
pal = {"b": "292234", "c": "a8aea5", "d": "d5d2b8", "e": "79746e", "f": "392e3c", "g": "44464c", "h": "5f6868",
       "x": "88948a", "i": "80929b",
       "P": "f4ecc0", "M": "e0d09b", "S": "c4aa6c", "D": "a08a58"}
im = Image.new("RGBA", (16, 32), (0, 0, 0, 3))
for y, r in enumerate(rows):
    assert len(r) == 16, y
    for x, ch in enumerate(r):
        if ch != ".":
            h = pal[ch]
            im.putpixel((x, y), (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255))
im.save(os.path.join(out, "vce_crackine_vial_l.png"))
small = Image.open(small_path).convert("RGBA")
S, P = 10, 12
sheet = Image.new("RGBA", (2 * 16 * S + 3 * P, 32 * S + 2 * P), (60, 64, 72, 255))
sheet.alpha_composite(small.resize((16 * S, 16 * S), Image.NEAREST), (P, P + 16 * S))
sheet.alpha_composite(im.resize((16 * S, 32 * S), Image.NEAREST), (2 * P + 16 * S, P))
sheet.save(os.path.join(out, "crackine_sizes.png"))
