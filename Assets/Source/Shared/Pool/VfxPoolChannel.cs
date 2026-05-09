using UnityEngine;

namespace BalloonParty.Shared
{
    public class VfxPoolChannel : PoolChannel<PoolableParticle>
    {
        private readonly ParticleSystem _prefab;

        public VfxPoolChannel(ParticleSystem prefab)
        {
            _prefab = prefab;
        }

        protected override PoolableParticle Create()
        {
            var instance = Object.Instantiate(_prefab);
            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var poolable = instance.gameObject.AddComponent<PoolableParticle>();
            poolable.Initialize(p => Return(p));

            instance.gameObject.SetActive(false);
            return poolable;
        }
    }
}
