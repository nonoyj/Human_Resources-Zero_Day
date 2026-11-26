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
        public DeskBootstrap bootstrap;

        private void Awake()
        {
            if (bootstrap == null) bootstrap = GetComponent<DeskBootstrap>();
            if (bootstrap == null) bootstrap = FindObjectOfType<DeskBootstrap>();
        }

        private void Update()
        {
            var game = bootstrap;
            if (game == null) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.lKey.wasPressedThisFrame) game.ToggleLetter();
                if (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) game.CloseLetter();
                if (kb.hKey.wasPressedThisFrame) game.ToggleHelp();
                if (kb.aKey.wasPressedThisFrame) game.Approve();
                if (kb.sKey.wasPressedThisFrame) game.Reject();
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) game.Continue();
                if (kb.rKey.wasPressedThisFrame && game.ShowEnding) game.Restart();

                if (kb.digit1Key.wasPressedThisFrame) game.NumberKey(1);
                if (kb.digit2Key.wasPressedThisFrame) game.NumberKey(2);
                if (kb.digit3Key.wasPressedThisFrame) game.NumberKey(3);
                if (kb.digit4Key.wasPressedThisFrame) game.NumberKey(4);
                if (kb.digit5Key.wasPressedThisFrame) game.NumberKey(5);
                if (kb.digit6Key.wasPressedThisFrame) game.NumberKey(6);
            }

            Vector2 point;
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
        }

        /// <summary>Only the applicant hotspots are world-space click targets; UI has keyboard keys.</summary>
        private void DispatchPointer(Vector2 screenPoint)
        {
            if (bootstrap == null || bootstrap.LetterOpen || bootstrap.ShowDebrief ||
                bootstrap.ShowDaySummary || bootstrap.ShowEnding) return;

            var cam = Camera.main;
            if (cam == null) cam = FindObjectOfType<Camera>();
            if (cam == null || !cam.orthographic) return;

            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            float halfH = cam.orthographicSize;
            float halfW = halfH * aspect;
            float nx = screenPoint.x / Mathf.Max(1, Screen.width) - 0.5f;
            float ny = screenPoint.y / Mathf.Max(1, Screen.height) - 0.5f;
            var world = new Vector2(cam.transform.position.x + nx * 2f * halfW,
                                    cam.transform.position.y + ny * 2f * halfH);

            var hotspots = GetComponentsInChildren<HotspotInteractor>();
            for (int i = 0; i < hotspots.Length; i++)
            {
                var hotspot = hotspots[i];
                var box = hotspot.GetComponent<BoxCollider2D>();
                if (box == null) continue;
                var centre = new Vector2(hotspot.transform.position.x, hotspot.transform.position.y);
                var half = new Vector2(box.size.x * hotspot.transform.lossyScale.x * 0.5f,
                                       box.size.y * hotspot.transform.lossyScale.y * 0.5f);
                if (Mathf.Abs(world.x - centre.x) <= half.x && Mathf.Abs(world.y - centre.y) <= half.y)
                {
                    hotspot.OnPointerDown(null);
                    return;
                }
            }
        }
    }
}
