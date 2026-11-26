# A separatory funnel on a retort stand, 2x4 cells (32x64), in the vanilla glassware palette, seen like vanilla items:
# straight from the front but from slightly above, so round things show their top as an ellipse about a third as tall
# as it is wide (the heating plate, the blender) and boxes show a lit strip of top face (the fridge).
#   ground-glass stopper | pear-shaped bulb held by a ring clamp (back half seen through the glass) | PTFE stopcock |
#   stem dripping into a beaker | stand rod on a cast-iron base plate.
# Glass is drawn half transparent so the back of the ring, the stem in the beaker and the liquids read as "inside".
# SUPERSEDED: Textures/Items/vce_sep_funnel.png has been a hand-drawn sprite since PR #7 (after 1.11.0), and
# SepFunnel.cs now takes the glass row by row from that picture (GlassFrom / GlassTo). Do not write this script's
# output over the texture; it is kept for reference only.
# The liquid in the funnel is drawn by the game (SepFunnel.BuildSprite); liquid_layer() here was the same algorithm,
# for the preview, with the old bulb radius and ring.
# Writes the empty funnel, a two-layer version and a 4x preview with some fill levels next to vanilla glass.
# usage: python draw_sep_funnel.py OUT_DIR PREVIEW_PNG [VANILLA_PNG ...]
import math
import os
import sys
from PIL import Image

OUT_DIR, PREVIEW = sys.argv[1], sys.argv[2]
VANILLA = sys.argv[3:]
W, H = 32, 64
TILT = 0.3                                      # ellipse height / width for anything round seen from above


def c(h, a=255):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


OUTLINE = c('292234')
GLASS_A = 150                                   # glass body alpha
GLASS, GLASS_SH = c('80929b', GLASS_A), c('5f6868', 200)
RIM, HI, SPEC = c('a8aea5'), c('c4c4b0'), c('e9e6dc')
TICK = c('3e6979')
M_HI, M, M_SH, M_DK = c('a8aea5'), c('79746e'), c('5f6868'), c('44464c')
IRON_HI, IRON_TOP, IRON_FRONT, IRON_DK = c('79746e'), c('5f6868'), c('44464c'), c('392e3c')
PTFE, PTFE_SH = c('e9e6dc'), c('c4c4b0')
AQ, ORG = c('4f8fa6'), c('e0a83a')

CX = 19.5                                       # funnel axis


def blank():
    return Image.new('RGBA', (W, H), (0, 0, 0, 0))


def over(dst, x, y, col):
    """Alpha-composite one pixel."""
    if not (0 <= x < W and 0 <= y < H):
        return
    r0, g0, b0, a0 = dst[x, y]
    r1, g1, b1, a1 = col
    a = a1 / 255.0
    na = a1 + a0 * (1 - a)
    if na <= 0:
        return
    k = a1 / na
    dst[x, y] = (int(r1 * k + r0 * (1 - k)), int(g1 * k + g0 * (1 - k)), int(b1 * k + b0 * (1 - k)), int(min(255, na)))


def outline_mask(mask):
    """Pixels of the mask that touch a pixel outside it (4-neighbourhood)."""
    edge = set()
    for (x, y) in mask:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in mask:
                edge.add((x, y))
                break
    return edge


def rows_mask(rows):
    mask = set()
    for y, (l, r) in rows.items():
        for x in range(l, r + 1):
            mask.add((x, y))
    return mask


def inside(x, y, cx, cy, a, b):
    return ((x + 0.5 - cx) / a) ** 2 + ((y + 0.5 - cy) / b) ** 2 <= 1.0


def filled_ellipse(cx, cy, a, b):
    pts = set()
    for y in range(int(cy - b) - 1, int(cy + b) + 2):
        for x in range(int(cx - a) - 1, int(cx + a) + 2):
            if inside(x, y, cx, cy, a, b):
                pts.add((x, y))
    return pts


def ellipse_ring(cx, cy, a, b):
    """The one-pixel outline of a filled ellipse."""
    return outline_mask(filled_ellipse(cx, cy, a, b))


