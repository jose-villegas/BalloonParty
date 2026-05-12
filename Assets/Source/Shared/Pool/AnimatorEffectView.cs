using System;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Poolable effect driven by an <see cref="Animator" />. Completion is
    ///     detected when the first animation clip finishes playing (timer-based).
    ///     Attach to prefab roots that use an Animator; pool via
    ///     <see cref="EffectPoolChannel" />.
    /// </summary>
    public class AnimatorEffectView : EffectView
    {
        private Animator _animator;
        private float _animationLength;
        private bool _animationPlaying;
        private float _animationTimer;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                var clips = _animator.runtimeAnimatorController.animationClips;
                _animationLength = clips.Length > 0 ? clips[0].length : 1f;
            }
        }

        private void Update()
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
                return;
            }

            _animationTimer = 0f;
            _animationPlaying = true;
            _animator.Play(0, -1, 0f);
        }
    }
}
