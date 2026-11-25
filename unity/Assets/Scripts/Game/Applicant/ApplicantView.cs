// HR//0D -- the person across the table.
// Assembly: HRZeroDay.Game
// Owner: Dev A (systems) / Dev B (scene wiring).
//
// The applicant is ONE IsometricSpriteRenderer driven by a 4-frame idle strip:
//   frame 0 neutral / 1 breathe / 2 breathe + fidget / 3 blink
// Synthetics with the `no_blink` artifact hold frame 0 on the blink step, which is the animation
// tell the player is meant to catch (docs/08-behaviour-model.md).
using System.Collections.Generic;
using UnityEngine;

namespace HRZeroDay.Game
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ApplicantView : MonoBehaviour
    {
        public const int FrameNeutral = 0;
        public const int FrameBreathe = 1;
        public const int FrameFidget = 2;
        public const int FrameBlink = 3;

        [Header("Frames (assign in the prefab; order is neutral, breathe, fidget, blink)")]
        public Sprite[] frames = new Sprite[4];

        [Range(0.5f, 12f)] public float fps = 4f;
        public bool animate = true;

        [Header("Artifacts present on this applicant (drives the animation tell)")]
        public List<string> artifacts = new List<string>();

        private SpriteRenderer _renderer;
        private float _accum;
        private int _frame;

        public bool NeverBlinks { get { return artifacts.Contains("no_blink"); } }
        public int CurrentFrame { get { return _frame; } }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            ApplyFrame(FrameNeutral);
        }

        private void Update()
        {
            if (!animate || frames == null || frames.Length < 4) return;
            _accum += Time.unscaledDeltaTime * fps;
            if (_accum < 1f) return;
            _accum -= 1f;
            _frame = (_frame + 1) % frames.Length;
            ApplyFrame(_frame);
        }

        private void ApplyFrame(int index)
        {
            if (frames == null || frames.Length == 0) return;
            // ARTIFACT: a synthetic with no_blink never reaches frame 3.
            if (NeverBlinks && index == FrameBlink) index = FrameNeutral;
            if (_renderer != null) _renderer.sprite = frames[index];
        }

        /// <summary>Sets which applicant is seated. `archetypeId` is the file suffix, e.g. synthetic_cloned_a.</summary>
        public void SetApplicant(string archetypeId, Sprite[] strip, IEnumerable<string> tells)
        {
            frames = strip;
            artifacts.Clear();
            if (tells != null) artifacts.AddRange(tells);
            ApplyFrame(FrameNeutral);
        }

        /// <summary>The applicant's own pixel space -> canvas pixel space, for hotspot hit-testing.</summary>
        public Rect HotspotToCanvas(Rect spriteLocal)
        {
            var pos = transform.position;
            float ppu = 16f;
            float originX = pos.x * ppu - frames[0].rect.width * 0.5f;
            float originY = pos.y * ppu - frames[0].rect.height * 0.5f;
            return new Rect(originX + spriteLocal.x, originY + spriteLocal.y,
                            spriteLocal.width, spriteLocal.height);
        }
    }
}
