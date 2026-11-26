// HR//0D -- save/resume tests, headless. The save is the only thing standing between a player and
// losing a shift, so it is tested as a rule: serialise -> deserialise -> the same desk, the same
// person, the same counters. Runs without Unity (System.Text.Json stands in for JsonUtility;
// both are "public fields only" serialisers, and SaveData is deliberately shaped for both).
using System;
using System.Collections.Generic;
using System.Text.Json;
using HRZeroDay.Core.Data;
using HRZeroDay.Game.Interaction;

namespace HRZeroDay.Tests
{
    internal static class SaveResumeTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return Check("save_round_trip_keeps_the_run", SaveRoundTripKeepsTheRun);
            yield return Check("save_resume_lands_on_the_same_applicant", SaveResumeLandsOnSameApplicant);
            yield return Check("save_mid_day_keeps_day_counters", SaveMidDayKeepsDayCounters);
            yield return Check("save_refuses_newer_version", SaveRefusesNewerVersion);
            yield return Check("save_resume_keeps_tool_budget_day", SaveResumeKeepsToolBudgetDay);
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

        // Stands in for SaveSystem.Write/Read (which need Application.persistentDataPath).
        private static SaveData RoundTrip(SaveData data)
        {
            var json = JsonSerializer.Serialize(data);
            return JsonSerializer.Deserialize<SaveData>(json);
        }

        private static SaveData FreshSave(int seed)
        {
            var save = new SaveData();
            save.runSeed = seed;
            save.saveId = "test-save";
            return save;
        }

        private static DeskSession Play(DeskSession session, int stamps)
        {
            for (int i = 0; i < stamps && !session.DayFinished && !session.Finished; i++)
            {
                session.Stamp(Decision.Reject, EvidenceCategory.Insufficient, 10);
                if (!session.Finished) session.BeginCurrent();
            }
            return session;
        }

        private static void SaveRoundTripKeepsTheRun()
        {
            var session = Play(new DeskSession(4242), 2);
            var save = FreshSave(4242);
            session.WriteTo(save);

            var back = RoundTrip(save);
            True(back != null, "save did not round-trip");
            True(back.runSeed == 4242, "seed changed: " + back.runSeed);
            True(back.day == session.Day, "day changed: " + back.day + " != " + session.Day);
            True(back.applicantIndex == session.Shift.CurrentIndex,
                 "applicant index changed: " + back.applicantIndex);
            True(back.trust == session.Trust, "trust changed: " + back.trust + " != " + session.Trust);
        }

        private static void SaveResumeLandsOnSameApplicant()
        {
            var original = Play(new DeskSession(99), 2);
            var expected = original.Current.FullName + "/" + original.Current.IdNumber;

            var save = FreshSave(99);
            original.WriteTo(save);
            var resumed = new DeskSession(RoundTrip(save));

            True(resumed.Current.FullName + "/" + resumed.Current.IdNumber == expected,
                 "resumed on a different applicant: " + resumed.Current.FullName);
            True(resumed.Day == original.Day, "resumed on day " + resumed.Day);
        }

        private static void SaveMidDayKeepsDayCounters()
        {
            var session = new DeskSession(1234);
            int approvedBefore = session.Shift.DayApproved;
            session.Stamp(Decision.Approve, null, 8);
            var save = FreshSave(1234);
            session.WriteTo(save);

            var resumed = new DeskSession(RoundTrip(save));
            // A resumed day must report the same summary it would have reported un-interrupted.
            True(resumed.Shift.DayApproved == approvedBefore + 1,
                 "day approvals lost on resume: " + resumed.Shift.DayApproved);
            if (!resumed.Shift.DayFinished)
            {
                resumed.BeginCurrent();
                var day = resumed.CloseDay();
                if (day != null)
                    True(day.Approved >= approvedBefore + 1,
                         "closing the resumed day lost approvals: " + day.Approved);
            }
        }

        private static void SaveRefusesNewerVersion()
        {
            var save = FreshSave(7);
            save.saveVersion = SaveData.CurrentVersion + 1;
            True(save.Migrate() == false, "a save from a newer build was accepted");
        }

        private static void SaveResumeKeepsToolBudgetDay()
        {
            var session = new DeskSession(31337);
            var save = FreshSave(31337);
            session.WriteTo(save);
            var resumed = new DeskSession(RoundTrip(save));
            True(resumed.Tools != null, "tool budget missing after resume");
            // The budget is scoped to the resumed day: same remaining count as the live session.
            True(resumed.Tools.Remaining(ToolKind.IdRegistry) ==
                 session.Tools.Remaining(ToolKind.IdRegistry),
                 "tool budget not carried across resume");
        }
    }
}
