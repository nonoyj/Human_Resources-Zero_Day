// HR//0D -- RoomClockView: the wall clock's hands, turned by the shift's own progress.
// Assembly: HRZeroDay.Game
//
// WHAT IT DOES
//     Draws the clock's two hands onto a small texture the size of the clock face, once per minute of room
//     time, and puts it over the face that make_desk_room.wall_clock() painted. The face is furniture and is
//     baked into the room sprite; the HANDS are the only part that moves, which is one 30x30 texture upload
//     per minute, i.e. nothing.
//
// WHY IT IS NOT A SPRITE PER HAND
//     A hand is 1-2 px wide at 26-30 px across. Two SpriteRenderers rotated by transform would land on
//     fractional positions and be resampled -- a blurred hand in a game that never blurs anything. Painting
//     whole pixels from RoomClock's integer HandTip() is the same rule the rest of the art follows.
//
// WHERE THE TIME COMES FROM
//     RoomClock.MinuteOfDay(progress), and `progress` is the SAME value DeskLightDriver feeds the daylight
//     tint. One clock, drawn twice: the room's light and the wall's time cannot disagree.
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game
{
    public class RoomClockView : MonoBehaviour
    {
        public const string RootName = "RoomClock";

        /// <summary>
        /// The clock face's rect in canvas px, top-down (left, top, size). Mirrors
        /// make_desk_room.CLOCK_X / CLOCK_Y / CLOCK_SIZE.
        ///
        /// v130.4 (the player: "The wall clock is covered by the monitor transfer it to the center"): the face
        /// was painted at x 388 on a 480-wide canvas, and the CRT is mounted at x 268 with a 186-wide body --
        /// the clock sat BEHIND the monitor. A clock you cannot see is not a clock. It now sits at x 226, which
        /// is the centre of the wall segment the player actually looks at, clear of the monitor's left edge
        /// (268) by 12 px and clear of the window's right edge (52+116=168) by 28 px.
        /// </summary>
        public int x = 203, y = 22, size = 30;

        public float progress;        // 0..1, written by DeskLightDriver every frame
        private int _lastMinute = -1;
        private Texture2D _tex;
        private SpriteRenderer _sr;

        public static RoomClockView Attach(Transform root)
        {
            var existing = root.Find(RootName);
            if (existing != null) return existing.GetComponent<RoomClockView>();
            var go = new GameObject(RootName);
            go.transform.SetParent(root, false);
            return go.AddComponent<RoomClockView>();
        }

        private void Start()
        {
            _tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.name = "ClockHands";
            _sr = gameObject.GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            _sr.sortingOrder = -25;          // over the room painting, under the CRT
            Repaint(force: true);
        }

        private void Update()
        {
            int minute = RoomClock.MinuteOfDay(progress);
            if (minute == _lastMinute) return;
            Repaint(force: false);
        }

        /// <summary>Sets the hands for a progress value. Public so a test or the editor can pose it.</summary>
        public void Repaint(bool force)
        {
            if (_tex == null) return;
            int minute = RoomClock.MinuteOfDay(progress);
            _lastMinute = minute;

            // Color32 has no parameterless constructor in the stubs (and none in Unity either), so the
            // transparent face is written out: a hand texture with alpha 0 everywhere except the ink.
            var bg = new Color32[size * size];
            for (int i = 0; i < bg.Length; i++) bg[i] = new Color32(0, 0, 0, 0);
            var ink = new Color32(11, 10, 15, 255);          // void: the darkest palette entry
            var c = size / 2;

            int hx, hy, mx, my;
            RoomClock.HandTip(RoomClock.HourAngle(minute), size * 0.20f, out hx, out hy);
            RoomClock.HandTip(RoomClock.MinuteAngle(minute), size * 0.33f, out mx, out my);

            HLine(bg, c, c, c + hx, c + hy, ink);
            HLine(bg, c, c, c + mx, c + my, ink);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    Plot(bg, c + dx, c + dy, ink);

            _tex.SetPixels32(bg);
            _tex.Apply(false);

            // place it over the face: canvas px, y-UP anchor like every other layer
            float ppu = 16f;
            transform.position = new Vector3((x + size * 0.5f) / ppu, (270 - y - size * 0.5f) / ppu, 0f);
        }

        /// <summary>A 1 px line, integer endpoints, no anti-aliasing: Bresenham in two lines.</summary>
        private void HLine(Color32[] buf, int x0, int y0, int x1, int y1, Color32 col)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Plot(buf, x0, y0, col);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void Plot(Color32[] buf, int px, int py, Color32 col)
        {
            if (px < 0 || py < 0 || px >= size || py >= size) return;
            buf[py * size + px] = col;
        }
    }
}
