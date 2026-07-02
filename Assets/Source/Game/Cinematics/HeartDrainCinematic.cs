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

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     The overflow heart-drain cinematic, as a plain C# trigger over the shared
    ///     <see cref="CameraRigCinematic"/> runner: begins when the first heart trail launches from the
    ///     UI, follows the heart about to land (framing the rest) in slow-mo, and ends when the pile has fully
    ///     drained or the run is over (extra hearts past 0 HP don't extend it). Uses
    ///     <see cref="CinematicState.HeartDrain"/>/<see cref="CinematicState.HeartDrainRestore"/>,
    ///     which are neither loss-blocking (the 0-HP game-over fires through) nor shake-blocking (each
    ///     heart launch punches the camera through the pan).
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
            // Begin on the first heart of an overflow burst, only in active play; the runner drops the
            // request while any cinematic is busy.
            if (Navigation.Current.Value == NavigationState.Game)
            {
                _cinematic.TryBegin();
            }
        }

        // Ends when the run is over (game-over already fired at 0 HP — later hearts don't count) or the
        // pile has fully drained: the overflow hold released and no heart trails remain in flight.
        private bool ShouldEnd()
        {
            return Navigation.Current.Value == NavigationState.GameOver
                   || (!_overflow.IsOverflowActive && _tracker.Active.Count == 0);
        }
    }
}
