#!/usr/bin/env python3
"""
check_ascii.py -- every script and source file in the repo must be pure ASCII.

WHY (BUG-027, v130)
    The player's art gate died on Windows with

        python.exe : Traceback (most recent call last):

    and the cause was not Python: eight .py files and every .ps1 carried a non-ASCII character in a COMMENT
    (an arrow, a section sign). PowerShell 5.1 reads a script in the ANSI codepage, so one UTF-8 byte
    sequence it cannot map turns the file into mojibake and the parser refuses it before line one runs. The
    gate that was supposed to protect the build was killed by a comment.

    A comment is documentation for humans -- but a comment can still stop a script from parsing. So: ASCII, or
    it does not ship.

USAGE
    python tools/check_ascii.py            # check every tracked .py/.ps1/.cs/.json/.md
    python tools/check_ascii.py --fix      # replace the usual suspects with ASCII and rewrite in place

Exit codes: 0 clean, 1 offenders found.
"""
from __future__ import annotations

import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXTS = ('.py', '.ps1', '.cs', '.json', '.md', '.asmdef', '.yml', '.yaml')

# the replacements that keep the meaning and lose the byte
REPLACEMENTS = {
    '\u2192': '->', '\u2190': '<-', '\u00a7': '#', '\u2014': '--', '\u2013': '-',
    '\u2026': '...', '\u2018': "'", '\u2019': "'", '\u201c': '"', '\u201d': '"',
    '\u00d7': 'x', '\u2265': '>=', '\u2264': '<=', '\u2260': '!=', '\u00b0': 'deg',
    '\u00b7': '-', '\u2022': '*', '\u2713': 'ok', '\u2717': 'x', '\u00b1': '+/-',
    # the block glyphs SecurityRating's own comment uses to draw a five-cell bar in prose
    '\u25ae': '#', '\u25af': '-', '\u2588': '#', '\u2591': '.', '\u25a0': '#', '\u25a1': '-',
    # box drawing: the docs use tables and trees; a box is not worth a parse failure
    '\u2500': '-', '\u2502': '|', '\u250c': '+', '\u2510': '+', '\u2514': '+', '\u2518': '+',
    '\u251c': '+', '\u2524': '+', '\u252c': '+', '\u2534': '+', '\u253c': '+',
    # marks and symbols that appear in the checklists
    '\u2705': '[x]', '\u274c': '[ ]', '\u2610': '[ ]', '\u2248': '~', '\u2212': '-',
    '\u2b1c': '[ ]', '\u25bc': 'v', '\u25ba': '>', '\u25b6': '>', '\u2194': '<->', '\u21c4': '<->',
    '\u26a0': '!', '\u00bd': '1/2', '\u20ac': 'EUR', '\u00e2': 'a', '\ufe0f': '', '\ufeff': '',
}


def tracked_files() -> list[str]:
    try:
        out = subprocess.run(['git', '-C', ROOT, 'ls-files'], capture_output=True, text=True, check=True).stdout
        return [f for f in out.split('\n') if f]
    except (OSError, subprocess.CalledProcessError):
        found = []
        for base, dirs, files in os.walk(ROOT):
            dirs[:] = [d for d in dirs if d not in ('.git', 'Library', 'Temp', 'obj', 'bin', '__pycache__')]
            for f in files:
                found.append(os.path.relpath(os.path.join(base, f), ROOT))
        return found


def main() -> int:
    fix = '--fix' in sys.argv
    offenders = []
    for rel in tracked_files():
        if not rel.lower().endswith(EXTS):
            continue
        path = os.path.join(ROOT, rel)
        if not os.path.isfile(path):
            continue
        raw = open(path, 'rb').read()
        bom = raw.startswith(b'\xef\xbb\xbf')
        try:
            text = raw.decode('utf-8-sig')
        except UnicodeDecodeError:
            offenders.append((rel, 0, 'not valid UTF-8'))
            continue
        if all(ord(c) < 128 for c in text):
            continue
        bad = [(i + 1, ln) for i, ln in enumerate(text.splitlines()) if any(ord(c) > 127 for c in ln)]
        offenders.append((rel, bad[0][0] if bad else 0,
                          '; '.join('line %d: %r' % (n, ln.strip()[:60]) for n, ln in bad[:2])))
        if fix:
            new = text
            for k, v in REPLACEMENTS.items():
                new = new.replace(k, v)
            if all(ord(c) < 128 for c in new):
                data = new.encode('utf-8')
                open(path, 'wb').write((b'\xef\xbb\xbf' + data) if bom else data)
                print('fixed', rel)

    if not offenders:
        print('PASS: every tracked script, source, data and doc file is pure ASCII')
        return 0
    for rel, line, why in offenders:
        print('  NON-ASCII  %-52s %s' % (rel, why))
    print('FAIL: %d file(s) carry non-ASCII characters. PowerShell 5.1 will not parse these on Windows.'
          % len(offenders))
    print('      run: python tools/check_ascii.py --fix')
    return 1


if __name__ == '__main__':
    sys.exit(main())
