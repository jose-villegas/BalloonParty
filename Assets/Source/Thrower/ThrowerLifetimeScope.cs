using BalloonParty.Shared.Pool;
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

            // The pivot, not the spawn point: an anchor beside the cannon must not swing with the aim.
            builder.RegisterBuildCallback(resolver => resolver.Resolve<TrailEndpointRegistry>()
                .Register(TrailEndpointKeys.Cannon, new TransformTrailEndpoint(resolver.Resolve<ThrowerView>().transform)));
        }
    }
}
