# High-performance liquid chromatograph, large (48x64, 3 cells wide x 4 tall), in the vanilla machines' three-quarter
# view: a lighter top face over each front face, round parts as cylinders with elliptical tops, light from the top left,
# outline 292234. Pale instrument casing with the vanilla screens, teal trim and status lights.
# From the top: a solvent tray with four reservoir bottles whose lines run down the left side into the pump; the
# pump module (display, pressure readout, two purge valves, status lights); the autosampler (window onto two rows of
# vials, the needle arm over one of them); the column compartment (glass door onto the column, temperature display);
# the detector (chromatogram screen, lamp light, vent); a capillary loop on the right side joining the modules; feet.
# Writes OUT_PNG, a 4x preview between the vanilla machines and an 8x close-up.
# usage: python draw_hplc_large.py OUT_PNG VANILLA_DIR PREVIEW_PNG
import os
import sys
from PIL import Image

OUT, VAN, PREVIEW = sys.argv[1], sys.argv[2], sys.argv[3]
W, H = 48, 64


def c(h):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


OUTLINE = c('292234')
P_HI, P_LT, P, P_SH, P_DK = c('d5d2b8'), c('c4c4b0'), c('a8aea5'), c('88948a'), c('79746e')
M_HI, M_LT, M, M_SH, M_DK = c('a8aea5'), c('88948a'), c('5f6868'), c('44464c'), c('373541')
SCREEN, SCREEN_LT = c('2f2b3a'), c('373248')
TEAL_DK, TEAL, TEAL_LT = c('275654'), c('45918b'), c('67aaa5')
GREEN = c('8abfbb')
GLASS, GLASS_LT, GLASS_HI = c('67aaa5'), c('8abfbb'), c('c8e6e2')
AMBER, AMBER_LT, AMBER_DK = c('bb9165'), c('e0d09b'), c('8f6a4a')
BLUE, BLUE_LT, BLUE_DK = c('4f72c0'), c('80929b'), c('3a548f')
CLEAR, CLEAR_LT = c('c4c4b0'), c('e9e6dc')
RED, RED_DK = c('c24a3d'), c('9b4a3d')
TUBE = c('e9e6dc')

im = Image.new('RGBA', (W, H), (0, 0, 0, 0))


def px_():
    return im.load()


def paste(layer):
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


def bottle(cx, rx, y_top, height, side, top, fill_from):
    """Reservoir bottle: glass shoulder, liquid below fill_from, cap with a line out of it."""
    lt, mid, sh = side
    layer = {}
    yb = y_top + height
    for y in range(int(y_top - 2), int(yb + 2)):
        for x in range(int(cx - rx - 1), int(cx + rx + 2)):
            body = (cx - rx <= x + 0.5 <= cx + rx) and y_top <= y + 0.5 <= yb
            if ellipse_in(x, y, cx, y_top, rx, 1.0):
                layer[(x, y)] = GLASS_HI
            elif body or ellipse_in(x, y, cx, yb, rx, 0.8):
                f = (x + 0.5 - (cx - rx)) / (2 * rx)
                if y < fill_from:
                    layer[(x, y)] = GLASS_HI if f < 0.34 else GLASS_LT
                else:
                    layer[(x, y)] = lt if f < 0.34 else mid if f < 0.7 else sh
    for x in range(int(cx - 1), int(cx + 1)):
        layer[(x, int(y_top) - 2)] = M_SH
        layer[(x, int(y_top) - 1)] = M_DK
    return layer


def frame(x0, y0, x1, y1, fill, top_fill=None):
    """A recessed panel: outline, a darker top row (its shadow), fill."""
    px = im.load()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if x in (x0, x1) or y in (y0, y1):
                px[x, y] = OUTLINE
            elif y == y0 + 1 and top_fill is not None:
                px[x, y] = top_fill
            else:
                px[x, y] = fill


def led(x, y, col):
    px = im.load()
    for dx in (0, 1):
        px[x + dx, y - 1] = OUTLINE
        px[x + dx, y] = col
        px[x + dx, y + 1] = OUTLINE


def knob(x, y):
    """A 4x4 round valve knob, lit from the top left."""
    px = im.load()
    pattern = [".##.", "#hm#", "#ms#", ".##."]
    cols = {'#': OUTLINE, 'h': M_HI, 'm': M, 's': M_SH}
    for dy, row in enumerate(pattern):
        for dx, ch in enumerate(row):
            if ch != '.':
                px[x + dx, y + dy] = cols[ch]


# ---- casing ---------------------------------------------------------------------------------------------------------
stack = block(3, 44, 15, 4, 43, (P_HI, P_LT), (P_LT, P, P_SH))
paste(stack)
px = im.load()

# module seams: outline, then a lit lip under it
for sy in (30, 42, 51):
    for x in range(4, 44):
        px[x, sy] = OUTLINE
        px[x, sy + 1] = P_HI if x < 43 else P

