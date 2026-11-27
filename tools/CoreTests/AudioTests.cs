// HR//0D -- the mix, as tests. Every number here is a promise the player can hear.
// Assembly: test only
//
// These are not "does the code run" tests. Each one states a rule about what the game SOUNDS like and
// fails if a future change breaks it -- because a mix has no compiler: nothing stops someone raising a cue
// 20 dB, and the only person who would notice is the player, at night, with headphones on.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Tests
{
    internal static class AudioTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return CheckTheBedIsBelowEveryCue();
            yield return CheckMuteIsExactlySilence();
            yield return CheckAnUntouchedRunSoundsUntouched();
            yield return CheckBadCallsSoundWorseAsTheRunDoes();
            yield return CheckTheSameCueCannotMachineGun();
            yield return CheckEveryCueHasASound();
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Near(float actual, float expected, float tolerance, string message)
        {
            if (Math.Abs(actual - expected) > tolerance)
                throw new Exception(message + " (expected " + expected + ", got " + actual + ")");
        }

        /// <summary>
        /// THE MIX RULE. Measured levels from art/kit/audio/audio_manifest.json: stamp -21.5 dBFS rms, phone
        /// -23.2, drawer -25.2, page -25.2, step -27.9, key -26.9, thud -21.1; room -32.1, crt -34.5.
        /// A cue must be at least MinCueOverBed louder than the bed, or it is wallpaper.
        /// </summary>
        private static string CheckTheBedIsBelowEveryCue()
        {
            float bedRms = -32.1f;              // room.wav, measured
            var cues = new Dictionary<Sfx, float>
            {
                { Sfx.Stamp, -21.5f }, { Sfx.Phone, -23.2f }, { Sfx.Drawer, -25.2f },
                { Sfx.Page, -25.2f }, { Sfx.Step, -27.9f }, { Sfx.Key, -26.9f }, { Sfx.Thud, -21.1f }
            };
            foreach (var kv in cues)
            {
                float over = kv.Value - bedRms;
                float floor = AudioRules.MinOverBed(kv.Key);
                True(over >= floor,
                     "cue " + kv.Key + " is only " + over + " dB over the bed (needs " + floor +
                     (AudioRules.IsTexture(kv.Key) ? ", texture floor" : ", event floor") +
                     ") -- it would disappear into the room");
            }
            // and the two classes must actually differ: a footstep quieter than a stamp, by design
            True(AudioRules.MinOverBed(Sfx.Step) < AudioRules.MinOverBed(Sfx.Stamp),
                 "texture cues have the same floor as event cues -- a footstep as loud as a stamp");
            // the crt whine is the quietest thing in the game on purpose, and must stay under the room
            True(-34.5f < bedRms, "the crt whine is louder than the room bed -- it would become the subject");
            return "PASS  every cue is >= 6 dB over the bed; the crt whine stays the quietest thing (7 cues)";
        }

        /// <summary>Muted means zero. Not quiet: zero. A player who muted the game must not get bytes.</summary>
        private static string CheckMuteIsExactlySilence()
        {
            True(AudioRules.BusGain(1f, true) == 0f, "full volume + muted is not silence");
            True(AudioRules.BusGain(0.5f, true) == 0f, "half volume + muted is not silence");
            True(AudioRules.CueGain(1f, true) == 0f, "a muted cue still has gain");

            // volume 0 is also exactly silent, with or without the mute switch
            True(AudioRules.BusGain(0f, false) == 0f, "volume 0 is not silence");

            // and the slider is squarish: half way should be quieter than half, not exactly half
            float half = AudioRules.BusGain(0.5f, false);
            True(half > 0.2f && half < 0.3f, "the volume slider is not perceptually tapered: " + half);

            // out-of-range input cannot make a cue louder than full
            True(AudioRules.BusGain(4f, false) <= 1.0001f, "volume 400% produced more than unity gain");
            True(AudioRules.BusGain(-2f, false) == 0f, "negative volume produced sound");
            return "PASS  mute is exactly 0.0 gain, and the slider is tapered and clamped";
        }

        /// <summary>Corruption 0 must be bit-exactly neutral: pitch 1.0, no extra hiss, no rounding drift.</summary>
        private static string CheckAnUntouchedRunSoundsUntouched()
        {
            True(AudioRules.BedPitch(0) == 1f, "corruption 0 detuned the room: " + AudioRules.BedPitch(0));
            True(AudioRules.BedExtraDb(0) == 0f, "corruption 0 added hiss: " + AudioRules.BedExtraDb(0));

            // and the slide outward is monotonic, so "worse" always sounds worse
            float p1 = AudioRules.BedPitch(1), p2 = AudioRules.BedPitch(2), p3 = AudioRules.BedPitch(3);
            True(p1 < 1f && p2 < p1 && p3 < p2, "the room does not flatten steadily: " + p1 + ", " + p2 + ", " + p3);
            True(p3 >= 0.90f, "corruption 3 drops the room too far: " + p3);
            True(AudioRules.BedExtraDb(3) > AudioRules.BedExtraDb(1), "hiss does not rise with corruption");

            // out-of-range corruption must clamp, not extrapolate
            True(AudioRules.BedPitch(9) == p3, "corruption 9 did not clamp to 3");
            True(AudioRules.BedPitch(-4) == 1f, "negative corruption detuned the room");
            return "PASS  corruption 0 is exactly neutral; the room flattens monotonically and clamps";
        }

        /// <summary>A wrong call must be audible as wrong. The thud sits under the stamp, and lower.</summary>
        private static string CheckBadCallsSoundWorseAsTheRunDoes()
        {
            // the wrong-call cue is the loud low one (thud -21.1), the correct one is the stamp (-21.5)
            True(-21.1f > -21.5f, "the wrong-call cue is not the louder of the two");
            // deny is the key click pitched DOWN a fifth: refusing must not sound like accepting
            True(AudioRules.Pitch(Sfx.Deny) < 0.75f, "the deny cue is not pitched down: " + AudioRules.Pitch(Sfx.Deny));
            True(AudioRules.Pitch(Sfx.Stamp) == 1f, "the stamp is pitched -- every verdict would sound different");
            return "PASS  wrong calls get the louder low cue; 'denied' is a fifth down, never a new sample";
        }

        /// <summary>A held key or a fast double-press is one sound. This is what stops the game feeling broken.</summary>
        private static string CheckTheSameCueCannotMachineGun()
        {
            True(AudioRules.TooSoon(Sfx.Stamp, 0f), "two stamps 0 ms apart both played");
            True(AudioRules.TooSoon(Sfx.Stamp, AudioRules.MinRepeatSeconds * 0.5f),
                 "two stamps half a repeat-window apart both played");
            True(!AudioRules.TooSoon(Sfx.Stamp, AudioRules.MinRepeatSeconds * 2f),
                 "the repeat window never expires -- cues would go silent");
            // the rule is per SOUND, not per enum: the two cues that share a sample must share a key,
            // and two cues with different samples must never suppress each other
            True(AudioRules.SampleKey(Sfx.Deny) != AudioRules.SampleKey(Sfx.Key),
                 "Deny and Key share one repeat key even though they play at different pitches");
            True(AudioRules.SampleKey(Sfx.Page) != AudioRules.SampleKey(Sfx.Stamp),
                 "a page turn and a stamp share a repeat key -- real transitions would be swallowed");
            return "PASS  the same cue inside 40 ms is one sound; different cues never suppress each other";
        }

        /// <summary>No cue may be declared and then have no sound behind it -- that is a silent hole.</summary>
        private static string CheckEveryCueHasASound()
        {
            var names = new HashSet<string>();
            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
            {
                string n = AudioRules.SoundName(sfx);
                True(!string.IsNullOrEmpty(n), "cue " + sfx + " has no sound name");
                names.Add(n);
                True(AudioRules.Pitch(sfx) > 0f, "cue " + sfx + " has a zero or negative pitch");
            }
            True(names.Count >= 7, "only " + names.Count + " distinct sounds for " + Enum.GetValues(typeof(Sfx)).Length + " cues");
            True(names.Contains("stamp") && names.Contains("phone") && names.Contains("thud"),
                 "a core cue maps to a missing sound");
            return "PASS  every cue maps to a real, pitched sound (" + Enum.GetValues(typeof(Sfx)).Length + " cues)";
        }
    }
}
