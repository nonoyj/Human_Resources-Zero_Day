// HR//0D -- touch/mouse/keyboard input for the first playable minute.
// Assembly: HRZeroDay.Game
//
// Uses the NEW Input System only (docs/02-tech-stack.md). Hotspot picking uses an orthographic
// screen-to-world conversion instead of the EventSystem: the scene has 2D colliders but no Canvas,
// and this keeps pointer handling identical in the Editor, on a phone and in a player build.
// All gameplay actions are delegated to DeskBootstrap; this file owns no rules.
using UnityEngine;
using UnityEngine.InputSystem;

namespace HRZeroDay.Game.Interaction
{
    public sealed class GameInputRouter : MonoBehaviour
    {
        /// <summary>The layout's canvas. Every hit test in the game is expressed in these pixels.</summary>
        private const int ReferenceWidth = 480;
        private const int ReferenceHeight = 270;
        private const float ReferencePpu = 16f;

        public DeskBootstrap bootstrap;

        private void Awake()
        {
            if (bootstrap == null) bootstrap = GetComponent<DeskBootstrap>();
            if (bootstrap == null) bootstrap = FindFirstObjectByType<DeskBootstrap>();
        }

        /// <summary>
        /// Screen pixels -> the layout's 480x270 canvas pixels. Same orthographic conversion the hotspot
        /// picker uses, so the menu's hit tests agree with everything else in the game.
        /// </summary>
        private void ToCanvas(UnityEngine.Vector2 screen, out int x, out int y)
        {
            var cam = Camera.main;
            if (cam == null) { x = y = -1; return; }
            int sw = Screen.width > 0 ? Screen.width : 1;
            int sh = Screen.height > 0 ? Screen.height : 1;
            int cx = (int)(screen.x * ReferenceWidth / sw);
            int cy = (int)((sh - screen.y) * ReferenceHeight / sh);
            x = cx < 0 ? 0 : (cx > ReferenceWidth - 1 ? ReferenceWidth - 1 : cx);
            y = cy < 0 ? 0 : (cy > ReferenceHeight - 1 ? ReferenceHeight - 1 : cy);
        }

