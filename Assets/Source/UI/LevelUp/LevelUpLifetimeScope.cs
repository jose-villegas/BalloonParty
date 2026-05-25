using BalloonParty.Shared;
using BalloonParty.Shared.Pause;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.LevelUp
{
    public class LevelUpLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<LevelUpPopUp>();
            builder.Register<PauseResumedGate>(Lifetime.Singleton)
                .WithParameter(PauseSource.Cinematic)
                .As<IReadyGate>();
        }
    }
}
