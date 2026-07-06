using System;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration;
using BalloonParty.Game.Health;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Messages;
using MessagePipe;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Begins on the first heart trail launch, follows the heart about to land in slow-mo, and ends
    ///     when the pile drains or the run ends.
    /// </summary>
    internal sealed class HeartDrainCinematic : IStartable, IDisposable
    {
        private readonly CinematicDirector _director;
        private readonly CinematicCameraRig _rig;
        private readonly TimeScaleService _timeScale;
        private readonly ICinematicsSettings _settings;
        private readonly HeartTrailTracker _tracker;
        private readonly RejectedBalloonEffect _overflow;
        private readonly ISubscriber<OverflowHeartRequestedMessage> _heartRequestedSubscriber;

        private CameraRigCinematic _cinematic;
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
        {
            _director = director;
            _rig = rig;
            _timeScale = timeScale;
            _settings = settings;
            _tracker = tracker;
            _overflow = overflow;
            _heartRequestedSubscriber = heartRequestedSubscriber;
        }

        public void Start()
        {
            _cinematic = new CameraRigCinematic(_director, _rig, _timeScale, _settings, new CameraRigCinematicConfig
            {
                PanInState = CinematicState.HeartDrain,
                RestoreState = CinematicState.HeartDrainRestore,
                Focus = new HeartTrailFocus(_tracker),
                EndCondition = ShouldEnd,
            });

            _subscription = _heartRequestedSubscriber.Subscribe(_ => OnFirstHeart());
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _cinematic?.Abort();
        }

        private void OnFirstHeart()
        {
            // Only in active play; the runner drops the request while any cinematic is busy.
            if (Navigation.Current.Value == NavigationState.Game)
            {
                _cinematic.TryBegin();
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
