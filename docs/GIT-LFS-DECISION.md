# Why this project does not use Git LFS (BUG-010)

**Short version:** the LFS filter was configured in `.gitattributes` but the files were committed as
normal blobs, and on a machine with the LFS client installed git then considered every PNG *modified*
forever. That is why `Update-FromBundle.ps1` reported:

```
 M art/concepts/desk_concept_A.png
 M art/concepts/menu_concept_A.png
```

even though nobody had touched them.

## What went wrong

1. `.gitattributes` declared `*.png filter=lfs ...` — telling git "these files must be stored as LFS
   pointers".
2. The files were committed here (in the agent's sandbox) where the **LFS client is not installed**, so
   git stored the real PNG bytes instead of pointers. The history is therefore *not* LFS.
3. On your machine, the LFS client **is** installed (we did it in setup). Now git applies the LFS
   clean/smudge filter on read, sees "this should be an LFS pointer but it is a real file", and reports
   the file as modified on every `git status`.
4. Consequence: every future update is blocked by a false-positive, and a `git commit` would rewrite
   those PNGs into LFS pointers, fragmenting the art history.

My fault: I mixed two incompatible states (filter declared / content not migrated) and never verified
`git status` on a machine where the filter was active.

## The decision

**No LFS for this project.** Reasons:

| Factor | Reality here |
|---|---|
| Asset sizes | Pixel art PNGs: 5–80 KB. Concept mockups: 1–2 MB but only two. SFX: 20–200 KB. |
| Total art volume at ship | Estimated < 25 MB for the whole game |
| Cost of LFS | Every teammate must install the client *before* cloning; GitHub free LFS is 1 GB storage / 1 GB per month bandwidth |
| Benefit of LFS | None at this size |
| Risk of LFS | Exactly what happened: a clone-level assumption that silently breaks `git status` |

`.gitattributes` now marks art as **binary** (no diff, no line-ending conversion) without any filter.

## If you ever do need LFS (large PSDs, long audio, video trailers)

Do it in this order, with the whole team notified:

```powershell
# 1. every machine
winget install GitHub.GitLFS
git lfs install

# 2. re-add the filter lines to .gitattributes
#    *.psd filter=lfs diff=lfs merge=lfs -text

# 3. rewrite history that touches those files (one machine only)
git lfs migrate import --include="*.psd,*.wav,*.mp4" --everything

# 4. everyone else re-clones (their old clone is invalid)
```

Never mix the two states again: **either no filter at all, or filter + migrated history.**

## Verification (run on any machine, any time)

```powershell
cd C:\users\user\HR-ZeroDay
git status --short            # must be empty on a clean machine
git check-attr -a .\art\concepts\desk_concept_A.png
# expected: binary: set / diff: unset / merge: unset / text: unset
```

If `filter: lfs` appears there, your `.gitattributes` is stale — replace it from the repo.
