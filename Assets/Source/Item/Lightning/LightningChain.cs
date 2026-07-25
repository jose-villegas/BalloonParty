using System;
using System.Collections.Generic;
using BalloonParty.Item.Effects;
using UnityEngine;

namespace BalloonParty.Item.Lightning
{
    /// <summary>Config-free Lightning chain-selection core (@ref plan_shot_solver_accuracy Phase C4) —
    /// runs identically over <c>GridEffectBoard</c> (live) and <c>ShotSimEffectBoard</c> (solver),
    /// mirroring <c>LightningItemHandler.CollectSortedTargets</c>/<c>Activate</c>'s selection exactly,
    /// minus the chain/glow/light presentation. Unlike Bomb/Laser, selection is grid TOPOLOGY, not
    /// physical overlap — matches read <see cref="EffectOccupant.SlotPosition" /> (the lattice home),
    /// never <see cref="EffectOccupant.Position" />.</summary>
    internal static class LightningChain
    {
        // Cube direction vectors for the six hex edges, used by FindNearestConcreteColor's ring walk —
        // mirrors BalloonModelExtensions.FindNearestColorId's own table exactly. Duplicated rather than
        // shared: that helper is SlotGrid-bound (live-only) and this core must stay grid-free so it
        // runs over either board adapter.
        private static readonly (int dq, int dr)[] CubeDirections =
        {
            (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)
        };

        // Reused per activation: matched occupants paired with their squared distance from the chain's
        // origin, sorted nearest-first — mirrors LightningItemHandler's own per-activation targets list
        // + ByDistanceComparer. Single-threaded scratch-buffer convention, same as
        // BombBlast.NeighborBuffer/LaserCross.HitScratch.
        private static readonly List<(int handle, float sqrDistance)> Matches = new();

        // Handle as the secondary key: List.Sort is unstable and the two effect boards enumerate in
        // different orders, so a pure-distance comparison would let EXACTLY equidistant targets swap
        // jump order between boards. Handles follow each board's own enumeration, so this pins a
        // deterministic order per board; a residual live-vs-sim tie swap stays a declared
        // approximation until the live handler shares this core.
        private static readonly Comparison<(int handle, float sqrDistance)> ByDistance =
            (a, b) => a.sqrDistance != b.sqrDistance
                ? a.sqrDistance.CompareTo(b.sqrDistance)
                : a.handle.CompareTo(b.handle);

        /// <summary>Every occupant whose <see cref="EffectOccupant.ColorId" /> equals
        /// <paramref name="matchColorId" /> (a plain string match — mirrors
        /// <c>CollectSortedTargets</c>'s own gate exactly, with no separate rainbow exclusion; callers
        /// guarantee <paramref name="matchColorId" /> is already a concrete colour), distance-ordered
        /// nearest-first from <paramref name="origin" /> over lattice <see cref="EffectOccupant.SlotPosition" />
        /// (mirrors <c>_grid.IndexToWorldPosition</c>-based sorting — topology, not live view transforms).
        /// <see cref="EffectHit.Group" /> carries each match's jump index in that order. A normal host
        /// (<paramref name="convertsToRainbow" /> false) emits a <see cref="EffectHitKind.Damage" /> hit
        /// per match, unconditionally (mirrors the live handler's own unconditional
        /// <c>_hitDispatcher.Dispatch</c> call). A rainbow host instead CONVERTS — emits
        /// <see cref="EffectHitKind.Recolor" /> to <paramref name="rainbowColorId" /> for every
        /// <see cref="EffectOccupant.IsPaintable" /> match only (mirrors the live handler's own
        /// <c>model is IPaintable</c> guard); a matched-but-not-paintable occupant still consumes a jump
        /// index (the chain still visits it) but produces no hit. <paramref name="hostSlot" /> is unused
        /// by the selection itself — the board already excludes the popped host (see
        /// <c>ShotSimEffectBoard.Bind</c>/<c>GridEffectBoard.Rebuild</c>'s own exclusion) — and is kept
        /// only for signature symmetry with <c>BombBlast</c>/<c>LaserCross</c>.</summary>
        internal static void Resolve(
            IEffectBoard board, Vector2 origin, Vector2Int hostSlot, string matchColorId, bool convertsToRainbow,
            string rainbowColorId, List<EffectHit> hitsOut)
        {
            if (hitsOut == null)
            {
                return;
            }

            hitsOut.Clear();
            Matches.Clear();

            var occupants = board.Occupants;
            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (!string.Equals(occupant.ColorId, matchColorId, StringComparison.Ordinal))
                {
                    continue;
                }

                Matches.Add((occupant.Handle, (occupant.SlotPosition - origin).sqrMagnitude));
            }

