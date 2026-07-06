using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Pool channel for effects driven exclusively by a <see cref="ParticleSystem" />.
    /// </summary>
    internal class ParticlePoolChannel : PoolChannel<PoolableParticle>
    {
        private readonly GameObject _prefab;

        public ParticlePoolChannel(GameObject prefab)
        {
            _prefab = prefab;
        }

        protected override PoolableParticle Create()
        {
            var instance = Object.Instantiate(_prefab, Container);

            // Suppress the prefab's auto-play
            var ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var poolable = instance.GetComponent<PoolableParticle>();
            if (poolable == null)
            {
                poolable = instance.AddComponent<PoolableParticle>();
            }

            instance.SetActive(false);
            return poolable;
        }
    }
}
