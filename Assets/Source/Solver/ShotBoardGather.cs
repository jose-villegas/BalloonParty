using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Item;
using BalloonParty.Item.Effects;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using BalloonParty.Thrower;
using UnityEngine;

namespace BalloonParty.Solver
{
    /// <summary>Everything one <see cref="ShotSimulator.Simulate" /> sweep needs, gathered from the
    /// live game — shared by the editor Shot Solver window and the Fire Best Shot cheat so the two
    /// can never gather differently.</summary>
    internal readonly struct ShotSolveContext
    {
        public readonly IReadOnlyList<ShotBalloonSnapshot> Board;
        public readonly Vector4 WallLimitsClockwise;
        public readonly Vector2 ThrowerPivot;
        public readonly Vector3 SpawnLocalOffset;
        public readonly int StartingShields;
        public readonly float ProjectileContactRadius;
        public readonly float ProjectileSpeed;
        public readonly ShotCruiseConfig CruiseConfig;
        public readonly ShotBoardDynamics Dynamics;
        public readonly float NudgeAmplitude;

        /// <summary>The board-global colours a rainbow pays/counts under (Phase D-core) — the live
        /// active level's <see cref="ILevelParameters.AllowedColors" />. Was per-balloon on
        /// <c>ColorProfile</c>; hoisted here since it's board-global, not per-target.</summary>
        public readonly IReadOnlyList<string> AllowedColors;

        /// <summary>The reserved rainbow/wildcard colour marker (<c>GamePalette.RainbowColorId</c>) —
        /// threaded through so <c>ShotSimulator</c>'s pop-neighbour conversion can write it without
        /// depending on <see cref="IGamePalette" /> itself (a plain string is all the sim needs).</summary>
        public readonly string RainbowColorId;

        /// <summary>Item-carrier selection + params (Phase C) — a peer of <see cref="Dynamics" />: both
        /// are opt-in board-scoped services <c>ShotSimulator.Simulate</c> accepts nullable.</summary>
        public readonly ShotItemLayer Items;

        public ShotSolveContext(
            IReadOnlyList<ShotBalloonSnapshot> board, Vector4 wallLimitsClockwise, Vector2 throwerPivot,
            Vector3 spawnLocalOffset, int startingShields, float projectileContactRadius,
            float projectileSpeed, ShotCruiseConfig cruiseConfig, ShotBoardDynamics dynamics,
            float nudgeAmplitude, IReadOnlyList<string> allowedColors, string rainbowColorId, ShotItemLayer items)
        {
            Board = board;
            WallLimitsClockwise = wallLimitsClockwise;
            ThrowerPivot = throwerPivot;
            SpawnLocalOffset = spawnLocalOffset;
            StartingShields = startingShields;
            ProjectileContactRadius = projectileContactRadius;
            ProjectileSpeed = projectileSpeed;
            CruiseConfig = cruiseConfig;
            Dynamics = dynamics;
            NudgeAmplitude = nudgeAmplitude;
            AllowedColors = allowedColors;
            RainbowColorId = rainbowColorId;
            Items = items;
        }
    }

