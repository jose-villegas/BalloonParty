#if UNITY_EDITOR || DEVELOPMENT_BUILD
using BalloonParty.Cheats;
#endif

using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Spawner;
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
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Controller;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Spawner;
using BalloonParty.UI.GameOver;
using BalloonParty.Slots.Grid;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game
{
    /// <summary>
    ///     Per-section registration for <see cref="GameLifetimeScope"/> — call in the sequence declared here; VContainer starts entry points in registration order.
    /// </summary>
    internal static class GameScopeRegistration
    {
        internal static void RegisterMessages(this IContainerBuilder builder, MessagePipeOptions options)
        {
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
            builder.RegisterMessageBroker<LevelTransitionCompletedMessage>(options);
            builder.RegisterMessageBroker<LevelUpGlowTrailsMessage>(options);
            builder.RegisterMessageBroker<PausedMessage>(options);
            builder.RegisterMessageBroker<ResumedMessage>(options);
        }

        internal static void RegisterCoreServices(this IContainerBuilder builder)
        {
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
            builder.Register<ProjectileMotionResolver>(Lifetime.Singleton);
            builder.Register<INavigation, NavigationService>(Lifetime.Singleton);
            builder.Register<ICinematicState, CinematicStateService>(Lifetime.Singleton);
            builder.Register<RunMeta>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.RegisterComponentInHierarchy<SlotGridView>();
        }

        // Do not reorder or split — entry points start in registration order and the game depends on it.
        internal static void RegisterGameplaySystems(this IContainerBuilder builder)
        {
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
            // Registered before BalloonSpawner, which reads IActiveLevelParameters during prewarm sizing.
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
            builder.RegisterEntryPoint<ProjectileBuffService>().As<IProjectileBuffs>();
        }

        internal static void RegisterItems(this IContainerBuilder builder)
        {
            builder.Register<ItemEffectPlayer>(Lifetime.Singleton);
            builder.Register<BalloonOverlapQuery>(Lifetime.Singleton);

            builder.Register<ShieldItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<BombItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<LaserItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<LightningItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<PaintItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
        }

        internal static void RegisterPresentation(this IContainerBuilder builder)
        {
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
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static void RegisterCheats(this IContainerBuilder builder)
        {
            builder.Register<SpawnBalloonLineCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SpawnBalloonCheat>(Lifetime.Singleton).AsImplementedInterfaces();
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
        }
#endif
    }
}
