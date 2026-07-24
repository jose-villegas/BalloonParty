using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game
{
    [DefaultExecutionOrder(-5001)]
    public class LaunchLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Empty: AppLifetimeScope (the persistent project root) now owns the display config, the ambient
            // time-of-day, and the single shared camera pipeline — all resolved from the parent. The launcher
            // scene has no injected consumers of its own (its begin-screen interactivity is driven by the
            // Game scope's fields and by static hand-offs), so this scope only parents to the app root.
        }
    }
}
