# The flow hose (导流管): a coiled hose, 2x2 cells (32x32), seen from slightly above like vanilla's coil of wire
# ("wire"): three stacked elliptical turns (ellipses half as tall as wide), dark gaps between the turns, a metal
# coupling at each end, and an inline ball valve on the back of the top turn: a short metal sleeve exactly as thick
# as the hose there, with a red lever on top. Light from the top left, outline 292234.
# --half draws half a hose: two turns instead of three and only the front coupling (the back-left end is gone).
# usage: python draw_hose.py [--half] OUT_PNG PREVIEW_PNG [VANILLA_PNG ...]
import math
import sys
from PIL import Image

W, H = 32, 32
OUTLINE = (0x29, 0x22, 0x34, 255)
# hose rubber, dark to light
HOSE = [(0x2f, 0x3a, 0x48), (0x3d, 0x4f, 0x61), (0x52, 0x6a, 0x7d), (0x71, 0x8e, 0x9e), (0x9c, 0xb7, 0xc0)]
# metal (vanilla blue-greys)
METAL = [(0x37, 0x35, 0x41), (0x5f, 0x68, 0x68), (0x88, 0x94, 0x8a), (0xa8, 0xae, 0xa5), (0xc5, 0xc8, 0xbd)]
# red lever
RED = [(0x64, 0x34, 0x3b), (0x9a, 0x33, 0x33), (0xc8, 0x45, 0x3d), (0xe8, 0x74, 0x5c)]

CX = 15.5           # coil centre x (pixel centres at x + 0.5)
A_OUT, A_IN = 13.0, 6.2     # outer / inner half widths of a turn
TILT = 0.5          # ellipses half as tall as wide
TURN_STEP = 3       # pixels each turn sits above the one below
BASE_Y = 21.5       # centre row of the bottom turn
TURNS = 3
HALF = False


def blank():
    return Image.new("RGBA", (W, H), (0, 0, 0, 0))


def ring_shade(x, y, cy):
    """Inside the ring of a turn centred on (CX, cy)? Return a shade index or None."""
    dx = (x + 0.5 - CX)
    dy = (y + 0.5 - cy) / TILT
    r = math.hypot(dx, dy)
    if r > A_OUT or r < A_IN:
        return None
    # across the tube: 0 at the inner edge, 1 at the outer edge
    t = (r - A_IN) / (A_OUT - A_IN)
    # the tube is round: lit on its upper side; which side is "upper" depends on where on the ring we are
    ang = math.atan2(dy, dx)            # dy > 0 is the front (lower on screen)
    front = math.sin(ang)               # +1 front, -1 back
    # on the front the outer edge faces down (dark), on the back the outer edge faces up (lit)
    up = (1 - t) if front > 0 else t
    light = 0.55 * up + 0.25 * (1 - abs(t - 0.5) * 2) - 0.25 * max(0.0, math.cos(ang))   # right side a bit darker
    light += 0.15 * (-math.cos(ang) * 0.5)  # left side a touch lighter (light from the top left)
    if light > 0.62:
        return 4
    if light > 0.45:
        return 3
    if light > 0.28:
        return 2
    if light > 0.12:
        return 1
    return 0


def draw():
    img = blank()
    px = img.load()
    # turns bottom to top; each later turn covers the earlier one where it overlaps
    for k in range(TURNS):
        cy = BASE_Y - k * TURN_STEP
        for y in range(H):
            for x in range(W):
                s = ring_shade(x, y, cy)
                if s is not None:
                    px[x, y] = HOSE[s] + (255,)
        # a dark seam along the front lower edge of every turn but the bottom one, so the turns read apart
        if k > 0:
            for x in range(W):
                for y in range(H - 1, -1, -1):
                    if px[x, y][3] and ring_shade(x, y, cy) is not None and (x + 0.5 - CX) ** 2 / A_OUT ** 2 < 0.97:
                        # lowest pixel of this turn in this column, on the front half
                        if y + 0.5 > cy:
                            px[x, y] = HOSE[0] + (255,)
                        break
    # the hole in the middle shows the dark floor of the coil
    for y in range(H):
        for x in range(W):
            dx = x + 0.5 - CX
            dy = (y + 0.5 - (BASE_Y - (TURNS - 1) * TURN_STEP)) / TILT
            if math.hypot(dx, dy) < A_IN and px[x, y][3] == 0:
                px[x, y] = (0x22, 0x26, 0x30, 255)

    # couplings: the outer end leaves the bottom turn at the front right, the inner end the top turn at the back left
    def box(x0, y0, w, h, ramp):
        for y in range(y0, y0 + h):
            for x in range(x0, x0 + w):
                i = 3 if y == y0 else (1 if y == y0 + h - 1 else 2)
                if x == x0 + w - 1:
                    i = max(0, i - 1)
                px[x, y] = ramp[i] + (255,)
    # front right end: hose stub then a metal coupling
    for x in range(26, 29):
        for y in range(24, 27):
            px[x, y] = HOSE[2 if y == 24 else 1] + (255,)
    box(29, 23, 3, 5, METAL)
    # back left end: metal coupling sticking out to the left (a half hose has none: that end is the other half)
    if not HALF:
        box(0, 14, 3, 4, METAL)
        for x in range(3, 5):
            for y in range(14, 17):
                px[x, y] = HOSE[3 if y == 14 else 2] + (255,)

    valve(px)
    outline(img)
    return img


