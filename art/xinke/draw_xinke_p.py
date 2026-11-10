"""Draw the Xinke shrub as it grows in the hydroponic machine (48x64 overlays, like vanilla coffee_bean_p).
_p: growing, with small yellow-white flowers; _p2: ripening, with single red berries.
Usage: python draw_xinke_p.py <vanilla plant sprite dir> <out dir>"""
import math
import os
import random
import sys

from PIL import Image

ref_dir, out_dir = sys.argv[1], sys.argv[2]
os.makedirs(out_dir, exist_ok=True)
W, H = 48, 64
FILLER = (0, 0, 0, 3)
OUTLINE = (0x29, 0x22, 0x34)
EDGE = (0x30, 0x3a, 0x41)  # dark rim between overlapping leaves, from the vanilla coffee plant
BR_DK, BR = (0x45, 0x39, 0x43), (0x64, 0x53, 0x55)
LF_DK, LF, LF_LT, LF_HI = (0x41, 0x60, 0x3f), (0x5c, 0x6f, 0x46), (0x84, 0x85, 0x52), (0xaa, 0xa9, 0x82)
BERRY_DK, BERRY, BERRY_HI = (0x74, 0x31, 0x3b), (0x9c, 0x37, 0x38), (0xcc, 0x77, 0x55)
PETAL, POLLEN = (0xd5, 0xd2, 0xb8), (0xe0, 0xd0, 0x9b)
WATER = {(0x27, 0x56, 0x54), (0x45, 0x91, 0x8b), (0x67, 0xaa, 0xa5), (0x27, 0x34, 0x56)}

BASE = (24, 45)
# Straight branches fanning out from a short trunk: (end x, end y)
BRANCHES = [(14, 25), (19, 22), (25, 21), (31, 22), (35, 27), (11, 32), (38, 33)]


