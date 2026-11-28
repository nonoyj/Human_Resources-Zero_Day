// HR//0D -- the light through the window, as a function of the shift clock.
//
// The player asked for something better than a countdown: start at day, end at night, so the room itself
// tells you the shift is nearly over. This file is the pure rule (tested headlessly); RoomLighting.cs
// applies it to sprites in the scene.
//
// DESIGN CONSTRAINTS
//   * The room is already dark and mysterious (bible #2). Bright midday would break the mood, so "day"
//     here is OVERCAST daylight -- a grey-blue high sun behind cloud, which is what a city office with a
//     night shift actually sees at 08:00. Night is the darkest, coldest step, not black.
//   * Three stops, because the art is palette-locked and the tint must stay on-palette:
//         day (shift start) -> dusk (half way) -> night (shift end)
//   * The tint is multiplied over the whole room, so it must never wash out the monitor's green or the
//     corruption orange. Values stay within ~0.75..1.0 per channel.
//   * Pure and deterministic: no Unity types, no time. Testable in tools/CoreTests.
using System;

namespace HRZeroDay.Core.Rules
{
    /// <summary>One lighting stop: how the room is tinted and how bright the window is.</summary>
    public struct LightStop
    {
        public float R, G, B;          // multiplier applied to every room pixel
        public float WindowGlow;       // 0..1 extra light bloom inside the glass
        public string Label;           // shown on the CRT header so the player can read the hour

        public LightStop(float r, float g, float b, float windowGlow, string label)
        {
            R = r; G = g; B = b; WindowGlow = windowGlow; Label = label;
        }
    }

    public static class Daylight
    {
        // Overcast noon is the "day" stop on purpose: a bright, cheerful noon would fight the tone.
        public static readonly LightStop Day   = new LightStop(1.00f, 1.00f, 1.02f, 0.85f, "08:40");
        public static readonly LightStop Dusk  = new LightStop(0.86f, 0.80f, 0.82f, 0.45f, "16:20");
        public static readonly LightStop Night = new LightStop(0.62f, 0.68f, 0.84f, 0.15f, "00:00");

        /// <summary>
        /// progress: 0 at the start of the shift, 1 at the end. Clamped, so an overrun or a paused
        /// clock can never tint the room outside the three stops.
        /// </summary>
        public static LightStop At(double progress)
        {
            if (progress <= 0) return Day;
            if (progress >= 1) return Night;
            if (progress < 0.5) return Lerp(Day, Dusk, (float)(progress / 0.5));
            return Lerp(Dusk, Night, (float)((progress - 0.5) / 0.5));
        }

        /// <summary>Progress from the shift clock: elapsed / total. 0 when there is no shift yet.</summary>
        public static double Progress(double remainingSeconds, double totalSeconds)
        {
            if (totalSeconds <= 0) return 0;
            double p = 1.0 - (remainingSeconds / totalSeconds);
            return p < 0 ? 0 : (p > 1 ? 1 : p);
        }

        /// <summary>The hour on the wall clock, so the CRT header and the sky never disagree.</summary>
        public static string ClockLabel(double progress)
        {
            int minutes = 8 * 60 + 40 + (int)Math.Round(Math.Max(0, Math.Min(1, progress)) * (15 * 60 + 20));
            int h = (minutes / 60) % 24;
            int m = minutes % 60;
            return h.ToString("00") + ":" + m.ToString("00");
        }

        private static LightStop Lerp(LightStop a, LightStop b, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return new LightStop(
                a.R + (b.R - a.R) * t,
                a.G + (b.G - a.G) * t,
                a.B + (b.B - a.B) * t,
                a.WindowGlow + (b.WindowGlow - a.WindowGlow) * t,
                t < 0.5f ? a.Label : b.Label);
        }
    }

    /// <summary>
    /// Which background art to show. The three existing room images were painted as corruption levels
    /// (L0 clean, L1, L2 breach); they are reused here as the three times of day, which is why the sky
    /// needs no new art to change: L0 is dawn/dusk grey, L1 is the flat late evening, L2 is night with the
    /// scan band. One sprite per stop, no runtime compositing.
    /// </summary>
    public static class RoomStage
    {
        public static int SpriteIndex(double progress)
        {
            if (progress < 0.34) return 0;
            if (progress < 0.72) return 1;
            return 2;
        }

        public static string Label(int index)
        {
            switch (index)
            {
                case 0: return "DAY";
                case 1: return "DUSK";
                default: return "NIGHT";
            }
        }
    }
}
