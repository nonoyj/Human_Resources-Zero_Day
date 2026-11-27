// HR//0D -- AudioRules: what the game sounds like, as arithmetic. No UnityEngine in this file.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
//
// WHY THIS IS NOT IN THE VIEW. The mix is a set of numbers with rules attached -- the bed must sit under
// every one-off, the whine must duck while the phone rings, a muted player must get silence rather than
// quiet -- and rules with numbers attached are exactly the things that rot when nobody checks them. So the
// levels live here, in a file CoreTests can run without Unity, and the Unity side only carries out orders.
//
// THE MIX (measured, see art/kit/audio/audio_manifest.json and tools/audio/preview_audio.py):
//
//      one-off cues   rms -21 .. -31 dBFS at peak            the things the player causes
//      room bed       rms -32 dBFS, forever                  the thing the player stops hearing
//      crt whine      rms -34.5 dBFS                         the thing that says "a monitor is on"
//
// A cue is never loud in absolute terms; it is loud RELATIVE to the bed, and the bed is felt rather than
// heard. That is the whole design, and it is why this file is mostly subtraction.
using System;

namespace HRZeroDay.Core.Rules
{
    /// <summary>Every sound the game can make. The enum is the contract; the WAV names are in SoundName.</summary>
    public enum Sfx
    {
        Stamp,
        Phone,
        Drawer,
        Page,
        Step,
        Key,
        Thud,
        Deny
    }

    public static class AudioRules
    {
        // ---- the master levels, in dB. These are the numbers a mix engineer would turn. -----------------
        public const float MasterDb = 0f;
        public const float BedDb = -6f;          // the room, relative to its own generated level
        public const float CrtDb = -9f;
        public const float CueDb = 0f;

        /// <summary>While a cue is sounding, the bed drops by this much. Prevents the room masking a cue.</summary>
        public const float DuckDb = -7f;
        public const float DuckSeconds = 0.35f;
        public const float DuckReleaseSeconds = 0.8f;

        /// <summary>
        /// How much LOUDER an EVENT cue is than the bed. Below this it disappears into the room.
        /// Events are things the player causes once and should feel: the stamp, the phone, the drawer.
        /// </summary>
        public const float MinCueOverBed = 6f;

        /// <summary>
        /// The same floor for TEXTURE cues -- footsteps on the walk, key clicks in a menu. They fire
        /// repeatedly, so they are allowed to sit lower (a footstep as loud as a stamp is a broken game),
        /// but they still have to be audible: below this they are not quiet, they are absent.
        /// </summary>
        public const float MinTextureOverBed = 4f;

        /// <summary>True for cues that repeat during play, which the mix treats as texture rather than events.</summary>
        public static bool IsTexture(Sfx sfx)
        {
            return sfx == Sfx.Step || sfx == Sfx.Key;
        }

        /// <summary>The loudness floor this cue must clear over the bed, in dB.</summary>
        public static float MinOverBed(Sfx sfx)
        {
            return IsTexture(sfx) ? MinTextureOverBed : MinCueOverBed;
        }

        /// <summary>Same cue twice inside this window is one sound, not two (a held key, a double click).</summary>
        public const float MinRepeatSeconds = 0.04f;

        /// <summary>The room bed is the same bed all run; corruption detunes it instead of replacing it.</summary>
        public const float MaxCorruptionSemitones = -1.5f;   // at corruption 3, the bed plays 1.5 semitones flat
        public const float MaxCorruptionHissDb = 3.5f;       // and a little more hiss

        public const int MaxVoices = 6;

        /// <summary>The WAV basename for a cue, under Resources/Art/Kit/audio/.</summary>
        public static string SoundName(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Stamp: return "stamp";
                case Sfx.Phone: return "phone";
                case Sfx.Drawer: return "drawer";
                case Sfx.Page: return "page";
                case Sfx.Step: return "step";
                case Sfx.Key: return "key";
                case Sfx.Thud: return "thud";
                case Sfx.Deny: return "key";     // v105 reuses the key click, pitched down, rather than shipping a file
                default: return "key";
            }
        }

        /// <summary>
        /// A cue's playback pitch. Only one cue is currently pitched: the "denied" sound is the key click
        /// dropped a fifth, which reads as a refusal without inventing a new sample.
        /// </summary>
        public static float Pitch(Sfx sfx)
        {
            return sfx == Sfx.Deny ? 0.667f : 1f;
        }

        /// <summary>Volume in linear gain for a category. Everything is mixed in dB and converted once, here.</summary>
        public static float Gain(float db)
        {
            return (float)Math.Pow(10.0, db / 20.0);
        }

        /// <summary>
        /// The bed's pitch for a corruption level (0..3). Zero corruption is exactly 1.0 -- an untouched run
        /// must sound untouched, so this returns 1f rather than "approximately 1".
        /// </summary>
        public static float BedPitch(int corruptionLevel)
        {
            int c = Clamp(corruptionLevel, 0, 3);
            if (c == 0) return 1f;
            float t = c / 3f;
            return (float)Math.Pow(2.0, (MaxCorruptionSemitones * t) / 12.0);
        }

        /// <summary>Extra hiss in dB on the bed for a corruption level. 0 at level 0, always.</summary>
        public static float BedExtraDb(int corruptionLevel)
        {
            int c = Clamp(corruptionLevel, 0, 3);
            if (c == 0) return 0f;
            return MaxCorruptionHissDb * (c / 3f);
        }

        /// <summary>
        /// True when a cue should be skipped because the SAME cue just played.
        ///
        /// "The same cue" means the same sound: Deny is a pitched key click, so a deny and a key press
        /// inside 40 ms is a double-trigger of one sample, not two events. Everything else is judged by
        /// its own identity -- the first version of this compared Sfx values and a page turn 0 s after a
        /// stamp was reported as "suppressed", which is a rule that would have muted the game's own
        /// transitions. The test caught it before the sound did.
        /// </summary>
        public static bool TooSoon(Sfx sfx, float secondsSinceSameCue)
        {
            if (secondsSinceSameCue < 0f || secondsSinceSameCue >= MinRepeatSeconds) return false;
            return true;
        }

        /// <summary>The identity two cues share when they play the same sample. Used by the voice pool.</summary>
        public static string SampleKey(Sfx sfx)
        {
            return SoundName(sfx) + "@" + Pitch(sfx).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The final linear gain for a cue, with the player's settings applied. Muted means EXACTLY zero:
        /// a muted player must not hear a quiet version of the game, and code that multiplies by 0.0001
        /// still puts bytes on the wire.
        /// </summary>
        public static float CueGain(float sfxVolume, bool muted)
        {
            return BusGain(sfxVolume, muted) * Gain(CueDb);
        }

        /// <summary>Volume for a bus, 0..1, muted -> 0. Used by MusicRules' sibling and by tests.</summary>
        public static float BusGain(float volume01, bool muted)
        {
            if (muted) return 0f;
            float v = Clamp(volume01, 0f, 1f);
            if (v <= 0.0001f) return 0f;
            // a linear slider is not a linear ear: square it so the middle of the slider sounds like half
            return v * v;
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }
}
