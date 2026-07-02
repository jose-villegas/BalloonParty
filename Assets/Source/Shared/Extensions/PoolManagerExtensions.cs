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

        // These run on every balloon pop: the key comes from PoolManager's prefab-key cache
        // (Object.name allocates a fresh string per access) and registration is checked
        // explicitly (GetOrRegister's factory closure allocates even when already registered).
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
