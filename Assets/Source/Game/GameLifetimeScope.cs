using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Debug;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;

namespace BalloonParty.Game
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameConfiguration _gameConfiguration;
        [SerializeField] private GameObject _balloonPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<BalanceBalloonsMessage>(options);
            builder.RegisterMessageBroker<SpawnBalloonLineMessage>(options);

            builder.RegisterInstance<IGameConfiguration>(_gameConfiguration);
            builder.RegisterInstance(new BalloonSpawnerSettings(_balloonPrefab));

            builder.Register<SlotGrid>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<SlotGridView>();
            builder.RegisterComponentInHierarchy<SlotGridController>();

            builder.RegisterEntryPoint<BalloonBalancer>();
            builder.RegisterEntryPoint<BalloonSpawner>().AsSelf();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.Register<SpawnBalloonLineCheat>(Lifetime.Singleton).AsImplementedInterfaces();
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