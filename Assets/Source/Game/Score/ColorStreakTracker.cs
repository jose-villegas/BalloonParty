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
        private bool _carryOnColorChange;

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

        /// <summary>Records a balloon pop against the streak and returns the multiplier to score it at.
        /// Pass <paramref name="canIncrement" /> as false for a pop that didn't come from the shot itself
        /// (an item/AOE pop): a same-colour pop then keeps the streak alive at its current multiplier
        /// without pushing it higher, and a colour change still tracks the new colour (so a later direct
        /// hit resumes from the right place) but can't cash in a carried rainbow bonus or the deferred
        /// bank — both stay armed, untouched, for whichever direct hit claims them. <paramref
        /// name="breaksStreak" /> always resets regardless of <paramref name="canIncrement" />.</summary>
        public int Record(string colorId, bool breaksStreak = false, bool canIncrement = true)
        {
            if (breaksStreak)
            {
                Reset();
                return 1;
            }

            if (colorId == LastColor)
            {
                if (!canIncrement)
                {
                    return CurrentStreak;
                }

                CurrentStreak++;
                PublishChanged();
                return CurrentStreak;
            }

            if (!canIncrement)
            {
                if (_carryOnColorChange && CurrentStreak > 0)
                {
                    // Only the direct hit that actually changes the projectile's own colour may cash in
                    // a carried rainbow bonus — leave it (and the colour it's waiting to land on) alone
                    // for whichever hit claims it. This pop just scores at 1, unattributed to any streak.
                    return 1;
                }

                LastColor = colorId;
                CurrentStreak = 1;
                PublishChanged();
                return CurrentStreak;
            }

            if (_carryOnColorChange && CurrentStreak > 0)
            {
                // A rainbow pop flagged carry: the multiplier transfers to the new colour intact,
                // folding in any deferred rainbow pops banked since the carry was armed.
                CurrentStreak += 1 + _deferredPops;
            }
            else
            {
                // Flush any deferred rainbow pops into the new colour's streak — they happened
                // before the projectile had a colour, so they count toward this first real hit.
                CurrentStreak = 1 + _deferredPops;
            }

            LastColor = colorId;
            _carryOnColorChange = false;
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
            _carryOnColorChange = true;
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
            _carryOnColorChange = false;
            PublishChanged();
        }

        private void PublishChanged()
        {
            _changedPublisher.Publish(new StreakChangedMessage(LastColor, CurrentStreak));
        }
    }
}
