using BalloonParty.UI.Telemetry;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.GameOver
{
    public class GameOverLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // No-ops until a MetricLabel is authored onto one of this screen's labels — the whole point
            // of the wave is that adding a stat line is inspector work, not a code change here.
            builder.RegisterMetricLabels(this);
        }
    }
}
