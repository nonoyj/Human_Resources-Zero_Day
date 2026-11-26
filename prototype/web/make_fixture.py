#!/usr/bin/env python3
"""Build the interaction-twin fixtures from the reference generator.

The web twin must not invent applicants: it reads the same seeded data the C# core generates.
The public fixture has no truth flag and no archetype -- only what the player may see, plus the
hidden tell ids the client needs in order to reveal evidence. The answer key is a separate file
that the app never fetches; tests use it to prove every synthetic was catchable.

    python3 prototype/web/make_fixture.py            # seed 1000
    python3 prototype/web/make_fixture.py --seed 7
"""
from __future__ import annotations

import argparse
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '..')))
import generator_sim as sim  # noqa: E402


def build(seed: int):
    rng = random.Random(seed)
    registry = sim.NameRegistry()
    days = {d: sim.generate_day(rng, d, registry) for d in sim.DAY_PLAN}
    errors = sim.validate_run(days)
    if errors:
        raise SystemExit('generator invariants failed: ' + '; '.join(errors[:5]))

    public = {'schema': 1, 'seed': seed, 'currentYear': sim.CURRENT_YEAR, 'days': {}}
    key = {'seed': seed, 'days': {}}
    for day, applicants in days.items():
        public['days'][str(day)] = []
        key['days'][str(day)] = []
        for a in applicants:
            public['days'][str(day)].append({
                'id': a.aid,
                'name': a.name,
                'age': a.age,
                'idNo': a.id_no,
                'university': a.university,
                'gradYear': a.grad_year,
                'employer': a.employer,
                'employerFounded': a.employer_founded,
                'startYear': a.start_year,
                'expYears': a.exp_years,
                'referral': a.referral,
                'role': a.role if hasattr(a, 'role') else 'Systems Administrator',
                'tells': list(a.tells),
                'distractors': list(a.distractors),
                'sprite': sprite_key(a.archetype),
            })
            key['days'][str(day)].append({'id': a.aid, 'synthetic': a.is_synthetic,
                                          'archetype': a.archetype, 'tells': list(a.tells)})
    return public, key


def sprite_key(archetype: str) -> str:
    if archetype.startswith('synthetic'):
        return 'synthetic_' + archetype.split('_', 1)[1].split('_')[0] + '_a'
    if archetype == 'human_bait':
        return 'human_bait_a'
    if archetype == 'human_nervous':
        return 'human_nervous_a'
    return 'human_solid_a'


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--seed', type=int, default=1000)
    args = ap.parse_args()
    public, key = build(args.seed)
    outdir = os.path.join(HERE, 'fixtures')
    os.makedirs(outdir, exist_ok=True)
    a = os.path.join(outdir, 'run-%d.json' % args.seed)
    b = os.path.join(outdir, 'run-%d.key.json' % args.seed)
    with open(a, 'w', encoding='utf-8') as fh:
        json.dump(public, fh, indent=2, ensure_ascii=False)
        fh.write('\n')
    with open(b, 'w', encoding='utf-8') as fh:
        json.dump(key, fh, indent=2, ensure_ascii=False)
        fh.write('\n')
    print('wrote', os.path.relpath(a, HERE), 'and', os.path.relpath(b, HERE))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
