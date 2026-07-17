using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Score;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
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
        private readonly SlotGrid _grid;
        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[6];

        public ProjectileHitResolver(
            IHitDispatcher hitDispatcher,
            IPublisher<ShieldGainedMessage> shieldGainedPublisher,
            ColorStreakTracker streakTracker,
            SlotGrid grid)
        {
            _hitDispatcher = hitDispatcher;
            _shieldGainedPublisher = shieldGainedPublisher;
            _streakTracker = streakTracker;
            _grid = grid;
        }

        public ProjectileHitVisual Resolve(
            IWriteableProjectileModel projectile,
            IBalloonModel balloon,
            Vector3 balloonWorldPosition)
        {
            projectile.LastHitBalloon = balloon;

            if (!projectile.IsPiercing.Value)
            {
                // Building phase: any balloon contact ends the empty-corridor cruise and restarts
                // the wall-bounce counter.
                projectile.ConsecutiveWallBounces = 0;
                if (projectile.IsCruising.Value)
                {
                    projectile.IsCruising.Value = false;
                }
            }
            else
            {
                // Armed piercing: the cruise (and its speed) rides on through pops — but plowing a
                // TOUGH actor (one that would take more than one hit) costs it half its current
                // speed. The motion resolver floors the total at base, and the next wall ends it.
                var requiresMultipleHits =
                    (balloon is IHasDurability durable && durable.HitsRemaining.Value > 1)
                    || balloon is UnbreakableBalloonModel;
                if (requiresMultipleHits)
                {
                    projectile.CruisePierceSpeedScale *= 0.5f;
                }
            }

            // A rainbow-buffed projectile pierces (plows through tough/unbreakable balloons instead of
            // one-shotting or deflecting off them), scores colour-agnostically, and rainbow-converts what
            // it pops near — until it loses a shield to a wall (which ends the buff).
            var isRainbowBuff = projectile.HasBuff(ProjectileBuffId.RainbowShield);
            var isPiercing = projectile.IsPiercing.Value;
            var flags = (isRainbowBuff ? DamageFlags.WildcardStreak | DamageFlags.Piercing : DamageFlags.Normal)
                        | (isPiercing ? DamageFlags.Piercing : DamageFlags.Normal)
                        | DamageFlags.DirectHit;
            var damageContext = new DamageContext(1, flags, projectile.ColorName.Value);
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
            var isWildcardPop = balloon is IHasColor wildcard && wildcard.Color.Value == GamePalette.RainbowColorId;

            // A colourless projectile popping a rainbow can't anchor the streak yet — defer the count
            // so it folds into the streak once the projectile adopts a real colour on a later hit.
            if (isWildcardPop && !isRainbowBuff && string.IsNullOrEmpty(projectile.ColorName.Value))
            {
                damageContext = new DamageContext(
                    damageContext.Damage,
                    damageContext.Flags | DamageFlags.DeferredStreak,
                    damageContext.SourceColorId);
            }

            var recolored = ApplyColorChange(projectile, balloon, outcome, isWildcardPop);

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

            // The pop has already left the grid (dispatch routed it synchronously), but its slot index
            // is retained on the model, so its surviving neighbours are still resolvable.
            if (isRainbowBuff && outcome == HitOutcome.Pop)
            {
                ConvertNeighborsToRainbow(balloon.SlotIndex.Value);
            }

            return recolored ? ProjectileHitVisual.Recolored : ProjectileHitVisual.None;
        }

        // Soap washes the projectile colourless on any contact; otherwise a normal (non-wildcard) pop
        // steals the balloon's colour. Returns whether the projectile's colour actually changed.
        private static bool ApplyColorChange(
            IWriteableProjectileModel projectile, IBalloonModel balloon, HitOutcome outcome, bool isWildcardPop)
        {
            if (balloon is IWashesProjectileColor)
            {
                if (string.IsNullOrEmpty(projectile.ColorName.Value))
                {
                    return false;
                }

                projectile.ColorName.Value = null;
                return true;
            }

            if (outcome == HitOutcome.Pop && !isWildcardPop && balloon is IHasColor colorable &&
                !string.IsNullOrEmpty(colorable.Color.Value) &&
                projectile.ColorName.Value != colorable.Color.Value)
            {
                projectile.ColorName.Value = colorable.Color.Value;
                return true;
            }

            return false;
        }

        private void ConvertNeighborsToRainbow(Vector2Int slot)
        {
            HexCoordinates.HexNeighborIndices(slot.x, slot.y, _neighborBuffer);

            for (var n = 0; n < 6; n++)
            {
                var neighbor = _neighborBuffer[n];
                if (_grid.IsEmpty(neighbor.x, neighbor.y))
                {
                    continue;
                }

                if (_grid.At(neighbor) is IPaintable paintable)
                {
                    paintable.Color.Value = GamePalette.RainbowColorId;
                }
            }
        }
    }
}
