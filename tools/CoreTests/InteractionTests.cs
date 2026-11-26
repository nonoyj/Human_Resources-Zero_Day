// HR//0D -- headless tests for the interaction layer (no Unity, no device).
// These are the rules the desk must never break: evidence must be reachable, art-only artifacts
// must never become proof, tools stay imperfect, and the view must not learn the hidden truth.
using System;
using System.Collections.Generic;
using System.Reflection;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;
using HRZeroDay.Game.Interaction;

namespace HRZeroDay.Tests
{
    public static class InteractionTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return Check("every_tell_has_a_reveal", EveryTellHasAReveal);
            yield return Check("presentation_matches_locked_decision", PresentationMatchesLockedDecision);
            yield return Check("honest_humans_never_yield_evidence", HonestHumansNeverYieldEvidence);
            yield return Check("every_synthetic_is_provable_on_its_day", EverySyntheticIsProvableOnItsDay);
            yield return Check("locked_surface_cannot_be_proven", LockedSurfaceCannotBeProven);
            yield return Check("repeat_taps_do_not_double_count", RepeatTapsDoNotDoubleCount);
            yield return Check("hit_test_safe_area_and_overlap", HitTestSafeAreaAndOverlap);
            yield return Check("tool_budget_is_2_2_1", ToolBudgetIs221);
            yield return Check("full_minute_stamp_and_view_dto", FullMinuteStampAndViewDto);
            yield return Check("paperwork_flags_close_and_pay_trust", PaperworkFlagsCloseAndPayTrust);
        }

        private static string Check(string name, Action test)
        {
            try { test(); return "PASS  " + name; }
            catch (Exception e) { return "FAIL  " + name + ": " + e.Message; }
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Near(float a, float b, string message)
        {
            if (Math.Abs(a - b) > 0.01f) throw new Exception(message + " (" + a + " != " + b + ")");
        }

        private static ApplicantRecord Make(GroundTruth truth, params string[] tells)
        {
            var a = new ApplicantRecord();
            a.id = "test";
            a.day = 1;
            a.groundTruthRaw = (int)truth;
            a.archetypeRaw = (int)(truth == GroundTruth.Synthetic
                ? Archetype.SyntheticPolished : Archetype.HumanSolid);
            a.fullName = "Test Person";
            a.tellsPresent.AddRange(tells);
            return a;
        }

        private static void EveryTellHasAReveal()
        {
            foreach (var tell in TellCatalog.All)
            {
                var target = EvidenceMap.ForTell(tell.Id);
                True(target != null, tell.Id + " has no reveal target");
                True(target.Surface == tell.Surface, tell.Id + " reveals on the wrong surface");
                True(string.IsNullOrEmpty(target.Artifact) == false, tell.Id + " has no artifact id");
            }
        }

        private static void PresentationMatchesLockedDecision()
        {
            string[] body = { "seam", "ear", "hands", "straight_badge", "shadow" };
            string[] face = { "mirror", "glasses_merge", "skin_band", "no_blink" };
            foreach (var id in body)
                True(EvidenceMap.ForArtifact(id).Style == RevealStyle.MagnifierRing,
                     id + " must use the magnifier ring");
            foreach (var id in face)
                True(EvidenceMap.ForArtifact(id).Style == RevealStyle.Callout,
                     id + " must use a text callout");
            // Art-only artifacts must not be promoted into catalog evidence.
            True(EvidenceMap.ForArtifact("hands").TellId == null, "hands became a catalog tell");
        }

        private static void HonestHumansNeverYieldEvidence()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var shift = new ShiftController(seed);
                foreach (var day in shift.AllDays.Keys)
                    foreach (var a in shift.AllDays[day])
                    {
                        if (a.Truth != GroundTruth.Human) continue;
                        var ledger = new EvidenceLedger();
                        ledger.Begin(a);
                        foreach (var hp in LayoutArtifacts())
                            True(ledger.InspectArtifact(hp, day).IsEvidence == false,
                                 "seed " + seed + ": honest human proven by " + hp);
                        foreach (RevealSurface surface in Enum.GetValues(typeof(RevealSurface)))
                            True(ledger.OpenSurface(surface, day).Count == 0,
                                 "seed " + seed + ": honest human proven by " + surface);
                        True(ledger.FoundCount == 0, "seed " + seed + ": human notebook is not empty");
                    }
            }
        }

        private static void EverySyntheticIsProvableOnItsDay()
        {
            int checkedCount = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var shift = new ShiftController(seed);
                foreach (var day in shift.AllDays.Keys)
                    foreach (var a in shift.AllDays[day])
                    {
                        if (a.IsSynthetic == false) continue;
                        checkedCount++;
                        var ledger = new EvidenceLedger();
                        ledger.Begin(a);
                        foreach (var tellId in a.tellsPresent)
                        {
                            var tell = TellCatalog.Get(tellId);
                            if (tell.Surface == RevealSurface.ApplicantStage)
                                ledger.InspectArtifact(EvidenceMap.ForTell(tellId).Artifact, day, 9f);
                            else ledger.OpenSurface(tell.Surface, day);
                        }
                        True(ledger.FoundCount > 0,
                             "seed " + seed + " day " + day + ": synthetic [" + a.id + "] unprovable");
                    }
            }
            True(checkedCount > 0, "no synthetics were generated");
        }

        private static void LockedSurfaceCannotBeProven()
        {
            var ledger = new EvidenceLedger();
            ledger.Begin(Make(GroundTruth.Synthetic, TellCatalog.ExpVsGrad));
            var results = ledger.OpenSurface(RevealSurface.ResumeRow, 1);   // locked on day 1
            True(results.Count == 0, "day 1 opened the locked resume surface");
            True(ledger.FoundCount == 0, "day 1 proved a resume tell");
            True(ledger.OpenSurface(RevealSurface.ResumeRow, 2).Count == 1, "day 2 resume is locked");
        }

        private static void RepeatTapsDoNotDoubleCount()
        {
            var ledger = new EvidenceLedger();
            ledger.Begin(Make(GroundTruth.Synthetic, TellCatalog.PortraitBlendSeam));
            var first = ledger.InspectArtifact("seam", 1);
            var second = ledger.InspectArtifact("seam", 1);
            True(first.IsEvidence && first.Changed, "first seam tap did not prove the tell");
            True(second.IsEvidence && second.Changed == false, "repeat tap changed the ledger");
            True(ledger.FoundCount == 1, "seam counted twice");
            var input = ledger.BuildDecision(Decision.Reject, EvidenceCategory.SyntheticMedia, 12);
            True(input.TellsInspected.Count == 1, "decision input has duplicate evidence");
            True(input.SurfacesOpened.Contains(RevealSurface.ApplicantStage), "stage surface not recorded");
        }

        private static void HitTestSafeAreaAndOverlap()
        {
            var p = HotspotHitTest.ScreenToCanvas(new PixelPoint(240f, 135f),
                new PixelRect(20f, 10f, 460f, 250f), 480, 270);
            Near(p.X, 480f * 220f / 460f, "safe area x");
            Near(p.Y, 135f, "safe area y");

            var local = new PixelRect(76f, 156f, 40f, 28f);
            var canvasRect = HotspotHitTest.LocalToCanvas(local, 280f, 18f);
            Near(canvasRect.X, 356f, "applicant local x");
            Near(canvasRect.Y, 174f, "applicant local y");

            var spots = new List<HotspotGeometry>
            {
                new HotspotGeometry("mirror", canvasRect),
                new HotspotGeometry("no_blink", canvasRect)
            };
            var centre = new PixelPoint(canvasRect.X + 20f, canvasRect.Y + 14f);
            True(HotspotHitTest.Resolve(spots, centre, 0f, 0f) == "mirror", "mirror not first");
            True(HotspotHitTest.Resolve(spots, centre, 0f, 9f) == "no_blink", "timed tell never wins");
            True(HotspotHitTest.Resolve(spots, new PixelPoint(10f, 10f), 0f, 0f) == null,
                 "empty space hit a hotspot");
        }

        private static void ToolBudgetIs221()
        {
            var budget = new ToolBudget();
            True(budget.TryUse(ToolKind.IdRegistry, 1), "ID use 1 refused");
            True(budget.TryUse(ToolKind.IdRegistry, 1), "ID use 2 refused");
            True(budget.TryUse(ToolKind.IdRegistry, 1) == false, "ID use 3 allowed");
            True(budget.TryUse(ToolKind.MetadataScan, 1), "metadata use 1 refused");
            True(budget.TryUse(ToolKind.MetadataScan, 1), "metadata use 2 refused");
            True(budget.TryUse(ToolKind.MetadataScan, 1) == false, "metadata use 3 allowed");
            True(budget.TryUse(ToolKind.ReferenceCall, 1), "reference use 1 refused");
            True(budget.TryUse(ToolKind.ReferenceCall, 1) == false, "reference use 2 allowed");
            budget.AdvanceToDay(2);
            True(budget.Remaining(ToolKind.IdRegistry) == 2, "budget did not reset on day 2");
        }

        private static void FullMinuteStampAndViewDto()
        {
            int foundSeed = -1;
            for (int seed = 1; seed <= 100; seed++)
            {
                var probe = new ShiftController(seed);
                var a = probe.Current;
                if (a.IsSynthetic &&
                    (a.tellsPresent.Contains(TellCatalog.PortraitBlendSeam) ||
                     a.tellsPresent.Contains(TellCatalog.PortraitAsymmetry)))
                { foundSeed = seed; break; }
            }
            True(foundSeed > 0, "no seed with a stage tell in the first 100 seeds");

            var session = new DeskSession(foundSeed);
            var view = session.Current;
            var fields = typeof(ApplicantViewData).GetMembers(BindingFlags.Public | BindingFlags.Instance);
            foreach (var member in fields)
            {
                string lower = member.Name.ToLowerInvariant();
                True(lower.Contains("truth") == false && lower.Contains("tell") == false,
                     "view DTO leaks " + member.Name);
            }
            True(view.FullName.Length > 0 && view.SpriteKey.Length > 0, "view DTO is empty");

            session.Inspect("seam");
            session.Inspect("mirror");
            True(session.Ledger.FoundCount >= 1, "inspecting the stage proved nothing");
            var result = session.Stamp(Decision.Reject, EvidenceCategory.SyntheticMedia, 15);
            True(result != null && result.IsCorrect, "correct rejection was not scored as correct");
            True(session.BeginCurrent(), "next applicant did not arrive");
            True(session.Ledger.FoundCount == 0, "notebook was carried to the next applicant");
        }

        private static void PaperworkFlagsCloseAndPayTrust()
        {
            int foundSeed = -1;
            for (int seed = 1; seed <= 100; seed++)
            {
                var probe = new ShiftController(seed);
                if (probe.Current.IsSynthetic) { foundSeed = seed; break; }
            }
            True(foundSeed > 0, "no synthetic first applicant found");

            var flagged = new DeskSession(foundSeed);
            var clean = new DeskSession(foundSeed);
            True(flagged.Paperwork != null && flagged.Paperwork.flags.Count == 5,
                 "generated letter does not offer the five shipping flags");
            True(flagged.Paperwork.CanOpen, "letter is closed before it was read");
            string flag = flagged.Paperwork.flags[0].id;
            True(flagged.TogglePaperFlag(flag), "flag could not be toggled");
            True(flagged.Paperwork.flagged.Contains(flag), "toggle did not reach the paperwork");
            int delta = flagged.PaperTrustDelta;

            var a = flagged.Stamp(Decision.Reject, EvidenceCategory.SyntheticMedia, 10);
            var b = clean.Stamp(Decision.Reject, EvidenceCategory.SyntheticMedia, 10);
            True(flagged.Paperwork.readClosed, "letter stayed open after the stamp");
            True(flagged.TogglePaperFlag(flag) == false, "flag changed after the stamp");
            True(a.TrustDelta == b.TrustDelta + delta,
                 "paperwork trust delta was not folded into the decision result");
        }

        private static string[] LayoutArtifacts()
        {
            return new[]
            {
                "seam", "mirror", "ear", "glasses_merge", "skin_band", "hands",
                "straight_badge", "shadow", "no_blink"
            };
        }
    }
}
