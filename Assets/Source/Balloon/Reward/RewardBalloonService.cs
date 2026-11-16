using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Assist;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Game.Upgrades;
using BalloonParty.Shared;
using UniRx;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     Decides when the reward balloons come (PLAN-RewardBalloons, PLAN-DiscoBalloon). At most one of each on the
    ///     board, and only a type the catalogue has an entry for.
    ///     <para>
    ///         The plated ones, the golden balloon and the disco ball, share their rules (<see cref="PlatedRewardSchedule" />)
    ///         and differ only in their numbers: on a purple level, rolled every
    ///         <see cref="IRewardBalloonSettings.PurpleRollEveryLines" /> lines from a share of the level done, each
    ///         further one rarer; elsewhere, now and then a single one, likelier early in the run and the longer
    ///         since the last, and always one on its sure level. The disco ball's first on a purple level is sure.
    ///     </para>
    ///     <para>
    ///         The heart, on a purple level, is rolled every few lines from the level's start (at full hearts too),
    ///         each further one rarer (<see cref="IRewardBalloonSettings.PurpleHeartChances" />). Elsewhere it comes from
    ///         a roll on every line while a heart is missing, likelier the more are, after a cooldown since the last.
    ///     </para>
    ///     <para>
    ///         The nuke (PLAN-NukeBalloon) rolls on every line from its first level, its chance raised by the trouble
    ///         the player is in (<see cref="NukeSchedule" />, <see cref="INukeOdds" />).
    ///     </para>
    ///     A line brings one at most: the nuke first, then the golden balloon, the disco ball and the heart. One that
    ///     loses the line stays due for the next. The Party Favors card (<see cref="IWeaponStats.RewardBalloonOdds" />)
    ///     raises every roll's chance, the nuke's too: that one still only rises with trouble.
    /// </summary>
    internal sealed class RewardBalloonService : IRewardBalloonRequests, IStartable, IRunResettable, IDisposable
    {
        private readonly IRewardBalloonSettings _settings;
        private readonly ILevelProgress _levelProgress;
        private readonly IActiveLevelParameters _levelParams;
        private readonly IPlayerHealth _health;
        private readonly IPlayerNeed _need;
        private readonly IWeaponStats _weapon;
        private readonly PityRoll _heartPity;
        private readonly PlatedRewardSchedule _golden;
        private readonly PlatedRewardSchedule _disco;
        private readonly NukeSchedule _nuke;
        private readonly bool _goldenPlaceable;
        private readonly bool _discoPlaceable;
        private readonly bool _heartPlaceable;
        private readonly bool _nukePlaceable;

        private IDisposable _levelSubscription;
        private int _heartsThisLevel;
        // A purple level's lines counted towards the heart's next roll.
        private int _heartRollLines;
        private bool _purpleHeartOwed;
        private bool _heartAlive;
        // A heart rolled for a line that had no room for it, still owed to the next.
        private bool _heartPending;
        private int _linesSinceHeart;
        // Which placement a leave callback belongs to; a stale one (an earlier balloon, or one from before a run
        // reset) must not clear the flag of the balloon on the board now.
        private int _heartPlacement;

        public int ResetOrder => RunResetOrder.Counters;

        // Party Favors' multiplier on every reward roll, read live so a card picked mid-run applies at once.
        private float Odds => 1f + Math.Max(0f, _weapon.RewardBalloonOdds);

        // A seam for tests, as UnbreakableFormationService's.
        internal Func<float> Random01 { get; set; } = () => Random.value;

        public RewardBalloonService(
            IBalloonsConfiguration balloons,
            ILevelProgress levelProgress,
            IActiveLevelParameters levelParams,
            IPlayerHealth health,
            IPlayerNeed need,
            IAssistSettings assist,
            IWeaponStats weapon,
            INukeOdds nukeOdds)
        {
            _settings = balloons.RewardBalloons;
            _levelProgress = levelProgress;
            _levelParams = levelParams;
            _health = health;
            _need = need;
            _weapon = weapon;
            _heartPity = new PityRoll(assist.HeartPity);
            // Through the property each draw, so a test's seam set after construction still reaches the schedules.
            _golden = new PlatedRewardSchedule(() => Random01());
            _disco = new PlatedRewardSchedule(() => Random01());
            _nuke = new NukeSchedule(balloons.NukeOdds, nukeOdds, () => Random01());
            _goldenPlaceable = HasEntry(balloons, BalloonType.Golden);
            _discoPlaceable = HasEntry(balloons, BalloonType.Disco);
            _heartPlaceable = HasEntry(balloons, BalloonType.Heart);
            _nukePlaceable = HasEntry(balloons, BalloonType.Nuke);
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
            if (_nukePlaceable && _nuke.Due(_levelProgress.Level.Value, Odds))
            {
                type = BalloonType.Nuke;
                return true;
            }

            if (_goldenPlaceable && PlatedDue(_golden, PlatedRewardTuning.Golden(_settings)))
            {
                type = BalloonType.Golden;
                return true;
            }

            if (_discoPlaceable && PlatedDue(_disco, PlatedRewardTuning.Disco(_settings)))
            {
                type = BalloonType.Disco;
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
                    return _golden.Placed(_levelProgress.Level.Value);
                case BalloonType.Disco:
                    return _disco.Placed(_levelProgress.Level.Value);
                case BalloonType.Nuke:
                    return _nuke.Placed();
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
            _golden.ResetRun(_settings.PurpleRollEveryLines);
            _disco.ResetRun(_settings.PurpleRollEveryLines);
            _nuke.ResetRun();
            _heartPlacement++;
            ForgetHeartsLevel();
            _heartAlive = false;
            _heartPending = false;
            _linesSinceHeart = _settings.HeartCooldownLines;
            _heartPity.Clear();
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

        private bool PlatedDue(PlatedRewardSchedule schedule, in PlatedRewardTuning tuning)
        {
            return schedule.Due(
                in tuning, _levelParams.Current, _levelProgress.Level.Value, _levelProgress,
                _settings.PurpleRollEveryLines, Odds);
        }

        private void ForgetLevelsRewards()
        {
            _golden.ForgetLevel(_settings.PurpleRollEveryLines);
            _disco.ForgetLevel(_settings.PurpleRollEveryLines);
            _nuke.ForgetLevel();
            ForgetHeartsLevel();
        }

        private void ForgetHeartsLevel()
        {
            _heartsThisLevel = 0;
            _purpleHeartOwed = false;
            // Primed so the heart's first line on a purple level rolls.
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
                    _purpleHeartOwed = PlatedRewardSchedule.RollPurple(
                        _settings.PurpleHeartChances, _heartsThisLevel, ref _heartRollLines,
                        _settings.PurpleRollEveryLines, Random01, Odds);
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

            // Pity forgives the lines rolled without a heart while in trouble (IAssistSettings.HeartPity).
            var chance = Math.Min(1f, HeartChance(_settings, missing) * Odds);
            _heartPending = chance > 0f && _heartPity.Roll(chance, _need.Need, Random01());
            return _heartPending;
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
