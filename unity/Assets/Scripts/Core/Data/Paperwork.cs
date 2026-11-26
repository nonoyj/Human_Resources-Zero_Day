// HR//0D -- Paperwork: the letter is read ONCE, and what you flagged is what you carry to the stamp.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
// Owner: Agent (written) / Dev A (owns the letter UI). Started 2026-11-26.
//
// USER DECISION 2026-11-26, question 2: a letter CANNOT be reopened after the stamp.
//   "One read, then commit." That is the whole weight of the flag choice -- if you can re-read, you
//   can also re-decide, and the flags become a scouting report instead of a judgement.
//
// WHAT LIVES HERE AND NOT IN THE VIEW
//   The letter body is GENERATED FROM THE RECORD (deterministic, invariant I7) rather than stored, so
//   a save file does not have to carry five days of prose. The one thing that must be stored is the
//   RUN'S LORE SEED -- and that is derived from the seed that is already in the save.
using System;
using System.Collections.Generic;
using System.Text;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Core.Data
{
    public sealed class Paperwork
    {
        /// <summary>Name of the company the player works for. Fixed, because it is on every wall.</summary>
        public const string Company = "NORTHSTAR LOGISTICS";

        public ApplicantRecord applicant;
        public string title;
        public string body;
        public string signature;
        public List<FlagOption> flags = new List<FlagOption>();
        public HashSet<string> flagged = new HashSet<string>();

        /// <summary>Set at the stamp. After this the letter can never be opened again (user decision).</summary>
        public bool readClosed;
        public int pageIndex;

        public bool CanOpen => !readClosed;
        public bool HasOpenFlags => flagged.Count > 0;

        /// <summary>
        /// +1 for every flag that was true, -1 for every flag that was not. Named flags reach the
        /// debrief; the number never appears on the glass (the CRT does not grade the player).
        /// </summary>
        public int Score
        {
            get
            {
                int s = 0;
                foreach (var f in flags)
                {
                    if (!flagged.Contains(f.id)) continue;
                    s += f.isReal ? 1 : -1;
                }
                return s;
            }
        }

        /// <summary>Trust owed back (right flags) or taken (wrong flags), applied when the stamp lands.</summary>
        public int TrustDelta
        {
            get
            {
                int d = 0;
                foreach (var f in flags)
                    if (flagged.Contains(f.id)) d += f.isReal ? f.trustDelta : -f.trustDelta;
                return d;
            }
        }

        public bool Toggle(string flagId)
        {
            if (readClosed) return false;                 // no edit after the stamp either
            if (!flagged.Remove(flagId)) flagged.Add(flagId);
            return true;
        }

        public void CloseForStamp() { readClosed = true; }

        // ------------------------------------------------------------------ generation
        /// <summary>
        /// Builds the letter for one applicant. Deterministic in (seed, applicant.id): the same run
        /// always produces the same letter, and the tells in the prose match the flags offered.
        /// </summary>
        public static Paperwork For(ApplicantRecord a, int runSeed)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            var p = new Paperwork { applicant = a };
            var rng = new Random(Hash(runSeed, a.id));
            bool fake = a.IsSynthetic || a.Truth == GroundTruth.HumanBait;

            p.title = "COVER LETTER  " + (a.fullName ?? "APPLICANT").ToUpperInvariant();
            p.body = fake ? FakeBody(a, rng) : RealBody(a, rng);
            p.signature = Signature(a, fake, rng);
            // The signature is part of the letter the player reads, so it is part of the text the flags
            // are derived from. Passing the body alone made "NO SIGNATURE" true on EVERY letter --
            // caught by the 40-seed test, not by reading the code.
            p.flags = LetterFlags.Build(a, p.body + "\n" + p.signature);
            return p;
        }

        private static int Hash(int seed, string id)
        {
            unchecked
            {
                int h = seed * 397;
                foreach (char c in id ?? string.Empty) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        private static string RealBody(ApplicantRecord a, Random rng)
        {
            var sb = new StringBuilder();
            sb.Append("Dear Hiring Team,\n\n");
            sb.Append("I am applying for the ").Append((a.role ?? "open").ToLowerInvariant())
              .Append(" position at ").Append(Company).Append(". ");
            if (!string.IsNullOrEmpty(a.employer))
                sb.Append("I have spent ").Append(a.yearsExperience).Append(" years at ")
                  .Append(a.employer).Append(" and I am looking for work closer to home. ");
            if (!string.IsNullOrEmpty(a.university))
                sb.Append("I finished my studies at ").Append(a.university).Append(" in ")
                  .Append(a.graduationYear).Append(". ");
            sb.Append(Pick(rng, new[]
            {
                "I am available to start on short notice.",
                "I can provide references from my previous supervisor.",
                "I work well with a fixed daily list and I do not need to be chased."
            }));
            sb.Append("\n\nThank you for your time.");
            return sb.ToString();
        }

        private static string FakeBody(ApplicantRecord a, Random rng)
        {
            // A synthetic writes a letter that flatters the reader, not one that answers the job.
            // A human bait writes one that pressures. Both carry paper tells on purpose.
            var sb = new StringBuilder();
            if (a.Truth == GroundTruth.HumanBait)
            {
                sb.Append("Dear Sir/Madam,\n\n")
                  .Append("Your application was selected immediately. To activate your slot you must pay a ")
                  .Append("processing fee of 500 pesos and send a deposit today. This offer is urgent and ")
                  .Append("expires in two hours, so reply now.\n\nThank you");
            }
            else
            {
                sb.Append("Dear [NAME],\n\n")
                  .Append("I have reviewed your requirements and I meet every one of them. My records are ")
                  .Append("complete and my references are available immediately. I can start today and I ")
                  .Append("require no training.\n\nThank you");
            }
            return sb.ToString();
        }

        private static string Signature(ApplicantRecord a, bool fake, Random rng)
        {
            // The missing-signature tell has to be a real absence, so the fake letters drop it
            // deterministically -- not at random, or the same applicant would sign on one run and
            // not the next, breaking invariant I7.
            if (fake && rng.Next(2) == 0) return string.Empty;
            return "Regards,\n" + (a.fullName ?? string.Empty);
        }

        private static string Pick(Random rng, string[] options) => options[rng.Next(options.Length)];
    }
}
