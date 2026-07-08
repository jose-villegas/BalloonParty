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
            // A streak never carries across turns.
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

        /// <summary>Records a pop attribution and returns the streak multiplier to apply to points.</summary>
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

        /// <summary>Extends the streak regardless of colour — used while the projectile carries a
        /// colour-agnostic (rainbow) buff, so every pop keeps the multiplier climbing.</summary>
        public int RecordWildcard()
        {
            CurrentStreak++;
            return CurrentStreak;
        }

        internal void Reset()
        {
            LastColor = null;
            CurrentStreak = 0;
        }
    }
}
