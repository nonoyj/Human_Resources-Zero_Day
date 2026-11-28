// HR//0D -- MenuRules: the front door, as data and arithmetic. No UnityEngine.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
//
// WHY A MENU AT ALL, given the game already booted straight to the desk: because two things in the save file
// have never had a home -- `profile.consentGiven` (the research-ethics commitment, currently a field nothing
// sets) and the audio settings added in v105. A front door is the cheapest place to put both, and it is the
// first screen a player sees, which makes it the right place to say what the game does with their data.
//
// THE RULES THIS FILE ENCODES
//   1. CONTINUE only exists when there is a save. A dead entry on a first run reads as a bug.
//   2. Nothing in the menu may be unreadable: every label is upper case and <= 24 characters, because it is
//      painted with the game's 5x7 atlas (which has no lowercase).
//   3. Declining telemetry NEVER stops the game. Consent is about what leaves the device, not about access.
//      That is the difference between consent and a paywall, and the tests enforce it.
using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Rules
{
    /// <summary>What a menu row does when it is chosen.</summary>
    public enum MenuAction
    {
        None,
        Continue,        // resume the save
        NewRun,          // start again (re-shows the intro: a new run is a new first minute)
        OpenSettings,    // audio
        OpenConsent,     // data & consent
        OpenCredits,
        Back,
        QuitApp
    }

    /// <summary>Which screen the menu is showing.</summary>
    public enum MenuScreen
    {
        Root,
        Settings,
        Consent,
        Credits
    }

    public sealed class MenuItem
    {
        public MenuAction Action;
        public string Label;
        /// <summary>False for a row that exists but cannot be chosen (greyed, not hidden).</summary>
        public bool Enabled;

        public MenuItem(MenuAction action, string label, bool enabled)
        {
            Action = action;
            Label = label;
            Enabled = enabled;
        }
    }

    public static class MenuRules
    {
        /// <summary>The longest label the atlas can paint in the menu column without wrapping (24 chars).</summary>
        public const int MaxLabelChars = 24;

        /// <summary>
        /// The root screen. `hasSave` decides whether CONTINUE is offered at all -- not greyed: absent.
        /// A first-time player should see three choices, not four with one dead.
        /// </summary>
        public static List<MenuItem> Root(bool hasSave, bool hasRunInProgress)
        {
            var items = new List<MenuItem>();
            if (hasSave)
                items.Add(new MenuItem(MenuAction.Continue,
                                       hasRunInProgress ? "CONTINUE THE RUN" : "CONTINUE", true));
            items.Add(new MenuItem(MenuAction.NewRun, hasSave ? "NEW RUN" : "START", true));
            items.Add(new MenuItem(MenuAction.OpenSettings, "SETTINGS", true));
            items.Add(new MenuItem(MenuAction.OpenConsent, "DATA & CONSENT", true));
            items.Add(new MenuItem(MenuAction.OpenCredits, "CREDITS", true));
            return items;
        }

        /// <summary>The settings screen: two volumes, a mute, and a way back.</summary>
        public static List<MenuItem> Settings(bool muted)
        {
            return new List<MenuItem>
            {
                new MenuItem(MenuAction.None, "ROOM VOLUME", true),
                new MenuItem(MenuAction.None, "SOUND VOLUME", true),
                new MenuItem(MenuAction.None, muted ? "SOUND: OFF" : "SOUND: ON", true),
                new MenuItem(MenuAction.Back, "BACK", true)
            };
        }

        /// <summary>
        /// The consent screen. TWO rows and no third: "allow" or "do not allow". There is deliberately no
        /// "ask me later", because a deferral is indistinguishable from a no in the data and the player would
        /// be told one thing while the game did another.
        /// </summary>
        public static List<MenuItem> Consent(bool granted, bool decided)
        {
            return new List<MenuItem>
            {
                new MenuItem(MenuAction.None, granted ? "ALLOW: YES" : "ALLOW: NO", true),
                new MenuItem(MenuAction.None, decided ? "CHANGE IT" : "CHOOSE", true),
                new MenuItem(MenuAction.Back, "BACK", true)
            };
        }

        /// <summary>Up/down with wrap-around. A menu that stops at the ends feels broken on a keyboard.</summary>
        public static int Move(int index, int count, int delta)
        {
            if (count <= 0) return 0;
            int next = index + delta;
            while (next < 0) next += count;
            return next % count;
        }

        /// <summary>Skips disabled rows when moving, so a greyed row is never the selection.</summary>
        public static int MoveSkipping(IList<MenuItem> items, int index, int delta)
        {
            if (items == null || items.Count == 0) return 0;
            int next = index;
            for (int i = 0; i < items.Count; i++)
            {
                next = Move(next, items.Count, delta);
                if (items[next].Enabled) return next;
            }
            return index;
        }

        /// <summary>True when a label can be painted: upper case, atlas characters, not too long.</summary>
        public static bool LabelIsPaintable(string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            if (label.Length > MaxLabelChars) return false;
            return IntroScriptRules.IsPaintable(label);
        }

        // ---- audio settings, in the units the player actually changes ----------------------------------

        /// <summary>Volume steps. Ten stops is the most a 5x7 bar can show honestly.</summary>
        public const float VolumeStep = 0.1f;

        public static float StepVolume(float current, int delta)
        {
            if (delta == 0) return Clamp01(current);
            // A slider that is NOT on the grid (0.85, say) must still land on the next honest step: up -> 0.9,
            // down -> 0.8. Snapping first and then adding would jump 0.85 straight to 1.0, which is the kind
            // of "the volume control is a lottery" bug that survives to release.
            double cells = current * 10.0;
            double next = delta > 0 ? Math.Floor(cells + 1e-6) + 1.0 : Math.Ceiling(cells - 1e-6) - 1.0;
            return Clamp01((float)(next / 10.0));
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        /// <summary>"80%" -- what the settings row shows next to the bar.</summary>
        public static string Percent(float volume01)
        {
            float v = volume01 < 0f ? 0f : (volume01 > 1f ? 1f : volume01);
            return ((int)Math.Round(v * 100f)) + "%";
        }

        /// <summary>A ten-cell bar, filled to the volume. Painted with two atlas characters, not a sprite.</summary>
        public static string Bar(float volume01)
        {
            float v = volume01 < 0f ? 0f : (volume01 > 1f ? 1f : volume01);
            int filled = (int)Math.Round(v * 10f);
            string s = "";
            for (int i = 0; i < 10; i++) s += i < filled ? "#" : "-";
            return s;
        }
    }

    /// <summary>
    /// The research-ethics half. The game records anonymous run telemetry; this is where the player decides
    /// whether it may leave the device, and the answer must stick across sessions.
    /// </summary>
    public static class ConsentRules
    {
        /// <summary>Bumped when the notice text changes, so a changed promise is re-asked.</summary>
        public const int Version = 1;

        /// <summary>The notice, in the game's voice, wrapped for the 5x7 atlas. Painted one line at a time.</summary>
        public static string[] Notice()
        {
            return new string[]
            {
                "THIS GAME CAN RECORD HOW YOU PLAY.",
                "WHAT: THE DECISIONS YOU MAKE, HOW LONG YOU TAKE,",
                "AND THE SEED OF THE RUN. NOTHING ELSE.",
                "WHAT IT NEVER RECORDS: YOUR NAME, ANYTHING YOU",
                "TYPE, OR ANYTHING ELSE ON THIS DEVICE.",
                "WHY: SO THE PEOPLE WHO MADE THIS CAN TELL",
                "WHETHER IT TEACHES WHAT IT MEANS TO.",
                "IT IS OFF UNTIL YOU SAY YES, AND YOU CAN",
                "CHANGE YOUR MIND AT ANY TIME IN THIS MENU.",
                "SAYING NO DOES NOT CHANGE THE GAME. AT ALL."
            };
        }

        /// <summary>True only when the player has decided AND said yes. Silence is not consent.</summary>
        public static bool TelemetryAllowed(bool decided, bool granted)
        {
            return decided && granted;
        }

        /// <summary>Whether the notice has to be shown again (never decided, or a new version of the text).</summary>
        public static bool MustAsk(bool decided, int decidedVersion)
        {
            return !decided || decidedVersion < Version;
        }

        /// <summary>The one-line summary the settings screen shows.</summary>
        public static string Summary(bool decided, bool granted)
        {
            if (!decided) return "DATA: NOT ASKED YET";
            return granted ? "DATA: SHARED" : "DATA: NOT SHARED";
        }
    }
}
