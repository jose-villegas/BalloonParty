using BalloonParty.Game.Danger;
using BalloonParty.UI.Binding;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Danger
{
    /// <summary>
    ///     Child scope for the danger early-warning UI; binds every <see cref="DangerGradientView" /> to
    ///     the parent scope's danger level.
    /// </summary>
    public class DangerUILifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterBoundViews<DangerGradientView, IDangerLevel, float>(this, danger => danger.Level);
        }
    }
}