# ---- funnel silhouette: radius per row ---------------------------------------------------------------------------
def radius(y):
    if y < 8:
        return None
    if y <= 9:
        return 2.5                                          # neck
    if y <= 14:
        return 2.5 + 7.5 * math.sin((y - 9) / 5.0 * math.pi / 2)  # round shoulder
    if y <= 22:
        return 10.0                                         # widest (kept in step with SepFunnel.Radius)
    if y <= 36:
        return 10.0 - 8.5 * (y - 22) / 14.0                 # cone
    if y <= 50:
        return 1.5                                          # stem
    return None


FUNNEL = {}
for y in range(H):
    r = radius(y)
    if r is not None:
        FUNNEL[y] = (int(round(CX - r)), int(round(CX + r - 1)))
FUNNEL_MASK = rows_mask(FUNNEL)
FUNNEL_EDGE = outline_mask(FUNNEL_MASK)
STOPCOCK_Y = (39, 42)
LIQ_TOP, LIQ_BOT = 10, 36                                   # rows that can show liquid (SepFunnel.LIQ_TOP/BOT)


def glass_shade(x, y, l, r):
    w = max(1, r - l)
    t = (x - l) / w
    if t <= 0.12:
        return RIM
    if 0.22 <= t <= 0.34 and 11 <= y <= 30:
        return HI
    if t >= 0.80:
        return GLASS_SH
    return GLASS


RING_CX, RING_CY, RING_A, RING_B = CX, 28.0, 8.0, 2.6   # the ring holds the cone, below the widest part


def draw_stand_and_base(px):
    # cast-iron base plate: a lit strip of top face over a darker front face, corners rounded
    top = {53: (2, 29), 54: (1, 30), 55: (1, 30), 56: (1, 30), 57: (1, 30), 58: (1, 30)}
    front = {59: (1, 30), 60: (1, 30), 61: (1, 30), 62: (2, 29)}
    plate = rows_mask(top) | rows_mask(front)
    edge = outline_mask(plate)
    for (x, y) in plate:
        if (x, y) in edge and not (y == 53 and BK_L < x < BK_R):  # its far edge is hidden behind the beaker
            col = OUTLINE
        elif y < 59:
            col = IRON_HI if y == 54 else IRON_TOP
        else:
            col = IRON_FRONT if y < 61 else IRON_DK
        px[x, y] = col
    for x in range(1, 31):                                    # the edge between top and front faces
        if (x, 59) not in edge:
            px[x, 59] = IRON_DK
    # rod foot: a round boss on the plate, its top an ellipse
    boss = filled_ellipse(3.5, 55.0, 2.6, 1.2)
    bedge = outline_mask(boss)
    for (x, y) in boss:
        px[x, y] = OUTLINE if (x, y) in bedge else (M if y < 55 else M_SH)
    # rod: from the boss up to the top, a round cap seen from above
    for y in range(1, 55):
        px[2, y] = OUTLINE
        px[3, y] = M_HI if y % 9 else M
        px[4, y] = M_SH
        px[5, y] = OUTLINE
    px[2, 0] = px[5, 0] = (0, 0, 0, 0)
    px[3, 0] = OUTLINE
    px[4, 0] = OUTLINE
    px[3, 1] = SPEC
    px[4, 1] = M_HI


def draw_ring(px, front):
    pts = ellipse_ring(RING_CX, RING_CY, RING_A, RING_B)
    for (x, y) in pts:
        is_front = y + 0.5 >= RING_CY
        if is_front != front:
            continue
        if front:
            col = M_HI if x < RING_CX - 2 else (M_SH if x > RING_CX + 3 else M)
            over(px, x, y, col)
            if (x, y + 1) not in pts:
                over(px, x, y + 1, OUTLINE)
        else:
            over(px, x, y, c('5f6868', 200))
    if front:
        # arm from the rod to the ring, with the clamp boss on the rod and a thumbscrew
        ay = int(RING_CY) - 1
        for x in range(5, int(RING_CX - RING_A) + 1):
            over(px, x, ay - 1, OUTLINE)
            over(px, x, ay, M_HI)
            over(px, x, ay + 1, M_SH)
            over(px, x, ay + 2, OUTLINE)
        for y in range(ay - 3, ay + 5):
            for x in range(1, 7):
                edge = x in (1, 6) or y in (ay - 3, ay + 4)
                over(px, x, y, OUTLINE if edge else (M_HI if y == ay - 2 else (M if x < 5 else M_SH)))
        for y in range(ay - 1, ay + 3):
            over(px, 0, y, OUTLINE)


