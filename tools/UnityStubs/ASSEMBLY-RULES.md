# Stub rules — read this before adding anything to UnityStubs.cs

The stubs exist so `dotnet build` can type-check the Unity-facing scripts without Unity. They are a
**compile check**, not an emulator, and their only job is to be *exactly as strict as Unity*.

## The rule

| Namespace | Assembly | Belongs in the stubs? |
|---|---|---|
| `UnityEngine` (Object, GameObject, Transform, Camera, Texture2D, SpriteRenderer, TextMesh, Input, Time, Debug, Resources, Mathf, JsonUtility, …) | engine, always referenced | **yes** |
| `UnityEditor` (AssetDatabase, TextureImporter, MenuItem, EditorUtility, EditorSceneManager, …) | editor, always referenced by an Editor asmdef | **yes** |
| `UnityEditor.U2D.Sprites` | **com.unity.2d.sprite** package | **NO** — needs an asmdef reference |
| `UnityEngine.EventSystems`, `UnityEngine.UI` | **com.unity.ugui** package | **NO** — needs an asmdef reference |
| `UnityEngine.InputSystem` | **com.unity.inputsystem** package | **NO** |

If a stub declares something from the "NO" rows, the local gate goes green while the editor fails to
compile and drops the project into **Safe Mode**. That has now cost two rounds:

- `TextureImporter.spritesheet` — removed in Unity 6, stub still had it, import silently sliced nothing.
- `UnityEngine.EventSystems` — package type, stub invented it, `HRZeroDay.Game` failed to compile
  (CS0234) and the editor opened in Safe Mode.

## What to do instead of stubbing a package type

1. **Do not reference the package for one feature.** Either the asmdef references it deliberately, or the
   code does not use it. A 480x270 single-touch game does not need uGUI's event system: `Input.GetMouseButtonDown(0)`
   fires for the first touch too.
2. If the asmdef *does* reference a package, write the asmdef reference **in the same commit** as the code.
3. Add the forbidden namespaces to the guard list in `scripts/Test-ArtPipeline.ps1` so the gate fails
   before the editor does.

## Also true of stubs

- Signatures must match Unity exactly (`Color(float,float,float)` exists; a 4-arg-only ctor is a lie).
- A stub that accepts a **removed** API is worse than no stub at all.
