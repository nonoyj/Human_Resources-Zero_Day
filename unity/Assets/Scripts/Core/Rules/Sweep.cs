// HR//0D -- the after-hours sweep. What the player's own mistakes left in the office.
//
// IDEA (from the team, 2026-11-26): after the shift ends there is a routine walk-through of the office,
// and it hints at the AI that got through. This file is that idea as a rule rather than a cutscene, so it
// can be tested and so it cannot cheat.
//
// THE DESIGN CONSTRAINT THAT MAKES IT HONEST
//   The sweep may only report things the player actually caused. If it invented creepy events, it would be
//   a jump-scare; because every trace is keyed to a specific synthetic the player approved, it is a
//   receipt. That also means it works as a teaching tool: "this is the tell you missed, and here is what
//   it is doing now."
//
//   One trace per hired synthetic, never more, and the traces escalate: the first is deniable (a mug that
//   moved), the last is not (a chair still warm, a badge photo that is not the person who came in). The
//   order a trace appears in comes from the shift, not from a random number, so a player who hires
//   nothing gets a quiet, empty office -- which is its own reward and its own kind of unsettling.
//
// Pure C#, no UnityEngine: tested headlessly in tools/CoreTests/SweepTests.cs.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Core.Rules
{
    /// <summary>One thing left behind in the office after the shift.</summary>
    public sealed class SweepTrace
    {
        public string Id;                 // stable id for art + telemetry
        public string Title;              // one line, plain language
        public string Detail;             // the second line: what it means
        public int Severity;              // 1 deniable .. 3 unmistakable
        public string Floor;              // which part of the office: desk / corridor / lift / server
        public string CauseArchetype;     // the synthetic whose hire put it there (null = always present)
    }

    public static class Sweep
    {
        /// <summary>Traces, cheapest to most damning. Keyed by corruption level (0..3).</summary>
        private static readonly SweepTrace[] ByLevel =
        {
            new SweepTrace { Id = "quiet", Severity = 0, Floor = "desk",
                Title = "The office is exactly as you left it.",
                Detail = "Everyone you passed through was a person. The lift panel is warm.", CauseArchetype = null },

            new SweepTrace { Id = "mug_moved", Severity = 1, Floor = "desk",
                Title = "Your mug has been moved to the other side of the desk.",
                Detail = "Nobody else has a key. It is still warm.", CauseArchetype = "synthetic_polished" },

            new SweepTrace { Id = "chair_warm", Severity = 2, Floor = "corridor",
                Title = "The interview chair is still warm.",
                Detail = "The badge you filed is face down on the seat. It is a different face.", CauseArchetype = "synthetic_cloned" },

            new SweepTrace { Id = "lift_waits", Severity = 3, Floor = "lift",
                Title = "The lift is holding on your floor with the doors open.",
                Detail = "It has been holding since 00:00. The call button is lit from the inside.", CauseArchetype = "synthetic_collusion" },
        };

        /// <summary>
        /// Builds the sweep for a finished day. `corruption` is ShiftController.CorruptionLevel (0..3).
        /// Returns at least one trace; a clean day returns the quiet one, never an empty list, so the sweep
        /// never reads as "the game forgot to say anything".
        /// </summary>
        public static List<SweepTrace> AfterShift(int corruption, IList<ApplicantRecord> today)
        {
            var traces = new List<SweepTrace>();
            int level = corruption < 0 ? 0 : (corruption > 3 ? 3 : corruption);

            if (level == 0)
            {
                traces.Add(ByLevel[0]);
                return traces;
            }

            // one trace per hired synthetic, capped at the level, escalating with it
            for (int i = 1; i <= level && i < ByLevel.Length; i++)
                traces.Add(WithCause(ByLevel[i], today));
            return traces;
        }

        /// <summary>
        /// Whether the walk happens at all. DECIDED 2026-11-26: only on days when something got through.
        /// A quiet office is not a scene -- it is the summary screen, and that keeps the walk meaningful
        /// instead of a chore the player learns to skip.
        /// </summary>
        public static bool HasSweep(int corruption) { return corruption > 0; }

        /// <summary>How long the player has to walk the office, in seconds. Fixed: no clock pressure here.</summary>
        public const double WalkSeconds = 45.0;

        /// <summary>
        /// The floor the sweep must end on. Ending at the lift on a bad day is the point: the doors were
        /// already open.
        /// </summary>
        public static string FinalFloor(IList<SweepTrace> traces)
        {
            if (traces == null || traces.Count == 0) return "desk";
            var worst = traces[0];
            for (int i = 1; i < traces.Count; i++)
                if (traces[i].Severity > worst.Severity) worst = traces[i];
            return worst.Floor;
        }

        /// <summary>Archetype -> the art family that owns the trace: polished / cloned / collusion.</summary>
        public static string ArchetypeFamily(Archetype a)
        {
            switch (a)
            {
                case Archetype.SyntheticPolished:  return "synthetic_polished";
                case Archetype.SyntheticCloned:    return "synthetic_cloned";
                case Archetype.SyntheticCollusion: return "synthetic_collusion";
                default:
                    return "human";
            }
        }

        private static SweepTrace WithCause(SweepTrace template, IList<ApplicantRecord> today)
        {
            var copy = new SweepTrace
            {
                Id = template.Id, Title = template.Title, Detail = template.Detail,
                Severity = template.Severity, Floor = template.Floor,
                CauseArchetype = template.CauseArchetype
            };

            // Name the actual person who came in, when we can: "the badge is a different face" lands
            // harder if the player remembers interviewing them an hour ago.
            if (today != null && !string.IsNullOrEmpty(copy.CauseArchetype))
            {
                for (int i = 0; i < today.Count; i++)
                {
                    var a = today[i];
                    if (a == null) continue;
                    // Archetype is an enum on the record; map it to the art family that left the trace.
                    var family = ArchetypeFamily((Archetype)a.archetypeRaw);
                    if (family.StartsWith(copy.CauseArchetype, StringComparison.Ordinal))
                    {
                        copy.Detail = copy.Detail + " (" + a.fullName + ")";
                        break;
                    }
                }
            }
            return copy;
        }
    }
}
