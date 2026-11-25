# Tooling Status — live

Paste the output of `.\scripts\Setup-DevEnvironment.ps1 -Verify` here (or in chat) and this
table gets updated. Until then everything is assumed `not verified`.

| Dev | Machine | D02 editor 6000.3.29f1 | D03/D04 Android | D05/D06 git+lfs | D07 VS+Unity | D09 gh | Verify run |
|---|---|---|---|---|---|---|---|
| Dev A (systems) | | ☐ | ☐ | ☐ | ☐ | n/a | ☐ |
| Dev B (UI) | | ☐ | ☐ | ☐ | ☐ | n/a | ☐ |
| Dev C (build/QA) | | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Agent (art/docs) | n/a (sandboxed) | n/a | n/a | ✔ | n/a | ✖ no connector | n/a |

Notes:
- The GitHub connector is **not available** to the agent this session; the agent maintains a local
  repo and hands you commits to push.
- Anyone whose editor is **not exactly 6000.3.29f1** must reinstall before touching `unity/`,
  otherwise `Library/` churn causes phantom merge conflicts.
