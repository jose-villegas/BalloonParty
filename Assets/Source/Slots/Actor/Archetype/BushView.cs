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
        private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int ShadowOffsetId = Shader.PropertyToID("_ShadowOffset");
        private static readonly int ShadowSoftnessId = Shader.PropertyToID("_ShadowSoftness");
        private static readonly int SpriteScaleId = Shader.PropertyToID("_SpriteScale");

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

            for (var i = 0; i < SlotCount; i++)
            {
                var center = SlotCentersBuffer[i];
                var worldPos = new Vector2(center.x, center.y);
                var variant = variants[i % variants.Length];

                var entry = new SlotRenderData();
                ConfigureBranch(ref entry, worldPos, variant);
                ConfigureLeaves(ref entry, worldPos, variant);
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
            ref SlotRenderData entry, Vector2 worldPos, BushVariantData variant)
        {
            var sprites = _settings.LeafAtlasSprites;
            if (_settings.LeafMaterial == null || sprites == null || sprites.Length == 0)
            {
                return;
            }

            var slots = variant.LeafSlots;
            entry.LeafSlots = slots;
            entry.WorldPos = worldPos;
            entry.ScaleCompensation = 1f / Mathf.Max(_settings.LeafSpriteScale, 0.3f);
            entry.PivotOffset = _settings.LeafPivotOffset;

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
                innerIndices, slots, sprites, worldPos, entry.ScaleCompensation, entry.PivotOffset);
            entry.OuterLeaves = BuildLeafTier(
                outerIndices, slots, sprites, worldPos, entry.ScaleCompensation, entry.PivotOffset);
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
            return mat;
        }

        private static LeafTier BuildLeafTier(
            List<int> indices,
            IReadOnlyList<LeafSlotData> slots,
            Sprite[] sprites,
            Vector2 worldPos,
            float scaleCompensation,
            float pivotOffset)
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

            // The sprite center (Gielis shape center) sits at UV (0.5, 0.5), which maps
            // to local y = 0.5 on the bottom-pivoted quad. The fixed 0.5 offset aligns the
            // sprite center with the attachment point. PivotOffset fine-tunes from there:
            //   0   → sprite center at attachment (default)
            //  >0   → pivot moves toward leaf tip, leaf tucks onto branch
            //  <0   → pivot moves toward petiole, leaf extends outward
            var tints = new Vector4[indices.Count];
            var uvRects = new Vector4[indices.Count];

            for (var t = 0; t < indices.Count; t++)
            {
                var slot = slots[indices[t]];
                var leafWorldPos = worldPos + slot.Position;
                var angleDeg = slot.BaseAngle * Mathf.Rad2Deg - 90f;
                var scale = slot.Scale * scaleCompensation;
                var rot = Quaternion.Euler(0f, 0f, angleDeg);
                var pivotShift = rot * new Vector3(0f, -(pivotOffset + 0.5f) * scale, 0f);
                tier.Matrices[t] = Matrix4x4.TRS(
                    new Vector3(leafWorldPos.x, leafWorldPos.y, 0f) + pivotShift,
                    rot,
                    Vector3.one * scale);

                var tint = (Color)slot.Tint;
                tints[t] = new Vector4(tint.r, tint.g, tint.b, tint.a);

                var spriteIndex = Mathf.Clamp(slot.SpriteVariant, 0, sprites.Length - 1);
                var rect = sprites[spriteIndex].textureRect;
                var tex = sprites[spriteIndex].texture;
                uvRects[t] = new Vector4(
                    rect.x / tex.width,
                    rect.y / tex.height,
                    rect.width / tex.width,
                    rect.height / tex.height);
            }

            tier.Tints = tints;
            tier.UVRects = uvRects;

            tier.Props = new MaterialPropertyBlock();
            tier.Props.SetVectorArray(LeafTintId, tints);
            tier.Props.SetVectorArray(UVRectId, uvRects);
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
        }

#if UNITY_EDITOR
        [SerializeField] private bool _debugLeafPivots;

        private void OnDrawGizmos()
        {
            if (!_debugLeafPivots || _settings == null || _slotRenderData.Count == 0)
            {
                return;
            }

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

        private static void DrawTierGizmos(SlotRenderData entry, LeafTier tier, float totalPivotOffset)
        {
            if (tier.Count == 0 || tier.SlotIndices == null)
            {
                return;
            }

            for (var t = 0; t < tier.Count; t++)
            {
                var slot = entry.LeafSlots[tier.SlotIndices[t]];
                var attachPos = (Vector3)(entry.WorldPos + slot.Position);
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
