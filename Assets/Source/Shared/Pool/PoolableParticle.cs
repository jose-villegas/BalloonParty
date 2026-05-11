using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    public class PoolableParticle : MonoBehaviour, IPoolable
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

        public void OnSpawned() { }

        public void OnDespawned()
        {
            _onComplete = null;
            _particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Play(Vector3 position, Color color, Action onComplete)
        {
            _onComplete = onComplete;
            transform.position = position;
            var main = _particle.main;
            main.startColor = color;
            _particle.Play();
        }
    }
}
