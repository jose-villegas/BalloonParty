using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Non-generic marker so we can store any PoolChannel in a typed dictionary.
    /// </summary>
    public interface IPoolChannel
    {
        void SetParent(Transform parent);
    }

    public abstract class PoolChannel<TItem> : IPoolChannel
        where TItem : Component, IPoolable
    {
        private readonly Stack<TItem> _available = new();
        protected Transform Container { get; private set; }

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

            item ??= Create();
            item.gameObject.SetActive(true);
            item.OnSpawned();
            return item;
        }

        public void Return(TItem item)
        {
            if (item == null)
            {
                return;
            }

            item.OnDespawned();
            item.gameObject.SetActive(false);
            if (Container != null)
            {
                item.transform.SetParent(Container, false);
            }

            _available.Push(item);
        }

        /// <summary>
        ///     Returns the most recently returned item without removing it from the pool
        ///     or activating it. Useful for reading state that was preserved on the
        ///     component before it was returned (e.g. rotation snapshots).
        ///     Returns null if no items are available.
        /// </summary>
        public TItem Peek()
        {
            return _available.Count > 0 ? _available.Peek() : null;
        }

        protected abstract TItem Create();
    }
}
