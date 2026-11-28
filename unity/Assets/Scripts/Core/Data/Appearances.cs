// HR//0D -- Appearances: which of the shipped sprites is this person?
// Assembly: HRZeroDay.Core  (pure C#, seeded -- no UnityEngine)
//
// THE PLAYER'S INSTRUCTION (v130, 2026-11-29): "add varieties of the applicant".
//
// THE BUG THIS FIXES, stated plainly
//     DeskSession.ArtKey() mapped an ARCHETYPE to a fixed file: every HumanSolid was human_solid_a, every
//     SyntheticPolished was synthetic_polished_a. Six faces for a five-day run of 23 people, and the three
//     humans of one archetype were literally the same drawing. The art for fourteen (now fifteen) variants
//     was already in the kit and the run never asked for it.
//
// THE RULE
//     * The KEY is a pure function of (archetype, the run's own Random) -- so invariant I7 still holds:
//       the same seed produces the same people, in the same order, wearing the same faces.
//     * It is chosen once, when the applicant is GENERATED, and stored on the record, so a resumed run shows
//       the same person it showed before the quit.
//     * Deterministic and explicit: no reflection, no file listing at runtime, no UnityEngine in this file.
//
// WHY THE VARIANT MATTERS FOR THE GAME AND NOT ONLY FOR THE LOOKS
//     The synthetic tells are drawn INTO the sprite (docs/07). Two applicants of the same archetype sharing
//     one drawing means the player can learn "the eyes of that one are wrong" instead of reading the tell --
//     which is the exact spot-the-difference failure docs/17 section 8 warns about.
using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Data
{
    public static class Appearances
    {
        /// <summary>
        /// Every shipped variant, by archetype. The count here must match art/kit/applicants/ -- a manifest
        /// test asserts it, because a key with no PNG is a blank applicant and a silent one.
        /// </summary>
        public static readonly Dictionary<Archetype, string[]> Variants = new Dictionary<Archetype, string[]>
        {
            { Archetype.HumanSolid,          new[] { "human_solid_a", "human_solid_b", "human_solid_c" } },
            { Archetype.HumanNervous,        new[] { "human_nervous_a", "human_nervous_b" } },
            { Archetype.HumanBait,           new[] { "human_bait_a", "human_bait_b" } },
            { Archetype.SyntheticPolished,   new[] { "synthetic_polished_a", "synthetic_polished_b", "synthetic_polished_c" } },
            { Archetype.SyntheticCloned,     new[] { "synthetic_cloned_a", "synthetic_cloned_b" } },
            { Archetype.SyntheticCollusion,  new[] { "synthetic_collusion_a", "synthetic_collusion_b" } },
        };

        public const string Fallback = "human_solid_a";

        /// <summary>The archetype's list, never null, always at least one entry.</summary>
        public static string[] For(Archetype archetype)
        {
            string[] list;
            if (Variants.TryGetValue(archetype, out list) && list != null && list.Length > 0) return list;
            return new[] { Fallback };
        }

        /// <summary>
        /// Pick a variant for this applicant. `rng` is the RUN's generator, so the choice is seeded: two runs
        /// from one seed pick the same faces, which is what makes a bug report reproducible.
        /// </summary>
        public static string Pick(Archetype archetype, Random rng)
        {
            var list = For(archetype);
            if (list.Length == 1) return list[0];
            int i = rng == null ? 0 : rng.Next(0, list.Length);
            return list[i];
        }

        /// <summary>
        /// The key an applicant is drawn with: the stored one when it is a key we ship, otherwise a stable
        /// pick from the id (so an old save or an old content file still shows a face, and always the same one).
        /// </summary>
        public static string KeyFor(ApplicantRecord a)
        {
            if (a == null) return Fallback;
            var list = For(a.Archetype);
            if (!string.IsNullOrEmpty(a.spriteKey))
            {
                for (int i = 0; i < list.Length; i++)
                    if (list[i] == a.spriteKey) return a.spriteKey;
            }
            // stable fallback: the id's own characters, not a clock, not a random
            int h = 0;
            string id = a.id ?? string.Empty;
            for (int i = 0; i < id.Length; i++) h = (h * 31 + id[i]) & 0x7fffffff;
            return list[h % list.Length];
        }

        /// <summary>Every key the game can draw, for the manifest gate.</summary>
        public static List<string> AllKeys()
        {
            var all = new List<string>();
            foreach (var kv in Variants)
                for (int i = 0; i < kv.Value.Length; i++)
                    if (!all.Contains(kv.Value[i])) all.Add(kv.Value[i]);
            return all;
        }
    }
}
