using BalloonParty.Shared.GameState;
using BalloonParty.UI.Telemetry;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.LevelUp
{
    public class LevelUpLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // No-ops until a MetricLabel is authored onto one of this popup's labels — the whole point
            // of the wave is that adding a stat line is inspector work, not a code change here.
            builder.RegisterMetricLabels(this);

            builder.RegisterComponentInHierarchy<LevelUpPopUp>();
            // Registered by concrete type (not .As<IReadyGate>()) so LevelUpPopUp names the exact gate it
            // waits on — and can't silently fall back to the parent scope's NavigationReadyGate(Game).
            builder.Register<CinematicEndGate>(Lifetime.Singleton)
                .WithParameter(CinematicState.LevelCompleteHit);
        }
    }
}
