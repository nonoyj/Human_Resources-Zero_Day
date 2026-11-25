// HR//0D -- Analytics event models (docs/10-analytics-spec.md).
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
// HARD RULES: no PII, no free text, no character names. The backend rejects PII-looking keys.
using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Analytics
{
    public static class AnalyticsEventTypes
    {
        public const string AppOpen = "app_open";
        public const string Consent = "consent";
        public const string ScreenEnter = "screen_enter";
        public const string ScreenExit = "screen_exit";
        public const string IntroSkip = "intro_skip";
        public const string SettingsChange = "settings_change";
        public const string AppQuit = "app_quit";
        public const string Decision = "decision";
        public const string EmailAction = "email_action";
        public const string Hire = "hire";
        public const string Fire = "fire";
        public const string DayEnd = "day_end";
        public const string ManualOpen = "manual_open";
        public const string RunEnd = "run_end";
        public const string QuizResult = "quiz_result";

        public static readonly HashSet<string> All = new HashSet<string>
        {
            AppOpen, Consent, ScreenEnter, ScreenExit, IntroSkip, SettingsChange, AppQuit,
            Decision, EmailAction, Hire, Fire, DayEnd, ManualOpen, RunEnd, QuizResult
        };
    }

    /// <summary>One row per hiring decision -- the primary research instrument.</summary>
    [Serializable]
    public class DecisionEvent
    {
        public string type = AnalyticsEventTypes.Decision;
        public string t;                 // ISO-8601 UTC, set at dispatch
        public int day;
        public string applicantId;
        public string archetype;         // ground-truth archetype (never shown in game)
        public string groundTruth;       // human | human_bait | synthetic
        public string decision;          // approve | reject | fire
        public string reason;            // EvidenceCategory name or null
        public int? confidence;          // 1..3 self-rating, nullable
        public List<string> tellsPresent = new List<string>();
        public List<string> tellsInspected = new List<string>();
        public List<string> surfacesOpened = new List<string>();
        public List<ToolUse> toolsUsed = new List<ToolUse>();
        public List<string> distractorsPresent = new List<string>();
        public bool distractorResolved;
        public int secondsOnCard;
        public int stampsUndone;
        public bool hintShown;
        public bool correct;             // client estimate; server recomputes and compares
    }

    [Serializable]
    public class ToolUse
    {
        public string id;
        public string result;            // VALID | INVALID | UNREACHABLE | CLEAN | SUSPECT
        public int msAfter;
    }

    [Serializable]
    public class DayEndEvent
    {
        public string type = AnalyticsEventTypes.DayEnd;
        public string t;
        public int day;
        public bool quotaMet;
        public int approved;
        public int rejected;
        public int wrongfulRejections;
        public int syntheticsHired;
        public int trust;
        public int corruptionLevel;
        public int shiftMs;
    }

    [Serializable]
    public class RunEndEvent
    {
        public string type = AnalyticsEventTypes.RunEnd;
        public string t;
        public string outcome;
        public int daysCompleted;
        public int syntheticsHired;
        public int syntheticsCaught;
        public int wrongfulRejections;
        public int finalTrust;
        public string ending;
        public int seed;
    }

    [Serializable]
    public class QuizResultEvent
    {
        public string type = AnalyticsEventTypes.QuizResult;
        public string t;
        public int item;
        public bool correct;
        public int msShown;
    }

    /// <summary>Envelope actually written to the local outbox as one JSON line.</summary>
    [Serializable]
    public class AnalyticsEnvelope
    {
        public string eventType;         // mirrors payload.type so the backend can route without parsing
        public string json;              // serialised payload
    }
}
