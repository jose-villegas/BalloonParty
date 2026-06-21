using BalloonParty.Balloon.Model;
using BalloonParty.Game.Score;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UnityEngine;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     Resolves what happens when a projectile collides with a balloon: evaluates the hit, applies
    ///     the colour-steal and streak-shield rules, mutates the projectile model, and publishes the
    ///     resulting messages. Plain C# so the rules are testable without a collider — the view only
    ///     supplies the already-filtered collision and plays the returned <see cref="ProjectileHitVisual" />.
    /// </summary>
    internal class ProjectileHitResolver
    {
        private readonly IPublisher<ActorHitMessage> _hitPublisher;
        private readonly IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        private readonly ColorStreakTracker _streakTracker;

        public ProjectileHitResolver(
            IPublisher<ActorHitMessage> hitPublisher,
            IPublisher<ShieldGainedMessage> shieldGainedPublisher,
            ColorStreakTracker streakTracker)
        {
            _hitPublisher = hitPublisher;
            _shieldGainedPublisher = shieldGainedPublisher;
            _streakTracker = streakTracker;
        }

        public ProjectileHitVisual Resolve(
            IWriteableProjectileModel projectile,
            IBalloonModel balloon,
            Vector3 balloonWorldPosition)
        {
            projectile.LastHitBalloon = balloon;

            var damageContext = new DamageContext(1, DamageFlags.Normal, projectile.ColorName.Value);
            var outcome = balloon.EvaluateHit(damageContext);

            if (outcome == HitOutcome.Absorb)
            {
                _hitPublisher.Publish(new ActorHitMessage(
                    balloon, balloonWorldPosition, projectile.Direction, HitOutcome.Absorb));
                projectile.IsFree = false;
                return ProjectileHitVisual.Destroyed;
            }

            var recolored = false;
            if (outcome == HitOutcome.Pop && balloon is IHasColor colorable &&
                !string.IsNullOrEmpty(colorable.Color.Value) &&
                projectile.ColorName.Value != colorable.Color.Value)
            {
                projectile.ColorName.Value = colorable.Color.Value;
                recolored = true;
            }

            _hitPublisher.Publish(new ActorHitMessage(
                balloon, balloonWorldPosition, projectile.Direction, outcome, damageContext));

            // ScoreController handled the message synchronously above, so the tracker is current.
            if (outcome == HitOutcome.Pop && balloon is IHasColor &&
                _streakTracker.CurrentStreak >= 2 &&
                _streakTracker.LastColor == projectile.ColorName.Value)
            {
                projectile.ShieldsRemaining.Value++;
                _shieldGainedPublisher.Publish(new ShieldGainedMessage(projectile.LastHitBalloon.SlotIndex.Value));
            }

            return recolored ? ProjectileHitVisual.Recolored : ProjectileHitVisual.None;
        }
    }
}
