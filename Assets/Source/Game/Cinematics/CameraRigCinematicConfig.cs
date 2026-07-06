using System;
using BalloonParty.Shared.GameState;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>How one <see cref="CameraRigCinematic" /> composes the shared shape.</summary>
    internal sealed class CameraRigCinematicConfig
    {
        /// <summary>State (and settings entry) of the pan-in segment.</summary>
        public CinematicState PanInState;

        /// <summary>State (and settings entry) of the restore segment.</summary>
        public CinematicState RestoreState;

        /// <summary>What the camera frames during the pan-in.</summary>
        public ICinematicFocus Focus;

        /// <summary>Polled each pan-in tick; true rolls straight into restore. Null means the producer ends the pan-in and restore itself.</summary>
        public Func<bool> EndCondition;

        /// <summary>Pan-in evaluates its segment curve into <c>Time.timeScale</c> each tick.</summary>
        public bool DrivesTimeScale = true;

        /// <summary>Restore samples its curve absolutely instead of tweening from the current timeScale — needed to ramp from a frozen 0.</summary>
        public bool RestoreEvaluatesCurve;

        /// <summary>Extra pan-in work: (unscaled dt, pan-in curve value) — e.g. puppeting a trail.</summary>
        public Action<float, float> OnPanInTick;

        /// <summary>Invoked after the restore completes and the cinematic has ended.</summary>
        public Action OnEnded;
    }
}
