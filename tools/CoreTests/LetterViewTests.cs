// HR//0D -- tests for the letter view: wrapping, pagination, flag scoring, and the row budget.
//
// The flag scoring is the game's teaching mechanism (a player who flags a normal letter loses trust),
// so it gets tests before it gets UI.
using System;
using System.Collections.Generic;
using System.Linq;
using HRZeroDay.Core.Data;
using HRZeroDay.Game;

namespace HRZeroDay.Tests
{
    public static class LetterViewTests
    {
        private const string CleanLetter =
            "Dear Ms Santos,\n\nWe reviewed your application for the data entry post and would like " +
            "to invite you for an interview on Monday at nine in the morning. Please bring one " +
            "government identification card.\n\nWe look forward to meeting you.\n\nRegards,\nNorthstar Logistics";

        private const string ScamLetter =
            "Dear Sir/Madam,\n\nYour application was selected immediately. To activate your slot you " +
            "must pay a processing fee of 500 pesos and send a deposit today. This offer expires in " +
            "two hours, so reply now or lose the position. Training starts the same day with no " +
            "further review of your documents or your references.\n\nThank you, Human Resources";

        public static IEnumerable<string> RunAll()
        {
            yield return WrapNeverExceedsTheColumnCount();
            yield return WrapKeepsEveryWord();
            yield return LetterPageFitsTheRowBudget();
            yield return LongLetterPaginatesWithoutLosingText();
            yield return CleanLetterProducesNoRealFlags();
            yield return ScamLetterFlagsMoneyUrgencyAndTemplate();
            yield return FlaggingTheTruthScoresPositiveFlaggingInnocenceScoresNegative();
            yield return ToggleIsIdempotentPerFlag();
        }

        private static ApplicantRecord Applicant(string referral = "WALK-IN") => new ApplicantRecord
        {
            id = "A-0007", day = 2, fullName = "Maria Santos", role = "Data Entry II",
            idNumber = "8814-2231", university = "Cebu Normal University", graduationYear = 2019,
            employer = "Northstar Logistics", yearsExperience = 6, referral = referral,
            coverLetterId = "CL-0007"
        };

        private static string WrapNeverExceedsTheColumnCount()
        {
            var wrapped = LetterLayout.Wrap(CleanLetter, LetterLayout.Cols);
            foreach (var line in wrapped)
                if (line.Length > LetterLayout.Cols)
                    return $"FAIL  wrap_line_too_long {line.Length}>{LetterLayout.Cols} '{line}'";
            return $"PASS  every wrapped line is <= {LetterLayout.Cols} columns ({wrapped.Count} lines)";
        }

        private static string WrapKeepsEveryWord()
        {
            var wrapped = string.Join(" ", LetterLayout.Wrap(CleanLetter, LetterLayout.Cols));
            foreach (var word in CleanLetter.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                if (!wrapped.Contains(word))
                    return $"FAIL  wrap_lost_word '{word}'";
            return "PASS  wrapping keeps every word (nothing dropped, nothing duplicated at the seams)";
        }

        private static string LetterPageFitsTheRowBudget()
        {
            var pages = LetterPaginator.Paginate("COVER LETTER  MARIA SANTOS", CleanLetter, "Regards,", null);
            foreach (var p in pages)
            {
                if (p.RequiredRows > LetterLayout.TotalRows)
                    return $"FAIL  letter_page_overflows {p.RequiredRows}>{LetterLayout.TotalRows} rows";
                var rects = MonitorScreen.LinesToRects(p.ToLines(),
                    new MonitorScreen.GlassRect { x = 0, y = 0, w = 456, h = 246 });
                foreach (var r in rects)
                    if (r.truncated) return $"FAIL  letter_line_was_truncated '{r.text}'";
            }
            return $"PASS  a one-page letter needs {pages[0].RequiredRows} of {LetterLayout.TotalRows} rows, nothing truncated";
        }

        private static string LongLetterPaginatesWithoutLosingText()
        {
            var body = string.Join(" ", Enumerable.Repeat(
                "This is a long paragraph written to overflow a single page of the letter view.", 20));
            var pages = LetterPaginator.Paginate("LONG", body, "Regards,", null);
            if (pages.Count < 2) return "FAIL  long_letter_did_not_paginate";
            int totalBodyLines = pages.Sum(p => p.body.Count);
            int expected = LetterLayout.Wrap(body, LetterLayout.Cols).Count;
            if (totalBodyLines != expected)
                return $"FAIL  pagination_lost_lines {totalBodyLines} != {expected}";
            return $"PASS  a {expected}-line letter paginates into {pages.Count} pages with no lost lines";
        }

        private static string CleanLetterProducesNoRealFlags()
        {
            var flags = LetterFlags.Build(Applicant(), CleanLetter);
            var real = flags.Where(f => f.isReal).Select(f => f.id).ToList();
            if (real.Count > 0) return "FAIL  clean_letter_has_real_flags " + string.Join(",", real);
            return $"PASS  the normal letter has 0 of {flags.Count} real flags -- flagging it is a mistake";
        }

        private static string ScamLetterFlagsMoneyUrgencyAndTemplate()
        {
            var flags = LetterFlags.Build(Applicant(), ScamLetter);
            var real = flags.Where(f => f.isReal).Select(f => f.id).ToList();
            foreach (var need in new[] { LetterFlags.FlagMoneyDemand, LetterFlags.FlagUrgency,
                                         LetterFlags.FlagMassTemplate })
                if (!real.Contains(need)) return $"FAIL  scam_letter_missed '{need}'";
            return "PASS  the scam letter flags money demand, urgency and mass-template language";
        }

        private static string FlaggingTheTruthScoresPositiveFlaggingInnocenceScoresNegative()
        {
            var scam = new LetterSession { applicant = Applicant(), flags = LetterFlags.Build(Applicant(), ScamLetter) };
            var real = scam.flags.First(f => f.isReal).id;
            scam.Toggle(real);
            if (scam.Score != 1) return $"FAIL  correct_flag_scores_{scam.Score}";

            var clean = new LetterSession { applicant = Applicant(), flags = LetterFlags.Build(Applicant(), CleanLetter) };
            foreach (var f in clean.flags) clean.Toggle(f.id);            // flag EVERYTHING
            if (clean.Score >= 0) return $"FAIL  flagging_everything_scored_{clean.Score}";
            return $"PASS  right flag = +1, flagging all {clean.flags.Count} options = {clean.Score}";
        }

        private static string ToggleIsIdempotentPerFlag()
        {
            var s = new LetterSession { applicant = Applicant(), flags = LetterFlags.Build(Applicant(), ScamLetter) };
            var id = s.flags[0].id;
            s.Toggle(id); if (!s.flagged.Contains(id)) return "FAIL  toggle_on_failed";
            s.Toggle(id); if (s.flagged.Contains(id)) return "FAIL  toggle_off_failed";
            s.Toggle(id); s.Toggle(id); s.Toggle(id);
            if (!s.flagged.Contains(id)) return "FAIL  toggle_parity_failed";
            return "PASS  flags toggle cleanly and statelessly";
        }
    }
}
