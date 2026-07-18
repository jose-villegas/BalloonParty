using System;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>Poolable effect driven by a <see cref="ParticleSystem" />; completion is detected automatically when it finishes.</summary>
    public class ParticleEffectView : EffectView
    {
        private bool _awaitingChildren;
        private ParticleSystem _particle;

        public override float Duration => _particle != null ? _particle.main.duration : 0f;

        private void Awake()
        {
            _particle = GetComponent<ParticleSystem>();
            if (_particle != null)
            {
                var main = _particle.main;
                main.stopAction = ParticleSystemStopAction.Callback;
            }
        }

        private void Update()
        {
            // Fallback tail only: a child system (sub-emitter) can still be alive after the root
            // system's own OnParticleSystemStopped already fired.
            if (_awaitingChildren && _particle != null && !_particle.IsAlive(true))
            {
                _awaitingChildren = false;
                InvokeComplete();
            }
        }

        // Unity only sends this to a MonoBehaviour on the same GameObject as the ParticleSystem.
        private void OnParticleSystemStopped()
        {
            if (OnComplete == null)
            {
                return;
            }

            if (_particle.IsAlive(true))
            {
                // Root stopped but a child system (sub-emitter) is still simulating — wait for it.
                _awaitingChildren = true;
                return;
            }

            InvokeComplete();
        }

        public override void OnSpawned()
        {
            base.OnSpawned();
            _awaitingChildren = false;
            // SetActive(true) runs before OnSpawned, so "Play on Awake" would fire with the stale colour.
            _particle?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            _awaitingChildren = false;

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
                // No ParticleSystem to drive completion — fire it now so the pooled instance still returns.
                InvokeComplete();
                return;
            }

            var main = _particle.main;
            main.startColor = tint;
            _particle.Play();
        }
    }
}
