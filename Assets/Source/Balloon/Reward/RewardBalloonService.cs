using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using UniRx;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     Decides when the reward balloons come (PLAN-RewardBalloons). The golden balloon: once a purple level, from
    ///     the line that finds the level <see cref="IRewardBalloonSettings.GoldenAtCompletion" /> done. The heart
    ///     balloon: a roll on every line while a heart is missing, likelier the more are, after a cooldown since the
    ///     last. At most one of each on the board, and only a type the catalogue has an entry for.
    /// </summary>
    internal sealed class RewardBalloonService : IRewardBalloonRequests, IStartable, IRunResettable, IDisposable
    {
        private readonly IRewardBalloonSettings _settings;
        private readonly ILevelProgress _levelProgress;
        private readonly IActiveLevelParameters _levelParams;
        private readonly IPlayerHealth _health;
        private readonly bool _goldenPlaceable;
        private readonly bool _heartPlaceable;

        private IDisposable _levelSubscription;
        private bool _goldenPlacedThisLevel;
        private bool _goldenAlive;
        private bool _heartAlive;
        // A heart rolled for a line that had no room for it, still owed to the next.
        private bool _heartPending;
        private int _linesSinceHeart;
        // Which placement a leave callback belongs to; a stale one (an earlier balloon, or one from before a run
        // reset) must not clear the flag of the balloon on the board now.
        private int _goldenPlacement;
        private int _heartPlacement;

        public int ResetOrder => RunResetOrder.Counters;

        // A seam for tests, as UnbreakableFormationService's.
        internal Func<float> Random01 { get; set; } = () => Random.value;

        public RewardBalloonService(
            IBalloonsConfiguration balloons,
            ILevelProgress levelProgress,
            IActiveLevelParameters levelParams,
            IPlayerHealth health)
        {
            _settings = balloons.RewardBalloons;
            _levelProgress = levelProgress;
            _levelParams = levelParams;
            _health = health;
            _goldenPlaceable = HasEntry(balloons, BalloonType.Golden);
            _heartPlaceable = HasEntry(balloons, BalloonType.Heart);
            _linesSinceHeart = _settings.HeartCooldownLines;
        }

        public void Start()
        {
            // Every new level gets its own golden balloon; a retry's restart is a run reset.
            _levelSubscription = _levelProgress.Level.Subscribe(_ => _goldenPlacedThisLevel = false);
        }

        public bool TryTakeDue(out BalloonType type)
        {
            type = default;
            if (_levelProgress.Phase.Value != LevelUpPhase.Playing)
            {
                return false;
            }

            _linesSinceHeart++;
            if (GoldenDue())
            {
                type = BalloonType.Golden;
                return true;
            }

            if (HeartDue())
            {
                type = BalloonType.Heart;
                return true;
            }

            return false;
        }

        public Action Placed(BalloonType type)
        {
            switch (type)
            {
                case BalloonType.Golden:
                {
                    _goldenPlacedThisLevel = true;
                    _goldenAlive = true;
                    var placement = ++_goldenPlacement;
                    return () => GoldenLeft(placement);
                }
                case BalloonType.Heart:
                {
                    _heartAlive = true;
                    _heartPending = false;
                    _linesSinceHeart = 0;
                    var placement = ++_heartPlacement;
                    return () => HeartLeft(placement);
                }
                default:
                    return null;
            }
        }

        public void ResetRun(int generation)
        {
            _goldenPlacement++;
            _heartPlacement++;
            _goldenPlacedThisLevel = false;
            _goldenAlive = false;
            _heartAlive = false;
            _heartPending = false;
            _linesSinceHeart = _settings.HeartCooldownLines;
        }

        public void Dispose()
        {
            _levelSubscription?.Dispose();
        }

        /// <summary>The heart balloon's chance on a line: none at full hearts, more the more are missing.</summary>
        internal static float HeartChance(IRewardBalloonSettings settings, int missingHearts, bool purple)
        {
            if (missingHearts <= 0)
            {
                return 0f;
            }

            var chance = missingHearts == 1 ? settings.HeartChanceMissingOne : settings.HeartChanceMissingTwo;
            return chance + (purple ? settings.HeartChanceOnPurple : 0f);
        }

        private static bool HasEntry(IBalloonsConfiguration balloons, BalloonType type)
        {
            foreach (var entry in balloons.Entries)
            {
                if (entry.BalloonType == type)
                {
                    return true;
                }
            }

            return false;
        }

        private bool GoldenDue()
        {
            var level = _levelParams.Current;
            return _goldenPlaceable && level != null && level.IsExtraColorLevel && !_goldenPlacedThisLevel
                   && !_goldenAlive
                   && LevelCompletion.Of(_levelProgress, level.AllowedColors) >= _settings.GoldenAtCompletion;
        }

        private bool HeartDue()
        {
            if (!_heartPlaceable || _heartAlive)
            {
                return false;
            }

            var missing = _health.Max - _health.Current.Value;
            if (_heartPending)
            {
                // Healed in the meantime (a level-up refill): the help is no longer needed.
                _heartPending = missing > 0;
                return _heartPending;
            }

            if (_linesSinceHeart < _settings.HeartCooldownLines)
            {
                return false;
            }

            var level = _levelParams.Current;
            var chance = HeartChance(_settings, missing, level != null && level.IsExtraColorLevel);
            _heartPending = chance > 0f && Random01() < chance;
            return _heartPending;
        }

        private void GoldenLeft(int placement)
        {
            if (placement == _goldenPlacement)
            {
                _goldenAlive = false;
            }
        }

        private void HeartLeft(int placement)
        {
            if (placement == _heartPlacement)
            {
                _heartAlive = false;
            }
        }
    }
}
