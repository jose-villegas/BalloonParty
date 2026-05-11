using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Poolable VFX that auto-detects whether the prefab uses a ParticleSystem or an Animator
    ///     and handles playback + completion for both. Attach to a VFX prefab root.
    /// </summary>
    public class PoolableVfx : MonoBehaviour, IPoolable
    {
        private Animator _animator;
        private bool _isParticle;
        private Action _onComplete;
        private ParticleSystem _particle;
        private float _animationLength;
        private float _animationTimer;
        private bool _animationPlaying;

        private void Awake()
        {
            _particle = GetComponent<ParticleSystem>();
            _animator = GetComponent<Animator>();
            _isParticle = _particle != null;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                var clips = _animator.runtimeAnimatorController.animationClips;
                _animationLength = clips.Length > 0 ? clips[0].length : 1f;
            }
        }

        private void Update()
        {
            if (_onComplete == null)
            {
                return;
            }

            if (_isParticle)
            {
                if (!_particle.IsAlive())
                {
                    InvokeComplete();
                }
            }
            else if (_animationPlaying)
            {
                _animationTimer += Time.deltaTime;
                if (_animationTimer >= _animationLength)
                {
                    _animationPlaying = false;
                    InvokeComplete();
                }
            }
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _onComplete = null;
            _animationPlaying = false;
            _animationTimer = 0f;

            if (_isParticle && _particle != null)
            {
                _particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void Play(Vector3 position, Color color, Action onComplete)
        {
            _onComplete = onComplete;
            transform.position = position;

            if (_isParticle)
            {
                var main = _particle.main;
                main.startColor = color;
                _particle.Play();
            }
            else if (_animator != null)
            {
                _animationTimer = 0f;
                _animationPlaying = true;
                _animator.Play(0, -1, 0f);
            }
        }

        public void Play(Vector3 position, Quaternion rotation, Color color, Action onComplete)
        {
            transform.rotation = rotation;
            Play(position, color, onComplete);
        }

        private void InvokeComplete()
        {
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }
    }
}