# a label strip down the left of every module (brand bar)
for (y0, y1) in ((20, 29), (32, 41), (44, 50), (53, 60)):
    for y in range(y0, y1 + 1):
        px[5, y] = TEAL_DK if y == y0 else TEAL

# ---- pump module (rows 19-29) ---------------------------------------------------------------------------------------
frame(8, 21, 22, 27, TEAL, TEAL_LT)
for x, y in ((10, 24), (11, 24), (12, 24), (14, 24), (15, 24), (17, 24), (18, 24), (19, 24), (10, 25), (11, 25)):
    px[x, y] = TEAL_DK
# pressure readout: a small amber number field
frame(24, 21, 31, 25, SCREEN, SCREEN_LT)
for x in (26, 27, 29):
    px[x, 23] = AMBER_LT
# purge valves
knob(34, 21)
knob(39, 21)
# status lights
for (lx, col) in ((25, GREEN), (28, GREEN), (31, AMBER)):
    led(lx, 28, col)
# power button
for (x, y), col in (((36, 27), OUTLINE), ((37, 27), OUTLINE), ((36, 28), TEAL_LT), ((37, 28), TEAL), ((36, 29), OUTLINE),
                    ((37, 29), OUTLINE)):
    px[x, y] = col

# ---- autosampler (rows 32-41): a window onto two rows of vials, the needle over one -----------------------------
frame(8, 32, 35, 41, SCREEN, SCREEN_LT)
caps = (RED, BLUE_LT, AMBER_LT, GREEN, RED, BLUE_LT, AMBER_LT, GREEN)
for i, vx in enumerate(range(10, 34, 3)):       # back row: smaller, dimmer
    px[vx, 35] = M_SH
    px[vx, 36] = SCREEN_LT
for i, vx in enumerate(range(9, 34, 3)):        # front row
    px[vx, 37] = caps[i % len(caps)]
    px[vx + 1, 37] = caps[i % len(caps)]
    px[vx, 38] = GLASS_LT
    px[vx + 1, 38] = GLASS
    px[vx, 39] = GLASS
    px[vx + 1, 39] = TEAL_DK
# needle arm along the top of the window, the needle down onto the fourth vial
for x in range(10, 24):
    px[x, 34] = M_LT
px[18, 35] = M_HI
px[18, 36] = M_HI
# glare on the glass
px[9, 34] = GLASS_HI
px[10, 33] = GLASS_HI
# side panel: tray light and a button
led(38, 34, GREEN)
for (x, y), col in (((38, 37), OUTLINE), ((39, 37), OUTLINE), ((40, 37), OUTLINE), ((38, 38), P_HI), ((39, 38), P_LT),
                    ((40, 38), P_SH), ((38, 39), OUTLINE), ((39, 39), OUTLINE), ((40, 39), OUTLINE)):
    px[x, y] = col

# ---- column compartment (rows 44-50): glass door onto the column, temperature display --------------------------
frame(8, 43, 30, 50, SCREEN, SCREEN_LT)
for x in range(11, 28):                          # the column: a steel tube with end fittings
    px[x, 46] = M_HI
    px[x, 47] = M_SH
for (fx) in (10, 28):
    px[fx, 46] = AMBER_LT
    px[fx, 47] = AMBER
px[9, 47] = TUBE                                 # capillaries out of both ends
px[29, 47] = TUBE
px[9, 44] = GLASS_HI
frame(33, 44, 41, 48, SCREEN, SCREEN_LT)        # temperature: "40" in red digits
for x in (35, 36, 38, 39):
    px[x, 46] = RED

# ---- detector (rows 53-60): chromatogram screen, lamp light, vent -------------------------------------------------
frame(8, 53, 30, 60, SCREEN, SCREEN_LT)
trace = {9: 59, 10: 59, 11: 59, 12: 58, 13: 56, 14: 55, 15: 57, 16: 59, 17: 59, 18: 59, 19: 58, 20: 57, 21: 58,
         22: 59, 23: 58, 24: 55, 25: 54, 26: 56, 27: 59, 28: 59, 29: 59}
for x, y in trace.items():
    px[x, y] = GREEN
for x in range(9, 30, 4):                        # grid ticks along the bottom
    px[x, 59] = TEAL_DK if px[x, 59] != GREEN else GREEN
led(34, 55, AMBER)                               # lamp
for y in (58, 60):                               # vent
    for x in range(33, 42):
        px[x, y] = OUTLINE if x % 2 else P_DK

# ---- tubing: 2 px thick, lit side and shaded side, own outline, brass fittings where it goes in ------------------
TUBE_SH = P_SH
BRASS, BRASS_LT, BRASS_DK = AMBER, AMBER_LT, AMBER_DK


