"""
Applicant generator simulation — reference implementation for the Unity port.

Purpose: prove that *randomised* applicants never produce an unfair run.
Run it before every content/schema change. If any invariant fails, the Unity
generator will ship the same bug.

    python prototype/generator_sim.py             # default: 10,000 applicants
    python prototype/generator_sim.py --runs 500   # simulate 500 full 5-day runs

Invariants enforced (see docs/07-randomization-spec.md):
  I1  every synthetic has >= 1 tell that is visible through a UI surface
  I2  no identity collision within a run (name and ID unique)
  I3  every arithmetic contradiction is *actually* contradictory
  I4  per-day synthetic count is exact and the quota is reachable
  I5  humans never carry a hard tell (only soft/behavioural ones)
  I6  every distractor has a resolvable explanation
  I7  same seed -> identical run (determinism)
  I8  no tell is conveyed by colour alone (each tell has a text surface)
"""

from __future__ import annotations

import argparse
import hashlib
import random
import statistics
import time
from dataclasses import dataclass, field
from typing import Callable

CURRENT_YEAR = 2026

FIRST_NAMES = ["Marisol", "Elias", "Tomas", "Bianca", "Rafael", "Josefina", "Andres", "Corazon",
               "Miguel", "Lourdes", "Enrique", "Carmela", "Nestor", "Divina", "Ferdinand",
               "Imelda", "Rodrigo", "Teresa", "Alonzo", "Rosario", "Crispin", "Malaya",
               "Dalisay", "Amihan", "Bayani", "Liwayway", "Makiling", "Sampaguita", "Tala",
               "Kidlat", "Araw", "Bituin", "Mayumi", "Joaquin", "Emilio", "Pilar", "Soledad",
               "Arturo", "Graciano", "Remedios", "Vicente", "Esperanza", "Leonora", "Cesar"]

LAST_NAMES = ["Vance", "Tan", "Reyes", "Lim", "Cruz", "Bautista", "Delgado", "Mercado",
              "Alonzo", "Santiago", "Ocampo", "Villanueva", "Navarro", "Domingo", "Salazar",
              "Panganiban", "Aguilar", "Escobar", "Mendoza", "Roxas", "Quiambao", "Ylarde",
              "Bathan", "Cordial", "Dumagsa", "Estrella", "Fuentes", "Gallardo", "Hilario",
              "Ignacio", "Jarabejo", "Kasilag", "Lacson", "Manalo", "Nolasco", "Ong", "Pineda"]

UNIVERSITIES: list[tuple[str, int]] = [  # (name, founded)  -> grad_year must be >= founded
    ("University of San Carlos", 1595), ("University of the Philippines", 1908),
    ("Ateneo de Manila", 1859), ("De La Salle University", 1911),
    ("Cebu Institute of Technology", 1946), ("Mapua University", 1925),
    ("Polytechnic University of the Philippines", 1904), ("Silliman University", 1901),
    ("Technological Institute of the Philippines", 1962),
]

EMPLOYERS: list[tuple[str, int]] = [  # (name, founded) -> start_year must be >= founded
    ("Northwind Logistics", 1998), ("Cebu Pacific Data Services", 1996),
    ("Meridian Bank", 1974), ("Pacific Ridge Health", 2003),
    ("Ayala Systems Group", 1988), ("Kalayaan Telecom", 1992),
    ("Veritas Compliance", 2011), ("Sampaguita Foods", 1985),
    ("Bisaya Freight", 2009), ("Tala Insurance", 1957),
]

ROLES = ["SOC Tier 1 Analyst", "Systems Administrator", "Data Engineer",
         "Compliance Officer", "Network Technician", "Incident Responder",
         "Database Administrator", "Helpdesk Specialist"]

REFERRALS = ["Public posting", "Employee referral", "Recruiter agency",
             "University program", "Internal transfer", "Director fast-track"]

# Evidence categories, exactly as the game's rejection-reason dropdown uses them.
CATEGORIES = ["SYNTHETIC_MEDIA", "DOC_INCONSISTENCY", "IMPERSONATION",
              "COERCION", "SOCIAL_ENGINEERING", "INSUFFICIENT"]

