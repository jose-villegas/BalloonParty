using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Danger
{
    /// <summary>
    ///     Child scope for the danger early-warning UI, mirroring <c>HealthUILifetimeScope</c>. Gathers
    ///     the <see cref="DangerGradientView"/>(s) under this hierarchy and registers
    ///     <see cref="DangerGradientBinder"/>, which binds them to the parent scope's
    ///     <c>SpaceDanger</c> at <c>Start</c>. Safe when no views are present (empty array).
    /// </summary>
    public class DangerUILifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var views = GetComponentsInChildren<DangerGradientView>(true);
            builder.RegisterInstance(views);
            builder.RegisterEntryPoint<DangerGradientBinder>();
        }
    }
}