def on_front_ring(x, y):
    """SepFunnel.InFrontRing: the front half of the ring and the outline under it, drawn over the liquid."""
    pts = ellipse_ring(RING_CX, RING_CY, RING_A, RING_B)
    front = {(px_, py) for (px_, py) in pts if py + 0.5 >= RING_CY}
    return (x, y) in front or (x, y - 1) in front


def draw_funnel_fill(px):
    """The glass body tint."""
    for (x, y) in FUNNEL_MASK:
        l, r = FUNNEL[y]
        if (x, y) in FUNNEL_EDGE:
            continue
        col = glass_shade(x, y, l, r)
        if col in (GLASS, GLASS_SH):
            over(px, x, y, col)


def draw_funnel(px):
    """Reflections and outline."""
    for (x, y) in FUNNEL_MASK:
        l, r = FUNNEL[y]
        if (x, y) in FUNNEL_EDGE:
            continue
        col = glass_shade(x, y, l, r)
        if col == RIM:
            over(px, x, y, c('a8aea5', 200))
        elif col == HI:
            over(px, x, y, c('c4c4b0', 150))
    # specular glint on the shoulder, following its curve
    for (x, y) in ((14, 11), (13, 12), (12, 13), (12, 14)):
        px[x, y] = SPEC
    # graduation marks on the cone: short arcs that bow down like the rest of the round glass
    for y in (30, 33):
        l, r = FUNNEL[y]
        px[r - 3, y] = TICK
        px[r - 2, y - 1] = TICK
    for (x, y) in FUNNEL_EDGE:
        if STOPCOCK_Y[0] <= y <= STOPCOCK_Y[1]:
            continue
        if y == 8 and FUNNEL[8][0] < x < FUNNEL[8][1]:
            continue                                          # the neck opens into the lip above
        px[x, y] = OUTLINE


STOPPER = [                                                  # columns 16..22, rows 0..7; too small to draw by formula
    ' AAAAA ',
    'ABBBCCA',                                               # ground-glass stopper: a flat round knob, top face lit,
    'ADDDEEA',                                               #   its rim below,
    ' AAAAA ',
    ' ADDEA ',                                               #   the plug,
    'AADDEAA',                                               # going into the funnel's lip, a short collar
    'ADDDEEA',
    'AAAAAAA',
]


def draw_stopper(px):
    cols = {'A': OUTLINE, 'B': SPEC, 'C': HI, 'D': RIM, 'E': c('80929b')}
    for y, row in enumerate(STOPPER):
        for i, ch in enumerate(row):
            if ch != ' ':
                px[16 + i, y] = cols[ch]


def draw_stopcock(px):
    y0, y1 = STOPCOCK_Y
    # barrel across the stem: a short horizontal cylinder, lit along its top, its right end seen as an ellipse
    barrel = rows_mask({y: (15, 23) for y in range(y0, y1 + 1)})
    edge = outline_mask(barrel)
    for (x, y) in barrel:
        if (x, y) in edge:
            col = OUTLINE
        elif y == y0 + 1:
            col = HI
        else:
            col = GLASS_SH if y == y1 - 1 else c('80929b')
        px[x, y] = col
    end = filled_ellipse(23.5, (y0 + y1 + 1) / 2.0, 1.5, 2.5)
    eedge = outline_mask(end)
    for (x, y) in end:
        px[x, y] = OUTLINE if (x, y) in eedge else (PTFE if y <= y0 + 1 else PTFE_SH)
    # PTFE key on the left end: a shaft and a T-shaped grip, turned across the flow (closed)
    for x in (13, 14):
        px[x, y0] = OUTLINE
        px[x, y0 + 1] = PTFE
        px[x, y0 + 2] = PTFE_SH
        px[x, y1] = OUTLINE
    grip = rows_mask({y: (10, 12) for y in range(y0 - 3, y1 + 4)})
    gedge = outline_mask(grip)
    for (x, y) in grip:
        px[x, y] = OUTLINE if (x, y) in gedge else (PTFE if x == 11 and y < y1 + 2 else PTFE_SH)


