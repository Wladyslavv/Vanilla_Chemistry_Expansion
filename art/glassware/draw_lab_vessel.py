# A 2x4 lab vessel (32x64): a tall graduated reagent bottle with a rubber stopper, a hazard label, a drain tap near
# the bottom and a metal foot. Vanilla glassware palette (exported 100ml / large / xl), outline 292234, light from the
# top left. Every 16x16 cell is filled with alpha-1 pixels so the whole 2x4 shape counts.
# Also writes a preview: the vessel next to vanilla's large bottle, plus a mock of liquid layering (sediment / water /
# oil) drawn inside its glass.
# usage: python draw_lab_vessel.py OUT_PNG PREVIEW_PNG [VANILLA_LARGE_PNG]
import sys
from PIL import Image

OUT, PREVIEW = sys.argv[1], sys.argv[2]
VANILLA = sys.argv[3] if len(sys.argv) > 3 else None
W, H = 32, 64


def c(h, a=255):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


OUTLINE = c('292234')
GLASS, GLASS_SH, GLASS_DK = c('80929b'), c('5f6868'), c('53585b')
GLASS_HI, GLASS_HI2, GLASS_HI3 = c('a8aea5'), c('c4c4b0'), c('d5d2b8')
TICK, TICK_DK = c('3e6979'), c('355271')
METAL, METAL_HI, METAL_DK, METAL_DK2 = c('5f6868'), c('a8aea5'), c('44464c'), c('392e3c')
RUBBER, RUBBER_HI, RUBBER_DK = c('79746e'), c('a8aea5'), c('44464c')
PAPER, PAPER_SH, INK = c('d5d2b8'), c('c4c4b0'), c('79746e')
HAZARD, HAZARD_DK = c('e0a040'), c('b06a28')

# Glass body: outline columns, the inside x 5..26, from the shoulder to the bottom.
BODY_L, BODY_R = 4, 27
BODY_TOP, BODY_BOT = 14, 55
NECK_L, NECK_R = 12, 19

im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
px = im.load()


def put(x, y, col):
    if 0 <= x < W and 0 <= y < H:
        px[x, y] = col


def glass_row(y, left, right):
    """One row of glass between outline columns left and right: highlight on the left, shade on the right."""
    put(left, y, OUTLINE)
    put(right, y, OUTLINE)
    for x in range(left + 1, right):
        d = x - left
        e = right - x
        if d == 1:
            col = GLASS_HI2
        elif d == 2:
            col = GLASS_HI
        elif e == 1:
            col = GLASS_DK
        elif e <= 3:
            col = GLASS_SH
        else:
            col = GLASS
        put(x, y, col)


# --- stopper (rows 1..7): rubber plug, elliptical top -------------------------------------------------------------
for x in range(NECK_L, NECK_R + 1):
    put(x, 1, OUTLINE)
for y in range(2, 8):
    put(NECK_L - 1 if y >= 3 else NECK_L, y, OUTLINE)
    put(NECK_R + 1 if y >= 3 else NECK_R, y, OUTLINE)
    lo = NECK_L if y >= 3 else NECK_L + 1
    hi = NECK_R if y >= 3 else NECK_R - 1
    for x in range(lo, hi + 1):
        if y == 2:
            col = RUBBER_HI
        elif x == lo:
            col = RUBBER_HI
        elif x >= hi - 1:
            col = RUBBER_DK
        else:
            col = RUBBER
        put(x, y, col)
put(NECK_L - 1, 2, (0, 0, 0, 0))
put(NECK_R + 1, 2, (0, 0, 0, 0))
for x in range(NECK_L - 1, NECK_R + 2):
    put(x, 8, OUTLINE)                          # lip of the stopper

# --- neck (rows 9..11) ---------------------------------------------------------------------------------------------
for y in range(9, 12):
    glass_row(y, NECK_L, NECK_R)

# --- shoulder (rows 12..13), widening to the body -------------------------------------------------------------------
glass_row(12, NECK_L - 4, NECK_R + 4)
for x in range(NECK_L - 4, NECK_L):
    put(x, 11, OUTLINE)
for x in range(NECK_R + 1, NECK_R + 5):
    put(x, 11, OUTLINE)
glass_row(13, BODY_L + 2, BODY_R - 2)
for x in range(BODY_L + 1, NECK_L - 3):
    put(x, 12, OUTLINE)
for x in range(NECK_R + 4, BODY_R):
    put(x, 12, OUTLINE)
put(BODY_L + 1, 13, OUTLINE)
put(BODY_R - 1, 13, OUTLINE)

# --- body (rows 14..54) --------------------------------------------------------------------------------------------
for y in range(BODY_TOP, BODY_BOT):
    glass_row(y, BODY_L, BODY_R)
# rounded bottom corners
glass_row(BODY_BOT, BODY_L + 1, BODY_R - 1)
put(BODY_L, BODY_BOT, (0, 0, 0, 0))
put(BODY_R, BODY_BOT, (0, 0, 0, 0))

# glass reflections: a bright streak down the left third, a faint one near the right edge, and a light rim where the
# shoulder meets the cylinder
for y in range(BODY_TOP + 1, BODY_BOT - 1):
    put(12, y, GLASS_HI)
    if (y - BODY_TOP) % 7 not in (0, 1):
        put(13, y, GLASS_HI2)
    put(BODY_R - 4, y, GLASS_HI if y % 5 else GLASS)
