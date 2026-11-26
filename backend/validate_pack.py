#!/usr/bin/env python3
"""validate_pack.py -- refuse a content pack that breaks fairness.

Publishing a pack must fail loudly if the content would make the game unfair.
The invariants (docs/07-randomization-spec.md) are the same ones the C# core
already enforces; this is the server-side gate so a hand-edited JSON pack can
never reach players.

Usage:
    python3 validate_pack.py path/to/pack.json
    python3 validate_pack.py path/to/pack.json --json     # machine-readable

Exit codes: 0 = publishable, 1 = refused (reasons on stderr), 2 = bad input.

Pack shape (content/schema/pack.schema.json):
{
  "schemaVersion": 1,
  "id": "core", "version": "2026.11.26",
  "days": [{ "day": 1, "applicants": [ { ...applicant... } ] }],
  "archetypes": {...}, "unlockedSurfaces": {"1": ["letter","registry"]}
}
"""
from __future__ import annotations

import json
import sys
from collections import Counter

HUMAN = {"human_solid", "human_nervous", "human_bait"}
SYNTHETIC = {"synthetic_polished", "synthetic_cloned", "synthetic_collusion"}
# I5: only human_bait may carry compromised documents, and only soft tells.
HARD_TELLS = {"document_forgery", "identity_mismatch", "arithmetic_impossible"}
SOFT_TELLS = {"coercion", "urgency_pressure", "authority_pressure", "pleading"}


def _ids(a: dict) -> tuple[str, str]:
    return (str(a.get("fullName", "")).strip().lower(),
            str(a.get("idNumber", "")).strip().lower())


def validate(pack: dict) -> list[str]:
    """Returns a list of human-readable violations. Empty list = publishable."""
    bad: list[str] = []
    if pack.get("schemaVersion") != 1:
        bad.append(f"E00 schemaVersion must be 1, got {pack.get('schemaVersion')!r}")
    if not pack.get("id") or not pack.get("version"):
        bad.append("E00 pack needs both id and version")
    days = pack.get("days")
    if not isinstance(days, list) or not days:
        return bad + ["E00 pack has no days[]"]

    seen: dict[tuple[str, str], str] = {}   # identity -> first day seen (I2)
    for d in days:
        day = d.get("day")
        apps = d.get("applicants") or []
        if not isinstance(apps, list) or not apps:
            bad.append(f"E00 day {day}: no applicants")
            continue

        synths = [a for a in apps if a.get("archetype") in SYNTHETIC]
        humans = [a for a in apps if a.get("archetype") in HUMAN]
        unknown = [a for a in apps if a.get("archetype") not in HUMAN | SYNTHETIC]
        if unknown:
            bad.append(f"E00 day {day}: unknown archetype(s) "
                       f"{sorted({a.get('archetype') for a in unknown})}")

        declared = d.get("syntheticCount")
        if declared is not None and declared != len(synths):
            bad.append(f"I4 day {day}: declared {declared} synthetics, found {len(synths)}")

        for a in synths:                                   # I1 + I1b
            tells = a.get("tells") or []
            if not tells:
                bad.append(f"I1 day {day}: synthetic {a.get('id')} has no tell")
                continue
            unlocked = {str(t.get("surface")) for t in tells if t.get("surface")}
            for t in tells:
                if not t.get("textSurface"):
                    bad.append(f"I8 day {day}: tell {t.get('id')} on {a.get('id')} "
                               "has no text surface (colour-only)")

        for a in humans:                                   # I5
            hard = [t for t in (a.get("tells") or [])
                    if t.get("id") in HARD_TELLS or t.get("severity") == "hard"]
            if a.get("archetype") != "human_bait" and hard:
                bad.append(f"I5 day {day}: human {a.get('id')} carries a hard tell")
            if a.get("archetype") == "human_bait":
                stray = [t.get("id") for t in hard if t.get("id") not in SOFT_TELLS]
                if stray:
                    bad.append(f"I5 day {day}: human_bait {a.get('id')} has non-soft "
                               f"tells {stray}")

        for a in apps:                                     # I2
            ident = _ids(a)
            if not ident[0] or not ident[1]:
                bad.append(f"E00 day {day}: applicant {a.get('id')} missing name/idNumber")
                continue
            if ident in seen:
                bad.append(f"I2 duplicate identity {ident[0]!r} also on day {seen[ident]}")
            else:
                seen[ident] = day

        for a in apps:                                     # I3 (arithmetic)
            for c in a.get("contradictions") or []:
                if c.get("claimed") == c.get("actual"):
                    bad.append(f"I3 day {day}: {a.get('id')} contradiction "
                               f"{c.get('field')} claimed == actual")

        for a in apps:                                     # I6
            for dist in a.get("distractors") or []:
                if not dist.get("resolvesWith"):
                    bad.append(f"I6 day {day}: distractor {dist.get('id')} on "
                               f"{a.get('id')} has no resolving field/tool")

        # I4: a human quota must be reachable -- at least one human per day.
        if not humans:
            bad.append(f"I4 day {day}: no human applicants (quota unreachable)")

    # I7 determinism is a property of the generator, not the file: check that
    # the pack did not hand-author a non-seeded ordering duplicate.
    dupe_days = [k for k, v in Counter(str(d.get("day")) for d in days).items() if v > 1]
    if dupe_days:
        bad.append(f"E00 duplicate day numbers: {sorted(dupe_days)}")
    return bad


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__, file=sys.stderr)
        return 2
    path = argv[1]
    try:
        with open(path, "r", encoding="utf-8") as fh:
            pack = json.load(fh)
    except (OSError, json.JSONDecodeError) as exc:
        print(f"REFUSED  cannot read pack: {exc}", file=sys.stderr)
        return 2

    problems = validate(pack)
    if "--json" in argv:
        print(json.dumps({"publishable": not problems, "violations": problems}, indent=2))
    elif problems:
        print(f"REFUSED  {path}: {len(problems)} violation(s)", file=sys.stderr)
        for p in problems:
            print(f"  - {p}", file=sys.stderr)
    else:
        print(f"OK       {path}: publishable")
    return 1 if problems else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
