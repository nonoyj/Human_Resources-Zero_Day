# Open Questions & Decisions Needed

Status key: **[DECIDED]** locked · **[REC]** my recommendation, awaiting your yes · **[OPEN]** needs you

---

## 1. Product & scope

| # | Question | My recommendation | Status |
|---|---|---|---|
| 1.1 | **DECIDED 2026-11-26: target = product**, not a booth demo. The 14-day target is a shippable store build. |  | [DECIDED] |
| 1.2 | Session length target? | 10–15 min full run; 3–5 min "one shift" mode for event queues | [REC] |
| 1.3 | Is the game **saved locally only**? | Yes. No backend in v1. (If your team's "backend group" means a real server, tell me — that changes the architecture) | [OPEN] |
| 1.4 | Language: English only for the event, or EN + Tagalog/Cebuano? | English for v1, all strings in JSON so translation is a content task | [REC] |

## 2. Gameplay specifics (need your call)

| # | Question | Options | My recommendation |
|---|---|---|---|
| 2.1 | **DECIDED 2026-11-26:** diegetic detection (no "AI" label) plus a **few explicit checks** (VERITAS). |  | [DECIDED] |
| 2.2 | Does the player have to pick a rejection reason? | Yes / no | **Yes** — it's the teaching mechanism and lets the ending say *why* you were right or wrong |
| 2.3 | How many applicants per day? | 3 / 4 / 5 | **4**, ramping to 5 on Day 5. Total ≈ 21 applicants |
| 2.4 | "Hire enough people and you survive" — how is this measured? | Threshold X of Y approved | **Daily quota** (e.g. must approve ≥3/day) + a global trust meter. Rejecting too many humans = staffing crisis = lost trust; hiring an AI = trust loss when it acts. Both failure directions are real | [OPEN] |
| 2.5 | **DECIDED 2026-11-26: 3 hired AIs = defeat.** 1 = warning beat, 2 = visible corruption. |  | [DECIDED] |
| 2.6 | Is rejecting a *human* actually punished? | Yes, and how | Yes — +difficulty: fewer warm bodies, harder days, a "wrongful rejection" backlash email, and a trust penalty. This is the *Papers, Please* tension and the core lesson | [REC] |
| 2.7 | Can the player "un-hire"/fire? | Per your brief: yes, via the inbox assessment | Yes — 1 fire action per day, costs trust; firing a *human* is a much bigger incident than firing an AI | [REC] |
| 2.8 | Is there a timer per applicant/shift? | None / soft timer / hard | **Soft**: a shift clock that only creates pressure (overtime penalty after the quota), never a hard "you lose at 0:00". Mobile players need pause-ability | [OPEN] |
| 2.9 | Punctuation between shifts — is there a minigame? | No / inbox task / both | **Inbox task** (1–2 emails) + an optional "patch server" micro-step. Keeps rotation without new mechanics | [REC] |
| 2.10 | Does failing a day end the game or just cost? | | Just costs — the run only ends at Day 5 or 3 AIs. Failure should feel like a slope, not a cliff | [REC] |

## 3. Art & presentation

| # | Question | My recommendation | Status |
|---|---|---|---|
| 3.1 | Pixel art resolution: 320×180 (chunky, fast, readable on phones) vs 640×360 (more detail, more work)? | **Base 320×180 scaled 3×–4×**, integer scaling only. Portraits 96×96, full-screen backgrounds 320×180 | [REC] |
| 3.2 | Palette: 16-colour custom (cold corporate cyan/teal + warning amber/red) vs PICO-8-style? | **Custom 20-colour** corporate-terminal palette, given in `02-tech-stack.md` | [REC] |
| 3.3 | **DECIDED 2026-11-26: LANDSCAPE**, 480x270 @ PPU 16. Portrait is rejected — this row supersedes the old recommendation. |  | [DECIDED] |
| 3.4 | **DECIDED 2026-11-26:** name + cosmetics only. **No face customization.** |  | [DECIDED] |
| 3.5 | Is there a face/portrait for the player? | Yes — a small pixel avatar in the corner of the desk + used in the ending. No face morphing, just presets | [REC] |
| 3.6 | Do applicants have photographed portraits (sprite) or only text? | **Sprite**, 96×96, stylized. AI applicants have *off-looking* portraits — subtle artifacts (misaligned eyes, wrong shadow, blend seams). This is a real skill: spotting synthetic images | [REC] |

## 4. Technical

| # | Question | My recommendation | Status |
|---|---|---|---|
| 4.1 | Exact Unity editor version available on all 3 machines? | **6000.3.29f1** as stated — confirm all three have it installed plus Android Build Support (SDK/NDK/JDK) | [OPEN] |
| 4.2 | Render pipeline: URP 2D vs Built-in? | **URP 2D** (better 2D lights/shaders, Unity 6 default) | [REC] |
| 4.3 | Input: new Input System vs legacy? | **Input System package**, with a single `InputActions` asset for touch + mouse | [REC] |
| 4.4 | Save system: plain JSON vs `JsonUtility` vs SQLite? | **JSON + `JsonUtility`** with a `SaveVersion` int and atomic write (temp file → rename) | [REC] |
| 4.5 | Do you have a GitHub remote ready? | I can't reach GitHub from here (connector unavailable), so I'll manage a **local git repo in this workspace** with clean, reviewable commits and handoffs you push. Give me the remote URL and I'll write the exact `git remote add` / push commands for each teammate | [OPEN] |
| 4.6 | Target minimum Android API / device? | **API 24 (Android 7.0) min, target API 35**, arm64 + armv7, IL2CPP, portrait | [REC] |
| 4.7 | Do we need a build server/CI? | No for the event. Local builds by Dev C | [REC] |

## 5. Team & process

| # | Question | My recommendation | Status |
|---|---|---|---|
| 5.1 | Names/skills of Dev A, B, C — who is strong in what? | Tell me roughly (C#? UI/art? writing?) so I assign the roadmap correctly | [OPEN] |
| 5.2 | Do all three know git? | If not, I'll ship a 1-page `docs/git-for-the-team.md` with the 6 commands they need | [OPEN] |
| 5.3 | Who owns the event build/final sign-off? | Dev C | [REC] |
| 5.4 | Is there a presentation/demo slot, and does it need a pitch deck? | If yes, I'll add a 5-slide deck to the deliverable list | [OPEN] |

---

## Defaults I will assume if you don't answer

So we never stall, these are the assumptions I proceed with:

1. Portrait orientation, 320×180 art base, 4 applicants/day, 5 days.
2. Detect-AI is diegetic: 5 evidence categories, player picks a rejection reason.
3. 3 AIs hired = defeat; quota deficit = trust loss, not instant loss.
4. Local JSON save only, no server, no analytics beyond a local event log.
5. English strings only, externalised in JSON.
6. Unity 6000.3.29f1 / URP 2D / Input System / IL2CPP / API 24+.
7. Local git repo here, you push to your own remote.
