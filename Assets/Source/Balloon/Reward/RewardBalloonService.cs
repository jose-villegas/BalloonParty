using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using UniRx;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     Decides when the reward balloons come (PLAN-RewardBalloons). At most one of each on the board, and only a
    ///     type the catalogue has an entry for.
    ///     <para>
    ///         On a purple level both are rolled every <see cref="IRewardBalloonSettings.PurpleRollEveryLines" /> lines,
    ///         each kind on its own count: the heart from the level's start (at full hearts too), the golden from
    ///         <see cref="IRewardBalloonSettings.PurpleGoldenAtCompletion" />. Each roll's chance is the next entry of
    ///         its list (<see cref="IRewardBalloonSettings.PurpleHeartChances" />,
    ///         <see cref="IRewardBalloonSettings.PurpleGoldenChances" />), so each further one is rarer and the list's
    ///         length caps the level.
    ///     </para>
    ///     <para>
    ///         Off a purple level, every <see cref="IRewardBalloonSettings.OrdinaryGoldenEveryLevels" /> levels roll once
    ///         for a single golden balloon at <see cref="IRewardBalloonSettings.GoldenAtCompletion" />, and
    ///         <see cref="IRewardBalloonSettings.SureGoldenLevel" /> always brings one, early
    ///         (<see cref="IRewardBalloonSettings.SureGoldenAtCompletion" />). The heart comes from a roll on every line
    ///         while a heart is missing, likelier the more are, after a cooldown since the last.
    ///     </para>
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
        private int _goldensThisLevel;
        private int _heartsThisLevel;
        // A golden balloon won (a purple level's roll, or an ordinary level's), still to be placed.
        private bool _goldenOwed;
        // Off a purple level: whether this level has rolled for its single golden balloon, and won it.
        private bool _ordinaryGoldenRolled;
        private bool _ordinaryGoldenWon;
        private float _ordinaryGoldenAt;
        // A purple level's lines counted towards each kind's next roll.
        private int _goldenRollLines;
        private int _heartRollLines;
        private bool _purpleHeartOwed;
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
            // Every new level gets its own reward balloons; a retry's restart is a run reset.
            _levelSubscription = _levelProgress.Level.Subscribe(_ => ForgetLevelsRewards());
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
                    _goldensThisLevel++;
                    _goldenOwed = false;
                    _goldenAlive = true;
                    var placement = ++_goldenPlacement;
                    return () => GoldenLeft(placement);
                }
                case BalloonType.Heart:
                {
                    _heartsThisLevel++;
                    _heartAlive = true;
                    _heartPending = false;
                    _purpleHeartOwed = false;
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
            ForgetLevelsRewards();
            _goldenAlive = false;
            _heartAlive = false;
            _heartPending = false;
            _linesSinceHeart = _settings.HeartCooldownLines;
        }

        public void Dispose()
        {
            _levelSubscription?.Dispose();
        }

        /// <summary>
        ///     The heart balloon's chance on a line off a purple level: none at full hearts (or past them), more the
        ///     more are missing.
        /// </summary>
        internal static float HeartChance(IRewardBalloonSettings settings, int missingHearts)
        {
            if (missingHearts <= 0)
            {
                return 0f;
            }

            return missingHearts == 1 ? settings.HeartChanceMissingOne : settings.HeartChanceMissingTwo;
        }

        internal static bool IsOrdinaryGoldenLevel(IRewardBalloonSettings settings, int level)
        {
            var every = settings.OrdinaryGoldenEveryLevels;
            return every > 0 && level > 0 && level % every == 0;
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
            if (!_goldenPlaceable || _goldenAlive || level == null)
            {
                return false;
            }

            if (!level.IsExtraColorLevel)
            {
                return OrdinaryGoldenDue(level);
            }

            if (_goldenOwed)
            {
                return true;
            }

            if (LevelCompletion.Of(_levelProgress, level.AllowedColors) < _settings.PurpleGoldenAtCompletion)
            {
                return false;
            }

            _goldenOwed = RollPurple(_settings.PurpleGoldenChances, _goldensThisLevel, ref _goldenRollLines);
            return _goldenOwed;
        }

        // Rolled on the level's first line, where the level's parameters are surely its own.
        private bool OrdinaryGoldenDue(ILevelParameters level)
        {
            if (!_ordinaryGoldenRolled)
            {
                _ordinaryGoldenRolled = true;
                var number = _levelProgress.Level.Value;
                var sure = number > 0 && number == _settings.SureGoldenLevel;
                _ordinaryGoldenWon = sure
                                     || (IsOrdinaryGoldenLevel(_settings, number)
                                         && Random01() < _settings.OrdinaryGoldenChance);
                _ordinaryGoldenAt = sure ? _settings.SureGoldenAtCompletion : _settings.GoldenAtCompletion;
            }

            return _ordinaryGoldenWon && _goldensThisLevel == 0
                                      && LevelCompletion.Of(_levelProgress, level.AllowedColors) >= _ordinaryGoldenAt;
        }

        // One counted line towards a purple level's next roll of a kind, and the roll when it's time: the first on
        // the first line counted, then every PurpleRollEveryLines. The chance is the next entry, so the list's length
        // caps the level.
        private bool RollPurple(IReadOnlyList<float> chances, int placedThisLevel, ref int linesCounted)
        {
            if (chances == null || placedThisLevel >= chances.Count)
            {
                return false;
            }

            linesCounted++;
            if (linesCounted < _settings.PurpleRollEveryLines)
            {
                return false;
            }

            linesCounted = 0;
            var chance = chances[placedThisLevel];
            return chance >= 1f || Random01() < chance;
        }

        private void ForgetLevelsRewards()
        {
            _goldensThisLevel = 0;
            _heartsThisLevel = 0;
            _goldenOwed = false;
            _ordinaryGoldenRolled = false;
            _ordinaryGoldenWon = false;
            _purpleHeartOwed = false;
            // Primed so each kind's first line on a purple level rolls.
            _goldenRollLines = _settings.PurpleRollEveryLines - 1;
            _heartRollLines = _settings.PurpleRollEveryLines - 1;
        }

        private bool HeartDue()
        {
            if (!_heartPlaceable || _heartAlive)
            {
                return false;
            }

            var level = _levelParams.Current;
            if (level is { IsExtraColorLevel: true })
            {
                if (!_purpleHeartOwed)
                {
                    _purpleHeartOwed = RollPurple(_settings.PurpleHeartChances, _heartsThisLevel, ref _heartRollLines);
                }

                return _purpleHeartOwed;
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

            var chance = HeartChance(_settings, missing);
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
