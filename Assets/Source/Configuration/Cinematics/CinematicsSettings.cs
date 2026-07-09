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

        public LevelAscendSettings LevelAscend => _levelAscend;

        public BoardFloatAwaySettings BoardFloatAway => _boardFloatAway;

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
