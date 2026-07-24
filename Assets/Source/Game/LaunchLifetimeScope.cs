using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Display;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game
{
    [DefaultExecutionOrder(-5001)]
    public class LaunchLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameDisplayConfiguration _displayConfiguration;
        [SerializeField] private SceneLightFieldSettings _sceneLightFieldSettings;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance<IGameDisplayConfiguration>(_displayConfiguration);
            builder.RegisterComponentInHierarchy<OrthogonalSizeCameraController>();
            builder.RegisterComponentInHierarchy<SceneCaptureService>();

            // Push the ambient scene-light globals in the launcher too, so its backdrop shows the same
            // time-of-day tint as gameplay instead of the neutral no-owner fallback. No light field or GI
            // here — with no SceneLightFieldService, _SceneLightFieldOn stays 0 and consumers use the flat
            // ambient path, which is all the launcher needs.
            builder.RegisterInstance<ISceneLightSettings>(_sceneLightFieldSettings);
            builder.RegisterInstance<ITimeOfDaySettings>(_sceneLightFieldSettings);

            // RegisterEntryPoint (not plain Register): the launcher scope has no other entry points, so
            // without this the IStartable/ITickable dispatcher is never set up and Start would never run.
            // No TimeOfDayCycle here — the launcher has no levels, so it shows level 1 (the authored
            // direction), which is exactly what the game opens on.
            builder.RegisterEntryPoint<TimeOfDayService>();
        }
    }
}
