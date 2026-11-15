using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Assist;
using BalloonParty.Game.Danger;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Game.Score.Behaviours;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>
    ///     Brings balloon lines in on a timer, whatever the player does: one line per
    ///     <see cref="ISpawnClockSettings.IntervalForLevel" /> of scaled game time (shorter on an extra-colour
    ///     level, <see cref="ILevelParameters.SpawnIntervalScale" />), after a grace at each level's start.
    ///     Scaled, so slow-mo slows it with everything else. It stops outright through
    ///     pauses, the level-up and game over, and while a lone doomed ball glides its last breath: lines
    ///     landing in that frozen moment read wrong, and the board must hold still under the glide.
    ///     A sparse board runs the clock faster (<see cref="ISpawnClockSettings.SparseBoardSpeedup" />), so
    ///     clearing it never leaves the player waiting on an empty screen. Balls in the air push it too, but only
    ///     to refill a board below its normal fill, in proportion to how far below: firing while another ball
    ///     still flies pulls the next line closer (<see cref="ISpawnClockSettings.OverlapShotPull" />), and
    ///     every ball's flight time speeds the clock (<see cref="ISpawnClockSettings.FlightSpeedup" />), so the
    ///     board refills while long ricochets and rapid fire clear it. On a board at its fill they add nothing:
    ///     a crowded board needs more balls in the air, and those must not bring its lines sooner. A big clear in a short window
    ///     (<see cref="ISpawnClockSettings.BreatherClear" />) earns a breather instead: the clock drops to
    ///     <see cref="ISpawnClockSettings.BreatherPace" /> and eases back to its full pace, so the release
    ///     after a great shot lasts a moment before the tension builds again. A cooldown after each
    ///     (<see cref="ISpawnClockSettings.BreatherCooldownSeconds" />) keeps releases rarer than the clears.
    ///     A board about to cost the last heart earns one too (<see cref="ISpawnClockSettings.BreatherDanger" />).
    ///     Pressure pushes back on a comfortable player: while the board stays under
    ///     <see cref="ISpawnClockSettings.PressureHoldOccupancy" /> and the player is not in need, it builds toward
    ///     <see cref="ISpawnClockSettings.PressureSpeedup" />, so a strong build is pushed until its board climbs. It
    ///     holds above that line and partly goes when a line costs a heart.
    /// </summary>
    internal sealed class SpawnClock : IStartable, ITickable, IRunResettable, IDisposable, ISpawnClock
    {
        private readonly ISpawnClockSettings _settings;
        private readonly ILevelProgress _levelProgress;
        private readonly PauseService _pauseService;
        private readonly IProjectileRegistry _projectiles;
        private readonly IBoardFill _boardFill;
        private readonly IPublisher<SpawnBalloonLineMessage> _linePublisher;
        private readonly IActiveLevelParameters _levelParams;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly IScoreShapeCover _shapeCover;
        private readonly IProjectileFlightConfig _flightConfig;
        private readonly IDangerLevel _danger;
        private readonly IPlayerNeed _need;
        private readonly ISubscriber<WaveDamageMessage> _damageSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        // Each in-flight ball's flight time while the clock runs; dropped when the ball retires.
        private readonly Dictionary<IProjectileModel, float> _flightAges = new();

        // The current wait (a grace or an interval) and how much of it is left.
        private float _window;
        private float _remaining;
        private float _secondsOnLevel;

        // The highest recent occupancy, falling at the rate that drains BreatherClear in one window, so
        // peak minus now reads how much the board lost within about that window.
        private float _recentPeakOccupancy;
        private float _breatherLeft;
        private float _cooldownLeft;
        private float _pressure;

        public int ResetOrder => RunResetOrder.Counters;
        public float NextLineProgress => _window > 0f ? 1f - _remaining / _window : 0f;
        public float SecondsOnLevel => _secondsOnLevel;
        public float BreatherSecondsLeft => _breatherLeft;
        public float Pressure => _pressure;

        [Inject]
        internal SpawnClock(
            ILevelPacingConfiguration pacing,
            ILevelProgress levelProgress,
            PauseService pauseService,
            IProjectileRegistry projectiles,
            IBoardFill boardFill,
            IPublisher<SpawnBalloonLineMessage> linePublisher,
            ISubscriber<ProjectileFiredMessage> firedSubscriber,
            IActiveLevelParameters levelParams,
            IScoreShapeCover shapeCover,
            IProjectileFlightConfig flightConfig,
            IDangerLevel danger,
            IPlayerNeed need,
            ISubscriber<WaveDamageMessage> damageSubscriber)
        {
            _danger = danger;
            _need = need;
            _damageSubscriber = damageSubscriber;
            _levelParams = levelParams;
            _shapeCover = shapeCover;
            _flightConfig = flightConfig;
            _settings = pacing.SpawnClock;
            _levelProgress = levelProgress;
            _pauseService = pauseService;
            _projectiles = projectiles;
            _boardFill = boardFill;
            _linePublisher = linePublisher;
            _firedSubscriber = firedSubscriber;
            StartWindow(_settings.GraceSeconds);
        }

        public void Start()
        {
            // Skip the current value: construction already started level 1's grace.
            _levelProgress.Level.Skip(1).Subscribe(_ => StartLevel()).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(OnFired).AddTo(_subscriptions);
            _projectiles.Retired.Subscribe(ball => _flightAges.Remove(ball)).AddTo(_subscriptions);
            _damageSubscriber.Subscribe(OnWaveDamage).AddTo(_subscriptions);
        }

        public void Tick()
        {
            Advance(Time.deltaTime);
        }

        public void ResetRun(int generation)
        {
            _flightAges.Clear();
            _pressure = 0f;
            StartLevel();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // Internal for tests: Time.deltaTime isn't injectable.
        internal void Advance(float deltaTime)
        {
            if (!IsRunning())
            {
                return;
            }

            _secondsOnLevel += deltaTime;
            var flightSeconds = AgeFlights(deltaTime);
            TrackClears(deltaTime);
            BuildPressure(deltaTime);
            _remaining -= deltaTime * Mathf.Lerp(_settings.BreatherPace, 1f, BreatherBlend()) * Pace(flightSeconds)
                          * Mathf.Lerp(1f, _settings.PressureSpeedup, _pressure);
            if (_remaining > 0f)
            {
                return;
            }

            // One line per tick, however long the frame: a hitch must not land a backlog of lines at once.
            StartWindow(LineInterval());
            _linePublisher.Publish(new SpawnBalloonLineMessage(1));
        }

        // The pull only shortens the wait; the next Tick lands the line, so it obeys the same holds as the timer.
        private void OnFired(ProjectileFiredMessage msg)
        {
            if (AnotherBallInFlight(msg.Projectile))
            {
                _remaining -= _window * _settings.OverlapShotPull * (1f - BoardFill())
                              * Mathf.Lerp(_settings.BreatherPace, 1f, BreatherBlend());
            }
        }

        // The fired ball is already in flight when its message goes out, so it's skipped.
        private bool AnotherBallInFlight(IProjectileModel fired)
        {
            for (var i = 0; i < _projectiles.InFlightCount; i++)
            {
                if (_projectiles.InFlightAt(i) != fired)
                {
                    return true;
                }
            }

            return false;
        }

        // Ages every ball in the air and returns their summed flight seconds.
        private float AgeFlights(float deltaTime)
        {
            var total = 0f;
            for (var i = 0; i < _projectiles.InFlightCount; i++)
            {
                var ball = _projectiles.InFlightAt(i);
                _flightAges.TryGetValue(ball, out var age);
                age += deltaTime;
                _flightAges[ball] = age;
                total += age;
            }

            return total;
        }

        // The board sets the base: full speedup on an empty board, easing to normal pace as it reaches its fill.
        // Flight adds on top in proportion to the room left below that fill, so it refills what the balls cleared
        // and never rushes a board that is already full.
        private float Pace(float flightSeconds)
        {
            var fill = BoardFill();
            var boardPace = Mathf.Lerp(_settings.SparseBoardSpeedup, 1f, fill);
            var flightPressure = Mathf.Clamp01(flightSeconds / _settings.FlightPressureSeconds);
            return boardPace + (_settings.FlightSpeedup - 1f) * flightPressure * (1f - fill);
        }

        // 0 on an empty board to 1 once it is at both the level's starting fill and the target occupancy
        // (blended by OccupancyWeight).
        private float BoardFill()
        {
            var occupancy = Mathf.Clamp01(_boardFill.Occupancy / _settings.TargetOccupancy);
            return Mathf.Clamp01(Mathf.Lerp(_boardFill.StartingFill, occupancy, _settings.OccupancyWeight));
        }

        // Counts down a running breather and the cooldown after it, or starts one when the board has lost
        // BreatherClear of its open slots within about the window, when score shapes cover BreatherShapeCover
        // of the play area, or when the danger reaches BreatherDanger. The peak follows the board through the
        // breather and the cooldown, so no clear made then can start one once they end.
        private void TrackClears(float deltaTime)
        {
            var occupancy = _boardFill.Occupancy;
            if (_breatherLeft > 0f)
            {
                _breatherLeft = Mathf.Max(0f, _breatherLeft - deltaTime);
                if (_breatherLeft <= 0f)
                {
                    _cooldownLeft = _settings.BreatherCooldownSeconds;
                }

                _recentPeakOccupancy = occupancy;
                return;
            }

            if (_cooldownLeft > 0f)
            {
                _cooldownLeft = Mathf.Max(0f, _cooldownLeft - deltaTime);
                _recentPeakOccupancy = occupancy;
                return;
            }

            if (ShapesCoverTheBoard() || InDanger())
            {
                StartBreather(occupancy);
                return;
            }

            var clear = _settings.BreatherClear;
            if (clear <= 0f)
            {
                return;
            }

            var fall = deltaTime * clear / _settings.BreatherClearWindow;
            _recentPeakOccupancy = Mathf.Max(occupancy, _recentPeakOccupancy - fall);
            if (_recentPeakOccupancy - occupancy >= clear)
            {
                StartBreather(occupancy);
            }
        }

        private void StartBreather(float occupancy)
        {
            _breatherLeft = _settings.BreatherSeconds;
            _recentPeakOccupancy = occupancy;
        }

        // A burst of big scores draws shapes over most of the board and hides it, so the player gets the release a
        // big clear earns while they fly.
        private bool ShapesCoverTheBoard()
        {
            var share = _settings.BreatherShapeCover;
            if (share <= 0f || _shapeCover == null)
            {
                return false;
            }

            // Clockwise from the top: top, right, bottom, left.
            var limits = _flightConfig.LimitsClockwise;
            var playArea = (limits.y - limits.w) * (limits.x - limits.z);
            return playArea > 0f && _shapeCover.CoveredArea >= share * playArea;
        }

        // The board climbing past the hold occupancy is the push working, so it holds there. A breather is the
        // release, so it doesn't build through one; on the last heart (full need) it never builds.
        private void BuildPressure(float deltaTime)
        {
            if (_breatherLeft > 0f || _boardFill.Occupancy >= _settings.PressureHoldOccupancy)
            {
                return;
            }

            var comfort = 1f - Mathf.Clamp01(_need.Need);
            _pressure = Mathf.Min(1f, _pressure + deltaTime * comfort / _settings.PressureBuildSeconds);
        }

        private void OnWaveDamage(WaveDamageMessage msg)
        {
            if (msg.HeartsLost > 0)
            {
                _pressure *= _settings.PressureKeptOnHeartLoss;
            }
        }

        // A board about to cost the last heart gets the release a big clear earns, so the player can dig
        // out before the line lands. The cooldown still holds, so a board left full doesn't stay slowed.
        private bool InDanger()
        {
            var level = _settings.BreatherDanger;
            return level > 0f && _danger != null && _danger.Level.Value >= level;
        }

        // 0 as a breather starts, easing to 1 as it ends; 1 outside one.
        private float BreatherBlend()
        {
            return _breatherLeft > 0f
                ? Mathf.SmoothStep(0f, 1f, 1f - _breatherLeft / _settings.BreatherSeconds)
                : 1f;
        }

        private bool IsRunning()
        {
#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
            if (BalloonParty.Cheats.CheatState.EmptyBoard)
            {
                return false;
            }
#endif

            return Navigation.Current.Value == NavigationState.Game
                   && _levelProgress.Phase.Value == LevelUpPhase.Playing
                   && !_pauseService.IsAnyPaused.Value
                   && !_projectiles.DoomedShotFliesAlone();
        }

        private void StartLevel()
        {
            _secondsOnLevel = 0f;
            _breatherLeft = 0f;
            _cooldownLeft = 0f;
            _recentPeakOccupancy = 0f;
            StartWindow(_settings.GraceSeconds);
        }

        // An extra-colour level brings its lines faster (ILevelParameters.SpawnIntervalScale).
        private float LineInterval()
        {
            var scale = _levelParams.Current?.SpawnIntervalScale ?? 1f;
            return _settings.IntervalForLevel(_levelProgress.Level.Value) * (scale > 0f ? scale : 1f);
        }

        private void StartWindow(float seconds)
        {
            _window = seconds;
            _remaining = seconds;
        }
    }
}
