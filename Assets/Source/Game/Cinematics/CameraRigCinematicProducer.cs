using System;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Shared.Pause;
using VContainer.Unity;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Shared shape for the producers that drive a <see cref="CameraRigCinematic" />: builds the runner
    ///     from <see cref="BuildConfig" /> on start and aborts it on dispose. Subclasses supply their
    ///     domain trigger in <see cref="OnStart" /> and their own teardown in <see cref="OnDispose" />.
    /// </summary>
    internal abstract class CameraRigCinematicProducer : IStartable, IDisposable
    {
        protected readonly CinematicDirector Director;
        protected readonly CinematicCameraRig Rig;
        protected readonly TimeScaleService TimeScale;
        protected readonly ICinematicsSettings Settings;

        private CameraRigCinematic _runner;

        protected CameraRigCinematicProducer(
            CinematicDirector director,
            CinematicCameraRig rig,
            TimeScaleService timeScale,
            ICinematicsSettings settings)
        {
            Director = director;
            Rig = rig;
            TimeScale = timeScale;
            Settings = settings;
        }

        protected CameraRigCinematic Runner => _runner;

        public void Start()
        {
            _runner = new CameraRigCinematic(Director, Rig, TimeScale, Settings, BuildConfig());
            OnStart();
        }

        public void Dispose()
        {
            OnDispose();
            _runner?.Abort();
        }

        protected abstract CameraRigCinematicConfig BuildConfig();

        protected abstract void OnStart();

        protected virtual void OnDispose()
        {
        }
    }
}
