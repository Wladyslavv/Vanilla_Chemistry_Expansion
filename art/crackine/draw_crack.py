"""Small vial of pale yellow powder, same family as the vanilla Dream Dust vial.
Usage: python draw_crack.py <vanilla sprite dir> <out dir>"""
import os, sys
from PIL import Image
ref, out = sys.argv[1], sys.argv[2]
rows = [
    "................",
    "......bbbb......",
    ".....bccccb.....",
    "....bddccccb....",
    "....bedddceb....",
    "....bfeeeefb....",
    ".....bffffb.....",
    "....bcgggghb....",
    "....bhcgggxb....",
    "....bhPPPPhb....",
    "....bcPMMMSb....",
    "....bcMMMSSb....",
    "....biMMSSDb....",
    ".....biSSib.....",
    "......bbbb......",
    "................",
]
pal = {"b": "292234", "c": "a8aea5", "d": "d5d2b8", "e": "79746e", "f": "392e3c", "g": "44464c", "h": "5f6868",
       "x": "88948a", "i": "80929b",
       "P": "f4ecc0", "M": "e0d09b", "S": "c4aa6c", "D": "a08a58"}
im = Image.new("RGBA", (16, 16), (0, 0, 0, 3))
for y, r in enumerate(rows):
    for x, ch in enumerate(r):
        if ch != ".":
            h = pal[ch]
            im.putpixel((x, y), (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255))
im.save(os.path.join(out, "vce_crackine_vial.png"))
S, P = 10, 12
refs = [Image.open(os.path.join(ref, n + ".png")).convert("RGBA") for n in ("dream_dust", "nightmare_dust")]
tiles = [im] + refs
sheet = Image.new("RGBA", (len(tiles) * (16 * S + P) + P, 16 * S + 2 * P), (60, 64, 72, 255))
for i, t in enumerate(tiles):
    sheet.alpha_composite(t.resize((16 * S, 16 * S), Image.NEAREST), (P + i * (16 * S + P), P))
sheet.save(os.path.join(out, "crackine_compare.png"))
