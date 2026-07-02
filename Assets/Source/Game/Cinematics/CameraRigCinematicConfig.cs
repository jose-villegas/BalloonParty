using System;
using BalloonParty.Shared.GameState;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     How one <see cref="CameraRigCinematic" /> composes the shared shape. The heart-drain runs the
    ///     default continuous form (timeScale-driving pan-in → polled end condition → tween-from-current
    ///     restore); the level-up splits its phases around the popup gate, leaves timeScale alone during
    ///     pan-in (gameplay is paused; the curve modulates its tracked trail instead) and restores by
    ///     sampling its curve from the popup's frozen 0.
    /// </summary>
    internal sealed class CameraRigCinematicConfig
    {
        /// <summary>State (and settings entry) of the pan-in segment.</summary>
        public CinematicState PanInState;

        /// <summary>State (and settings entry) of the restore segment.</summary>
        public CinematicState RestoreState;

        /// <summary>What the camera frames during the pan-in.</summary>
        public ICinematicFocus Focus;

        /// <summary>
        ///     Polled each pan-in tick; true → the runner rolls straight into the restore segment.
        ///     Null → the producer ends the pan-in itself (<see cref="CameraRigCinematic.EndPanIn" />)
        ///     and later starts the restore (<see cref="CameraRigCinematic.TryBeginRestore" />).
        /// </summary>
        public Func<bool> EndCondition;

        /// <summary>Pan-in evaluates its segment curve into <c>Time.timeScale</c> each tick.</summary>
        public bool DrivesTimeScale = true;

        /// <summary>
        ///     Restore samples its segment curve absolutely (needed to ramp from the popup's frozen 0);
        ///     false → tween from the current timeScale to 1 over the curve's duration, so an early end
        ///     mid-ramp doesn't snap speed down first.
        /// </summary>
        public bool RestoreEvaluatesCurve;

        /// <summary>Extra pan-in work: (unscaled dt, pan-in curve value) — e.g. puppeting a trail.</summary>
        public Action<float, float> OnPanInTick;

        /// <summary>Invoked after the restore completes and the cinematic has ended.</summary>
        public Action OnEnded;
    }
}
