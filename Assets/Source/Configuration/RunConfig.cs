using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Run Config", fileName = "RunConfig")]
    internal class RunConfig : ScriptableObject, IRunConfig
    {
        [Header("Run")]
        [SerializeField] private int _startingHitPoints = 3;

        [Tooltip("Hearts given back at each level-up, never past the starting hit points. Set it to the " +
                 "starting hit points to refill to full, 0 for none: a smaller refill lets hearts lost in " +
                 "one level carry into the next.")]
        [SerializeField] [Min(0)] private int _levelUpHeartRefill = 1;

        [Tooltip("Retries at the level reached, per run. 0 (or less) = unlimited.")]
        [SerializeField] private int _maxRetries;

        [Tooltip("Free retries per run; each retry after them needs a rewarded ad. 0 = retries never " +
                 "need an ad. No ad SDK yet: the stand-in counts every ad as watched.")]
        [SerializeField] [Min(0)] private int _retriesBeforeAd;

        [Tooltip("Off makes items the only source of shields — the streak stops granting them. " +
            "A balance experiment: leave it on for current behaviour.")]
        [SerializeField] private bool _streakGrantsShields = true;

        [Tooltip("A ball that deflects off balloons this many times in a row without touching a wall earns a " +
            "shield, and again at each multiple. 0 = never.")]
        [SerializeField] [Min(0)] private int _deflectChainShieldEvery = 3;

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

        [Header("Upgrades")]
        [SerializeField] private UpgradeSettings _upgrades = new();

        public int StartingHitPoints => _startingHitPoints;
        public int LevelUpHeartRefill => _levelUpHeartRefill;
        public int MaxRetries => _maxRetries;
        public int RetriesBeforeAd => Mathf.Max(0, _retriesBeforeAd);
        public bool StreakGrantsShields => _streakGrantsShields;
        public int DeflectChainShieldEvery => _deflectChainShieldEvery;
        public bool PlanShieldChains => _planShieldChains;
        public IShieldChainSettings ShieldChain => _shieldChain;
        public float LevelCompleteRampUpDuration => _levelCompleteRampUpDuration;
        public float LevelCompleteFlightGrace => _levelCompleteFlightGrace;
        public float LevelCompleteRampUpScale => _levelCompleteRampUpScale;
        public IUpgradeSettings Upgrades => _upgrades;
    }
}
