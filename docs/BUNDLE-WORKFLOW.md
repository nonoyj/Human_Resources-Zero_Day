# Bundle workflow -- how to take an update

**Status:** current (2026-11-25, bundle v77) - **Read this instead of guessing.** The old version of this
page was four paragraphs of history and one wrong command, which is how the player ended up running
`.\scripts\Open-Game.ps1` before the file existed.

---

## The only two commands you need

The zip is in **Downloads**. `Apply-Bundle.ps1` is in **Downloads** too. The repo is somewhere else.
Those are three different folders, and mixing them up is the single most common failure.

```powershell
cd $env:USERPROFILE\Downloads
.\Apply-Bundle.ps1 -BundlePath .\HR-ZeroDay-bundle-v77.zip -Clean
```

Then, **only after that finishes**:

```powershell
cd $env:USERPROFILE\HR-ZeroDay
.\scripts\Open-Game.ps1
```

`Open-Game.ps1` ships *inside* the bundle, so it does not exist until the apply has run. Running it first
gives `The term '.\scripts\Open-Game.ps1' is not recognized` -- that is not a broken script, it is the
wrong order.

You do **not** need to close Unity any more. The apply retries the `.git` swap instead of failing on a
locked handle, and Unity reimports on the next focus.

---

## After the apply, in Unity: press Play

That is the whole ritual. `GameBoot.cs` builds the desk at `AfterSceneLoad` when no `DeskBootstrap` exists,
so an empty scene boots the game.

**This only became true on 2026-11-25 (BUG-044).** Until then the runtime art lived in an ignored folder
and Play logged `sprite not found at Resources/Art/Kit/...` on any machine that had not run the art
scripts. The runtime mirror is committed now, so a clone is self-sufficient.

The `HR//0D` menu (note: **two** slashes, `HR//0D`, not `HR`) is needed **only** when the PNGs themselves
changed -- it reimports the art kit and rewrites the scene:

| Menu item | When you actually need it |
|---|---|
| `HR//0D > Setup Project (one click)` | first open after a bundle that changed art, or a broken Game view |
| `HR//0D > Publish Art To Resources` | only if `art/kit` changed and `Publish` was not run |
| `HR//0D > Build Desk Scene` | you deleted or broke `Assets/Scenes/Desk.unity` |
| `HR//0D > Open Desk Scene` | convenience; Play works without it |

---

## Once the repo is pushed: stop using bundles

```powershell
cd $env:USERPROFILE\HR-ZeroDay
git pull
```

Bundles exist because the agent sandbox cannot authenticate to GitHub. Pushing happened on 2026-11-25
(`1d94711`), so `git pull` is now the normal path and a bundle is only for a machine with no clone.

---

## If something goes wrong

| Symptom | Cause | Fix |
|---|---|---|
| `bundle not found` | you are in the repo, the zip is in Downloads | `cd $env:USERPROFILE\Downloads` first |
| `Open-Game.ps1 is not recognized` | ran it before applying the bundle | apply first, it ships inside the zip |
| `Enter Safe Mode?` | a compile error, see the Console | the gate now catches these: `.\scripts\Test-ArtPipeline.ps1` |
| apply fails on `.git` | a process holds it | close Unity and re-run, or `git pull` |
| Game view is black | art not published to Resources | `HR//0D > Setup Project (one click)` |
