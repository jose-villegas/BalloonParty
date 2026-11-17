using System.Collections.Generic;
using System.Threading;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Rendering;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Non-generic marker so any PoolChannel can be stored in a typed dictionary.
    /// </summary>
    public interface IPoolChannel
    {
        void SetParent(Transform parent);
        int AvailableCount { get; }

        /// <summary>
        ///     Creates items in a single frame — use only for lightweight items.
        /// </summary>
        void Prewarm(int count);

        /// <summary>
        ///     Creates items one per frame — for heavy prefabs (e.g. VContainer child scopes).
        /// </summary>
        UniTask PrewarmAsync(int count, CancellationToken ct = default);
    }

    public abstract class PoolChannel<TItem> : IPoolChannel
        where TItem : Component, IPoolable
    {
        private readonly Stack<TItem> _available = new();
        protected Transform Container { get; private set; }

        public int AvailableCount => _available.Count;

        public void SetParent(Transform parent)
        {
            Container = parent;
        }

        public TItem Get()
        {
            TItem item = null;
            while (_available.Count > 0)
            {
                item = _available.Pop();
                if (item != null)
                {
                    break;
                }

                item = null;
            }

            item ??= CreateTracked();
            item.gameObject.SetActive(true);
            item.OnSpawned();
            return item;
        }

        public void Return(TItem item)
        {
            if (item == null)
            {
                Log.Warn("PoolChannel",
                    $"<{typeof(TItem).Name}>.Return: attempted to return a null item " +
                    "— possible double-return.");
                return;
            }

            item.OnDespawned();
            item.gameObject.SetActive(false);
            if (Container != null && item.transform.parent != Container)
            {
                item.transform.SetParent(Container, false);
            }

            _available.Push(item);
        }

        /// <summary>
        ///     Returns the most recently returned item without removing or activating it.
        /// </summary>
        public TItem Peek()
        {
            return _available.Count > 0 ? _available.Peek() : null;
        }

        public void Prewarm(int count)
        {
            for (var i = 0; i < count; i++)
            {
                PushWarm(CreateTracked());
            }
        }

        public async UniTask PrewarmAsync(int count, CancellationToken ct = default)
        {
            for (var i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                PushWarm(CreateTracked());
                await UniTask.Yield(ct);
            }
        }

        protected abstract TItem Create();

        // Every pooled object is tracked by its renderers' layers from birth, so an effects map can tell when its
        // layer is empty (LayerPresence).
        private TItem CreateTracked()
        {
            var item = Create();
            LayerPresence.Track(item.gameObject);
            return item;
        }

        private void PushWarm(TItem item)
        {
            item.gameObject.SetActive(false);
            if (Container != null && item.transform.parent != Container)
            {
                item.transform.SetParent(Container, false);
            }

            _available.Push(item);
        }
    }
}
