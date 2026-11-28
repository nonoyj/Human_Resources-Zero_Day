# Applicant Presence & Behaviour Model

The screen is the interview room: **left half = your monitor**, **right half = the applicant
sitting across from you**. This document specifies what the player can see on the right half and
what it may and may not be used to conclude.

---

## 1. Why behaviour exists at all

It teaches three lessons at once:

1. **Nervousness is not guilt.** An honest applicant with money problems behaves like a suspect.
   If the player learns to read fidgeting as "AI", they will start rejecting real people -- which
   is a real and damaging failure mode in security work.
2. **Synthetics are *too* still.** The designed tell is the opposite of a lie-detector: the AI is
   the one that never breaks posture, never blinks irregularly, never breathes out of rhythm.
3. **Observation must be corroborated.** Behaviour alone can justify *investigating* (using a tool,
   reading the cover letter) -- never a rejection by itself.

**Rule of the design:** behaviour is a **prompt to look closer**, never evidence on its own. The
rejection-reason dropdown has no "acted suspicious" option.

## 2. State machine (per applicant)

```
ENTER -> IDLE <-> {FIDGET, BLINK, LOOK_AWAY, LEAN_IN, SWALLOW, NERVOUS_LAUGH, GLANCE_AT_MONITOR}
             |
             +-> REACT_STAMP   (approved / rejected -> reaction clip, then EXIT)
             +-> REACT_STARE   (player leaves an inspect zoom open >= 3 s)
```

- Base loop `IDLE`: 2-frame breathe, 5-7 s cycle, randomised phase per applicant so a row of
  applicants never syncs.
- `BLINK`: every 2.5-6 s (human) / every **9-14 s** (synthetic -- too regular, too rare).
- `FIDGET`: needs a `stress` value (see #4).
- `REACT_STARE`: humans sit up / break eye contact; synthetics **do not react at all** (subtle, and
  the player will only notice after a few days -- that's the intended late-game tell).

## 3. Authoritative parameter table

| Parameter | Human (`solid`) | Human (`nervous`) | Human (`bait`) | Synthetic |
|---|---|---|---|---|
| blink interval | 2.5-6 s | 1.5-3.5 s | 1.2-3 s | **9-14 s** |
| blink regularity (jitter) | high | high | high | **very low** |
| breathe cycle | 5-7 s | 4.5-6 s | 4-5.5 s | 6.0 s **exactly** |
| fidget chance/min | 12% | 45% | 60% | **0%** |
| look-away chance/min | 20% | 35% | 50% | **0%** |
| micro-saccade into camera | rare | rare | rare | **never** |
| reacts to long stare | yes | yes (more) | yes (most) | **no** |
| idle animation frames | 2 | 2 (+sweat) | 2 (+tremor) | 2 (mirror-identical) |

Two extra synthetic tells available for later days (content-gated, not code-gated):
`mirror_symmetry` (both eyes close simultaneously with identical timing) and
`shadow_mismatch` (the applicant's shadow doesn't match the office light direction).

## 4. `stress` -- the honest vulnerability signal

```
stress = 0.35 * timeUnderObservation + 0.4 * (isBait ? 1 : 0)
       + 0.15 * (hasRejectionHistory ? 1 : 0) + small noise
```
- `stress` raises fidget/laugh/look-away rates in **humans only**.
- Synthetics never enter `FIDGET` -- their `stress` is displayed nowhere and used only to pick
  flavour lines.
- Consequence: **more scrutiny -> more human nervousness -> more false rejections.** The game
  punishes suspicion spirals mechanically, not with a lecture. This is the single most important
  behavioural design in the game.

## 5. Presentation & art requirements

| Item | Spec |
|---|---|
| Framing | waist-up, seated, 3/4 view facing the monitor (left), 1 light source from the monitor |
| Canvas | 192 x 216 per applicant sprite, 4 idle frames max |
| Palette | uses the 20-colour palette; skin tones from 6 approved ramps |
| Chair/table | shared furniture sprite so the scene reads as one room |
| Background | office wall + window; **3 variants** driven by `CorruptionLevel` (clean -> glitch -> red alert) |
| Portrait in documents | the *same* sprite cropped to 96 x 96, and for synthetics the crop shows the artifact |
| Reactions | 3 frames each for APPROVED / REJECTED / STARE (10-12 fps) |
| Reduce-motion setting | disables breathe + fidget, keeps blinks (accessibility) |

**Consistency requirement:** the document portrait and the live applicant must be the same character.
A mismatch (different hair) is *not* a tell -- that would be an unfair, unteachable inconsistency.
For `synthetic_cloned`, the live applicant and the ID photo *do* differ, and that **is** a tell with
wording in the Field Manual.

## 6. Dialogue (sparse, and never a confession)

Applicants may speak 0-2 short lines per meeting, drawn from a pool per archetype:
- `human_solid`: neutral, factual
- `human_nervous`: self-deprecating, over-explains
- `human_bait`: pleading, mentions a deadline or a dependent (no villain signposting)
- `synthetic_polished`: flawless, slightly *too* on-message, echoes the job posting's wording
- `synthetic_cloned`: knows facts about the real ID holder (the AI did its homework)
- `synthetic_collusion`: mentions an internal name it shouldn't know ("Mr. Reyes said to expect me")

**Hard rule:** no line ever says "I am not human", "I am an AI", or gives a verbal giveaway that
replaces the evidence. Dialogue is flavour + one class of tell (echoed corporate language).

## 7. QA checklist for the behaviour layer

- [ ] Two applicants side by side never blink in sync
- [ ] Reduce-motion ON -> no fidget/breathe, blinks still present, no gameplay information lost
- [ ] Synthetic idle is visually identical across all synthetic applicants except the portrait
      (they share mannerisms -- that is itself a subtle, fair tell)
- [ ] A human under 30 s of staring reaches max stress and fidgets; a synthetic does not move
- [ ] Portrait in the resume always matches the live applicant (except the clone archetype)
- [ ] Frame rate: applicant layer < 1.5 ms CPU on a mid-range device (it's 2D sprites; if it costs
      more than that something is wrong)
