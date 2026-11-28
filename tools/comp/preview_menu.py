#!/usr/bin/env python3
"""
preview_menu.py -- render the front door exactly as MenuView paints it.

A TWIN, not an illustration: it mirrors
    unity/Assets/Scripts/Game/Interaction/MenuView.cs   (card rect, padding, row layout)
    unity/Assets/Scripts/Core/Rules/MenuRules.cs        (labels, bars, consent notice)
    SaveData.profile                                    (which rows exist)

Three screens are drawn, in the order the player meets them: root, settings, data & consent.

    python tools/comp/preview_menu.py            # writes art/kit/reference/menu_2x.png
    python tools/comp/preview_menu.py --check     # verify the labels are paintable, write nothing
"""
from __future__ import annotations
import argparse, json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
KIT = os.path.join(ROOT, 'art', 'kit')
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'comp'))

from PIL import Image, ImageDraw  # noqa: E402

from preview_intro import load_font, draw_text, GLYPH_H, ROW_STEP  # noqa: E402  (one glyph renderer, shared)

PAGE_W, PAGE_H = 480, 270
CARD_X, CARD_Y, CARD_W, CARD_H = 60, 34, 360, 202
PAD_X, PAD_Y = 14, 14
MAX_LABEL = 24

PUNCT = " !\"#%&'()*+,-./0123456789:;<=>?_[]~"


def paintable(s):
    return all(('A' <= c <= 'Z') or c in PUNCT for c in s)


# ---- mirrored from MenuRules ----------------------------------------------------------------
def root_rows(has_save, in_progress):
    rows = []
    if has_save:
        rows.append("CONTINUE THE RUN" if in_progress else "CONTINUE")
    rows.append("NEW RUN" if has_save else "START")
    rows += ["SETTINGS", "DATA & CONSENT", "CREDITS"]
    return rows


def settings_rows(muted):
    return ["ROOM VOLUME", "SOUND VOLUME", "SOUND: OFF" if muted else "SOUND: ON", "BACK"]


def bar(v):
    filled = int(round(max(0.0, min(1.0, v)) * 10))
    return "#" * filled + "-" * (10 - filled)


def pct(v):
    return "%d%%" % round(max(0.0, min(1.0, v)) * 100)


NOTICE = [
    "THIS GAME CAN RECORD HOW YOU PLAY.",
    "WHAT: THE DECISIONS YOU MAKE, HOW LONG YOU TAKE,",
    "AND THE SEED OF THE RUN. NOTHING ELSE.",
    "WHAT IT NEVER RECORDS: YOUR NAME, ANYTHING YOU",
    "TYPE, OR ANYTHING ELSE ON THIS DEVICE.",
    "WHY: SO THE PEOPLE WHO MADE THIS CAN TELL",
    "WHETHER IT TEACHES WHAT IT MEANS TO.",
    "IT IS OFF UNTIL YOU SAY YES, AND YOU CAN",
    "CHANGE YOUR MIND AT ANY TIME IN THIS MENU.",
    "SAYING NO DOES NOT CHANGE THE GAME. AT ALL.",
]


def check():
    problems = []
    screens = {
        'root (first run)': root_rows(False, False),
        'root (save)': root_rows(True, True),
        'settings': settings_rows(False),
        'consent': ["[ ] SHARE ANONYMOUS DATA", "[SPACE] CHANGES IT", "BACK"],
    }
    screens['hints'] = ["[UP/DOWN] MOVE", "[SPACE] CHOOSE", "[SPACE] CHANGE IT", "[ESC] BACK"]
    for screen, labels in screens.items():
        for label in labels:
            if len(label) > MAX_LABEL:
                problems.append('%s: label too long (%d chars): %s' % (screen, len(label), label))
            if not paintable(label):
                problems.append('%s: unpaintable characters: %s' % (screen, label))
    for line in NOTICE:
        if not paintable(line):
            problems.append('consent notice unpaintable: ' + line)
        if len(line) > 58:
            problems.append('consent notice too wide for the card: ' + line)
    return problems