BK_L, BK_R, BK_TOP, BK_BOT = 12, 27, 47, 55                   # beaker: walls, rim row, bottom row at the walls
BK_CX = (BK_L + BK_R + 1) / 2.0
BK_A = (BK_R - BK_L + 1) / 2.0
BK_B = BK_A * TILT


def beaker_body():
    """Inside of the walls, from the rim down to the bottom, which bows down like the rim."""
    pts = set()
    for y in range(BK_TOP, BK_BOT + 3):
        for x in range(BK_L, BK_R + 1):
            if y <= BK_BOT or inside(x, y, BK_CX, BK_BOT - 0.5 + 0.5, BK_A, BK_B):
                pts.add((x, y))
    return pts


def draw_beaker(px, liquid, part):
    """part 'back': the inside of the far wall seen over the rim; part 'front': contents, glass, near rim."""
    rim_disc = filled_ellipse(BK_CX, BK_TOP + 0.5, BK_A, BK_B)
    rim = outline_mask(rim_disc)
    if part == 'back':
        for (x, y) in rim_disc:                               # looking in through the mouth: the far wall
            if (x, y) not in rim:
                over(px, x, y, c('44464c', 150) if y + 0.5 < BK_TOP + 0.5 else c('5f6868', 110))
        return
    body = {p for p in beaker_body() | rim_disc if p not in rim_disc or p[1] + 0.5 >= BK_TOP + 0.5}
    body = {p for p in body if p not in rim_disc or p in rim}   # the mouth itself stays open
    edge = {p for p in outline_mask(beaker_body()) if p[1] > BK_TOP and p not in rim_disc}
    if liquid is not None:                                    # a shallow pool, its surface an ellipse
        sy = 53.5
        for (x, y) in body:
            if (x, y) in edge:
                continue
            if inside(x, y, BK_CX, sy, BK_A - 1, (BK_A - 1) * TILT):
                over(px, x, y, lighten(liquid, 40))
            elif y + 0.5 > sy:
                over(px, x, y, shade(liquid, 0.82) if x >= BK_R - 2 else liquid)
    for (x, y) in body:
        if (x, y) in edge or (x, y) in rim:
            continue
        t = (x - BK_L) / float(BK_R - BK_L)
        over(px, x, y, c('a8aea5', 170) if t < 0.14 else (c('5f6868', 140) if t > 0.85 else c('80929b', 70)))
    for (x, y) in edge:
        px[x, y] = OUTLINE
    for (x, y) in rim:                                        # near half of the rim, lit
        if y + 0.5 >= BK_TOP + 0.5:
            px[x, y] = RIM if x < BK_CX + 3 else c('80929b')
    for (x, y) in rim:                                        # and its far half, an outline seen through nothing
        if y + 0.5 < BK_TOP + 0.5:
            px[x, y] = OUTLINE
    px[BK_L - 1, BK_TOP - 1] = OUTLINE                        # pouring spout on the left lip
    px[BK_L - 1, BK_TOP] = RIM
    px[BK_L - 2, BK_TOP - 1] = OUTLINE
    for y in (50, 53):                                        # graduation marks, bowed like the rim
        px[BK_R - 3, y] = TICK
        px[BK_R - 2, y - 1] = TICK
    if liquid is not None:                                    # a drop falling from the stem tip
        px[19, 51] = lighten(liquid, 40)


# ---- the liquid, as SepFunnel.BuildSprite draws it -------------------------------------------------------------------
def columns(y):
    r = radius(y)
    l = int(round(CX - r))
    rr = int(round(CX + r - 1))
    return l + 2, rr - 1


def lighten(col, d):
    return (min(255, col[0] + d), min(255, col[1] + d), min(255, col[2] + d), col[3])


def shade(col, k):
    return (int(col[0] * k), int(col[1] * k), int(col[2] * k), col[3])


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))


