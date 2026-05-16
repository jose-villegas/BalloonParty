using BalloonParty.Projectile.View;
using BalloonParty.Shared.Pool;
using VContainer;

namespace BalloonParty.Projectile
{
    internal class ProjectilePoolChannel : InjectingPoolChannel<ProjectileView>
    {
        public ProjectilePoolChannel(IObjectResolver resolver, ProjectileView prefab)
            : base(resolver, prefab)
        {
        }
    }
}
