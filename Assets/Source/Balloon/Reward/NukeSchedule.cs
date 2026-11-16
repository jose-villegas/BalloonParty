using System;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     When the nuke balloon is due (PLAN-NukeBalloon), and whether one is on the board. Every clock line from
    ///     <see cref="INukeOddsSettings.FromLevel" /> on rolls <see cref="INukeOdds.Chance" />, up to
    ///     <see cref="INukeOddsSettings.PerLevel" /> a level, once <see cref="INukeOddsSettings.CooldownLines" /> lines
    ///     have passed since the last was placed. <see cref="INukeOddsSettings.SureLevel" /> brings one for sure,
    ///     <see cref="INukeOddsSettings.SureAfterLines" /> lines in. A won roll stays due until the nuke is placed: a
    ///     line with no room for it passes it on. A new level drops one still owed.
    /// </summary>
    internal sealed class NukeSchedule
    {
        private readonly INukeOddsSettings _settings;
        private readonly INukeOdds _odds;
        private readonly Func<float> _random01;

        private bool _alive;
        private bool _owed;
        private int _placedThisLevel;
        private int _linesSinceLast;
        private int _linesThisLevel;
        // Which placement a leave callback belongs to; a stale one (an earlier nuke, or one from before a run
        // reset) must not clear the flag of the nuke on the board now.
        private int _placement;

        internal NukeSchedule(INukeOddsSettings settings, INukeOdds odds, Func<float> random01)
        {
            _settings = settings;
            _odds = odds;
            _random01 = random01;
            ResetRun();
        }

        /// <summary>One clock line: whether it brings a nuke. Asked once per line while the level is played.</summary>
        internal bool Due(int levelNumber)
        {
            if (_linesSinceLast < int.MaxValue)
            {
                _linesSinceLast++;
            }

            _linesThisLevel++;

            var due = Roll(levelNumber);
            _odds.LinePassed();
            return due;
        }

        internal Action Placed()
        {
            _placedThisLevel++;
            _owed = false;
            _alive = true;
            _linesSinceLast = 0;
            var placement = ++_placement;
            return () => Left(placement);
        }

        internal void ForgetLevel()
        {
            _placedThisLevel = 0;
            _linesThisLevel = 0;
            _owed = false;
        }

        // The run's first nuke waits on no cooldown.
        internal void ResetRun()
        {
            _placement++;
            _alive = false;
            _linesSinceLast = int.MaxValue;
            ForgetLevel();
        }

        private bool Roll(int levelNumber)
        {
            if (_alive)
            {
                return false;
            }

            if (_owed)
            {
                return true;
            }

            if (_placedThisLevel >= _settings.PerLevel)
            {
                return false;
            }

            if (levelNumber == _settings.SureLevel && _linesThisLevel >= _settings.SureAfterLines)
            {
                _owed = true;
                return true;
            }

            if (levelNumber < _settings.FromLevel || _linesSinceLast < _settings.CooldownLines)
            {
                return false;
            }

            var chance = _odds.Chance;
            _owed = chance > 0f && (chance >= 1f || _random01() < chance);
            return _owed;
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