def liquid_layer(fill, lower_share, mixed, upper, lower, settled):
    im = blank()
    px = im.load()
    rows, widths, area = [], [], 0
    for y in range(LIQ_BOT, LIQ_TOP - 1, -1):
        f, t = columns(y)
        if t >= f:
            rows.append(y)
            widths.append(t - f + 1)
            area += t - f + 1
    filled_area = area * fill
    lower_area = filled_area * lower_share
    filled_rows = lower_rows = 0
    s = 0.0
    for i in range(len(rows)):
        if i > 0 and s + widths[i] / 2.0 > filled_area:
            break
        if s + widths[i] / 2.0 <= lower_area:
            lower_rows = i + 1
        s += widths[i]
        filled_rows = i + 1
    if filled_rows >= 2 and 0.0 < lower_share < 1.0:
        lower_rows = max(1, min(filled_rows - 1, lower_rows))
    if filled_rows == 0:
        return im
    up = mix(mixed, upper, settled)
    lo = mix(mixed, lower, settled)
    top_row = rows[filled_rows - 1]
    f, t = columns(top_row)
    s_cx, s_a = (f + t + 1) / 2.0, (t - f + 1) / 2.0
    s_b = max(1.0, s_a * TILT)
    i_row = rows[lower_rows - 1] if 0 < lower_rows < filled_rows else None
    if i_row is not None:
        f2, t2 = columns(i_row)
        i_cx, i_a = (f2 + t2 + 1) / 2.0, (t2 - f2 + 1) / 2.0
        i_b = max(1.0, i_a * TILT)

    def is_lower(x, y):
        if lower_rows == 0:
            return False
        if i_row is None:
            return True
        dx = (x + 0.5 - i_cx) / i_a
        bow = i_b * math.sqrt(max(0.0, 1.0 - dx * dx))
        return y + 0.5 > i_row - i_b * 0.5 + bow       # the interface's front arc, bowed down in the middle

    cells = {}
    for y in range(LIQ_TOP, LIQ_BOT + 1):
        f, t = columns(y)
        for x in range(f, t + 1):
            surface = inside(x, y, s_cx, top_row + 0.5, s_a, s_b)
            if not surface and y <= top_row:
                continue
            cells[(x, y)] = surface
    for (x, y), surface in cells.items():
        if on_front_ring(x, y):
            continue
        low = is_lower(x, y)
        col = lo if low else up
        if surface:
            col = lighten(col, 45)
        else:
            f, t = columns(y)
            if x >= t - 1:
                col = shade(col, 0.82)
            if settled >= 0.6 and i_row is not None and low and (x, y - 1) in cells and not is_lower(x, y - 1) \
                    and not cells[(x, y - 1)]:
                col = lighten(col, 45)                  # a light line along the settled interface
        px[x, y] = (col[0], col[1], col[2], 230)
    return im


def draw(liquid_in_beaker):
    im = blank()
    px = im.load()
    draw_stand_and_base(px)
    draw_beaker(px, None, 'back')
    draw_ring(px, front=False)
    draw_funnel_fill(px)
    draw_funnel(px)
    draw_ring(px, front=True)
    draw_stopper(px)
    draw_stopcock(px)
    draw_beaker(px, liquid_in_beaker, 'front')
    return im


def finish(im):
    px = im.load()
    for y in range(H):                                        # every cell of the 2x4 shape must count
        for x in range(W):
            if px[x, y][3] == 0:
                px[x, y] = (0, 0, 0, 1)
    return im


os.makedirs(OUT_DIR, exist_ok=True)
empty = finish(draw(None))
empty.save(os.path.join(OUT_DIR, 'vce_sep_funnel.png'))
layers = draw(AQ)
layers.alpha_composite(liquid_layer(0.75, 0.45, mix(AQ, ORG, 0.5), ORG, AQ, 1.0))
finish(layers).save(os.path.join(OUT_DIR, 'vce_sep_funnel_layers.png'))
print('funnel', empty.size)

# ---- preview: vanilla for scale, the empty funnel, then some fill levels as the game draws them ------------------
examples = []
for fill, share, settled in ((0.25, 1.0, 1.0), (0.55, 0.5, 0.0), (0.55, 0.5, 1.0), (1.0, 0.3, 1.0)):
    im = draw(None)
    up, lo = (ORG, AQ) if share < 1.0 else (AQ, AQ)
    im.alpha_composite(liquid_layer(fill, share, mix(AQ, ORG, 0.5) if share < 1.0 else AQ, up, lo, settled))
    examples.append(finish(im))
S = 4
pieces = [Image.open(p).convert('RGBA') for p in VANILLA] + [empty] + examples
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
    sheet.alpha_composite(big, (x, 12 + (H - q.size[1]) * S))
    x += big.size[0] + 12
sheet.save(PREVIEW)
print('preview', sheet.size)
