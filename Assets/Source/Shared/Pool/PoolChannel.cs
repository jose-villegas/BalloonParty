using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    /// Non-generic marker so we can store any PoolChannel in a typed dictionary.
    /// </summary>
    public interface IPoolChannel { }

    public abstract class PoolChannel<TItem> : IPoolChannel where TItem : Component, IPoolable
    {
        private readonly Stack<TItem> _available = new();

        public TItem Get()
        {
            TItem item = null;
            while (_available.Count > 0)
            {
                item = _available.Pop();
                if (item != null) break;
                item = null;
            }

            item ??= Create();
            item.gameObject.SetActive(true);
            item.OnSpawned();
            return item;
        }

        public void Return(TItem item)
        {
            if (item == null) return;
            item.OnDespawned();
            item.gameObject.SetActive(false);
            _available.Push(item);
        }

        protected abstract TItem Create();
    }
}