def valve(px):
    # An inline valve on the back of the top turn: a metal sleeve over exactly the rows the hose fills in each column
    # there, so it is as thick as the hose and its ends meet the rubber squarely; a spindle and the red lever on top.
    top_cy = BASE_Y - (TURNS - 1) * TURN_STEP
    x0, x1 = 13, 18                      # sleeve columns (inclusive), centred on the coil's axis
    lo_top = None
    for x in range(x0, x1 + 1):
        rows = [y for y in range(H) if y + 0.5 < top_cy and ring_shade(x, y, top_cy) is not None]
        if not rows:
            continue
        y_top, y_bot = min(rows), max(rows)
        lo_top = y_top if lo_top is None else min(lo_top, y_top)
        for y in range(y_top, y_bot + 1):
            if y == y_top:
                i = 4 if x < x1 - 1 else 3                  # lit top edge
            elif y == y_bot:
                i = 1                                       # shaded underside
            else:
                i = 3 if x < x1 - 1 else 2
            if x in (x0, x1):                               # the sleeve's two collars, a shade darker
                i = max(1, i - 1)
            px[x, y] = METAL[i] + (255,)
    # dark joints where the sleeve meets the rubber on both sides
    for x in (x0 - 1, x1 + 1):
        for y in range(H):
            if y + 0.5 < top_cy and ring_shade(x, y, top_cy) is not None:
                px[x, y] = OUTLINE
    if lo_top is None:
        return
    # spindle, then the red lever: a two-pixel bar from the spindle up and to the right, ending in a grip
    sx = (x0 + x1) // 2
    px[sx, lo_top - 1] = METAL[2] + (255,)
    lever = [(sx - 2, lo_top - 2), (sx - 1, lo_top - 2), (sx, lo_top - 2), (sx + 1, lo_top - 2), (sx + 2, lo_top - 3),
             (sx + 3, lo_top - 3), (sx + 4, lo_top - 4), (sx + 5, lo_top - 4), (sx + 6, lo_top - 4)]
    for i, (x, y) in enumerate(lever):
        px[x, y] = RED[3 if i < 3 else 2] + (255,)
        if (x, y + 1) not in lever and not (x == sx and y + 1 == lo_top - 1):
            px[x, y + 1] = RED[1] + (255,)
    for (x, y), c in (((sx + 6, lo_top - 5), RED[3]), ((sx + 7, lo_top - 5), RED[2]), ((sx + 7, lo_top - 4), RED[2]),
                      ((sx + 7, lo_top - 3), RED[1]), ((sx + 6, lo_top - 3), RED[1])):
        px[x, y] = c + (255,)


def outline(img):
    px = img.load()
    solid = [[px[x, y][3] > 0 for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if solid[y][x]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < W and 0 <= ny < H and solid[ny][nx]:
                    px[x, y] = OUTLINE
                    break
    # outline pixels must stay inside the image: shapes touching the edge get their edge pixels outlined instead
    for y in range(H):
        for x in (0, W - 1):
            if solid[y][x]:
                px[x, y] = OUTLINE
    for x in range(W):
        for y in (0, H - 1):
            if solid[y][x]:
                px[x, y] = OUTLINE


def mark_cells(img):
    # every 16x16 cell must count for the grid: the game reads (16c+8, 16r+7)
    px = img.load()
    for r in range(H // 16):
        for c in range(W // 16):
            x, y = 16 * c + 8, 16 * r + 7
            if px[x, y][3] == 0:
                px[x, y] = (0x29, 0x22, 0x34, 1)


def main():
    global HALF, TURNS
    args = sys.argv[1:]
    if args and args[0] == "--half":
        HALF, TURNS = True, 2
        args = args[1:]
    out, preview = args[0], args[1]
    img = draw()
    mark_cells(img)
    img.save(out)
    refs = [Image.open(p).convert("RGBA") for p in args[2:]]
    S = 8
    tiles = [img] + refs
    sheet = Image.new("RGBA", (sum(t.width * S + 16 for t in tiles) + 8, max(t.height for t in tiles) * S + 16), (0x3a, 0x3f, 0x4a, 255))
    x = 8
    for t in tiles:
        sheet.alpha_composite(t.resize((t.width * S, t.height * S), Image.NEAREST), (x, 8))
        x += t.width * S + 16
    sheet.save(preview)


if __name__ == "__main__":
    main()
