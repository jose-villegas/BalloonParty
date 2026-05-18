using System;
using BalloonParty.UI.Score;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Shields
{
    public class ShieldUILifetimeScope : LifetimeScope
    {
        [SerializeField] private FlyingTrail _trailPrefab;
        [SerializeField] private Transform _trailTarget;

        protected override void Configure(IContainerBuilder builder)
        {
            var labels = GetComponentsInChildren<ShieldCounterLabel>(true);
            builder.RegisterInstance(labels);
            builder.RegisterComponentInHierarchy<ShieldCounterAnimation>();

            builder.RegisterInstance(_trailPrefab);
            builder.RegisterInstance<Func<Vector3>>(() => _trailTarget.position);
            builder.RegisterEntryPoint<ShieldTrailController>();
        }
    }
}
