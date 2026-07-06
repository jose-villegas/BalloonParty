using System;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>Poolable effect driven by a <see cref="ParticleSystem" />; completion is detected automatically when it finishes.</summary>
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

        public override void OnSpawned()
        {
            base.OnSpawned();
            // SetActive(true) runs before OnSpawned, so "Play on Awake" would fire with the stale colour.
            _particle?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

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
