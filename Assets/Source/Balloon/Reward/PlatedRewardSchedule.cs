using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     When one plated reward balloon (the golden balloon, the disco ball) is due, and whether one is on the board.
    ///     <para>
    ///         On a purple level it rolls every few lines from <see cref="PlatedRewardTuning.PurpleAtCompletion" />, each
    ///         roll's chance the next entry of <see cref="PlatedRewardTuning.PurpleChances" />, so each further one is
    ///         rarer and the list's length caps the level. A first entry of 1 makes the first one sure.
    ///     </para>
    ///     <para>
    ///         Off a purple level, every <see cref="PlatedRewardTuning.OrdinaryEveryLevels" /> levels from the sure level
    ///         on roll once, on their first line, for a single one at <see cref="PlatedRewardTuning.AtCompletion" />.
    ///         The chance eases off the further past the sure level the run is
    ///         (<see cref="PlatedRewardTuning.ChanceHalvedAfterLevels" />), and a soft pity raises it by
    ///         <see cref="PlatedRewardTuning.ChanceGrowthPerLevel" /> for each level since the last one was placed, so
    ///         an eased chance never stays low. <see cref="PlatedRewardTuning.SureLevel" /> needs no roll and brings
    ///         its one early (<see cref="PlatedRewardTuning.SureAtCompletion" />), the player's first sight of the
    ///         balloon.
    ///     </para>
    ///     <para>
    ///         A level that lost that roll can still win one as it goes: each
    ///         <see cref="PlatedRewardTuning.PityEveryCompletion" /> more of it done rolls again, the first at
    ///         <see cref="PlatedRewardTuning.PityChancePerStep" />, each later one that much likelier, and a won one
    ///         comes at once. Only the disco ball's tuning sets it.
    ///     </para>
    ///     A won roll stays due until the balloon is placed: a line with no room for it passes it on.
    /// </summary>
    internal sealed class PlatedRewardSchedule
    {
        private readonly Func<float> _random01;

        private int _placedThisLevel;
        private bool _owed;
        // Off a purple level: whether this level has rolled for its single one, and won it.
        private bool _ordinaryRolled;
        private bool _ordinaryWon;
        private float _ordinaryAt;
        // How many of the level's completion steps have rolled their pity.
        private int _pitySteps;
        // A purple level's lines counted towards the next roll.
        private int _rollLines;
        private bool _alive;
        // The level the last one was placed on, purple or not, for the soft pity; 0 before the run's first.
        private int _lastPlacedLevel;
        // Which placement a leave callback belongs to; a stale one (an earlier balloon, or one from before a run
        // reset) must not clear the flag of the balloon on the board now.
        private int _placement;

        internal PlatedRewardSchedule(Func<float> random01)
        {
            _random01 = random01;
        }

        /// <summary>
        ///     One counted line towards a purple level's next roll of a kind, and the roll when it's time: the first on
        ///     the first line counted, then every <paramref name="rollEveryLines" />. The chance is the next entry, so the
        ///     list's length caps the level, times <paramref name="odds" /> (Party Favors), up to certain.
        /// </summary>
        internal static bool RollPurple(
            IReadOnlyList<float> chances, int placedThisLevel, ref int linesCounted, int rollEveryLines,
            Func<float> random01, float odds = 1f)
        {
            if (chances == null || placedThisLevel >= chances.Count)
            {
                return false;
            }

            linesCounted++;
            if (linesCounted < rollEveryLines)
            {
                return false;
            }

            linesCounted = 0;
            var chance = Math.Min(1f, chances[placedThisLevel] * odds);
            return chance >= 1f || random01() < chance;
        }

        // The sure level is the first: a level before it never rolls, so the balloon is met there first.
        internal static bool IsOrdinaryLevel(in PlatedRewardTuning tuning, int level)
        {
            var every = tuning.OrdinaryEveryLevels;
            return every > 0 && level > 0 && level >= tuning.SureLevel && level % every == 0;
        }

        // The ease is hyperbolic, so it never reaches 0: half at ChanceHalvedAfterLevels past the sure level, a third
        // at twice that. The soft pity adds its growth for each level missed since the last one (or the sure level).
        internal static float OrdinaryChance(in PlatedRewardTuning tuning, int level, int lastPlacedLevel = 0)
        {
            var sureLevel = Math.Max(0, tuning.SureLevel);
            var chance = tuning.OrdinaryChance;
            var halvedAfter = tuning.ChanceHalvedAfterLevels;
            if (halvedAfter > 0)
            {
                chance /= 1f + Math.Max(0, level - sureLevel) / (float)halvedAfter;
            }

            var missed = Math.Max(0, level - Math.Max(lastPlacedLevel, sureLevel) - 1);
            return Math.Min(1f, chance + Math.Max(0f, tuning.ChanceGrowthPerLevel) * missed);
        }

        /// <summary>
        ///     Whether one is due on this line. <paramref name="odds" /> multiplies every roll's chance (Party Favors,
        ///     1 without it); a sure one needs no roll.
        /// </summary>
        internal bool Due(
            in PlatedRewardTuning tuning, ILevelParameters level, int levelNumber, ILevelProgress progress,
            int rollEveryLines, float odds = 1f)
        {
            if (_alive || level == null)
            {
                return false;
            }

            if (!level.IsExtraColorLevel)
            {
                return OrdinaryDue(in tuning, level, levelNumber, progress, odds);
            }

            if (_owed)
            {
                return true;
            }

            if (LevelCompletion.Of(progress, level.AllowedColors) < tuning.PurpleAtCompletion)
            {
                return false;
            }

            _owed = RollPurple(
                tuning.PurpleChances, _placedThisLevel, ref _rollLines, rollEveryLines, _random01, odds);
            return _owed;
        }

        internal Action Placed(int levelNumber)
        {
            _lastPlacedLevel = levelNumber;
            _placedThisLevel++;
            _owed = false;
            _alive = true;
            var placement = ++_placement;
            return () => Left(placement);
        }

        internal void ForgetLevel(int rollEveryLines)
        {
            _placedThisLevel = 0;
            _owed = false;
            _ordinaryRolled = false;
            _ordinaryWon = false;
            _pitySteps = 0;
            // Primed so the first line on a purple level rolls.
            _rollLines = rollEveryLines - 1;
        }

        internal void ResetRun(int rollEveryLines)
        {
            _placement++;
            _alive = false;
            _lastPlacedLevel = 0;
            ForgetLevel(rollEveryLines);
        }

        // Rolled on the level's first line, where the level's parameters are surely its own.
        private bool OrdinaryDue(
            in PlatedRewardTuning tuning, ILevelParameters level, int levelNumber, ILevelProgress progress,
            float odds)
        {
            if (!_ordinaryRolled)
            {
                _ordinaryRolled = true;
                var sureLevel = levelNumber > 0 && levelNumber == tuning.SureLevel;
                _ordinaryWon = sureLevel
                               || (IsOrdinaryLevel(in tuning, levelNumber)
                                   && _random01() < Math.Min(
                                       1f, OrdinaryChance(in tuning, levelNumber, _lastPlacedLevel) * odds));
                _ordinaryAt = sureLevel ? tuning.SureAtCompletion : tuning.AtCompletion;
            }

            var completion = LevelCompletion.Of(progress, level.AllowedColors);
            if (!_ordinaryWon)
            {
                RollCompletionPity(in tuning, levelNumber, completion, odds);
            }

            return _ordinaryWon && _placedThisLevel == 0 && completion >= _ordinaryAt;
        }

        // One roll for each step of completion reached since the last line, so a line that crosses two rolls both.
        private void RollCompletionPity(in PlatedRewardTuning tuning, int levelNumber, float completion, float odds)
        {
            if (tuning.PityEveryCompletion <= 0f || tuning.PityChancePerStep <= 0f
                || !IsOrdinaryLevel(in tuning, levelNumber))
            {
                return;
            }

            // The epsilon keeps a completion that is exactly a step (0.3 of 0.15) from rounding below it.
            var reached = (int)(completion / tuning.PityEveryCompletion + 0.0001f);
            while (_pitySteps < reached && !_ordinaryWon)
            {
                _pitySteps++;
                if (_random01() < Math.Min(1f, tuning.PityChancePerStep * _pitySteps * odds))
                {
                    _ordinaryWon = true;
                    _ordinaryAt = 0f;
                }
            }
        }

        private void Left(int placement)
        {
            if (placement == _placement)
            {
                _alive = false;
            }
        }
    }
}
