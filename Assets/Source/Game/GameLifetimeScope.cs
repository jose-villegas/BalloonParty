#if UNITY_EDITOR || DEVELOPMENT_BUILD
using BalloonParty.Cheats;
#endif

using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration;
using BalloonParty.Display;
using BalloonParty.Game.Cinematics;
using BalloonParty.Game.Danger;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
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
using BalloonParty.Projectile.Controller;
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
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;

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
        [SerializeField] private OverflowSettings _overflowSettings;
        [SerializeField] private CinematicsSettings _cinematicsSettings;
        [SerializeField] private GridActorConfiguration _gridActorConfiguration;
        [SerializeField] private PuffCloudSettings _puffCloudSettings;
        [SerializeField] private BushSettings _bushSettings;
        [SerializeField] private DisturbanceFieldSettings _disturbanceFieldSettings;
        [SerializeField] private LevelPacingConfiguration _levelPacingConfiguration;
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
            builder.RegisterMessageBroker<OverflowHeartRequestedMessage>(options);
            builder.RegisterMessageBroker<EndRunRequestedMessage>(options);
            builder.RegisterMessageBroker<RunResetMessage>(options);
            builder.RegisterMessageBroker<BoardClearMessage>(options);
            builder.RegisterMessageBroker<ProjectileLoadedMessage>(options);
            builder.RegisterMessageBroker<ItemCheckMessage>(options);
            builder.RegisterMessageBroker<ItemActivatedMessage>(options);
            builder.RegisterMessageBroker<TransformCapturedMessage>(options);
            builder.RegisterMessageBroker<ShieldGainedMessage>(options);
            builder.RegisterMessageBroker<ShieldLostMessage>(options);
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
            builder.RegisterInstance<IOverflowSettings>(_overflowSettings);
            builder.RegisterInstance<ICinematicsSettings>(_cinematicsSettings);
            builder.RegisterInstance<IGridActorConfiguration>(_gridActorConfiguration);
            builder.RegisterInstance<IPuffCloudSettings>(_puffCloudSettings);
            builder.RegisterInstance<IBushSettings>(_bushSettings);
            builder.RegisterInstance<IDisturbanceFieldSettings>(_disturbanceFieldSettings);
            builder.RegisterInstance<ILevelPacingConfiguration>(_levelPacingConfiguration);
            builder.RegisterInstance(new ThrowerSettings(_projectilePrefab));
            builder.RegisterInstance(_scoreTrailPrefab);

            builder.Register<BalancePathHolder>(Lifetime.Singleton).AsSelf().As<IRunResettable>();
            builder.Register<SlotGrid>(Lifetime.Singleton);
            builder.Register<ScenarioContentRoot>(Lifetime.Singleton);
            builder.Register<GridBalanceQuery>(Lifetime.Singleton);
            builder.Register<PoolManager>(Lifetime.Singleton);
            builder.Register<TrailEndpointRegistry>(Lifetime.Singleton);
            builder.Register<PauseService>(Lifetime.Singleton).AsSelf().As<IRunResettable>();
            builder.Register<TimeScaleService>(Lifetime.Singleton).AsSelf().As<IRunResettable>();
            builder.Register<ProjectilePositionProvider>(Lifetime.Singleton);
            builder.Register<ImpactEventBus>(Lifetime.Singleton)
                .AsImplementedInterfaces().AsSelf();
            builder.Register<NudgeOverrideResolver>(Lifetime.Singleton);
            builder.Register<ColorStreakTracker>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<ProjectileHitResolver>(Lifetime.Singleton);
            builder.Register<INavigation, NavigationService>(Lifetime.Singleton);
            builder.Register<ICinematicState, CinematicStateService>(Lifetime.Singleton);
            builder.Register<RunMeta>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.RegisterComponentInHierarchy<SlotGridView>();

            builder.RegisterEntryPoint<BalloonBalancer>().AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<NudgeService>();
            builder.RegisterEntryPoint<GridActorHitController>();
            builder.RegisterInstance<IReadyGate>(new NavigationReadyGate(NavigationState.Game));
            builder.RegisterEntryPoint<GridSpawnerCoordinator>().AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<StaticActorSpawner>().AsSelf().As<IGridSpawner>();
            builder.RegisterEntryPoint<PuffClusterRegistry>().AsSelf();
            builder.RegisterEntryPoint<PuffCloudViewController>().As<ITransitionOutgoingContent>();
            builder.RegisterEntryPoint<BushClusterRegistry>().AsSelf();
            builder.RegisterEntryPoint<BushViewController>().AsSelf().As<ITransitionOutgoingContent>();
            builder.Register<DisturbanceFieldService>(Lifetime.Singleton)
                .AsImplementedInterfaces().AsSelf();
            builder.RegisterEntryPoint<BalloonMotionTicker>().AsSelf();
            builder.RegisterEntryPoint<RejectedBalloonEffect>().AsSelf().As<IRunResettable>().As<IPendingHealthCharges>();
            builder.RegisterEntryPoint<BalloonControllerRegistry>().AsSelf().As<ITransitionOutgoingContent>();
            builder.Register<BalloonControllerContext>(Lifetime.Singleton);
            builder.Register<BalloonPlacementResolver>(Lifetime.Singleton);
            builder.Register<BalloonFactory>(Lifetime.Singleton);
            // Registered before BalloonSpawner so its Start() (synchronous ResolveFor(1)) runs first —
            // BalloonSpawner's own Start() reads IActiveLevelParameters during prewarm sizing.
            builder.RegisterEntryPoint<LevelDifficultyResolver>().AsSelf().As<IActiveLevelParameters>().As<ILevelThresholds>().As<IRunResettable>();
            builder.RegisterEntryPoint<BalloonSpawner>().As<IGridSpawner>().AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<ScoreController>().AsSelf().As<IRunScore>().As<IRunResettable>();
            builder.RegisterEntryPoint<LevelController>().AsSelf().As<ILevelProgress>().As<IRunResettable>();
            builder.Register<HitPipeline>(Lifetime.Singleton).As<IHitDispatcher>();
            builder.RegisterEntryPoint<RunController>().AsSelf();
            builder.Register<BoardClearController>(Lifetime.Singleton).As<IRunResettable>();
            builder.RegisterEntryPoint<PlayerHealthController>().AsSelf().As<IRunResettable>().As<IPlayerHealth>();
            builder.Register<ILossForecast, LossForecast>(Lifetime.Singleton);
            builder.RegisterEntryPoint<SpaceDanger>().AsSelf().As<IDangerLevel>();
            builder.Register<HeartTrailTracker>(Lifetime.Singleton).AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<ScoreTrailService>().AsSelf();
            builder.RegisterEntryPoint<ItemAssigner>();
            builder.RegisterEntryPoint<ItemActivator>();

            builder.Register<ItemEffectPlayer>(Lifetime.Singleton);
            builder.Register<BalloonOverlapQuery>(Lifetime.Singleton);

            builder.Register<ShieldItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<BombItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<LaserItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<LightningItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<PaintItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<OrthogonalSizeCameraController>();
            builder.RegisterComponentInHierarchy<CameraShakeService>();
            builder.RegisterComponentInHierarchy<SceneCaptureService>();
            builder.RegisterComponentInHierarchy<CinematicCameraView>();
            builder.Register<CinematicCameraRig>(Lifetime.Singleton);

            builder.RegisterEntryPoint<CinematicDirector>().AsSelf();
            builder.RegisterEntryPoint<LevelUpCinematic>();
            builder.RegisterEntryPoint<HeartDrainCinematic>();
            builder.RegisterEntryPoint<LevelTransitionController>();
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