def build(stage):
    px = {}
    ring = set()  # thin dark rims between leaves; turned into the outline where they touch empty space

    def put(x, y, c):
        if 4 <= x <= 44 and 12 <= y < H:  # stay inside the area vanilla plants use (under the lamp)
            px[(x, y)] = c

    def line(x0, y0, x1, y1, c):
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        pts = []
        for i in range(n + 1):
            p = (round(x0 + (x1 - x0) * i / n), round(y0 + (y1 - y0) * i / n))
            put(p[0], p[1], c)
            pts.append(p)
        return pts

    def leaf(cx, cy, length, width, ang):
        a, b = length / 2.0, width / 2.0
        ux, uy = math.cos(ang), math.sin(ang)
        for y in range(int(cy - a - 1), int(cy + a + 2)):
            for x in range(int(cx - a - 1), int(cx + a + 2)):
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                along, across = dx * ux + dy * uy, -dx * uy + dy * ux
                v = (along / (a + 0.55)) ** 2 + (across / (b + 0.55)) ** 2
                inner = (along / a) ** 2 + (across / b) ** 2
                if v > 1.0:
                    continue
                if inner > 1.0:
                    put(x, y, EDGE)
                    ring.add((x, y))
                    continue
                v = inner
                lower = across * (1 if ux >= 0 else -1) > 0  # the half facing down on screen
                if v > 0.7 and lower:
                    c = LF_DK
                elif abs(across) < 0.45 and abs(along) < a * 0.8:
                    c = LF_LT
                elif lower:
                    c = LF_DK
                else:
                    c = LF
                if along > a * 0.35 and not lower and 0.45 <= abs(across) < 1.1:
                    c = LF_HI
                put(x, y, c)
                ring.discard((x, y))

    rng = random.Random(11)
    # trunk
    for y in range(37, 46):
        put(23, y, BR)
        put(24, y, BR_DK)
    stems = []
    for ex, ey in BRANCHES:
        sx, sy = 23 + (1 if ex > 24 else 0), 38 if abs(ex - 24) < 12 else 40
        stems.append(line(sx, sy, ex, ey, BR))
    # leaves, back to front: outer branches first, alternate sides every 3 px along each stem
    flowers, berries = [], []
    for stem in sorted(stems, key=lambda s: -abs(s[-1][0] - 24)):
        (x0, y0), (x1, y1) = stem[0], stem[-1]
        base_ang = math.atan2(y1 - y0, x1 - x0)
        n = len(stem)
        for k, i in enumerate(range(n - 3, 6, -6)):
            side = 1 if k % 2 == 0 else -1
            x, y = stem[i]
            spread = math.radians(34 + rng.randint(-5, 5))
            ang = base_ang + side * spread
            ln = 10 + (1 if i < n * 0.6 else 0) + rng.randint(0, 1)
            leaf(x + math.cos(ang) * ln * 0.45, y + math.sin(ang) * ln * 0.48, ln, 4.6, ang)
            if rng.random() < 0.75:  # flowers / berries hang just under the leaves along the branch
                fx = round(x + math.cos(ang) * ln * 0.5)
                fy = round(y + math.sin(ang) * ln * 0.5 + 2.5)
                (flowers if stage == "p" else berries).append((fx, fy))
        # leaf bud at the tip
        leaf(x1 + math.cos(base_ang) * 3.5, y1 + math.sin(base_ang) * 3.5, 8, 3.8, base_ang)
    # redraw the stems on top in the lower part so the fan shape stays readable
    for stem in stems:
        for (x, y) in stem[: len(stem) * 3 // 4]:
            put(x, y, BR)
    if stage == "p":
        for (x, y) in flowers:
            for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
                put(x + dx, y + dy, EDGE)
            for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
                put(x + dx, y + dy, PETAL)
            put(x, y, POLLEN)
    else:
        for (x, y) in berries:
            for dx, dy in ((2, 0), (2, 1), (0, 2), (1, 2), (2, 2)):
                put(x + dx, y + dy, EDGE)
            put(x, y, BERRY_HI)
            put(x + 1, y, BERRY)
            put(x, y + 1, BERRY)
            put(x + 1, y + 1, BERRY_DK)

    im = Image.new("RGBA", (W, H), FILLER)
    filled = set(px)
    outer = set()
    for (x, y) in filled:
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < W and 0 <= ny < 47 and (nx, ny) not in filled:
                if (x, y) in ring:
                    px[(x, y)] = OUTLINE  # a leaf rim on the silhouette is the outline itself
                else:
                    outer.add((nx, ny))
    for (x, y) in outer:
        im.putpixel((x, y), OUTLINE + (255,))
    for (x, y), c in px.items():
        im.putpixel((x, y), c + (255,))
    # water ripples around the trunk, taken from the vanilla coffee overlay
    coffee = Image.open(os.path.join(ref_dir, "coffee_bean_p.png")).convert("RGBA")
    for y in range(42, 47):
        for x in range(10, 40):
            p = coffee.getpixel((x, y))
            if p[3] > 3 and p[:3] in WATER and (x, y) not in filled:
                im.putpixel((x + 1, y), p)
    return im


sprites = {"xinke_leaf_p": build("p"), "xinke_leaf_p2": build("p2")}
for name, im in sprites.items():
    im.save(os.path.join(out_dir, name + ".png"))

machine = Image.open(os.path.join(ref_dir, "hydroponic.png")).convert("RGBA")
tiles = []
for name in ("xinke_leaf_p", "xinke_leaf_p2", "coffee_bean_p", "kotton_fiber_p", "bloomberry_p2"):
    t = machine.copy()
    src = sprites[name] if name in sprites else Image.open(os.path.join(ref_dir, name + ".png")).convert("RGBA")
    t.alpha_composite(src)
    tiles.append(t)
S = 5
sheet = Image.new("RGBA", (len(tiles) * (W * S + 8) + 8, H * S + 16), (60, 64, 72, 255))
for i, t in enumerate(tiles):
    sheet.alpha_composite(t.resize((W * S, H * S), Image.NEAREST), (8 + i * (W * S + 8), 8))
sheet.save(os.path.join(out_dir, "xinke_machine_compare.png"))
print("saved", list(sprites))