        private void Update()
        {
            var game = bootstrap;
            if (game == null) return;

            var kb = Keyboard.current;

            // THE FRONT DOOR OWNS EVERY KEY WHILE IT IS UP (v110). It comes before the intro and before the
            // desk: nothing behind the menu may move, and no key may stamp an applicant the player cannot see.
            if (game.ShowingMenu)
            {
                if (kb != null)
                {
                    // ONE key, one action. v110 could fire twice on a frame where two bindings were true
                    // (arrow and WASD both pressed, say), which is how a toggle appears not to work at all.
                    if (kb.escapeKey.wasPressedThisFrame) { game.MenuBack(); return; }
                    if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                    { game.MenuChoose(); return; }
                    if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                    { game.MenuMove(-1); return; }
                    if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
                    { game.MenuMove(1); return; }
                    // LEFT/RIGHT step the selected row -- the way back down a volume (v111)
                    if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                    { game.MenuStep(-1); return; }
                    if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                    { game.MenuStep(1); return; }
                }
                // THE POINTER (v119). Hover is tracked every frame so the row under the cursor lights up,
                // and a click is resolved against the row first and the room's props second. A click that
                // lands on nothing still chooses -- the old behaviour -- so a keyboard player who happens to
                // click somewhere harmless is never stuck.
                var mm = Mouse.current;
                var ts = Touchscreen.current;
                int px = -1, py = -1;
                if (mm != null) { var pos = mm.position.ReadValue(); ToCanvas(pos, out px, out py); }
                else if (ts != null && ts.primaryTouch.press.isPressed)
                { var pos = ts.primaryTouch.position.ReadValue(); ToCanvas(pos, out px, out py); }

                if (px >= 0) game.MenuPointer(px, py);
                else game.MenuPointerLeave();

                bool mouseClick = mm != null && mm.leftButton.wasPressedThisFrame;
                bool touchTap = ts != null && ts.primaryTouch.press.wasPressedThisFrame;
                bool tappedMenu = mouseClick || touchTap;
                // A THUMB IS NOT A CURSOR (v120): the menu asks twice for a decision on touch only.
                bool byTouch = touchTap && !mouseClick;
                if (tappedMenu && px >= 0)
                {
                    // THE ROOM IS NOT A BUTTON (v119.5, BUG-086). This line used to read
                    // `if (px < 0 || !game.MenuClick(px, py)) game.MenuChoose();` -- so a click that hit
                    // NOTHING chose whatever row happened to be selected. Combined with the old clamp in
                    // MenuLayout.RowAt, that meant a click anywhere on screen pressed CREDITS. Now the
                    // click has to land on a row or a prop, and a click on the room does nothing at all,
                    // which is what makes the props read as props.
                    game.MenuClick(px, py, byTouch);
                }
                return;
            }

            // THE INTRO OWNS EVERY KEY WHILE IT IS UP (v102). It is the first thing the player sees, and
            // nothing behind it may move: pressing A during the intro must not stamp the first applicant.
            // SPACE/ENTER steps a beat, X/ESC leaves, everything else is swallowed until it closes.
            if (game.ShowingIntro)
            {
                if (kb != null)
                {
                    if (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) game.IntroSkip();
                    else if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) game.IntroAdvance();
                }
                var m0 = Mouse.current;
                bool tapped = (m0 != null && m0.leftButton.wasPressedThisFrame) ||
                              (Touchscreen.current != null &&
                               Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
                if (tapped) game.IntroAdvance();
                return;
            }

            if (kb != null)
            {
                if (kb.lKey.wasPressedThisFrame) game.ToggleLetter();
                if (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) game.CloseLetter();
                if (kb.hKey.wasPressedThisFrame) game.ToggleHelp();
                // ALERTS (v119.6): TAB is the one key the desk and the letter both honour, because the panel is
                // read-only and never steals a decision. ESC closes it before it closes anything else.
                if (kb.tabKey.wasPressedThisFrame) game.ToggleAlerts();
                else if (game.AlertsOpen && (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame))
                { game.CloseAlerts(); return; }
                if (kb.aKey.wasPressedThisFrame) game.Approve();
                if (kb.sKey.wasPressedThisFrame) game.Reject();
                // The walk owns SPACE while it is on screen: turning a page must never also advance the
                // day behind it. Order matters here, not cleverness.
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                {
                    if (!game.SweepAdvance()) game.Continue();
                }
                if (kb.rKey.wasPressedThisFrame && game.ShowEnding) game.Restart();

                if (kb.digit1Key.wasPressedThisFrame) game.NumberKey(1);
                if (kb.digit2Key.wasPressedThisFrame) game.NumberKey(2);
                if (kb.digit3Key.wasPressedThisFrame) game.NumberKey(3);
                if (kb.digit4Key.wasPressedThisFrame) game.NumberKey(4);
                if (kb.digit5Key.wasPressedThisFrame) game.NumberKey(5);
                if (kb.digit6Key.wasPressedThisFrame) game.NumberKey(6);
            }

            // CS0165: this was declared unassigned and then passed to DispatchPointer under a flag the
            // compiler cannot prove, so the Game assembly did not compile at all (Unity too, not just the
            // headless gate).
            Vector2 point = Vector2.zero;
            bool pressed = false;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                point = mouse.position.ReadValue();
                pressed = true;
            }
            if (!pressed && Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch != null && touch.press.wasPressedThisFrame)
                {
                    point = touch.position.ReadValue();
                    pressed = true;
                }
            }
            if (pressed) DispatchPointer(point);
            else if (mouse != null) UpdateHoverOnly(mouse.position.ReadValue());
        }

        /// <summary>
        /// Turns a touch into an artifact. The applicant's hotspots are the only world-space targets;
        /// the rest of the desk is keyboard.
        ///
        /// WHAT WAS WRONG (found 2026-11-26, the player's report: "I can't click anything")
        ///   The old version derived the canvas point from the CAMERA (orthographicSize x pixel aspect).
        ///   That is only the same as the layout's 480x270 canvas when the window is exactly 16:9. In any
        ///   other window the whole canvas is letterboxed inside the view, so every hit was offset by
        ///   part of the bar -- small near the middle, huge at the edges. It also tested "is the point
        ///   inside objectOrigin +- colliderSize/2", which is only true where the hotspot object happens
        ///   to sit, not where the player sees the body part.
        ///
        /// Now: one normalised pointer -> the SAME canvas rect the layout and the art use, then the
        /// smallest containing box wins (so the eyes' 40x28 'no_blink' target is not swallowed by the
        /// 60x16 glasses band). The mapping is a pure function in HotspotHitTest, so it is tested.
        private void DispatchPointer(Vector2 screenPoint)
        {
            // THE WALK TAKES A TAP LIKE IT TAKES SPACE (v97). The player's report was literal: "I tried
            // clicking everything in the walk page but nothing responds." The strip says [SPACE] and it
            // was the only way forward -- correct for a keyboard, hostile on a phone and non-obvious on a
            // desktop. A tap anywhere on the page now turns it, which is what a page turn should always
            // have been; the desk behind it is untouched while the walk is up.
            if (bootstrap != null && bootstrap.ShowingSweep) { bootstrap.SweepAdvance(); return; }

            if (bootstrap == null || bootstrap.ShowingSweep) return;
            if (bootstrap.ShowingMenu || bootstrap.ShowingIntro) return;

            int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            var canvas = new PixelRect(0f, 0f, ReferenceWidth, ReferenceHeight);
            var point = HotspotHitTest.ScreenToCanvasNdc(
                new PixelPoint((float)screenPoint.x / w, (float)screenPoint.y / h), canvas);
            // HotspotHitTest returns canvas px with y UP; HudRules.Rects are top-down, like the page art.
            int hx = Mathf.RoundToInt(point.X);
            int hy = Mathf.RoundToInt(ReferenceHeight - point.Y);

            // THE HUD IS ON TOP OF THE ROOM (v126). It used to be prose, so a click anywhere went to the
            // desk. Now it is a rack of objects, and a press on APPROVE must not also land on the applicant
            // standing behind it -- an approve that inspects a body part is a way to lose a shift by
            // accident. The objects get the click first, and if one takes it, the desk never sees it.
            //
            // The exception is the LETTER's own page: while a letter is open the page covers the middle of
            // the screen and its own gestures (page turns) live on the page, but the flag tabs and the stamp
            // pads are the letter's edge and must stay live. HudRules already decides which objects exist in
            // that state, so there is nothing to special-case here.
            if (bootstrap.HudObjects != null && !bootstrap.HudObjects.IsEmpty && bootstrap.HudObjects.Click(hx, hy))
                return;

            // Whatever the object layer did NOT take: the letter page and the three full-screen states own
            // the rest of the screen, and the desk behind them is inert. This used to be one long `return`
            // that also turned off the hotspots; it is now after the objects, so a flag tab and a stamp pad
            // still work while the page is open -- which is the whole gesture of the letter.
            if (bootstrap.LetterOpen || bootstrap.ShowDebrief ||
                bootstrap.ShowDaySummary || bootstrap.ShowEnding) return;

            if (bootstrap.HudObjects != null && !bootstrap.HudObjects.IsEmpty) bootstrap.HudObjects.Hover(hx, hy);

            var best = FindAt(screenPoint);
            // hover: brighten the bracket under the mouse, so a desktop player can see the target before
            // committing to a click. Cheap -- it only touches a colour when the hovered hotspot CHANGES.
            if (Mouse.current != null) SetHovered(best);

            if (best != null) { best.OnPointerDown(null); return; }

            // A tap on the callout dismisses it. Without this the panel sits over the applicant and
            // swallows the very body parts the player is trying to inspect (the 2026-11-26 report:
            // "opens something on the screen after that I can't click anything else").
            var overlay = bootstrap.GetComponentInChildren<DeskEvidenceOverlay>(true);
            if (overlay != null && overlay.Contains(new Vector2(point.X, point.Y)))
            {
                overlay.Hide();
                return;
            }

            // A tap on the desk that hits no hotspot dismisses any open callout, so the desk is never
            // left in a state where the player cannot tell what is clickable.
            if (overlay != null) overlay.Hide();
        }

        private HotspotInteractor _hovered;

        /// <summary>
        /// The hotspot under a screen point, smallest first. ONE implementation, used by both the click and
        /// the hover path -- the bug that made everything unclickable was partly two copies of this test.
        /// </summary>
        private HotspotInteractor FindAt(Vector2 screenPoint)
        {
            if (bootstrap == null) return null;
            int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            var canvas = new PixelRect(0f, 0f, ReferenceWidth, ReferenceHeight);
            var point = HotspotHitTest.ScreenToCanvasNdc(
                new PixelPoint((float)screenPoint.x / w, (float)screenPoint.y / h), canvas);

            var hotspots = GetComponentsInChildren<HotspotInteractor>();
            HotspotInteractor best = null;
            float bestArea = float.MaxValue;
            for (int i = 0; i < hotspots.Length; i++)
            {
                var hotspot = hotspots[i];
                var box = hotspot.GetComponent<BoxCollider2D>();
                if (box == null) continue;
                var centre = CanvasOf(hotspot);
                float scale = hotspot.transform.lossyScale.x;
                if (scale <= 0f) scale = 1f;
                var half = new Vector2(box.size.x * scale * ReferencePpu * 0.5f,
                                       box.size.y * scale * ReferencePpu * 0.5f);
                if (Mathf.Abs(point.X - centre.x) > half.x || Mathf.Abs(point.Y - centre.y) > half.y) continue;
                float area = half.x * half.y;
                if (area < bestArea) { best = hotspot; bestArea = area; }
            }
            return best;
        }

        /// <summary>Hover without a click: same target search, no side effects beyond the bracket.</summary>
        private void UpdateHoverOnly(Vector2 screenPoint)
        {
            // THE HUD OBJECTS HOVER TOO (v126). A clickable plate that does not change when the pointer is
            // over it is a plate a player will not believe is clickable, so hover has to reach the object
            // layer as well as the desk hotspots. It is fed the same canvas point the click path computes.
            if (bootstrap != null && bootstrap.HudObjects != null && !bootstrap.HudObjects.IsEmpty)
            {
                int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
                var canvas = new PixelRect(0f, 0f, ReferenceWidth, ReferenceHeight);
                var point = HotspotHitTest.ScreenToCanvasNdc(
                    new PixelPoint((float)screenPoint.x / w, (float)screenPoint.y / h), canvas);
                bootstrap.HudObjects.Hover(Mathf.RoundToInt(point.X),
                                            Mathf.RoundToInt(ReferenceHeight - point.Y));
            }

            if (bootstrap == null || bootstrap.LetterOpen || bootstrap.ShowDebrief ||
                bootstrap.ShowDaySummary || bootstrap.ShowEnding || bootstrap.ShowingSweep)
            {
                SetHovered(null);
                return;
            }
            SetHovered(FindAt(screenPoint));
        }

        private void SetHovered(HotspotInteractor next)
        {
            if (_hovered == next) return;
            if (_hovered != null) _hovered.SetHover(false);
            _hovered = next;
            if (_hovered != null) _hovered.SetHover(true);
            if (bootstrap != null) bootstrap.SetHoveredArtifact(next == null ? null : next.artifact);
        }

        /// <summary>
        /// A hotspot's position in canvas pixels. Hotspots are children of L_applicant, which carries the
        /// layout's scaleFactor, so the world position is converted by ppu and multiplied by the parent
        /// chain's scale -- Unity's lossyScale already includes it.
        /// </summary>
        private Vector2 CanvasOf(HotspotInteractor hotspot)
        {
            var p = hotspot.transform.position;
            float scale = hotspot.transform.lossyScale.x;
            if (scale <= 0f) scale = 1f;
            return new Vector2(p.x * ReferencePpu / scale, p.y * ReferencePpu / scale);
        }
    }
}
