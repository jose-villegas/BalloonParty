using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared
{
    public abstract class PoolChannel<TItem> where TItem : Component, IPoolable
    {
        private readonly Stack<TItem> _available = new();

        public TItem Get()
        {
            var item = _available.Count > 0 ? _available.Pop() : Create();
            item.gameObject.SetActive(true);
            item.OnSpawned();
            return item;
        }

        public void Return(TItem item)
        {
            item.OnDespawned();
            item.gameObject.SetActive(false);
            _available.Push(item);
        }

        protected abstract TItem Create();
    }
}