def tube(points):
    """A 2 px tube along axis-aligned points [(x, y), ...]; (x, y) is the tube's top-left pixel at each point.
    Horizontal runs: light top row, shaded bottom row. Vertical runs: light left column, shaded right column."""
    layer = {}
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        if y0 == y1:
            for x in range(min(x0, x1), max(x0, x1) + 2):
                layer[(x, y0)] = TUBE
                layer[(x, y0 + 1)] = TUBE_SH
        else:
            for y in range(min(y0, y1), max(y0, y1) + 2):
                layer[(x0, y)] = TUBE
                layer[(x0 + 1, y)] = TUBE_SH
    # round the outer corner of every bend
    for (xa, ya), (xb, yb), (xc, yc) in zip(points, points[1:], points[2:]):
        dx = 1 if (xa < xb or xc > xb) else 0
        dy = 1 if (ya < yb or yc > yb) else 0
        corner = (xb + (0 if xa < xb or xc < xb else 1), yb + (0 if ya < yb or yc < yb else 1))
        if (xa != xb) != (xb != xc) and corner in layer:
            del layer[corner]
    return layer


def fitting(x, y):
    """A 2x3 brass nut where a tube meets a module, lit from the top."""
    px = im.load()
    for dy, col in enumerate((BRASS_LT, BRASS, BRASS_DK)):
        px[x, y - 1 + dy] = col
        px[x + 1, y - 1 + dy] = col
    for dy in range(-2, 3):
        px[x - 1, y + dy] = OUTLINE if px[x - 1, y + dy][3] == 0 or dy in (-2, 2) else px[x - 1, y + dy]
    px[x, y - 2] = OUTLINE
    px[x + 1, y - 2] = OUTLINE
    px[x, y + 2] = OUTLINE
    px[x + 1, y + 2] = OUTLINE


def clip(x, y):
    """A small clip holding a vertical tube to the casing."""
    px = im.load()
    px[x, y] = M_SH
    px[x + 1, y] = M_DK
    px[x - 1, y] = OUTLINE
    px[x + 2, y] = OUTLINE


# capillary on the right side: pump -> autosampler -> column -> detector
right = tube([(42, 27), (45, 27), (45, 55), (42, 55)])
for y in (38, 48):
    right.update(tube([(42, y), (45, y)]))
paste(right)   # one outline round the whole run, so the joins stay clean
px = im.load()
for y in (28, 39, 49, 56):
    fitting(42, y)

# ---- feet -----------------------------------------------------------------------------------------------------------
for fx in (6, 38):
    for x in range(fx, fx + 5):
        px[x, 63] = OUTLINE
        if x not in (fx, fx + 4):
            px[x, 62] = M_DK

# ---- solvent tray and four reservoir bottles -------------------------------------------------------------------
tray = block(6, 41, 12, 2, 3, (M_HI, M_LT), (M_LT, M, M_SH))
paste(tray)
bottles = [
    (11.5, 3.0, 6, 6, (CLEAR_LT, CLEAR, P_SH), 8),
    (19.5, 3.0, 5, 7, (AMBER_LT, AMBER, AMBER_DK), 7),
    (27.5, 3.0, 6, 6, (GLASS_LT, GLASS, TEAL_DK), 9),
    (35.5, 3.0, 5, 7, (BLUE_LT, BLUE, BLUE_DK), 8),
]
# solvent lines first, so the bottles' caps sit over their ends: one bundle along the top, a drop into each cap,
# then down the left side and into the pump through a fitting
solvent = tube([(1, 28), (1, 1), (35, 1)])
solvent.update(tube([(1, 28), (6, 28)]))
for (cx, rx, top, h, side, fill_from) in bottles:
    drop = tube([(int(cx) - 1, 2), (int(cx) - 1, int(top) - 3)])
    for k, v in drop.items():
        solvent.setdefault(k, v)
paste(solvent)   # one outline round the whole bundle
for cx, rx, top, h, side, fill_from in bottles:
    paste(bottle(cx, rx, top, h, side, None, fill_from))
px = im.load()
fitting(7, 29)
for y in (8, 16):
    clip(1, y)

# ---- every 16x16 cell counts --------------------------------------------------------------------------------------
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
sheet = Image.new('RGBA', (sum(t.size[0] for t in tiles) + 12 * (len(tiles) + 1), 64 + 24), (0x44, 0x44, 0x4c, 255))
x = 12
for t in tiles:
    sheet.alpha_composite(t, (x, 12 + 64 - t.size[1]))
    x += t.size[0] + 12
sheet.resize((sheet.size[0] * S, sheet.size[1] * S), Image.NEAREST).save(PREVIEW)
zoom = Image.new('RGBA', (W + 8, H + 8), (0x44, 0x44, 0x4c, 255))
zoom.alpha_composite(im, (4, 4))
zoom.resize((zoom.size[0] * 8, zoom.size[1] * 8), Image.NEAREST).save(PREVIEW.replace('.png', '_zoom.png'))
