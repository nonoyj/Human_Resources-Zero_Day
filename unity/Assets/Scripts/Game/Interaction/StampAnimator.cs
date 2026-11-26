// HR//0D -- APPROVE / REJECT stamp feedback.
// Uses an Update state machine instead of a coroutine: nothing to leak across scene loads, and
// reduce-motion skips the flourish without losing the feedback (docs/08-behaviour-model.md).
// Owner: Agent (interaction layer).
using System;
using UnityEngine;
using HRZeroDay.Core.Data;   // Decision enum lives in Core, not in this namespace

namespace HRZeroDay.Game.Interaction
{
    public sealed class StampAnimator : MonoBehaviour
    {
        public Transform stampTransform;
        public SpriteRenderer stampRenderer;
        public Sprite approveSprite;
        public Sprite rejectSprite;
        [Range(0.05f, 1f)] public float duration = 0.28f;
        public bool reduceMotion;

        private bool _playing;
        private float _elapsed;
        private Action _onComplete;

        public bool IsPlaying { get { return _playing; } }

        public void Play(Decision decision, Action onComplete)
        {
            if (stampTransform == null) { if (onComplete != null) onComplete(); return; }
            if (stampRenderer != null)
                stampRenderer.sprite = decision == Decision.Approve ? approveSprite : rejectSprite;
            _onComplete = onComplete;
            _elapsed = 0f;
            _playing = true;
            stampTransform.gameObject.SetActive(true);
            if (reduceMotion) Finish();
        }

        public void Cancel()
        {
            _playing = false;
            _onComplete = null;
            if (stampTransform != null) stampTransform.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(_elapsed / duration) : 1f;
            float pop = reduceMotion ? 1f : 1f + 0.35f * (1f - t) * (1f - t);
            stampTransform.localScale = new Vector3(pop, pop, 1f);
            stampTransform.localRotation = Quaternion.Euler(0f, 0f, reduceMotion ? 0f : -8f * (1f - t));
            if (t >= 1f) Finish();
        }

        private void Finish()
        {
            _playing = false;
            if (stampTransform != null)
            {
                stampTransform.localScale = Vector3.one;
                stampTransform.localRotation = Quaternion.identity;
            }
            var done = _onComplete;
            _onComplete = null;
            if (done != null) done();
        }

        private void OnDisable()
        {
            // Standing rule 5: nothing may keep animating after the screen is disabled.
            _playing = false;
            _onComplete = null;
        }
    }
}
