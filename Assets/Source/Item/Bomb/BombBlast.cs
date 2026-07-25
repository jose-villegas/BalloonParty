using System.Collections.Generic;
using BalloonParty.Item.Effects;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Item.Bomb
{
    /// <summary>Bomb's config-free blast-selection scalars (@ref plan_shot_solver_accuracy Phase C2) —
    /// mirrors <c>BombSettings</c>'s kill-radius fields only; <c>RainbowEffectScale</c> is presentation-
    /// only (scales the VISUAL effect, never the kill radius — see <c>BombItemHandler.Activate</c>'s own
    /// comment) so it never rides along here.</summary>
    internal readonly struct BombBlastParams
    {
        public readonly float Radius;
        public readonly float RainbowConversionRange;

        public BombBlastParams(float radius, float rainbowConversionRange)
        {
            Radius = radius;
            RainbowConversionRange = rainbowConversionRange;
        }
    }

    /// <summary>Config-free Bomb blast-selection core (@ref plan_shot_solver_accuracy Phase C2) — runs
    /// identically over <c>GridEffectBoard</c> (live) and <c>ShotSimEffectBoard</c> (solver), mirroring
    /// <c>BombItemHandler.BlastBalloons</c>/<c>RainbowBlast</c>'s geometry exactly, minus the shockwave/
    /// disturbance-field/light presentation (unmodeled — @ref plan_shot_solver_accuracy Risk R5) and
    /// Physics2D itself (the board's own <see cref="IEffectBoard.Occupants" /> already excludes the
    /// popped host and any disabled-collider view — see <c>GridEffectBoard.Rebuild</c>).</summary>
    internal static class BombBlast
    {
        // Reused across calls — the solver and live handlers both run single-threaded, so a shared
        // scratch buffer never aliases across concurrent resolves.
        private static readonly Vector2Int[] NeighborBuffer = new Vector2Int[6];

        /// <summary>Normal (non-rainbow host) blast: every occupant whose CENTRE lies within
        /// <see cref="BombBlastParams.Radius" /> plus its own radius takes a hit — mirrors
        /// <c>Physics2D.OverlapCircle</c>'s circle-vs-circle overlap test exactly. A hex neighbour of
        /// <paramref name="hostSlot" /> among THOSE hits is a guaranteed kill (<see cref="EffectHitKind.PiercingDamage" />) —
        /// a hex neighbour OUTSIDE the overlap radius gets no hit at all, exactly like live (the
        /// neighbour check there only ever runs over <c>OverlapCircle</c>'s own result set, never a
        /// separate query). Rainbow host: centre-distance ALONE (no added occupant radius — mirrors
        /// <c>RainbowBlast</c>'s plain <c>sqrMagnitude</c> classification, unlike the physics-driven
        /// normal path) within <see cref="BombBlastParams.Radius" /> is a guaranteed kill; within the
        /// wider <see cref="BombBlastParams.RainbowConversionRange" /> ring, a paintable occupant
        /// recolors instead of dying.</summary>
        internal static void Resolve(
            IEffectBoard board, Vector2 origin, Vector2Int hostSlot, bool isRainbowHost, string rainbowColorId,
            in BombBlastParams p, List<EffectHit> hitsOut)
        {
            if (hitsOut == null)
            {
                return;
            }

            hitsOut.Clear();

            if (isRainbowHost)
            {
                ResolveRainbow(board, origin, rainbowColorId, in p, hitsOut);
                return;
            }

            ResolveNormal(board, origin, hostSlot, in p, hitsOut);
        }

        private static void ResolveNormal(
            IEffectBoard board, Vector2 origin, Vector2Int hostSlot, in BombBlastParams p, List<EffectHit> hitsOut)
        {
            HexCoordinates.HexNeighborIndices(hostSlot.x, hostSlot.y, NeighborBuffer);
            var occupants = board.Occupants;

            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                var combinedRadius = p.Radius + occupant.Radius;
                if ((occupant.Position - origin).sqrMagnitude > combinedRadius * combinedRadius)
                {
                    continue;
                }

                hitsOut.Add(IsHexNeighbor(occupant.Slot)
                    ? EffectHit.PiercingDamage(occupant.Handle)
                    : EffectHit.Damage(occupant.Handle));
            }
        }

        private static bool IsHexNeighbor(Vector2Int slot)
        {
            for (var n = 0; n < 6; n++)
            {
                if (NeighborBuffer[n] == slot)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ResolveRainbow(
            IEffectBoard board, Vector2 origin, string rainbowColorId, in BombBlastParams p, List<EffectHit> hitsOut)
        {
            var killRadius = p.Radius;
            var outerRadius = p.Radius + p.RainbowConversionRange;
            var killSqr = killRadius * killRadius;
            var outerSqr = outerRadius * outerRadius;
            var occupants = board.Occupants;

            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                var distSqr = (occupant.Position - origin).sqrMagnitude;

                if (distSqr <= killSqr)
                {
                    hitsOut.Add(EffectHit.PiercingDamage(occupant.Handle));
                }
                else if (distSqr <= outerSqr && occupant.IsPaintable)
                {
                    hitsOut.Add(EffectHit.Recolor(occupant.Handle, rainbowColorId));
                }
            }
        }
    }
}
