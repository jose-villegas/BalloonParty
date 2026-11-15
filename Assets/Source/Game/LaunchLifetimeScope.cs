using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game
{
    [DefaultExecutionOrder(-5001)]
    public class LaunchLifetimeScope : LifetimeScope
    {
        [Tooltip("The Continue button beside Play, offered while a run is saved. Optional: with none, the launch " +
            "screen offers Play alone.")]
        [SerializeField] private LaunchContinueView _continueView;

        // AppLifetimeScope (the persistent project root) owns the display config, the ambient time-of-day, the
        // single shared camera pipeline and the run save, all resolved from the parent. The begin-screen's live
        // clouds are driven by the app scope's fields and by static hand-offs; Continue is the one launch control
        // with injected logic of its own.
        protected override void Configure(IContainerBuilder builder)
        {
            if (_continueView == null)
            {
                return;
            }

            builder.RegisterComponent(_continueView);
            builder.RegisterComponentInHierarchy<LaunchPlayTrigger>();
            builder.RegisterEntryPoint<LaunchContinueController>();
        }
    }
}
