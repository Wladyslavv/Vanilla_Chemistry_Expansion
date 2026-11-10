"""Draw Xinke shrub sprites (seed packet, small/normal/large leafy branch) in Probably Stolen's plant style.
Usage: python draw_xinke.py <vanilla plant sprite dir> <out dir>"""
import math
import os
import sys

from PIL import Image

ref_dir, out_dir = sys.argv[1], sys.argv[2]
os.makedirs(out_dir, exist_ok=True)

FILLER = (0, 0, 0, 3)  # vanilla: near-invisible pixels so the whole rectangle is the item's shape
OUTLINE = (0x29, 0x22, 0x34)
BR_DK, BR = (0x45, 0x39, 0x43), (0x64, 0x53, 0x55)
LF_DK, LF, LF_LT, LF_HI = (0x41, 0x60, 0x3f), (0x5c, 0x6f, 0x46), (0x84, 0x85, 0x52), (0xaa, 0xa9, 0x82)
BERRY_DK, BERRY, BERRY_HI = (0x7a, 0x2e, 0x33), (0xa8, 0x40, 0x3a), (0xd8, 0x7a, 0x5e)
PETAL, POLLEN = (0xd5, 0xd2, 0xb8), (0xd5, 0xb0, 0x5c)
PAPER, PAPER_DOT = (0xd5, 0xd2, 0xb8), (0xc5, 0xac, 0x96)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h, self.px = w, h, {}

    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[(x, y)] = c

    def leaf(self, cx, cy, length, width, angle_deg, rows=False):
        """Long-elliptic leaf; light midrib along the long axis, dark lower half, highlight near the tip."""
        a, b = length / 2.0, width / 2.0
        t = math.radians(angle_deg)
        ux, uy = math.cos(t), math.sin(t)  # long axis (screen coords, y down)
        for y in range(int(cy - a - 2), int(cy + a + 3)):
            for x in range(int(cx - a - 2), int(cx + a + 3)):
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                along = dx * ux + dy * uy
                across = -dx * uy + dy * ux
                if (along / a) ** 2 + (across / b) ** 2 > 1.0:
                    continue
                if abs(across) < 0.5:
                    c = LF_LT
                elif rows and 0.9 < abs(across) < 1.6 and abs(along) < a * 0.6:
                    c = LF  # the two faint lines beside the midrib
                elif across > 0:
                    c = LF_DK
                else:
                    c = LF
                if along > a * 0.55 and across < 0 and abs(across) < 1.2:
                    c = LF_HI
                self.put(x, y, c)

    def stem(self, x0, y0, x1, y1, thick=1):
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(n + 1):
            x = round(x0 + (x1 - x0) * i / n)
            y = round(y0 + (y1 - y0) * i / n)
            self.put(x, y, BR)
            if thick > 1:
                self.put(x + 1, y, BR_DK)

    def berry(self, x, y, big=False):
        if big:
            for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1), (-1, 0), (-1, 1), (0, -1), (1, 2), (0, 2), (2, 1)):
                self.put(x + dx, y + dy, BERRY)
            for dx, dy in ((1, 1), (1, 2), (2, 1), (0, 2)):
                self.put(x + dx, y + dy, BERRY_DK)
            self.put(x - 1, y, BERRY_HI)
            self.put(x, y - 1, BERRY_HI)
        else:
            self.put(x, y, BERRY_HI)
            self.put(x + 1, y, BERRY)
            self.put(x, y + 1, BERRY)
            self.put(x + 1, y + 1, BERRY_DK)

    def flower(self, x, y):
        for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
            self.put(x + dx, y + dy, PETAL)
        self.put(x, y, POLLEN)

    def image(self):
        im = Image.new("RGBA", (self.w, self.h), FILLER)
        filled = set(self.px)
        for (x, y) in filled:
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < self.w and 0 <= ny < self.h and (nx, ny) not in filled:
                    im.putpixel((nx, ny), OUTLINE + (255,))
        for (x, y), c in self.px.items():
            im.putpixel((x, y), c + (255,))
        return im


def normal():
    c = Canvas(16, 32)
    c.stem(7, 29, 7, 3, thick=2)
    # alternate leaves pointing upward, larger toward the bottom
    for y, side, ln in ((6, 1, 6), (10, -1, 7), (14, 1, 7), (18, -1, 8), (22, 1, 7)):
        cx = 7.5 + side * 3.6
        c.leaf(cx, y, ln, 3.2, -90 + side * 55)
    c.berry(4, 22)
    c.berry(10, 17)
    c.flower(10, 9)
    c.flower(4, 14)
    return c.image()


