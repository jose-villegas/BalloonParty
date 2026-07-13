using System;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>Poolable effect driven by an <see cref="Animator" />; completion is timer-based off the first clip's length.</summary>
    public class AnimatorEffectView : EffectView
    {
        private float _animationLength;
        private bool _animationPlaying;
        private float _animationTimer;
        private Animator _animator;

        /// <summary>0..1 through the current play, for subclasses driving time-based visuals (e.g. colour cycling).</summary>
        protected float AnimationProgress => _animationLength > 0f ? Mathf.Clamp01(_animationTimer / _animationLength) : 0f;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                var clips = _animator.runtimeAnimatorController.animationClips;
                _animationLength = clips.Length > 0 ? clips[0].length : 1f;
            }
        }

        protected virtual void Update()
        {
            if (OnComplete == null || !_animationPlaying)
            {
                return;
            }

            _animationTimer += Time.deltaTime;
            if (_animationTimer >= _animationLength)
            {
                _animationPlaying = false;
                InvokeComplete();
            }
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            _animationPlaying = false;
            _animationTimer = 0f;
        }

        public override void Play(Vector3 position, Color tint, Action onComplete = null)
        {
            OnComplete = onComplete;
            transform.position = position;

            if (_animator == null)
            {
                // No Animator to time completion — fire it now so the pooled instance still returns.
                InvokeComplete();
                return;
            }

            _animationTimer = 0f;
            _animationPlaying = true;
            _animator.Play(0, -1, 0f);
        }
    }
}
