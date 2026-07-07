using BalloonParty.Balloon.Model;
using BalloonParty.Game.Score;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UnityEngine;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>Plain C# so hit rules are testable without a collider.</summary>
    internal class ProjectileHitResolver
    {
        private readonly IHitDispatcher _hitDispatcher;
        private readonly IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        private readonly ColorStreakTracker _streakTracker;

        public ProjectileHitResolver(
            IHitDispatcher hitDispatcher,
            IPublisher<ShieldGainedMessage> shieldGainedPublisher,
            ColorStreakTracker streakTracker)
        {
            _hitDispatcher = hitDispatcher;
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
                _hitDispatcher.Dispatch(new ActorHitMessage(
                    balloon, balloonWorldPosition, projectile.Direction, HitOutcome.Absorb));
                projectile.IsFree = false;
                return ProjectileHitVisual.Destroyed;
            }

            // A wildcard (rainbow) pop keeps the projectile's colour — that's what lets its streak
            // carry — even though the balloon is otherwise IHasColor.
            var isWildcardPop = balloon is IHasRainbowMode wildcard && wildcard.IsRainbow.Value;

            var recolored = false;
            if (outcome == HitOutcome.Pop && !isWildcardPop && balloon is IHasColor colorable &&
                !string.IsNullOrEmpty(colorable.Color.Value) &&
                projectile.ColorName.Value != colorable.Color.Value)
            {
                projectile.ColorName.Value = colorable.Color.Value;
                recolored = true;
            }

            _hitDispatcher.Dispatch(new ActorHitMessage(
                balloon, balloonWorldPosition, projectile.Direction, outcome, damageContext));

            // Dispatch runs the streak stage synchronously, so the tracker is already current here.
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