# Difficulty curve: one new attack category per day (docs/05-design-suggestions.md)
DAY_PLAN = {
    1: dict(applicants=4, synthetics=1, tells=(2, 2), new_category="SYNTHETIC_MEDIA"),
    2: dict(applicants=4, synthetics=1, tells=(2, 3), new_category="DOC_INCONSISTENCY"),
    3: dict(applicants=5, synthetics=2, tells=(2, 3), new_category="COERCION"),
    4: dict(applicants=5, synthetics=2, tells=(2, 3), new_category="SOCIAL_ENGINEERING"),
    5: dict(applicants=5, synthetics=2, tells=(3, 3), new_category="IMPERSONATION"),
}


@dataclass
class Tell:
    id: str
    category: str
    surface: str        # which UI element reveals it -> proves it is discoverable
    text_surface: str   # I8: never colour-only
    severity: int


# Every tell declares the exact screen element that reveals it. A synthetic whose
# tells all point at a surface the player cannot open would be unfair, so the
# generator filters against the day's unlocked surfaces.
# Which UI surfaces the player can open on each day. A tell is only fair if its
# surface is unlocked, so the generator filters tells against this table.
SURFACE_UNLOCK = {
    1: {"applicant_stage", "cover_letter"},
    2: {"applicant_stage", "cover_letter", "resume_row", "id_tool"},
    3: {"applicant_stage", "cover_letter", "resume_row", "id_tool"},
    4: {"applicant_stage", "cover_letter", "resume_row", "id_tool", "reference_tool"},
    5: {"applicant_stage", "cover_letter", "resume_row", "id_tool", "reference_tool"},
}

TELLS: dict[str, Tell] = {
    "portrait_asymmetry": Tell("portrait_asymmetry", "SYNTHETIC_MEDIA", "applicant_stage",
                               "Eyes/moles mismatched between sides", 2),
    "portrait_blend_seam": Tell("portrait_blend_seam", "SYNTHETIC_MEDIA", "applicant_stage",
                                "Hair edge cuts into the background", 2),
    "voice_flat_metrics": Tell("voice_flat_metrics", "SYNTHETIC_MEDIA", "cover_letter",
                               "Every sentence identical length, no contractions", 1),
    "exp_vs_grad": Tell("exp_vs_grad", "DOC_INCONSISTENCY", "resume_row",
                        "Experience years exceed years since graduation", 3),
    "employer_founded": Tell("employer_founded", "DOC_INCONSISTENCY", "resume_row",
                             "Employer founded after claimed start date", 3),
    "grad_too_young": Tell("grad_too_young", "DOC_INCONSISTENCY", "resume_row",
                           "Graduated at an impossible age", 3),
    "id_checksum": Tell("id_checksum", "DOC_INCONSISTENCY", "id_tool",
                        "ID checksum digit does not validate", 3),
    "ref_unreachable": Tell("ref_unreachable", "IMPERSONATION", "reference_tool",
                            "Referee says the applicant never applied", 3),
    "identity_clone": Tell("identity_clone", "IMPERSONATION", "id_tool",
                           "ID belongs to a different registered name", 3),
    "bait_script": Tell("bait_script", "COERCION", "cover_letter",
                        "Cover letter reads like dictated instructions", 2),
    "bait_pressure": Tell("bait_pressure", "COERCION", "cover_letter",
                          "Applicant pleads for 'any answer by Friday'", 2),
    "golden_fast_track": Tell("golden_fast_track", "SOCIAL_ENGINEERING", "referral_row",
                              "Fast-track referral with no requisition number", 2),
}

# A distractor looks like evidence but is resolved by another field (I6).
DISTRACTORS = {
    "career_gap": ("12-month gap in employment history", "cover_letter"),
    "surname_change": ("Surname differs from university records", "cover_letter"),
    "short_stints": ("Three employers in 18 months", "cover_letter"),
    "odd_lighting": ("Portrait lighting is uneven", "metadata_tool"),
}

ARCHETYPES = {
    "human_solid":   dict(tells=(0, 0), distractors=(0, 1), weight=40),
    "human_nervous": dict(tells=(0, 0), distractors=(0, 1), weight=22),   # behaviour only
    "human_bait":    dict(tells=(1, 2), distractors=(0, 1), weight=16),   # human, but compromised docs
    "synthetic_polished": dict(tells=(2, 3), distractors=(0, 1), weight=10),
    "synthetic_cloned":   dict(tells=(1, 2), distractors=(1, 1), weight=6),
    "synthetic_collusion": dict(tells=(2, 3), distractors=(0, 0), weight=6),
}
SYNTHETIC_ARCHETYPES = {"synthetic_polished", "synthetic_cloned", "synthetic_collusion"}
HUMAN_ARCHETYPES = {"human_solid", "human_nervous", "human_bait"}


