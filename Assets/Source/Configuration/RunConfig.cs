using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Run Config", fileName = "RunConfig")]
    internal class RunConfig : ScriptableObject, IRunConfig
    {
        [Header("Run")]
        [SerializeField] private int _startingHitPoints = 5;
        [SerializeField] private int _maxRetries = 1;

        [Header("Level Complete")]
        [Tooltip("Seconds over which timeScale ramps from 1 to peak after the completing flight ends.")]
        [SerializeField] [Min(0f)] private float _levelCompleteRampUpDuration = 1.5f;

        [Tooltip("Peak timeScale during the post-flight ramp-up (e.g. 2 = double speed).")]
        [SerializeField] [Min(1f)] private float _levelCompleteRampUpScale = 2f;

        public int StartingHitPoints => _startingHitPoints;
        public int MaxRetries => _maxRetries;
        public float LevelCompleteRampUpDuration => _levelCompleteRampUpDuration;
        public float LevelCompleteRampUpScale => _levelCompleteRampUpScale;
    }
}
