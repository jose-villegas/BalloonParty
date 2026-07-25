using System.Collections.Generic;
using BalloonParty.Item.Effects;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Item.Laser
{
    /// <summary>Laser-core scalars — mirrors <c>LaserSettings</c>'s cast geometry only (colour-cycle/
    /// light/telegraph fields are live-presentation, never read by a config-free core).</summary>
    internal readonly struct LaserCrossParams
    {
        public readonly float CastRadius;
        public readonly float CastDistance;

        public LaserCrossParams(float castRadius, float castDistance)
        {
            CastRadius = castRadius;
            CastDistance = castDistance;
        }
    }

    /// <summary>Config-free Laser cross-selection core (@ref plan_shot_solver_accuracy Phase C3) — runs
    /// identically over <c>GridEffectBoard</c> (live) and <c>ShotSimEffectBoard</c> (solver), mirroring
    /// <c>LaserItemHandler.CastCross</c>/<c>ConvertBorderingNeighbors</c>'s geometry exactly, minus the
    /// beam/light/telegraph presentation and Physics2D itself (the board's own
    /// <see cref="IEffectBoard.Occupants" /> already excludes the popped host — see
    /// <c>GridEffectBoard.Rebuild</c>/<c>ShotSimEffectBoard.Bind</c>).</summary>
    internal static class LaserCross
    {
        // Reused across a rainbow host's bordering-neighbour pass (mirrors LaserItemHandler's own
        // _neighborBuffer) — the solver/live handlers both run single-threaded, so a shared scratch
        // buffer never aliases across concurrent resolves.
        private static readonly Vector2Int[] NeighborBuffer = new Vector2Int[6];

        // Per-activation dedup, indexed by EffectOccupant.Handle (its own index into
        // IEffectBoard.Occupants — see that struct's own doc): true once an occupant has produced ANY
        // EffectHit (damage OR recolor) this activation. Mirrors LaserItemHandler.CastCross's
        // _hitModels HashSet — an occupant straddling two crossing arms (only possible right at the
        // shared origin, where all four arms start) must only ever take one Damage hit; doubling as
        // the recolor guard also stops two hit occupants that share a bordering neighbour from
        // emitting the same Recolor twice (a value no-op live, since ReactiveProperty skips an
        // unchanged value, but a real duplicate entry in an EffectHit list here).
        private static readonly List<bool> HitScratch = new();

        // The direct-hit occupants THIS activation found, in discovery order — the rainbow-host
        // conversion pass's own source set (mirrors LaserItemHandler's _hitModels serving double duty
        // as both the dedup set and the conversion source).
        private static readonly List<int> HitHandles = new();

        /// <summary>Four arms along the rotated ±right/±up directions (a pure 2D rotation of
        /// <see cref="Vector2.right" />/<see cref="Vector2.up" /> by <paramref name="rotationDegrees" /> —
        /// mirrors <c>laserRotation * Vector3.right</c>/<c>Vector3.up</c> for a pure Z-axis rotation
        /// quaternion, see <c>LaserItemRotation</c>'s own <c>Quaternion.AngleAxis(_angle, Vector3.forward)</c>).
        /// Each arm is a swept-circle corridor: an occupant hits iff the segment-to-centre distance is
        /// within <see cref="LaserCrossParams.CastRadius" /> plus its own radius, at an entry distance in
        /// [0, <see cref="LaserCrossParams.CastDistance" />] — INCLUDING an occupant already overlapping
        /// at the origin (Unity's own <c>CircleCast</c> reports a start-overlap as an immediate hit).
        /// Every unique occupant hits ONCE even if two arms cross it. A rainbow host additionally
        /// converts every surviving, paintable, not-already-hit hex neighbour of each hit occupant.</summary>
        internal static void Resolve(
            IEffectBoard board, Vector2 origin, float rotationDegrees, bool isRainbowHost, string rainbowColorId,
            in LaserCrossParams p, List<EffectHit> hitsOut)
        {
            if (hitsOut == null)
            {
                return;
            }

            hitsOut.Clear();
            HitScratch.Clear();
            HitHandles.Clear();

            var occupants = board.Occupants;
            for (var i = 0; i < occupants.Count; i++)
            {
                HitScratch.Add(false);
            }

            var right = Rotate(Vector2.right, rotationDegrees);
            var up = Rotate(Vector2.up, rotationDegrees);

            CastArm(occupants, origin, right, in p, hitsOut);
            CastArm(occupants, origin, -right, in p, hitsOut);
            CastArm(occupants, origin, up, in p, hitsOut);
            CastArm(occupants, origin, -up, in p, hitsOut);

            if (isRainbowHost)
            {
                ConvertBorderingNeighbors(board, rainbowColorId, hitsOut);
            }
        }

        private static void CastArm(
            IReadOnlyList<EffectOccupant> occupants, Vector2 origin, Vector2 direction, in LaserCrossParams p,
            List<EffectHit> hitsOut)
        {
            for (var i = 0; i < occupants.Count; i++)
            {
                if (HitScratch[i])
                {
                    continue;
                }

                var occupant = occupants[i];
                var combinedRadius = p.CastRadius + occupant.Radius;
                if (!SegmentHitsCircle(origin, direction, p.CastDistance, occupant.Position, combinedRadius))
                {
                    continue;
                }

                HitScratch[i] = true;
                HitHandles.Add(occupant.Handle);
                hitsOut.Add(EffectHit.Damage(occupant.Handle));
            }
        }

        // Mirrors the segment-vs-circle solve in ShotSimulator.SegmentHitsAnyBalloon — that method is
        // private to its own class and shaped to short-circuit across a working-set index (a single
        // bool for "does anything block this segment"), not to collect every per-occupant hit an
        // EffectHit list needs, so it isn't cleanly reusable here as-is; this mirrors its exact formula
        // (including the start-overlap branch) instead of inventing a different one.
        private static bool SegmentHitsCircle(
            Vector2 origin, Vector2 direction, float segmentLength, Vector2 center, float combinedRadius)
        {
            var toCenter = origin - center;
            if (toCenter.sqrMagnitude <= combinedRadius * combinedRadius)
            {
                return true;
            }

            var along = Vector2.Dot(toCenter, direction);
            var discriminant = (along * along) - toCenter.sqrMagnitude + (combinedRadius * combinedRadius);
            if (discriminant < 0f)
            {
                return false;
            }

            var entryDistance = -along - Mathf.Sqrt(discriminant);
            return entryDistance >= 0f && entryDistance <= segmentLength;
        }

        // Mirrors LaserItemHandler.ConvertBorderingNeighbors: every hex neighbour of each hit occupant
        // that's paintable and not itself already hit converts. HitScratch doubles as the "already
        // produced a hit" guard here too (see its own doc), so a neighbour bordering two different hit
        // occupants only ever gets one Recolor.
        private static void ConvertBorderingNeighbors(IEffectBoard board, string rainbowColorId, List<EffectHit> hitsOut)
        {
            for (var h = 0; h < HitHandles.Count; h++)
            {
                var hitSlot = board.Occupants[HitHandles[h]].Slot;
                HexCoordinates.HexNeighborIndices(hitSlot.x, hitSlot.y, NeighborBuffer);

                for (var n = 0; n < 6; n++)
                {
                    if (!board.TryGetOccupantAt(NeighborBuffer[n], out var neighbor))
                    {
                        continue;
                    }

                    if (HitScratch[neighbor.Handle] || !neighbor.IsPaintable)
                    {
                        continue;
                    }

                    HitScratch[neighbor.Handle] = true;
                    hitsOut.Add(EffectHit.Recolor(neighbor.Handle, rainbowColorId));
                }
            }
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2((v.x * cos) - (v.y * sin), (v.x * sin) + (v.y * cos));
        }
    }
}
