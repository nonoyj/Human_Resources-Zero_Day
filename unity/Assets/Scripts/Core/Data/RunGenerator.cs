// HR//0D -- Seeded run generator. Port of prototype/generator_sim.py (validated: 0 violations / 1000 runs).
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
// Invariants I1..I8 are enforced here AND re-checked by RunValidator so a content/weights change
// cannot silently ship an unfair run.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Core.Data
{
    public sealed class DayPlan
    {
        public int Day;
        public int Applicants;
        public int Synthetics;
        public int MinTells;
        public int MaxTells;
        public EvidenceCategory NewCategory;

        public DayPlan(int day, int applicants, int synthetics, int minTells, int maxTells, EvidenceCategory cat)
        {
            Day = day; Applicants = applicants; Synthetics = synthetics;
            MinTells = minTells; MaxTells = maxTells; NewCategory = cat;
        }
    }

    public sealed class GenerationConfig
    {
        public int CurrentYear = 2026;
        public readonly Dictionary<int, DayPlan> Days = new Dictionary<int, DayPlan>
        {
            { 1, new DayPlan(1, 4, 1, 2, 2, EvidenceCategory.SyntheticMedia) },
            { 2, new DayPlan(2, 4, 1, 2, 3, EvidenceCategory.DocInconsistency) },
            { 3, new DayPlan(3, 5, 2, 2, 3, EvidenceCategory.Coercion) },
            { 4, new DayPlan(4, 5, 2, 2, 3, EvidenceCategory.SocialEngineering) },
            { 5, new DayPlan(5, 5, 2, 3, 3, EvidenceCategory.Impersonation) }
        };

        /// <summary>Pure C# RNG so runs are reproducible across platforms.</summary>
        public Random Rng;

        public static readonly string[] FirstNames =
        {
            "Marisol","Elias","Tomas","Bianca","Rafael","Josefina","Andres","Corazon","Miguel",
            "Lourdes","Enrique","Carmela","Nestor","Divina","Ferdinand","Imelda","Rodrigo","Teresa",
            "Alonzo","Rosario","Crispin","Malaya","Dalisay","Amihan","Bayani","Liwayway","Makiling",
            "Sampaguita","Tala","Kidlat","Araw","Bituin","Mayumi","Joaquin","Emilio","Pilar","Soledad",
            "Arturo","Graciano","Remedios","Vicente","Esperanza","Leonora","Cesar"
        };

        public static readonly string[] LastNames =
        {
            "Vance","Tan","Reyes","Lim","Cruz","Bautista","Delgado","Mercado","Alonzo","Santiago",
            "Ocampo","Villanueva","Navarro","Domingo","Salazar","Panganiban","Aguilar","Escobar",
            "Mendoza","Roxas","Quiambao","Ylarde","Bathan","Cordial","Dumagsa","Estrella","Fuentes",
            "Gallardo","Hilario","Ignacio","Jarabejo","Kasilag","Lacson","Manalo","Nolasco","Ong","Pineda"
        };

        public static readonly string[][] Universities =   // name, founded
        {
            new[] { "University of San Carlos", "1595" },
            new[] { "University of the Philippines", "1908" },
            new[] { "Ateneo de Manila", "1859" },
            new[] { "De La Salle University", "1911" },
            new[] { "Cebu Institute of Technology", "1946" },
            new[] { "Mapua University", "1925" },
            new[] { "Silliman University", "1901" }
        };

        public static readonly string[][] Employers =     // name, founded
        {
            new[] { "Northwind Logistics", "1998" },
            new[] { "Cebu Pacific Data Services", "1996" },
            new[] { "Meridian Bank", "1974" },
            new[] { "Pacific Ridge Health", "2003" },
            new[] { "Ayala Systems Group", "1988" },
            new[] { "Kalayaan Telecom", "1992" },
            new[] { "Veritas Compliance", "2011" },
            new[] { "Sampaguita Foods", "1985" },
            new[] { "Bisaya Freight", "2009" },
            new[] { "Tala Insurance", "1957" }
        };

        public static readonly string[] Roles =
        {
            "SOC Tier 1 Analyst","Systems Administrator","Data Engineer","Compliance Officer",
            "Network Technician","Incident Responder","Database Administrator","Helpdesk Specialist"
        };

        public static readonly string[] Referrals =
        {
            "Public posting","Employee referral","Recruiter agency","University program",
            "Internal transfer","Director fast-track"
        };
    }

    /// <summary>Per-run uniqueness guard for names and ID numbers (invariant I2).</summary>
    public sealed class NameRegistry
    {
        private readonly HashSet<string> _names = new HashSet<string>();
        private readonly HashSet<string> _ids = new HashSet<string>();

        public bool TryRegister(string name, string id)
        {
            if (_names.Contains(name) || _ids.Contains(id)) return false;
            _names.Add(name); _ids.Add(id);
            return true;
        }
    }

    public sealed class RunGenerator
    {
        public const int CurrentYear = 2026;

        private readonly GenerationConfig _cfg;
        private readonly NameRegistry _registry = new NameRegistry();
        private int _idCounter;

        public RunGenerator(int seed, GenerationConfig config = null)
        {
            _cfg = config ?? new GenerationConfig();
            _cfg.Rng = new Random(seed);
            Seed = seed;
        }

        public int Seed { get; private set; }

        public List<ApplicantRecord> GenerateDay(int day)
        {
            DayPlan plan;
            if (!_cfg.Days.TryGetValue(day, out plan))
                throw new ArgumentOutOfRangeException("day", "no day plan for day " + day);

            var rng = _cfg.Rng;
            var apps = new List<ApplicantRecord>();

            // Synthetics are generated first so the day's count is exact (I4).
            var synthPool = new List<Archetype> { Archetype.SyntheticPolished };
            if (day >= 2) synthPool.Add(Archetype.SyntheticCloned);
            if (day >= 4) synthPool.Add(Archetype.SyntheticCollusion);

            for (int i = 0; i < plan.Synthetics; i++)
            {
                var pick = synthPool[rng.Next(synthPool.Count)];
                apps.Add(Build(day, pick));
            }

            int humans = plan.Applicants - plan.Synthetics;
            var humanPool = new List<Archetype> { Archetype.HumanSolid, Archetype.HumanSolid, Archetype.HumanSolid, Archetype.HumanNervous };
            if (day >= 3) humanPool.Add(Archetype.HumanBait);
            for (int i = 0; i < humans; i++)
                apps.Add(Build(day, humanPool[rng.Next(humanPool.Count)]));

            Shuffle(apps, rng);
            return apps;
        }

        private void Shuffle(List<ApplicantRecord> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        private ApplicantRecord Build(int day, Archetype archetype)
        {
            var rng = _cfg.Rng;
            var a = new ApplicantRecord();
            a.day = day;
            a.archetypeRaw = (int)archetype;
            a.groundTruthRaw = (int)TruthFor(archetype);

            // --- identity (unique within a run) ---
            string name = null, idNumber = null;
            for (int attempt = 0; attempt < 300; attempt++)
            {
                name = GenerationConfig.FirstNames[rng.Next(GenerationConfig.FirstNames.Length)] + " " +
                       GenerationConfig.LastNames[rng.Next(GenerationConfig.LastNames.Length)];
                idNumber = MakeIdNumber(rng);
                if (_registry.TryRegister(name, idNumber)) break;
            }
            a.fullName = name;
            a.idNumber = idNumber;

            a.age = rng.Next(22, 59);
            int birthYear = CurrentYear - a.age;
            var uni = GenerationConfig.Universities[rng.Next(GenerationConfig.Universities.Length)];
            a.university = uni[0];
            int uniFounded = int.Parse(uni[1]);
            a.graduationYear = Math.Min(CurrentYear - 1, Math.Max(birthYear + 21, uniFounded + 18) + rng.Next(0, 4));

            var emp = GenerationConfig.Employers[rng.Next(GenerationConfig.Employers.Length)];
            a.employer = emp[0];
            a.employerFounded = int.Parse(emp[1]);
            int span = CurrentYear - a.graduationYear;
            int earliestStart = Math.Max(a.employerFounded, a.graduationYear);
            int latestStart = Math.Max(earliestStart, CurrentYear - 1);
            a.employmentStartYear = earliestStart + rng.Next(0, Math.Max(1, latestStart - earliestStart + 1));
            a.yearsExperience = Math.Max(0, span - rng.Next(0, Math.Min(4, Math.Max(1, span)) + 1));

            a.role = GenerationConfig.Roles[rng.Next(GenerationConfig.Roles.Length)];
            a.referral = GenerationConfig.Referrals[rng.Next(GenerationConfig.Referrals.Length)];
            a.coverLetterId = "letter_" + archetype.ToString().ToLowerInvariant() + "_" + rng.Next(1, 4);
            a.portraitId = "portrait_" + (_idCounter++).ToString("D3");
            // ---- VARIETY (v130, the player's instruction: "add varieties of the applicant").
            // Until now the sprite was a function of the ARCHETYPE -- six values for a whole run -- so every
            // warehouse supervisor in the game was the same person. The art already ships variants
            // (human_solid_a/_b/_c, synthetic_polished_a/_b/_c, ...); this picks one per APPLICANT, from the
            // run's own rng, so it is stable under I7 (same seed -> same run -> same faces) and different
            // from seed to seed.
            a.spriteKey = Appearances.Pick(archetype, rng);

            ApplyArchetype(a, archetype, day, rng);
            return a;
        }

        private static GroundTruth TruthFor(Archetype archetype)
        {
            switch (archetype)
            {
                case Archetype.HumanSolid:
                case Archetype.HumanNervous: return GroundTruth.Human;
                case Archetype.HumanBait: return GroundTruth.HumanBait;
                default: return GroundTruth.Synthetic;
            }
        }

        private void ApplyArchetype(ApplicantRecord a, Archetype archetype, int day, Random rng)
        {
            List<string> candidates;
            int min, max;

            switch (archetype)
            {
                case Archetype.HumanBait:
                    candidates = new List<string> { TellCatalog.BaitScript, TellCatalog.BaitPressure };
                    min = 1; max = 2;
                    break;
                case Archetype.SyntheticCloned:
                    candidates = new List<string> { TellCatalog.IdentityClone, TellCatalog.RefUnreachable,
                                                    TellCatalog.IdChecksum, TellCatalog.ExpVsGrad };
                    min = 1; max = 2;
                    break;
                case Archetype.SyntheticCollusion:
                    candidates = new List<string> { TellCatalog.GoldenFastTrack, TellCatalog.RefUnreachable,
                                                    TellCatalog.PortraitBlendSeam, TellCatalog.VoiceFlatMetrics };
                    min = 2; max = 3;
                    break;
                case Archetype.SyntheticPolished:
                    candidates = new List<string> { TellCatalog.PortraitAsymmetry, TellCatalog.PortraitBlendSeam,
                                                    TellCatalog.VoiceFlatMetrics, TellCatalog.ExpVsGrad,
                                                    TellCatalog.EmployerFounded, TellCatalog.IdChecksum };
                    min = 2; max = 3;
                    break;
                default:                       // honest humans: no tells at all (I5)
                    min = 0; max = 0;
                    candidates = new List<string>();
                    break;
            }

            // Day gate (I1b): a tell whose surface is locked cannot be drawn.
            candidates.RemoveAll(t => !TellCatalog.IsUnlocked(day, TellCatalog.Get(t).Surface));

            int want = min == max ? min : min + rng.Next(0, max - min + 1);
            for (int i = 0; i < want && candidates.Count > 0; i++)
            {
                int idx = rng.Next(candidates.Count);
                string tellId = candidates[idx];
                candidates.RemoveAt(idx);
                if (ApplyTell(a, tellId, rng)) a.tellsPresent.Add(tellId);
            }

            // Distractors: look like evidence, resolvable elsewhere (I6).
            int distractors = (archetype == Archetype.HumanSolid) ? rng.Next(0, 2) : rng.Next(0, 2);
            var pool = new List<string> { "career_gap", "surname_change", "short_stints", "odd_lighting" };
            for (int i = 0; i < distractors && pool.Count > 0; i++)
            {
                int idx = rng.Next(pool.Count);
                a.distractorsPresent.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
        }

        /// <summary>Mutates the record so the tell is factually present. Returns false if impossible.</summary>
        private bool ApplyTell(ApplicantRecord a, string tellId, Random rng)
        {
            switch (tellId)
            {
                case TellCatalog.ExpVsGrad:
                    a.yearsExperience = a.CareerSpanYears + rng.Next(1, 6);
                    return true;
                case TellCatalog.EmployerFounded:
                    a.employmentStartYear = Math.Max(1950, a.employerFounded - rng.Next(1, 7));
                    return true;
                case TellCatalog.GradTooYoung:
                    a.graduationYear = (CurrentYear - a.age) + rng.Next(9, 15);
                    return true;
                case TellCatalog.IdChecksum:
                    a.idNumber = BreakChecksum(a.idNumber, rng);
                    return true;
                case TellCatalog.GoldenFastTrack:
                    a.referral = "Director fast-track";
                    return true;
                default:
                    return TellCatalog.Has(tellId);   // narrative tells live in art/letters, not the record
            }
        }

        private static string MakeIdNumber(Random rng)
        {
            var d = new int[7];
            int sum = 0;
            for (int i = 0; i < 7; i++) { d[i] = rng.Next(0, 10); sum += d[i]; }
            int check = sum % 10;
            return "PH-" + d[0] + d[1] + d[2] + d[3] + "-" + d[4] + d[5] + d[6] + check;
        }

        private static string BreakChecksum(string idNumber, Random rng)
        {
            var digits = new List<int>();
            foreach (char c in idNumber) if (c >= '0' && c <= '9') digits.Add(c - '0');
            digits[digits.Count - 1] = (digits[digits.Count - 1] + rng.Next(1, 9)) % 10;
            return "PH-" + digits[0] + digits[1] + digits[2] + digits[3] + "-" +
                   digits[4] + digits[5] + digits[6] + digits[7];
        }
    }
}
