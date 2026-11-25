# Heating plate with a condenser attached (96x48, 6 cells wide x 3 tall). The left half is the vanilla heating plate,
# unchanged; the right half is the condenser, an industrial cooling unit in a cyberpunk dress, and a vapour line joins
# the two.
# The heating plate is seen steeply from above: its plate is an ellipse half as tall as it is wide, boxes show a lit
# top face over a darker front face. The condenser follows that view:
#   - a steel plinth with a hazard-striped front, on plum feet;
#   - the condenser tank (a coil in coolant): a steel cylinder with riveted bands, a sight glass onto the coolant and
#     the coil, a neon level tube, and on its lid a cooling fan seen from above (the evaporative / closed-loop modes);
#   - a pump and power module behind on the right: a screen, a magenta neon strip, a lime power light;
#   - a spout from the tank into the collecting beaker standing on the plinth. The beaker is left clear inside (only
#     its outline and the lit edge of its mouth): the game draws the collected liquid there (Condenser.BuildBeaker,
#     which uses the beaker geometry below).
# Palette: the heating plate's (outline 292234, blue-greys, plums) plus dark steels, neon cyan and magenta, hazard
# yellow.
# Two states: coolant in the tank (vce_still_water: the sight glass and the level tube glow cyan) and empty
# (vce_still_dry, pass "dry": both dark).
# With "none" for the heating plate it draws the condenser on its own (48x48, 3x3 cells: the right half, with only the
# flange where the vapour line goes in), the item that is dragged onto a heating plate to make the still.
# usage: python draw_still.py HEATING_PLATE_PNG|none OUT_PNG PREVIEW_PNG [dry]
import math
import sys
from PIL import Image

HEATER, OUT, PREVIEW = sys.argv[1], sys.argv[2], sys.argv[3]
DRY = len(sys.argv) > 4 and sys.argv[4] == "dry"
ITEM = HEATER == "none"
W, H = 96, 48
RATIO = 0.5                                               # ellipse height / width, as on the heating plate


def c(h, a=255):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


OUTLINE = c('292234')
# Steel ramp, light to dark: cool and slightly green in the light, violet in the shade, like the vanilla machines.
ST_HI, LIT, MET, ST, ST_DK, ST_DEEP = c('c8d0cc'), c('a8aea5'), c('80929b'), c('5f6868'), c('44464c'), c('373541')
SCREEN, SCREEN_LT = c('2f2b3a'), c('303a41')
PLUM, PLUM_DK = c('5a3d4c'), c('4a2f38')
GL_HI, GL_PALE, GL_LT, GL, GL_DK = c('c8e6e2'), c('8abfbb'), c('30b4a4'), c('2f96a5'), c('1b6077')
COIL, COIL_SH = c('e8f4f0'), c('a8d8d0')
MAG, MAG_LT, MAG_DK = c('d95763'), c('f08aa0'), c('82304e')
HAZ, HAZ_DK = c('fbc836'), c('c48a3b')
LIME = c('99e550')

im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
if not ITEM:
    im.alpha_composite(Image.open(HEATER).convert("RGBA"), (0, 0))


def paste(layer, outline=True):
    px = im.load()
    for (x, y), col in layer.items():
        if 0 <= x < W and 0 <= y < H:
            px[x, y] = col
    if outline:
        for (x, y) in layer:
            for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (x + ox, y + oy)
                if n not in layer and 0 <= n[0] < W and 0 <= n[1] < H:
                    px[n[0], n[1]] = OUTLINE


def inside(x, y, cx, cy, a, b):
    return ((x + 0.5 - cx) / a) ** 2 + ((y + 0.5 - cy) / b) ** 2 <= 1.0


def disc(cx, cy, a, b):
    pts = set()
    for y in range(int(cy - b) - 1, int(cy + b) + 2):
        for x in range(int(cx - a) - 1, int(cx + a) + 2):
            if inside(x, y, cx, cy, a, b):
                pts.add((x, y))
    return pts


