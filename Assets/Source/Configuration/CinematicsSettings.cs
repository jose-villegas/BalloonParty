using System;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Every cinematic is declared and tuned here: <see cref="_traits" /> holds one
    ///     <see cref="CinematicTraits" /> entry per <see cref="CinematicState" /> (indexed by the enum's
    ///     ordinal, drawn with the enum names), and each camera-rig cinematic gets its own
    ///     <see cref="CameraRigCinematicSettings" /> block. The field initializers ARE the canonical
    ///     declarations — a fresh instance carries them, the asset starts from them, and the EditMode
    ///     test asserts them, so a new state without a declaration fails CI.
    /// </summary>
    [CreateAssetMenu(menuName = "Configuration/Cinematics Settings", fileName = "CinematicsSettings")]
    internal class CinematicsSettings : ScriptableObject, ICinematicsSettings
    {
        [Tooltip("Behavioural traits per cinematic state — the declaration, not derived anywhere else.")]
        [EnumIndexed(typeof(CinematicState))]
        [SerializeField] private CinematicTraits[] _traits =
        {
            CinematicTraits.None,                                       // None
            CinematicTraits.BlocksLoss | CinematicTraits.BlocksShake,   // LevelUpPanIn
            CinematicTraits.BlocksLoss | CinematicTraits.BlocksShake,   // LevelUpRestore
            CinematicTraits.None,                                       // HeartDrain
        };

        [Header("Level-up")]
        [SerializeField] private CameraRigCinematicSettings _levelUp = new();

        [Tooltip("Scale of the tipping trail over its manual flight during the pan-in.")]
        [SerializeField] private AnimationCurve _trackedTrailScaleCurve = AnimationCurve.EaseInOut(0f, 2f, 1f, 1f);

        [Header("Heart drain")]
        [SerializeField] private CameraRigCinematicSettings _heartDrain = new();

        public CameraRigCinematicSettings LevelUp => _levelUp;
        public CameraRigCinematicSettings HeartDrain => _heartDrain;
        public AnimationCurve LevelUpTrackedTrailScaleCurve => _trackedTrailScaleCurve;

        private void OnValidate()
        {
#if UNITY_EDITOR
            // Keep the traits array in lock-step with the enum so an asset saved before a new state
            // self-heals on open (new entries default to None — the test still enforces intent).
            var states = Enum.GetValues(typeof(CinematicState)).Length;
            if (_traits == null || _traits.Length != states)
            {
                Array.Resize(ref _traits, states);
            }
#endif
        }

        public CinematicTraits TraitsOf(CinematicState state)
        {
            var index = (int)state;
            if (_traits == null || index < 0 || index >= _traits.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(state), state, "Cinematic state has no traits declared — extend CinematicsSettings._traits.");
            }

            return _traits[index];
        }
    }
}
