# 17 — The paperwork mechanic (real vs fake), v1

**Status:** designed 2026-11-26, built in C# (`Game/Desk/LetterView.cs`), **not yet wired to input**.
**Owner:** Agent wrote the model; Dev A owns the presentation afterwards.
**Why this doc exists:** the user said the letter *is* the mechanic — "some emails are suspicious/fake,
the player must judge real vs fake, and there has to be an action for when they spot something
suspicious". This is that design, written down before any more code is spent on it.

---

## 1. The one-line version

Every applicant arrives with a cover letter. Most are ordinary. Some are **fake** — written *at* the
player, not by the applicant — and the only way to know is to read them and **flag** what is wrong.
Flagging is a guess under uncertainty, so a wrong flag costs you trust, the same currency the rest of
the game runs on.

## 2. Where it sits in the loop

```
 shift starts
   -> applicant walks in, sits down
   -> glass shows the CARD (day, trust, name, role, ID, grad/exp, ref) and "> READ LETTER"
   -> player opens the LETTER PAGE   (option (c): full screen over the desk, slides in)
        -> reads, optionally toggles FLAGS (a list of suspicions, not answers)
        -> closes with X  -> back to the desk
   -> stamps HIRE / REJECT (the existing mechanic, unchanged)
   -> debrief: what the flags were worth
```

The letter never replaces the stamp. It moves the *information* to where it can be read.

## 3. Two kinds of tell (this is the design, not flavour)

| | Visual tells (already built) | Paper tells (new) |
|---|---|---|
| Where | on the applicant sprite — hotspots, magnifier ring, callout | in the letter text |
| Fails when | the applicant is a **synthetic** and the tell is a *distractor* | the letter is well-written but the content is wrong |
| Cost to inspect | time (the shift clock) | nothing but the shift clock |
| What it teaches | "the body gives it away" | "the *situation* gives it away" |

A synthetic applicant can hand you a perfect letter — that is the joke of Day 3 onward. A human can
hand you a scam letter because they were scammed too. **Neither channel is sufficient alone**, which is
what stops the game from being a spot-the-difference puzzle.

## 4. The flags (shipping set, ids are data)

Five flags, always all five offered, each one a *question the player asks the letter*:

| id | Label on the button | Becomes `isReal` when the body contains |
|---|---|---|
| `paper_money_demand` | ASKING FOR MONEY / FEES | `fee`, `deposit`, `processing cost` |
| `paper_urgency` | PRESSURE / DEADLINE THREAT | `immediately`, `today`, `urgent` |
| `paper_mass_template` | TEMPLATE / SOMEONE ELSE'S LETTER | `[NAME]`, `dear sir/madam` |
| `paper_wrong_reference` | REFERENCE DOES NOT MATCH THE RECORD | the record has **no** referral |
| `paper_missing_signature` | NO SIGNATURE / NO CONTACT DETAILS | the body has no `regards` |

Rules:
- **`isReal` is hidden.** The player is never told which flags were right during the shift.
- Flags are built deterministically from the record + body (`LetterFlags.Build`), so the same seed
  always produces the same letter and the same flags — invariant **I7** holds.
- A flag is not "the letter is fake". It is "this specific thing is wrong". Five wrong flags on one
  letter is possible, and is the failure mode we want players to learn.

## 5. What suspicion costs — scoring

At the debrief only:

| Event | Effect |
|---|---|
| flag that is `isReal` | **+1** to the paperwork score; the flag is named in the debrief line |
| flag that is not `isReal` | **−1**; trust takes the flag's `trustDelta` (3–6) when the applicant is stamped HIRE |
| no flags at all on a fake letter | −1 implicit; the debrief says nothing — the player simply never learns |
| flags ignored on a real letter | no penalty (you are allowed to be uninterested) |

The tension: flagging is **free to try, expensive to be wrong about**. That is the whole mechanic in one
sentence, and it is why the flags are toggles the player can un-set before closing the page.

`LetterSession.Score` already implements exactly this table, and `LetterViewTests` asserts both
directions (a right flag = +1; flagging all five on a clean letter = negative).

## 6. The action when you *know*

Flagging is the cheap action. The expensive action is the existing **stamp**, and the two are linked:

- **REJECT with flags set** → trust **+**, and the debrief credits the flags. This is the correct play
  on a fake letter.
- **HIRE with flags set** → trust **−** by the sum of the wrong flags (`trustDelta`), and the debrief
  reads as "you saw it and hired them anyway". This is the interesting failure, and it is the one that
  feeds the 3-AI defeat path.
- **Either stamp with no flags** → no paperwork credit. Silence is neutral.

That gives the player three behaviours to express: *careful*, *lucky*, and *negligent* — without adding
a new verb or a new button to the desk.

## 7. Why this is cheap to build

| Already done (in the repo) | Still owed |
|---|---|
| `LetterLayout.Wrap` — word wrap at 63 cols, never loses a word (tested) | paper sprite + 9-slice frame (art, small) |
| `LetterPaginator` — 16 body rows/page, signature under the body (tested) | input: tap the prompt to open, X to close, tap a flag to toggle |
| `LetterFlags.Build` + `LetterSession.Score` (tested, both directions) | the flag panel UI: 5 rows, drawn on the paper page |
| `LetterPage.ToLines()` — renders through the *same* raster as the CRT | debrief copy (Dev C) |
| `LetterView` MonoBehaviour — open/close/repaint, one 444×234 texture | — |
| `tools/comp/preview_letter.py` — see the page without Unity (`--scam` for the fake) | — |

## 8. Open questions (pinned, not built on)

1. **Does the shift clock stop while the letter is open?** Reading is the point; a clock that punishes
   reading would kill the mechanic. Proposal: **the clock slows to 25%** — reading is cheap, not free.
2. **Can a letter be reopened after the stamp?** Proposal: **no.** One read, then commit. It is the only
   way the flag choice has weight.
3. **Where do the flags render — on the letter page or a separate overlay?** Proposal: **on the page**,
   the bottom five rows, so the player reads and judges in one place. The page has 23 rows; body is 16.

Answered in §9 once the user decides.
