using BalloonParty.Game;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.LevelUp
{
    public class LevelUpLifetimeScope : GameChildLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<LevelUpPopUp>();
        }
    }
}