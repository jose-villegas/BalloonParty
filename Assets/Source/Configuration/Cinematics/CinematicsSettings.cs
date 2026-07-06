using System;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>
    ///     Every cinematic state is declared and tuned here, in one entry per
    ///     <see cref="CinematicState" /> (indexed by the enum's ordinal, drawn with the enum names):
    ///     behavioural traits + the uniform camera-rig segment it plays + capability blocks. States are
    ///     the generalization — a restore is not special-cased, it's just another segment whose curve
    ///     ramps timeScale back to 1. The field initializers ARE the canonical declarations — a fresh
    ///     instance equals the shipped asset, and the EditMode test asserts them, so a new state
    ///     without a declaration fails CI.
    /// </summary>
    [CreateAssetMenu(menuName = "Configuration/Cinematics Settings", fileName = "CinematicsSettings")]
    internal class CinematicsSettings : ScriptableObject, ICinematicsSettings
    {
        [Tooltip("Traits + camera-rig segment + capability blocks, one entry per cinematic state.")]
        [EnumIndexed(typeof(CinematicState))]
        [SerializeField] private CinematicStateEntry[] _states =
        {
            // None — no cinematic; entry unused.
            new(),

            // LevelUpPanIn — slow-mo dips to half speed and self-recovers over 3 s while the camera
            // zooms hard onto the tipping trail, which pulses to 4× mid-flight.
            new(
                CinematicTraits.BlocksLoss | CinematicTraits.BlocksShake,
                new CameraRigCinematicSettings(
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1.5f, 0.5f), new Keyframe(3f, 1f)),
                    zoomAmount: 2f,
                    panWeight: 0.6f,
                    followSpeed: 0.7f),
                new TrackedTrailSettings(
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 4f), new Keyframe(1f, 1f)))),

            // LevelUpRestore — ramps from the popup's frozen 0 back to full over 3 s, camera returning
            // to base framing (zoom/pan 0).
            new(
                CinematicTraits.BlocksLoss | CinematicTraits.BlocksShake,
                new CameraRigCinematicSettings(
                    new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(3f, 1f)),
                    zoomAmount: 0f,
                    panWeight: 0f,
                    followSpeed: 0.7f),
                new TrackedTrailSettings()),

            // HeartDrain — quick ramp to 0.3 while the camera follows the landing heart trail.
            new(
                CinematicTraits.None,
                new CameraRigCinematicSettings(
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.3f)),
                    zoomAmount: 0.15f,
                    panWeight: 0.1f,
                    followSpeed: 2f),
                new TrackedTrailSettings()),

            // HeartDrainRestore — back to full speed over 0.85 s, camera returning to base framing.
            new(
                CinematicTraits.None,
                new CameraRigCinematicSettings(
                    new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.85f, 1f)),
                    zoomAmount: 0f,
                    panWeight: 0f,
                    followSpeed: 2f),
                new TrackedTrailSettings()),

            // LevelAscend — repurposes the segment fields for the staging root's descent, not a
            // camera move: the curve's VALUE is a 1→0 height fraction (not timeScale — gameplay is
            // already paused) — 1 at t=0 (fully elevated, matching the pre-set starting offset) down
            // to 0 at the end (settled at rest). ZoomAmount is the staging root's starting height in
            // world units, PanWeight is the fraction of the descent at which the new level's balloons
            // spawn (0.75 — partway through, so they're already mid-animation by the time the scenario
            // settles), and FollowSpeed is the descent-speed multiplier (traverses the curve faster
            // above 1 / slower below; real descent time = curve duration / FollowSpeed).
            new(
                CinematicTraits.BlocksLoss | CinematicTraits.BlocksShake,
                new CameraRigCinematicSettings(
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1.2f, 0f)),
                    zoomAmount: 8f,
                    panWeight: 0.75f,
                    followSpeed: 0.5f),
                new TrackedTrailSettings()),
        };

        private void OnValidate()
        {
#if UNITY_EDITOR
            // Keep the array in lock-step with the enum so an asset saved before a new state self-heals
            // on open (new entries default to trait-less — the test still enforces intent).
            var states = Enum.GetValues(typeof(CinematicState)).Length;
            if (_states == null || _states.Length != states)
            {
                Array.Resize(ref _states, states);
            }
#endif
        }

        public CinematicStateEntry EntryOf(CinematicState state)
        {
            var index = (int)state;
            if (_states == null || index < 0 || index >= _states.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(state), state, "Cinematic state has no entry declared — extend CinematicsSettings._states.");
            }

            return _states[index];
        }
    }
}
