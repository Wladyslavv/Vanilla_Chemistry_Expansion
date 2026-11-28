# 1.11.1: redraw the icons of the eight Workshop cover cards whose sprites have been redrawn since the cover was made
# (PR #7, and the beer mug), keeping the titles, the cartel card and the liquids legend.
# usage: python cover_1_11_1.py COVER_IN SPRITE_DIR HYDROPONIC_PNG COVER_OUT
#   SPRITE_DIR holds the mod sprites to draw (Textures/Items, or the old ones to check the layout against COVER_IN);
#   HYDROPONIC_PNG is the vanilla hydroponic machine (exported with the probably-stolen-sprites skill).
# Layout as in Cover.cs / Cover110.cs: cards 318 px apart horizontally and 224 px vertically, column centres
# x 194 / 512 / 830, icons centred on y 245.5 + 224 per row. A sprite's ink box (alpha > 8, so the alpha-1 cell
# markers are ignored) is centred there at 2x (the injector at 3x, the beer mug at 4x); a pair sits side by side, bottoms aligned,
# 10 px apart, centred as a group; the antibiotic tablets are a 3-2-1 pile left of the fermenter. Plant overlays sit
# on the hydroponic machine at (0, 0), as in the game.
import sys
from PIL import Image

BG = (0x22, 0x28, 0x31, 255)
PAIR_GAP = 10


def ink(im):
    return im.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()


def centre(col, row):
    return 830 - (2 - col) * 318, 245.5 + 224 * row


def clear(cover, col, row):
    # the card's inside above its title (title ink starts at y 322 + 224 per row)
    # inset 6 px so the rounded corners and border of the card stay as they are
    x0 = 684 - (2 - col) * 318 + 6
    y0 = 174 + 224 * row + 6
    cover.paste(Image.new("RGBA", (293 - 12, 318 - 174 - 6), BG), (x0, y0))


def draw(cover, sprite, left, top, scale):
    s = sprite.resize((sprite.width * scale, sprite.height * scale), Image.NEAREST)
    cover.alpha_composite(s, (left, top))


def single(cover, sprite, col, row, scale=2):
    cx, cy = centre(col, row)
    x0, y0, x1, y1 = ink(sprite)
    w, h = (x1 - x0) * scale, (y1 - y0) * scale
    draw(cover, sprite, round(cx - w / 2) - x0 * scale, round(cy - h / 2) - y0 * scale, scale)


def pair(cover, a, b, col, row, scale=2):
    """a on the left, b on the right, bottoms aligned, the taller one centred on the row's icon line."""
    cx, cy = centre(col, row)
    ia, ib = ink(a), ink(b)
    wa, ha = (ia[2] - ia[0]) * scale, (ia[3] - ia[1]) * scale
    wb, hb = (ib[2] - ib[0]) * scale, (ib[3] - ib[1]) * scale
    left = round(cx - (wa + PAIR_GAP + wb) / 2)
    bottom = round(cy + max(ha, hb) / 2)
    draw(cover, a, left - ia[0] * scale, bottom - ha - ia[1] * scale, scale)
    draw(cover, b, left + wa + PAIR_GAP - ib[0] * scale, bottom - hb - ib[1] * scale, scale)


def pile_and_fermenter(cover, tablet, fermenter, col, row, scale=2):
    # Cover.cs: tablets 12 sprite px apart in a row (3 px overlap on the old 15 px tablet), rows 7 px apart, each
    # row half a step to the right; the fermenter on the right, bottoms aligned.
    cx, cy = centre(col, row)
    it, iF = ink(tablet), ink(fermenter)
    tw, th = it[2] - it[0], it[3] - it[1]
    step_x = (tw - 3) // 2 * 2          # even, so half a step stays on whole sprite pixels
    step_y = 7 if th >= 11 else max(3, th - 4)
    pile_w = (step_x * 2 + tw) * scale
    fw, fh = (iF[2] - iF[0]) * scale, (iF[3] - iF[1]) * scale
    group_left = round(cx - (pile_w + PAIR_GAP + fw) / 2)
    bottom = round(cy + fh / 2)
    fl = group_left + pile_w + PAIR_GAP
    draw(cover, fermenter, fl - iF[0] * scale, bottom - fh - iF[1] * scale, scale)
    for r, idx in enumerate(([0, 1, 2], [0, 1], [0])):
        for i in idx:
            il = group_left + (i * step_x + r * step_x // 2) * scale
            itop = bottom - (th + r * step_y) * scale
            draw(cover, tablet, il - it[0] * scale, itop - it[1] * scale, scale)


def hydroponic(cover, machine, plant, col, row):
    c = machine.copy()
    c.alpha_composite(plant)
    single(cover, c, col, row)


def main():
    cover_in, sprites, hyd_png, cover_out = sys.argv[1:5]
    cover = Image.open(cover_in).convert("RGBA")
    sp = lambda n: Image.open(f"{sprites}/{n}.png").convert("RGBA")
    hyd = Image.open(hyd_png).convert("RGBA")
    cards = [(0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1), (0, 2), (2, 2)]
    for col, row in cards:
        clear(cover, col, row)
    single(cover, sp("dream_toxin_injector"), 0, 0, scale=3)
    single(cover, sp("vce_hplc"), 1, 0)
    pile_and_fermenter(cover, sp("vce_antibiotic"), sp("vce_fermenter_b"), 2, 0)
    single(cover, sp("ia_beer_mug"), 0, 1, scale=4)
    pair(cover, sp("vce_salt_ore"), sp("vce_electrolyzer"), 1, 1)
    hydroponic(cover, hyd, sp("vce_xinke_leaf_p2"), 2, 1)
    hydroponic(cover, hyd, sp("vce_chili_p2"), 0, 2)
    single(cover, sp("nc_opened_waste"), 2, 2)
    cover.convert("RGB").save(cover_out)


if __name__ == "__main__":
    main()