for x in range(BODY_L + 2, BODY_R - 1):
    put(x, BODY_TOP, GLASS_HI3 if BODY_L + 3 < x < BODY_R - 4 else GLASS_HI)

# graduations on the left: a short tick every 3 rows, a long one every 9
for i, y in enumerate(range(17, BODY_BOT - 2, 3)):
    long_tick = i % 3 == 0
    for x in range(7, 11 if long_tick else 9):
        put(x, y, TICK_DK if long_tick else TICK)

# --- label (x 13..24, y 25..38) with a hazard diamond and two lines of print -----------------------------------------
LX0, LX1, LY0, LY1 = 13, 24, 25, 38
for y in range(LY0, LY1 + 1):
    for x in range(LX0, LX1 + 1):
        edge = x in (LX0, LX1) or y in (LY0, LY1)
        put(x, y, OUTLINE if edge else (PAPER_SH if x == LX1 - 1 or y == LY1 - 1 else PAPER))
cx, cy = 18, 30
for dy in range(-3, 4):
    w = 3 - abs(dy)
    for dx in range(-w, w + 1):
        edge = abs(dx) == w
        put(cx + dx, cy + dy, HAZARD_DK if edge else HAZARD)
put(cx, cy - 1, OUTLINE)                      # exclamation mark
put(cx, cy, OUTLINE)
put(cx, cy + 2, OUTLINE)
for x in range(15, 23):
    put(x, 35, INK)
for x in range(15, 20):
    put(x, 36, INK)

# --- drain tap on the right (rows 46..52) ---------------------------------------------------------------------------
for y in range(46, 50):
    for x in range(BODY_R, BODY_R + 4):
        edge = x == BODY_R + 3 or y in (46, 49)
        put(x, y, OUTLINE if edge else (METAL_HI if y == 47 else METAL))
for y in range(43, 46):                       # handle
    put(BODY_R + 1, y, OUTLINE)
    put(BODY_R + 2, y, METAL_HI if y < 45 else METAL)
    put(BODY_R + 3, y, OUTLINE)
put(BODY_R + 2, 42, OUTLINE)
for y in range(50, 53):                       # spout
    put(BODY_R + 1, y, OUTLINE)
    put(BODY_R + 2, y, METAL_DK)
    put(BODY_R + 3, y, OUTLINE)
put(BODY_R + 2, 53, OUTLINE)

# --- metal foot (rows 56..62) --------------------------------------------------------------------------------------
FOOT_L, FOOT_R = 3, 28
for y in range(56, 63):
    inset = 0 if 57 <= y <= 61 else 1
    l, r = FOOT_L + inset, FOOT_R - inset
    for x in range(l, r + 1):
        edge = x in (l, r) or y in (56, 62)
        if edge:
            col = OUTLINE
        elif y == 57:
            col = METAL_HI
        elif y >= 60:
            col = METAL_DK2
        elif x <= l + 1:
            col = METAL_HI
        else:
            col = METAL if y == 58 else METAL_DK
        put(x, y, col)
for x in range(6, 26, 4):                      # rivets on the foot band
    put(x, 59, METAL_HI)

# Fill the empty pixels so every cell of the 2x4 shape counts.
for y in range(H):
    for x in range(W):
        if px[x, y][3] == 0:
            px[x, y] = (0, 0, 0, 1)
im.save(OUT)
print('vessel', im.size)


# --- preview ------------------------------------------------------------------------------------------------------
def layered(base):
    """Mock of liquid layering inside the glass: sediment, water, oil, from the bottom up."""
    out = base.copy()
    p = out.load()
    bands = [(51, 54, c('955c4b')), (32, 50, c('5d919c')), (27, 31, c('d8b04a'))]
    for y0, y1, col in bands:
        for y in range(y0, y1 + 1):
            for x in range(BODY_L + 1, BODY_R):
                r, g, b, a = p[x, y]
                if (r, g, b) in [GLASS[:3], GLASS_SH[:3], GLASS_DK[:3], GLASS_HI[:3], GLASS_HI2[:3]]:
                    k = 0.65
                    p[x, y] = (int(r * (1 - k) + col[0] * k), int(g * (1 - k) + col[1] * k), int(b * (1 - k) + col[2] * k), 255)
        for x in range(BODY_L + 1, BODY_R):   # meniscus line on top of each band
            r, g, b, a = p[x, y0]
            if (r, g, b) not in [OUTLINE[:3], PAPER[:3], PAPER_SH[:3], INK[:3]] and a > 200:
                p[x, y0] = (min(255, r + 40), min(255, g + 40), min(255, b + 40), 255)
    return out


S = 4
pieces = [im, layered(im)]
if VANILLA:
    pieces.insert(0, Image.open(VANILLA).convert('RGBA'))
sheet = Image.new('RGBA', (sum(p.size[0] for p in pieces) * S + 12 * (len(pieces) + 1), H * S + 24), (0x44, 0x44, 0x4c, 255))
x = 12
for p in pieces:
    q = p.copy()
    qp = q.load()
    for yy in range(q.size[1]):
        for xx in range(q.size[0]):
            if qp[xx, yy][3] <= 3:
                qp[xx, yy] = (0, 0, 0, 0)
    big = q.resize((q.size[0] * S, q.size[1] * S), Image.NEAREST)
    sheet.alpha_composite(big, (x, 12))
    x += big.size[0] + 12
sheet.save(PREVIEW)
print('preview', sheet.size)
