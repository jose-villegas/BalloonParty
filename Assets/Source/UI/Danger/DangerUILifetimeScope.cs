using BalloonParty.Game.Danger;
using BalloonParty.UI.Binding;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Danger
{
    /// <summary>
    ///     Child scope for the danger early-warning UI. Binds every <see cref="DangerGradientView" /> under
    ///     this hierarchy to the parent scope's <c>SpaceDanger.Level</c> at <c>Start</c>. Safe when no views
    ///     are present (empty array).
    /// </summary>
    public class DangerUILifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterBoundViews<DangerGradientView, SpaceDanger, float>(this, danger => danger.Level);
        }
    }
}
