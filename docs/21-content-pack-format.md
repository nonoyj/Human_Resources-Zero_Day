# Content pack format -- v1

The game's words live here, not in C#. A pack is JSON, validated on publish by
`backend/validate_pack.py` (invariants I1-I8 from `docs/07-randomization-spec.md`).

```
content/
  schema/pack.schema.json     formal shape (JSON Schema 2020-12)
  schema/email.schema.json    inbox item shape
  days/day_01.json            a complete, publishable day plan
  emails/*.json               15 items: 8 malicious templates, each with a legit twin
  letters/*.json              cover-letter voices (human warm, human nervous, synthetic flat)
  endings/*.json              1 win + 4 defeat endings, each with "what you could have done"
```

Check a day file before publishing:

```powershell
cd backend
python validate_pack.py ..\content\days\day_01.json
# OK       ../content/days/day_01.json: publishable
```

The validator refuses, with the invariant id, when a pack:

| Refusal | Invariant | Means |
|---|---|---|
| synthetic with no tell | I1 | an AI you cannot catch is unfair |
| duplicate name or ID | I2 | two people who are the same person |
| `claimed == actual` | I3 | a "contradiction" that is not contradictory |
| declared synthetic count != real count | I4 | the day is easier/harder than the plan says |
| hard tell on a human | I5 | you would learn to reject innocent people |
| distractor with no `resolvesWith` | I6 | a red herring you cannot clear |
| tell with empty `textSurface` | I8 | colour-only evidence is invisible to some players |

## Rules when adding content

1. Every string a player reads is in a JSON file -- no English in prefabs or scripts.
2. Every malicious email gets a **legit twin** (`twinOf`) with the same shape and a safe outcome.
   The twin is the lesson. Without it you teach fear, not judgement.
3. Every tell must be **sayable** (`textSurface`) and reachable on that day (`surface`).
4. Every distractor must say what resolves it (`resolvesWith`) -- a gap explained in the cover
   letter, a surname change explained by marriage.
5. Never add real people's data. All identities in `content/days/` are invented.

## What is still missing

`day_02.json` ... `day_05.json`, `content/manual/` (Field Manual entries), `content/dialogue/`,
`content/intro/`. The email pool covers days 1-5 (`earliestDay`), so day plans can reference
existing emails without new writing.
