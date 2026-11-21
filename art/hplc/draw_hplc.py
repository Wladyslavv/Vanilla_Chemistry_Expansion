# High-performance liquid chromatograph (32x48, 2 cells wide x 3 tall), in the vanilla machines' three-quarter view
# (chem_finisher, blender, heating_plate, evaporator): a lighter top face over each front face, round parts as
# cylinders with elliptical tops, grey-green metal with teal trim, light from the top left, outline 292234.
# A stacked HPLC: a solvent tray on top with three reservoir bottles (clear, amber, blue) whose lines run down into
# the stack; below it the pump module (small display, status lights), the autosampler (window onto a tray of vials)
# and the detector (screen with a chromatogram peak), on low feet.
# Writes OUT_PNG and a 4x preview between the vanilla machines.
# usage: python draw_hplc.py OUT_PNG VANILLA_DIR PREVIEW_PNG
import os
import sys
from PIL import Image

OUT, VAN, PREVIEW = sys.argv[1], sys.argv[2], sys.argv[3]
W, H = 32, 48


def c(h):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


OUTLINE = c('292234')
P_HI, P_LT, P, P_SH = c('d5d2b8'), c('c4c4b0'), c('a8aea5'), c('88948a')     # pale instrument casing
M_HI, M_LT, M, M_SH, M_DK = c('a8aea5'), c('88948a'), c('5f6868'), c('44464c'), c('373541')
SCREEN, SCREEN_LT = c('2f2b3a'), c('373248')
TEAL_DK, TEAL, TEAL_LT = c('275654'), c('45918b'), c('67aaa5')
GREEN = c('8abfbb')
GLASS, GLASS_LT, GLASS_HI = c('67aaa5'), c('8abfbb'), c('c8e6e2')
AMBER, AMBER_LT = c('bb9165'), c('e0d09b')
BLUE, BLUE_LT = c('4f72c0'), c('80929b')
RED = c('c24a3d')

im = Image.new('RGBA', (W, H), (0, 0, 0, 0))


def paste(layer):
    """Put a part (dict pixel -> colour) over the image and outline it on its own."""
    px = im.load()
    mask = set(layer)
    for (x, y), col in layer.items():
        if 0 <= x < W and 0 <= y < H:
            px[x, y] = col
    for (x, y) in mask:
        for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + ox, y + oy)
            if n not in mask and 0 <= n[0] < W and 0 <= n[1] < H:
                px[n[0], n[1]] = OUTLINE


def block(x0, x1, y_top, top_h, front_h, top_cols, front_cols):
    """A box seen from the front and above: top face rows, then front face rows; corners clipped."""
    layer = {}
    hi, top = top_cols
    lt, mid, sh = front_cols
    bottom = y_top + top_h + front_h - 1
    for y in range(y_top, bottom + 1):
        for x in range(x0, x1 + 1):
            if x in (x0, x1) and y in (y_top, bottom):
                continue
            if y < y_top + top_h:
                col = hi if (y == y_top or x == x0) else top
            else:
                col = lt if x == x0 else sh if (x == x1 or y == bottom) else mid
            layer[(x, y)] = col
    return layer


def ellipse_in(x, y, cx, cy, rx, ry):
    return ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1.0


def bottle(cx, rx, y_top, height, side, top):
    """A reservoir bottle: elliptical shoulder, body, a dark cap on top."""
    lt, mid, sh = side
    layer = {}
    yb = y_top + height
    for y in range(int(y_top - 2), int(yb + 2)):
        for x in range(int(cx - rx - 1), int(cx + rx + 2)):
            body = (cx - rx <= x + 0.5 <= cx + rx) and y_top <= y + 0.5 <= yb
            if ellipse_in(x, y, cx, y_top, rx, 1.0):
                layer[(x, y)] = top
            elif body or ellipse_in(x, y, cx, yb, rx, 0.8):
                f = (x + 0.5 - (cx - rx)) / (2 * rx)
                layer[(x, y)] = lt if f < 0.34 else mid if f < 0.7 else sh
    # cap
    for x in range(int(cx - 1), int(cx + 1)):
        layer[(x, int(y_top) - 2)] = M_SH
        layer[(x, int(y_top) - 1)] = M_DK
    return layer


# ---- the instrument stack: one tall casing, its top face carries the solvent tray --------------------------------
stack = block(2, 29, 11, 4, 31, (P_HI, P_LT), (P_LT, P, P_SH))
paste(stack)
px = im.load()

# module seams across the front, each with a shadow line under it
for sy in (22, 33):
    for x in range(3, 29):
        px[x, sy] = OUTLINE
        px[x, sy + 1] = P_HI if x < 28 else P

# ---- pump module (rows 15-21): a small teal display and three status lights -------------------------------------
for y in range(16, 20):
    for x in range(5, 15):
        if x in (5, 14) or y in (16, 19):
            px[x, y] = OUTLINE
        else:
            px[x, y] = TEAL_LT if y == 17 else TEAL
