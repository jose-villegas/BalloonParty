using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.LevelUp
{
    public class LevelUpLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<LevelUpPopUp>();
        }
    }
}
