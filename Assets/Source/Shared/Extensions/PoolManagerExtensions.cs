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
            var key = prefab.name;
            var effect = pool.GetOrRegister(key, () => new ParticlePoolChannel(prefab.gameObject));
            effect.Play(position, () => pool.Return(key, effect));
            return effect;
        }

        internal static PoolableParticle PlayParticle(
            this PoolManager pool,
            ParticleSystem prefab,
            Vector3 position,
            Color tint)
        {
            var key = prefab.name;
            var effect = pool.GetOrRegister(key, () => new ParticlePoolChannel(prefab.gameObject));
            effect.Play(position, tint, () => pool.Return(key, effect));
            return effect;
        }

        internal static PoolableParticle PlayParticle(
            this PoolManager pool,
            ParticleSystem prefab,
            Vector3 position,
            Quaternion rotation,
            Color tint)
        {
            var key = prefab.name;
            var effect = pool.GetOrRegister(key, () => new ParticlePoolChannel(prefab.gameObject));
            effect.Play(position, rotation, tint, () => pool.Return(key, effect));
            return effect;
        }

        internal static EffectView PlayEffect(
            this PoolManager pool,
            EffectView prefab,
            Vector3 position,
            Color tint)
        {
            var key = prefab.name;
            var effect = pool.GetOrRegister(key, () => new SimplePoolChannel<EffectView>(prefab));
            effect.Play(position, tint, () => pool.Return(key, effect));
            return effect;
        }

        internal static EffectView PlayEffect(
            this PoolManager pool,
            EffectView prefab,
            Vector3 position,
            Quaternion rotation,
            Color tint)
        {
            var key = prefab.name;
            var effect = pool.GetOrRegister(key, () => new SimplePoolChannel<EffectView>(prefab));
            effect.Play(position, rotation, tint, () => pool.Return(key, effect));
            return effect;
        }
    }
}
