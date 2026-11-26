// HR//0D -- the room's time of day, tested as a rule (user request 2026-11-26: "start at day, then as the
// time moves until night time where the shift ends"). The light is a function of the shift clock, so it
// can be wrong in exactly two ways: it can go the wrong direction, or it can leave the palette.
using System.Collections.Generic;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Tests
{
    internal static class DaylightTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return CheckStartsAtDay();
            yield return CheckGetsDarkerMonotonically();
            yield return CheckStaysInPaletteSafeRange();
            yield return CheckClampedOutsideTheShift();
            yield return CheckClockLabelMatchesProgress();
            yield return CheckStageOrder();
        }

        private static string CheckStartsAtDay()
        {
            var a = Daylight.At(0.0);
            if (a.R < 0.99f || a.WindowGlow < 0.8f)
                return "FAIL  the shift does not start at day (r=" + a.R + " glow=" + a.WindowGlow + ")";
            return "PASS  the shift starts at day: window glow " + a.WindowGlow;
        }

        private static string CheckGetsDarkerMonotonically()
        {
            float prevGlow = 2f, prevLum = 9f;
            for (int i = 0; i <= 20; i++)
            {
                var s = Daylight.At(i / 20.0);
                float lum = s.R + s.G + s.B;
                if (s.WindowGlow > prevGlow + 0.0001f) return "FAIL  window brightens at progress " + (i / 20.0);
                if (lum > prevLum + 0.0001f) return "FAIL  room brightens at progress " + (i / 20.0);
                prevGlow = s.WindowGlow; prevLum = lum;
            }
            return "PASS  day -> night only ever gets darker, at every step of the shift";
        }

        private static string CheckStaysInPaletteSafeRange()
        {
            for (int i = 0; i <= 40; i++)
            {
                var s = Daylight.At(i / 40.0);
                if (s.R < 0.55f || s.R > 1.05f || s.G < 0.55f || s.G > 1.05f || s.B < 0.55f || s.B > 1.05f)
                    return "FAIL  tint leaves the palette-safe range at progress " + (i / 40.0) +
                           " (" + s.R + "," + s.G + "," + s.B + ")";
            }
            return "PASS  the tint stays inside 0.55..1.05 on every channel -- dark, never mangled";
        }

        private static string CheckClampedOutsideTheShift()
        {
            if (Daylight.At(-0.5).R != Daylight.Day.R) return "FAIL  a negative progress does not clamp to day";
            if (Daylight.At(1.5).R != Daylight.Night.R) return "FAIL  an overrun shift does not clamp to night";
            return "PASS  progress is clamped: overrun stays at night, never beyond it";
        }

        private static string CheckClockLabelMatchesProgress()
        {
            var start = Daylight.ClockLabel(0);
            var end = Daylight.ClockLabel(1);
            if (start != "08:40") return "FAIL  shift starts at " + start + ", expected 08:40";
            if (end != "00:00") return "FAIL  shift ends at " + end + ", expected 00:00";
            if (Daylight.ClockLabel(0.5).CompareTo(start) <= 0)
                return "FAIL  the clock does not advance with progress";
            return "PASS  clock runs 08:40 -> 00:00 and advances with progress";
        }

        private static string CheckStageOrder()
        {
            if (RoomStage.SpriteIndex(0) != 0) return "FAIL  the shift does not open on the day room";
            if (RoomStage.SpriteIndex(1) != 2) return "FAIL  the shift does not close on the night room";
            if (RoomStage.SpriteIndex(0.5) != 1) return "FAIL  the middle of the shift is not dusk";
            return "PASS  rooms go L0 day -> L1 dusk -> L2 night in that order";
        }
    }
}
