using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Run Config", fileName = "RunConfig")]
    internal class RunConfig : ScriptableObject, IRunConfig
    {
        [Header("Run")]
        [SerializeField] private int _startingHitPoints = 3;
        [SerializeField] private int _maxRetries = 1;

        [Tooltip("Off makes items the only source of shields — the streak stops granting them. " +
            "A balance experiment: leave it on for current behaviour.")]
        [SerializeField] private bool _streakGrantsShields = true;

        [Tooltip("Place shields along a flight the thrower can actually fly, so a chain is " +
            "collectable in sequence. Off falls back to the weighted random draw.")]
        [SerializeField] private bool _planShieldChains = true;

        [Header("Level Complete")]
        [Tooltip("Seconds over which timeScale ramps from 1 to peak after the completing flight ends.")]
        [SerializeField] [Min(0f)] private float _levelCompleteRampUpDuration = 1.5f;

        [Tooltip("Peak timeScale during the post-flight ramp-up (e.g. 2 = double speed).")]
        [SerializeField] [Min(1f)] private float _levelCompleteRampUpScale = 2f;

        public int StartingHitPoints => _startingHitPoints;
        public int MaxRetries => _maxRetries;
        public bool StreakGrantsShields => _streakGrantsShields;
        public bool PlanShieldChains => _planShieldChains;
        public float LevelCompleteRampUpDuration => _levelCompleteRampUpDuration;
        public float LevelCompleteRampUpScale => _levelCompleteRampUpScale;
    }
}
