using BalloonParty.Game.Health;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Binding;
using BalloonParty.UI.Score;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Health
{
    /// <summary>
    ///     Child scope for the health UI. Binds every <see cref="HealthCounterLabel" /> under this
    ///     hierarchy to the parent scope's live HP (<c>PlayerHealthController.Current</c>) at <c>Start</c>,
    ///     and drives <see cref="HeartTrailController"/> — a heart trail flying from the bar to each
    ///     overflow pop. Wire <c>_heartTrailPrefab</c> (a heart <c>FlyingTrail</c>) and a world-space
    ///     <c>_heartTrailSource</c> (the bar) in the inspector; the source is published as the
    ///     <c>Heart</c> trail endpoint the controller flies from.
    /// </summary>
    public class HealthUILifetimeScope : LifetimeScope
    {
        [SerializeField] private FlyingTrail _heartTrailPrefab;
        [SerializeField] private Transform _heartTrailSource;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterBoundViews<HealthCounterLabel, IPlayerHealth, int>(this, health => health.Current);

            builder.RegisterInstance(_heartTrailPrefab);
            builder.RegisterEntryPoint<HeartTrailController>();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<TrailEndpointRegistry>()
                .Register(TrailEndpointKeys.Heart, new TransformTrailEndpoint(_heartTrailSource)));
        }
    }
}
