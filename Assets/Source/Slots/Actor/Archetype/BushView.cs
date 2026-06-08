using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Slots.Actor.Cluster;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Cluster renderer for bush obstacles. Renders depth-tiered leaf quads and a
    /// branch quad per slot via <c>Graphics.DrawMesh</c> / <c>DrawMeshInstanced</c>.
    /// Inner leaves (low depth) render behind branches; outer leaves (high depth)
    /// render in front — creating natural layering without per-fragment Z.
    /// Falls back to per-leaf <c>DrawMesh</c> when GPU instancing is unavailable.
    /// </summary>
    internal class BushView : ClusterView
    {
        private static readonly int LeafTintId = Shader.PropertyToID("_LeafTint");
        private static readonly int UVRectId = Shader.PropertyToID("_UVRect");
        private static readonly int LeafWindId = Shader.PropertyToID("_LeafWind");
        private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int ShadowOffsetId = Shader.PropertyToID("_ShadowOffset");
        private static readonly int ShadowSoftnessId = Shader.PropertyToID("_ShadowSoftness");
        private static readonly int SpriteScaleId = Shader.PropertyToID("_SpriteScale");
        private static readonly int WindFrequencyId = Shader.PropertyToID("_WindFrequency");
        private static readonly int WindAmplitudeId = Shader.PropertyToID("_WindAmplitude");
        private static readonly int WindNoiseAmplitudeId = Shader.PropertyToID("_WindNoiseAmplitude");
        private static readonly int WindScalePulseId = Shader.PropertyToID("_WindScalePulse");
        private static readonly int PivotOffsetId = Shader.PropertyToID("_PivotOffset");

        private static bool? _supportsInstancing;

        private readonly List<SlotRenderData> _slotRenderData = new();

        private static Mesh _sharedLeafQuad;
        private static Mesh _sharedBranchQuad;
        private IBushSettings _settings;
        private MaterialPropertyBlock _fallbackMpb;

        internal IReadOnlyList<SlotRenderData> SlotRenderEntries => _slotRenderData;

        private static bool SupportsInstancing
        {
            get
            {
                _supportsInstancing ??= SystemInfo.supportsInstancing;
                return _supportsInstancing.Value;
            }
        }

        internal void SetSettings(IBushSettings settings)
        {
            _settings = settings;
        }

        protected override void OnConfigured(MaterialPropertyBlock block)
        {
            if (Renderer != null)
            {
                Renderer.enabled = false;
            }

            RebuildSlots();
        }

        private void LateUpdate()
        {
            var branchMesh = GetBranchQuadMesh();
            var leafMesh = GetLeafQuadMesh();
            var layer = gameObject.layer;

            foreach (var slot in _slotRenderData)
            {
                DrawLeafTier(leafMesh, slot.InnerLeaves, slot.InnerLeafMaterial, layer);

                if (slot.BranchMaterial != null)
                {
                    Graphics.DrawMesh(
                        branchMesh,
                        slot.BranchMatrix,
                        slot.BranchMaterial,
                        layer);
                }

                DrawLeafTier(leafMesh, slot.OuterLeaves, slot.OuterLeafMaterial, layer);
            }
        }

        private void DrawLeafTier(Mesh leafMesh, LeafTier tier, Material material, int layer)
        {
            if (tier.Count <= 0 || material == null)
            {
                return;
            }

            if (SupportsInstancing)
            {
                Graphics.DrawMeshInstanced(
                    leafMesh,
                    0,
                    material,
                    tier.Matrices,
                    tier.Count,
                    tier.Props);
                return;
            }

            _fallbackMpb ??= new MaterialPropertyBlock();

            for (var i = 0; i < tier.Count; i++)
            {
                _fallbackMpb.SetVector(LeafTintId, tier.Tints[i]);
                _fallbackMpb.SetVector(UVRectId, tier.UVRects[i]);
                _fallbackMpb.SetVector(LeafWindId, tier.Winds[i]);
                Graphics.DrawMesh(leafMesh, tier.Matrices[i], material, layer, null, 0, _fallbackMpb);
            }
        }

        private void RebuildSlots()
        {
            _slotRenderData.Clear();

            if (_settings == null)
            {
                return;
            }

            var variants = _settings.BushVariants;
            if (variants == null || variants.Length == 0)
            {
                return;
            }

            var sprites = _settings.LeafAtlasSprites;

            for (var i = 0; i < SlotCount; i++)
            {
                var center = SlotCentersBuffer[i];
                var worldPos = new Vector2(center.x, center.y);
                var variant = variants[i % variants.Length];

                var entry = new SlotRenderData();
                ConfigureBranch(ref entry, worldPos, variant);
                ConfigureLeaves(ref entry, worldPos, variant, sprites, i);
                _slotRenderData.Add(entry);
            }
        }

        private void ConfigureBranch(
            ref SlotRenderData entry, Vector2 worldPos, BushVariantData variant)
        {
            if (_settings.BranchShader == null || variant.BranchMap == null)
            {
                return;
            }

            entry.BranchMaterial = new Material(_settings.BranchShader)
            {
                mainTexture = variant.BranchMap,
                renderQueue = 3000
            };

            var size = _settings.BushWorldSize;
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

            // Partition leaves into inner (below branches) and outer (above branches)
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

            entry.InnerLeafMaterial = CreateLeafMaterial(sprites, 2999);
            entry.OuterLeafMaterial = CreateLeafMaterial(sprites, 3001);

            entry.InnerLeaves = BuildLeafTier(
                innerIndices, slots, sprites, worldPos, entry.ScaleCompensation, entry.PivotOffset,
                entry.BushWorldSize, slotIndex);
            entry.OuterLeaves = BuildLeafTier(
                outerIndices, slots, sprites, worldPos, entry.ScaleCompensation, entry.PivotOffset,
                entry.BushWorldSize, slotIndex);
        }

        private Material CreateLeafMaterial(Sprite[] sprites, int queue)
        {
            var mat = new Material(_settings.LeafMaterial)
            {
                mainTexture = sprites[0].texture,
                enableInstancing = true,
                renderQueue = queue
            };
            mat.SetColor(ShadowColorId, _settings.LeafShadowColor);
            mat.SetVector(ShadowOffsetId, _settings.LeafShadowOffset);
            mat.SetFloat(ShadowSoftnessId, _settings.LeafShadowSoftness);
            mat.SetFloat(SpriteScaleId, _settings.LeafSpriteScale);

            var frequency = _settings.WindPeriod > 0f ? 1f / _settings.WindPeriod : 1f;
            mat.SetFloat(WindFrequencyId, frequency);
            mat.SetFloat(WindAmplitudeId, _settings.WindAmplitude);
            mat.SetFloat(WindNoiseAmplitudeId, _settings.WindNoiseAmplitude);
            mat.SetFloat(WindScalePulseId, _settings.WindScalePulse);
            mat.SetFloat(PivotOffsetId, _settings.LeafPivotOffset);
            return mat;
        }

        private static LeafTier BuildLeafTier(
            List<int> indices,
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

            tier.Props = new MaterialPropertyBlock();
            tier.Props.SetVectorArray(LeafTintId, tints);
            tier.Props.SetVectorArray(UVRectId, uvRects);
            tier.Props.SetVectorArray(LeafWindId, winds);
            return tier;
        }

        private static Mesh GetBranchQuadMesh()
        {
            if (_sharedBranchQuad != null)
            {
                return _sharedBranchQuad;
            }

            _sharedBranchQuad = new Mesh
            {
                name = "BushBranchQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            _sharedBranchQuad.UploadMeshData(true);

            return _sharedBranchQuad;
        }

        private static Mesh GetLeafQuadMesh()
        {
            if (_sharedLeafQuad != null)
            {
                return _sharedLeafQuad;
            }

            _sharedLeafQuad = new Mesh
            {
                name = "BushLeafQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, 0f, 0f),
                    new Vector3(0.5f, 0f, 0f),
                    new Vector3(0.5f, 1f, 0f),
                    new Vector3(-0.5f, 1f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            _sharedLeafQuad.UploadMeshData(true);

            return _sharedLeafQuad;
        }

        internal struct LeafTier
        {
            internal Matrix4x4[] Matrices;
            internal MaterialPropertyBlock Props;
            internal int Count;
            internal int[] SlotIndices;
            internal Vector4[] Tints;
            internal Vector4[] UVRects;
            internal Vector4[] Winds;
        }

        internal struct SlotRenderData
        {
            internal Material BranchMaterial;
            internal Matrix4x4 BranchMatrix;
            internal Material InnerLeafMaterial;
            internal Material OuterLeafMaterial;
            internal LeafTier InnerLeaves;
            internal LeafTier OuterLeaves;
            internal IReadOnlyList<LeafSlotData> LeafSlots;
            internal Vector2 WorldPos;
            internal float ScaleCompensation;
            internal float PivotOffset;
            internal float BushWorldSize;
        }

#if UNITY_EDITOR
        [SerializeField] private bool _debugLeafPivots;
        [SerializeField] private bool _debugBranchSegments;

        private void OnDrawGizmos()
        {
            if (_settings == null || _slotRenderData.Count == 0)
            {
                return;
            }

            if (_debugBranchSegments)
            {
                DrawBranchSegmentGizmos();
            }

            if (_debugLeafPivots)
            {
                var totalPivotOffset = _settings.LeafPivotOffset + 0.5f;

                foreach (var entry in _slotRenderData)
                {
                    if (entry.LeafSlots == null)
                    {
                        continue;
                    }

                    DrawTierGizmos(entry, entry.InnerLeaves, totalPivotOffset);
                    DrawTierGizmos(entry, entry.OuterLeaves, totalPivotOffset);
                }
            }
        }

        private void DrawBranchSegmentGizmos()
        {
            var variants = _settings.BushVariants;
            if (variants == null || variants.Length == 0)
            {
                return;
            }

            for (var i = 0; i < _slotRenderData.Count; i++)
            {
                var entry = _slotRenderData[i];
                var variant = variants[i % variants.Length];
                var segments = variant.DebugSegments;
                if (segments == null || segments.Count == 0)
                {
                    continue;
                }

                var size = _settings.BushWorldSize;

                foreach (var seg in segments)
                {
                    // Convert UV [0,1] → local → world
                    var startWorld = new Vector3(
                        entry.WorldPos.x + (seg.x - 0.5f) * size,
                        entry.WorldPos.y + (seg.y - 0.5f) * size,
                        0f);
                    var endWorld = new Vector3(
                        entry.WorldPos.x + (seg.z - 0.5f) * size,
                        entry.WorldPos.y + (seg.w - 0.5f) * size,
                        0f);

                    // Green line = generator centerline
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(startWorld, endWorld);

                    // Small magenta dot at segment endpoint (tip candidate)
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(endWorld, 0.004f);
                }
            }
        }

        private static void DrawTierGizmos(SlotRenderData entry, LeafTier tier, float totalPivotOffset)
        {
            if (tier.Count == 0 || tier.SlotIndices == null)
            {
                return;
            }

            var bushWorldSize = entry.BushWorldSize;

            for (var t = 0; t < tier.Count; t++)
            {
                var slot = entry.LeafSlots[tier.SlotIndices[t]];
                var attachPos = (Vector3)(entry.WorldPos
                    + (slot.UVPosition - new Vector2(0.5f, 0.5f)) * bushWorldSize);
                var angleDeg = slot.BaseAngle * Mathf.Rad2Deg - 90f;
                var rot = Quaternion.Euler(0f, 0f, angleDeg);
                var scale = slot.Scale * entry.ScaleCompensation;
                var up = rot * Vector3.up;

                // Attachment point (branch tip) — yellow dot
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(attachPos, 0.008f);

                // Branch direction from attachment — yellow line
                Gizmos.DrawLine(attachPos, attachPos + up * scale * 0.3f);

                // TRS origin — red dot
                var trsPos = attachPos + (Vector3)(rot * new Vector3(0f, -totalPivotOffset * scale, 0f));
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(trsPos, 0.005f);

                // Sprite center — cyan dot
                var spriteCenter = trsPos + (Vector3)(rot * new Vector3(0f, 0.5f * scale, 0f));
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(spriteCenter, 0.006f);

                // Line from TRS origin to sprite center — shows quad extent
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                Gizmos.DrawLine(trsPos, trsPos + (Vector3)(rot * new Vector3(0f, scale, 0f)));
            }
        }
#endif
    }
}
