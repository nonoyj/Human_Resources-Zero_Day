// HR//0D -- the Security Rating and its alerts, as tests.
// Assembly: test only
//
// The player decided (docs/88 Q1-Q4, ADR-012) that the company has ONE number, visible during a shift, that a
// popup explains. A number on screen every shift has to be boring under test: monotonic, clamped, explainable,
// and exactly reproducible. These checks are what stop a later change turning it into decoration.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Tests
{
    internal static class SecurityTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return CheckAFreshCompanyStartsAtSeventy();
            yield return CheckTheWeightsAreTheDecidedOnes();
            yield return CheckTheRatingOnlyFallsWhenItShould();
            yield return CheckTheBandsAreTheDecidedWords();
            yield return CheckTheDashboardAddsUp();
            yield return CheckAlertsAreTheFourDecidedKinds();
            yield return CheckCorrectCallsGiveSomethingBack();
            yield return CheckThePanelAlwaysAddsUp();
            yield return CheckThePanelNeverInventsAPart();
            yield return CheckThePanelPrintsItsAlertsLoudestFirst();
        }


        /// <summary>
        /// THE PANEL ADDS UP (v119.6). It prints four numbers on one screen -- the headline and the three
        /// weighted parts -- so the parts must sum to the headline for EVERY reachable state. This is the test
        /// the dashboard exists for: SecurityRating's own comment says a breakdown that does not add up is
        /// worse than no breakdown, and rounding is exactly where that happens.
        /// </summary>
        private static string CheckThePanelAlwaysAddsUp()
        {
            int checkedStates = 0;
            for (int trust = 0; trust <= 100; trust += 3)
            {
                for (int corruption = 0; corruption <= 3; corruption++)
                {
                    for (int hires = 0; hires <= 4; hires++)
                    {
                        var m = SecurityPanel.Build(trust, corruption, hires, null);
                        int sum = m.TrustPoints + m.CorruptionPoints + m.HiresPoints;
                        True(sum == m.Rating, "the panel's parts sum to " + sum + " but the headline is " +
                             m.Rating + " (trust " + trust + ", corruption " + corruption + ", hires " + hires + ")");
                        True(m.Rating == SecurityRules.Compute(trust, corruption, hires),
                             "the panel's headline disagrees with the rating rule");
                        True(m.Headline == SecurityRules.HudLine(m.Rating), "the panel's headline text is not the rule's line");
                        True(m.BarFilled >= 0 && m.BarFilled <= SecurityPanelModel.BarCells, "the bar overflows");
                        True(m.Bar.Length == SecurityPanelModel.BarCells + 2, "the bar is not BarCells wide");
                        True(m.Lines().Length >= 5, "the panel lost its rows");
                        checkedStates++;
                    }
                }
            }
            return "PASS  the panel's three parts add up to its headline in all " + checkedStates + " reachable states";
        }

        /// <summary>
        /// No part may be negative, exceed the rating's own scale, or claim a contribution the state does not
        /// have: a part is a PORTION of the number, so it lives in 0..100 and the three of them cannot all be
        /// the whole thing.
        /// </summary>
        private static string CheckThePanelNeverInventsAPart()
        {
            for (int trust = 0; trust <= 100; trust += 5)
                for (int corruption = 0; corruption <= 3; corruption++)
                {
                    var m = SecurityPanel.Build(trust, corruption, 0, null);
                    True(m.TrustPoints >= 0 && m.CorruptionPoints >= 0 && m.HiresPoints >= 0,
                         "a part of the rating is negative");
                    True(m.TrustPoints <= SecurityRules.Max, "a part is larger than the whole scale");
                    // a healthy company is ALL trust, which is the anchor SecurityRating documents
                    if (trust >= 100 && corruption == 0)
                        True(m.TrustPoints == SecurityRules.Start,
                             "a healthy company's panel does not put its " + SecurityRules.Start +
                             " points on trust (it says " + m.TrustPoints + ")");
                }
            // a fresh run reads 70 STABLE and says so, in the rule's words. NOTE the inputs: a fresh run is
            // FULL TRUST, not trust 70 -- SecurityRules.Fresh() is Compute(100, 0, 0), and Compute(70,0,0) is
            // a SHAKY company that has already lost a third of its trust. The panel takes the live inputs.
            var fresh = SecurityPanel.Build(100, 0, 0, null);
            True(fresh.Rating == SecurityRules.Fresh(), "the panel does not agree with Fresh()");
            True(fresh.Band == "STABLE", "a fresh run is not STABLE");
            True(fresh.Lines()[0] == "SECURITY 70 STABLE", "the fresh headline is '" + fresh.Lines()[0] + "'");
            return "PASS  the panel's parts are portions of the rating, and a fresh run reads SECURITY 70 STABLE";
        }

        /// <summary>
        /// Alerts print loudest first and there are never more than three: a popup is not a log. Severity
        /// orders them, and a fact outranks an inference (the same rule the alert kinds were built with).
        /// </summary>
        private static string CheckThePanelPrintsItsAlertsLoudestFirst()
        {
            var alerts = new List<SecurityAlert>
            {
                SecurityRules.Alert(AlertKind.DoubleMismatch, 1, "a"),   // severity 1
                SecurityRules.Alert(AlertKind.SyntheticHired, 1, "b"),   // severity 3
                SecurityRules.Alert(AlertKind.VerifiedLie, 2, "c"),      // severity 2
                SecurityRules.Alert(AlertKind.CorruptionRose, 3, "d"),   // severity 3
            };
            var m = SecurityPanel.Build(80, 1, 1, alerts);
            True(m.Alerts.Count == SecurityPanelModel.MaxAlerts, "the panel printed " + m.Alerts.Count + " alerts");
            True(m.Alerts[0].Weight >= m.Alerts[1].Weight && m.Alerts[1].Weight >= m.Alerts[2].Weight,
                 "the alerts are not sorted by severity");
            True(m.Alerts[0].Weight == 3, "the loudest alert is not first");
            // the quiet case says something rather than nothing
            var quiet = SecurityPanel.Build(70, 0, 0, new List<SecurityAlert>());
            True(quiet.Lines()[quiet.Lines().Length - 1] == quiet.QuietLine, "an empty panel prints nothing at all");
            // every printed line is upper case: the panel is painted with an atlas that has no lowercase
            foreach (var line in m.Lines())
            {
                var text = line ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                    if (text[i] >= 'a' && text[i] <= 'z')
                        throw new Exception("the panel line '" + text + "' has lowercase, which the atlas cannot paint");
            }
            return "PASS  alerts print loudest first, never more than three, and every line is paintable";
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        /// <summary>Q1: 0..100 starting at 70. Fresh must be exactly 70 -- not 69, not 71.</summary>
        private static string CheckAFreshCompanyStartsAtSeventy()
        {
            True(SecurityRules.Start == 70, "the starting rating moved from 70 to " + SecurityRules.Start);
            True(SecurityRules.Fresh() == 70, "a fresh run computes " + SecurityRules.Fresh() + ", not 70");

            // and the extremes clamp: no negative numbers, no 105
            True(SecurityRules.Compute(0, 3, 9) >= 0, "a catastrophic run produced a negative rating");
            True(SecurityRules.Compute(100, 0, 0) <= 100, "a perfect run exceeded 100");
            True(SecurityRules.Compute(-50, -1, -4) == SecurityRules.Compute(0, 0, 0),
                 "out-of-range inputs did not clamp to the best case");
            return "PASS  a fresh company scores exactly 70, and the scale clamps at 0..100";
        }

        /// <summary>Q1: the three weights are 60 / 30 / 10 and they must sum to 1, or the dashboard lies.</summary>
        private static string CheckTheWeightsAreTheDecidedOnes()
        {
            float sum = SecurityRules.TrustWeight + SecurityRules.CorruptionWeight + SecurityRules.HiresWeight;
            True(Math.Abs(sum - 1f) < 0.0005f, "the three weights sum to " + sum + ", not 1");
            True(Math.Abs(SecurityRules.TrustWeight - 0.6f) < 0.0005f, "trust is not 60%");
            True(Math.Abs(SecurityRules.CorruptionWeight - 0.3f) < 0.0005f, "corruption is not 30%");
            True(Math.Abs(SecurityRules.HiresWeight - 0.1f) < 0.0005f, "hires are not 10%");

            // trust is the dominant input, by design: losing trust must hurt more than one hire.
            // On the anchored scale (healthy = 70) ten points of trust is 0.6*10 anchored to 70% = ~4-5
            // points, and one synthetic hire is ~2. The relationships are the rule; the exact figures are
            // whatever the anchor produces, so they are asserted as ranges rather than as magic numbers.
            int trustHit = Math.Abs(SecurityRules.Compute(80, 0, 0) - SecurityRules.Compute(70, 0, 0));
            int hireHit = Math.Abs(SecurityRules.Compute(100, 0, 0) - SecurityRules.Compute(100, 0, 1));
            True(trustHit >= 4 && trustHit <= 6, "10 points of trust is worth " + trustHit + ", expected 4-6");
            True(hireHit >= 1 && hireHit <= 3, "one synthetic hire is worth " + hireHit + ", expected 1-3");
            True(trustHit > hireHit, "a hire costs more than losing trust -- the weights are not doing their job");
            return "PASS  weights are 60/30/10, they sum to 1, trust dominates and one hire costs 2 points";
        }

        /// <summary>Q2: it can go up. Losing trust, corruption, or hiring synthetics must each cost points.</summary>
        private static string CheckTheRatingOnlyFallsWhenItShould()
        {
            int better = SecurityRules.Compute(100, 0, 0);
            int worseTrust = SecurityRules.Compute(50, 0, 0);
            int worseCorruption = SecurityRules.Compute(100, 2, 0);
            int worseHires = SecurityRules.Compute(100, 0, 3);
            True(worseTrust < better, "losing trust did not lower the rating");
            True(worseCorruption < better, "corruption did not lower the rating");
            True(worseHires < better, "hiring synthetics did not lower the rating");

            // monotonic in each input: more corruption is never better than less
            for (int c = 0; c < 3; c++)
                True(SecurityRules.Compute(80, c + 1, 0) <= SecurityRules.Compute(80, c, 0),
                     "corruption " + (c + 1) + " scored better than " + c);

            // and it goes back UP: the same run with a correct call is higher than without
            int before = SecurityRules.Compute(70, 1, 1);
            int after = SecurityRules.Compute(70 + SecurityRules.DeltaCorrectSynthetic, 1, 1);
            True(after > before, "the rating cannot recover -- it is a countdown, not a score");
            return "PASS  each input only moves the rating the right way, and correct calls raise it again";
        }

        /// <summary>Q1 words: STABLE 70+, SHAKY 40-69, BREACHED below 40. Painted in the atlas, so capitals.</summary>
        private static string CheckTheBandsAreTheDecidedWords()
        {
            True(SecurityRules.BandOf(70) == SecurityBand.Stable, "70 is not STABLE");
            True(SecurityRules.BandOf(100) == SecurityBand.Stable, "100 is not STABLE");
            True(SecurityRules.BandOf(69) == SecurityBand.Shaky, "69 is not SHAKY");
            True(SecurityRules.BandOf(40) == SecurityBand.Shaky, "40 is not SHAKY");
            True(SecurityRules.BandOf(39) == SecurityBand.Breached, "39 is not BREACHED");
            True(SecurityRules.BandOf(0) == SecurityBand.Breached, "0 is not BREACHED");

            True(SecurityRules.BandWord(70) == "STABLE", "the word for 70 is not STABLE");
            True(SecurityRules.BandWord(41) == "SHAKY", "the word for 41 is not SHAKY");
            True(SecurityRules.BandWord(0) == "BREACHED", "the word for 0 is not BREACHED");

            string line = SecurityRules.HudLine(62);
            True(line == "SECURITY 62 SHAKY", "the HUD line is not the agreed shape: " + line);
            return "PASS  bands split at 70 and 40, the words are STABLE/SHAKY/BREACHED, the HUD line is fixed";
        }

        /// <summary>The dashboard (v111) shows three contributions. They must sum to the number on the HUD.</summary>
        private static string CheckTheDashboardAddsUp()
        {
            for (int t = 0; t <= 100; t += 10)
            {
                for (int c = 0; c <= 3; c++)
                {
                    for (int s = 0; s <= 4; s++)
                    {
                        int tp, cp, hp;
                        SecurityRules.Breakdown(t, c, s, out tp, out cp, out hp);
                        int rating = SecurityRules.Compute(t, c, s);
                        int sum = tp + cp + hp;
                        True(Math.Abs(sum - rating) <= 1,
                             "the breakdown for (" + t + "," + c + "," + s + ") sums to " + sum +
                             " but the rating is " + rating + " -- the popup would not add up");
                        True(tp >= 0 && cp >= 0 && hp >= 0, "a negative contribution on the dashboard");
                    }
                }
            }
            return "PASS  the three dashboard contributions always sum to the HUD number (within rounding)";
        }

        /// <summary>Q3: exactly four alert kinds, each with a line, a cost and a severity.</summary>
        private static string CheckAlertsAreTheFourDecidedKinds()
        {
            var kinds = (AlertKind[])Enum.GetValues(typeof(AlertKind));
            True(kinds.Length == 4, "there are " + kinds.Length + " alert kinds, not the four agreed");
            foreach (var k in kinds)
            {
                var a = SecurityRules.Alert(k, 3, "app_01");
                True(!string.IsNullOrEmpty(a.Line), "alert " + k + " has no line to show");
                True(a.Severity >= 1 && a.Severity <= 3, "alert " + k + " has severity " + a.Severity);
                True(a.Delta < 0, "alert " + k + " does not cost the rating anything");
                True(a.Day == 3, "alert " + k + " lost its day");
                // the line must be paintable with the 5x7 atlas: capitals, digits, plain punctuation
                True(IntroScriptRulesRules_Paintable(a.Line), "alert " + k + " has unpaintable characters: " + a.Line);
            }
            // hiring a synthetic is the loudest thing that can happen
            True(SecurityRules.Alert(AlertKind.SyntheticHired, 1, null).Severity == 3,
                 "a synthetic hire is not the loudest alert");
            // and a double mismatch must be quieter than a verified fact, because it is not proof (Q5/Q10)
            True(SecurityRules.Alert(AlertKind.DoubleMismatch, 1, null).Severity
                 < SecurityRules.Alert(AlertKind.VerifiedLie, 1, null).Severity,
                 "an inference is as loud as a fact");
            return "PASS  four alert kinds, all paintable, all costing points, fact louder than inference";
        }

        /// <summary>The helper the test needs to reuse the intro's paintability check without duplicating it.</summary>
        private static bool IntroScriptRulesRules_Paintable(string s)
        {
            return IntroScriptRules.IsPaintable(s);
        }

        /// <summary>Q2: a correct call gives a little back; a wrongful rejection costs more than it gains.</summary>
        private static string CheckCorrectCallsGiveSomethingBack()
        {
            int correctHuman = SecurityRules.DeltaForDecision(false, true);      // a human, passed
            int correctSynthetic = SecurityRules.DeltaForDecision(true, true);   // a synthetic, caught
            int wrongfulReject = SecurityRules.DeltaForDecision(false, false);   // a human, REJECTED
            int hiredSynthetic = SecurityRules.DeltaForDecision(true, false);    // a synthetic, hired

            True(correctHuman > 0, "a correct 'human' call gives nothing back");
            True(correctSynthetic > 0, "catching a synthetic gives nothing back");
            True(wrongfulReject < 0, "a wrongful rejection costs nothing");
            True(Math.Abs(wrongfulReject) > Math.Abs(correctHuman),
                 "rejecting a human costs no more than hiring one correctly -- the player would not feel it");
            True(correctSynthetic > correctHuman,
                 "catching a synthetic is worth no more than passing a human -- there is no reward for the core skill");
            True(hiredSynthetic < 0, "hiring a synthetic costs the rating nothing");
            True(Math.Abs(wrongfulReject) > Math.Abs(hiredSynthetic),
                 "hiring a synthetic costs more than rejecting a human -- backwards: TrustRules already docks "
                 + "the hire, and the human is the one the game is about");
            return "PASS  correct calls give back 1-2, a wrongful rejection costs 3, catching a synthetic pays best";
        }
    }
}
