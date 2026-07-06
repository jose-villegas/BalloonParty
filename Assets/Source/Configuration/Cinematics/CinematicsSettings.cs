using System;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>Field initializers are the canonical declarations — an EditMode test asserts them, so a new state without one fails CI.</summary>
    [CreateAssetMenu(menuName = "Configuration/Cinematics Settings", fileName = "CinematicsSettings")]
    internal class CinematicsSettings : ScriptableObject, ICinematicsSettings
    {
        [Tooltip("Traits + camera-rig segment + capability blocks, one entry per cinematic state.")]
        [EnumIndexed(typeof(CinematicState))]
        [SerializeField] private CinematicStateEntry[] _states =
        {
            // None — entry unused.
            new(),

            // LevelUpPanIn
            new(
                CinematicTraits.BlocksLoss | CinematicTraits.BlocksShake,
                new CameraRigCinematicSettings(
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1.5f, 0.5f), new Keyframe(3f, 1f)),
                    zoomAmount: 2f,
                    panWeight: 0.6f,
                    followSpeed: 0.7f),
                new TrackedTrailSettings(
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 4f), new Keyframe(1f, 1f)))),

            // LevelUpRestore — ramps from the popup's frozen 0 back to full.
            new(
                CinematicTraits.BlocksLoss | CinematicTraits.BlocksShake,
                new CameraRigCinematicSettings(
                    new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(3f, 1f)),
                    zoomAmount: 0f,
                    panWeight: 0f,
                    followSpeed: 0.7f),
                new TrackedTrailSettings()),

            // HeartDrain
            new(
                CinematicTraits.None,
                new CameraRigCinematicSettings(
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.3f)),
                    zoomAmount: 0.15f,
                    panWeight: 0.1f,
                    followSpeed: 2f),
                new TrackedTrailSettings()),

            // HeartDrainRestore
            new(
                CinematicTraits.None,
                new CameraRigCinematicSettings(
                    new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.85f, 1f)),
                    zoomAmount: 0f,
                    panWeight: 0f,
                    followSpeed: 2f),
                new TrackedTrailSettings()),

            // LevelAscend — repurposes the segment fields for the staging root's descent, not a
            // camera move: curve VALUE is a 1→0 height fraction, ZoomAmount is starting height,
            // PanWeight is the spawn point in the descent, FollowSpeed is the descent-speed multiplier.
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
            // Self-heals an asset saved before a new state was added.
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