            Matches.Sort(ByDistance);

            for (var jump = 0; jump < Matches.Count; jump++)
            {
                var handle = Matches[jump].handle;

                if (!convertsToRainbow)
                {
                    hitsOut.Add(EffectHit.Damage(handle, jump));
                    continue;
                }

                if (occupants[handle].IsPaintable)
                {
                    hitsOut.Add(EffectHit.Recolor(handle, rainbowColorId, jump));
                }
            }
        }

        /// <summary>Starting at <paramref name="center" />, searches outward in concentric hex rings for
        /// the nearest occupant with a concrete (non-rainbow, non-empty) colour — mirrors
        /// <c>BalloonModelExtensions.FindNearestColorId</c>'s ring walk exactly (same corner/side/step
        /// traversal, same odd-row offset↔cube conversion), over <paramref name="board" /> instead of a
        /// live <c>SlotGrid</c> (<see cref="IEffectBoard.SearchRadius" /> stands in for
        /// <c>Mathf.Max(grid.Columns, grid.Rows)</c>; <see cref="IEffectBoard.TryGetOccupantAt" /> stands
        /// in for <c>grid.At</c>/<c>IsEmpty</c>/<c>InBounds</c> — a missing/out-of-bounds slot returns
        /// false either way). No explicit exclude parameter: the board already excludes the popped host
        /// (same reasoning as <see cref="Resolve" />'s own <c>hostSlot</c> doc). Returns null if no
        /// concrete colour exists on the board.</summary>
        internal static string FindNearestConcreteColor(IEffectBoard board, Vector2Int center, string rainbowColorId)
        {
            if (TryGetConcreteColorAt(board, center, rainbowColorId, out var found))
            {
                return found;
            }

            var maxRadius = board.SearchRadius;
            var centerQ = center.x - ((center.y - (center.y & 1)) / 2);
            var centerR = center.y;

            for (var ring = 1; ring <= maxRadius; ring++)
            {
                if (TrySearchRing(board, centerQ, centerR, ring, rainbowColorId, out found))
                {
                    return found;
                }
            }

            return null;
        }

        private static bool TrySearchRing(
            IEffectBoard board, int centerQ, int centerR, int ring, string rainbowColorId, out string found)
        {
            var q = centerQ + (CubeDirections[4].dq * ring);
            var r = centerR + (CubeDirections[4].dr * ring);

            for (var side = 0; side < 6; side++)
            {
                for (var step = 0; step < ring; step++)
                {
                    var col = q + ((r - (r & 1)) / 2);
                    var slot = new Vector2Int(col, r);

                    if (TryGetConcreteColorAt(board, slot, rainbowColorId, out found))
                    {
                        return true;
                    }

                    q += CubeDirections[side].dq;
                    r += CubeDirections[side].dr;
                }
            }

            found = null;
            return false;
        }

        private static bool TryGetConcreteColorAt(
            IEffectBoard board, Vector2Int slot, string rainbowColorId, out string colorId)
        {
            if (!board.TryGetOccupantAt(slot, out var occupant))
            {
                colorId = null;
                return false;
            }

            var color = occupant.ColorId;
            if (string.IsNullOrEmpty(color)
                || (!string.IsNullOrEmpty(rainbowColorId) && string.Equals(color, rainbowColorId, StringComparison.Ordinal)))
            {
                colorId = null;
                return false;
            }

            colorId = color;
            return true;
        }
    }
}
