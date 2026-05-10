#region

using UnityEngine;

#endregion

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
            var instance = Object.Instantiate(_prefab, Container);
            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var poolable = instance.gameObject.AddComponent<PoolableParticle>();

            instance.gameObject.SetActive(false);
            return poolable;
        }
    }
}