def small():
    c = Canvas(16, 16)
    c.stem(8, 14, 8, 3, thick=1)
    c.leaf(11.2, 6.5, 6, 3, -35)
    c.leaf(4.8, 9, 6, 3, -145)
    c.leaf(11, 11.5, 5, 2.6, -25)
    c.berry(5, 12)
    return c.image()


def large():
    c = Canvas(32, 32)
    c.stem(15, 30, 15, 3, thick=2)
    c.stem(15, 18, 22, 11)
    c.stem(16, 22, 9, 16)
    for (x, y, ln, ang) in ((19, 5, 9, -50), (11, 8, 9, -130), (26, 9, 9, -20), (20, 15, 8, -40),
                            (6, 14, 9, -160), (11, 21, 9, -140), (21, 22, 9, -30), (9, 27, 7, -150),
                            (22, 27, 7, -25)):
        c.leaf(x, y, ln, 3.6, ang, rows=True)
    for bx, by in ((17, 12), (13, 24), (19, 18)):
        c.berry(bx, by, big=True)
    c.flower(23, 13)
    c.flower(8, 18)
    c.flower(14, 6)
    return c.image()


def seed():
    """Same packet layout as the vanilla seeds, with a crimson frame and a leaf + red berry icon."""
    frame_dk, frame, frame_lt = (0x5a, 0x2c, 0x34), (0x7e, 0x3c, 0x3e), (0x9e, 0x58, 0x4c)
    template = [
        "aaaaaaaaaaaaaaaa",
        "abbbbbbbbbbbbbba",
        "abcdcdcdcdccccba",
        "abeeeeefffeeeeba",
        "aabefccccccfebaa",
        "aabfccccccccfbaa",
        "aabgccccccccgbaa",
        "aabgccccccccgbaa",
        "aabgccccccccgbaa",
        "aabgccccccccgbaa",
        "aabgccccccccgbaa",
        "aabfgccccccfebaa",
        "aabeeeeeeeeeebaa",
        "abeffffffffffeba",
        "abbbbbbbbbbbbbba",
        "aaaaaaaaaaaaaaaa",
    ]
    colors = {"a": None, "b": OUTLINE, "c": PAPER, "d": PAPER_DOT, "e": frame_dk, "f": frame, "g": frame_lt}
    im = Image.new("RGBA", (16, 16), FILLER)
    for y, row in enumerate(template):
        for x, ch in enumerate(row):
            if colors[ch]:
                im.putpixel((x, y), colors[ch] + (255,))
    icon = Canvas(16, 16)  # leaf from lower-left to upper-right, two berries bottom-right, no outline like vanilla
    icon.leaf(7.6, 6.9, 8.4, 3.6, -45)
    icon.put(4, 10, BR)
    icon.put(5, 9, BR)
    icon.berry(9, 8)
    icon.berry(8, 10)
    for (x, y), col in icon.px.items():
        if 4 <= x <= 11 and 4 <= y <= 10:
            im.putpixel((x, y), col + (255,))
    return im


sprites = {"xinke_seed": seed(), "xinke_leaf_s": small(), "xinke_leaf": normal(), "xinke_leaf_l": large()}
for name, im in sprites.items():
    im.save(os.path.join(out_dir, name + ".png"))

# Comparison sheet next to vanilla plants, 6x on the game's dark inventory colour.
refs = ["kotton_plant_seed", "fillerweed_seed", "kotton_plant_s", "kotton_plant", "kotton_plant_l", "fillerweed"]
row = [Image.open(os.path.join(ref_dir, r + ".png")).convert("RGBA") for r in refs]
mine = list(sprites.values())
S, PAD = 6, 10
width = max(sum(i.width * S + PAD for i in row), sum(i.width * S + PAD for i in mine)) + PAD
sheet = Image.new("RGBA", (width, 2 * 32 * S + 3 * PAD), (60, 64, 72, 255))
for r, items in enumerate((mine, row)):
    x = PAD
    for i in items:
        big = i.resize((i.width * S, i.height * S), Image.NEAREST)
        sheet.alpha_composite(big, (x, PAD + r * (32 * S + PAD)))
        x += big.width + PAD
sheet.save(os.path.join(out_dir, "xinke_compare.png"))
print("saved", list(sprites))
