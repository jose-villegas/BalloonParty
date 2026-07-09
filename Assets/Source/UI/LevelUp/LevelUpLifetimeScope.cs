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
            // Registered by concrete type (not .As<IReadyGate>()) so LevelUpPopUp names the exact gate it
            // waits on — and can't silently fall back to the parent scope's NavigationReadyGate(Game).
            builder.Register<CinematicEndGate>(Lifetime.Singleton)
                .WithParameter(CinematicState.LevelUpPanIn);
        }
    }
}