@dataclass
class Applicant:
    aid: str
    is_synthetic: bool          # the ONE thing the player must infer
    archetype: str
    name: str
    age: int
    id_no: str
    id_checksum_ok: bool
    grad_year: int
    university: str
    employer: str
    employer_founded: int
    start_year: int
    exp_years: int
    referral: str
    tells: list[str] = field(default_factory=list)
    distractors: list[str] = field(default_factory=list)

    def career_span(self) -> int:
        return CURRENT_YEAR - self.grad_year

    def recompute_contradictions(self) -> list[str]:
        """I3: a claimed contradiction must be arithmetically true."""
        out = []
        if self.exp_years > self.career_span():
            out.append("exp_vs_grad")
        if self.start_year < self.employer_founded:
            out.append("employer_founded")
        if self.grad_year - (CURRENT_YEAR - self.age) < 15:   # graduated at 14 or younger
            out.append("grad_too_young")
        if not self.id_checksum_ok:
            out.append("id_checksum")
        return out


def id_checksum(id_no: str) -> bool:
    digits = [int(c) for c in id_no if c.isdigit()]
    return len(digits) == 8 and sum(digits[:-1]) % 10 == digits[-1]


class NameRegistry:
    """Per-run uniqueness guard (I2). In Unity this is a HashSet inside RunGenerator."""

    def __init__(self) -> None:
        self.names: set[str] = set()
        self.ids: set[str] = set()

    def fresh(self, rng: random.Random) -> dict:
        for _ in range(200):
            ident = _make_identity(rng)
            if ident["name"] not in self.names and ident["id_no"] not in self.ids:
                self.names.add(ident["name"])
                self.ids.add(ident["id_no"])
                return ident
        raise RuntimeError("name pool exhausted — widen FIRST_NAMES/LAST_NAMES")


def make_identity(rng: random.Random) -> dict:
    return _make_identity(rng)


def _make_identity(rng: random.Random) -> dict:
    age = rng.randint(22, 58)
    birth_year = CURRENT_YEAR - age
    grad_year = min(CURRENT_YEAR - 1, birth_year + rng.randint(21, 24))
    university, uni_founded = rng.choice(UNIVERSITIES)
    grad_year = max(grad_year, uni_founded + 18)
    employer, emp_founded = rng.choice(EMPLOYERS)
    span = CURRENT_YEAR - grad_year
    start_year = rng.randint(max(emp_founded, grad_year), max(emp_founded, CURRENT_YEAR - 1))
    exp_years = rng.randint(max(0, span - 4), span) if span >= 0 else 0
    base = [rng.randint(1, 9)] + [rng.randint(0, 9) for _ in range(6)]
    base.append(sum(base) % 10)
    id_no = "PH-" + "".join(str(d) for d in base[:4]) + "-" + "".join(str(d) for d in base[4:])
    return dict(
        name=f"{rng.choice(FIRST_NAMES)} {rng.choice(LAST_NAMES)}",
        age=age, id_no=id_no, id_checksum_ok=True, grad_year=grad_year,
        university=university, employer=employer, employer_founded=emp_founded,
        start_year=start_year, exp_years=exp_years,
        referral=rng.choice(REFERRALS),
    )


def pick_archetype(rng: random.Random) -> str:
    names = list(ARCHETYPES)
    return rng.choices(names, weights=[ARCHETYPES[n]["weight"] for n in names], k=1)[0]


def apply_tell(app: Applicant, tell_id: str, rng: random.Random) -> bool:
    """Mutate the applicant so the tell is factually present. Returns False if it can't apply."""
    if tell_id == "exp_vs_grad":
        app.exp_years = app.career_span() + rng.randint(1, 5)
    elif tell_id == "employer_founded":
        app.start_year = app.employer_founded - rng.randint(1, 6)
    elif tell_id == "grad_too_young":
        app.grad_year = CURRENT_YEAR - app.age + rng.randint(9, 14)
    elif tell_id == "id_checksum":
        digits = [int(c) for c in app.id_no if c.isdigit()]
        digits[-1] = (digits[-1] + rng.randint(1, 9)) % 10
        app.id_no = "PH-" + "".join(str(d) for d in digits[:4]) + "-" + "".join(str(d) for d in digits[4:])
        app.id_checksum_ok = False
    elif tell_id == "golden_fast_track":
        app.referral = "Director fast-track"
    else:
        pass  # narrative tells (portrait/voice/coercion) mutate art or text, not the record
    app.tells.append(tell_id)
    return True


