#region

using BalloonParty.Game;
using BalloonParty.Projectile.View;
using VContainer;
using VContainer.Unity;

#endregion

namespace BalloonParty.Projectile
{
    public class ProjectileLifetimeScope : GameChildLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ProjectileView>();
            builder.RegisterComponentInHierarchy<ProjectileShieldView>();
        }
    }
}
