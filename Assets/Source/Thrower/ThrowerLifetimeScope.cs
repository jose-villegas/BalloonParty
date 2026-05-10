#region

using BalloonParty.Game;
using VContainer;
using VContainer.Unity;

#endregion

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
