using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    public class PoolableParticle : MonoBehaviour, IPoolable, IEffect
    {
        private Action _onComplete;
        private ParticleSystem _particle;

        private void Awake()
        {
            _particle = GetComponent<ParticleSystem>();
        }

        private void Update()
        {
            if (_onComplete != null && _particle != null && !_particle.IsAlive())
            {
                var callback = _onComplete;
                _onComplete = null;
                callback.Invoke();
            }
        }

        // ── IPoolable ─────────────────────────────────────────────────────────────

        public void OnSpawned() { }

        public void OnDespawned()
        {
            _onComplete = null;
            _particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ── IEffect ───────────────────────────────────────────────────────────────

        public void Play(Vector3 position, Color tint, Action onComplete = null)
        {
            _onComplete = onComplete;
            transform.position = position;
            var main = _particle.main;
            main.startColor = tint;
            _particle.Play();
        }

        public void Play(Vector3 position, Quaternion rotation, Color tint, Action onComplete = null)
        {
            transform.rotation = rotation;
            Play(position, tint, onComplete);
        }

        public void Stop()
        {
            OnDespawned();
        }
    }
}
