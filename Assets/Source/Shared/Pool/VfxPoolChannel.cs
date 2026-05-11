using UnityEngine;

namespace BalloonParty.Shared
{
    public class VfxPoolChannel : PoolChannel<PoolableVfx>
    {
        private readonly GameObject _prefab;

        public VfxPoolChannel(GameObject prefab)
        {
            _prefab = prefab;
        }

        protected override PoolableVfx Create()
        {
            var instance = Object.Instantiate(_prefab, Container);

            // Stop any auto-playing particle system
            var particle = instance.GetComponent<ParticleSystem>();
            if (particle != null)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var poolable = instance.GetComponent<PoolableVfx>();
            if (poolable == null)
            {
                poolable = instance.AddComponent<PoolableVfx>();
            }

            instance.SetActive(false);
            return poolable;
        }
    }
}
