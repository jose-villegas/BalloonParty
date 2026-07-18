using System;
using System.Threading;
using BalloonParty.UI.Score;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Handles the pool lifecycle for trail orbs: get, position, setup, return on arrival.
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

        // Convenience: pools the prefab through a SimplePoolChannel (no injection).
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
            bool useUnscaledTime = false,
            TrailMotion motion = TrailMotion.Default)
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
                useUnscaledTime,
                motion);
            return trail.transform;
        }

        // Homes on a live-updating target instead of flying to a fixed point.
        internal Transform SpawnFollow(
            Vector3 from,
            Func<Vector3> targetProvider,
            float duration,
            Action onArrived = null,
            bool useUnscaledTime = false,
            TrailMotion motion = TrailMotion.Default)
        {
            var trail = _poolManager.GetOrRegister(_poolKey, _channelFactory);
            trail.transform.position = from;
            trail.transform.localScale = Vector3.one;
            ApplySortingOrder(trail);
            trail.SetupFollow(targetProvider,
                duration,
                () =>
                {
                    onArrived?.Invoke();
                    _poolManager.Return(_poolKey, trail);
                },
                useUnscaledTime,
                motion);
            return trail.transform;
        }

        internal Transform Spawn(
            Vector3 from,
            Vector3 to,
            float duration,
            Color? color = null,
            Action onArrived = null,
            bool useUnscaledTime = false,
            TrailMotion motion = TrailMotion.Default)
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
                trail.Setup(to, color.Value, duration, OnArrived, useUnscaledTime, motion);
            }
            else
            {
                trail.Setup(to, duration, OnArrived, useUnscaledTime, motion);
            }

            return trail.transform;
        }

        // Raw pool acquire for callers that drive the trail's motion themselves (the shape-formation ticker)
        // instead of through a Spawn* flight. Colour is applied here exactly as Spawn does; the consumer that
        // Acquires MUST Release. The ribbon is left as authored — the caller clears/retimes it as needed.
        internal FlyingTrail Acquire(Color color)
        {
            var trail = _poolManager.GetOrRegister(_poolKey, _channelFactory);
            trail.transform.localScale = Vector3.one;
            ApplySortingOrder(trail);
            trail.SetColor(color);
            return trail;
        }

        internal void Release(FlyingTrail trail)
        {
            _poolManager.Return(_poolKey, trail);
        }

        // GetOrRegister would hand out (and leak) one instance, so register separately before topping up.
        internal UniTask PrewarmAsync(int count, CancellationToken ct = default)
        {
            if (!_poolManager.IsRegistered(_poolKey))
            {
                _poolManager.Register(_poolKey, _channelFactory());
            }

            return _poolManager.PrewarmAsync(_poolKey, count, ct);
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
