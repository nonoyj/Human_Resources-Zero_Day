# HANDOFF -- Agent -> Whole Team -- Kickoff

**Date:** 2026-11-25 - **Branch:** `main` - **Status:** open, needs answers

## What is done
- Repository skeleton, `.gitignore`, docs: roadmap, tech stack, title shortlist, collaboration protocol, bug log, backlog.

## What we need from you (blocking Items)
1. Answers to the 6 questions in the ask-user form (orientation, detection model, session length,
   team skills, git experience, GitHub remote).
2. Confirm all 3 machines run Unity **6000.3.29f1** with Android Build Support installed.
3. Confirm the roles in `docs/01-roadmap-14-days.md` match reality.

## What happens next (agent)
- Generate the pixel-art kit: palette-consistent UI 9-slices, 4 backgrounds, 6 applicant portraits
  (3 human / 3 with synthetic artifacts), stamps, icons, logo.
- Write the C# core: `SaveSystem`, `ContentLoader`, `RuleEngine` + 3 rules, `DayManager`, `TrustService`
  as plain files you drop into `Assets/Scripts/`.
- Produce the JSON content for Day 1 and Day 2 as the reference implementation.

## How to verify
- Clone the workspace repo, read `README.md` -> `docs/00-open-questions.md`. Nothing to run yet.
