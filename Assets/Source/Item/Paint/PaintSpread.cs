using System;
using System.Collections.Generic;
using BalloonParty.Item.Effects;
using UnityEngine;

namespace BalloonParty.Item.Paint
{
    /// <summary>Config-free Paint blob-selection core (@ref plan_shot_solver_accuracy Phase C5) — runs
    /// identically over <c>GridEffectBoard</c> (live) and <c>ShotSimEffectBoard</c> (solver), mirroring
    /// <c>PaintItemHandler.CollectPaintTargets</c>/<c>TryClassify</c>/<c>NearestBlob</c>'s bucketing
    /// exactly, minus the drip/flight/disturbance-stamp presentation. Selection is grid TOPOLOGY, not
    /// physical overlap (like <c>LightningChain</c>) — every occupant is bucketed against its NEAREST
    /// packed blob over <see cref="EffectOccupant.SlotPosition" /> (the lattice home), never
    /// <see cref="EffectOccupant.Position" />, mirroring the live handler comparing
    /// <c>_grid.IndexToWorldPosition(slot)</c> against each blob centre.
    /// <para/>
    /// A <see cref="EffectOccupant.ResistsPaint" /> occupant (tough/unbreakable) and any other
    /// non-<see cref="EffectOccupant.IsPaintable" /> occupant never produce an <see cref="EffectHit" />
    /// here — the live handler still plays a drip on a resist occupant
    /// (<c>PaintItemHandler.ApplyPaint</c>'s own accept/reject split), but that is presentation the sim
    /// never models, and <c>PaintItemHandler</c> is deliberately NOT repointed onto this core (unlike
    /// Bomb/Laser) — it keeps its own <c>CollectPaintTargets</c>/<c>TryClassify</c> for its own
    /// reject/drip bookkeeping. A future repoint that wants the drip list back can read
    /// <see cref="EffectOccupant.ResistsPaint" /> straight off <see cref="IEffectBoard.Occupants" />
    /// instead of this core emitting anything for it — no <see cref="EffectHitKind" /> exists to
    /// represent "no recolour, just cosmetics" anyway, so inventing one now would be speculative.</summary>
    internal static class PaintSpread
    {
        /// <summary>Every paintable occupant whose colour differs from <paramref name="paintColorId" />
        /// (mirrors <c>TryClassify</c>'s accept branch — the same plain-string-equality check that also
        /// skips an already-rainbow balloon when the holder itself paints rainbow) is bucketed to its
        /// SINGLE nearest blob centre in <paramref name="blobPositions" />; a bucket farther than
        /// <paramref name="blobRadius" /> from every blob is dropped entirely (mirrors
        /// <c>CollectPaintTargets</c>'s own <c>nearestSqr &gt; radiusSqr</c> gate). Emits one
        /// <see cref="EffectHitKind.Recolor" /> per accepted occupant to <paramref name="paintColorId" />,
        /// with <see cref="EffectHit.Group" /> set to that nearest blob's index.</summary>
        internal static void Resolve(
            IEffectBoard board, IReadOnlyList<Vector2> blobPositions, float blobRadius, string paintColorId,
            List<EffectHit> hitsOut)
        {
            if (hitsOut == null)
            {
                return;
            }

            hitsOut.Clear();

            if (blobPositions == null || blobPositions.Count == 0)
            {
                return;
            }

            var radiusSqr = blobRadius * blobRadius;
            var occupants = board.Occupants;

            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (!occupant.IsPaintable
                    || string.Equals(occupant.ColorId, paintColorId, StringComparison.Ordinal))
                {
                    continue;
                }

                var nearest = NearestBlob(blobPositions, occupant.SlotPosition, out var nearestSqr);
                if (nearestSqr > radiusSqr)
                {
                    continue;
                }

                hitsOut.Add(EffectHit.Recolor(occupant.Handle, paintColorId, nearest));
            }
        }

        // Mirrors PaintItemHandler.NearestBlob exactly.
        private static int NearestBlob(IReadOnlyList<Vector2> blobPositions, Vector2 position, out float bestSqr)
        {
            var best = 0;
            bestSqr = float.MaxValue;
            for (var i = 0; i < blobPositions.Count; i++)
            {
                var sqr = (blobPositions[i] - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }
    }
}
