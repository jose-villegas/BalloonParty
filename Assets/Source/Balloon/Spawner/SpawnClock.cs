using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
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
    ///     <see cref="ISpawnClockSettings.IntervalForLevel" /> of scaled game time, after a grace at each
    ///     level's start. Scaled, so slow-mo slows it with everything else. It stops outright through
    ///     pauses, the level-up and game over, and while a lone doomed ball glides its last breath: lines
    ///     landing in that frozen moment read wrong, and the board must hold still under the glide.
    ///     Firing while another ball is still flying pulls the next line closer
    ///     (<see cref="ISpawnClockSettings.OverlapShotPull" />), so every extra ball in the air costs room.
    ///     A sparse board runs the clock faster (<see cref="ISpawnClockSettings.SparseBoardSpeedup" />), so
    ///     clearing it never leaves the player waiting on an empty screen. Balls in the air push it too, by
    ///     how long each has flown (<see cref="ISpawnClockSettings.FlightSpeedup" />): the board refills
    ///     while long ricochets and rapid fire clear it.
    /// </summary>
    internal sealed class SpawnClock : IStartable, ITickable, IRunResettable, IDisposable, ISpawnClock
    {
        private readonly ISpawnClockSettings _settings;
        private readonly ILevelProgress _levelProgress;
        private readonly PauseService _pauseService;
        private readonly IProjectileRegistry _projectiles;
        private readonly IBoardFill _boardFill;
        private readonly IPublisher<SpawnBalloonLineMessage> _linePublisher;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        // Each in-flight ball's flight time while the clock runs; dropped when the ball retires.
        private readonly Dictionary<IProjectileModel, float> _flightAges = new();

        // The current wait (a grace or an interval) and how much of it is left.
        private float _window;
        private float _remaining;

        public int ResetOrder => RunResetOrder.Counters;
        public float NextLineProgress => _window > 0f ? 1f - _remaining / _window : 0f;

        [Inject]
        internal SpawnClock(
            ILevelPacingConfiguration pacing,
            ILevelProgress levelProgress,
            PauseService pauseService,
            IProjectileRegistry projectiles,
            IBoardFill boardFill,
            IPublisher<SpawnBalloonLineMessage> linePublisher,
            ISubscriber<ProjectileFiredMessage> firedSubscriber)
        {
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
            _levelProgress.Level.Skip(1).Subscribe(_ => StartWindow(_settings.GraceSeconds)).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(OnFired).AddTo(_subscriptions);
            _projectiles.Retired.Subscribe(ball => _flightAges.Remove(ball)).AddTo(_subscriptions);
        }

        public void Tick()
        {
            Advance(Time.deltaTime);
        }

        public void ResetRun(int generation)
        {
            _flightAges.Clear();
            StartWindow(_settings.GraceSeconds);
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

            _remaining -= deltaTime * Pace(AgeFlights(deltaTime));
            if (_remaining > 0f)
            {
                return;
            }

            // One line per tick, however long the frame: a hitch must not land a backlog of lines at once.
            StartWindow(_settings.IntervalForLevel(_levelProgress.Level.Value));
            _linePublisher.Publish(new SpawnBalloonLineMessage(1));
        }

        // The pull only shortens the wait; the next Tick lands the line, so it obeys the same holds as the timer.
        private void OnFired(ProjectileFiredMessage msg)
        {
            if (AnotherBallInFlight(msg.Projectile))
            {
                _remaining -= _window * _settings.OverlapShotPull;
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

        // The board sets the base: full speedup on an empty board, easing to normal pace as it reaches both
        // the level's starting fill and the target occupancy (blended by OccupancyWeight). Flight adds on
        // top, so a healthy board still speeds up while balls cruise.
        private float Pace(float flightSeconds)
        {
            var occupancy = Mathf.Clamp01(_boardFill.Occupancy / _settings.TargetOccupancy);
            var fill = Mathf.Lerp(_boardFill.StartingFill, occupancy, _settings.OccupancyWeight);
            var boardPace = Mathf.Lerp(_settings.SparseBoardSpeedup, 1f, fill);
            var flightPressure = Mathf.Clamp01(flightSeconds / _settings.FlightPressureSeconds);
            return boardPace + (_settings.FlightSpeedup - 1f) * flightPressure;
        }

        private bool IsRunning()
        {
            return Navigation.Current.Value == NavigationState.Game
                   && _levelProgress.Phase.Value == LevelUpPhase.Playing
                   && !_pauseService.IsAnyPaused.Value
                   && !_projectiles.DoomedShotFliesAlone();
        }

        private void StartWindow(float seconds)
        {
            _window = seconds;
            _remaining = seconds;
        }
    }
}
