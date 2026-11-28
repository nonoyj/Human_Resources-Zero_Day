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

## 8. Decisions (user, 2026-11-26) — closed, and now enforced by tests

1. **The clock SLOWS to 25% while the letter is open. It never stops.**
   The user's reasoning, verbatim: stopping "could be taken advantage of and we have a pause button".
   So the pause button remains the only true stop, and reading costs a quarter of real time.
   Implemented in `Core/Data/ShiftClock.cs` (`ReadingTimeScale = 0.25`); `PaperworkTests` asserts
   4 s of wall time costs 1 s of shift time, and that a full minute of reading costs 15 s of a 180 s
   applicant.
2. **A letter cannot be reopened after the stamp.** One read, then commit.
   `Paperwork.CloseForStamp()` locks the body *and* the flags; `CanOpen` is false forever after, and
   `Toggle` returns false. Tests assert both.
3. **The flags render on the page**, bottom rows, so reading and judging happen in one place.
   Page has 23 rows; body is 16; five flag rows fit under the rule.

## 9. Where the letter text comes from (and why it is generated)

The prose is **generated from the record**, deterministically, by `Paperwork.For(applicant, runSeed)` —
not stored as content. Reasons:

- the save file does not have to carry five days of letters;
- invariant **I7** (same seed, same run) extends to the paperwork for free: the letter is a pure
  function of `(run seed, applicant id)`;
- generation and flagging can be tested *against each other*: `PaperworkTests` runs **40 seeds** and
  fails if an honest letter raises a true flag, or if a synthetic/bait letter raises none.

That last test caught a real bug on its first run: flags were derived from the body only, so
`NO SIGNATURE` was true on **every** letter, including the normal ones. A player who flagged it would
have been penalised for being right. The flags are now derived from `body + signature` — the text the
player actually reads. **This is the class of bug that only a many-seed test finds.**

---

## 8. What the glass shows: the applicant's own photo (CONFIRMED, not yet drawn)

The player asked on 2026-11-29, while reviewing v129: *"just a confirmation, we'll still change the
computers display with the staffs we have talked about during the mechanics right?"* — **Yes.** The CRT
carries the **applicant's own ID photo**, cut from the same sprite the room is drawing. It is the
mechanic that makes `docs/07`'s portrait tells (`portrait_asymmetry`, `portrait_blend_seam`,
`odd_lighting`) usable: the player reads the photo on the glass and the person across the desk, and the
mismatch is the evidence.

### The numbers (so the art can be generated against them)

| Thing | Value | Why |
|---|---|---|
| Glass | 175 × 116 px (`desk_layout.json` `monitor.screen`, v129's menu CRT) | 25 × 11 character cells at 5×7 |
| Photo | **48 × 48**, 1 px sunk border, glass-local `x 8`, top row 8 | nearest-neighbour half of the 96 × 96 ID portrait, doubled again by the phone's integer scale |
| Text area left of the photo | x 62 → 167 | **15 columns** of 5×7 — the trust bar `[####------] 62` is exactly 15, so it still fits |
| Rows | unchanged, 11 | the card keeps its `DAY / TRUST / NAME / ROLE / ID / GRAD·EXP / REF / > READ LETTER` order |

### Rules that come with it

1. **The photo is not a second drawing.** `make_applicants.py` already draws one 192×216 sprite per
   applicant; the portrait is that sprite's head, cropped (`96×96` in documents per `docs/00` 3.6,
   `48×48` on the glass), so *the glass can never disagree with the person* — except for
   `synthetic_cloned`, where disagreeing **is** the tell (`docs/08` §5).
2. **The artifact must survive the crop.** A mole or a blend seam that is 1 px at 96×96 is 1 px at 48×48
   after a nearest-neighbour halve, i.e. ~3 px on a 3× phone. If a tell cannot be seen in the 48 px
   version, the tell is too small and the generator is wrong, not the crop.
3. **The photo is drawn, never composited from a photo file.** The pixel grid stays the shape language
   (`docs/17` §-front-door, rule 1).
4. **Order on the glass**: header (day · trust), then the photo block with the name/role/ID beside it,
   then the prompt. The photo is a *reading*, not a button: `METADATA SCAN` and `ZOOM` are the actions
   that inspect it, and they live on the desk, not on the glass.

**Status:** spec confirmed by the player 2026-11-29 (v129); implementation follows the portrait generation
pass — `docs/17` §7 item 4 in `orders/05-phases.md`.