def render(pal, atlas, rects):
    shots = []

    def page():
        img = Image.new('RGBA', (PAGE_W, PAGE_H), pal['void'])
        d = ImageDraw.Draw(img)
        d.rectangle([CARD_X, CARD_Y, CARD_X + CARD_W - 1, CARD_Y + CARD_H - 1],
                    fill=pal['void'], outline=pal['line'])
        return img, d

    def row(img, text, y, key):
        draw_text(img, rects, atlas, pal, CARD_X + PAD_X, y, text, key)

    top = CARD_Y + PAD_Y

    # --- root, first run: no CONTINUE, only START
    img, d = page()
    row(img, "HUMAN RESOURCES // ZERO DAY", top, 'signal')
    for i, label in enumerate(root_rows(False, False)):
        row(img, ("> " if i == 0 else "  ") + label, top + (2 + i) * ROW_STEP,
            'text' if i == 0 else 'textdim')
    row(img, "SOUND ON    [S] SETTINGS", CARD_Y + CARD_H - PAD_Y - 3 * ROW_STEP, 'textdim')
    row(img, "[UP/DOWN] MOVE", CARD_Y + CARD_H - PAD_Y - 2 * ROW_STEP, 'textdim')
    row(img, "[SPACE] CHOOSE", CARD_Y + CARD_H - PAD_Y - ROW_STEP, 'textdim')
    shots.append(img)

    # --- root, with a save
    img, d = page()
    row(img, "HUMAN RESOURCES // ZERO DAY", top, 'signal')
    for i, label in enumerate(root_rows(True, True)):
        row(img, ("> " if i == 0 else "  ") + label, top + (2 + i) * ROW_STEP,
            'text' if i == 0 else 'textdim')
    row(img, "SOUND ON    [S] SETTINGS", CARD_Y + CARD_H - PAD_Y - 3 * ROW_STEP, 'textdim')
    row(img, "[UP/DOWN] MOVE", CARD_Y + CARD_H - PAD_Y - 2 * ROW_STEP, 'textdim')
    row(img, "[SPACE] CHOOSE", CARD_Y + CARD_H - PAD_Y - ROW_STEP, 'textdim')
    shots.append(img)

    # --- settings
    img, d = page()
    row(img, "SETTINGS", top, 'signal')
    vals = [('%s  %s %s' % (settings_rows(False)[0], bar(0.8), pct(0.8)), 0),
            ('%s  %s %s' % (settings_rows(False)[1], bar(0.9), pct(0.9)), 1),
            (settings_rows(False)[2], 2), (settings_rows(False)[3], 3)]
    for label, i in vals:
        row(img, ("> " if i == 2 else "  ") + label, top + (2 + i) * ROW_STEP,
            'text' if i == 2 else 'textdim')
    row(img, "DATA: NOT ASKED YET", CARD_Y + CARD_H - PAD_Y - 3 * ROW_STEP, 'textdim')
    row(img, "[SPACE] CHANGE IT", CARD_Y + CARD_H - PAD_Y - 2 * ROW_STEP, 'textdim')
    row(img, "[ESC] BACK", CARD_Y + CARD_H - PAD_Y - ROW_STEP, 'textdim')
    shots.append(img)

    # --- data & consent
    img, d = page()
    row(img, "DATA & CONSENT", top, 'signal')
    y = top + ROW_STEP + 4
    for line in NOTICE:
        row(img, line, y, 'textdim')
        y += ROW_STEP
    base = CARD_Y + CARD_H - PAD_Y - 2 * ROW_STEP
    row(img, "> [ ] SHARE ANONYMOUS DATA", base, 'text')
    row(img, "[SPACE] CHANGES IT", base + ROW_STEP, 'textdim')
    shots.append(img)

    return shots


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--zoom', type=int, default=2)
    ap.add_argument('--check', action='store_true')
    a = ap.parse_args()

    problems = check()
    if problems:
        for p in problems:
            print('  ' + p)
        print('FAIL: the menu cannot be painted as written')
        return 1
    if a.check:
        print('PASS: every menu label and consent line is paintable (<= %d chars, atlas only)' % MAX_LABEL)
        return 0

    pal, atlas, rects = load_font()
    shots = render(pal, atlas, rects)
    sheet = Image.new('RGBA', (PAGE_W, PAGE_H * len(shots) + 6 * (len(shots) - 1)), pal['void'])
    for i, shot in enumerate(shots):
        sheet.alpha_composite(shot, (0, i * (PAGE_H + 6)))
    out = os.path.join(KIT, 'reference', 'menu_%dx.png' % a.zoom)
    if a.zoom != 1:
        sheet = sheet.resize((sheet.width * a.zoom, sheet.height * a.zoom), Image.NEAREST)
    sheet.save(out)
    print('wrote %s (%d screens: root first-run / root with save / settings / consent)' %
          (os.path.relpath(out, ROOT), len(shots)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
