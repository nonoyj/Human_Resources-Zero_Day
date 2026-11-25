# Bug Log

Every non-trivial bug gets an entry. Format is mandatory — the **Prevention rule** line is
what stops us from repeating problems.

```
### BUG-000 — <one-line symptom>
- **Found:** <date> by <who>   **Severity:** P0/P1/P2
- **Symptom:** what the player/dev sees
- **Repro:** numbered steps
- **Root cause:** the actual reason
- **Fix:** what changed + which file/branch
- **Regression test:** the check added (or "manual: <steps>")
- **Prevention rule:** the rule we add to the checklist
```

Severity: **P0** blocks the event · **P1** breaks a core loop · **P2** cosmetic/annoying.

---

## Standing prevention rules (accumulated)

1. Never write directly to a save path — write `.tmp`, then replace.
2. Never `FindObjectOfType` inside `Update`.
3. No applicant data hardcoded in prefabs — always from JSON.
4. Every screen ships with a back affordance + safe-area anchor.
5. Any coroutine that animates a transition must be cancelled in `OnDisable`.
6. All string literals in UI come from content files.
7. Test every screen at 720×1280 *and* 1080×2400 before merging.
8. Never assume `Application.persistentDataPath` exists — create it.
9. Guard against double-tap on any irreversible action (stamp, grant access, fire).
10. Audio must survive scene loads → single persistent `AudioManager` in `00_Boot`.

---

## Entries

_No bugs logged yet — first entry will appear once the Unity scaffold is running._
