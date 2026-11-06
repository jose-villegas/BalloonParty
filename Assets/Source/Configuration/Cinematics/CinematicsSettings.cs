using System;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>Entries are authored in the editor, one per <see cref="CinematicState" />, indexed by its ordinal.</summary>
    [CreateAssetMenu(menuName = "Configuration/Cinematics Settings", fileName = "CinematicsSettings")]
    internal class CinematicsSettings : ScriptableObject, ICinematicsSettings
    {
        [Tooltip("Traits + camera-rig segment + capability blocks, one entry per cinematic state.")]
        [EnumIndexed(typeof(CinematicState))]
        [SerializeField] private CinematicStateEntry[] _states;

        [Tooltip("The Ascent's own tuning — a transform-descent, not a camera move.")]
        [SerializeField] private LevelAscendSettings _levelAscend;

        [Tooltip("Tuning for the float-away board effect (level-clear balloons rise + zigzag + shrink).")]
        [SerializeField] private BoardFloatAwaySettings _boardFloatAway;

        [Tooltip("Least real time, in seconds, between two heart-drain cinematics. Lines arrive on a clock, so " +
                 "an overflowing board loses hearts every few seconds; in between, hearts drain without the " +
                 "camera move. 0 = every heart loss may play one.")]
        [SerializeField] [Min(0f)] private float _heartDrainMinGapSeconds = 10f;

        [Tooltip("Longest real time, in seconds, a heart-drain cinematic holds the camera. Lines keep arriving " +
                 "during it, so the overflow pile may never empty on its own.")]
        [SerializeField] [Min(0.5f)] private float _heartDrainMaxSeconds = 4f;

        public LevelAscendSettings LevelAscend => _levelAscend;

        public BoardFloatAwaySettings BoardFloatAway => _boardFloatAway;

        public float HeartDrainMinGapSeconds => _heartDrainMinGapSeconds;

        public float HeartDrainMaxSeconds => _heartDrainMaxSeconds;

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
