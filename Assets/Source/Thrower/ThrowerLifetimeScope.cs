using VContainer;
using VContainer.Unity;

namespace BalloonParty.Thrower
{
    public class ThrowerLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ThrowerView>();
            // AsSelf so editor tooling (Shot Solver) can resolve the controller and force a shot.
            builder.RegisterEntryPoint<ThrowerController>().AsSelf();
        }
    }
}