def generate_applicant(rng: random.Random, day: int, archetype: str | None = None,
                       registry: NameRegistry | None = None) -> Applicant:
    arch = archetype or pick_archetype(rng)
    return _generate_with_identity(rng, day, arch, registry)


def _generate_with_identity(rng: random.Random, day: int, arch: str,
                            registry: NameRegistry | None = None) -> Applicant:
    ident = (registry or NameRegistry()).fresh(rng)
    app = Applicant(aid=f"d{day}_{rng.randrange(16**6):06x}", archetype=arch,
                    is_synthetic=arch in SYNTHETIC_ARCHETYPES, **ident)

    cfg = ARCHETYPES[arch]
    n_tells = rng.randint(*cfg["tells"])
    n_dist = rng.randint(*cfg["distractors"])

    # Day gating: the day's new category is always available, older ones stay in play.
    unlocked = {c for d, plan in DAY_PLAN.items() if d <= day for c in [plan["new_category"]]}
    candidates = [t for t, meta in TELLS.items() if meta.category in unlocked or meta.category == "DOC_INCONSISTENCY"]
    rng.shuffle(candidates)

    if arch == "human_bait":
        candidates = ["bait_script", "bait_pressure", "voice_flat_metrics"]
    elif arch == "synthetic_cloned":
        candidates = ["identity_clone", "ref_unreachable", "id_checksum", "exp_vs_grad"]
    elif arch == "synthetic_collusion":
        candidates = ["golden_fast_track", "ref_unreachable", "portrait_blend_seam", "voice_flat_metrics"]
    elif arch == "synthetic_polished":
        candidates = ["portrait_asymmetry", "portrait_blend_seam", "voice_flat_metrics",
                      "exp_vs_grad", "employer_founded", "id_checksum"]

    unlocked_surfaces = SURFACE_UNLOCK[day]
    candidates = [t for t in candidates if TELLS[t].surface in unlocked_surfaces]
    if not candidates:                       # never ship a synthetic with no reachable tell
        candidates = [t for t, meta in TELLS.items()
                      if meta.category == "SYNTHETIC_MEDIA" and meta.surface in unlocked_surfaces]
    if n_tells > 0:
        for tell_id in candidates[:n_tells]:
            apply_tell(app, tell_id, rng)

    for d in rng.sample(list(DISTRACTORS), k=min(n_dist, len(DISTRACTORS))):
        app.distractors.append(d)
    return app


def generate_day(rng: random.Random, day: int, registry: NameRegistry | None = None) -> list[Applicant]:
    registry = registry or NameRegistry()
    plan = DAY_PLAN[day]
    apps: list[Applicant] = []
    # I4: exact synthetic count, first, so the draw can never starve the day.
    # Collusion only unlocks on day 4+ (it needs the requisition/email systems).
    synth_pool = ["synthetic_polished"] if day == 1 else ["synthetic_polished", "synthetic_cloned"]
    synth_weights = [ARCHETYPES["synthetic_polished"]["weight"],
                     ARCHETYPES["synthetic_cloned"]["weight"]]
    if day >= 4:
        synth_pool.append("synthetic_collusion")
        synth_weights.append(ARCHETYPES["synthetic_collusion"]["weight"])
    chosen: list[str] = []
    for _ in range(plan["synthetics"]):
        pick = rng.choices([a for a in synth_pool if a not in chosen],
                           weights=[synth_weights[synth_pool.index(a)]
                                    for a in [a for a in synth_pool if a not in chosen]], k=1)[0]
        chosen.append(pick)
    for arch in chosen:
        apps.append(generate_applicant(rng, day, arch, registry))
    remaining = plan["applicants"] - plan["synthetics"]
    pool = ["human_solid"] * 3 + ["human_nervous"] * 2 + (["human_bait"] if day >= 3 else [])
    for _ in range(remaining):
        apps.append(generate_applicant(rng, day, rng.choice(pool), registry))
    rng.shuffle(apps)
    return apps


