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
    ///         <see cref="PlatedRewardTuning.SureLevel" /> needs no roll and brings its one early
    ///         (<see cref="PlatedRewardTuning.SureAtCompletion" />), the player's first sight of the balloon.
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
        // A purple level's lines counted towards the next roll.
        private int _rollLines;
        private bool _alive;
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
        ///     list's length caps the level.
        /// </summary>
        internal static bool RollPurple(
            IReadOnlyList<float> chances, int placedThisLevel, ref int linesCounted, int rollEveryLines,
            Func<float> random01)
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
            var chance = chances[placedThisLevel];
            return chance >= 1f || random01() < chance;
        }

        // The sure level is the first: a level before it never rolls, so the balloon is met there first.
        internal static bool IsOrdinaryLevel(in PlatedRewardTuning tuning, int level)
        {
            var every = tuning.OrdinaryEveryLevels;
            return every > 0 && level > 0 && level >= tuning.SureLevel && level % every == 0;
        }

        internal bool Due(
            in PlatedRewardTuning tuning, ILevelParameters level, int levelNumber, ILevelProgress progress,
            int rollEveryLines)
        {
            if (_alive || level == null)
            {
                return false;
            }

            if (!level.IsExtraColorLevel)
            {
                return OrdinaryDue(in tuning, level, levelNumber, progress);
            }

            if (_owed)
            {
                return true;
            }

            if (LevelCompletion.Of(progress, level.AllowedColors) < tuning.PurpleAtCompletion)
            {
                return false;
            }

            _owed = RollPurple(tuning.PurpleChances, _placedThisLevel, ref _rollLines, rollEveryLines, _random01);
            return _owed;
        }

        internal Action Placed()
        {
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
            // Primed so the first line on a purple level rolls.
            _rollLines = rollEveryLines - 1;
        }

        internal void ResetRun(int rollEveryLines)
        {
            _placement++;
            _alive = false;
            ForgetLevel(rollEveryLines);
        }

        // Rolled on the level's first line, where the level's parameters are surely its own.
        private bool OrdinaryDue(
            in PlatedRewardTuning tuning, ILevelParameters level, int levelNumber, ILevelProgress progress)
        {
            if (!_ordinaryRolled)
            {
                _ordinaryRolled = true;
                var sure = levelNumber > 0 && levelNumber == tuning.SureLevel;
                _ordinaryWon = sure || (IsOrdinaryLevel(in tuning, levelNumber) && _random01() < tuning.OrdinaryChance);
                _ordinaryAt = sure ? tuning.SureAtCompletion : tuning.AtCompletion;
            }

            return _ordinaryWon && _placedThisLevel == 0
                                && LevelCompletion.Of(progress, level.AllowedColors) >= _ordinaryAt;
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
