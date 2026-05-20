using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Pool channel that instantiates prefabs and injects <c>[Inject]</c> fields
    ///     from a parent <see cref="IObjectResolver"/> — without creating a VContainer
    ///     child scope per instance. Use for prefabs whose components only need singletons
    ///     from an ancestor scope (no per-instance registrations).
    /// </summary>
    internal class InjectingPoolChannel<TItem> : PoolChannel<TItem>
        where TItem : Component, IPoolable
    {
        private readonly IObjectResolver _resolver;
        private readonly TItem _prefab;

        public InjectingPoolChannel(IObjectResolver resolver, TItem prefab)
        {
            _resolver = resolver;
            _prefab = prefab;
        }

        protected override TItem Create()
        {
            return _resolver.Instantiate(_prefab, Container);
        }
    }
}
