using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Debug;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using BalloonParty.Thrower;

namespace BalloonParty.Game
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameConfiguration _gameConfiguration;
        [SerializeField] private GameObject _balloonPrefab;
        [SerializeField] private GameObject _projectilePrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<BalanceBalloonsMessage>(options);
            builder.RegisterMessageBroker<SpawnBalloonLineMessage>(options);
            builder.RegisterMessageBroker<ProjectileDestroyedMessage>(options);
            builder.RegisterMessageBroker<BalloonHitMessage>(options);
            builder.RegisterMessageBroker<BalloonScoredMessage>(options);
            builder.RegisterMessageBroker<ScoreLevelUpMessage>(options);
            builder.RegisterMessageBroker<ProjectileLoadedMessage>(options);

            builder.RegisterInstance<IGameConfiguration>(_gameConfiguration);
            builder.RegisterInstance(new BalloonSpawnerSettings(_balloonPrefab));
            builder.RegisterInstance(new ThrowerSettings(_projectilePrefab));

            builder.Register<SlotGrid>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<SlotGridView>();
            builder.RegisterComponentInHierarchy<SlotGridController>();

            builder.RegisterEntryPoint<BalloonBalancer>();
            builder.RegisterEntryPoint<BalloonSpawner>().AsSelf();
            builder.RegisterEntryPoint<ScoreController>().AsSelf();

            builder.RegisterComponentInHierarchy<ThrowerController>().AsImplementedInterfaces().AsSelf();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.Register<SpawnBalloonLineCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<FireProjectileCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentOnNewGameObject<BalloonRemoverCheat>(Lifetime.Singleton, "BalloonRemoverCheat")
                .AsImplementedInterfaces()
                .AsSelf();
            builder.RegisterComponentOnNewGameObject<CheatConsoleView>(Lifetime.Singleton, "CheatConsole")
                .AsImplementedInterfaces();
#endif
        }
    }
}
