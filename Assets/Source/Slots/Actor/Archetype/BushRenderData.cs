using System.Collections.Generic;
using BalloonParty.Configuration;
using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// One depth tier of leaf instances for a single slot — translation matrices plus
    /// the per-instance tint, atlas UV rect, and wind parameters the leaf shader reads.
    /// </summary>
    internal struct LeafTier
    {
        internal Matrix4x4[] Matrices;
        internal int Count;
        internal int[] SlotIndices;
        internal Vector4[] Tints;
        internal Vector4[] UVRects;
        internal Vector4[] Winds;
    }

    /// <summary>
    /// A draw-ready batch of leaves merged across slots (≤1023 instances), with the
    /// per-instance arrays pre-packed into a <see cref="MaterialPropertyBlock"/>.
    /// </summary>
    internal struct LeafBatch
    {
        internal Matrix4x4[] Matrices;
        internal MaterialPropertyBlock Props;
        internal Vector4[] Tints;
        internal Vector4[] UVRects;
        internal Vector4[] Winds;
        internal int Count;
    }

    /// <summary>
    /// Per-slot branch material/matrix plus the slot's two leaf tiers and the source
    /// data the editor gizmos draw from.
    /// </summary>
    internal struct SlotRenderData
    {
        internal Material BranchMaterial;
        internal Matrix4x4 BranchMatrix;
        internal LeafTier InnerLeaves;
        internal LeafTier OuterLeaves;
        internal IReadOnlyList<LeafSlotData> LeafSlots;
        internal Vector2 WorldPos;
        internal float ScaleCompensation;
        internal float PivotOffset;
        internal float BushWorldSize;
    }

    /// <summary>
    /// The full set of render data for one bush: per-slot entries (for branch draws and
    /// gizmos) plus the merged inner/outer leaf batches submitted each frame.
    /// </summary>
    internal sealed class BushRenderData
    {
        internal BushRenderData(
            IReadOnlyList<SlotRenderData> slots, LeafBatch[] innerBatches, LeafBatch[] outerBatches)
        {
            Slots = slots;
            InnerBatches = innerBatches;
            OuterBatches = outerBatches;
        }

        internal IReadOnlyList<SlotRenderData> Slots { get; }
        internal LeafBatch[] InnerBatches { get; }
        internal LeafBatch[] OuterBatches { get; }
    }
}
