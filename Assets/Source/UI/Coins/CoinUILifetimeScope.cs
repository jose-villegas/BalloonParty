using BalloonParty.Game.Coins;
using BalloonParty.UI.Binding;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Coins
{
    /// <summary>Child scope for the coin counter: binds the balance label and injects the counter's view.</summary>
    public class CoinUILifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterBoundViews<CoinCounterLabel, ICoinBalance, int>(this, balance => balance.Balance);
            builder.RegisterComponentInHierarchy<CoinCounterView>();
        }
    }
}
