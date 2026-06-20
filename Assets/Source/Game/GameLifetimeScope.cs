#if UNITY_EDITOR || DEVELOPMENT_BUILD
using BalloonParty.Cheats;
#endif

using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration;
using BalloonParty.Display;
using BalloonParty.Game.Cinematics;
using BalloonParty.Game.Health;
using BalloonParty.Game.Run;
using BalloonParty.Game.Score;
using BalloonParty.Item;
using BalloonParty.Item.Bomb;
using BalloonParty.Item.Laser;
using BalloonParty.Item.Lightning;
using BalloonParty.Item.Paint;
using BalloonParty.Item.Shield;
using BalloonParty.Nudge;
using BalloonParty.Projectile;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Spawner;
using BalloonParty.Thrower;
using BalloonParty.UI.GameOver;
using BalloonParty.UI.Score;
using BalloonParty.Slots.Grid;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game
{
    [DefaultExecutionOrder(-5001)]
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameConfiguration _gameConfiguration;
        [SerializeField] private GameDisplayConfiguration _displayConfiguration;
        [SerializeField] private ItemConfiguration _itemConfiguration;
        [SerializeField] private GamePalette _gamePalette;
        [SerializeField] private BalloonsConfiguration _balloonsConfiguration;
        [SerializeField] private GridActorConfiguration _gridActorConfiguration;
        [SerializeField] private PuffCloudSettings _puffCloudSettings;
        [SerializeField] private BushSettings _bushSettings;
        [SerializeField] private DisturbanceFieldSettings _disturbanceFieldSettings;
        [SerializeField] private ProjectileView _projectilePrefab;
        [SerializeField] private FlyingTrail _scoreTrailPrefab;

        protected override void Awake()
        {
            DOTween.SetTweensCapacity(1000, 50);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<BalanceBalloonsMessage>(options);
            builder.RegisterMessageBroker<SpawnBalloonLineMessage>(options);
            builder.RegisterMessageBroker<ProjectileDestroyedMessage>(options);
            builder.RegisterMessageBroker<ActorHitMessage>(options);
            builder.RegisterMessageBroker<BalloonDeflectedMessage>(options);
            builder.RegisterMessageBroker<NudgeMessage>(options);
            builder.RegisterMessageBroker<ScorePointMessage>(options);
            builder.RegisterMessageBroker<ScoreLevelUpMessage>(options);
            builder.RegisterMessageBroker<GameOverMessage>(options);
            builder.RegisterMessageBroker<SpawnBlockedMessage>(options);
            builder.RegisterMessageBroker<EndRunRequestedMessage>(options);
            builder.RegisterMessageBroker<BoardClearMessage>(options);
            builder.RegisterMessageBroker<ProjectileLoadedMessage>(options);
            builder.RegisterMessageBroker<ItemCheckMessage>(options);
            builder.RegisterMessageBroker<ItemActivatedMessage>(options);
            builder.RegisterMessageBroker<TransformCapturedMessage>(options);
            builder.RegisterMessageBroker<ShieldGainedMessage>(options);
            builder.RegisterMessageBroker<ScoreTrailArrivedMessage>(options);
            builder.RegisterMessageBroker<LevelUpDismissedMessage>(options);
            builder.RegisterMessageBroker<LevelUpGlowTrailsMessage>(options);
            builder.RegisterMessageBroker<PausedMessage>(options);
            builder.RegisterMessageBroker<ResumedMessage>(options);

            builder.RegisterInstance<IGameConfiguration>(_gameConfiguration);
            builder.RegisterInstance<IGameDisplayConfiguration>(_displayConfiguration);
            builder.RegisterInstance<IItemConfiguration>(_itemConfiguration);
            builder.RegisterInstance<IGamePalette>(_gamePalette);
            builder.RegisterInstance<IBalloonsConfiguration>(_balloonsConfiguration);
            builder.RegisterInstance<IGridActorConfiguration>(_gridActorConfiguration);
            builder.RegisterInstance<IPuffCloudSettings>(_puffCloudSettings);
            builder.RegisterInstance<IBushSettings>(_bushSettings);
            builder.RegisterInstance<IDisturbanceFieldSettings>(_disturbanceFieldSettings);
            builder.RegisterInstance(new ThrowerSettings(_projectilePrefab));
            builder.RegisterInstance(_scoreTrailPrefab);

            builder.Register<BalancePathHolder>(Lifetime.Singleton).AsSelf().As<IRunResettable>();
            builder.Register<SlotGrid>(Lifetime.Singleton);
            builder.Register<PoolManager>(Lifetime.Singleton);
            builder.Register<PauseService>(Lifetime.Singleton).AsSelf().As<IRunResettable>();
            builder.Register<ProjectilePositionProvider>(Lifetime.Singleton);
            builder.Register<ImpactEventBus>(Lifetime.Singleton)
                .AsImplementedInterfaces().AsSelf();
            builder.Register<NudgeOverrideResolver>(Lifetime.Singleton);
            builder.Register<ColorStreakTracker>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<INavigation, NavigationService>(Lifetime.Singleton);
            builder.Register<ICinematicState, CinematicStateService>(Lifetime.Singleton);
            builder.Register<RunMeta>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.RegisterComponentInHierarchy<SlotGridView>();

            builder.RegisterEntryPoint<BalloonBalancer>().AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<NudgeService>();
            builder.RegisterEntryPoint<GridActorHitController>();
            builder.RegisterInstance<IReadyGate>(new NavigationReadyGate(NavigationState.Game));
            builder.RegisterEntryPoint<GridSpawnerCoordinator>().AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<StaticActorSpawner>().As<IGridSpawner>();
            builder.RegisterEntryPoint<PuffClusterRegistry>().AsSelf();
            builder.RegisterEntryPoint<PuffCloudViewController>();
            builder.RegisterEntryPoint<BushClusterRegistry>().AsSelf();
            builder.RegisterEntryPoint<BushViewController>().AsSelf();
            builder.Register<DisturbanceFieldService>(Lifetime.Singleton)
                .AsImplementedInterfaces().AsSelf();
            builder.Register<RejectedBalloonEffect>(Lifetime.Singleton).AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<BalloonSpawner>().As<IGridSpawner>().AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<ScoreController>().AsSelf().As<IRunScore>().As<IRunResettable>();
            builder.RegisterEntryPoint<RunController>().AsSelf();
            builder.Register<BoardClearController>(Lifetime.Singleton).As<IRunResettable>();
            builder.RegisterEntryPoint<PlayerHealthController>().AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<ScoreTrailService>().AsSelf();
            builder.RegisterEntryPoint<ItemAssigner>();
            builder.RegisterEntryPoint<ItemActivator>();

            builder.Register<ShieldItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<BombItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<LaserItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<LightningItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<PaintItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<OrthogonalSizeCameraController>();
            builder.RegisterComponentInHierarchy<CameraShakeService>();

            builder.RegisterEntryPoint<CinematicDirector>().AsSelf();
            builder.RegisterComponentInHierarchy<LevelUpTrailEffect>();
            builder.RegisterComponentInHierarchy<GameOverScreen>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.Register<SpawnBalloonLineCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<FireProjectileCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<TriggerLevelUpCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<NearLevelUpCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ForceGameOverCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ForceRestartCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentOnNewGameObject<BalloonRemoverCheat>(Lifetime.Singleton, "BalloonRemoverCheat")
                .AsImplementedInterfaces()
                .AsSelf();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<BalloonRemoverCheat>());

            builder.RegisterComponentOnNewGameObject<DisturbanceStampCheat>(Lifetime.Singleton, "DisturbanceStampCheat")
                .AsImplementedInterfaces()
                .AsSelf();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<DisturbanceStampCheat>());

            builder.RegisterComponentOnNewGameObject<CheatConsoleView>(Lifetime.Singleton, "CheatConsole");
            builder.RegisterBuildCallback(resolver => resolver.Resolve<CheatConsoleView>());
#endif
        }
    }
}
