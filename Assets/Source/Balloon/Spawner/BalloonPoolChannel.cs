using BalloonParty.Balloon.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using VContainer.Unity;

namespace BalloonParty.Balloon.Spawner
{
    public class BalloonPoolChannel : PoolChannel<BalloonView>
    {
        private readonly LifetimeScope _parentScope;
        private readonly LifetimeScope _prefab;

        public BalloonPoolChannel(LifetimeScope parentScope, LifetimeScope prefab)
        {
            _parentScope = parentScope;
            _prefab = prefab;
        }

        protected override BalloonView Create()
        {
            var childScope = _parentScope.CreateChildFromPrefab(_prefab);
            childScope.transform.SetParent(Container);
            return childScope.GetComponentInChildren<BalloonView>();
        }
    }
}
