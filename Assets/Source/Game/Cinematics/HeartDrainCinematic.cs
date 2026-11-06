using System;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Game.Health;
using BalloonParty.Game.Run;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Begins on a wave-damage event, follows the heart trail in slow-mo, and ends when the pile
    ///     drains or the run ends. At most one per <see cref="ICinematicsSettings.HeartDrainMinGapSeconds" />:
    ///     with lines on a clock a full board loses hearts every few seconds, and a camera move each time
    ///     would take the game away from the player.
    /// </summary>
    internal sealed class HeartDrainCinematic : CameraRigCinematicProducer, IRunResettable
    {
        private readonly HeartTrailTracker _tracker;
        private readonly RejectedBalloonEffect _overflow;
        private readonly ISubscriber<WaveDamageMessage> _waveDamageSubscriber;
        private readonly float _minGapSeconds;
        private readonly float _maxSeconds;

        private IDisposable _subscription;
        private float _lastBeganAt = float.NegativeInfinity;

        public int ResetOrder => RunResetOrder.Counters;

        [Inject]
        internal HeartDrainCinematic(
            CinematicDirector director,
            CinematicCameraRig rig,
            TimeScaleService timeScale,
            ICinematicsSettings settings,
            HeartTrailTracker tracker,
            RejectedBalloonEffect overflow,
            ISubscriber<WaveDamageMessage> waveDamageSubscriber)
            : base(director, rig, timeScale, settings)
        {
            _tracker = tracker;
            _overflow = overflow;
            _waveDamageSubscriber = waveDamageSubscriber;
            _minGapSeconds = settings.HeartDrainMinGapSeconds;
            _maxSeconds = settings.HeartDrainMaxSeconds;
        }

        // A new run's first heart loss must not wait out the gap the last run's drain started.
        public void ResetRun(int generation)
        {
            _lastBeganAt = float.NegativeInfinity;
        }

        protected override CameraRigCinematicConfig BuildConfig()
        {
            return new CameraRigCinematicConfig
            {
                PanInState = CinematicState.HeartDrain,
                RestoreState = CinematicState.HeartDrainRestore,
                Focus = new HeartTrailFocus(_tracker),
                EndCondition = ShouldEnd,
            };
        }

        protected override void OnStart()
        {
            _subscription = _waveDamageSubscriber.Subscribe(_ => OnWaveDamage());
        }

        protected override void OnDispose()
        {
            _subscription?.Dispose();
        }

        private void OnWaveDamage()
        {
            // Only in active play; the runner drops the request while any cinematic is busy. Real time, so
            // the drain's own slow-mo doesn't stretch the gap.
            if (Navigation.Current.Value != NavigationState.Game
                || Time.unscaledTime - _lastBeganAt < _minGapSeconds)
            {
                return;
            }

            if (Runner.TryBegin())
            {
                _lastBeganAt = Time.unscaledTime;
            }
        }

        // Ends on game-over, once the pile is empty with no heart trails in flight, or at the cap: lines
        // keep arriving on the clock, so a full board can keep the pile from ever emptying.
        private bool ShouldEnd()
        {
            return Navigation.Current.Value == NavigationState.GameOver
                   || (!_overflow.IsOverflowActive && _tracker.Active.Count == 0)
                   || Time.unscaledTime - _lastBeganAt >= _maxSeconds;
        }
    }
}
