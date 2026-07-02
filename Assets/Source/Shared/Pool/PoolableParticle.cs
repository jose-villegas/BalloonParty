using System;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    public class PoolableParticle : MonoBehaviour, IPoolable, IEffect
    {
        private Action _onComplete;
        private ParticleSystem _particle;
        private PoolManager _pool;
        private string _poolKey;
        private Action _selfReturn;

        internal Action ReturnToPool => _selfReturn;

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

        public void OnSpawned()
        {
            // PoolChannel calls SetActive(true) before OnSpawned, so a particle system
            // with "Play on Awake" enabled would fire with the stale colour from the
            // previous use. Stop+clear here so Play() always applies a fresh colour.
            _particle?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void OnDespawned()
        {
            _onComplete = null;
            _particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // The delegate is created once per pooled instance and survives despawns, so repeat
        // plays reuse it instead of allocating a completion closure per call.
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
    }
}
