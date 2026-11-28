// HR//0D -- The applicant record: everything shown on the desk, plus hidden ground truth.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
// JsonUtility-friendly: public fields only, no dictionaries, no nullable value types.
using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Data
{
    public enum Archetype
    {
        HumanSolid, HumanNervous, HumanBait,
        SyntheticPolished, SyntheticCloned, SyntheticCollusion
    }

    [Serializable]
    public class ApplicantRecord
    {
        public string id;
        public int day;

        // --- hidden from the player -------------------------------------------------
        public int archetypeRaw;          // (int)Archetype
        public int groundTruthRaw;        // (int)GroundTruth
        public List<string> tellsPresent = new List<string>();
        public List<string> distractorsPresent = new List<string>();

        // --- shown on the desk ------------------------------------------------------
        public string fullName;
        public int age;
        public string idNumber;
        public string university;
        public int graduationYear;
        public string employer;
        public int employerFounded;
        public int employmentStartYear;
        public int yearsExperience;
        public string role;
        public string referral;
        public string coverLetterId;
        public string portraitId;

        /// <summary>
        /// WHICH SPRITE THIS PERSON IS (v130). Empty in older saves and older content files, in which case
        /// Appearances.Pick fills it from the archetype. Stored rather than derived so a resume shows the same
        /// face it showed before the quit -- a person who changes face between sessions is a bug the player
        /// will read as a tell.
        /// </summary>
        public string spriteKey;

        public Archetype Archetype { get { return (Archetype)archetypeRaw; } }
        public GroundTruth Truth { get { return (GroundTruth)groundTruthRaw; } }
        public bool IsSynthetic { get { return Truth == GroundTruth.Synthetic; } }
        public int CareerSpanYears { get { return RunGenerator.CurrentYear - graduationYear; } }

        /// <summary>
        /// The factual contradictions this record actually contains, computed from the values --
        /// never from a hand-written flag. Invariant I3 is checked against this method.
        /// </summary>
        public List<string> RecomputeFactualTells()
        {
            var found = new List<string>();
            if (yearsExperience > CareerSpanYears) found.Add(TellCatalog.ExpVsGrad);
            if (employmentStartYear < employerFounded) found.Add(TellCatalog.EmployerFounded);
            int ageAtGraduation = graduationYear - (RunGenerator.CurrentYear - age);
            if (ageAtGraduation < 15) found.Add(TellCatalog.GradTooYoung);
            if (!IdChecksumValid(idNumber)) found.Add(TellCatalog.IdChecksum);
            return found;
        }

        /// <summary>Demo ID checksum: last digit is the sum of the previous digits, mod 10.</summary>
        public static bool IdChecksumValid(string idNumber)
        {
            if (string.IsNullOrEmpty(idNumber)) return false;
            var digits = new List<int>();
            for (int i = 0; i < idNumber.Length; i++)
            {
                char c = idNumber[i];
                if (c >= '0' && c <= '9') digits.Add(c - '0');
            }
            if (digits.Count != 8) return false;
            int sum = 0;
            for (int i = 0; i < 7; i++) sum += digits[i];
            return (sum % 10) == digits[7];
        }

        public override string ToString()
        {
            return fullName + " (" + Archetype + ", " + Truth + ")";
        }
    }
}
