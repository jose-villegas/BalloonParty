using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Danger;
using BalloonParty.Game.Flight;
using BalloonParty.Game.Run;
using BalloonParty.Projectile.Controller;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game.Telemetry
{
    /// <summary>
    ///     Counts what happens during play and hands immutable snapshots to the popups and the sink.
    ///     Subscriber-only: it never publishes, never mutates gameplay state and never blocks a
    ///     boundary. See @ref plan_gameplay_telemetry for the requirement numbers cited below.
    /// </summary>
    internal sealed class GameplayMetricsService : IStartable, IDisposable, IRunResettable, ILevelMetricsView
    {
        private const string EndCauseGameOver = "GameOver";

        // IDangerLevel.Level is a 0..1 float and MetricSet stores ints, so the Max fold keeps it as
        // hundredths. Rounding to a bare 0/1 would make max_danger_level useless.
        private const int DangerLevelScale = 100;

        private static readonly BalloonType[] BalloonTypes = (BalloonType[])Enum.GetValues(typeof(BalloonType));

        private readonly ISubscriber<ProjectileLoadedMessage> _projectileLoadedSubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _projectileFiredSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _projectileDestroyedSubscriber;
        private readonly ISubscriber<ActorHitMessage> _actorHitSubscriber;
        private readonly ISubscriber<PierceDischargedMessage> _pierceDischargedSubscriber;
        private readonly ISubscriber<SpeedTapMintedMessage> _speedTapSubscriber;
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private readonly ISubscriber<ScorePointsGroupMessage> _pointsGroupSubscriber;
        private readonly ISubscriber<StreakChangedMessage> _streakSubscriber;
        private readonly ISubscriber<WaveDamageMessage> _waveDamageSubscriber;
        private readonly ISubscriber<StrikethroughArrivedMessage> _strikethroughSubscriber;
        private readonly ISubscriber<ShieldGainedMessage> _shieldGainedSubscriber;
        private readonly ISubscriber<ShieldLostMessage> _shieldLostSubscriber;
        private readonly ISubscriber<ItemActivatedMessage> _itemActivatedSubscriber;
        private readonly ISubscriber<BoardDepletedMessage> _boardDepletedSubscriber;
        private readonly ISubscriber<ScoreLevelUpMessage> _scoreLevelUpSubscriber;
        private readonly ISubscriber<LevelUpDismissedMessage> _levelUpDismissedSubscriber;
        private readonly ISubscriber<LevelUpAbortedMessage> _levelUpAbortedSubscriber;
        private readonly ISubscriber<LevelUpAbandonedMessage> _levelUpAbandonedSubscriber;
        private readonly ISubscriber<LevelTransitionCompletedMessage> _transitionCompletedSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;

        private readonly ITelemetrySink _sink;
        private readonly SessionTelemetryContext _sessionContext;
        private readonly IFlightScope _flightScope;
        private readonly IFlightStats _flightStats;
        private readonly IHoldSpeedUpState _holdState;
        private readonly IRunScore _runScore;
        private readonly IRetryState _retryState;
        private readonly IDangerLevel _dangerLevel;
        private readonly INavigation _navigation;
        private readonly PauseService _pauseService;

        // All ~21 subscriptions live here and are disposed as a unit — fourteen-plus individually named
        // IDisposable fields would fight the field-ordering rule and guarantee that one is eventually
        // forgotten in Dispose.
        private readonly CompositeDisposable _subscriptions = new();

        private readonly MetricScope _flightScopeMetrics;
        private readonly MetricScope _levelMetrics;
        private readonly MetricScope _runMetrics;
        private readonly MetricScope _sessionMetrics;

        // Iterated whenever a clock changes state: one loop over the scopes per transition, never one
        // call per scope per timer. Every scope runs its own clocks and Absorb never folds timers.
        private readonly MetricScope[] _scopes;

        private readonly Dictionary<string, int> _colorIndexByName;
        private readonly int _otherColorIndex;

        // Chain-scoped (R14a): how many times each level number has been opened since the last fresh
        // run. Cleared only when a reset arrives with RetryLevel == 0.
        private readonly Dictionary<int, int> _levelAttemptsByLevel = new();

        private readonly ReactiveProperty<LevelMetricsSnapshot> _ceremonyLevel;
        private readonly ReactiveProperty<LevelMetricsSnapshot> _lastFlushedLevel;
        private readonly ReactiveProperty<RunMetricsSnapshot> _runSnapshot;
        private readonly LevelMetricsSnapshot _emptyLevel;
        private readonly RunMetricsSnapshot _emptyRun;

        private LevelState _state = LevelState.Idle;
        private bool _paused;
        private bool _holding;
        private bool _holdEngagedThisFlight;
        private bool _runCheatActive;
        private bool _levelDirty;
        private int _runId = 1;
        private int _attemptId = 1;
        private int _attemptIndex;
        private int _levelAttemptOrdinal;
        private int _ceremonyLevelIndex;
        private int _pendingNewLevel;
        private int _flightIndexInLevel;
        private int _currentLevelIndex;

        public IReadOnlyReactiveProperty<LevelMetricsSnapshot> CeremonyLevel => _ceremonyLevel;

        public IReadOnlyReactiveProperty<LevelMetricsSnapshot> LastFlushedLevel => _lastFlushedLevel;

        public IReadOnlyReactiveProperty<RunMetricsSnapshot> Run => _runSnapshot;

        // Quiesce (0) must stay below RetryTracker's 110 (R21/RK-4) — ResetRun reads
        // IRetryState.RetryLevel, and that tracker zeroes it. GameplayMetricsServiceTests pins the
        // inequality.
        public int ResetOrder => RunResetOrder.Quiesce;

        [Inject]
        internal GameplayMetricsService(
            ISubscriber<ProjectileLoadedMessage> projectileLoadedSubscriber,
            ISubscriber<ProjectileFiredMessage> projectileFiredSubscriber,
            ISubscriber<ProjectileDestroyedMessage> projectileDestroyedSubscriber,
            ISubscriber<ActorHitMessage> actorHitSubscriber,
            ISubscriber<PierceDischargedMessage> pierceDischargedSubscriber,
            ISubscriber<SpeedTapMintedMessage> speedTapSubscriber,
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            ISubscriber<ScorePointsGroupMessage> pointsGroupSubscriber,
            ISubscriber<StreakChangedMessage> streakSubscriber,
            ISubscriber<WaveDamageMessage> waveDamageSubscriber,
            ISubscriber<StrikethroughArrivedMessage> strikethroughSubscriber,
            ISubscriber<ShieldGainedMessage> shieldGainedSubscriber,
            ISubscriber<ShieldLostMessage> shieldLostSubscriber,
            ISubscriber<ItemActivatedMessage> itemActivatedSubscriber,
            ISubscriber<BoardDepletedMessage> boardDepletedSubscriber,
            ISubscriber<ScoreLevelUpMessage> scoreLevelUpSubscriber,
            ISubscriber<LevelUpDismissedMessage> levelUpDismissedSubscriber,
            ISubscriber<LevelUpAbortedMessage> levelUpAbortedSubscriber,
            ISubscriber<LevelUpAbandonedMessage> levelUpAbandonedSubscriber,
            ISubscriber<LevelTransitionCompletedMessage> transitionCompletedSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ITelemetrySink sink,
            SessionTelemetryContext sessionContext,
            IFlightScope flightScope,
            IFlightStats flightStats,
            IHoldSpeedUpState holdState,
            IRunScore runScore,
            IRetryState retryState,
            IDangerLevel dangerLevel,
            INavigation navigation,
            PauseService pauseService,
            IGamePalette palette,
            Func<float> clock)
        {
            _projectileLoadedSubscriber = projectileLoadedSubscriber;
            _projectileFiredSubscriber = projectileFiredSubscriber;
            _projectileDestroyedSubscriber = projectileDestroyedSubscriber;
            _actorHitSubscriber = actorHitSubscriber;
            _pierceDischargedSubscriber = pierceDischargedSubscriber;
            _speedTapSubscriber = speedTapSubscriber;
            _trailArrivedSubscriber = trailArrivedSubscriber;
            _pointsGroupSubscriber = pointsGroupSubscriber;
            _streakSubscriber = streakSubscriber;
            _waveDamageSubscriber = waveDamageSubscriber;
            _strikethroughSubscriber = strikethroughSubscriber;
            _shieldGainedSubscriber = shieldGainedSubscriber;
            _shieldLostSubscriber = shieldLostSubscriber;
            _itemActivatedSubscriber = itemActivatedSubscriber;
            _boardDepletedSubscriber = boardDepletedSubscriber;
            _scoreLevelUpSubscriber = scoreLevelUpSubscriber;
            _levelUpDismissedSubscriber = levelUpDismissedSubscriber;
            _levelUpAbortedSubscriber = levelUpAbortedSubscriber;
            _levelUpAbandonedSubscriber = levelUpAbandonedSubscriber;
            _transitionCompletedSubscriber = transitionCompletedSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _sink = sink;
            _sessionContext = sessionContext;
            _flightScope = flightScope;
            _flightStats = flightStats;
            _holdState = holdState;
            _runScore = runScore;
            _retryState = retryState;
            _dangerLevel = dangerLevel;
            _navigation = navigation;
            _pauseService = pauseService;

            // Progress colours only (R12) — ColorNames also carries presentation-only tints that never
            // appear on a balloon, so indexing by it would leave permanent holes in every breakdown.
            var progressColors = palette.ProgressColorNames;
            _colorIndexByName = new Dictionary<string, int>(progressColors.Count);
            for (var i = 0; i < progressColors.Count; i++)
            {
                _colorIndexByName[progressColors[i]] = i;
            }

            // The trailing bucket catches rainbow, paint-converted and colourless actors, so an unknown
            // id can never throw or index out of range.
            _otherColorIndex = progressColors.Count;
            var colorAxisSize = progressColors.Count + 1;

            _flightScopeMetrics = MetricScope.Create(MetricScopeKind.Flight, colorAxisSize, clock);
            _levelMetrics = MetricScope.Create(MetricScopeKind.Level, colorAxisSize, clock);
            _runMetrics = MetricScope.Create(MetricScopeKind.Run, colorAxisSize, clock);
            _sessionMetrics = MetricScope.Create(MetricScopeKind.Session, colorAxisSize, clock);
            _scopes = new[] { _flightScopeMetrics, _levelMetrics, _runMetrics, _sessionMetrics };

            // R20: every consumer must render against an empty snapshot, so the read model is never
            // null — sealed here while the scopes are still pristine.
            _emptyLevel = _levelMetrics.Seal(0, false);
            _emptyRun = _runMetrics.Seal();
            _ceremonyLevel = new ReactiveProperty<LevelMetricsSnapshot>(_emptyLevel);
            _lastFlushedLevel = new ReactiveProperty<LevelMetricsSnapshot>(_emptyLevel);
            _runSnapshot = new ReactiveProperty<RunMetricsSnapshot>(_emptyRun);
        }

        // Subscriptions only. Clocks stay stopped until INavigation reports Game (R23) — entry points
        // start during the Launcher's additive preload, long before the player taps Play.
        public void Start()
        {
            _projectileLoadedSubscriber.Subscribe(OnProjectileLoaded).AddTo(_subscriptions);
            _projectileFiredSubscriber.Subscribe(OnProjectileFired).AddTo(_subscriptions);
            _projectileDestroyedSubscriber.Subscribe(OnProjectileDestroyed).AddTo(_subscriptions);
            _actorHitSubscriber.Subscribe(OnActorHit).AddTo(_subscriptions);
            _pierceDischargedSubscriber.Subscribe(OnPierceDischarged).AddTo(_subscriptions);
            _speedTapSubscriber.Subscribe(OnSpeedTapMinted).AddTo(_subscriptions);
            _trailArrivedSubscriber.Subscribe(OnScoreTrailArrived).AddTo(_subscriptions);
            _pointsGroupSubscriber.Subscribe(OnScorePointsGroup).AddTo(_subscriptions);
            _streakSubscriber.Subscribe(OnStreakChanged).AddTo(_subscriptions);
            _waveDamageSubscriber.Subscribe(OnWaveDamage).AddTo(_subscriptions);
            _strikethroughSubscriber.Subscribe(OnStrikethroughArrived).AddTo(_subscriptions);
            _shieldGainedSubscriber.Subscribe(OnShieldGained).AddTo(_subscriptions);
            _shieldLostSubscriber.Subscribe(OnShieldLost).AddTo(_subscriptions);
            _itemActivatedSubscriber.Subscribe(OnItemActivated).AddTo(_subscriptions);
            _boardDepletedSubscriber.Subscribe(OnBoardDepleted).AddTo(_subscriptions);
            _scoreLevelUpSubscriber.Subscribe(OnScoreLevelUp).AddTo(_subscriptions);
            _levelUpDismissedSubscriber.Subscribe(OnLevelUpDismissed).AddTo(_subscriptions);
            _levelUpAbortedSubscriber.Subscribe(OnCeremonyLeft).AddTo(_subscriptions);
            _levelUpAbandonedSubscriber.Subscribe(OnCeremonyLeft).AddTo(_subscriptions);
            _transitionCompletedSubscriber.Subscribe(OnLevelTransitionCompleted).AddTo(_subscriptions);
            _gameOverSubscriber.Subscribe(OnGameOver).AddTo(_subscriptions);

            _navigation.Current.Subscribe(OnNavigationChanged).AddTo(_subscriptions);

            // IsAnyPaused, never PausedMessage/ResumedMessage edges (guardrail 3): PauseService.ResetRun
            // clears its source stack WITHOUT publishing a resume, so an edge counter leaks depth on
            // every loss and freezes every later timer at zero. The concrete PauseService is injected on
            // purpose — it has no read interface and is registered AsSelf.
            _pauseService.IsAnyPaused.Subscribe(OnPauseChanged).AddTo(_subscriptions);

            _dangerLevel.Level.Subscribe(OnDangerLevelChanged).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();

            // Completing them releases W4's bindings; leaving them open holds every popup that bound to
            // this service alive against a dead instance across a scene reload.
            _ceremonyLevel.Dispose();
            _lastFlushedLevel.Dispose();
            _runSnapshot.Dispose();
        }

        // Invoked directly by RunController's cascade — RunResetMessage is deliberately NOT subscribed
        // (guardrail 9), because both would double-reset.
        public void ResetRun(int generation)
        {
            // Read before RetryTracker (ResetOrder 110) zeroes it. A retry is a continuation of one run
            // chain: the score carries over and only the attempt changes (R14).
            var retryLevel = _retryState.RetryLevel;

            _attemptId = generation;
            if (retryLevel > 0)
            {
                _attemptIndex++;
            }
            else
            {
                _runId++;
                _attemptIndex = 0;
                _levelAttemptsByLevel.Clear();
            }

            _flightScopeMetrics.Reset();
            _levelMetrics.Reset();
            _runMetrics.Reset();
            _runMetrics.SetLast(MetricId.RetriesUsed, _attemptIndex);

            _runCheatActive = false;
            _holding = false;
            _holdEngagedThisFlight = false;
            _ceremonyLevelIndex = 0;
            _pendingNewLevel = 0;
            _ceremonyLevel.Value = _emptyLevel;
            _lastFlushedLevel.Value = _emptyLevel;
            _runSnapshot.Value = _emptyRun;

            // LevelController resets at order 100, so ILevelProgress.Level is still the *previous* run's
            // value here. RetryLevel is the authoritative answer instead: it is the level the restart
            // begins at, and a fresh chain begins at the cheat's start level (mirroring
            // LevelController.ClearRunState) with an empty attempt table anyway.
            OpenLevel(retryLevel > 0 ? retryLevel : FreshRunStartLevel());

            _state = LevelState.Playing;
            RefreshClocks();
        }

        private static int FreshRunStartLevel()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
            return Mathf.Max(1, BalloonParty.Cheats.CheatState.StartLevel);
#else
            return 1;
#endif
        }

        // Triple guard with an #else (R28c, guardrail 6): dotnet build defines UNITY_EDITOR, so a
        // missing #else compiles here and breaks only the real release build. "Any flag active" needs
        // AnyCheatUsed because StartLevel/TimeOfDaySpeedScale default to 1 and one-shot cheats leave no
        // trace at all.
        private static bool ReadCheatActive()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
            return BalloonParty.Cheats.CheatState.AnyCheatUsed
                || BalloonParty.Cheats.CheatState.BlockLevelUp
                || BalloonParty.Cheats.CheatState.InstantScoreTrails
                || BalloonParty.Cheats.CheatState.StartLevel != 1
                || !Mathf.Approximately(BalloonParty.Cheats.CheatState.TimeOfDaySpeedScale, 1f);
#else
            return false;
#endif
        }

        private void OnNavigationChanged(NavigationState state)
        {
            if (state != NavigationState.Game || _state != LevelState.Idle)
            {
                return;
            }

            OpenLevel(FreshRunStartLevel());
            _state = LevelState.Playing;
            RefreshClocks();
        }

        private void OnPauseChanged(bool paused)
        {
            _paused = paused;
            RefreshClocks();
        }

        private void OnDangerLevelChanged(float level)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelMetrics.RecordMax(MetricId.MaxDangerLevel, Mathf.RoundToInt(level * DangerLevelScale));
        }

        private void OnProjectileLoaded(ProjectileLoadedMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            // The flight scope is reset here rather than at the seal so the [Destroyed, next Loaded)
            // interflight window cannot alias the previous flight (R2).
            _flightScopeMetrics.Reset();
            _holdEngagedThisFlight = false;
            _flightIndexInLevel++;
            _levelMetrics.Increment(MetricId.FlightsStarted);
        }

        private void OnProjectileFired(ProjectileFiredMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            SampleHold();
            _levelMetrics.Increment(MetricId.ShotsFired);
        }

        private void OnProjectileDestroyed(ProjectileDestroyedMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            SampleHold();
            SealFlight();
        }

        private void OnActorHit(ActorHitMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            SampleHold();

            var scope = ActiveScope();

            if ((message.Outcome & HitOutcome.Pop) != 0)
            {
                scope.Increment(MetricId.Pops);
                scope.IncrementAxis(MetricId.Pops, MetricAxis.Color, ColorIndexOf(message.Actor));

                // Inside a flight the balloon-type breakdown has exactly one owner (FlightStatsService,
                // folded in at the seal); outside one there is no flight to seal, so it is counted here.
                if (scope.Scope != MetricScopeKind.Flight && message.Actor is IBalloonModel popped)
                {
                    scope.IncrementAxis(MetricId.Pops, MetricAxis.BalloonType, (int)popped.TypeName);
                }

                // Accuracy is shots vs DIRECT hits (R13) — total pops includes item-, cheat- and
                // board-driven pops, which would make every accuracy figure meaningless.
                if ((message.Context.Flags & DamageFlags.DirectHit) != 0)
                {
                    scope.Increment(MetricId.DirectHitPops);
                }

                return;
            }

            if ((message.Outcome & HitOutcome.Deflect) != 0)
            {
                scope.Increment(MetricId.Deflects);
                if (scope.Scope != MetricScopeKind.Flight && message.Actor is IBalloonModel deflected)
                {
                    scope.IncrementAxis(MetricId.Deflects, MetricAxis.BalloonType, (int)deflected.TypeName);
                }

                return;
            }

            // The one outcome family where two values share a metric, hence a mask rather than the
            // repo's usual == comparison.
            if ((message.Outcome & (HitOutcome.Absorb | HitOutcome.PassThrough)) != 0)
            {
                scope.Increment(MetricId.Absorbs);
            }
        }

        private void OnPierceDischarged(PierceDischargedMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            SampleHold();

            // PierceDischarges itself is folded from IFlightStats at the seal — only the payload-derived
            // counters are read off the message.
            var scope = ActiveScope();
            scope.Add(MetricId.PierceToughsCleared, message.ToughCount);
            if (message.IsRainbow)
            {
                scope.Increment(MetricId.RainbowPierceDischarges);
            }
        }

        private void OnSpeedTapMinted(SpeedTapMintedMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            SampleHold();
            ActiveScope().Increment(MetricId.SpeedTapsMinted);
            _levelMetrics.RecordMax(MetricId.MaxSpeedTapsInFlight, message.TotalTaps);
        }

        private void OnScoreTrailArrived(ScoreTrailArrivedMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            // Stragglers arriving between the dismissal and the transition belong to the level that just
            // ended — which is exactly why the flush is on LevelTransitionCompletedMessage (R3).
            _levelMetrics.Add(MetricId.PointsBanked, message.Points);
            _levelMetrics.AddAxis(MetricId.PointsBanked, MetricAxis.Color, ColorIndexOf(message.ColorName),
                message.Points);
        }

        private void OnScorePointsGroup(ScorePointsGroupMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            _levelMetrics.RecordMax(MetricId.MaxMultiplier, message.Multiplier);
        }

        private void OnStreakChanged(StreakChangedMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            _levelMetrics.RecordMax(MetricId.MaxStreak, message.Streak);
            if (message.Streak == 0)
            {
                _levelMetrics.Increment(MetricId.StreakBreaks);
            }
        }

        private void OnWaveDamage(WaveDamageMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            _levelMetrics.Add(MetricId.HeartsLost, message.HeartsLost);
            _levelMetrics.RecordMax(MetricId.MaxHeartsLostInWave, message.HeartsLost);
            _levelMetrics.Add(MetricId.BlockedSlots, message.BlockedSlots);
        }

        private void OnStrikethroughArrived(StrikethroughArrivedMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            _levelMetrics.Increment(MetricId.Strikethroughs);
        }

        private void OnShieldGained(ShieldGainedMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            _levelMetrics.Increment(MetricId.ShieldsGained);
        }

        private void OnShieldLost(ShieldLostMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            _levelMetrics.Increment(MetricId.ShieldsSpent);
        }

        private void OnItemActivated(ItemActivatedMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            _levelMetrics.Increment(MetricId.ItemsActivated);

            // Same guarded cast ItemSoundRouter uses — a non-item-eligible balloon (ToughBalloonModel)
            // no-ops instead of throwing. The message is published after the handler ran, so
            // ItemType.None buckets like any other value.
            if (message.Balloon is IHasItemSlot slot)
            {
                _levelMetrics.IncrementAxis(MetricId.ItemsActivated, MetricAxis.ItemType, (int)slot.Item.Value);
            }
        }

        private void OnBoardDepleted(BoardDepletedMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            _levelDirty = true;

            _levelMetrics.RecordMax(MetricId.BoardCleared, 1);
        }

        private void OnScoreLevelUp(ScoreLevelUpMessage message)
        {
            if (_state != LevelState.Playing)
            {
                return;
            }

            _pendingNewLevel = message.NewLevel;

            // Never an internal counter: CheatState.StartLevel lets a dev run start anywhere, so a
            // counter drifts from the real level within one shot.
            _ceremonyLevelIndex = message.NewLevel - 1;

            // R18: the popup shows PROJECTED points beside these stats, and ScoreController has already
            // snapped TotalScore to the projected total by now — reading banked here would contradict
            // the number rendered next to it.
            _levelMetrics.SetLast(MetricId.PointsProjected, _runScore.TotalScore.Value);

            _state = LevelState.Ceremony;
            RefreshClocks();

            // The first of the two snapshots per level (R17): what the popup renders. The flush snapshot
            // taken at LevelTransitionCompletedMessage is what the sink gets; the gap between them is
            // data, not a bug.
            var snapshot = _levelMetrics.Seal(_ceremonyLevelIndex, true);
            _ceremonyLevel.Value = snapshot;
            Log.Info("Telemetry", $"Ceremony snapshot — level {_ceremonyLevelIndex}, " +
                $"pops {snapshot[MetricId.Pops]}, banked {snapshot[MetricId.PointsBanked]}, " +
                $"projected {snapshot[MetricId.PointsProjected]}, " +
                $"gameplay {snapshot[TimerId.Gameplay]:0.0}s");
        }

        private void OnLevelUpDismissed(LevelUpDismissedMessage message)
        {
            if (_state != LevelState.Ceremony)
            {
                return;
            }

            _state = LevelState.Transitioning;
            RefreshClocks();
        }

        // Both ceremony aborts (R8/R8a). LevelUpAbandonedMessage is a NESTED re-publish —
        // LevelController.AbandonCeremony publishes it synchronously from inside both the
        // LevelUpAbortedMessage handler and the GameOverMessage handler — so this must be idempotent and
        // must never enter Ended. Had it done so, the abort path would swallow the Aborted (wedging the
        // gameplay clock forever) and the loss path would swallow the outer GameOver (so no run record
        // would ever be written for a lost run). GameOverMessage alone owns the terminal transition.
        private void OnCeremonyLeft<TMessage>(TMessage message)
        {
            if (_state != LevelState.Ceremony)
            {
                return;
            }

            _state = LevelState.Playing;
            _ceremonyLevelIndex = 0;
            _pendingNewLevel = 0;

            // Written when the ceremony opened (R18). Left behind, the eventual record for this level
            // would carry a points_projected from a level-up that never happened.
            _levelMetrics.SetLast(MetricId.PointsProjected, 0);

            // RK-9: a stale ceremony snapshot would let the next popup render the aborted level.
            _ceremonyLevel.Value = _emptyLevel;
            RefreshClocks();
        }

        private void OnLevelTransitionCompleted(LevelTransitionCompletedMessage message)
        {
            // A transition-completed without a preceding ceremony (or a second one) neither flushes nor
            // re-opens a level, so no phantom empty record is ever emitted.
            if (_state != LevelState.Transitioning)
            {
                return;
            }

            var completedIndex = _ceremonyLevelIndex;
            var nextLevel = _pendingNewLevel;

            FlushLevel(completedIndex, true, null);

            _ceremonyLevelIndex = 0;
            _pendingNewLevel = 0;
            OpenLevel(nextLevel);
            _state = LevelState.Playing;
            RefreshClocks();
        }

        private void OnGameOver(GameOverMessage message)
        {
            if (_state == LevelState.Ended)
            {
                return;
            }

            // A loss deferred by the level-up gate is re-fired from RunController.OnNavigationChanged,
            // which LevelController drives from inside its OWN LevelTransitionCompletedMessage handler —
            // so on that path the GameOver below lands nested, before this service has seen the
            // transition at all. Left unhandled, the level the player actually completed would be
            // flushed under the NEXT level's index and marked incomplete, and levels_completed would
            // undercount. Settling the boundary here rather than reordering the registration keeps the
            // outcome independent of who subscribed first.
            if (_state == LevelState.Transitioning)
            {
                var completedIndex = _ceremonyLevelIndex;
                var nextLevel = _pendingNewLevel;

                FlushLevel(completedIndex, true, null);

                _ceremonyLevelIndex = 0;
                _pendingNewLevel = 0;
                OpenLevel(nextLevel);
            }

            // The fatal flight is still airborne on the ordinary death (hearts reach zero from a wave
            // while the projectile is mid-air), and its ProjectileDestroyedMessage arrives during the
            // loss cinematic — after Ended, where OnProjectileDestroyed returns early. Without this the
            // most interesting flight of the run is discarded whole: its pops, deflects, absorbs, speed
            // taps, pierce clears and the entire FlightStats breakdown.
            if (_flightScope.IsLoaded.Value)
            {
                SealFlight();
            }

            // FinalLevel, not FinalLevel - 1. The two sources are offset from each other:
            // ScoreLevelUpMessage.NewLevel is the level being *entered*, so NewLevel - 1 names the one
            // just completed; GameOverMessage.FinalLevel is the level being *played*, which is the one
            // this partial record is about. Subtracting here would file a death on level 2 under the
            // same index as completed-level-1 — two different levels in one bucket, and the levels-
            // reached distribution silently wrong.
            var levelIndex = message.FinalLevel;

            // A loss deferred by the level-up gate fires the moment play resumes
            // (RunController.OnNavigationChanged), i.e. immediately after the transition that opened this
            // level — so the level can genuinely be untouched here. Emitting it would put a phantom
            // all-zero record in the stream and inflate every per-level distribution's zero bucket.
            if (_levelDirty)
            {
                FlushLevel(levelIndex, false, EndCauseGameOver);
            }
            else
            {
                // FlushLevel is where the sticky run tag is normally refreshed; the skipped-record path
                // must still see a cheat toggled during this level.
                _runCheatActive |= ReadCheatActive();
            }

            // R14b/RK-3b: the run total is the authoritative final score, never the sum of the level
            // records — a retry replays a level, so the chain holds two records for it and summing
            // double-counts.
            //
            // Its own metric, deliberately, not an override of PointsBanked. PointsBanked is a Sum
            // metric carrying a Color axis, so the roll-up has already built both its scalar and its
            // per-colour buckets by now; overwriting only the scalar would ship a record whose
            // points_banked disagreed with the sum of its own points_banked_color — guaranteed on any
            // retried chain, and unexplainable to whoever reconciles them downstream.
            _runMetrics.SetLast(MetricId.TotalScore, message.FinalScore);

            // Entered before the run is sealed so the run's own clocks stop here (R9): the loss
            // cinematic completes straggler score trails after this point and must not reach the run.
            _state = LevelState.Ended;
            RefreshClocks();

            // Dying during the ceremony leaves the popup's snapshot live. LevelController does publish
            // LevelUpAbandonedMessage from its own game-over handler, which would clear it via
            // OnCeremonyLeft — but only if that handler runs after this one, so clear it here too and
            // stop the invariant depending on subscription order.
            _ceremonyLevelIndex = 0;
            _pendingNewLevel = 0;
            _ceremonyLevel.Value = _emptyLevel;

            _runSnapshot.Value = _runMetrics.Seal();
            WriteEnvelope(RecordKind.Run, _runSnapshot.Value, levelIndex, _levelAttemptOrdinal, false,
                _runCheatActive, EndCauseGameOver);

            // The Session scope accumulates (R1) even though nothing flushes it yet — RecordKind.Session
            // is reserved and no SessionMetricsSnapshot exists. Folding here rather than leaving the
            // outermost scope permanently empty keeps the four-scope hierarchy real.
            _sessionMetrics.Absorb(_runMetrics);
        }

        // R14a: the ordinal increments when the level OPENS, not at flush, so a level abandoned at game
        // over still records the attempt it was. AttemptIndex alone is wrong per level — after a retry
        // at level 7, levels 8+ carry AttemptIndex 1 but are first attempts.
        private void OpenLevel(int levelNumber)
        {
            _levelDirty = false;
            _flightIndexInLevel = 0;
            _currentLevelIndex = levelNumber;
            _levelAttemptsByLevel.TryGetValue(levelNumber, out var played);
            played++;
            _levelAttemptsByLevel[levelNumber] = played;
            _levelAttemptOrdinal = played;
        }

        private void FlushLevel(int levelIndex, bool completed, string endCause)
        {
            // Evaluated at flush time, and sticky for the run: once any level is tagged the run stays
            // tagged. Records are never dropped — the tag is for filtering.
            var cheatActive = ReadCheatActive();
            _runCheatActive |= cheatActive;

            var snapshot = _levelMetrics.Seal(levelIndex, completed);
            _lastFlushedLevel.Value = snapshot;

            WriteEnvelope(RecordKind.Level, snapshot, levelIndex, _levelAttemptOrdinal, completed, cheatActive,
                endCause);

            _runMetrics.Absorb(_levelMetrics);

            // Run-scoped, so Absorb skips it (folding a Level child's zero into a Last metric is exactly
            // what the catalog's Scope column exists to prevent) — it is derived here instead.
            if (completed)
            {
                _runMetrics.Add(MetricId.LevelsCompleted, 1);
            }

            _levelMetrics.Reset();
        }

        private void SealFlight()
        {
            // Per-flight balloon-type counts, wall bounces and pierce discharges have exactly one owner
            // (FlightStatsService, written by HitPipeline before the publish). Metrics folds them in at
            // the seal rather than recounting them — RK-2a is two owners drifting apart.
            for (var i = 0; i < BalloonTypes.Length; i++)
            {
                var type = BalloonTypes[i];

                var pops = _flightStats.PopsOf(type);
                if (pops > 0)
                {
                    _flightScopeMetrics.AddAxis(MetricId.Pops, MetricAxis.BalloonType, (int)type, pops);
                }

                var deflects = _flightStats.DeflectsOf(type);
                if (deflects > 0)
                {
                    _flightScopeMetrics.AddAxis(MetricId.Deflects, MetricAxis.BalloonType, (int)type, deflects);
                }
            }

            var wallBounces = _flightStats.WallBounces;
            _flightScopeMetrics.Add(MetricId.WallBounces, wallBounces);
            _flightScopeMetrics.Add(MetricId.PierceDischarges, _flightStats.PierceDischarges);

            _levelMetrics.RecordMax(MetricId.MaxWallBouncesInFlight, wallBounces);
            if (_holdEngagedThisFlight)
            {
                _levelMetrics.Increment(MetricId.HoldSpeedUpFlights);
            }

            // Before the fold, while the scope still holds this flight alone. Its own clocks are the
            // time of flight: OnProjectileLoaded resets the scope, and Reset() zeroes the stopwatches
            // too, so gameplay_seconds here is [Loaded, Destroyed) minus any pause.
            WriteEnvelope(RecordKind.Flight, _flightScopeMetrics.SealFlight(_flightIndexInLevel),
                _currentLevelIndex, _levelAttemptOrdinal, false, _runCheatActive, null,
                _flightIndexInLevel);

            _levelMetrics.Absorb(_flightScopeMetrics);
            _flightScopeMetrics.Reset();
            _holdEngagedThisFlight = false;
        }

        // R2's [Loaded, Destroyed) window, read off its single owner rather than re-derived: increments
        // in the interflight window belong to the Level and to no flight.
        private MetricScope ActiveScope()
        {
            return _flightScope.IsLoaded.Value ? _flightScopeMetrics : _levelMetrics;
        }

        // R11/R30: HoldSpeedUpController publishes nothing and metrics owns no tick, so the hold state
        // is sampled on the message boundaries the service already observes. hold_seconds is therefore
        // accurate to the gap between two such messages, not to the frame.
        private void SampleHold()
        {
            var holding = _holdState.IsHolding;
            if (holding)
            {
                _holdEngagedThisFlight = true;
            }

            if (holding == _holding)
            {
                return;
            }

            _holding = holding;
            RefreshClocks();
        }

        // One loop over the scopes per transition — never one call per scope per timer. Each scope owns
        // its own instance of each clock and measures its own window, which is why Absorb never folds
        // timers (a Run scope's Gameplay stopwatch has already measured the whole run).
        private void RefreshClocks()
        {
            var counting = _state != LevelState.Idle && _state != LevelState.Ended;

            // Ceremony is deliberately NOT pause-gated: the ceremony IS a pause (LevelUpCinematic holds
            // PauseSource.Cinematic and LevelUpPopUp holds PauseSource.LevelUp for its whole duration),
            // so a gated ceremony clock would read ~0. Wall is total elapsed including ceremony and
            // transition; every PauseSource today is gameplay- or ceremony-owned, not a user
            // interruption, so gating Wall would collapse it toward the gameplay duration.
            var gameplay = _state == LevelState.Playing && !_paused;
            var ceremony = _state == LevelState.Ceremony;
            var hold = counting && _holding;

            for (var i = 0; i < _scopes.Length; i++)
            {
                var scope = _scopes[i];
                SetRunning(scope.Timer(TimerId.Gameplay), gameplay);
                SetRunning(scope.Timer(TimerId.Ceremony), ceremony);
                SetRunning(scope.Timer(TimerId.Wall), counting);
                SetRunning(scope.Timer(TimerId.Hold), hold);
            }
        }

        private void WriteEnvelope(RecordKind kind, ISealedMetrics metrics, int levelIndex, int levelAttemptOrdinal,
            bool completed, bool cheatActive, string endCause, int flightIndex = 0)
        {
            // Defence in depth (guardrail 8, RK-5): TelemetrySinkBase guards its own side, and this
            // guards ours. MessagePipe runs handlers synchronously on the publisher's stack with no
            // exception isolation, and both flush points publish before releasing critical state — an
            // escaping exception freezes the game. Metrics data is never worth a soft-lock.
            try
            {
                _sink.Write(new TelemetryEnvelope(
                    kind,
                    _sessionContext.SchemaVersion,
                    _sessionContext.SessionId,
                    _runId,
                    _attemptId,
                    _attemptIndex,
                    levelIndex,
                    levelAttemptOrdinal,
                    flightIndex,
                    completed,
                    cheatActive,
                    endCause,
                    DateTime.UtcNow.Ticks,
                    metrics));
            }
            catch (Exception exception)
            {
                Log.Warn("Telemetry", $"Sink threw writing a {kind} record — dropped. {exception.Message}");
            }
        }

        private int ColorIndexOf(ISlotActor actor)
        {
            return actor is IHasColor colored ? ColorIndexOf(colored.Color.Value) : _otherColorIndex;
        }

        private int ColorIndexOf(string colorName)
        {
            return colorName != null && _colorIndexByName.TryGetValue(colorName, out var index)
                ? index
                : _otherColorIndex;
        }

        private static void SetRunning(TelemetryStopwatch stopwatch, bool running)
        {
            if (running)
            {
                stopwatch.Resume();
            }
            else
            {
                stopwatch.Pause();
            }
        }

        // Idle -> Playing on INavigation == Game; Ceremony is entered by ScoreLevelUpMessage and left by
        // dismissal (Transitioning) or abort/abandon (Playing, idempotent); Ended is entered by
        // GameOverMessage alone and left only by ResetRun.
        private enum LevelState
        {
            Idle,
            Playing,
            Ceremony,
            Transitioning,
            Ended
        }
    }
}
