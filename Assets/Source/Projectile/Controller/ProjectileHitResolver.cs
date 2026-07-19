using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Score;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Extensions;
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
        private readonly IItemConfiguration _itemConfig;
        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[6];

        public ProjectileHitResolver(
            IHitDispatcher hitDispatcher,
            IPublisher<ShieldGainedMessage> shieldGainedPublisher,
            ColorStreakTracker streakTracker,
            SlotGrid grid,
            IItemConfiguration itemConfig)
        {
            _hitDispatcher = hitDispatcher;
            _shieldGainedPublisher = shieldGainedPublisher;
            _streakTracker = streakTracker;
            _grid = grid;
            _itemConfig = itemConfig;
        }

        public ProjectileHitVisual Resolve(
            IWriteableProjectileModel projectile,
            IBalloonModel balloon,
            Vector3 balloonWorldPosition)
        {
            projectile.LastHitBalloon = balloon;

            var isPiercing = projectile.IsPiercing.Value;
            if (!isPiercing)
            {
                // Building phase: any balloon contact ends the empty-corridor cruise and restarts
                // the wall-bounce counter.
                projectile.Flight.ConsecutiveWallBounces = 0;
                if (projectile.IsCruising.Value)
                {
                    projectile.IsCruising.Value = false;
                }
            }

            // A piercing shot plows through a TOUGH actor (one that would take more than one hit)
            // WITHOUT popping it: the tough is recorded and shattered together with the rest at the
            // discharge, not on contact. Normal balloons still pop as the shot passes through.
            var plowsTough = isPiercing
                && ((balloon is IHasDurability durable && durable.HitsRemaining.Value > 1)
                    || balloon is UnbreakableBalloonModel);
            if (plowsTough)
            {
                projectile.Flight.PendingPierceHits.Add(new PendingPierceHit(balloon, balloonWorldPosition));
                // Re-arm the discharge countdown: it fires this-many-seconds after the LAST tough, so a
                // run of toughs holds it open and the whole line shatters together once the shot is clear.
                projectile.Flight.DischargeArmed = true;
                // Capture rainbow now, while the buff is live — the discharge ends the pierce (dropping
                // the buff) before it resolves, so HasBuff would read false there.
                projectile.Flight.PierceWasRainbow = projectile.HasBuff(ProjectileBuffId.RainbowShield);
                return ProjectileHitVisual.None;
            }

            return ResolveContactPop(projectile, balloon, balloonWorldPosition, isPiercing);
        }

        // The discharge: shatter every tough the shot plowed through but left standing, each at the
        // position it was struck (piercing kill — unbreakables included). Skips any that already left
        // the board since the plow. A rainbow lance also blooms a colour conversion around the shattered
        // line, scaled by how much armor it ate. Clears the pending set.
        public void DischargePending(IWriteableProjectileModel projectile)
        {
            var pending = projectile.Flight.PendingPierceHits;
            if (pending.Count == 0)
            {
                return;
            }

            // Captured at plow time: the pierce end that precedes this discharge already dropped the
            // RainbowShield buff, so HasBuff would read false here.
            var isRainbowBuff = projectile.Flight.PierceWasRainbow;
            var flags = DamageFlags.Piercing | DamageFlags.DirectHit
                        | (isRainbowBuff ? DamageFlags.WildcardStreak : DamageFlags.Normal);

            var center = Vector3.zero;
            foreach (var hit in pending)
            {
                center += hit.Position;
                var balloon = hit.Balloon;
                var slot = balloon.SlotIndex.Value;
                if (_grid.IsEmpty(slot.x, slot.y) || !ReferenceEquals(_grid.At(slot), balloon))
                {
                    continue;
                }

                var context = new DamageContext(1, flags, projectile.ColorName.Value);
                var outcome = balloon.EvaluateHit(context);
                _hitDispatcher.Dispatch(new ActorHitMessage(balloon, hit.Position, projectile.Direction, outcome, context));
            }

            // You can't paint armor, but shattering it powers the conversion of everything soft around
            // it — a rainbow lance blooms outward from the centre of the line it plowed, wider the more
            // toughs it ate. The toughs themselves are the fuel (popped above, and not paintable anyway).
            if (isRainbowBuff)
            {
                BloomConvert(center / pending.Count, pending.Count);
            }

            pending.Clear();
        }

        // The pop path for a balloon the shot destroys on contact (normal balloons, and any hit while
        // not piercing). Toughs under piercing never reach here — they are recorded and discharged.
        private ProjectileHitVisual ResolveContactPop(
            IWriteableProjectileModel projectile, IBalloonModel balloon, Vector3 balloonWorldPosition, bool isPiercing)
        {
            // A rainbow-buffed projectile pierces (plows through tough/unbreakable balloons instead of
            // one-shotting or deflecting off them), scores colour-agnostically, and rainbow-converts what
            // it pops near — until it loses a shield to a wall (which ends the buff).
            var isRainbowBuff = projectile.HasBuff(ProjectileBuffId.RainbowShield);
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

        // Converts every paintable balloon within the charge-scaled radius of the discharge centre to
        // rainbow. Charge is the count of plowed toughs; radius grows per charge up to a hard cap so a
        // big plow never eats the whole board. Non-paintable actors (toughs/unbreakables) are skipped.
        private void BloomConvert(Vector3 center, int toughCount)
        {
            var snipe = _itemConfig[ItemType.Snipe].Snipe;
            var charge = toughCount * snipe.ChargePerToughHit;
            var radius = Mathf.Min(snipe.BloomBaseRadius + charge * snipe.BloomRadiusPerCharge, snipe.BloomRadiusCap);

            foreach (var slot in _grid.AllOccupiedSlots())
            {
                if (_grid.At(slot) is IPaintable paintable
                    && _grid.IndexToWorldPosition(slot).WithinRadius(center, radius))
                {
                    paintable.Color.Value = GamePalette.RainbowColorId;
                }
            }
        }
    }
}
