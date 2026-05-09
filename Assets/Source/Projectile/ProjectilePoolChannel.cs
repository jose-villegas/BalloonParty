using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using VContainer.Unity;

namespace BalloonParty.Projectile
{
    public class ProjectilePoolChannel : PoolChannel<ProjectileView>
    {
        private readonly LifetimeScope _parentScope;
        private readonly ProjectileLifetimeScope _prefab;

        public ProjectilePoolChannel(LifetimeScope parentScope, ProjectileLifetimeScope prefab)
        {
            _parentScope = parentScope;
            _prefab = prefab;
        }

        protected override ProjectileView Create()
        {
            var childScope = _parentScope.CreateChildFromPrefab(_prefab);
            childScope.transform.SetParent(null);
            return childScope.GetComponentInChildren<ProjectileView>();
        }
    }
}
