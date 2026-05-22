using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.LevelUp
{
    public class LevelUpLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<LevelUpPopUp>();
            builder.RegisterInstance<IReadyGate>(new CinematicEndGate());
        }
    }
}
