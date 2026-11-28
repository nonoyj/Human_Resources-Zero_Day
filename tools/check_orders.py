#!/usr/bin/env python3
"""
check_orders.py -- the standing orders folder is INTACT.

WHY THIS EXISTS
    The player's instruction, twice: "Keep the repo in undeletable memory" and "make a undeletable what we
    learned list ... which you check from time to time or when an error comes". A memory that a single careless
    `git clean` or a bad merge can delete is not memory. So the folder is GATED: every order file must exist,
    must be non-trivial, and must contain the sections that make it usable.

    This gate cannot stop a person from deleting a file. What it can do is make the deletion FAIL THE BUILD in
    the same minute, so it is impossible to ship a round that quietly dropped the player's instructions.

USAGE
    python tools/check_orders.py
Exit codes: 0 intact, 1 something is missing or gutted.
"""
from __future__ import annotations

import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ORDERS = os.path.join(ROOT, 'orders')

# file -> (minimum characters, a phrase that must appear)
REQUIRED = {
    '00-READ-FIRST.md': (1500, 'four-heading report'),
    '01-art-rules.md': (800, ''),
    '02-report-format.md': (800, ''),
    '03-unity-and-bundle.md': (800, ''),
    '04-IMPORTANT-MEMORY.md': (3000, 'Keep the repo in undeletable memory'),
    '05-phases.md': (1500, 'P'),
    '06-art-and-light-research.md': (4000, 'VALUE IS THE HIERARCHY'),
    '07-THE-REPO.md': (3000, 'undeletable'),
    '08-angles-and-placement.md': (4000, 'ONE EYE, ONE HORIZON, ONE GRID'),
    '09-what-we-learned.md': (4000, 'WHAT WE LEARNED'),
    'CHECKLIST.md': (1000, 'What I did'),
}


def main() -> int:
    if not os.path.isdir(ORDERS):
        print('FAIL: the orders folder is gone. It is the player\'s standing instructions -- restore it from git.')
        return 1
    problems = []
    for name, (floor, phrase) in REQUIRED.items():
        path = os.path.join(ORDERS, name)
        if not os.path.isfile(path):
            problems.append('%s is missing' % name)
            continue
        text = open(path, encoding='utf-8', errors='replace').read()
        if len(text) < floor:
            problems.append('%s is %d chars, under the %d floor -- it has been gutted'
                            % (name, len(text), floor))
        if phrase and phrase not in text:
            problems.append('%s no longer contains %r' % (name, phrase))
    if problems:
        for p in problems:
            print('  ORDER  ' + p)
        print('FAIL: %d problem(s) in orders/ -- the standing instructions are not intact' % len(problems))
        return 1
    print('PASS: %d order files intact (%s)'
          % (len(REQUIRED), ', '.join(sorted(REQUIRED))[:60] + '...'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