px[7, 18] = TEAL_DK
px[8, 18] = TEAL_DK
px[10, 18] = TEAL_DK
px[12, 17] = TEAL_DK
for (lx, col) in ((19, GREEN), (22, GREEN), (25, AMBER)):
    px[lx, 17] = OUTLINE
    px[lx + 1, 17] = OUTLINE
    px[lx, 18] = col
    px[lx + 1, 18] = col
    px[lx, 19] = OUTLINE
    px[lx + 1, 19] = OUTLINE

# ---- autosampler (rows 24-32): a dark window onto a tray of vials with coloured caps ----------------------------
for y in range(25, 32):
    for x in range(5, 27):
        if x in (5, 26) or y in (25, 31):
            px[x, y] = OUTLINE
        else:
            px[x, y] = SCREEN_LT if y == 26 else SCREEN
for i, vx in enumerate(range(7, 25, 3)):
    cap = (RED, BLUE_LT, AMBER_LT, GREEN, RED, BLUE_LT)[i % 6]
    px[vx, 28] = cap
    px[vx + 1, 28] = cap
    px[vx, 29] = GLASS_LT
    px[vx + 1, 29] = GLASS
# window glare
px[6, 27] = GLASS_HI
px[7, 26] = GLASS_HI

# ---- detector (rows 35-44): a screen with a chromatogram, and a dial ---------------------------------------------
for y in range(36, 43):
    for x in range(5, 21):
        if x in (5, 20) or y in (36, 42):
            px[x, y] = OUTLINE
        else:
            px[x, y] = SCREEN
# baseline with two peaks, green on dark
trace = {6: 41, 7: 41, 8: 41, 9: 40, 10: 38, 11: 37, 12: 39, 13: 41, 14: 41, 15: 40, 16: 39, 17: 41, 18: 41, 19: 41}
for x, y in trace.items():
    px[x, y] = GREEN
# a round knob to the right of the screen
for (x, y), col in (((24, 37), OUTLINE), ((25, 37), OUTLINE), ((23, 38), OUTLINE), ((24, 38), M_HI), ((25, 38), M),
                    ((26, 38), OUTLINE), ((23, 39), OUTLINE), ((24, 39), M), ((25, 39), M_SH), ((26, 39), OUTLINE),
                    ((24, 40), OUTLINE), ((25, 40), OUTLINE)):
    px[x, y] = col
# a small vent under the knob
for x in range(23, 27):
    px[x, 42] = P_SH if x % 2 else OUTLINE

# ---- feet ---------------------------------------------------------------------------------------------------------
for fx in (4, 24):
    for x in range(fx, fx + 4):
        px[x, 46] = OUTLINE
        if x not in (fx, fx + 3):
            px[x, 45] = M_DK

# ---- solvent tray on the top face, with three reservoir bottles ------------------------------------------------
tray = block(4, 27, 9, 2, 2, (M_HI, M_LT), (M_LT, M, M_SH))
paste(tray)
bottles = [
    (8.5, 2.5, 3, 7, (GLASS_HI, GLASS_LT, GLASS), GLASS_HI),
    (15.5, 2.5, 2, 8, (AMBER_LT, AMBER, c('8f6a4a')), AMBER_LT),
    (22.5, 2.5, 3, 7, (BLUE_LT, BLUE, c('3a548f')), BLUE_LT),
]
for cx, rx, top, h, side, cap_col in bottles:
    paste(bottle(cx, rx, top, h, side, cap_col))
px = im.load()
# the solvent lines: from each cap, along the tray edge, into the stack's top
for (x0, x1) in ((8, 6), (15, 15), (22, 25)):
    for y in range(0, 2):
        px[x0, y] = M_DK
    step = 1 if x1 > x0 else -1
    for x in range(x0, x1 + step, step) if x1 != x0 else [x0]:
        px[x, 0] = M_DK
    for y in range(0, 9):
        if px[x1, y][3] == 0:
            px[x1, y] = M_DK

# ---- every 16x16 cell must count: fill a cell's empty pixels with alpha-1 ones if its centre is empty -------------
for cy in range(H // 16):
    for cx in range(W // 16):
        x, y = cx * 16 + 8, cy * 16 + 8
        if px[x, y][3] == 0:
            for yy in range(cy * 16, cy * 16 + 16):
                for xx in range(cx * 16, cx * 16 + 16):
                    if px[xx, yy][3] == 0:
                        px[xx, yy] = (0, 0, 0, 1)

im.save(OUT)
print(OUT, im.size)

names = ['chem_finisher', 'blender', None, 'heating_plate', 'evaporator']
tiles = [im if n is None else Image.open(os.path.join(VAN, n + '.png')).convert('RGBA') for n in names]
S = 4
sheet = Image.new('RGBA', (sum(t.size[0] for t in tiles) + 12 * (len(tiles) + 1), 48 + 24), (0x44, 0x44, 0x4c, 255))
x = 12
for t in tiles:
    sheet.alpha_composite(t, (x, 12))
    x += t.size[0] + 12
sheet.resize((sheet.size[0] * S, sheet.size[1] * S), Image.NEAREST).save(PREVIEW)
zoom = Image.new('RGBA', (W + 8, H + 8), (0x44, 0x44, 0x4c, 255))
zoom.alpha_composite(im, (4, 4))
zoom.resize((zoom.size[0] * 8, zoom.size[1] * 8), Image.NEAREST).save(PREVIEW.replace('.png', '_zoom.png'))
