using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Pool channel for prefabs that need no VContainer injection.
    /// </summary>
    internal class SimplePoolChannel<TItem> : PoolChannel<TItem>
        where TItem : Component, IPoolable
    {
        private readonly GameObject _prefab;

        public SimplePoolChannel(TItem prefab) : this(prefab.gameObject)
        {
        }

        public SimplePoolChannel(GameObject prefab)
        {
            _prefab = prefab;
        }

        protected override TItem Create()
        {
            var instance = Object.Instantiate(_prefab, Container);
            instance.SetActive(false);
            return instance.GetComponent<TItem>();
        }
    }
}
