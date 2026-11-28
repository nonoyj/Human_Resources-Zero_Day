// HR//0D -- Unity.InputSystem compile stubs, used ONLY by the headless test project.
//
// WHY THIS FILE IS SEPARATE from UnityStubs.cs -- read tools/UnityStubs/ASSEMBLY-RULES.md first.
//
// Unity.InputSystem IS a package type, so it must never go into UnityStubs.cs, which stands in for the
// engine assembly. But the game legitimately uses it: unity/Packages/manifest.json pins
// com.unity.inputsystem 1.11.2 and HRZeroDay.Game.asmdef references Unity.InputSystem -- that is the
// allowed case, "the asmdef references it deliberately", written in the same commit as the code.
//
// The headless test project compiles the Game sources without Unity, so it needs SOMETHING to compile
// against. It includes this file; Unity never sees it, because it is outside Assets/. The rule that
// protects the editor is therefore intact: no package namespace was invented inside the engine stubs,
// and scripts/Test-ArtPipeline.ps1 still fails if anyone adds one there.
//
// If the game starts using more of the Input System, extend this file -- not UnityStubs.cs.

#if !UNITY_5_3_OR_NEWER
using System;

namespace UnityEngine.InputSystem
{
    /// <summary>Minimal Keyboard surface: one flag per key the game reads.</summary>
    public class Keyboard
    {
        public static Keyboard current { get; internal set; }

        public ButtonControl fKey = new ButtonControl();      // v130.4: the monitor's power switch
        public ButtonControl lKey = new ButtonControl();
        public ButtonControl kKey = new ButtonControl();
        public ButtonControl xKey = new ButtonControl();
        public ButtonControl hKey = new ButtonControl();
        public ButtonControl tabKey = new ButtonControl();   // real UnityEngine.InputSystem.Keyboard member
        public ButtonControl aKey = new ButtonControl();
        public ButtonControl sKey = new ButtonControl();
        public ButtonControl rKey = new ButtonControl();
        public ButtonControl escapeKey = new ButtonControl();
        public ButtonControl spaceKey = new ButtonControl();
        public ButtonControl enterKey = new ButtonControl();
        // arrow keys: real Unity.InputSystem names (upArrowKey / downArrowKey), used by the front door (v110)
        public ButtonControl wKey = new ButtonControl();
        public ButtonControl dKey = new ButtonControl();
        public ButtonControl upArrowKey = new ButtonControl();
        public ButtonControl leftArrowKey = new ButtonControl();
        public ButtonControl rightArrowKey = new ButtonControl();
        public ButtonControl downArrowKey = new ButtonControl();
        public ButtonControl digit1Key = new ButtonControl();
        public ButtonControl digit2Key = new ButtonControl();
        public ButtonControl digit3Key = new ButtonControl();
        public ButtonControl digit4Key = new ButtonControl();
        public ButtonControl digit5Key = new ButtonControl();
        public ButtonControl digit6Key = new ButtonControl();
        public ButtonControl digit7Key = new ButtonControl();
        public ButtonControl digit8Key = new ButtonControl();
        public ButtonControl digit9Key = new ButtonControl();
        public ButtonControl spaceKeyDuplicateUnused = null;
    }

    public class ButtonControl
    {
        public bool wasPressedThisFrame { get { return false; } }
        public bool isPressed { get { return false; } }
    }

    public class Mouse
    {
        public static Mouse current { get; internal set; }
        public Vector2Control position = new Vector2Control();
        public ButtonControl leftButton = new ButtonControl();
        public ButtonControl leftClick = new ButtonControl();
    }

    public class Vector2Control
    {
        public UnityEngine.Vector2 ReadValue() { return new UnityEngine.Vector2(0f, 0f); }
    }

    public class Touchscreen
    {
        public static Touchscreen current { get; internal set; }
        public TouchControl primaryTouch = new TouchControl();
    }

    public class TouchControl
    {
        public ButtonControl press = new ButtonControl();
        public Vector2Control position = new Vector2Control();
        public Vector2Control startPosition = new Vector2Control();
    }
}
#endif
