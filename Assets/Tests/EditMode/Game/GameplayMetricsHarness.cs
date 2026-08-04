using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Danger;
using BalloonParty.Game.Flight;
using BalloonParty.Game.Run;
using BalloonParty.Game.Telemetry;
using BalloonParty.Projectile.Controller;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using UniRx;

namespace BalloonParty.Tests.Game
{
    // Shared rig for the two W3 fixtures (RunIdentityTests, GameplayMetricsServiceTests). Built once
    // here rather than duplicated per fixture because the service takes 32 constructor arguments and a
    // capture that is silently missing from one copy compiles, asserts nothing and passes green — the
    // exact failure the plan's test-strategy section warns about.
    //
    // Uses ScoreControllerTests' capture pattern: each ISubscriber substitute records the
    // IMessageHandler the service passes to Subscribe, so a test "publishes" by invoking that handler.
    internal sealed class GameplayMetricsHarness : IDisposable
    {
        internal const string Red = "Red";
        internal const string Blue = "Blue";

        internal readonly List<IDisposable> Subscriptions = new();
        internal readonly RecordingTelemetrySink Sink = new();
        internal readonly ITelemetrySink Wired;
        internal readonly IRetryState RetryState = Substitute.For<IRetryState>();
        internal readonly IFlightStats FlightStats = Substitute.For<IFlightStats>();
        internal readonly IHoldSpeedUpState HoldState = Substitute.For<IHoldSpeedUpState>();
        internal readonly ReactiveProperty<NavigationState> Navigation = new(NavigationState.Launch);
        internal readonly ReactiveProperty<bool> FlightLoaded = new(false);
        internal readonly ReactiveProperty<int> TotalScore = new(0);
        internal readonly ReactiveProperty<float> Danger = new(0f);

        internal readonly PauseService Pause;
        internal readonly GameplayMetricsService Service;

        internal IMessageHandler<ProjectileLoadedMessage> ProjectileLoaded;
        internal IMessageHandler<ProjectileFiredMessage> ProjectileFired;
        internal IMessageHandler<ProjectileDestroyedMessage> ProjectileDestroyed;
        internal IMessageHandler<ActorHitMessage> ActorHit;
        internal IMessageHandler<PierceDischargedMessage> PierceDischarged;
        internal IMessageHandler<SpeedTapMintedMessage> SpeedTapMinted;
        internal IMessageHandler<ScoreTrailArrivedMessage> TrailArrived;
        internal IMessageHandler<ScorePointsGroupMessage> PointsGroup;
        internal IMessageHandler<StreakChangedMessage> StreakChanged;
        internal IMessageHandler<WaveDamageMessage> WaveDamage;
        internal IMessageHandler<StrikethroughArrivedMessage> Strikethrough;
        internal IMessageHandler<ShieldGainedMessage> ShieldGained;
        internal IMessageHandler<ShieldLostMessage> ShieldLost;
        internal IMessageHandler<ItemActivatedMessage> ItemActivated;
        internal IMessageHandler<BoardDepletedMessage> BoardDepleted;
        internal IMessageHandler<ScoreLevelUpMessage> ScoreLevelUp;
        internal IMessageHandler<LevelUpDismissedMessage> LevelUpDismissed;
        internal IMessageHandler<LevelUpAbortedMessage> LevelUpAborted;
        internal IMessageHandler<LevelUpAbandonedMessage> LevelUpAbandoned;
        internal IMessageHandler<LevelTransitionCompletedMessage> TransitionCompleted;
        internal IMessageHandler<GameOverMessage> GameOver;

        // The single time seam (the plan forbids a test-only Advance hook): tests bump this between
        // handler invocations and every scope's clocks move with it.
        internal float Now;

        internal IReadOnlyList<TelemetryEnvelope> Written => Sink.Written;

        // sink: pass a throwing fake to exercise the service-side never-throw guard; otherwise the
        // recording sink the assertions read from.
        internal GameplayMetricsHarness(ITelemetrySink sink = null)
        {
            Wired = sink ?? Sink;

            var palette = Substitute.For<IGamePalette>();
            palette.ProgressColorNames.Returns(new[] { Red, Blue });
            palette.ColorNames.Returns(new[] { Red, Blue });

            var navigation = Substitute.For<INavigation>();
            navigation.Current.Returns(Navigation);

            var flightScope = Substitute.For<IFlightScope>();
            flightScope.IsLoaded.Returns(FlightLoaded);

            var runScore = Substitute.For<IRunScore>();
            runScore.TotalScore.Returns(TotalScore);

            var dangerLevel = Substitute.For<IDangerLevel>();
            dangerLevel.Level.Returns(Danger);

            Pause = new PauseService(
                Substitute.For<IPublisher<PausedMessage>>(),
                Substitute.For<IPublisher<ResumedMessage>>());

            Service = new GameplayMetricsService(
                Capture<ProjectileLoadedMessage>(h => ProjectileLoaded = h),
                Capture<ProjectileFiredMessage>(h => ProjectileFired = h),
                Capture<ProjectileDestroyedMessage>(h => ProjectileDestroyed = h),
                Capture<ActorHitMessage>(h => ActorHit = h),
                Capture<PierceDischargedMessage>(h => PierceDischarged = h),
                Capture<SpeedTapMintedMessage>(h => SpeedTapMinted = h),
                Capture<ScoreTrailArrivedMessage>(h => TrailArrived = h),
                Capture<ScorePointsGroupMessage>(h => PointsGroup = h),
                Capture<StreakChangedMessage>(h => StreakChanged = h),
                Capture<WaveDamageMessage>(h => WaveDamage = h),
                Capture<StrikethroughArrivedMessage>(h => Strikethrough = h),
                Capture<ShieldGainedMessage>(h => ShieldGained = h),
                Capture<ShieldLostMessage>(h => ShieldLost = h),
                Capture<ItemActivatedMessage>(h => ItemActivated = h),
                Capture<BoardDepletedMessage>(h => BoardDepleted = h),
                Capture<ScoreLevelUpMessage>(h => ScoreLevelUp = h),
                Capture<LevelUpDismissedMessage>(h => LevelUpDismissed = h),
                Capture<LevelUpAbortedMessage>(h => LevelUpAborted = h),
                Capture<LevelUpAbandonedMessage>(h => LevelUpAbandoned = h),
                Capture<LevelTransitionCompletedMessage>(h => TransitionCompleted = h),
                Capture<GameOverMessage>(h => GameOver = h),
                Wired,
                new SessionTelemetryContext(),
                flightScope,
                FlightStats,
                HoldState,
                runScore,
                RetryState,
                dangerLevel,
                navigation,
                Pause,
                palette,
                () => Now);

            Service.Start();
        }

