// HR//0D -- Save file schema (docs/02-tech-stack.md) + version for migrations.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
// Writer lives in SaveSystem (Unity side) and must write tmp -> replace (BUG prevention rule 1).
using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Data
{
    [Serializable]
    public class AvatarConfig
    {
        public int body;
        public int hair;
        public int skin;
        public int outfit;
        public string accent = "#18e0b0";
    }

    [Serializable]
    public class PlayerProfile
    {
        public string displayName = "OFFICER";
        public AvatarConfig avatar = new AvatarConfig();
        public bool consentGiven;
        public bool introSeen;
        public bool tutorialDone;
    }

    [Serializable]
    public class DayRecord
    {
        public int day;
        public bool completed;
        public int approved;
        public int rejected;
        public int wrongfulRejections;
        public int syntheticsHired;
        public int trustAtEnd;
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int saveVersion = CurrentVersion;
        public string saveId = Guid.NewGuid().ToString("N");
        public int runSeed;
        public PlayerProfile profile = new PlayerProfile();

        public int day = 1;
        public int applicantIndex;                 // resume exactly where the player was
        public int trust = 100;
        public int corruptionLevel;                // 0..3 (ADR-003)
        public int wrongfulRejections;
        public int syntheticsHired;
        public int syntheticsCaught;
        public int dayApproved;
        public int dayRejected;

        public List<string> approvedIds = new List<string>();
        public List<string> rejectedIds = new List<string>();
        public List<string> pendingApplicantIds = new List<string>();
        public List<DayRecord> days = new List<DayRecord>();
        public List<string> manualSeen = new List<string>();
        public List<string> unlockedSurfaces = new List<string>();
        public int runOutcomeRaw;                  // (int)RunOutcome
        public string savedAtUtc = "";

        /// <summary>Applies forward migrations. Returns false if the save is from a newer build.</summary>
        public bool Migrate()
        {
            if (saveVersion > CurrentVersion) return false;
            if (saveVersion < 1) saveVersion = 1;
            return true;
        }

        public RunOutcome Outcome
        {
            get { return (RunOutcome)runOutcomeRaw; }
            set { runOutcomeRaw = (int)value; }
        }
    }
}
