#region

using BalloonParty.Balloon;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Cheats;
using BalloonParty.Configuration;
using BalloonParty.Display;
using BalloonParty.Item;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using BalloonParty.Thrower;
using BalloonParty.UI.GameStart;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

#endregion

namespace BalloonParty.Game
{
    [DefaultExecutionOrder(-5001)]
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameConfiguration _gameConfiguration;
        [SerializeField] private BalloonLifetimeScope _balloonScopePrefab;
        [SerializeField] private ProjectileLifetimeScope _projectileScopePrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<BalanceBalloonsMessage>(options);
            builder.RegisterMessageBroker<SpawnBalloonLineMessage>(options);
            builder.RegisterMessageBroker<ProjectileDestroyedMessage>(options);
            builder.RegisterMessageBroker<BalloonHitMessage>(options);
            builder.RegisterMessageBroker<BalloonNudgeMessage>(options);
            builder.RegisterMessageBroker<BalloonScoredMessage>(options);
            builder.RegisterMessageBroker<ScoreLevelUpMessage>(options);
            builder.RegisterMessageBroker<ProjectileLoadedMessage>(options);
            builder.RegisterMessageBroker<ItemCheckMessage>(options);

            builder.RegisterInstance<IGameConfiguration>(_gameConfiguration);
            builder.RegisterInstance(new BalloonSpawnerSettings(_balloonScopePrefab));
            builder.RegisterInstance(new ThrowerSettings(_projectileScopePrefab));

            builder.Register<SlotGrid>(Lifetime.Singleton);
            builder.Register<PoolManager>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<SlotGridView>();

            builder.RegisterEntryPoint<BalloonBalancer>();
            builder.RegisterEntryPoint<BalloonNudgeHandler>();
            builder.RegisterEntryPoint<BalloonSpawner>().AsSelf();
            builder.RegisterEntryPoint<ScoreController>().AsSelf();
            builder.RegisterEntryPoint<ItemAssigner>();

            builder.RegisterComponentInHierarchy<GameStartButton>();

            builder.RegisterEntryPoint<OrthogonalSizeCameraController>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.Register<SpawnBalloonLineCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<FireProjectileCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<TriggerLevelUpCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<NearLevelUpCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentOnNewGameObject<BalloonRemoverCheat>(Lifetime.Singleton, "BalloonRemoverCheat")
                .AsImplementedInterfaces()
                .AsSelf();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<BalloonRemoverCheat>());

            builder.RegisterComponentOnNewGameObject<CheatConsoleView>(Lifetime.Singleton, "CheatConsole");
            builder.RegisterBuildCallback(resolver => resolver.Resolve<CheatConsoleView>());
#endif
        }
    }
}
