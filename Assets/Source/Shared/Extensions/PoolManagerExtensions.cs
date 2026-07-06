using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class PoolManagerExtensions
    {
        internal static PoolableParticle PlayParticle(
            this PoolManager pool,
            ParticleSystem prefab,
            Vector3 position)
        {
            var effect = GetPooledParticle(pool, prefab);
            effect.Play(position, effect.ReturnToPool);
            return effect;
        }

        internal static PoolableParticle PlayParticle(
            this PoolManager pool,
            ParticleSystem prefab,
            Vector3 position,
            Color tint)
        {
            var effect = GetPooledParticle(pool, prefab);
            effect.Play(position, tint, effect.ReturnToPool);
            return effect;
        }

        internal static PoolableParticle PlayParticle(
            this PoolManager pool,
            ParticleSystem prefab,
            Vector3 position,
            Quaternion rotation,
            Color tint)
        {
            var effect = GetPooledParticle(pool, prefab);
            effect.Play(position, rotation, tint, effect.ReturnToPool);
            return effect;
        }

        internal static EffectView PlayEffect(
            this PoolManager pool,
            EffectView prefab,
            Vector3 position,
            Color tint)
        {
            var effect = GetPooledEffect(pool, prefab);
            effect.Play(position, tint, effect.ReturnToPool);
            return effect;
        }

        internal static EffectView PlayEffect(
            this PoolManager pool,
            EffectView prefab,
            Vector3 position,
            Quaternion rotation,
            Color tint)
        {
            var effect = GetPooledEffect(pool, prefab);
            effect.Play(position, rotation, tint, effect.ReturnToPool);
            return effect;
        }

        // Runs per balloon pop — avoids GetOrRegister's allocating factory closure when already registered.
        private static PoolableParticle GetPooledParticle(PoolManager pool, ParticleSystem prefab)
        {
            var key = pool.KeyFor(prefab);
            if (!pool.IsRegistered(key))
            {
                pool.Register(key, new ParticlePoolChannel(prefab.gameObject));
            }

            var effect = pool.Get<PoolableParticle>(key);
            effect.BindPool(pool, key);
            return effect;
        }

        private static EffectView GetPooledEffect(PoolManager pool, EffectView prefab)
        {
            var key = pool.KeyFor(prefab);
            if (!pool.IsRegistered(key))
            {
                pool.Register(key, new SimplePoolChannel<EffectView>(prefab));
            }

            var effect = pool.Get<EffectView>(key);
            effect.BindPool(pool, key);
            return effect;
        }
    }
}
