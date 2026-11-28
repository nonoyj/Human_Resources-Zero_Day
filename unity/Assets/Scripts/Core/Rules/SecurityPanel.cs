// HR//0D -- SecurityPanel: the ALERTS popup, as a MODEL. Pure data, no UnityEngine.
// Assembly: HRZeroDay.Core
//
// WHY A MODEL AND NOT A VIEW (v119.6, docs/87 v110 item A4)
//   The player asked for "a dashboard showing how the rating is made". A dashboard that computes its own
//   numbers is a second copy of the rules, and the second copy is always the one that is wrong. So this file
//   owns EVERY cell the panel prints -- the headline, the three weighted parts, the meeting point where they
//   add up, and the alert rows -- and the HUD only draws what it is handed. Two consequences that matter:
//
//     * the three parts can never fail to add up, because EnsureAddsUp() repairs the rounding BEFORE anyone
//       sees the panel, and a unit test asserts the sum for every reachable state;
//     * the panel is testable headlessly, in a project that has no Unity in it.
//
// WHAT IT SHOWS, from top to bottom:
//
//     SECURITY 62 SHAKY
//     [###############.......]         a bar, filled to the rating
//     TRUST        44   (44)           the parts, in points, anchored the same way the headline is
//     CORRUPT      12   (12)
//     HIRES         6    (6)
//     --------------------------------
//     A SYNTHETIC WAS HIRED.            one row per alert, loudest first, with its own delta
//     TWO MISMATCHES ON ONE FILE.
//
// READING IT IN THE GAME'S VOICE: the panel never says "score", "points" or "penalty". The three parts are
// what the company IS (trust, corruption, hires); the alerts are what HAPPENED. A number that only falls
// stops being a score and becomes a countdown, so deltas print with their sign.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Core.Rules
{
    /// <summary>One row of the panel: a label, a value, and how loud it is.</summary>
    public sealed class PanelRow
    {
        public string Label;
        public string Value;
        /// <summary>1 = a fact of the state, 2 = something that moved it, 3 = it happened to you.</summary>
        public int Weight;
    }

    /// <summary>Everything the ALERTS popup prints. Built in one call, so it cannot disagree with itself.</summary>
    public sealed class SecurityPanelModel
    {
        public int Rating;
        public string Band = "STABLE";
        public string Headline = "SECURITY 70 STABLE";

        /// <summary>The three parts, in POINTS of the final rating. They sum to Rating (see EnsureAddsUp).</summary>
        public int TrustPoints, CorruptionPoints, HiresPoints;

        /// <summary>The raw inputs, so the panel can say WHERE a part came from without recomputing anything.</summary>
        public int Trust, CorruptionLevel, SyntheticsHired;

        /// <summary>The bar, 0..BarCells. Filled cells are printed as a solid block, the rest as dots.</summary>
        public const int BarCells = 24;
        public int BarFilled;
        public string Bar = "";

        /// <summary>What happened, loudest first. Never more than MaxAlerts rows -- a popup is not a log.</summary>
        public List<PanelRow> Alerts = new List<PanelRow>();
        public const int MaxAlerts = 3;

        /// <summary>The one line the panel shows when nothing has happened yet.</summary>
        public string QuietLine = "NOTHING HAS MOVED YET.";

        public string[] Lines()
        {
            var lines = new List<string>();
            lines.Add(Headline);
            lines.Add(Bar);
            lines.Add("TRUST      " + Pad(TrustPoints) + "  (" + Trust + ")");
            lines.Add("CORRUPT    " + Pad(CorruptionPoints) + "  (" + CorruptionLevel + "/3)");
            lines.Add("HIRES      " + Pad(HiresPoints) + "  (" + SyntheticsHired + ")");
            lines.Add("------------------------------");
            if (Alerts.Count == 0)
            {
                lines.Add(QuietLine);
            }
            else
            {
                for (int i = 0; i < Alerts.Count; i++)
                    lines.Add(Alerts[i].Value.Length == 0 ? Alerts[i].Label
                                                          : Alerts[i].Label + "  " + Alerts[i].Value);
            }
            return lines.ToArray();
        }

        private static string Pad(int value)
        {
            if (value < 0) return value.ToString();
            if (value < 10) return "  " + value;
            if (value < 100) return " " + value;
            return value.ToString();
        }
    }

    public static class SecurityPanel
    {
        /// <summary>
        /// Builds the panel. `alerts` is the day's alert list, newest last, as the game recorded them; they are
        /// printed loudest first (severity, then recency, then the bigger hit) and cut to MaxAlerts.
        /// </summary>
        public static SecurityPanelModel Build(int trust, int corruptionLevel, int syntheticsHired,
                                               IList<SecurityAlert> alerts)
        {
            var model = new SecurityPanelModel();
            model.Trust = trust;
            model.CorruptionLevel = corruptionLevel < 0 ? 0 : (corruptionLevel > 3 ? 3 : corruptionLevel);
            model.SyntheticsHired = syntheticsHired < 0 ? 0 : syntheticsHired;

            model.Rating = SecurityRules.Compute(model.Trust, model.CorruptionLevel, model.SyntheticsHired);
            model.Band = SecurityRules.BandWord(model.Rating);
            model.Headline = SecurityRules.HudLine(model.Rating);

            int t, c, h;
            SecurityRules.Breakdown(model.Trust, model.CorruptionLevel, model.SyntheticsHired, out t, out c, out h);
            model.TrustPoints = t;
            model.CorruptionPoints = c;
            model.HiresPoints = h;
            EnsureAddsUp(model);

            model.BarFilled = (int)Math.Round(model.Rating / 100.0 * SecurityPanelModel.BarCells);
            if (model.BarFilled < 0) model.BarFilled = 0;
            if (model.BarFilled > SecurityPanelModel.BarCells) model.BarFilled = SecurityPanelModel.BarCells;
            model.Bar = Bar(model.BarFilled);

            if (alerts != null && alerts.Count > 0)
            {
                var sorted = new List<SecurityAlert>(alerts);
                // stable-ish: severity desc, then the bigger absolute hit, then later day first. No LINQ:
                // this assembly is deliberately old-C# and the sort has to be identical everywhere.
                sorted.Sort(delegate (SecurityAlert a, SecurityAlert b)
                {
                    if (a == null) return 1;
                    if (b == null) return -1;
                    if (a.Severity != b.Severity) return b.Severity.CompareTo(a.Severity);
                    int abs = Math.Abs(b.Delta).CompareTo(Math.Abs(a.Delta));
                    if (abs != 0) return abs;
                    return b.Day.CompareTo(a.Day);
                });
                for (int i = 0; i < sorted.Count && i < SecurityPanelModel.MaxAlerts; i++)
                {
                    var a = sorted[i];
                    if (a == null) continue;
                    model.Alerts.Add(new PanelRow
                    {
                        Label = a.Line,
                        Value = a.Delta == 0 ? "" : (a.Delta > 0 ? "+" + a.Delta : a.Delta.ToString()),
                        Weight = a.Severity
                    });
                }
            }
            return model;
        }

        /// <summary>
        /// THE MEETING POINT. The three rounded parts must equal the headline, because the panel prints all
        /// four numbers on one screen and a breakdown that does not add up is worse than no breakdown
        /// (SecurityRating's own words). Rounding is repaired by giving the remainder to the biggest part, which
        /// is trust by construction -- so the repair is invisible and always lands where the eye expects it.
        /// </summary>
        private static void EnsureAddsUp(SecurityPanelModel m)
        {
            int sum = m.TrustPoints + m.CorruptionPoints + m.HiresPoints;
            int diff = m.Rating - sum;
            if (diff == 0) return;
            // give it to whichever part is largest, so the visible error looks like the shape of the sum
            int biggest = 0;
            if (m.CorruptionPoints > m.TrustPoints) biggest = 1;
            if (biggest == 0 && m.HiresPoints > m.TrustPoints) biggest = 2;
            if (biggest == 1 && m.HiresPoints > m.CorruptionPoints) biggest = 2;
            if (biggest == 0) m.TrustPoints += diff;
            else if (biggest == 1) m.CorruptionPoints += diff;
            else m.HiresPoints += diff;
        }

        /// <summary>The bar: '#' to the rating, '.' for the rest. ASCII so it can be printed anywhere.</summary>
        public static string Bar(int filled)
        {
            if (filled < 0) filled = 0;
            if (filled > SecurityPanelModel.BarCells) filled = SecurityPanelModel.BarCells;
            var chars = new char[SecurityPanelModel.BarCells];
            for (int i = 0; i < chars.Length; i++) chars[i] = i < filled ? '#' : '.';
            return "[" + new string(chars) + "]";
        }

        /// <summary>
        /// The alerts a day's outcome raises, in the order they happened. One place, so the HUD, the summary
        /// and the panel can never disagree about what the day did to the company.
        /// </summary>
        public static List<SecurityAlert> AlertsForDay(DayResult day, int corruptionBefore, int corruptionAfter)
        {
            var list = new List<SecurityAlert>();
            if (day == null) return list;
            for (int i = 0; i < day.SyntheticsHired; i++)
                list.Add(SecurityRules.Alert(AlertKind.SyntheticHired, day.Day, null));
            if (corruptionAfter > corruptionBefore)
                list.Add(SecurityRules.Alert(AlertKind.CorruptionRose, day.Day, null));
            if (day.WrongfulRejections >= 2)
                list.Add(SecurityRules.Alert(AlertKind.DoubleMismatch, day.Day, null));
            return list;
        }
    }
}
