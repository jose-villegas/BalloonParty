using BalloonParty.Game.Health;
using BalloonParty.UI.Binding;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Health
{
    /// <summary>
    ///     Child scope for the health UI. Binds every <see cref="HealthCounterLabel" /> under this
    ///     hierarchy to the parent scope's live HP (<c>PlayerHealthController.Current</c>) at <c>Start</c>.
    /// </summary>
    public class HealthUILifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterBoundViews<HealthCounterLabel, IPlayerHealth, int>(this, health => health.Current);
        }
    }
}
