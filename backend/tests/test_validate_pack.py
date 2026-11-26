"""Fairness validator tests -- a bad pack must never be publishable."""
import copy
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))

from validate_pack import validate

DAY = {
    "day": 1,
    "syntheticCount": 1,
    "applicants": [
        {"id": "a1", "archetype": "human_solid", "fullName": "Ada Reyes",
         "idNumber": "PH-1001", "tells": []},
        {"id": "a2", "archetype": "synthetic_polished", "fullName": "Bea Cruz",
         "idNumber": "PH-1002",
         "tells": [{"id": "flat_prose", "surface": "letter", "textSurface": "flat prose"}]},
    ],
}


def pack(**over):
    # Deep copy: tests mutate the pack, and DAY is module-level.
    base = {"schemaVersion": 1, "id": "core", "version": "1.0.0",
            "days": [copy.deepcopy(DAY)]}
    base.update(over)
    return base


def test_good_pack_passes():
    assert validate(pack()) == []


def test_synthetic_without_tell_is_I1():
    p = pack()
    p["days"][0]["applicants"][1]["tells"] = []
    assert any(v.startswith("I1 ") for v in validate(p))


def test_duplicate_identity_across_days_is_I2():
    p = pack()
    p["days"].append({"day": 2, "applicants": [dict(DAY["applicants"][0])]})
    assert any(v.startswith("I2 ") for v in validate(p))


def test_colour_only_tell_is_I8():
    p = pack()
    p["days"][0]["applicants"][1]["tells"][0]["textSurface"] = ""
    assert any(v.startswith("I8 ") for v in validate(p))


def test_hard_tell_on_human_is_I5():
    p = pack()
    p["days"][0]["applicants"][0]["tells"] = [{"id": "document_forgery"}]
    assert any(v.startswith("I5 ") for v in validate(p))


def test_declared_synthetic_count_mismatch_is_I4():
    p = pack()
    p["days"][0]["syntheticCount"] = 3
    assert any(v.startswith("I4 ") for v in validate(p))


def test_claimed_equals_actual_is_I3():
    p = pack()
    p["days"][0]["applicants"][0]["contradictions"] = [
        {"field": "age", "claimed": 30, "actual": 30}]
    assert any(v.startswith("I3 ") for v in validate(p))


def test_distractor_without_resolution_is_I6():
    p = pack()
    p["days"][0]["applicants"][0]["distractors"] = [{"id": "accent"}]
    assert any(v.startswith("I6 ") for v in validate(p))


def test_unknown_archetype_refused():
    p = pack()
    p["days"][0]["applicants"][0]["archetype"] = "wizard"
    assert any(v.startswith("E00") for v in validate(p))
