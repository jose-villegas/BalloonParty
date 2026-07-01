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
        private readonly int _sortingOrder;

        internal TrailSpawner(
            PoolManager poolManager,
            string poolKey,
            Func<PoolChannel<FlyingTrail>> channelFactory,
            int sortingOrder = -1)
        {
            _poolManager = poolManager;
            _poolKey = poolKey;
            _channelFactory = channelFactory;
            _sortingOrder = sortingOrder;
        }

        // Convenience for the common case: pool the prefab through a SimplePoolChannel (no injection).
        internal TrailSpawner(PoolManager poolManager, string poolKey, FlyingTrail prefab, int sortingOrder = -1)
            : this(poolManager, poolKey, () => new SimplePoolChannel<FlyingTrail>(prefab), sortingOrder)
        {
        }

        internal Transform SpawnBurst(
            Vector3 center,
            Vector3 burstTo,
            Vector3 to,
            float burstDuration,
            float traceDuration,
            Color color,
            Action onArrived = null,
            bool useUnscaledTime = false)
        {
            var trail = _poolManager.GetOrRegister(_poolKey, _channelFactory);
            trail.transform.position = center;
            trail.transform.localScale = Vector3.one;
            ApplySortingOrder(trail);
            trail.SetupBurst(burstTo,
                to,
                color,
                burstDuration,
                traceDuration,
                () =>
                {
                    onArrived?.Invoke();
                    _poolManager.Return(_poolKey, trail);
                },
                useUnscaledTime);
            return trail.transform;
        }

        internal Transform Spawn(
            Vector3 from,
            Vector3 to,
            float duration,
            Color? color = null,
            Action onArrived = null,
            bool useUnscaledTime = false)
        {
            var trail = _poolManager.GetOrRegister(_poolKey, _channelFactory);

            trail.transform.position = from;
            trail.transform.localScale = Vector3.one;
            ApplySortingOrder(trail);

            void OnArrived()
            {
                onArrived?.Invoke();
                _poolManager.Return(_poolKey, trail);
            }

            if (color.HasValue)
            {
                trail.Setup(to, color.Value, duration, OnArrived, useUnscaledTime);
            }
            else
            {
                trail.Setup(to, duration, OnArrived, useUnscaledTime);
            }

            return trail.transform;
        }

        private void ApplySortingOrder(FlyingTrail trail)
        {
            if (_sortingOrder >= 0)
            {
                trail.SetSortingOrder(_sortingOrder);
            }
        }
    }
}
