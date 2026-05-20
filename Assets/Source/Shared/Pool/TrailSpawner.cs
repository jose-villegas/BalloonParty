using System;
using BalloonParty.UI.Score;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Lightweight utility that handles the pool lifecycle for trail orbs:
    ///     get from pool, position, setup, return on arrival. Composed by trail
    ///     services that need spawn-and-forget behaviour.
    /// </summary>
    internal class TrailSpawner
    {
        private readonly Func<PoolChannel<FlyingTrail>> _channelFactory;
        private readonly PoolManager _poolManager;
        private readonly string _poolKey;

        internal TrailSpawner(PoolManager poolManager, string poolKey, Func<PoolChannel<FlyingTrail>> channelFactory)
        {
            _poolManager = poolManager;
            _poolKey = poolKey;
            _channelFactory = channelFactory;
        }

        internal Transform Spawn(Vector3 from, Vector3 to, float duration, Color? color = null, Action onArrived = null)
        {
            var trail = _poolManager.GetOrRegister(_poolKey, _channelFactory);

            trail.transform.position = from;
            trail.transform.localScale = Vector3.one;

            if (color.HasValue)
            {
                trail.Setup(to,
                    color.Value,
                    duration,
                    () =>
                    {
                        onArrived?.Invoke();
                        _poolManager.Return(_poolKey, trail);
                    });
            }
            else
            {
                trail.Setup(to,
                    duration,
                    () =>
                    {
                        onArrived?.Invoke();
                        _poolManager.Return(_poolKey, trail);
                    });
            }

            return trail.transform;
        }

        internal Transform SpawnUnscaled(
            Vector3 from,
            Vector3 to,
            float duration,
            Color? color = null,
            Action onArrived = null)
        {
            var trail = _poolManager.GetOrRegister(_poolKey, _channelFactory);

            trail.transform.position = from;
            trail.transform.localScale = Vector3.one;

            if (color.HasValue)
            {
                trail.Setup(to,
                    color.Value,
                    duration,
                    () =>
                    {
                        onArrived?.Invoke();
                        _poolManager.Return(_poolKey, trail);
                    },
                    true);
            }
            else
            {
                trail.Setup(to,
                    duration,
                    () =>
                    {
                        onArrived?.Invoke();
                        _poolManager.Return(_poolKey, trail);
                    },
                    true);
            }

            return trail.transform;
        }
    }
}