    /// <summary>Gathers the live board/thrower/config into a <see cref="ShotSolveContext" /> and hosts
    /// the shared per-angle plumbing (rotated launch origin, one-call simulate).</summary>
    internal static class ShotBoardGather
    {
        /// <summary>Snapshots the live game. <paramref name="pulseExecutionDelay" /> models the
        /// balancer's render-frame lag (callers estimate ~1.5 × frame time).</summary>
        internal static ShotSolveContext Gather(
            SlotGrid grid, IProjectileFlightConfig config, ISlotGridConfig gridConfig,
            IBalloonsConfiguration balloonsConfig, IItemConfiguration itemConfig, ThrowerView thrower,
            ThrowerSettings throwerSettings, IGamePalette palette, IActiveLevelParameters levelParams,
            float pulseExecutionDelay)
        {
            var targets = new List<ShotBalloonSnapshot>();
            var otherDynamicActors = new List<ShotDynamicActorSnapshot>();
            var staticActors = new List<ShotStaticActorSnapshot>();
            CollectBoard(grid, balloonsConfig, palette, targets, otherDynamicActors, staticActors);

            var dynamics = new ShotBoardDynamics(
                gridConfig, balloonsConfig, targets, otherDynamicActors, staticActors, pulseExecutionDelay);
            var cruiseConfig = new ShotCruiseConfig(config);
            var lattice = ShotSlotLattice.From(gridConfig);
            var items = new ShotItemLayer(ItemEffectParams.FromConfiguration(itemConfig), in lattice);

            // Un-rotate the spawn point back into the thrower's aim-neutral frame so per-angle
            // simulation can re-rotate it — the launch origin orbits the pivot with the aim.
            var spawnLocalOffset =
                Quaternion.Inverse(thrower.Rotation) * (thrower.SpawnPointPosition - thrower.Position);

            return new ShotSolveContext(
                targets,
                config.LimitsClockwise,
                thrower.Position,
                spawnLocalOffset,
                config.ProjectileStartingShields,
                ResolveProjectileContactRadius(throwerSettings),
                config.ProjectileSpeed,
                cruiseConfig,
                dynamics,
                balloonsConfig.NudgeDistance,
                levelParams.Current.AllowedColors,
                GamePalette.RainbowColorId,
                items);
        }

        internal static ShotSimulationResult SimulateAt(
            float angleDegrees, in ShotSolveContext context, ShotBalloonState[] workingSet,
            List<Vector2> pathOut = null, List<float> timesOut = null, float radiusBias = 0f,
            string targetColorId = null, in ShotFlightSeed seed = default)
        {
            return ShotSimulator.Simulate(
                context.Board, context.WallLimitsClockwise, OriginForAngle(angleDegrees, context),
                DirectionFromDegrees(angleDegrees), context.StartingShields, context.ProjectileContactRadius,
                workingSet, pathOut: pathOut, projectileSpeed: context.ProjectileSpeed,
                cruiseConfig: context.CruiseConfig, dynamics: context.Dynamics, timestampsOut: timesOut,
                targetColorId: targetColorId, radiusBias: radiusBias, allowedColors: context.AllowedColors,
                rainbowColorId: context.RainbowColorId, seed: seed, items: context.Items);
        }

        // The thrower rotates around its pivot to aim (ThrowerView.RotateTo: fire-direction angle − 90°),
        // carrying the child spawn point with it — so the true launch origin orbits the pivot per angle.
        internal static Vector2 OriginForAngle(float angleDegrees, in ShotSolveContext context)
        {
            var rotation = Quaternion.AngleAxis(angleDegrees - 90f, Vector3.forward);
            return context.ThrowerPivot + (Vector2)(rotation * context.SpawnLocalOffset);
        }

        internal static Vector2 DirectionFromDegrees(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        // Every occupied slot feeds the dynamic-board sim's SlotGrid: poppable/deflectable shot
        // targets — including never-popping Unbreakables — carry geometry + balance/nudge properties;
        // any other Dynamic occupant carries balance properties only; Static occupants their slot only.
        private static void CollectBoard(
            SlotGrid grid, IBalloonsConfiguration balloonsConfig, IGamePalette palette,
            List<ShotBalloonSnapshot> targets, List<ShotDynamicActorSnapshot> otherDynamicActors,
            List<ShotStaticActorSnapshot> staticActors)
        {
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var index = new Vector2Int(col, row);
                    var actor = grid.At(index);

                    if (actor.Kind == SlotActorKind.Static)
                    {
                        // A static always occupies its slot for the balance planner; an interactive
                        // archetype (Deflector/Gatekeeper/Absorber — IHitable) ADDITIONALLY becomes a
                        // collision target, still at that same slot (Phase A).
                        staticActors.Add(new ShotStaticActorSnapshot(index));
                        if (actor is IHitable hitable
                            && TryBuildStaticContactSnapshot(grid, index, hitable, out var staticTarget))
                        {
                            targets.Add(staticTarget);
                        }

                        continue;
                    }

                    if (TryBuildTargetSnapshot(grid, balloonsConfig, palette, index, actor, out var target))
                    {
                        targets.Add(target);
                        continue;
                    }

                    var influence = actor as IBalanceInfluence;
                    var biasSource = actor as IBalanceBiasSource;
                    otherDynamicActors.Add(new ShotDynamicActorSnapshot(
                        index, influence?.BalancePriority ?? 0, influence?.MaxBalanceSteps ?? 0,
                        balloonsConfig.ResolveMoveSpeed(influence?.MoveSpeed ?? 0f),
                        influence?.DirectBalanceMotion ?? false, influence?.OmnidirectionalBalance ?? false,
                        biasSource?.ColorId ?? "", biasSource?.BiasKind ?? BalanceBiasKind.None,
                        biasSource?.BiasValue ?? 0f, biasSource?.BiasTypeId ?? 0));
                }
            }
        }

