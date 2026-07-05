using System;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Game.Score
{
    internal class ColorStreakTracker : IColorStreak, IDisposable
    {
        private readonly IDisposable _levelUpSubscription;
        private readonly IDisposable _projectileLoadedSubscription;

        public string LastColor { get; private set; }
        public int CurrentStreak { get; private set; }

        internal ColorStreakTracker(
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            ISubscriber<ProjectileLoadedMessage> projectileLoadedSubscriber)
        {
            _levelUpSubscription = levelUpSubscriber.Subscribe(_ => Reset());
            // Each new throw starts its own chain — a streak never carries across turns.
            _projectileLoadedSubscription = projectileLoadedSubscriber.Subscribe(_ => Reset());
        }

        public void Dispose()
        {
            _levelUpSubscription.Dispose();
            _projectileLoadedSubscription.Dispose();
        }

        public int GetStreak(string colorName)
        {
            return LastColor == colorName ? CurrentStreak : 0;
        }

        /// <summary>
        /// Records a pop attribution. Returns the streak multiplier to apply to points.
        /// When <paramref name="breaksStreak"/> is true the chain is reset and 1 is returned —
        /// the attribution still scores but accrues no multiplier bonus.
        /// </summary>
        public int Record(string colorId, bool breaksStreak = false)
        {
            if (breaksStreak)
            {
                Reset();
                return 1;
            }

            if (colorId == LastColor)
            {
                CurrentStreak++;
            }
            else
            {
                LastColor = colorId;
                CurrentStreak = 1;
            }

            return CurrentStreak;
        }

        private void Reset()
        {
            LastColor = null;
            CurrentStreak = 0;
        }
    }
}
