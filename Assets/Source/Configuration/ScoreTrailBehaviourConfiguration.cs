using System;
using System.Collections.Generic;
using BalloonParty.Game.Score.Behaviours;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>One row of the choreography table: the handler to use once a group clears <see cref="MinPoints"/>.</summary>
    [Serializable]
    internal struct ScoreTrailBehaviourEntry
    {
        [SerializeField] private ScoreTrailBehaviourId _id;

        [Tooltip("Lowest group total this handler claims; the resolver picks the highest-MinPoints entry the group clears.")]
        [SerializeField] private int _minPoints;

        public ScoreTrailBehaviourId Id => _id;
        public int MinPoints => _minPoints;

        internal ScoreTrailBehaviourEntry(ScoreTrailBehaviourId id, int minPoints)
        {
            _id = id;
            _minPoints = minPoints;
        }
    }

    [CreateAssetMenu(menuName = "Configuration/Score Trail Behaviours", fileName = "ScoreTrailBehaviourConfiguration")]
    internal class ScoreTrailBehaviourConfiguration : ScriptableObject, IScoreTrailBehaviourConfiguration
    {
        [Tooltip("Choreography handlers keyed by score magnitude, evaluated highest MinPoints first. " +
                 "A DefaultScore entry at MinPoints 0 is the catch-all.")]
        [SerializeField] private ScoreTrailBehaviourEntry[] _entries = Array.Empty<ScoreTrailBehaviourEntry>();

        public IReadOnlyList<ScoreTrailBehaviourEntry> Entries => _entries;
    }
}
