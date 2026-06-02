using BalloonParty.Configuration;
using BalloonParty.Display;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game
{
    [DefaultExecutionOrder(-5001)]
    public class LaunchLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameDisplayConfiguration _displayConfiguration;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance<IGameDisplayConfiguration>(_displayConfiguration);
            builder.RegisterComponentInHierarchy<OrthogonalSizeCameraController>();
        }
    }
}
