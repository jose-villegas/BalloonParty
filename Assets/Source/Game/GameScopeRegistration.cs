#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
using BalloonParty.Cheats;
#endif

using System;
using System.Collections.Generic;
using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using BalloonParty.Audio.Routing;
using BalloonParty.Audio.View;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Display;
using BalloonParty.Game.Cinematics;
using BalloonParty.Game.Danger;
using BalloonParty.Game.Flight;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Game.Score;
using BalloonParty.Game.Score.Behaviours;
using BalloonParty.Game.Telemetry;
using BalloonParty.Item;
using BalloonParty.Item.Bomb;
using BalloonParty.Item.Laser;
using BalloonParty.Item.Lightning;
using BalloonParty.Item.Paint;
using BalloonParty.Balloon;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Item.Shield;
using BalloonParty.Item.Snipe;
using BalloonParty.Nudge;
using BalloonParty.Prediction;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Controller;
using BalloonParty.Scenario;
using BalloonParty.Scenario.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Cadence;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Shared.Thermal;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Spawner;
using BalloonParty.UI.GameOver;
using BalloonParty.Slots.Grid;
using BalloonParty.Thrower;
using BalloonParty.UI;
using BalloonParty.UI.Tooltip;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game
{
    /// <summary>
    ///     Per-section registration for <see cref="GameLifetimeScope"/> — call in the sequence declared here; VContainer starts entry points in registration order.
    /// </summary>
    internal static class GameScopeRegistration
    {
        // Audio coalescing tuning — device-audition feel values (Pixel 9). Consts for now because
        // the Step-1 SoundBankConfiguration is frozen; migrate onto the SO in Phase 2.
        private const float CoalesceWindowSeconds = 0.06f;
        private const int MaxBurstPerWindow = 4;

        // Hoisted to a field so the registration below binds one delegate instance rather than an
        // implicitly cached lambda whose overload resolution depends on WithParameter's Func<TParam>
        // overload picking float instead of Func<float>.
        private static readonly Func<float> UnscaledClock = () => Time.unscaledTime;

        internal static void RegisterMessages(this IContainerBuilder builder, MessagePipeOptions options)
        {
            builder.RegisterMessageBroker<BalanceBalloonsMessage>(options);
            builder.RegisterMessageBroker<SpawnBalloonLineMessage>(options);
            builder.RegisterMessageBroker<ProjectileDestroyedMessage>(options);
            builder.RegisterMessageBroker<ActorHitMessage>(options);
            builder.RegisterMessageBroker<BalloonDeflectedMessage>(options);
            builder.RegisterMessageBroker<NudgeMessage>(options);
            builder.RegisterMessageBroker<ScorePointsGroupMessage>(options);
            builder.RegisterMessageBroker<StreakChangedMessage>(options);
            builder.RegisterMessageBroker<ScoreLevelUpMessage>(options);
            builder.RegisterMessageBroker<GameOverMessage>(options);
            builder.RegisterMessageBroker<OverflowHeartRequestedMessage>(options);
            builder.RegisterMessageBroker<WaveDamageMessage>(options);
            builder.RegisterMessageBroker<StrikethroughArrivedMessage>(options);
            builder.RegisterMessageBroker<EndRunRequestedMessage>(options);
            builder.RegisterMessageBroker<RunResetMessage>(options);
            builder.RegisterMessageBroker<RunRestartCompletedMessage>(options);
            builder.RegisterMessageBroker<BoardClearMessage>(options);
            builder.RegisterMessageBroker<BoardDepletedMessage>(options);
            builder.RegisterMessageBroker<ForceDestroyProjectileMessage>(options);
            builder.RegisterMessageBroker<ProjectileLoadedMessage>(options);
            builder.RegisterMessageBroker<ProjectileFiredMessage>(options);
            builder.RegisterMessageBroker<ProjectileCruiseStartedMessage>(options);
            builder.RegisterMessageBroker<ProjectileCruiseEndedMessage>(options);
            builder.RegisterMessageBroker<ProjectileDoomedStartedMessage>(options);
            builder.RegisterMessageBroker<ProjectileDoomedEndedMessage>(options);
            builder.RegisterMessageBroker<SpeckSpawnRequestMessage>(options);
            builder.RegisterMessageBroker<ItemCheckMessage>(options);
            builder.RegisterMessageBroker<ItemActivatedMessage>(options);
            builder.RegisterMessageBroker<TransformCapturedMessage>(options);
            builder.RegisterMessageBroker<ShieldGainedMessage>(options);
            builder.RegisterMessageBroker<ShieldLostMessage>(options);
            builder.RegisterMessageBroker<WallHitMessage>(options);
            builder.RegisterMessageBroker<SpeedTapMintedMessage>(options);
            builder.RegisterMessageBroker<PierceDischargedMessage>(options);
            builder.RegisterMessageBroker<ScoreTrailArrivedMessage>(options);
            builder.RegisterMessageBroker<LevelUpAbortedMessage>(options);
            builder.RegisterMessageBroker<LevelUpAbandonedMessage>(options);
            builder.RegisterMessageBroker<LevelUpDismissedMessage>(options);
            builder.RegisterMessageBroker<GameOverDismissedMessage>(options);
            builder.RegisterMessageBroker<LevelTransitionCompletedMessage>(options);
            builder.RegisterMessageBroker<LevelAscendStartedMessage>(options);
            builder.RegisterMessageBroker<LevelDescendStartedMessage>(options);
            builder.RegisterMessageBroker<LevelUpFillTrailsMessage>(options);
            builder.RegisterMessageBroker<ProgressBarCompletedMessage>(options);
            builder.RegisterMessageBroker<PausedMessage>(options);
            builder.RegisterMessageBroker<ResumedMessage>(options);
        }

        internal static void RegisterCoreServices(this IContainerBuilder builder)
        {
            builder.Register<BalancePathHolder>(Lifetime.Singleton).AsSelf().As<IRunResettable>();
            builder.Register<SlotGrid>(Lifetime.Singleton);
            // Deflectors come from the grid, not from the balloon registry: statics deflect too, and
            // the grid is the one place that holds every actor regardless of family.
            builder.Register<SlotGridDeflectorField>(Lifetime.Singleton).As<IDeflectorField>();
            // The shield preference is attached rather than injected: ten call sites build a
            // MoveWeightEvaluator, including the shot simulator's board mirror, and only the live
            // board has a thrower to measure reachability from.
            builder.Register(resolver =>
            {
                var query = new GridBalanceQuery(resolver.Resolve<SlotGrid>());
                var runConfig = resolver.Resolve<IRunConfig>();
                if (runConfig.PlanShieldChains)
                {
                    query.Evaluator.ShieldPreference = new ShieldSlotPreference(
                        resolver.Resolve<ShieldReachabilityField>(), runConfig.ShieldChain);
                }

                return query;
            }, Lifetime.Singleton);

            builder.Register<ShieldReachabilityField>(Lifetime.Singleton);
            builder.Register(resolver => new BalloonContactRadii(
                    resolver.Resolve<IBalloonsConfiguration>(),
                    resolver.Resolve<ISlotGridConfig>().SlotSeparation.x),
                Lifetime.Singleton);
            // Recording is editor-only ([Conditional]); at runtime this is an inert empty instance.
            builder.Register<BalanceDebugRecorder>(Lifetime.Singleton);
            builder.Register<TrailEndpointRegistry>(Lifetime.Singleton);
            builder.Register<PauseService>(Lifetime.Singleton).AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<TimeScaleService>().AsSelf().As<ITimeScaleClaims>().As<IRunResettable>();
            builder.RegisterEntryPoint<ProjectileDoomedTimeScaleController>();
            builder.RegisterEntryPoint<HoldSpeedUpController>().AsSelf().As<IHoldSpeedUpState>();
            builder.Register<ProjectilePositionProvider>(Lifetime.Singleton);
            // Game scope, not the thrower's: ItemAssigner plans shield chains from it and cannot
            // reach into a child scope for ThrowerView.
            builder.Register<ThrowerOriginProvider>(Lifetime.Singleton);
            builder.Register<PredictionTraceProvider>(Lifetime.Singleton);
            builder.RegisterEntryPoint<ProjectileFacingSource>().As<IProjectileFacingSource>();
            builder.Register<NudgeOverrideResolver>(Lifetime.Singleton);
            builder.Register<ColorStreakTracker>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<ProjectileHitResolver>(Lifetime.Singleton);
            builder.Register<ProjectileTapResolver>(Lifetime.Singleton);
            builder.Register<ProjectileMotionResolver>(Lifetime.Singleton);
            builder.Register<INavigation, NavigationService>(Lifetime.Singleton);
            builder.Register<ICinematicState, CinematicStateService>(Lifetime.Singleton);
            builder.Register<RunMeta>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<RetryTracker>(Lifetime.Singleton).AsSelf().As<IRetryState>().As<IRunResettable>();

#if UNITY_ANDROID && !UNITY_EDITOR
            builder.Register<IThermalSource, AndroidThermalSource>(Lifetime.Singleton);
#else
            builder.Register<IThermalSource, StubThermalSource>(Lifetime.Singleton);
#endif
            builder.RegisterEntryPoint<ThermalFrameRateGovernor>();

            builder.RegisterComponentInHierarchy<SlotGridView>();
#if UNITY_EDITOR
            // Scene-view debug overlay only; hierarchy registration throws if the component is absent,
            // so the guard keeps device builds independent of the debug object.
            builder.RegisterComponentInHierarchy<BalanceGizmos>();
#endif
        }

        // The composite is always registered (R28b) so a later consumer (GameplayMetricsService, W3)
        // can depend on ITelemetrySink unconditionally — only which leaf sinks it wraps is gated. An
        // empty array is the inert "no export configured" state; there is deliberately no
        // NullTelemetrySink. Same #if shape as RegisterCheats below, but the method itself is never
        // compiled out, only the JsonLinesTelemetrySink branch inside it.
        internal static void RegisterTelemetrySinks(this IContainerBuilder builder)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterEntryPoint<JsonLinesTelemetrySink>().AsSelf();
            builder.Register<ITelemetrySink>(resolver =>
                new CompositeTelemetrySink(new ITelemetrySink[] { resolver.Resolve<JsonLinesTelemetrySink>() }),
                Lifetime.Singleton);
#else
            builder.Register<ITelemetrySink>(_ => new CompositeTelemetrySink(Array.Empty<ITelemetrySink>()), Lifetime.Singleton);
#endif
        }

        // Do not reorder or split — entry points start in registration order and the game depends on it.
        internal static void RegisterGameplaySystems(this IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<BalloonBalancer>().AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<NudgeService>();
            builder.RegisterEntryPoint<GridActorHitController>();
            builder.Register<NavigationReadyGate>(Lifetime.Singleton).WithParameter(NavigationState.Game);
            builder.RegisterEntryPoint<GridSpawnerCoordinator>().AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<StaticActorSpawner>().AsSelf().As<IGridSpawner>();
            builder.RegisterEntryPoint<PuffClusterRegistry>().AsSelf();
            builder.RegisterEntryPoint<PuffCloudViewController>().As<ITransitionOutgoingContent>();
            builder.RegisterEntryPoint<BushClusterRegistry>().AsSelf();
            builder.RegisterEntryPoint<BushViewController>().AsSelf().As<ITransitionOutgoingContent>();
            builder.Register<SceneLightFieldService>(Lifetime.Singleton)
                .AsImplementedInterfaces().AsSelf();
            builder.RegisterEntryPoint<BalloonMotionTicker>().AsSelf();
            builder.RegisterEntryPoint<RejectedBalloonEffect>().AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<BalloonControllerRegistry>().AsSelf();
            builder.Register<BalloonPopPresenter>(Lifetime.Singleton);
            builder.Register<BalloonControllerContext>(Lifetime.Singleton);
            builder.Register<BalloonPlacementResolver>(Lifetime.Singleton);
            builder.Register<BalloonFactory>(Lifetime.Singleton);
            // Ahead of everything that reads it: HitPipeline writes it, audio and the hold controller
            // read it. Its own counters reset on ProjectileLoadedMessage, so start order only has to
            // beat the first shot, but keeping it early makes the dependency direction obvious.
            builder.RegisterEntryPoint<FlightStatsService>()
                .As<IFlightScope>().As<IFlightStats>().As<IFlightStatsWriter>();
            // Registered before BalloonSpawner, which reads IActiveLevelParameters during prewarm sizing.
            builder.RegisterEntryPoint<LevelDifficultyResolver>().AsSelf().As<IActiveLevelParameters>().As<ILevelThresholds>().As<IRunResettable>();
            builder.RegisterEntryPoint<BalloonSpawner>().As<IGridSpawner>().AsSelf().As<IRunResettable>();
            builder.RegisterEntryPoint<ScoreController>().AsSelf().As<IRunScore>().As<IRunResettable>();
            builder.RegisterEntryPoint<LevelController>().AsSelf().As<ILevelProgress>().As<IRunResettable>();
            builder.RegisterEntryPoint<TimeOfDayCycle>().As<IRunResettable>();
            builder.Register<HitPipeline>(Lifetime.Singleton).As<IHitDispatcher>();
            builder.RegisterEntryPoint<RunController>().AsSelf();
            builder.Register<BoardClearController>(Lifetime.Singleton).As<IRunResettable>();
            builder.RegisterEntryPoint<PlayerHealthController>().AsSelf().As<IRunResettable>().As<IPlayerHealth>();
            builder.Register<ILossForecast, LossForecast>(Lifetime.Singleton);
            builder.RegisterEntryPoint<SpaceDanger>().AsSelf().As<IDangerLevel>();
            builder.Register<HeartTrailTracker>(Lifetime.Singleton).AsSelf().As<IRunResettable>();
            builder.Register<DefaultScoreTrailBehaviour>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<ShapeFormationTicker>().AsSelf();
            builder.Register<BigScoreTrailBehaviour>(Lifetime.Singleton).AsSelf();
            builder.Register(BuildScoreTrailResolver, Lifetime.Singleton);
            builder.RegisterEntryPoint<ScoreTrailService>().AsSelf().As<IRunResettable>().As<ITransitionOutgoingContent>();
            builder.RegisterEntryPoint<ItemAssigner>();
            builder.RegisterEntryPoint<ItemActivator>();
            builder.RegisterEntryPoint<ProjectileBuffService>().As<IProjectileBuffs>();
            builder.RegisterEntryPoint<ActiveProjectilePierce>().As<IActiveProjectilePierce>();
            builder.RegisterEntryPoint<PierceDischargeEffects>();

            // Last on purpose: it subscribes to everything above it and must never sit in front of a
            // gameplay system in the entry-point start order. Clocks sample Time.unscaledTime (not
            // realtimeSinceStartup — nothing here observes app backgrounding, so a pocket-suspend would
            // inflate that by the full background time), injected the same way SfxThrottleGate takes its.
            builder.RegisterEntryPoint<GameplayMetricsService>()
                .AsSelf()
                .As<IRunResettable>()
                .As<ILevelMetricsView>()
                .WithParameter(typeof(Func<float>), UnscaledClock);
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
            builder.Register<SnipeItemHandler>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterEntryPoint<SnipeDischargeBloom>();
        }

        internal static void RegisterPresentation(this IContainerBuilder builder)
        {
            // The camera pipeline now lives on the single persistent camera owned by AppLifetimeScope; this
            // scope resolves those from the parent. Only the run-scoped shake CONTROLLER lives here — it
            // drives the App-owned CameraShakeView and holds the gameplay subscriptions that reset per run.
            builder.RegisterEntryPoint<CameraShakeController>();
            builder.RegisterComponentInHierarchy<SpeckField>();
            builder.RegisterComponentInHierarchy<WallNetView>();
            builder.RegisterComponentInHierarchy<SmokeFieldView>();
            builder.Register<SmokeFieldService>(Lifetime.Singleton)
                .AsImplementedInterfaces().AsSelf();
            builder.Register<CinematicCameraRig>(Lifetime.Singleton);
            builder.Register<EffectCadenceCoordinator>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.Register<GameOverPresentationGate>(Lifetime.Singleton);
            builder.Register<BoardPopWave>(Lifetime.Singleton);
            builder.Register<BoardFloatAwayEffect>(Lifetime.Singleton).As<IBoardEffect>();

            builder.RegisterEntryPoint<CinematicDirector>().AsSelf();
            builder.RegisterEntryPoint<LevelUpCinematic>();
            builder.RegisterEntryPoint<HeartDrainCinematic>();
            builder.RegisterEntryPoint<GameOverLossCinematic>();
            builder.RegisterEntryPoint<LevelTransitionController>();
            builder.RegisterComponentInHierarchy<GameOverScreen>();

            builder.RegisterBuildCallback(InjectTints);
            builder.RegisterBuildCallback(InjectTooltips);
            return;

            void InjectTints(IObjectResolver resolver)
            {
                foreach (var tint in UnityEngine.Object.FindObjectsByType<TimeOfDayTint>(
                             UnityEngine.FindObjectsSortMode.None))
                {
                    resolver.Inject(tint);
                }
            }

            void InjectTooltips(IObjectResolver resolver)
            {
                foreach (var tooltip in UnityEngine.Object.FindObjectsByType<HoldSpeedUpTooltip>(
                             UnityEngine.FindObjectsSortMode.None))
                {
                    resolver.Inject(tooltip);
                }
            }
        }

        internal static void RegisterAudioCore(this IContainerBuilder builder, SoundBankConfiguration soundBank,
            AudioSourceVoice voicePrefab, AudioMixerSettings mixerSettings)
        {
            if (voicePrefab == null)
            {
                Log.Warn("Audio", "SfxVoice prefab unassigned — audio disabled.");
                return;
            }

            var bank = soundBank != null ? soundBank : ScriptableObject.CreateInstance<SoundBankConfiguration>();
            var mixer = mixerSettings != null ? mixerSettings : ScriptableObject.CreateInstance<AudioMixerSettings>();

            builder.RegisterInstance<ISoundBankConfiguration>(bank);
            builder.RegisterInstance(voicePrefab);
            builder.RegisterInstance<IAudioMixerSettings>(mixer);
            builder.Register<IAudioMixerRouter, AudioMixerRouter>(Lifetime.Singleton);

            builder.RegisterInstance(new VoiceLimiter(bank.GlobalVoiceCap));
            builder.RegisterInstance(new SfxThrottleGate(() => Time.unscaledTime, CoalesceWindowSeconds, MaxBurstPerWindow));
            builder.RegisterInstance(new VariationPicker(new System.Random(), bank.MelodicScale,
                bank.MelodicRootSemitone));

            builder.Register<SfxService>(Lifetime.Singleton)
                .As<ISoundPlayer>().As<IMelodicContext>().As<IRunResettable>().AsSelf();

            builder.RegisterEntryPoint<SfxVoicePoolBootstrap>();
        }

        internal static void RegisterAudioRouters(this IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<CombatSoundRouter>();
            builder.RegisterEntryPoint<ProgressionSoundRouter>();
            builder.RegisterEntryPoint<ItemSoundRouter>();
            builder.RegisterEntryPoint<DangerSoundRouter>();
            builder.RegisterEntryPoint<WindSoundRouter>();
            builder.RegisterEntryPoint<MusicSoundRouter>();
            builder.RegisterEntryPoint<AudioChannelController>();
        }

        // The dictionary is the choreography seam: the resolver maps each ScoreTrailBehaviourId to its handler.
        private static ScoreTrailBehaviourResolver BuildScoreTrailResolver(IObjectResolver container)
        {
            var handlers = new Dictionary<ScoreTrailBehaviourId, IScoreTrailBehaviour>
            {
                { ScoreTrailBehaviourId.DefaultScore, container.Resolve<DefaultScoreTrailBehaviour>() },
                { ScoreTrailBehaviourId.BigScore, container.Resolve<BigScoreTrailBehaviour>() },
            };
            return new ScoreTrailBehaviourResolver(container.Resolve<IScoreTrailBehaviourConfiguration>(), handlers);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
        internal static void RegisterCheats(this IContainerBuilder builder)
        {
            builder.Register<SpawnBalloonLineCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SpawnBalloonCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<TriggerLevelUpCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<NearLevelUpCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<AwardScorePopCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<BlockLevelUpCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ForceGameOverCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ForceRestartCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<FireBestShotCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<StartFromLevelCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<AddShieldCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<TimeOfDayCheat>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentOnNewGameObject<BalloonRemoverCheat>(Lifetime.Singleton, "BalloonRemoverCheat")
                .AsImplementedInterfaces()
                .AsSelf();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<BalloonRemoverCheat>());

            builder.RegisterComponentOnNewGameObject<DisturbanceStampCheat>(Lifetime.Singleton, "DisturbanceStampCheat")
                .AsImplementedInterfaces()
                .AsSelf();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<DisturbanceStampCheat>());

            builder.RegisterComponentOnNewGameObject<LightStampCheat>(Lifetime.Singleton, "LightStampCheat")
                .AsImplementedInterfaces()
                .AsSelf();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<LightStampCheat>());

            builder.RegisterComponentOnNewGameObject<CheatConsoleView>(Lifetime.Singleton, "CheatConsole");
            builder.RegisterBuildCallback(resolver => resolver.Resolve<CheatConsoleView>());
        }
#endif
    }
}
