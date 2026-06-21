using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Pool channel for prefabs that need no VContainer injection: instantiates the prefab
    ///     under <see cref="PoolChannel{TItem}.Container" />, deactivates it, and hands back the
    ///     <typeparamref name="TItem" /> component. For prefabs that require injection use
    ///     <see cref="InjectingPoolChannel{TItem}" />.
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
