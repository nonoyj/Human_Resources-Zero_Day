# How every agent reply is structured (agreed 2026-11-26)

The user asked for a predictable shape so nothing gets missed. **Every reply from the agent uses exactly
these four sections, in this order, with these headings** (revised 2026-11-27 -- the player asked for a
fourth, "what I should see", and for the commands to stop being omitted):

---

## A. WHAT I DID
What the agent actually changed in the workspace this turn. Bullet list, files named, verification status
stated (`executed` / `dry-run` / `reviewed-only`). Nothing the user must act on belongs here.

## B. WHAT YOU DO
Numbered, copy-pasteable actions for the human. **If the answer is "nothing", the agent says
"Nothing this turn" -- explicitly.** No action is ever implied or buried in prose.
Each step says what "done" looks like.

## C. WHAT I SHOULD SEE
What the player is looking at on screen / in the Console once B is done, phrased as observable things.
Name the still-open items explicitly so a known bug is never mistaken for a new one.

## D. WHAT'S NEXT
- **Expect:** what will be true after the user's actions.
- **Agent's next move:** the specific thing the agent will do next unless redirected.
- **Questions:** only decisions the agent genuinely cannot default. If there are none, say "none".
- **Blocked / waiting on:** anything the agent cannot progress without.

---

## E. THE COMMAND BLOCK (mandatory, every single reply)

The player's standing correction, 2026-11-27: *"You did not give me powershell code on your last response,
you're losing your memory. Always refer to our handouts and files where we have stated every command."*

**Every reply ends with the copy-paste block from `docs/80-COMMAND-BOOK.md #2`,** including replies that
change nothing. If the commands for this round differ (a new bundle number, a dry run first), the block is
edited to match and the difference is stated in one line. Omitting it is a defect in the reply, not a
style choice.

---

## Rules that make this work

1. **Never mix A and B.** If a step is the user's, it lives in B and nowhere else.
2. **Say "nothing" out loud.** An empty B section is only valid as the words "Nothing this turn".
3. **No new download unless something changed.** If the turn produced no repo change, the agent must say
   "no new zip" -- downloads are a cost, not a default.
4. **Verification is stated every time.** One line: what was executed vs only reviewed.
5. **Dropped instructions are announced.** When an instruction no longer applies (e.g. pushing to GitHub
   while the token is unavailable), the agent says so explicitly so the user stops carrying it.

---

## Standing instruction ledger (who owes what)

| # | Owner | Item | Status |
|---|---|---|---|
| 1 | Human | Push repo to GitHub | **DEFERRED** -- no token yet; bundles continue until then. Do not attempt. |
| 2 | Human | Enable Windows long paths (admin) | Optional; needed before Unity asset imports get deep |
| 3 | Human | Create Unity project skeleton | **Reassigned to BE1** -- the human does not do this |
| 4 | Human | Send BE1/BE2 handoff files | Done when those devs start (they start later) |
| 5 | Human | Approve art direction | Done (contact sheet approved 2026-11-25) |
| 6 | Agent | Applicant sprites + portraits | Next art pass |
| 7 | Agent | Backend catch-up document | On request ("when I say it or when I'm stopping") |
| 8 | Agent | Unity glue layer (SaveSystem, ContentLoader, Analytics, GameManager) | After art, or on request |

### Ledger update 2026-11-26 -- item #1 (push) is no longer DEFERRED

The user joined the GitHub repo as a collaborator, so pushing is now available. Standing rule replaces
the old "never attempt pushes": **use `scripts/Connect-Remote.ps1` (dry run first); never
`--force` without `-ReplaceRemoteHistory`, and never store a credential inside the repo.**
