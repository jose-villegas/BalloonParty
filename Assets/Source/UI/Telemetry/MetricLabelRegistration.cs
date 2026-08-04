using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Telemetry;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Telemetry
{
    internal static class MetricLabelRegistration
    {
        /// <summary>
        ///     Gathers every <see cref="MetricLabel" /> under <paramref name="scope" /> — including
        ///     inactive ones, since a popup's labels live on a hidden object until it opens — and binds
        ///     them to <see cref="ILevelMetricsView" />.
        /// </summary>
        public static void RegisterMetricLabels(this IContainerBuilder builder, LifetimeScope scope)
        {
            var labels = scope.GetComponentsInChildren<MetricLabel>(true);
            if (labels.Length == 0)
            {
                return;
            }

            builder.Register(resolver => new MetricValueResolver(resolver.Resolve<IGamePalette>()),
                Lifetime.Singleton);
            builder.RegisterEntryPoint<MetricLabelBinder>().WithParameter(labels);
        }
    }
}
