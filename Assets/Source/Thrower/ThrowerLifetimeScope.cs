using VContainer;
using VContainer.Unity;

namespace BalloonParty.Thrower
{
    public class ThrowerLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ThrowerView>();
            builder.RegisterEntryPoint<ThrowerController>();
        }
    }
}
