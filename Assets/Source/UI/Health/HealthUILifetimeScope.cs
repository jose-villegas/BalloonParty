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
    ///     Child scope for the health UI; binds <see cref="HealthCounterLabel" /> to live HP and drives
    ///     <see cref="HeartTrailController"/>.
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