        // Durable + scorable actors are poppable/deflectable targets. Unbreakables have no
        // IHasDurability (EvaluateHit never mutates HitsRemaining) yet still DEFLECT the live shot,
        // so they enter as never-popping deflect geometry — int.MaxValue durability keeps the sim's
        // HitsRemaining > 1 branch permanently deflecting (deflects score nothing, matching the game).
        private static bool TryBuildTargetSnapshot(
            SlotGrid grid, IBalloonsConfiguration balloonsConfig, IGamePalette palette, Vector2Int index,
            IWriteableSlotActor actor, out ShotBalloonSnapshot snapshot)
        {
            snapshot = default;

            int hitsRemaining;
            if (actor is IHasDurability durable)
            {
                hitsRemaining = durable.HitsRemaining.Value;
            }
            else if (actor is UnbreakableBalloonModel)
            {
                hitsRemaining = int.MaxValue;
            }
            else
            {
                return false;
            }

            if (actor is not IHasScore scored)
            {
                return false;
            }

            var colorId = actor is IHasColor colorable ? colorable.Color.Value : null;
            var washesProjectileColor = actor is IWashesProjectileColor;
            var influence = actor as IBalanceInfluence;
            var biasSource = actor as IBalanceBiasSource;
            var nudgeOverrides = actor is IHasNudge nudgeable ? nudgeable.NudgeOverrides : null;

            // The view's live position, not the slot's lattice home: balance tweens and nudge wobble
            // displace views, and the shot collides with the view's collider — the slot is where the
            // balloon belongs, the view is where it IS right now.
            var view = grid.ActorViewAt<BalloonView>(index);
            var radius = view != null ? view.ContactRadius : 0f;
            var position = view != null
                ? (Vector2)view.transform.position
                : (Vector2)grid.IndexToWorldPosition(index);

            var balance = new BalanceProfile(
                index, influence?.BalancePriority ?? 0, influence?.MaxBalanceSteps ?? 0,
                balloonsConfig.ResolveMoveSpeed(influence?.MoveSpeed ?? 0f),
                influence?.DirectBalanceMotion ?? false, nudgeOverrides,
                omnidirectional: influence?.OmnidirectionalBalance ?? false,
                biasKind: biasSource?.BiasKind ?? BalanceBiasKind.None,
                biasValue: biasSource?.BiasValue ?? 0f,
                biasTypeId: biasSource?.BiasTypeId ?? 0);

            var item = BuildItemProfile(actor, view);

            // A rainbow balloon's colour IS a real (non-empty) IHasColor value — the reserved
            // GamePalette.RainbowColorId marker — so it must be classified BEFORE the colored/tough
            // branch below, or it silently mis-scores as an ordinary green target (the latent bug
            // this factory closes).
            if (!string.IsNullOrEmpty(colorId) && palette.IsRainbow(colorId))
            {
                snapshot = ShotBalloonSnapshot.ForRainbowTarget(
                    position, radius, colorId, scored.ScoreValue, hitsRemaining, balance, item);
            }
            else if (string.IsNullOrEmpty(colorId))
            {
                snapshot = ShotBalloonSnapshot.ForToughTarget(
                    position, radius, scored.ScoreValue, hitsRemaining, balance, washesProjectileColor);
            }
            else
            {
                snapshot = ShotBalloonSnapshot.ForColorTarget(
                    position, radius, colorId, scored.ScoreValue, hitsRemaining, balance, item);
            }

            return true;
        }