def rim(pts):
    return {(x, y) for (x, y) in pts if any((x + ox, y + oy) not in pts for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}


def steel(u):
    """Colour across a lit steel cylinder, u from 0 (left edge) to 1 (right edge): a rim, a highlight with a specular
    streak, the falloff into shade, and a little reflected light at the far edge."""
    for limit, col in ((0.05, MET), (0.12, LIT), (0.2, ST_HI), (0.3, LIT), (0.45, MET), (0.62, ST), (0.78, ST_DK),
                       (0.92, ST_DEEP)):
        if u < limit:
            return col
    return ST_DK


def tint(col, toward, t):
    return (int(col[0] + (toward[0] - col[0]) * t), int(col[1] + (toward[1] - col[1]) * t),
            int(col[2] + (toward[2] - col[2]) * t), max(col[3], 200))


def front_arc(cx, cy, a, b, x):
    """Row of an ellipse's near edge at column x, or None outside it."""
    dx = (x + 0.5 - cx) / a
    if abs(dx) > 1:
        return None
    return int(cy + b * math.sqrt(1 - dx * dx))


# ---- layout --------------------------------------------------------------------------------------------------------------
# Every part is placed in 3D and projected the way the heating plate is drawn: x stays x, a point d px back from the
# plinth's front edge and z px up from the floor lands on row FLOOR - z - d / 2 (depth is halved, height is not), so
# a circle lying flat is an ellipse half as tall as it is wide. Parts are drawn back to front.
FLOOR = 46                                                # the floor at the plinth's front edge, level with the plate
P_Z = 4                                                   # plinth height


def row(d, z):
    return FLOOR - z - d * 0.5


# Where the parts stand (their sections below draw them): the tank (radius 11.5, 13 px back, 22 tall), the module
# (x 76-93, d 14-26, 20 tall) and the beaker (radius 6.5, 7 px back, 9 tall).
TCX, TA, T_D = 61.5, 11.5, 13
BCX, BA, B_D = 86.0, 6.5, 7


# ---- plinth: a low steel deck whose top every other part stands on; hazard stripes on its front --------------------------
P_X0, P_X1, P_DEPTH = 48, 94, 26
P_BACK = int(row(P_DEPTH, P_Z))                           # back edge of the top face (row 29)
P_LIP = int(row(0, P_Z))                                  # front edge of the top face (row 42)
plinth = {}
for y in range(P_BACK, FLOOR + 1):
    for x in range(P_X0, P_X1 + 1):
        if x in (P_X0, P_X1) and y in (P_BACK, FLOOR):
            continue
        if y < P_LIP:
            col = ST_DK if y == P_BACK else (LIT if y == P_LIP - 1 else ST)       # top face, lit along its near lip
            if P_BACK < y < P_LIP - 1 and (x + (y // 2) * 2) % 5 == 0 and y % 2 == 0:
                col = MET                                                         # tread plate studs
            if x == P_X0:
                col = MET
        elif y == P_LIP:
            col = ST_DK
        elif y in (P_LIP + 1, P_LIP + 2):
            col = HAZ if (x + y) % 6 < 3 else ST_DEEP                             # hazard stripes on the front
        else:
            col = ST_DEEP
        plinth[(x, y)] = col
paste(plinth)
px = im.load()
for x in range(P_X0 + 1, P_X1):                           # a seam across the deck, and the edge above the stripes
    if px[x, 36][3]:
        px[x, 36] = ST_DK
    px[x, P_LIP + 3] = OUTLINE
for (x, y) in ((P_X0 + 2, P_BACK + 2), (P_X1 - 2, P_BACK + 2), (P_X0 + 2, P_LIP - 2), (P_X1 - 2, P_LIP - 2)):
    px[x, y] = ST_HI                                      # corner bolts on the deck, each with its shadow
    px[x + 1, y + 1] = ST_DEEP
for x in range(P_X0 + 3, P_X1 - 2, 6):                    # slits along the front face
    px[x, P_LIP + 4] = OUTLINE
# Contact shadows where the parts stand, falling to the lower right (the light is at the upper left).
shadows = disc(TCX + 2.0, row(T_D, P_Z) + 1.0, TA + 1.5, (TA + 1.5) * RATIO) \
    | disc(BCX + 1.5, row(B_D, P_Z) + 1.0, BA + 1.0, (BA + 1.0) * RATIO) \
    | {(x, y) for x in range(77, 95) for y in range(int(row(14, P_Z)), int(row(14, P_Z)) + 3)}
for (x, y) in shadows:
    if P_X0 < x < P_X1 and P_BACK < y < P_LIP and px[x, y][3] and px[x, y] != OUTLINE:
        px[x, y] = ST_DK if px[x, y] in (ST, MET) else (ST if px[x, y] == LIT else px[x, y])

# ---- pump and power module, at the back right of the deck ---------------------------------------------------------------
# A box 18 wide, 12 deep (d 14-26), 20 tall: its top face takes 6 rows, its front face 20.
M_X0, M_X1 = 76, 93
M_D0, M_D1, M_H = 14, 26, 20
M_TOP = int(row(M_D1, P_Z + M_H))                         # back edge of its top face (row 9)
M_FRONT = int(row(M_D0, P_Z + M_H))                       # front edge of its top face (row 15)
M_BOT = int(row(M_D0, P_Z))                               # where its front face meets the deck (row 35)
module = {}
for y in range(M_TOP, M_BOT + 1):
    for x in range(M_X0, M_X1 + 1):
        if x in (M_X0, M_X1) and y == M_TOP:
            continue
        if y < M_FRONT:
            col = MET if y < M_FRONT - 1 else LIT         # top face, its near edge catching the light
            if (x - M_X0) % 4 == 2 and M_TOP < y < M_FRONT - 1:
                col = ST                                  # vent slots on the top
        else:
            col = ST_DK if x < M_X1 - 1 else ST_DEEP
            if x == M_X0:
                col = ST
            elif x == M_X0 + 1:
                col = MET if y == M_FRONT else ST_DK
            if y == M_FRONT:
                col = ST if x < M_X1 - 1 else ST_DK       # the front face's top edge, under the lit lip
        module[(x, y)] = col
paste(module)
px = im.load()
# screen with a readout: a bevel, a scan line, a little trace
for y in range(16, 21):
    for x in range(79, 91):
        edge = x in (79, 90) or y in (16, 20)
        px[x, y] = OUTLINE if edge else (SCREEN_LT if y == 17 else SCREEN)
for x in range(79, 91):
    px[x, 21] = ST if x < 90 else ST_DK                   # the bezel's lit lower lip
trace = ((80, 19), (81, 19), (82, 18), (83, 18), (84, 19), (85, 19), (86, 18), (87, 17), (88, 19), (89, 19))
for (x, y) in trace:
    px[x, y] = (GL_HI if x == 87 else GL_LT) if not DRY else GL_DK
# magenta neon strip with its glow on the casing above and below
for x in range(78, 92):
    px[x, 24] = MAG_LT if x < 81 else MAG
    px[x, 23] = tint(px[x, 23], MAG, 0.35)
    px[x, 25] = tint(px[x, 25], MAG, 0.25)
# status lights and a lime power light on the top lip
for (x, col) in ((79, LIME), (81, GL_LT if not DRY else ST_DEEP), (83, MAG)):
    px[x, 26] = col
px[91, 16] = LIME
px[92, 16] = tint(px[92, 16], LIME, 0.5)
# corner screws, each with a shadow
for (x, y) in ((M_X0 + 2, M_FRONT + 2), (M_X1 - 2, M_FRONT + 2), (M_X0 + 2, M_BOT - 2)):
    px[x, y] = LIT
    px[x + 1, y + 1] = ST_DEEP
# a small warning label on the left of the front face
for y in (28, 29, 30):
    for x in (78, 79, 80):
        px[x, y] = HAZ if y < 30 else HAZ_DK
px[79, 28] = OUTLINE                                      # the label's exclamation mark
# a power cable from the module's foot to the tank, lying on the deck
for (x, y) in ((76, 34), (75, 35), (74, 36), (73, 36)):
    px[x, y] = ST_DEEP
    if px[x, y - 1] not in (OUTLINE,):
        px[x, y - 1] = tint(px[x, y - 1], ST_DEEP, 0.5)

# ---- the condenser tank: a steel cylinder with riveted bands and a sight glass ------------------------------------------
# A cylinder of radius 11.5 standing 13 px back on the deck (so its foot, d 1.5-24.5, is on the deck), 22 tall.
TCX, TA = 61.5, 11.5
TB = TA * RATIO
T_D, T_H = 13, 22
T_BOT = row(T_D, P_Z)                                     # centre row of its foot (35.5)
T_TOP = T_BOT - T_H                                       # centre row of its lid (13.5)
tank = {}
body = set()
for y in range(int(T_TOP), int(T_BOT + TB) + 2):
    for x in range(int(TCX - TA), int(TCX + TA) + 1):
        if TCX - TA <= x + 0.5 <= TCX + TA and (y + 0.5 <= T_BOT or inside(x, y, TCX, T_BOT, TA, TB)):
            body.add((x, y))
for (x, y) in body:
    u = (x + 0.5 - (TCX - TA)) / (2 * TA)
    tank[(x, y)] = steel(u)
paste(tank)
px = im.load()
# the foot is in its own shadow: the lowest rows of the body darken towards the deck
for x in range(int(TCX - TA), int(TCX + TA) + 1):
    y = front_arc(TCX, T_BOT, TA, TB, x)
    if y is None:
        continue
    for k in (1, 2):
        if (x, y - k) in body:
            px[x, y - k] = tint(px[x, y - k], ST_DEEP, 0.45 if k == 1 else 0.25)

# riveted bands: the near halves of two rings round the tank
BANDS = (T_BOT - 15.0, T_BOT - 5.5)                       # rings 15 and 5.5 px up the tank
for cy in BANDS:
    for x in range(int(TCX - TA), int(TCX + TA) + 1):
        y = front_arc(TCX, cy, TA, TB, x)
        if y is None:
            continue
        u = (x + 0.5 - (TCX - TA)) / (2 * TA)
        px[x, y] = ST_HI if 0.12 < u < 0.22 else (LIT if u < 0.35 else (MET if u < 0.7 else ST))
        px[x, y + 1] = MET if u < 0.3 else (ST if u < 0.65 else ST_DK)
        px[x, y + 2] = OUTLINE
        if x % 4 == 1 and 0.1 < u < 0.9:
            px[x, y + 1] = ST_HI if u < 0.5 else LIT      # rivets, lit on top
            if px[x + 1, y + 1] != OUTLINE:
                px[x + 1, y + 1] = ST_DEEP                # and their shadow
for (x, y) in rim(body):
    px[x, y] = OUTLINE

# sight glass between the bands: the coolant and the coil behind it, or the empty dark tank
for x in range(56, 67):
    top = front_arc(TCX, BANDS[0], TA, TB, x) + 3
    bottom = front_arc(TCX, BANDS[1], TA, TB, x) - 1
    for y in range(top, bottom + 1):
        if x in (56, 66) or y in (top, bottom):
            px[x, y] = OUTLINE
            continue
        turn = (x - 2 * y) % 7                            # the coil's turns: a lit top and a shaded underside
        glint = (x + y) % 11 == 0 and x < 63
        if DRY:
            px[x, y] = MET if turn == 0 else (ST if turn == 1 else (SCREEN_LT if glint else SCREEN))
        else:
            px[x, y] = COIL if turn == 0 else (COIL_SH if turn == 1 else
                                               (GL_HI if glint else (GL_LT if x < 59 else (GL if x < 64 else GL_DK))))
    if not DRY and top + 1 <= bottom - 1:
        px[x, top + 1] = GL_HI if x < 64 else GL_PALE      # the coolant's level line
for x in (57, 65):                                        # bolts at the window's corners
    for y in (front_arc(TCX, BANDS[0], TA, TB, x) + 4, front_arc(TCX, BANDS[1], TA, TB, x) - 2):
        px[x, y] = LIT

# neon level tube on the right of the tank, capped top and bottom, glowing on the steel around it
for y in range(21, 32):
    cap = y in (21, 31)
    px[67, y] = OUTLINE
    px[70, y] = OUTLINE
    if cap:
        px[68, y] = LIT
        px[69, y] = MET
    else:
        px[68, y] = (GL_HI if y > 23 else GL_DK) if not DRY else ST_DEEP
        px[69, y] = (GL_LT if y > 23 else GL_DK) if not DRY else ST_DEEP
    if not DRY and 23 < y < 31:
        for gx in (66, 71):
            if (gx, y) in body and px[gx, y] != OUTLINE:
                px[gx, y] = tint(px[gx, y], GL_LT, 0.35)

# a coolant label low on the left of the tank
for y in (31, 32):
    for x in range(52, 56):
        px[x, y] = HAZ if y == 31 else HAZ_DK
px[53, 31] = GL_DK
px[54, 31] = GL_DK

# ---- the lid, seen from above: a steel rim with bolts round a cooling fan ------------------------------------------------
lid = disc(TCX, T_TOP, TA, TB)
lid_layer = {}
for (x, y) in lid:
    # a flat top lit from the upper left: lightest at the back left, darkest at the front right
    s = (x + 0.5 - (TCX - TA)) / (2 * TA) * 0.7 + (y + 0.5 - (T_TOP - TB)) / (2 * TB) * 0.3
    lid_layer[(x, y)] = ST_HI if s < 0.16 else (LIT if s < 0.36 else (MET if s < 0.72 else ST))
paste(lid_layer)
px = im.load()
# the lid's front edge, a band below its near rim
for x in range(int(TCX - TA), int(TCX + TA) + 1):
    y = front_arc(TCX, T_TOP, TA, TB, x)
    if y is None:
        continue
    u = (x + 0.5 - (TCX - TA)) / (2 * TA)
    px[x, y] = LIT if u < 0.25 else (MET if u < 0.55 else ST)
    px[x, y + 1] = ST if u < 0.3 else ST_DK
    px[x, y + 2] = OUTLINE
for (x, y) in rim(lid):
    if y + 0.5 < T_TOP + 0.5:
        px[x, y] = OUTLINE
# fan well: recessed, so its far inside wall shows (lit) and its near half is in shadow; five blades, each with a lit
# leading edge; a guard ring; the hub light
FA = 7.5
well = disc(TCX, T_TOP, FA, FA * RATIO)
for (x, y) in well:
    ang = math.atan2((y + 0.5 - T_TOP) / RATIO, x + 0.5 - TCX)
    phase = ((ang / (2 * math.pi)) * 5) % 1.0
    if phase < 0.08:
        col = LIT                                         # leading edge
    elif phase < 0.38:
        col = MET if y + 0.5 < T_TOP else ST              # blade, lit on the far side
    else:
        col = SCREEN if y + 0.5 >= T_TOP else SCREEN_LT
    px[x, y] = col
for (x, y) in rim(well):
    px[x, y] = OUTLINE
for x in range(int(TCX - FA) + 1, int(TCX + FA)):         # the far inside wall of the well, catching the light
    dx = (x + 0.5 - TCX) / FA
    if abs(dx) < 0.85:
        y = int(T_TOP - FA * RATIO * math.sqrt(1 - dx * dx)) + 1
        if (x, y) in well and (x, y) not in rim(well):
            px[x, y] = ST
for (x, y) in rim(disc(TCX, T_TOP, FA * 0.6, FA * 0.6 * RATIO)):   # guard ring
    if px[x, y] in (SCREEN, SCREEN_LT):
        px[x, y] = ST_DK
for (x, y) in disc(TCX, T_TOP, 2.2, 1.2):
    px[x, y] = MAG if not DRY else MAG_DK                 # hub light
px[int(TCX) - 1, int(T_TOP)] = MAG_LT if not DRY else MAG
for ang in range(0, 360, 60):                             # bolts round the rim, each with its shadow
    a = math.radians(ang + 30)
    bx = int(TCX + (TA - 1.8) * math.cos(a))
    by = int(T_TOP + (TB - 0.9) * math.sin(a))
    px[bx, by] = ST_HI
    if (bx + 1, by + 1) in lid:
        px[bx + 1, by + 1] = ST_DK

# ---- collecting beaker near the deck's front right, clear inside ----------------------------------------------------------
# A glass of radius 6.5 standing 7 px back (d 0.5-13.5, in front of the module), 9 tall. Kept in step with
# Condenser.BuildBeaker.
BCX, BA = 86.0, 6.5
BB = BA * RATIO
B_BOT = row(7, P_Z)                                       # centre row of its foot (38.5)
B_TOP = B_BOT - 9                                         # centre row of its mouth (29.5)
bbody = set()
for y in range(int(B_TOP), int(B_BOT + BB) + 2):
    for x in range(int(BCX - BA), int(BCX + BA) + 1):
        if BCX - BA <= x + 0.5 <= BCX + BA and (y + 0.5 <= B_BOT or inside(x, y, BCX, B_BOT, BA, BB)):
            bbody.add((x, y))
btop = disc(BCX, B_TOP, BA, BB)
shape = bbody | btop
# Only the glass is drawn, a faint tint with its outline: inside, what stands behind shows through, and the collected
# liquid is drawn over it by the game.
px = im.load()
edge = rim(shape)
for (x, y) in shape - edge:
    r0, g0, b0, a0 = px[x, y]
    t = 0.45 if x + 0.5 < BCX - BA + 2 else 0.22          # the left wall catches the light
    px[x, y] = (int(r0 + (GL_PALE[0] - r0) * t), int(g0 + (GL_PALE[1] - g0) * t), int(b0 + (GL_PALE[2] - b0) * t),
                max(a0, 200))
for p in edge:
    px[p] = OUTLINE

# ---- spout: out of the tank's right side 14 px up, across to the beaker, down into its mouth ------------------------------
# It runs at the tank's depth, in front of the module, and goes down behind the beaker's near lip.
SP_Y = int(row(T_D, P_Z + 14))                            # row 21
spout = {}
for x in range(int(TCX + TA) + 1, 86):
    spout[(x, SP_Y)] = LIT if x < 80 else MET
    spout[(x, SP_Y + 1)] = ST_DK
for y in range(SP_Y, int(B_TOP) + 2):
    spout[(84, y)] = MET
    spout[(85, y)] = ST_DK
spout[(84, SP_Y)] = LIT                                   # the elbow's lit corner
paste(spout)
px = im.load()
for (x, y) in rim(btop):                                  # the near lip of the mouth, in front of the spout
    if y + 0.5 >= B_TOP:
        px[x, y] = GL_HI

# ---- vapour line: out of the heating plate's hub, in front of its frame, into a flange on the tank -----------------------
# The hub is the block where the heating plate's rod meets the frame's top bar (x 20-27, rows 8-10). The line leaves its
# right face through a collar, crosses the frame's right post in front of it and goes into a bolted flange on the tank's
# upper left. On the condenser alone only the flange is there.
LY = 9
line = {}
for x in (range(28, 49) if not ITEM else ()):
    joint = x in (35, 36, 43, 44)                         # two coupling rings along the line
    line[(x, LY)] = (ST_HI if x in (32, 33) else LIT) if not joint else MET
    line[(x, LY + 1)] = MET if not joint else ST
    line[(x, LY + 2)] = ST_DK if not joint else ST_DEEP
paste(line)
px = im.load()
for x in ((35, 43) if not ITEM else ()):                  # the rings stand proud of the pipe by a pixel
    for xx in (x, x + 1):
        px[xx, LY - 1] = LIT if xx == x else MET
        px[xx, LY + 3] = ST_DEEP
fittings = {}
for y in (range(LY - 1, LY + 4) if not ITEM else ()):     # collar where it leaves the hub
    fittings[(28, y)] = LIT if y == LY - 1 else MET
    fittings[(29, y)] = LIT if y == LY - 1 else ST
for y in range(LY - 2, LY + 5):                           # flange on the tank
    fittings[(48, y)] = LIT if y == LY - 2 else MET
    fittings[(49, y)] = LIT if y == LY - 2 else ST
    fittings[(50, y)] = ST_DK
paste(fittings)
px = im.load()
px[49, LY - 1] = HAZ                                      # bolt heads
px[49, LY + 3] = HAZ

# ---- every 16x16 cell must count -----------------------------------------------------------------------------------
if ITEM:
    im = im.crop((48, 0, 96, 48))
    W = 48
px = im.load()
# The game reads each cell's pixel at x = 16c + 8 and, counting rows from the bottom of the texture, y = 16r + 8: from
# the top that is row 16 * cell + 7 (GameItem.SetSpriteAndShapeFromMod).
for cy in range(H // 16):
    for cx in range(W // 16):
        if px[cx * 16 + 8, cy * 16 + 7][3] == 0:
            for yy in range(cy * 16, cy * 16 + 16):
                for xx in range(cx * 16, cx * 16 + 16):
                    if px[xx, yy][3] == 0:
                        px[xx, yy] = (0, 0, 0, 1)
im.save(OUT)
print(OUT, im.size)

S = 6
q = im.copy()
qp = q.load()
for y in range(H):
    for x in range(W):
        if qp[x, y][3] <= 3:
            qp[x, y] = (0, 0, 0, 0)
sheet = Image.new('RGBA', (W + 16, H + 16), (0x44, 0x44, 0x4c, 255))
sheet.alpha_composite(q, (8, 8))
sheet.resize((sheet.size[0] * S, sheet.size[1] * S), Image.NEAREST).save(PREVIEW)
print(PREVIEW)
