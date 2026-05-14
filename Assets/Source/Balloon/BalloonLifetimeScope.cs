using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Game;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon
{
    public class BalloonLifetimeScope : GameChildLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<BalloonView>();
            builder.RegisterComponentInHierarchy<IBalloonVariant>();
        }
    }
}
