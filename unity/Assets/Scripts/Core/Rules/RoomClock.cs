// HR//0D -- RoomClock: the clock on the wall, and what it is allowed to say.
// Assembly: HRZeroDay.Core  (pure C#, testable headlessly)
//
// THE PLAYER'S INSTRUCTION (v130, 2026-11-29)
//   "a clock on the wall that tells the player what time it is"
//
// WHY IT IS A RULE AND NOT A TEXTURE
//   A clock that disagrees with the shift clock is worse than no clock: it is a second source of truth that
//   the player will trust and that the game will then break. So the wall clock is DERIVED from the same
//   progress value the daylight stages use -- there is exactly one clock, drawn twice.
//
// THE SHIFT (docs/17, unchanged): the desk opens at 08:40 and the last applicant walks out at 00:00. The
//   wall clock must never show a time outside that window, must never run backwards, and must not tick
//   while the game is paused -- all three are asserted in ClockTests.
//
// THE HANDS
//   The clock is 26x26 px on the wall: a rim, four tick marks, an hour hand and a minute hand. At that size
//   a minute hand that moves every minute is noise, so the hand moves in 3-minute increments (20 positions
//   round the face) and the hour hand moves continuously. The player reads it at a glance -- "it is nearly
//   nine" -- which is all a wall clock is for in this game.
using System;

namespace HRZeroDay.Core.Rules
{
    public static class RoomClock
    {
        /// <summary>The shift opens at this many minutes past midnight.</summary>
        public const int OpenMinute = 8 * 60 + 40;      // 08:40
        /// <summary>The last applicant leaves at this many minutes past midnight.</summary>
        public const int CloseMinute = 24 * 60;         // 00:00

        /// <summary>How long the shift is, in minutes of room time.</summary>
        public static int ShiftMinutes { get { return CloseMinute - OpenMinute; } }

        /// <summary>
        /// Minutes past midnight for a progress value in 0..1. Clamped: a tampered or overrunning progress
        /// never produces a time before opening or after closing, because a clock that lies is a bug.
        /// </summary>
        public static int MinuteOfDay(float progress)
        {
            if (progress < 0f) progress = 0f;
            if (progress > 1f) progress = 1f;
            return OpenMinute + (int)Math.Round(progress * ShiftMinutes);
        }

        /// <summary>"21:05" -- always 24-hour, always four digits plus the colon, so the plate never reflows.</summary>
        public static string TwentyFour(int minuteOfDay)
        {
            int m = minuteOfDay % (24 * 60);
            if (m < 0) m += 24 * 60;
            return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
        }

        /// <summary>"9:05" on the face of the clock itself (no leading zero: a clock face never has one).</summary>
        public static string TwelveHour(int minuteOfDay)
        {
            int m = minuteOfDay % (24 * 60);
            if (m < 0) m += 24 * 60;
            int h = m / 60 % 12;
            if (h == 0) h = 12;
            return h.ToString() + ":" + (m % 60).ToString("00");
        }

        /// <summary>"EVENING" / "NIGHT" / "MORNING" -- the word under the clock. Cheap, and it teaches the arc.</summary>
        public static string PartOfShift(int minuteOfDay)
        {
            int m = minuteOfDay % (24 * 60);
            if (m < 12 * 60) return "MORNING";
            if (m < 18 * 60) return "AFTERNOON";
            if (m < 21 * 60) return "EVENING";
            return "NIGHT";
        }

        /// <summary>
        /// Hour hand angle in degrees, 0 = straight up, clockwise. Continuous, so it creeps like a real one.
        /// </summary>
        public static float HourAngle(int minuteOfDay)
        {
            int m = minuteOfDay % (12 * 60);
            if (m < 0) m += 12 * 60;
            return 360f * (m / (12f * 60f));
        }

        /// <summary>
        /// Minute hand angle in degrees, 0 = straight up, clockwise. Snapped to 3-minute steps: 120 degrees of
        /// travel across the whole shift would otherwise be sub-pixel movement that reads as a shimmer.
        /// </summary>
        public static float MinuteAngle(int minuteOfDay, int stepMinutes = 3)
        {
            if (stepMinutes < 1) stepMinutes = 1;
            int m = minuteOfDay % 60;
            if (m < 0) m += 60;
            int snapped = (m / stepMinutes) * stepMinutes;
            return 360f * (snapped / 60f);
        }

        /// <summary>
        /// The hand position in whole pixels from the face centre. Integer maths on purpose: a hand that
        /// lands on a half pixel is a blurred hand, and this game does not blur.
        /// </summary>
        public static void HandTip(float angleDeg, float lengthPx, out int dx, out int dy)
        {
            double rad = (angleDeg - 90.0) * Math.PI / 180.0;   // 0 deg = up = -y on a top-down canvas
            dx = (int)Math.Round(Math.Cos(rad) * lengthPx);
            dy = (int)Math.Round(Math.Sin(rad) * lengthPx);
        }
    }
}