        // Only BalloonModel implements IHasItemSlot — a hosted item's spin is data-only until Phase C
        // reads it; only the Laser's held icon spins (ISpinningItemVisual), so every other item type
        // (or no item at all) gathers 0 for both spin fields. LaserItemRotation.CaptureSnapshot is
        // destructive (stops the spin) — this reads the same component through TransformCapture instead.
        private static ItemProfile? BuildItemProfile(IWriteableSlotActor actor, BalloonView view)
        {
            if (actor is not IHasItemSlot itemSlot || itemSlot.Item.Value == ItemType.None)
            {
                return null;
            }

            var spinning = view?.TransformCapture as ISpinningItemVisual;
            return new ItemProfile(itemSlot.Item.Value, spinning?.AngleDegrees ?? 0f, spinning?.SpinDegreesPerSecond ?? 0f);
        }

        // Interactive static archetypes (Deflector/Gatekeeper/Absorber) collide with the shot while
        // still occupying their slot for the planner (staticActors, in CollectBoard) — durability comes
        // from IHasDurability when present (Gatekeeper), else the deflect-forever convention
        // (int.MaxValue, mirroring TryBuildTargetSnapshot's Unbreakable case) for a static with no
        // durability capability at all (Deflector, Absorber).
        private static bool TryBuildStaticContactSnapshot(
            SlotGrid grid, Vector2Int index, IHitable hitable, out ShotBalloonSnapshot snapshot)
        {
            var hitsRemaining = hitable is IHasDurability durable ? durable.HitsRemaining.Value : int.MaxValue;

            // Mirrors TryBuildTargetSnapshot's view-position preference — a static's own view has no
            // balance tween to displace it, but this keeps both paths identical in shape.
            var view = grid.ActorViewAt<GridActorView>(index);
            var radius = view != null ? view.ContactRadius : 0f;
            var position = view != null
                ? (Vector2)view.transform.position
                : (Vector2)grid.IndexToWorldPosition(index);

            snapshot = ShotBalloonSnapshot.ForStaticContact(index, position, radius, ClassifyContactKind(hitable), hitsRemaining);
            return true;
        }

        // A damage-0 probe is non-mutating for all three archetypes: AbsorberActorModel/
        // DeflectorActorModel ignore damage entirely, and GatekeeperActorModel's
        // Math.Max(0, HitsRemaining - 0) writes back the same value — safe to call at gather time.
        // DeflectForever isn't a separate case: durability (HitsRemaining) alone decides whether a
        // Poppable-kind contact deflects forever (int.MaxValue) or eventually pops.
        internal static ShotContactKind ClassifyContactKind(IHitable actor)
        {
            return actor.EvaluateHit(new DamageContext(0)) == HitOutcome.Absorb
                ? ShotContactKind.Absorb
                : ShotContactKind.Poppable;
        }

        // Mirrors ProjectileView.Awake's own (private) contact-radius derivation — a capsule's
        // cross-section half-extent, or a circle's radius, scaled by the prefab's world scale.
        private static float ResolveProjectileContactRadius(ThrowerSettings settings)
        {
            var prefabView = settings?.ProjectilePrefab;
            if (prefabView == null)
            {
                return 0f;
            }

            var collider = prefabView.GetComponent<Collider2D>();
            return ContactRadius.FromCollider(collider, prefabView.transform.lossyScale.x);
        }
    }
}
