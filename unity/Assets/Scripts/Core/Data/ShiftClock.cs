// HR//0D -- ShiftClock: the only place time is allowed to pass in a shift.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine -- the Unity layer passes deltaTime in)
// Owner: Agent (written) / Dev B (owns the shift loop). Started 2026-11-26.
//
// WHY THIS IS A SEPARATE TYPE AND NOT `timeLeft -= Time.deltaTime` IN A MONOBEHAVIOUR
//   There are now FOUR states a shift clock can be in, and they were about to be scattered:
//     running      the applicant is in front of you
//     reading      the letter page is open  (user decision 2026-11-26: SLOW to 25%, never stop)
//     paused       the player hit pause      (already exists; the pause button owns this)
//     stamped      the applicant is decided, the clock keeps running to the next one
//   "Slow, do not stop" is the user's exact ruling: stopping would be exploitable, and the game
//   already has a pause button for people who need to actually stop.
using System;

namespace HRZeroDay.Core.Data
{
    public enum ClockState
    {
        Running,
        Reading,
        Paused
    }

    public static class ShiftClock
    {
        /// <summary>Seconds of shift time an applicant is worth. 3 minutes is generous on purpose.</summary>
        public const double SecondsPerApplicant = 180.0;

        /// <summary>
        /// Reading the letter runs the clock at a quarter speed. Chosen over 0 (exploitable, and the
        /// pause button already exists) and over 1 (punishes the reading the game is built around).
        /// </summary>
        public const double ReadingTimeScale = 0.25;

        public static double ScaleFor(ClockState state)
        {
            switch (state)
            {
                case ClockState.Paused: return 0.0;
                case ClockState.Reading: return ReadingTimeScale;
                default: return 1.0;
            }
        }

        /// <summary>
        /// Advances `remaining` by `deltaSeconds` of real time. Clamped at zero; never negative, because
        /// a negative shift clock makes "time ran out" comparisons wrong on the next frame.
        /// </summary>
        public static double Advance(double remaining, double deltaSeconds, ClockState state)
        {
            if (deltaSeconds < 0) deltaSeconds = 0;                 // a tampered/hitched deltaTime
            double next = remaining - deltaSeconds * ScaleFor(state);
            return next < 0 ? 0 : next;
        }

        public static bool Expired(double remaining) => remaining <= 0;

        /// <summary>
        /// Minutes:seconds for the HUD. Rounds DOWN, so the clock only reads 0:00 when it is actually out.
        /// </summary>
        public static string Format(double remaining)
        {
            if (remaining < 0) remaining = 0;
            int total = (int)Math.Floor(remaining);
            return (total / 60).ToString("0") + ":" + (total % 60).ToString("00");
        }
    }
}
