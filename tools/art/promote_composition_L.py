#!/usr/bin/env python3
"""
Promote the approved composition (study L, 2026-11-26) into real kit art.

What this writes into art/kit (and nothing else -- publish_to_unity.py mirrors it afterwards):

    backgrounds/office_L0..L2.png   480x270  the room: night window behind the applicant, board,
                                             clock, filing cabinet, plant, desk front face
    desk/desk_foreground.png        480x112  the desk you sit at, with the work on it
    desk/monitor_small.png          154x106  1-px-crisp redraw at the approved 0.58 scale
    desk/document_small.png         108x63   1-px-crisp redraw at the approved 0.56 scale

Why no downscaling: the approved composition calls for the monitor at 58% and the email sheet at 56%.
Resampling the 264x200 bezel would blur its 1 px frame, which the Point filter then bakes in. These two
sprites are therefore re-authored at their final pixel size with the same 1 px structure and the same
palette entries. Nothing else in the kit is touched.

Palette rule (docs/15-art-bible.md §3): solid PALETTE colours only, alpha 0 or 255, no blending.
Corruption states (bible §7): the same room with lighting changes only -- never a different room.
"""
import os, sys
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))
from make_art_kit import PALETTE, rgba, new   # noqa: E402
from ragged import ragged_rect, notch_corner, scuff, LCG   # noqa: E402

# Three approved "glow" steps (user, 2026-11-26) -- light falls off without blending. They are added to
# the kit palette by tools/art/make_art_kit.py; referenced here by name only.

W, H = 480, 270
FLOOR_ROW = 198
DESK_ROW = 206
DESK_H = H - DESK_ROW          # 64 px band the desk occupies at the bottom of the screen
DESK_LIP = 19                  # desk_foreground is the front lip only (see desk_work)
MON_W, MON_H = 154, 106
DOC_W, DOC_H = 96, 54     # measured against the monitor glass; 108x63 overflowed it by 6 px


def out(rel):
    return os.path.join(ROOT, 'art', 'kit', rel)


