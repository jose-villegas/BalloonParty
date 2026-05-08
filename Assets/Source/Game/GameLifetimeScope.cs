using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Balloon.Controller;
using BalloonParty.Debug;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;

namespace BalloonParty.Game
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameConfiguration _gameConfiguration;

        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<BalanceBalloonsMessage>(options);

            builder.RegisterInstance<IGameConfiguration>(_gameConfiguration);

            builder.Register<SlotGrid>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<SlotGridView>();
            builder.RegisterComponentInHierarchy<SlotGridController>();

            builder.RegisterEntryPoint<BalloonBalancer>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterComponentOnNewGameObject<BalloonRemoverCheat>(Lifetime.Singleton, "BalloonRemoverCheat")
                .AsImplementedInterfaces()
                .AsSelf();
            builder.RegisterComponentOnNewGameObject<CheatConsoleView>(Lifetime.Singleton, "CheatConsole")
                .AsImplementedInterfaces()
                .AsSelf();
#endif
        }
    }
}