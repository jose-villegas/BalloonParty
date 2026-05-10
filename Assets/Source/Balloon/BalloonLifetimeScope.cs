#region

using BalloonParty.Balloon.View;
using BalloonParty.Game;
using VContainer;
using VContainer.Unity;

#endregion

namespace BalloonParty.Balloon
{
    public class BalloonLifetimeScope : GameChildLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<BalloonView>();
        }
    }
}