def validate_run(days: dict[int, list[Applicant]]) -> list[str]:
    errors: list[str] = []
    seen_names: set[str] = set()
    seen_ids: set[str] = set()
    for day, apps in days.items():
        plan = DAY_PLAN[day]
        synth = [a for a in apps if a.is_synthetic]
        if len(synth) != plan["synthetics"]:
            errors.append(f"I4 day{day}: {len(synth)} synthetics, expected {plan['synthetics']}")
        humans = len(apps) - len(synth)
        if humans < min(3, plan["applicants"] - plan["synthetics"]):
            errors.append(f"I4 day{day}: only {humans} humans, quota unreachable")
        for a in apps:
            if a.name in seen_names:
                errors.append(f"I2 duplicate name {a.name}")
            if a.id_no in seen_ids:
                errors.append(f"I2 duplicate id {a.id_no}")
            seen_names.add(a.name)
            seen_ids.add(a.id_no)

            if a.is_synthetic:
                if not a.tells:                                     # I1
                    errors.append(f"I1 {a.aid}: synthetic with no tell")
                for t in a.tells:
                    meta = TELLS[t]
                    if not meta.surface or not meta.text_surface:    # I8
                        errors.append(f"I8 {a.aid}: tell {t} has no text surface")
                    if meta.surface not in SURFACE_UNLOCK[day]:      # I1b
                        errors.append(f"I1b {a.aid}: tell {t} needs '{meta.surface}' "
                                      f"which is locked on day {day}")
                claimed = set(a.recompute_contradictions())
                factual = {t for t in a.tells if t in claimed}
                # every factual contradiction the generator *claims* must really hold
                for t in a.tells:
                    if t in ("exp_vs_grad", "employer_founded", "grad_too_young", "id_checksum"):
                        if t not in claimed:                        # I3
                            errors.append(f"I3 {a.aid}: claimed {t} but record does not contradict")
            else:
                hard = {"portrait_asymmetry", "portrait_blend_seam", "identity_clone", "id_checksum"}
                if hard & set(a.tells):                             # I5
                    if a.archetype != "human_bait":
                        errors.append(f"I5 {a.aid}: human carries hard tell {hard & set(a.tells)}")
            for d in a.distractors:
                if not DISTRACTORS[d][1]:                           # I6
                    errors.append(f"I6 {a.aid}: distractor {d} has no resolution source")
    return errors


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", type=int, default=500)
    args = ap.parse_args()

    t0 = time.perf_counter()
    all_errors: list[str] = []
    tell_counts: list[int] = []
    per_archetype: dict[str, int] = {}
    synthetic_total = human_total = 0
    first_seed_snapshot = None

    for run in range(args.runs):
        run_seed = 1000 + run
        rng = random.Random(run_seed)
        registry = NameRegistry()          # one registry per run -> I2 across all 5 days
        days = {d: generate_day(rng, d, registry) for d in DAY_PLAN}
        errs = validate_run(days)
        all_errors.extend(f"seed {run_seed}: {e}" for e in errs)
        for apps in days.values():
            for a in apps:
                per_archetype[a.archetype] = per_archetype.get(a.archetype, 0) + 1
                if a.is_synthetic:
                    synthetic_total += 1
                    tell_counts.append(len(a.tells))
                else:
                    human_total += 1
        if run == 0:
            first_seed_snapshot = [
                [f"{a.archetype}:{a.name}:{','.join(a.tells) or '-'}" for a in days[d]] for d in sorted(days)
            ]

    elapsed = time.perf_counter() - t0

    # I7 determinism check
    rng_a, rng_b = random.Random(424242), random.Random(424242)
    snap_a = [[a.aid for a in generate_day(rng_a, d)] for d in DAY_PLAN]
    snap_b = [[a.aid for a in generate_day(rng_b, d)] for d in DAY_PLAN]
    deterministic = snap_a == snap_b

    apps_total = synthetic_total + human_total
    print(f"runs simulated        : {args.runs}")
    print(f"applicants generated  : {apps_total}  ({apps_total / args.runs:.1f} per run)")
    print(f"synthetic / human     : {synthetic_total} / {human_total} "
          f"({synthetic_total / apps_total:.0%} synthetic)")
    print(f"tells per synthetic   : min {min(tell_counts)}  "
          f"median {statistics.median(tell_counts):.0f}  max {max(tell_counts)}")
    print(f"ms per run            : {elapsed / args.runs * 1000:.2f}")
    print(f"deterministic (I7)    : {deterministic}")
    print(f"invariant violations  : {len(all_errors)}")
    for e in all_errors[:10]:
        print("   ", e)
    print("\narchetype mix:")
    for k, v in sorted(per_archetype.items(), key=lambda kv: -kv[1]):
        print(f"   {k:<22} {v:>6}  {v / apps_total:>6.1%}")
    print("\nfirst-run snapshot (seed 1000):")
    for i, day in enumerate(first_seed_snapshot or [], start=1):
        print(f"   day {i}: " + " | ".join(day))


if __name__ == "__main__":
    main()
