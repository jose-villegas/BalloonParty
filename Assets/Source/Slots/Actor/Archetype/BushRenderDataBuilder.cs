using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration;
using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Assembles a bush's <see cref="BushRenderData"/> from its variant data and slot
    /// centers: per-slot branch material/matrix and leaf tiers, then merges every slot's
    /// leaves into ≤1023-instance batches so the whole bush draws in one instanced call
    /// per batch (one per tier in the common case) rather than one call per slot.
    /// </summary>
    internal sealed class BushRenderDataBuilder
    {
        private const int MaxInstancesPerBatch = 1023;

        private readonly IBushSettings _settings;
        private readonly BushMaterialSet _materials;

        internal BushRenderDataBuilder(IBushSettings settings, BushMaterialSet materials)
        {
            _settings = settings;
            _materials = materials;
        }

        internal BushRenderData Build(IReadOnlyList<Vector4> slotCenters, int slotCount)
        {
            var slots = new List<SlotRenderData>();
            var variants = _settings.BushVariants;
            if (variants == null || variants.Length == 0)
            {
                return new BushRenderData(
                    slots, System.Array.Empty<LeafBatch>(), System.Array.Empty<LeafBatch>());
            }

            var sprites = _settings.LeafAtlasSprites;

            for (var i = 0; i < slotCount; i++)
            {
                var center = slotCenters[i];
                var worldPos = new Vector2(center.x, center.y);
                var variant = variants[i % variants.Length];

                var entry = new SlotRenderData();
                ConfigureBranch(ref entry, worldPos, variant);
                ConfigureLeaves(ref entry, worldPos, variant, sprites, i);
                slots.Add(entry);
            }

            var innerBatches = BuildLeafBatches(slots, inner: true);
            var outerBatches = BuildLeafBatches(slots, inner: false);
            return new BushRenderData(slots, innerBatches, outerBatches);
        }

        private void ConfigureBranch(ref SlotRenderData entry, Vector2 worldPos, BushVariantData variant)
        {
            var material = _materials.GetBranchMaterial(variant.BranchMap);
            if (material == null)
            {
                return;
            }

            entry.BranchMaterial = material;

            var branchSpriteScale = Mathf.Max(_settings.BranchSpriteScale, 0.3f);
            var size = _settings.BushWorldSize / branchSpriteScale;
            entry.BranchMatrix = Matrix4x4.TRS(
                new Vector3(worldPos.x, worldPos.y, 0f),
                Quaternion.identity,
                new Vector3(size, size, 1f));
        }

        private void ConfigureLeaves(
            ref SlotRenderData entry, Vector2 worldPos, BushVariantData variant,
            Sprite[] sprites, int slotIndex)
        {
            if (_settings.LeafMaterial == null || sprites == null || sprites.Length == 0)
            {
                return;
            }

            var slots = variant.LeafSlots;
            entry.LeafSlots = slots;
            entry.WorldPos = worldPos;
            entry.ScaleCompensation = 1f / Mathf.Max(_settings.LeafSpriteScale, 0.3f);
            entry.PivotOffset = _settings.LeafPivotOffset;
            entry.BushWorldSize = _settings.BushWorldSize;

            var depthSplit = _settings.LeafDepthSplit;

            var innerIndices = new List<int>();
            var outerIndices = new List<int>();

            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].Depth < depthSplit)
                {
                    innerIndices.Add(i);
                }
                else
                {
                    outerIndices.Add(i);
                }
            }

            entry.InnerLeaves = BuildLeafTier(
                innerIndices, slots, sprites, worldPos, entry.ScaleCompensation, entry.PivotOffset,
                entry.BushWorldSize, slotIndex);
            entry.OuterLeaves = BuildLeafTier(
                outerIndices, slots, sprites, worldPos, entry.ScaleCompensation, entry.PivotOffset,
                entry.BushWorldSize, slotIndex);
        }

        private static LeafTier BuildLeafTier(
            IReadOnlyList<int> indices,
            IReadOnlyList<LeafSlotData> slots,
            Sprite[] sprites,
            Vector2 worldPos,
            float scaleCompensation,
            float pivotOffset,
            float bushWorldSize,
            int slotIndex)
        {
            var tier = new LeafTier
            {
                Count = indices.Count,
                SlotIndices = indices.ToArray(),
                Matrices = new Matrix4x4[indices.Count]
            };

            if (indices.Count == 0)
            {
                return tier;
            }

            var tints = new Vector4[indices.Count];
            var uvRects = new Vector4[indices.Count];
            var winds = new Vector4[indices.Count];

            for (var t = 0; t < indices.Count; t++)
            {
                var slot = slots[indices[t]];
                var leafWorldPos = worldPos + (slot.UVPosition - new Vector2(0.5f, 0.5f)) * bushWorldSize;

                // Static translation-only matrix — rotation and scale handled by shader
                tier.Matrices[t] = Matrix4x4.TRS(
                    new Vector3(leafWorldPos.x, leafWorldPos.y, 0f),
                    Quaternion.identity,
                    Vector3.one);

                var tint = (Color)slot.Tint;
                tints[t] = new Vector4(tint.r, tint.g, tint.b, tint.a);

                var spriteIndex = slotIndex % sprites.Length;
                var rect = sprites[spriteIndex].textureRect;
                var tex = sprites[spriteIndex].texture;
                uvRects[t] = new Vector4(
                    rect.x / tex.width,
                    rect.y / tex.height,
                    rect.width / tex.width,
                    rect.height / tex.height);

                // Per-instance wind data: phase, depth, baseAngle, scale
                winds[t] = new Vector4(
                    slot.PhaseOffset,
                    slot.Depth,
                    slot.BaseAngle,
                    slot.Scale * scaleCompensation);
            }

            tier.Tints = tints;
            tier.UVRects = uvRects;
            tier.Winds = winds;
            return tier;
        }

        private static LeafBatch[] BuildLeafBatches(IReadOnlyList<SlotRenderData> slots, bool inner)
        {
            var matrices = new List<Matrix4x4>();
            var tints = new List<Vector4>();
            var uvRects = new List<Vector4>();
            var winds = new List<Vector4>();

            foreach (var slot in slots)
            {
                var tier = inner ? slot.InnerLeaves : slot.OuterLeaves;
                for (var i = 0; i < tier.Count; i++)
                {
                    matrices.Add(tier.Matrices[i]);
                    tints.Add(tier.Tints[i]);
                    uvRects.Add(tier.UVRects[i]);
                    winds.Add(tier.Winds[i]);
                }
            }

            var total = matrices.Count;
            if (total == 0)
            {
                return System.Array.Empty<LeafBatch>();
            }

            var batchCount = (total + MaxInstancesPerBatch - 1) / MaxInstancesPerBatch;
            var batches = new LeafBatch[batchCount];

            for (var b = 0; b < batchCount; b++)
            {
                var start = b * MaxInstancesPerBatch;
                var count = Mathf.Min(MaxInstancesPerBatch, total - start);

                var batchMatrices = new Matrix4x4[count];
                var batchTints = new Vector4[count];
                var batchUVRects = new Vector4[count];
                var batchWinds = new Vector4[count];

                for (var i = 0; i < count; i++)
                {
                    batchMatrices[i] = matrices[start + i];
                    batchTints[i] = tints[start + i];
                    batchUVRects[i] = uvRects[start + i];
                    batchWinds[i] = winds[start + i];
                }

                var props = new MaterialPropertyBlock();
                props.SetVectorArray(BushShaderProperties.LeafTint, batchTints);
                props.SetVectorArray(BushShaderProperties.UVRect, batchUVRects);
                props.SetVectorArray(BushShaderProperties.LeafWind, batchWinds);

                batches[b] = new LeafBatch
                {
                    Matrices = batchMatrices,
                    Props = props,
                    Tints = batchTints,
                    UVRects = batchUVRects,
                    Winds = batchWinds,
                    Count = count
                };
            }

            return batches;
        }
    }
}
