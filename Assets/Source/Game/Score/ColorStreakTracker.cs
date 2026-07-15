using System;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Game.Score
{
    internal class ColorStreakTracker : IColorStreak, IDisposable
    {
        private readonly IPublisher<StreakChangedMessage> _changedPublisher;
        private readonly IDisposable _levelUpSubscription;
        private readonly IDisposable _projectileLoadedSubscription;

        private int _deferredPops;

        public string LastColor { get; private set; }
        public int CurrentStreak { get; private set; }

        internal ColorStreakTracker(
            IPublisher<StreakChangedMessage> changedPublisher,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            ISubscriber<ProjectileLoadedMessage> projectileLoadedSubscriber)
        {
            _changedPublisher = changedPublisher;
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
                // Flush any deferred rainbow pops into the new colour's streak — they happened
                // before the projectile had a colour, so they count toward this first real hit.
                LastColor = colorId;
                CurrentStreak = 1 + _deferredPops;
            }

            _deferredPops = 0;
            PublishChanged();
            return CurrentStreak;
        }

        /// <summary>Extends the streak regardless of colour — used while the projectile carries a
        /// colour-agnostic (rainbow) buff, so every pop keeps the multiplier climbing.</summary>
        public int RecordWildcard()
        {
            _deferredPops = 0;
            CurrentStreak++;
            PublishChanged();
            return CurrentStreak;
        }

        /// <summary>Banks a pop from a colourless projectile hitting a rainbow balloon. The count
        /// is folded into the streak the next time <see cref="Record"/> establishes a colour.</summary>
        public int RecordDeferred()
        {
            _deferredPops++;
            return _deferredPops;
        }

        internal void Reset()
        {
            LastColor = null;
            CurrentStreak = 0;
            _deferredPops = 0;
            PublishChanged();
        }

        private void PublishChanged()
        {
            _changedPublisher.Publish(new StreakChangedMessage(LastColor, CurrentStreak));
        }
    }
}
