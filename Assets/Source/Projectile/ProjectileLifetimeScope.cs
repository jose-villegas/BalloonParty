using BalloonParty.Projectile.View;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Projectile
{
    public class ProjectileLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ProjectileView>();
            builder.RegisterComponentInHierarchy<ProjectileShieldView>();
        }
    }
}
