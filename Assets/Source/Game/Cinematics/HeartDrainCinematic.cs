using System;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Game.Health;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using VContainer;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Begins on the first heart trail launch, follows the heart about to land in slow-mo, and ends
    ///     when the pile drains or the run ends.
    /// </summary>
    internal sealed class HeartDrainCinematic : CameraRigCinematicProducer
    {
        private readonly HeartTrailTracker _tracker;
        private readonly RejectedBalloonEffect _overflow;
        private readonly ISubscriber<OverflowHeartRequestedMessage> _heartRequestedSubscriber;

        private IDisposable _subscription;

        [Inject]
        internal HeartDrainCinematic(
            CinematicDirector director,
            CinematicCameraRig rig,
            TimeScaleService timeScale,
            ICinematicsSettings settings,
            HeartTrailTracker tracker,
            RejectedBalloonEffect overflow,
            ISubscriber<OverflowHeartRequestedMessage> heartRequestedSubscriber)
            : base(director, rig, timeScale, settings)
        {
            _tracker = tracker;
            _overflow = overflow;
            _heartRequestedSubscriber = heartRequestedSubscriber;
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
            _subscription = _heartRequestedSubscriber.Subscribe(_ => OnFirstHeart());
        }

        protected override void OnDispose()
        {
            _subscription?.Dispose();
        }

        private void OnFirstHeart()
        {
            // Only in active play; the runner drops the request while any cinematic is busy.
            if (Navigation.Current.Value == NavigationState.Game)
            {
                Runner.TryBegin();
            }
        }

        // Ends on game-over or once the overflow hold is released with no heart trails in flight.
        private bool ShouldEnd()
        {
            return Navigation.Current.Value == NavigationState.GameOver
                   || (!_overflow.IsOverflowActive && _tracker.Active.Count == 0);
        }
    }
}
