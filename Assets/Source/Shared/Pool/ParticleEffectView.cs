using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Poolable effect driven by a <see cref="ParticleSystem" />. Completion is
    ///     detected automatically when the particle system finishes. Attach to prefab
    ///     roots that use a ParticleSystem; pool via <see cref="EffectPoolChannel" />.
    /// </summary>
    public class ParticleEffectView : EffectView
    {
        private ParticleSystem _particle;

        private void Awake()
        {
            _particle = GetComponent<ParticleSystem>();
        }

        private void Update()
        {
            if (OnComplete != null && _particle != null && !_particle.IsAlive())
            {
                InvokeComplete();
            }
        }

        // ── EffectView ────────────────────────────────────────────────────────────

        public override void OnDespawned()
        {
            base.OnDespawned();

            if (_particle != null)
            {
                _particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public override void Play(Vector3 position, Color tint, Action onComplete = null)
        {
            OnComplete = onComplete;
            transform.position = position;

            if (_particle == null)
            {
                return;
            }

            var main = _particle.main;
            main.startColor = tint;
            _particle.Play();
        }
    }
}

