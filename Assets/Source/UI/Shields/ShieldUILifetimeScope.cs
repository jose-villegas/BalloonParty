#region

using BalloonParty.Game;
using VContainer;
using VContainer.Unity;

#endregion

namespace BalloonParty.UI.Shields
{
    public class ShieldUILifetimeScope : GameChildLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var labels = GetComponentsInChildren<ShieldCounterLabel>(true);
            builder.RegisterInstance(labels);
            builder.RegisterComponentInHierarchy<ShieldCounterAnimation>();
        }
    }
}
