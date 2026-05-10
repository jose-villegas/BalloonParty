#region

using System;
using UnityEngine;

#endregion

namespace BalloonParty.Shared
{
    public class PoolableParticle : MonoBehaviour, IPoolable
    {
        private ParticleSystem _particle;
        private Action<PoolableParticle> _returnToPool;

        private void Update()
        {
            if (_particle != null && !_particle.IsAlive())
            {
                _returnToPool?.Invoke(this);
            }
        }

        public void OnSpawned() { }

        public void OnDespawned()
        {
            _particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Initialize(Action<PoolableParticle> returnToPool)
        {
            _particle = GetComponent<ParticleSystem>();
            _returnToPool = returnToPool;
        }

        public void Play(Vector3 position, Color color)
        {
            transform.position = position;
            var main = _particle.main;
            main.startColor = color;
            _particle.Play();
        }
    }
}
