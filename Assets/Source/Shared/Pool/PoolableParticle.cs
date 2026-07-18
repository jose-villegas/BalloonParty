using System;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    public class PoolableParticle : MonoBehaviour, IPoolable, IEffect
    {
        private bool _awaitingChildren;
        private Action _onComplete;
        private ParticleSystem _particle;
        private PoolManager _pool;
        private string _poolKey;
        private Action _selfReturn;

        internal Action ReturnToPool => _selfReturn;

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
            // Fallback tail only: a child sub-emitter (e.g. tough-balloon smoke) can still be
            // alive after the root system's own OnParticleSystemStopped already fired.
            if (_awaitingChildren && _particle != null && !_particle.IsAlive(true))
            {
                _awaitingChildren = false;
                Complete();
            }
        }

        // Unity only sends this to a MonoBehaviour on the same GameObject as the ParticleSystem.
        private void OnParticleSystemStopped()
        {
            if (_onComplete == null)
            {
                return;
            }

            if (_particle.IsAlive(true))
            {
                // Root stopped but a child system (sub-emitter) is still simulating — wait for it.
                _awaitingChildren = true;
                return;
            }

            Complete();
        }

        public void OnSpawned()
        {
            _awaitingChildren = false;

            // Stop+clear so "Play on Awake" doesn't fire with a stale colour from last use.
            _particle?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void OnDespawned()
        {
            _onComplete = null;
            _awaitingChildren = false;
            _particle?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Cached once per instance so repeat plays don't allocate a new closure.
        internal void BindPool(PoolManager pool, string key)
        {
            _pool = pool;
            _poolKey = key;
            _selfReturn ??= () => _pool.Return(_poolKey, this);
        }

        public void Play(Vector3 position, Action onComplete = null)
        {
            _onComplete = onComplete;
            transform.position = position;
            _particle.Play();
        }

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

        private void Complete()
        {
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }
    }
}
