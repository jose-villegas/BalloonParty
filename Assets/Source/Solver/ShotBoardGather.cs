using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
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

        public ShotSolveContext(
            IReadOnlyList<ShotBalloonSnapshot> board, Vector4 wallLimitsClockwise, Vector2 throwerPivot,
            Vector3 spawnLocalOffset, int startingShields, float projectileContactRadius,
            float projectileSpeed, ShotCruiseConfig cruiseConfig, ShotBoardDynamics dynamics,
            float nudgeAmplitude)
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
            IBalloonsConfiguration balloonsConfig, ThrowerView thrower, ThrowerSettings throwerSettings,
            float pulseExecutionDelay)
        {
            var targets = new List<ShotBalloonSnapshot>();
            var otherDynamicActors = new List<ShotDynamicActorSnapshot>();
            var staticActors = new List<ShotStaticActorSnapshot>();
            CollectBoard(grid, targets, otherDynamicActors, staticActors);

            var dynamics = new ShotBoardDynamics(
                gridConfig, balloonsConfig, targets, otherDynamicActors, staticActors, pulseExecutionDelay);
            var cruiseConfig = new ShotCruiseConfig(
                config.CruiseWallBounceThreshold, config.CruiseSpeedPerShield,
                config.MaxCruiseSpeedMultiplier,
                config.CruiseTapEaseDuration, config.CruiseTapCurve, config.CruisePiercingTapThreshold);

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
                balloonsConfig.NudgeDistance);
        }

        internal static ShotSimulationResult SimulateAt(
            float angleDegrees, in ShotSolveContext context, ShotBalloonState[] workingSet,
            List<Vector2> pathOut = null, List<float> timesOut = null, float radiusBias = 0f,
            string targetColorId = null)
        {
            return ShotSimulator.Simulate(
                context.Board, context.WallLimitsClockwise, OriginForAngle(angleDegrees, context),
                DirectionFromDegrees(angleDegrees), context.StartingShields, context.ProjectileContactRadius,
                workingSet, pathOut: pathOut, projectileSpeed: context.ProjectileSpeed,
                cruiseConfig: context.CruiseConfig, dynamics: context.Dynamics, timestampsOut: timesOut,
                targetColorId: targetColorId, radiusBias: radiusBias);
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
            SlotGrid grid, List<ShotBalloonSnapshot> targets, List<ShotDynamicActorSnapshot> otherDynamicActors,
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
                        staticActors.Add(new ShotStaticActorSnapshot(index));
                        continue;
                    }

                    if (TryBuildTargetSnapshot(grid, index, actor, out var target))
                    {
                        targets.Add(target);
                        continue;
                    }

                    var influence = actor as IBalanceInfluence;
                    otherDynamicActors.Add(new ShotDynamicActorSnapshot(
                        index, influence?.BalancePriority ?? 0, influence?.MaxBalanceSteps ?? 0,
                        influence?.DirectBalanceMotion ?? false));
                }
            }
        }

        // Durable + scorable actors are poppable/deflectable targets. Unbreakables have no
        // IHasDurability (EvaluateHit never mutates HitsRemaining) yet still DEFLECT the live shot,
        // so they enter as never-popping deflect geometry — int.MaxValue durability keeps the sim's
        // HitsRemaining > 1 branch permanently deflecting (deflects score nothing, matching the game).
        private static bool TryBuildTargetSnapshot(
            SlotGrid grid, Vector2Int index, IWriteableSlotActor actor, out ShotBalloonSnapshot snapshot)
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
            var influence = actor as IBalanceInfluence;
            var nudgeOverrides = actor is IHasNudge nudgeable ? nudgeable.NudgeOverrides : null;

            // The view's live position, not the slot's lattice home: balance tweens and nudge wobble
            // displace views, and the shot collides with the view's collider — the slot is where the
            // balloon belongs, the view is where it IS right now.
            var view = grid.ActorViewAt<BalloonView>(index);
            var radius = view != null ? view.ContactRadius : 0f;
            var position = view != null
                ? (Vector2)view.transform.position
                : (Vector2)grid.IndexToWorldPosition(index);

            snapshot = new ShotBalloonSnapshot(
                position, radius, colorId, scored.ScoreValue, hitsRemaining, index,
                influence?.BalancePriority ?? 0, influence?.MaxBalanceSteps ?? 0,
                influence?.DirectBalanceMotion ?? false, nudgeOverrides);
            return true;
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
            return collider switch
            {
                CircleCollider2D circle => circle.radius * prefabView.transform.lossyScale.x,
                CapsuleCollider2D capsule =>
                    Mathf.Min(capsule.size.x, capsule.size.y) * 0.5f * prefabView.transform.lossyScale.x,
                _ => 0f,
            };
        }
    }
}
