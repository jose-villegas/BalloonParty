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

        [SerializeField] private ShieldChainSettings _shieldChain = new();

        [Header("Level Complete")]
        [Tooltip("Seconds over which timeScale ramps from 1 to peak after the completing flight ends.")]
        [SerializeField] [Min(0f)] private float _levelCompleteRampUpDuration = 1.5f;

        [Tooltip("Peak timeScale during the post-flight ramp-up (e.g. 2 = double speed).")]
        [SerializeField] [Min(1f)] private float _levelCompleteRampUpScale = 2f;

        [Tooltip("Unscaled seconds after the level-complete beat before balls still in flight are popped " +
                 "(no new hits) so the level-up can present. With rapid fire several balls may still be " +
                 "cruising. 0 = wait for them to die on their own.")]
        [SerializeField] [Min(0f)] private float _levelCompleteFlightGrace = 1f;

        public int StartingHitPoints => _startingHitPoints;
        public int MaxRetries => _maxRetries;
        public bool StreakGrantsShields => _streakGrantsShields;
        public bool PlanShieldChains => _planShieldChains;
        public IShieldChainSettings ShieldChain => _shieldChain;
        public float LevelCompleteRampUpDuration => _levelCompleteRampUpDuration;
        public float LevelCompleteFlightGrace => _levelCompleteFlightGrace;
        public float LevelCompleteRampUpScale => _levelCompleteRampUpScale;
    }
}
