using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Health
{
    /// <summary>
    ///     Child scope for the health UI, mirroring <c>ShieldUILifetimeScope</c>. Gathers the
    ///     <see cref="HealthCounterLabel"/>(s) under this hierarchy and registers
    ///     <see cref="HealthLabelBinder"/>, which binds them to the parent scope's
    ///     <c>PlayerHealthController</c> at <c>Start</c>.
    /// </summary>
    public class HealthUILifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var labels = GetComponentsInChildren<HealthCounterLabel>(true);
            builder.RegisterInstance(labels);
            builder.RegisterEntryPoint<HealthLabelBinder>();
        }
    }
}
