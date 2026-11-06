using System;
using System.Collections.Generic;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;

namespace BalloonParty.Game.Score
{
    /// <summary>
    ///     Owns a <see cref="ColorStreak" /> per ball. A pop records against the ball that caused it (an
    ///     item pop against the ball that set the item off); a pop no ball caused, or one credited to a
    ///     ball already retired, records against one shared streak that resets on every load. A ball's
    ///     streak is dropped when the registry retires the ball, and every streak resets on level-up.
    /// </summary>
    internal class ColorStreakTracker : IColorStreak, IDisposable
    {
        private readonly IPublisher<StreakChangedMessage> _changedPublisher;
        private readonly IProjectileRegistry _projectiles;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly ColorStreak _unattributed;
        private readonly Dictionary<IProjectileModel, ColorStreak> _byBall = new();
        private readonly Stack<ColorStreak> _spare = new();

        public int HighestStreak
        {
            get
            {
                var best = _unattributed.CurrentStreak;
                foreach (var pair in _byBall)
                {
                    if (_projectiles.IsAlive(pair.Key))
                    {
                        best = Math.Max(best, pair.Value.CurrentStreak);
                    }
                }

                return best;
            }
        }

        internal ColorStreakTracker(
            IPublisher<StreakChangedMessage> changedPublisher,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            ISubscriber<ProjectileLoadedMessage> projectileLoadedSubscriber,
            IProjectileRegistry projectiles)
        {
            _changedPublisher = changedPublisher;
            _projectiles = projectiles;
            _unattributed = new ColorStreak(changedPublisher);
            levelUpSubscriber.Subscribe(_ => Reset()).AddTo(_subscriptions);

            // The shared streak never carries across turns, as the single streak never did.
            projectileLoadedSubscriber.Subscribe(_ => _unattributed.Reset()).AddTo(_subscriptions);
            projectiles.Retired.Subscribe(OnRetired).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        /// <summary>
        ///     The highest streak on <paramref name="colorName" /> among balls still in play. A landed
        ///     ball's streak is over even before it retires, so it never shows past its death.
        /// </summary>
        public int GetStreak(string colorName)
        {
            var best = _unattributed.GetStreak(colorName);
            foreach (var pair in _byBall)
            {
                if (_projectiles.IsAlive(pair.Key))
                {
                    best = Math.Max(best, pair.Value.GetStreak(colorName));
                }
            }

            return best;
        }

        /// <summary>
        ///     The streak of <paramref name="projectile" />. A null or already-retired ball gets the shared
        ///     streak, so a late pop can never resurrect an entry nothing would drop.
        /// </summary>
        public ColorStreak For(IProjectileModel projectile)
        {
            if (projectile == null || !_projectiles.IsTracked(projectile))
            {
                return _unattributed;
            }

            if (!_byBall.TryGetValue(projectile, out var streak))
            {
                streak = _spare.Count > 0 ? _spare.Pop() : new ColorStreak(_changedPublisher);
                streak.Owner = projectile;
                _byBall[projectile] = streak;
            }

            return streak;
        }

        /// <summary>Resets every streak and announces it once, if any had anything to reset.</summary>
        internal void Reset()
        {
            var changed = !_unattributed.IsClear;
            foreach (var streak in _byBall.Values)
            {
                changed |= !streak.IsClear;
                streak.Clear();
            }

            _unattributed.Clear();
            if (changed)
            {
                _changedPublisher.Publish(new StreakChangedMessage(null, null, 0));
            }
        }

        // The ball's streak stopped counting when it landed (GetStreak skips it), but readers only
        // re-query on a message, and with rapid fire no load follows a death to announce it. So a ball
        // that ends with a streak announces its own end, once; a ball with nothing to end stays silent.
        private void OnRetired(IProjectileModel projectile)
        {
            if (!_byBall.Remove(projectile, out var streak))
            {
                return;
            }

            var hadStreak = !streak.IsClear;
            streak.Clear();
            streak.Owner = null;
            _spare.Push(streak);
            if (hadStreak)
            {
                _changedPublisher.Publish(new StreakChangedMessage(projectile, null, 0));
            }
        }
    }
}
