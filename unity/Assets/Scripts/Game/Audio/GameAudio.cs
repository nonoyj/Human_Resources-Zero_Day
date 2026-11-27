// HR//0D -- GameAudio: the one place in the game that makes noise.
// Assembly: HRZeroDay.Game  (UnityEngine)
//
// WHAT IS HERE AND WHAT IS NOT. The numbers -- levels, ducking, pitch-by-corruption, the "muted means
// exactly zero" rule -- are in Core/Rules/AudioRules.cs where CoreTests can check them without Unity.
// This file only carries out orders: load the clips, hold two looping beds, and play one-shots on demand.
//
// THREE RULES, and they are all about the same thing: audio must never be the reason the game breaks.
//
//  1. NO CLIP, NO CRASH. The art kit is published into Resources by a python script; a fresh clone that
//     has not run it yet has no WAVs. Every clip load here is allowed to fail, and `Ready` records whether
//     the kit was there. A silent game is a degraded game; a NullReferenceException on the first stamp is
//     a dead one.
//  2. THE BED IS A LOOP AND THE CRT IS A LOOP, and both start on the first frame. There is no "start
//     music" moment -- the room is already there when the player sits down, because they are already in it.
//  3. ONE-SHOTS ARE POOLED. Six voices, round-robin. A rapid sequence of stamps steps on the tail of the
//     previous one instead of refusing to play, and never allocates during play (this runs on phones).
using System.Collections.Generic;
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game.Audio
{
    public class GameAudio : MonoBehaviour
    {
        public const string ClipPath = "Art/Kit/audio/";

        /// <summary>True when the clips loaded. False means the kit is missing and the game is silent.</summary>
        public bool Ready { get; private set; }

        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private AudioSource _bed;
        private AudioSource _crt;
        private AudioSource[] _voices;
        private int _nextVoice;
        private readonly Dictionary<string, float> _lastPlayedAt = new Dictionary<string, float>();

        private float _bedVolume = 0.8f;
        private float _sfxVolume = 0.9f;
        private bool _muted;
        private int _corruption;
        private float _time;

        /// <summary>Builds the audio rig. Called once, by DeskBootstrap, before the first frame of play.</summary>
        public static GameAudio Attach(GameObject host)
        {
            var audio = host.GetComponent<GameAudio>();
            if (audio == null) audio = host.AddComponent<GameAudio>();
            audio.Build();
            return audio;
        }

        private void Build()
        {
            _voices = new AudioSource[AudioRules.MaxVoices];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = MakeSource("voice" + i, false);
            }
            _bed = MakeSource("bed_room", true);
            _crt = MakeSource("bed_crt", true);

            _clips[Sfx.Stamp] = Load("stamp");
            _clips[Sfx.Phone] = Load("phone");
            _clips[Sfx.Drawer] = Load("drawer");
            _clips[Sfx.Page] = Load("page");
            _clips[Sfx.Step] = Load("step");
            _clips[Sfx.Key] = Load("key");
            _clips[Sfx.Thud] = Load("thud");
            // Deny is the key click pitched down (AudioRules.Pitch) -- no extra file to ship.
            _clips[Sfx.Deny] = _clips[Sfx.Key];

            _bed.clip = Load("room");
            _crt.clip = Load("crt");

            Ready = _clips[Sfx.Stamp] != null && _bed.clip != null;
            if (!Ready)
            {
                // ONE line, not one per missing file: the art gate names the files, this just says why
                // the game is quiet.
                Debug.LogWarning("[HR//0D] audio kit not found under Resources/" + ClipPath +
                                 " -- the game will run silent. Run: python tools/audio/make_audio.py");
            }
            StartBeds();
        }

        private AudioSource MakeSource(string name, bool looping)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = looping;
            src.playOnAwake = false;
            src.volume = 0f;
            return src;
        }

        private AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>(ClipPath + name);
            if (clip == null && name != "crt") { /* reported once by Ready, not per file */ }
            return clip;
        }

        private void StartBeds()
        {
            if (_bed.clip != null) { _bed.Play(); }
            if (_crt.clip != null) { _crt.Play(); }
            ApplyMix();
        }

        /// <summary>Called by DeskBootstrap with the save's profile. Clamps and stores; the mix is applied at once.</summary>
        public void Configure(float bedVolume01, float sfxVolume01, bool muted)
        {
            _bedVolume = bedVolume01;
            _sfxVolume = sfxVolume01;
            _muted = muted;
            ApplyMix();
        }

        /// <summary>Corruption detunes the room instead of replacing it (AudioRules.BedPitch).</summary>
        public void SetCorruption(int level)
        {
            if (level == _corruption) return;
            _corruption = level;
            ApplyMix();
        }

        private void ApplyMix()
        {
            float bed = AudioRules.BusGain(_bedVolume, _muted) * AudioRules.Gain(AudioRules.BedDb);
            float crt = AudioRules.BusGain(_bedVolume, _muted) * AudioRules.Gain(AudioRules.BedDb + AudioRules.CrtDb);
            float hiss = AudioRules.BedExtraDb(_corruption);
            if (hiss > 0f) bed *= AudioRules.Gain(hiss);
            float pitch = AudioRules.BedPitch(_corruption);

            if (_bed != null) { _bed.volume = bed; _bed.pitch = pitch; }
            if (_crt != null) { _crt.volume = crt; _crt.pitch = pitch; }
        }

        private void Update()
        {
            _time += Time.deltaTime;
        }

        // ------------------------------------------------------------------ cues
        /// <summary>
        /// Plays a cue. Silent (not an error) when the kit is missing, when the player is muted, or when the
        /// same cue played less than AudioRules.MinRepeatSeconds ago.
        /// </summary>
        public void Play(Sfx sfx)
        {
            if (!Ready || _muted) return;
            // keyed by SAMPLE identity, not by enum: Deny and Key are the same clip at different pitches,
            // so a deny immediately after a key press is one sound, and a page turn after a stamp is two.
            string key = AudioRules.SampleKey(sfx);
            float last;
            if (_lastPlayedAt.TryGetValue(key, out last) && AudioRules.TooSoon(sfx, _time - last)) return;
            _lastPlayedAt[key] = _time;

            AudioClip clip;
            if (!_clips.TryGetValue(sfx, out clip) || clip == null) return;

            var src = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            src.clip = clip;
            src.pitch = AudioRules.Pitch(sfx);
            src.volume = AudioRules.CueGain(_sfxVolume, _muted);
            src.Play();
        }

        // Named cues. The names say what happens, not what file plays, so the desk code reads as actions.

        /// <summary>The player's verdict lands on paper. The loudest thing in the game on purpose.</summary>
        public void Stamp() { Play(Sfx.Stamp); }

        /// <summary>An email arrives, or the hotline rings.</summary>
        public void Phone() { Play(Sfx.Phone); }

        /// <summary>A folder, a drawer, a manual.</summary>
        public void Drawer() { Play(Sfx.Drawer); }

        /// <summary>A cover letter, a page of the manual, a sweep page.</summary>
        public void Page() { Play(Sfx.Page); }

        /// <summary>A footstep on carpet. The after-hours walk calls this per segment.</summary>
        public void Step() { Play(Sfx.Step); }

        /// <summary>A key. Menus only -- never during the desk, where the sound would be wallpaper.</summary>
        public void Key() { Play(Sfx.Key); }

        /// <summary>Something went wrong and the player should feel it before they read it.</summary>
        public void Wrong() { Play(Sfx.Thud); }

        /// <summary>A refused action: not allowed yet, not applicable, out of shifts.</summary>
        public void Deny() { Play(Sfx.Deny); }
    }
}
