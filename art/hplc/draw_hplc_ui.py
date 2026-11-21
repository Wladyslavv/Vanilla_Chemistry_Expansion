# Window art for the HPLC (Hplc.cs): the column with its collecting line, cut into four row segments
# vce_hplc_seg_0..3 (32x28 each). In the window each collection row is [segment | slot] with no gap, and the four
# rows are stacked with no gap, so the segments join into one column and every branch meets its own slot.
#   segment 0: inlet and column head        segments 1, 2: column body        segment 3: column foot, outlet
# The outlet turns right into the collecting line, which runs up past all four branches.
# Rows are 28 high: a collection slot (MachineryIntake) is 24 high and is stretched to its row.
# usage: python draw_hplc_ui.py OUT_DIR PREVIEW_PNG
import os
import sys
from PIL import Image

OUT, PREVIEW = sys.argv[1], sys.argv[2]
SW, SH = 32, 28
ROWS = 4


def c(h):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


OUTLINE = c('292234')
TUBE, TUBE_SH = c('e9e6dc'), c('a8aea5')
GLASS_HI, GLASS = c('c8e6e2'), c('8abfbb')
PACK_HI, PACK, PACK_SH = c('e0d09b'), c('c4c4b0'), c('a8aea5')
BAND = c('67aaa5')
M_HI, M, M_SH = c('a8aea5'), c('5f6868'), c('44464c')
BRASS, BRASS_LT, BRASS_DK = c('bb9165'), c('e0d09b'), c('8f6a4a')

BODY_X0, BODY_X1 = 4, 13          # column outline columns
LINE_X = 22                       # collecting line: 22 lit, 23 shaded, outline 21 and 24
BRANCH_Y = 13                     # branch tube rows 13 (lit) and 14 (shaded), outline 12 and 15
VALVE_X0, VALVE_X1 = 26, 29


def segment(k):
    im = Image.new('RGBA', (SW, SH), (0, 0, 0, 0))
    px = im.load()

    def put(x, y, col):
        if 0 <= x < SW and 0 <= y < SH:
            px[x, y] = col

    def body_row(y):
        gy = k * SH + y                      # global row, so the packing bands run on across segments
        put(BODY_X0, y, OUTLINE)
        put(BODY_X1, y, OUTLINE)
        put(BODY_X0 + 1, y, GLASS_HI)
        put(BODY_X1 - 1, y, GLASS)
        for x in range(BODY_X0 + 2, BODY_X1 - 1):
            if gy % 13 == 0:
                put(x, y, BAND)
            else:
                put(x, y, PACK_HI if x == BODY_X0 + 2 else PACK_SH if x == BODY_X1 - 2 else PACK)

    def fitting(y0, y1):
        for y in range(y0, y1 + 1):
            for x in range(BODY_X0, BODY_X1 + 1):
                edge = x in (BODY_X0, BODY_X1) or y in (y0, y1)
                put(x, y, OUTLINE if edge else (BRASS_LT if y == y0 + 1 else BRASS_DK if y == y1 - 1 else BRASS))

    def vtube(x, y0, y1):
        for y in range(y0, y1 + 1):
            put(x - 1, y, OUTLINE)
            put(x, y, TUBE)
            put(x + 1, y, TUBE_SH)
            put(x + 2, y, OUTLINE)

    # column
    if k == 0:
        vtube(8, 0, 3)                       # inlet
        fitting(4, 8)                        # head
        for y in range(9, SH):
            body_row(y)
    elif k == ROWS - 1:
        for y in range(0, 19):
            body_row(y)
        fitting(19, 23)                      # foot
        # outlet: out of the foot and right along the bottom into the collecting line
        for x in range(8, LINE_X + 2):
            put(x, 24, OUTLINE if x > 9 and x < LINE_X else px[x, 24])
            put(x, 25, TUBE)
            put(x, 26, TUBE_SH)
            put(x, 27, OUTLINE)
        put(7, 25, OUTLINE)
        put(7, 26, OUTLINE)
        put(8, 24, TUBE)
        put(9, 24, TUBE_SH)
    else:
        for y in range(0, SH):
            body_row(y)

    # collecting line: from the first branch down to the outlet
    y_top = BRANCH_Y if k == 0 else 0
    y_bot = 26 if k == ROWS - 1 else SH - 1
    for y in range(y_top, y_bot + 1):
        put(LINE_X - 1, y, OUTLINE if not (k == ROWS - 1 and y >= 25) else px[LINE_X - 1, y])
        put(LINE_X, y, TUBE)
        put(LINE_X + 1, y, TUBE_SH)
        put(LINE_X + 2, y, OUTLINE)
    if k == 0:
        for x in range(LINE_X - 1, LINE_X + 3):
            put(x, BRANCH_Y - 1, OUTLINE)
    if k == ROWS - 1:
        put(LINE_X + 2, 27, OUTLINE)
        put(LINE_X + 1, 27, OUTLINE)
        put(LINE_X, 27, OUTLINE)

    # branch to the slot on the right, with a valve
    for x in range(LINE_X + 2, SW):
        put(x, BRANCH_Y - 1, OUTLINE)
        put(x, BRANCH_Y, TUBE)
        put(x, BRANCH_Y + 1, TUBE_SH)
        put(x, BRANCH_Y + 2, OUTLINE)
    put(LINE_X + 2, BRANCH_Y, TUBE)
    put(LINE_X + 2, BRANCH_Y + 1, TUBE_SH)
    for y in range(BRANCH_Y - 3, BRANCH_Y + 5):
        for x in range(VALVE_X0, VALVE_X1 + 1):
            edge = x in (VALVE_X0, VALVE_X1) or y in (BRANCH_Y - 3, BRANCH_Y + 4)
            put(x, y, OUTLINE if edge else (M_HI if y == BRANCH_Y - 2 else M_SH if y == BRANCH_Y + 3 else M))
    return im


os.makedirs(OUT, exist_ok=True)
segs = []
for k in range(ROWS):
    im = segment(k)
    im.save(os.path.join(OUT, 'vce_hplc_seg_%d.png' % k))
    segs.append(im)
print('segments', segs[0].size)

# Preview: the four rows as the window lays them out, each [segment | 24x28 slot frame].
S = 4
sheet = Image.new('RGBA', (SW + 24 + 16, SH * ROWS + 16), (0x44, 0x44, 0x4c, 255))
for k, im in enumerate(segs):
    y = 8 + k * SH
    sheet.alpha_composite(im, (8, y))
    for yy in range(y, y + SH):
        for xx in range(8 + SW, 8 + SW + 24):
            edge = yy in (y, y + SH - 1) or xx in (8 + SW, 8 + SW + 23)
            sheet.putpixel((xx, yy), (0x29, 0x22, 0x34, 255) if edge else (0x30, 0x3a, 0x41, 255))
sheet.resize((sheet.size[0] * S, sheet.size[1] * S), Image.NEAREST).save(PREVIEW)