        public void Dispose()
        {
            Service.Dispose();
        }

        internal static IBalloonModel Balloon(BalloonType type, string color)
        {
            var balloon = Substitute.For<IBalloonModel, IHasColor>();
            balloon.TypeName.Returns(type);
            ((IHasColor)balloon).Color.Returns(new ReactiveProperty<string>(color));
            return balloon;
        }

        internal static IBalloonModel ItemBalloon(ItemType item)
        {
            var balloon = Substitute.For<IBalloonModel, IHasItemSlot>();
            balloon.TypeName.Returns(BalloonType.Simple);
            ((IHasItemSlot)balloon).Item.Returns(new ReactiveProperty<ItemType>(item));
            return balloon;
        }

        // A balloon with no item slot at all — ToughBalloonModel's shape. Must no-op, not throw.
        internal static IBalloonModel ItemlessBalloon()
        {
            var balloon = Substitute.For<IBalloonModel>();
            balloon.TypeName.Returns(BalloonType.Tough);
            return balloon;
        }

        // A hit that is not an IBalloonModel and carries no IHasColor — the "other" bucket case.
        internal static ActorHitMessage ColorlessHit(HitOutcome outcome)
        {
            return new ActorHitMessage(Substitute.For<BalloonParty.Slots.Actor.ISlotActor>(),
                UnityEngine.Vector3.zero, UnityEngine.Vector3.right, outcome,
                new DamageContext(1, DamageFlags.DirectHit));
        }

        internal static ActorHitMessage Hit(IBalloonModel balloon, HitOutcome outcome,
            DamageFlags flags = DamageFlags.DirectHit)
        {
            return new ActorHitMessage(balloon, UnityEngine.Vector3.zero, UnityEngine.Vector3.right, outcome,
                new DamageContext(1, flags));
        }

        // Drives the service out of Idle exactly the way the game does (R23): entry points start during
        // the Launcher's preload, and the clocks only start when navigation reports Game.
        internal void EnterGame()
        {
            Navigation.Value = NavigationState.Game;
        }

        // The full clean level boundary: ceremony, dismissal, then the transition that actually flushes.
        internal void CompleteLevel(int newLevel)
        {
            ScoreLevelUp.Handle(new ScoreLevelUpMessage(newLevel));
            LevelUpDismissed.Handle(new LevelUpDismissedMessage());
            TransitionCompleted.Handle(new LevelTransitionCompletedMessage());
        }

        internal TelemetryEnvelope LastOf(RecordKind kind)
        {
            for (var i = Sink.Written.Count - 1; i >= 0; i--)
            {
                if (Sink.Written[i].Kind == kind)
                {
                    return Sink.Written[i];
                }
            }

            throw new InvalidOperationException($"No {kind} record was written.");
        }

        internal List<TelemetryEnvelope> AllOf(RecordKind kind)
        {
            var matches = new List<TelemetryEnvelope>();
            foreach (var envelope in Sink.Written)
            {
                if (envelope.Kind == kind)
                {
                    matches.Add(envelope);
                }
            }

            return matches;
        }

        internal int CountOf(RecordKind kind)
        {
            var count = 0;
            foreach (var envelope in Sink.Written)
            {
                if (envelope.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        // Records every IDisposable handed back by Subscribe, so a test can assert the service's
        // CompositeDisposable actually took ownership of all of them — a subscription collected
        // anywhere else would simply never be disposed, silently.
        private ISubscriber<TMessage> Capture<TMessage>(Action<IMessageHandler<TMessage>> assign)
        {
            var subscription = Substitute.For<IDisposable>();
            Subscriptions.Add(subscription);

            var subscriber = Substitute.For<ISubscriber<TMessage>>();
            subscriber
                .Subscribe(Arg.Do<IMessageHandler<TMessage>>(h => assign(h)),
                    Arg.Any<MessageHandlerFilter<TMessage>[]>())
                .Returns(subscription);
            return subscriber;
        }
    }
}
