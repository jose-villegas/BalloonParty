using BalloonParty.Game;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Thrower
{
    public class ThrowerLifetimeScope : GameChildLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ThrowerView>();
            builder.RegisterEntryPoint<ThrowerController>();
        }
    }
}
