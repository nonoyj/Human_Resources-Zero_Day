#!/usr/bin/env python3
"""HR//0D -- does the COMMIT work on a machine that has never seen this project?

WHY THIS EXISTS

Six bugs in a row (BUG-042, 044, 045, 046, 047, 048) shared one root cause: **my checks read the working
directory, and the player runs the commit.** Anything the working directory had but the commit did not --
a gitignored art folder, a .meta file Unity generated an hour ago, frames an editor menu cut -- passed my
gate and failed on his machine. Each was found separately, one crash at a time.

This script closes the whole class at once. It exports HEAD with `git archive` into a temp folder (so it
contains exactly what the commit contains and nothing else), then runs the project's own verification
INSIDE that folder. If the committed tree cannot verify itself, this fails here instead of on the player's
machine.

    python tools/verify_fresh_clone.py            # export HEAD, verify it in isolation
    python tools/verify_fresh_clone.py --keep     # leave the temp folder for inspection
"""
from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import tarfile
import tempfile

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))

# Every check that must pass on a fresh clone. Ordered cheapest first, so a failure is a fast failure.
CHECKS = [
    ('sprites + both mirrors',      ['tools/art/publish_to_unity.py', '--verify']),
    ('animation frames',            ['tools/art/extract_frames.py', '--check']),
    ('unity import settings',       ['tools/art/write_unity_metas.py', '--check']),
    ('art palette + alpha',         ['tools/art/make_applicants.py', '--check']),
    ('applicant proportions',       ['tools/art/check_proportions.py']),
    ('font atlas',                  ['tools/art/make_font.py', '--check']),
    ('manifest matches disk',       ['tools/art/make_sprite_manifest.py', '--verify']),
]


def export_head(dest: str) -> int:
    """git archive HEAD -> dest. Exactly the commit, no working-tree surprises."""
    proc = subprocess.run(['git', '-C', ROOT, 'archive', '--format=tar', 'HEAD'],
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if proc.returncode != 0:
        print('FAIL: could not export HEAD: ' + proc.stderr.decode('utf-8', 'replace'))
        return 1
    with tempfile.NamedTemporaryFile(suffix='.tar', delete=False) as fh:
        fh.write(proc.stdout)
        tar_path = fh.name
    try:
        with tarfile.open(tar_path) as tar:
            tar.extractall(dest, filter='data')     # py3.12+: refuse absolute/parent paths
    finally:
        os.unlink(tar_path)
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--keep', action='store_true')
    args = ap.parse_args()

    head = subprocess.run(['git', '-C', ROOT, 'rev-parse', '--short', 'HEAD'],
                          capture_output=True, text=True).stdout.strip()
    dirty = subprocess.run(['git', '-C', ROOT, 'status', '--porcelain'],
                           capture_output=True, text=True).stdout.strip()

    tmp = tempfile.mkdtemp(prefix='hr0d-clone-')
    try:
        print('==> exporting HEAD (%s) to %s' % (head, tmp))
        if dirty:
            print('    NOTE: the working tree has uncommitted changes; they are NOT in this test. '
                  'Commit before trusting a green result.')
        if export_head(tmp) != 0:
            return 1

        files = sum(len(f) for _d, _s, f in os.walk(tmp))
        print('    %d files, %.1f MB' % (files, sum(
            os.path.getsize(os.path.join(d, f)) for d, _s, fs in os.walk(tmp) for f in fs) / 1048576))

        failures = []
        for name, argv in CHECKS:
            proc = subprocess.run([sys.executable, os.path.join(tmp, argv[0])] + argv[1:],
                                  cwd=tmp, capture_output=True, text=True)
            out = (proc.stdout or '') + (proc.stderr or '')
            if proc.returncode != 0:
                failures.append((name, out.strip().splitlines()[-4:]))
                print('  [FAIL] %s' % name)
            else:
                print('  [ok]   %s' % name)

        if failures:
            print('\nFAIL: the COMMIT does not work on a clean machine:')
            for name, tail in failures:
                print('  %s' % name)
                for line in tail:
                    print('      ' + line)
            print('\nThis is the class of bug that has cost six round trips. Fix the commit, not the gate.')
            return 1

        print('\nPASS: a clean checkout of %s verifies itself.' % head)
        return 0
    finally:
        if args.keep:
            print('    kept: ' + tmp)
        else:
            shutil.rmtree(tmp, ignore_errors=True)


if __name__ == '__main__':
    raise SystemExit(main())