# --------------------------------------------------------------------------- room props
def window_night(img, d, x, row, w, h):
    """Night city, v3 -- SMOOTH. No dither anywhere.

    Three flat sky steps (hard boundaries read as haze layers; a checker dither read as noise),
    a slatted blind at the top, then two silhouette bands where the FAR band is *lighter* than the
    near band so depth comes from value, not from an extra haze rectangle.

    Zones are absolute -- nothing may cross them:
        blinds      glass_y0 .. glass_y0+BLIND_H
        sky         below blinds .. glass_y1   (stars + moon only, and only above the tall roofs)
        skyline     glass_y1-52 .. glass_y1    (buildings only; lit windows clipped inside each box)
    """
    from ragged import LCG

    rnd = LCG(0x51CE)
    glass_x0, glass_y0 = x + 2, row + 2
    glass_x1, glass_y1 = x + w - 2, row + h - 2
    gw, gh = glass_x1 - glass_x0 + 1, glass_y1 - glass_y0 + 1
    BLIND_H = 13
    CITY_H = 44                       # near-band buildings may be this tall
    star_bottom = glass_y1 - CITY_H - 6

    # ---- frame: straight and clean (the window is a built object, not a worn prop).
    d.rectangle([x, row, x + w, row + h], fill=rgba('panel2'))
    d.rectangle([x, row, x + w, row + h], outline=rgba('linehi'))
    d.rectangle([glass_x0 - 1, glass_y0 - 1, glass_x1 + 1, glass_y1 + 1], outline=rgba('line'))
    d.rectangle([glass_x0, glass_y0, glass_x1, glass_y1], fill=rgba('void'))

    # ---- sky: three flat steps, darkest at the top, light pollution at the bottom.
    band_top = glass_y0 + BLIND_H
    # ---- sky: ONE flat tone. Value ramp does all the work: sky (lightest) > far towers > near
    # towers (darkest). No gradient, no dither, no haze rectangle -- every tower reads against the
    # sky because it is darker than the sky, and every near tower reads against the far ones.
    d.rectangle([glass_x0, band_top, glass_x1, glass_y1], fill=rgba('panel2'))
    # faint wash of city light in the last rows of sky (behind the towers, so they occlude it)
    for y in range(glass_y1 - 9, glass_y1 - 1, 2):
        run = glass_x0
        while run < glass_x1:
            seg = rnd.irange(10, 26)
            if rnd.below(0.5):
                d.line([run, y, min(glass_x1, run + seg), y], fill=rgba('panel'))
            run += seg + rnd.irange(5, 13)

    # ---- stars: upper sky only, never adjacent, three brightness tiers.
    # Sky is panel2 now, so stars must be LIGHTER than panel2 -- textdim / text / white, with
    # muted only for the dimmest, or the field disappears into the background.
    placed = set()
    for _ in range(int(gw * 0.62)):
        sx = rnd.irange(glass_x0 + 2, glass_x1 - 2)
        sy = rnd.irange(band_top + 3, star_bottom)
        if any((sx + dx, sy + dy) in placed for dx in range(-3, 4) for dy in range(-3, 4)):
            continue
        placed.add((sx, sy))
        r = rnd.below(0.6)
        if r < 0.22:                                   # bright: a 2 px cross
            img.putpixel((sx, sy), rgba('text'))
            if sx + 1 < glass_x1:
                img.putpixel((sx + 1, sy), rgba('textdim'))
            img.putpixel((sx, sy - 1), rgba('textdim'))
        elif r < 0.62:
            img.putpixel((sx, sy), rgba('textdim'))
        else:
            img.putpixel((sx, sy), rgba('muted' if rnd.below(0.6) else 'linehi'))

    # ---- moon: crescent, one halo step, high on the left under the blinds
    mx, my, mr = glass_x0 + 25, band_top + 17, 6
    d.ellipse([mx - mr - 2, my - mr - 2, mx + mr + 2, my + mr + 2], fill=rgba('line'))
    d.ellipse([mx - mr, my - mr, mx + mr, my + mr], fill=rgba('text'))
    d.ellipse([mx - mr + 3, my - mr - 3, mx + mr + 3, my + mr - 3], fill=rgba('panel2'))

    # ---- skyline: FAR band first (lighter, shorter), then NEAR band (darkest, tallest)
    base = glass_y1

    def band(tone, lo, hi, wmin, wmax, gap, lit_p, lit_tones,
             setback_p=0.0, antenna_p=0.0, rim=None):
        bx = glass_x0 + rnd.irange(-10, 0)
        while bx < glass_x1 + 4:
            bw = rnd.irange(wmin, wmax)
            bh = rnd.irange(lo, hi)
            top = base - bh
            x0b, x1b = max(glass_x0, bx), min(glass_x1, bx + bw)
            if x1b - x0b >= 3:
                d.rectangle([x0b, top, x1b, base], fill=rgba(tone))
                if rim:                          # rim-lit roof edge: how a low-value block stays readable
                    d.line([x0b, top, x1b, top], fill=rgba(rim))
                if rnd.below(setback_p) and (x1b - x0b) >= 7:   # stepped roof: narrower storey
                    inset = rnd.irange(1, max(1, (x1b - x0b) // 3))
                    d.rectangle([x0b + inset, top - rnd.irange(3, 7), x1b - inset, top - 1],
                                fill=rgba(tone))
                if rnd.below(antenna_p):
                    ax = max(glass_x0 + 1, min(glass_x1 - 1, (x0b + x1b) // 2 + rnd.irange(-2, 2)))
                    top_a = top - rnd.irange(4, 8)
                    d.line([ax, top_a, ax, top - 1], fill=rgba(tone))
                    img.putpixel((ax, top_a - 1), rgba('danger'))
                if lit_p > 0:
                    for wy in range(top + 3, base - 3, 4):
                        for wx in range(x0b + 2, x1b - 1, 3):
                            if rnd.below(lit_p):
                                img.putpixel((wx, wy), rgba(rnd.pick(lit_tones)))
            bx += bw + rnd.irange(gap, gap + 3)

    # three value planes: sky haze -> far towers (panel2, rim-lit) -> near towers (void, darkest).
    band('panel', 14, 28, 9, 16, 1, 0.20, ('orange', 'warn'),
         setback_p=0.30, antenna_p=0.30, rim='line')
    band('void', 16, CITY_H, 9, 20, 0, 0.11, ('orange',),
         setback_p=0.35, antenna_p=0.55, rim='linehi')


    # ---- blinds: slats, pulled to BLIND_H, lit from inside
    slat_bottom = band_top - 1
    for by in range(glass_y0, slat_bottom, 3):
        d.line([glass_x0, by, glass_x1, by], fill=rgba('panel'))
        img.putpixel((rnd.irange(glass_x0, glass_x1), by), rgba('panel2'))
    d.line([glass_x0, slat_bottom, glass_x1, slat_bottom], fill=rgba('linehi'))
    d.line([glass_x0, slat_bottom + 1, glass_x1, slat_bottom + 1], fill=rgba('void'))

    # ---- centre mullion + sill
    d.line([glass_x0 + gw // 2, glass_y0, glass_x0 + gw // 2, glass_y1], fill=rgba('line'))
    d.line([x - 2, row + h, x + w + 2, row + h], fill=rgba('linehi'))
    d.line([x - 2, row + h + 1, x + w + 2, row + h + 1], fill=rgba('void'))


def noticeboard(img, d, x, row, w=84, h=48):
    """Cork board: ragged outer silhouette, crooked papers, one curling corner."""
    from ragged import LCG, ragged_rect, chip_corners, scuff
    rnd = LCG(0xB0A2D)
    ragged_rect(img, x, row, x + w, row + h, 'panel2', 'linehi', seed=901, amp=1)
    # pins row along the top rail
    d.line([x + 2, row + 2, x + w - 2, row + 2], fill=rgba('line'))
    for (px_, py_, pw, ph, tilt) in ((8, 6, 24, 18, 0), (40, 5, 30, 14, 1),
                                     (13, 27, 26, 15, -1), (48, 22, 22, 19, 1)):
        for i in range(ph):                                    # papers are never perfectly level
            off = tilt if (i + py_) % 7 < 4 else 0
            d.line([x + px_ + off, row + py_ + i, x + px_ + pw + off, row + py_ + i],
                   fill=rgba('void'))
        d.rectangle([x + px_, row + py_, x + px_ + pw, row + py_ + ph], outline=rgba('line'))
        d.point((x + px_ + pw // 2, row + py_), fill=rgba('danger'))
        for i in range(4, ph - 2, 5):
            d.line([x + px_ + 4, row + py_ + i, x + px_ + pw - 4, row + py_ + i], fill=rgba('panel2'))
    chip_corners(img, 902, per_corner=1)
    scuff(img, 903, n=3, box=(x, row, x + w, row + h))


def wall_clock(img, d, cx, row, r=11, hand='textdim', tick='muted', dot='signal'):
    d.ellipse([cx - r, row - r, cx + r, row + r], fill=rgba('panel2'), outline=rgba('linehi'))
    d.ellipse([cx - r + 2, row - r + 2, cx + r - 2, row + r - 2], outline=rgba('line'))
    d.line([cx, row, cx, row - 7], fill=rgba(hand))
    d.line([cx, row, cx + 4, row + 3], fill=rgba(hand))
    d.point((cx, row), fill=rgba(dot))
    for (tx, ty) in ((cx, row - 8), (cx, row + 8), (cx - 8, row), (cx + 8, row)):
        d.point((tx, ty), fill=rgba(tick))


def filing_cabinet(img, d, x, floor_row, w=44, h=76, body='panel2', face='panel', edge='line'):
    """Three-drawer cabinet: worn OUTER silhouette only -- the drawer faces stay crisp, because a
    steel box dents at its corners, it does not fray like paper."""
    from ragged import ragged_rect, notch_corner, scuff
    top = floor_row - h
    ragged_rect(img, x, top, x + w, floor_row, body, edge, seed=410, amp=1)
    notch_corner(img, x, top, 1, 1, depth=2, tone='void')
    notch_corner(img, x + w, floor_row, -1, -1, depth=2, tone='void')
    d.line([x + 2, top + 2, x + w - 2, top + 2], fill=rgba('linehi'))
    for i in range(3):
        dy = top + 6 + i * 23
        d.rectangle([x + 5, dy, x + w - 5, dy + 18], fill=rgba(face), outline=rgba(edge))
        d.line([x + 13, dy + 9, x + w - 13, dy + 9], fill=rgba('linehi'))
        d.point((x + w // 2, dy + 9), fill=rgba('textdim'))
        d.line([x + 5, dy, x + w - 5, dy], fill=rgba('linehi'))     # handle rail catch-light
    d.rectangle([x - 2, floor_row, x + w + 2, floor_row + 1], fill=rgba('void'))
    # contact shadow: the floor darkens where the box meets it
    for i in range(3):
        d.line([x - 2 + i, floor_row + 1 + i, x + w + 2 - i, floor_row + 1 + i], fill=rgba('void'))
    scuff(img, 411, n=3, box=(x + 5, top + 4, x + w - 5, floor_row - 2))


def plant_pot(img, d, x, floor_row, h=42, leaf='signaldim', tip='signal'):
    """Contact shadow added 2026-11-26: a pot with no shadow floats in front of the wall."""
    pot_top = floor_row - h
    d.rectangle([x, pot_top + 14, x + 18, floor_row], fill=rgba('panel2'), outline=rgba('line'))
    d.line([x, pot_top + 14, x + 18, pot_top + 14], fill=rgba('linehi'))
    d.line([x - 2, floor_row + 1, x + 20, floor_row + 1], fill=rgba('void'))
    for (dx, dy) in ((0, 14), (-7, 9), (7, 10), (-3, 20), (4, 21), (-9, 16), (9, 17), (-5, 25), (6, 24)):
        d.line([x + 9, pot_top + 14, x + 9 + dx, pot_top + 14 - dy], fill=rgba(leaf))
        d.point((x + 9 + dx, pot_top + 14 - dy), fill=rgba(tip))
    d.rectangle([x - 2, floor_row, x + 20, floor_row + 2], fill=rgba('void'))


def desk_front(img, d, level=0):
    """The desk: surface band + front face. Same construction in every corruption level.

    DEFINED 2026-11-26. The old front face was three flat bands with a tick mark every 44 px, which
    read as "underdefined wall". A desk seen from the player's side is a piece of furniture, so it now
    has: a bevelled front lip (3 steps), a lit top edge that catches the monitor glow, a recessed
    knee-well shadow at the ends, and an apron with one drawer + handle -- the single most recognisable
    desk cue that fits in 18 px of height.
    """
    LIP = 4                              # lit lip thickness, rows DESK_ROW..DESK_ROW+LIP
    APRON_TOP = DESK_ROW + LIP + 1
    d.rectangle([0, DESK_ROW, W - 1, H - 1], fill=rgba('panel2'))

    # ---- front lip: three bevelled steps, so the edge catches light instead of being a line
    seam = 'line' if level < 2 else 'dangerdim'
    d.line([0, DESK_ROW, W - 1, DESK_ROW], fill=rgba(seam))
    d.line([0, DESK_ROW + 1, W - 1, DESK_ROW + 1], fill=rgba('linehi'))
    d.line([0, DESK_ROW + 2, W - 1, DESK_ROW + 2], fill=rgba('line'))

    # ---- apron: one step darker than the lip, with a drawer reserved in the middle
    d.rectangle([0, APRON_TOP, W - 1, H - 1], fill=rgba('panel'))
    d.line([0, APRON_TOP, W - 1, APRON_TOP], fill=rgba('void'))

    # ---- knee-well shadow at both ends: the desk darkens where it turns away from the lamp
    for i in range(26):
        tone = 'void' if i < 12 else 'panel'
        d.line([0, APRON_TOP + 1 + i, W - 1, APRON_TOP + 1 + i], fill=rgba('panel'))
    for i in range(30):
        d.line([i, APRON_TOP + 2, i, H - 1], fill=rgba('void'))
        d.line([W - 1 - i, APRON_TOP + 2, W - 1 - i, H - 1], fill=rgba('void'))
    # re-light the middle of the apron so the darkening reads as a gradient, not a frame
    for i in range(30, W // 2):
        d.line([i, APRON_TOP + 2, i, H - 1], fill=rgba('panel'))
        d.line([W - 1 - i, APRON_TOP + 2, W - 1 - i, H - 1], fill=rgba('panel'))

    # ---- apron detail: two panel joints and a horizontal rail.
    # A centred drawer was tried first and read as a TRAY holding the papers (the props sit directly
    # above it) -- so the front face stays quiet and reads by its planes instead: lip, apron, rail,
    # kick shadow. Definition comes from value steps, not from added clutter.
    d.line([0, APRON_TOP + 9, W - 1, APRON_TOP + 9], fill=rgba('line'))
    d.line([0, APRON_TOP + 10, W - 1, APRON_TOP + 10], fill=rgba('panel2'))
    # NOTE: vertical panel joints were tried at x=168/312 and deleted -- they ran straight through
    # the mouse and the folder, which is exactly the "scrambled" read we were fixing.
    # kick shadow along the floor: the darkest plane, so the desk has a base
    d.rectangle([0, H - 7, W - 1, H - 1], fill=rgba('void'))
    d.line([0, H - 8, W - 1, H - 8], fill=rgba('line'))

    # ---- warm rim only from the off-screen lamp on the left (bible §2)
    if level == 0:
        d.line([0, DESK_ROW, 148, DESK_ROW], fill=rgba('glow_amber'))    # lamp falloff on the lip
        d.line([0, DESK_ROW + 1, 96, DESK_ROW + 1], fill=rgba('paperdim'))
        d.line([0, DESK_ROW + 2, 54, DESK_ROW + 2], fill=rgba('warn'))   # warmest under the lamp
        for i in range(40):                                              # spill onto the apron
            d.line([0, APRON_TOP + 1 + i // 4, 42 - i, APRON_TOP + 1 + i // 4],
                   fill=rgba('panel2' if i > 18 else 'panel'))
    elif level == 2:
        d.line([0, DESK_ROW, 132, DESK_ROW], fill=rgba('dangerdim'))


# --------------------------------------------------------------------------- the three levels
def room(level=0):
    img = new(W, H)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W - 1, H - 1], fill=rgba('void'))
    # ceiling: two flat steps, no hanging fixture (bible §2)
    d.rectangle([0, 0, W - 1, 10], fill=rgba('void'))
    d.line([0, 10, W - 1, 10], fill=rgba('panel'))
    # far wall
    wall = 'panel' if level < 2 else 'panel'
    d.rectangle([0, 16, W - 1, FLOOR_ROW - 1], fill=rgba(wall))
    seam = 'panel2' if level < 2 else 'dangerdim'
    for x in range(0, W, 56):
        d.line([x, 16, x, FLOOR_ROW - 1], fill=rgba(seam))
    # hand-drawn wall head: 1 px steps, so the wall/ceiling join is not a ruled line
    from ragged import LCG as _LCG
    _r = _LCG(0x1A11)
    _y = 16
    for _x in range(0, W):
        if _r.below(0.06):
            _y = max(15, min(17, _y + _r.pick((-1, 1))))
        img.putpixel((_x, _y), rgba(seam))
        if _y < 17:
            img.putpixel((_x, _y + 1), rgba(wall))

    pips = ('warn', 'orange')
    if level == 1:
        pips = ('orange', 'orange')
    elif level == 2:
        pips = ('danger', 'dangerdim')
    # Bigger city, per user 2026-11-26: frame runs from just below the shoulder line (row 145) all the
    # way above the head (row 8) and reads as a proper skyline behind him.
    window_night(img, d, 244, 8, 224, 137)
    noticeboard(img, d, 20, 22, 84, 48)
    wall_clock(img, d, 126, 44,
               hand='textdim' if level == 0 else ('danger' if level == 1 else 'dangerdim'),
               dot='signal' if level == 0 else 'danger')
    filing_cabinet(img, d, 206, FLOOR_ROW)
    plant_pot(img, d, 252, FLOOR_ROW,
              leaf='signaldim' if level == 0 else 'line', tip='signal' if level == 0 else 'linehi')

    # floor strip
    d.rectangle([0, FLOOR_ROW, W - 1, H - 1], fill=rgba('void'))
    d.line([0, FLOOR_ROW, W - 1, FLOOR_ROW], fill=rgba('panel2'))
    desk_front(img, d, level)

    if level >= 1:                                    # scan band, flat
        for y in range(20, DESK_ROW, 22):
            d.rectangle([0, y, W - 1, y + 1], fill=rgba('panel'))
    if level >= 2:                                    # breach: red frame + every 3rd row darkened
        d.rectangle([0, 0, W - 1, 3], fill=rgba('danger'))
        for y in range(0, DESK_ROW, 3):
            d.line([0, y, W - 1, y], fill=rgba('dangerdim'))
    return img


# --------------------------------------------------------------------------- desk work
def keyboard(img, d, cx, row, w=88, h=26):
    """DEFINED desk pass (2026-11-26). The old keyboard was 3 rows of identical key holes, which
    merged into a grille. Now: lit top face, recessed key well, staggered key rows, a real space
    bar, and a shadow under the front lip so it sits on the desk instead of floating on it."""
    x = cx - w // 2
    # contact shadow on the desk
    d.line([x + 2, row + h + 2, x + w - 2, row + h + 2], fill=rgba('void'))
    d.line([x + 6, row + h + 3, x + w - 6, row + h + 3], fill=rgba('panel'))
    # body: lit top edge, dark well, shadowed bottom
    d.rectangle([x, row, x + w, row + h], fill=rgba('panel2'), outline=rgba('line'))
    d.line([x + 1, row + 1, x + w - 1, row + 1], fill=rgba('linehi'))
    d.rectangle([x + 1, row + 2, x + w - 1, row + h - 1], outline=rgba('line'))
    # key well: a recessed plane, one step darker than the body
    wx0, wy0, wx1, wy1 = x + 4, row + 4, x + w - 4, row + h - 5
    d.rectangle([wx0, wy0, wx1, wy1], fill=rgba('void'))
    d.line([wx0, wy0, wx1, wy0], fill=rgba('line'))
    # staggered keys: back row is full width, front rows inset (that is what a keyboard looks like)
    rows = ((wx0 + 1, 13, 6), (wx0 + 3, 12, 6), (wx0 + 5, 11, 6))
    for i, (kx, count, step) in enumerate(rows):
        ky = wy0 + 2 + i * 5
        for c_ in range(count):
            bx = kx + c_ * step
            if bx + 4 > wx1:
                break
            d.rectangle([bx, ky, bx + 4, ky + 3], fill=rgba('panel'))
            d.line([bx, ky, bx + 4, ky], fill=rgba('linehi'))      # lit key top
    # space bar + two modifiers
    d.rectangle([cx - 16, wy1 - 5, cx + 16, wy1 - 2], fill=rgba('panel'))
    d.line([cx - 16, wy1 - 5, cx + 16, wy1 - 5], fill=rgba('linehi'))
    d.rectangle([wx0 + 1, wy1 - 5, wx0 + 7, wy1 - 2], fill=rgba('panel'))
    d.rectangle([wx1 - 7, wy1 - 5, wx1 - 1, wy1 - 2], fill=rgba('panel'))
    # front lip
    d.line([x, row + h - 2, x + w, row + h - 2], fill=rgba('line'))
    d.line([x, row + h, x + w, row + h], fill=rgba('void'))


def mouse(img, d, x, row):
    """Mouse built from row spans so it has a real silhouette. The previous version was a pale disc
    on a dark disc, which read as two coins. Crown is narrow, body is wide, and the split runs only
    down the front third -- that is what makes it read as a mouse and not an oval."""
    d.line([x + 1, row + 19, x + 12, row + 19], fill=rgba('void'))       # contact shadow
    # BODY TONE: 'line', not 'panel2' -- the desk surface itself is panel2, so the first version of
    # this mouse was the same value as the desk and only its edges showed (it read as a plus sign).
    spans = ((4, 11), (2, 13), (1, 14), (1, 14), (0, 15), (0, 15), (0, 15), (0, 15),
             (0, 15), (1, 14), (1, 14), (2, 13), (3, 12), (4, 11), (5, 10), (6, 9))
    for i, (a, b) in enumerate(spans):
        d.line([x + a, row + i, x + b, row + i], fill=rgba('line'))
    d.line([x + 4, row, x + 11, row], fill=rgba('linehi'))               # lit crown
    d.line([x + 1, row + 2, x + 1, row + 13], fill=rgba('linehi'))       # lit left edge
    d.line([x + 14, row + 3, x + 14, row + 13], fill=rgba('panel2'))     # shaded right edge
    d.line([x + 7, row + 1, x + 7, row + 6], fill=rgba('void'))          # button split
    d.line([x, row + 7, x + 15, row + 7], fill=rgba('linehi'))           # button/body seam
    d.point((x + 7, row + 9), fill=rgba('linehi'))                       # scroll notch
    d.line([x - 6, row + 1, x - 1, row + 2], fill=rgba('line'))          # cable


def coffee(img, d, x, row):
    """Mug: cylinder with a lit rim, liquid line, handle, two-step contact shadow, 1 wisp of steam.
    The old one was a rectangle with a wire handle -- the biggest single 'undefined' blob on the desk."""
    d.ellipse([x - 1, row + 22, x + 23, row + 27], fill=rgba('void'))          # contact shadow
    d.rectangle([x, row + 2, x + 22, row + 24], fill=rgba('panel2'))
    d.line([x, row + 2, x, row + 24], fill=rgba('linehi'))                    # lit left edge
    d.line([x + 22, row + 2, x + 22, row + 24], fill=rgba('line'))            # shadow right edge
    d.ellipse([x, row, x + 22, row + 6], fill=rgba('panel2'), outline=rgba('linehi'))
    d.ellipse([x + 2, row + 2, x + 20, row + 5], fill=rgba('ink'))            # coffee inside
    d.line([x + 4, row + 3, x + 12, row + 3], fill=rgba('paperdim'))          # surface catch-light
    # handle: a C, not a loop of wire
    d.line([x + 23, row + 8, x + 27, row + 9], fill=rgba('linehi'))
    d.line([x + 27, row + 9, x + 27, row + 16], fill=rgba('linehi'))
    d.line([x + 27, row + 16, x + 23, row + 17], fill=rgba('linehi'))
    d.line([x + 24, row + 10, x + 25, row + 10], fill=rgba('line'))
    d.line([x + 24, row + 15, x + 25, row + 15], fill=rgba('line'))
    # steam: two soft columns, tapering
    for i, sx in enumerate((x + 7, x + 14)):
        d.line([sx, row - 3 - i, sx + 1, row - 8], fill=rgba('line'))
        d.point((sx + 1, row - 9), fill=rgba('linehi'))
        d.point((sx + 2, row - 11), fill=rgba('line'))


def notepad_and_pen(img, d, x, row):
    d.rectangle([x, row, x + 40, row + 30], fill=rgba('paper'), outline=rgba('ink'))
    d.line([x + 1, row + 4, x + 39, row + 4], fill=rgba('ink'))
    for i in range(4):
        y = row + 9 + i * 5
        d.line([x + 5, y, x + 33 - (i % 2) * 9, y], fill=rgba('paperdim'))
    # pen: short, nearly flat, with a nib -- a long diagonal here read as a scratch on the desk
    d.line([x + 44, row + 27, x + 56, row + 24], fill=rgba('warn'))
    d.point((x + 57, row + 23), fill=rgba('linehi'))
    d.point((x + 43, row + 27), fill=rgba('ink'))


def folder_stack(img, d, x, row):
    """Manila folder with papers inside -- not a stack of white slabs.

    The old version was three overlapping white rectangles, which at phone size read as one bright
    undefined block: the single loudest thing on the desk. Now the FOLDER is the silhouette (paperdim,
    with a tab) and only a thin edge of paper shows, so the mass stays quiet.
    """
    # contact shadow
    d.line([x + 2, row + 32, x + 60, row + 32], fill=rgba('void'))
    d.line([x + 6, row + 33, x + 56, row + 33], fill=rgba('panel'))
    # paper peeking out of the folder: offset left and only 4 px proud, so it reads as a sheet
    # INSIDE the folder rather than a card floating above it
    d.rectangle([x + 2, row + 6, x + 50, row + 20], fill=rgba('paper'), outline=rgba('paperdim'))
    d.line([x + 2, row + 6, x + 50, row + 6], fill=rgba('paperdim'))
    for i in range(3):
        d.line([x + 5 + i, row + 10 + i * 3, x + 44 - i * 7, row + 10 + i * 3], fill=rgba('paperdim'))
    # folder body with a tab on the back edge
    d.rectangle([x + 4, row + 10, x + 12, row + 17], fill=rgba('paperdim'), outline=rgba('ink'))
    d.rectangle([x, row + 14, x + 58, row + 30], fill=rgba('paperdim'), outline=rgba('ink'))
    d.line([x + 1, row + 15, x + 57, row + 15], fill=rgba('warn'))      # lit folder edge
    d.line([x + 1, row + 29, x + 57, row + 29], fill=rgba('ink'))
    # label block on the folder face (text is TMP in game; this is the plate)
    d.rectangle([x + 6, row + 19, x + 30, row + 26], fill=rgba('paper'), outline=rgba('ink'))
    d.line([x + 8, row + 21, x + 26, row + 21], fill=rgba('paperdim'))
    d.line([x + 8, row + 24, x + 21, row + 24], fill=rgba('paperdim'))


def sticky_notes(img, d, x, row):
    for i, col in enumerate(('warn', 'paperdim')):
        d.rectangle([x, row + i * 12, x + 15, row + 10 + i * 12], fill=rgba(col), outline=rgba('ink'))
        d.line([x + 3, row + i * 12 + 4, x + 11, row + i * 12 + 4], fill=rgba('ink'))


def clipboard(img, d, x, row):
    d.rectangle([x, row, x + 34, row + 40], fill=rgba('panel'), outline=rgba('linehi'))
    d.rectangle([x + 3, row + 6, x + 31, row + 36], fill=rgba('paper'), outline=rgba('ink'))
    d.rectangle([x + 11, row - 2, x + 23, row + 3], fill=rgba('linehi'), outline=rgba('ink'))
    for i in range(5):
        d.line([x + 7, row + 11 + i * 5, x + 27 - (i % 3) * 6, row + 11 + i * 5], fill=rgba('paperdim'))


def desk_work():
    """Props on the desk + the front lip. 480x64 band at the bottom of the screen.

    PROPS ARE PLACED AROUND THE MONITOR ARM (2026-11-26). The arm occupies x 63..119 and the monitor
    case x 14..168, so nothing that sits tall on the desk may be placed under them -- the keyboard
    used to be centred at x=104 and covered the lower half of the arm. The desk now reads:

        x 12    notepad + pen        (clear of the arm, left of it)
        x 63    [monitor arm]        <- kept clear on purpose
        x 156   keyboard + mouse     (right of the monitor)
        x 268   sticky notes
        x 300   folder stack         paperwork in front of the applicant
        x 380   clipboard
        x 440   coffee               (comfort, far right)

    Everything is drawn with a contact shadow (bible 7b) and grouped, never one prop per band
    (bible 7c).
    """
    img = Image.new('RGBA', (W, DESK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    base = 6

    notepad_and_pen(img, d, 12, base + 2)
    keyboard(img, d, cx=200, row=base + 1)
    mouse(img, d, 250, base + 4)
    sticky_notes(img, d, 268, base + 3)
    folder_stack(img, d, 300, base - 2)
    clipboard(img, d, 374, base - 4)
    coffee(img, d, 436, base + 2)
    scuff(img, 301, n=12, tones=('panel', 'void'))

    # ---- front lip at the bottom of the band: lit bevel, apron, kick shadow
    LIP_Y = DESK_H - 19
    d.line([0, LIP_Y, W - 1, LIP_Y], fill=rgba('void'))
    d.line([0, LIP_Y + 1, W - 1, LIP_Y + 1], fill=rgba('linehi'))
    d.line([0, LIP_Y + 2, W - 1, LIP_Y + 2], fill=rgba('line'))
    d.rectangle([0, LIP_Y + 3, W - 1, DESK_H - 8], fill=rgba('panel'))
    d.line([0, DESK_H - 9, W - 1, DESK_H - 9], fill=rgba('line'))
    d.line([0, DESK_H - 8, W - 1, DESK_H - 8], fill=rgba('panel2'))
    d.rectangle([0, DESK_H - 7, W - 1, DESK_H - 1], fill=rgba('void'))
    d.line([0, LIP_Y + 1, 148, LIP_Y + 1], fill=rgba('glow_amber'))
    d.line([0, LIP_Y + 2, 96, LIP_Y + 2], fill=rgba('paperdim'))
    return img


# --------------------------------------------------------------------------- small sprites, 1 px crisp
def monitor_small():
    """The CRT. DEFINED pass, 2026-11-26.

    The RAGGED pass had wrecked this: `ragged_rect(..., amp=1)` on a 154x106 box made every edge a
    1 px dotted run with 2 px gaps, so the monitor read as debris rather than a machine. It is the
    biggest object on screen and the player reads the game off it -- hardness is the point.

    New construction, all straight, nothing dotted:
        outer case   - ONE dented edge (bottom-right), 2 chipped corners, 1 px lit top edge
        bezel face   - a real 5 px frame in `panel2` with a lit bevel top-left, shade bottom-right
        glass        - `void`, 1 px `line` inner frame, one diagonal glare streak, 4 dead pixels
        furniture    - camera dot, power LED, a small brand plate under the glass
    Wear is now the 4 loose scuffs and ONE dent -- not the silhouette.
    """
    w, h = MON_W, MON_H
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # ---- outer case: straight edges, a 1 px lit top and shaded bottom, one dent on the right
    d.rectangle([0, 0, w - 1, h - 1], fill=rgba('panel2'))
    d.rectangle([0, 0, w - 1, h - 1], outline=rgba('line'))
    d.line([1, 0, w - 2, 0], fill=rgba('linehi'))          # case catches the room light on top
    d.line([1, h - 1, w - 2, h - 1], fill=rgba('void'))    # and is dark underneath
    for i in range(4):                                     # one dent, right edge, low
        d.point((w - 1 - (i // 2), h - 12 + i), fill=rgba('void'))

    # ---- bezel face: 5 px frame, bevel lit top-left / shaded bottom-right
    bx0, by0, bx1, by1 = 1, 1, w - 2, h - 2
    d.rectangle([bx0, by0, bx1, by1], fill=rgba('panel'), outline=rgba('line'))
    d.line([bx0 + 1, by0 + 1, bx1 - 1, by0 + 1], fill=rgba('linehi'))
    d.line([bx0 + 1, by0 + 1, bx0 + 1, by1 - 1], fill=rgba('linehi'))
    d.line([bx0 + 1, by1 - 1, bx1 - 1, by1 - 1], fill=rgba('void'))
    d.line([bx1 - 1, by0 + 1, bx1 - 1, by1 - 1], fill=rgba('void'))

    # ---- glass: straight, one step darker than the frame at its edge
    gx0, gy0, gx1, gy1 = 6, 6, w - 7, h - 13
    d.rectangle([gx0 - 1, gy0 - 1, gx1 + 1, gy1 + 1], fill=rgba('line'))   # recess shadow
    d.rectangle([gx0, gy0, gx1, gy1], fill=rgba('void'))

    # ---- NO glare streak. A diagonal slash across the glass was drawn, reviewed and removed: it
    # reads as a scratch, and it competes with the email text the player is reading. The CRT curve is
    # sold by a single lit row along the glass top and a shaded row along its bottom instead.
    d.line([gx0 + 1, gy0, gx1 - 1, gy0], fill=rgba('panel'))
    d.line([gx0 + 1, gy1, gx1 - 1, gy1], fill=rgba('panel'))

    # ---- dead pixels: 4 only, and clustered, so it reads as a failing panel not static
    for (dpx, dpy) in ((gx1 - 18, gy0 + 12), (gx1 - 17, gy0 + 12), (gx1 - 18, gy0 + 13),
                       (gx0 + 24, gy1 - 9)):
        d.point((dpx, dpy), fill=rgba('panel2'))

    # ---- bezel furniture: camera dot + power LED + brand plate
    d.rectangle([w // 2 - 1, 3, w // 2 + 1, 4], fill=rgba('line'))
    d.point((w - 9, 4), fill=rgba('signal'))
    d.point((w - 8, 4), fill=rgba('signaldim'))
    d.rectangle([w // 2 - 9, h - 9, w // 2 + 9, h - 6], fill=rgba('panel2'), outline=rgba('linehi'))
    d.line([w // 2 - 7, h - 8, w // 2 + 7, h - 8], fill=rgba('panel'))

    # ---- wear: 4 loose scuffs on the bezel only (never on the glass)
    scuff(img, 104, n=4, tones=('panel', 'void'), box=(2, 2, w - 3, h - 3))
    # NOTE: no stand here -- see monitor_stand(). Sorting means a stand inside this sprite can
    # never reach the desk. (BUG-025)
    return img


def monitor_stand(w=56, h=34):
    """The ARM that holds the screen. Redrawn 2026-11-26 (user: "the one I wanted you to fix was the
    monitor stand or the arm holding the screen").

    What was wrong: the old 44x26 sprite was a ragged column plus one ellipse, with a ragged 1 px
    outline on every edge -- at 8x it looked like scratched glass, not hardware. It also had no
    articulation, so the screen read as balanced on a stick.

    What a real monitor arm is, in 34 px, bottom to top:
        base plate    - wide, low, bevelled, contact shadow underneath
        lower column  - short and thick, two value steps (lit left, shaded right)
        joint         - a visible collar, 2 px wider than the column
        upper column  - thinner, offset 2 px (the arm leans slightly; nothing is plumb)
        mount plate   - a widening wedge that meets the bottom of the bezel
    Nothing is dotted and nothing is ragged: hardware edges are straight.
    """
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = w // 2

    # ---- base plate: 3 rows of shadow, then a bevelled foot
    for i in range(3):
        d.line([cx - 22 - i, h - 1 - i, cx + 22 + i, h - 1 - i], fill=rgba('void'))
    d.rectangle([cx - 21, h - 9, cx + 21, h - 4], fill=rgba('panel2'), outline=rgba('line'))
    d.line([cx - 20, h - 9, cx + 20, h - 9], fill=rgba('linehi'))     # lit front edge
    d.line([cx - 20, h - 4, cx + 20, h - 4], fill=rgba('void'))       # shadow under the foot
    d.point((cx + 14, h - 7), fill=rgba('signal'))                    # standby LED

    # ---- lower column: short, thick, two value steps
    d.rectangle([cx - 6, h - 20, cx + 6, h - 9], fill=rgba('panel'))
    d.line([cx - 6, h - 20, cx - 6, h - 9], fill=rgba('linehi'))
    d.line([cx + 6, h - 20, cx + 6, h - 9], fill=rgba('line'))
    d.line([cx - 5, h - 19, cx + 5, h - 19], fill=rgba('panel2'))

    # ---- joint collar, 2 px wider than the columns -- this is what makes it read as an arm
    d.rectangle([cx - 8, h - 24, cx + 8, h - 20], fill=rgba('panel2'), outline=rgba('line'))
    d.line([cx - 7, h - 23, cx + 7, h - 23], fill=rgba('linehi'))

    # ---- upper column: thinner, and offset 2 px so the arm leans instead of being plumb
    d.rectangle([cx - 2, h - 32, cx + 8, h - 24], fill=rgba('panel'))
    d.line([cx - 2, h - 32, cx - 2, h - 24], fill=rgba('linehi'))
    d.line([cx + 8, h - 32, cx + 8, h - 24], fill=rgba('line'))

    # ---- mount plate: widens to meet the bezel, with a screw head on each side
    d.rectangle([cx - 11, h - 34, cx + 13, h - 30], fill=rgba('panel2'), outline=rgba('line'))
    d.line([cx - 10, h - 34, cx + 12, h - 34], fill=rgba('linehi'))
    d.point((cx - 8, h - 32), fill=rgba('line'))
    d.point((cx + 10, h - 32), fill=rgba('line'))

    # wear: two scuffs on the column ONLY (hardware, not paper)
    scuff(img, 132, n=2, tones=('panel2', 'void'), box=(cx - 5, h - 30, cx + 6, h - 10))
    return img


def document_small():
    w, h = DOC_W, DOC_H
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], fill=rgba('paper'), outline=rgba('ink'))
    # PAPER RULE (bible §4b): sheets get torn EDGES, never a stepped fill -- a stepped fill makes the
    # page look chewed. Tear the bottom and both sides by 1 px, occasionally 2.
    rnd = LCG(202)
    for x in range(w):
        if rnd.below(0.5):
            for k in range(rnd.irange(1, 2)):
                yy = h - 1 - k
                if 0 <= yy < h:
                    img.putpixel((x, yy), (0, 0, 0, 0))
    for y in range(h):
        if rnd.below(0.28):
            img.putpixel((0, y), (0, 0, 0, 0))
        if rnd.below(0.28):
            img.putpixel((w - 1, y), (0, 0, 0, 0))
    # the header rule that never printed straight
    for x in range(7, w - 8):
        y = 8 + (1 if (x // 11) % 4 == 0 else 0)
        img.putpixel((x, y), rgba('ink'))
    y = 14
    for i in range(6):
        ln = (w - 26) if i % 4 else (w - 50)
        d.line([7, y, 7 + ln, y], fill=rgba('paperdim'))
        y += 6
    d.rectangle([w - 18, 3, w - 12, 7], outline=rgba('muted'))    # paper clip
    return img


# --------------------------------------------------------------------------- main
def main():
    made = []
    for level in (0, 1, 2):
        p = out(f'backgrounds/office_L{level}.png')
        room(level).save(p)
        made.append(p)
    p = out('desk/desk_foreground.png')
    desk_work().save(p); made.append(p)
    p = out('desk/monitor_small.png')
    monitor_small().save(p); made.append(p)
    p = out('desk/monitor_stand.png')
    monitor_stand().save(p); made.append(p)
    p = out('desk/document_small.png')
    document_small().save(p); made.append(p)

    # palette gate over everything we just wrote
    vals = set(PALETTE.values())
    bad = 0
    for p in made:
        im = Image.open(p).convert('RGBA')
        for y in range(im.height):
            for x in range(im.width):
                px = im.getpixel((x, y))
                if px[3] == 0:
                    continue
                if px[3] != 255 or px[:3] not in vals:
                    bad += 1
    for p in made:
        print('wrote', os.path.relpath(p, ROOT))
    print(f'off-palette px: {bad}')
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
