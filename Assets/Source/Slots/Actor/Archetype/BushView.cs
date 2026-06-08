using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Slots.Actor.Cluster;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Cluster renderer for bush obstacles. Renders a branch quad and instanced
    /// leaf quads per slot via <c>Graphics.DrawMesh</c> / <c>DrawMeshInstanced</c>.
    /// Each slot picks its own <see cref="BushVariantData"/> by position hash.
    /// </summary>
    internal class BushView : ClusterView
    {
        private static readonly int LeafTintId = Shader.PropertyToID("_LeafTint");
        private static readonly int UVRectId = Shader.PropertyToID("_UVRect");
        private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int ShadowOffsetId = Shader.PropertyToID("_ShadowOffset");
        private static readonly int ShadowSoftnessId = Shader.PropertyToID("_ShadowSoftness");
        private static readonly int SpriteScaleId = Shader.PropertyToID("_SpriteScale");

        private readonly List<SlotRenderData> _slotRenderData = new();

        private static Mesh _sharedLeafQuad;
        private static Mesh _sharedBranchQuad;
        private IBushSettings _settings;

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

            foreach (var slot in _slotRenderData)
            {
                if (slot.BranchMaterial != null)
                {
                    Graphics.DrawMesh(
                        branchMesh,
                        slot.BranchMatrix,
                        slot.BranchMaterial,
                        gameObject.layer);
                }

                if (slot.LeafCount > 0 && slot.LeafMaterial != null)
                {
                    Graphics.DrawMeshInstanced(
                        leafMesh,
                        0,
                        slot.LeafMaterial,
                        slot.LeafMatrices,
                        slot.LeafCount,
                        slot.LeafProps);
                }
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
            if (_settings.LeafShader == null || sprites == null || sprites.Length == 0)
            {
                return;
            }

            entry.LeafMaterial = new Material(_settings.LeafShader)
            {
                mainTexture = sprites[0].texture,
                enableInstancing = true,
                renderQueue = 3001
            };
            entry.LeafMaterial.SetColor(ShadowColorId, _settings.LeafShadowColor);
            entry.LeafMaterial.SetVector(ShadowOffsetId, _settings.LeafShadowOffset);
            entry.LeafMaterial.SetFloat(ShadowSoftnessId, _settings.LeafShadowSoftness);
            entry.LeafMaterial.SetFloat(SpriteScaleId, _settings.LeafSpriteScale);

            var slots = variant.LeafSlots;
            entry.LeafCount = slots.Count;
            entry.LeafMatrices = new Matrix4x4[entry.LeafCount];

            var tints = new Vector4[entry.LeafCount];
            var uvRects = new Vector4[entry.LeafCount];

            var scaleCompensation = 1f / Mathf.Max(_settings.LeafSpriteScale, 0.3f);

            for (var i = 0; i < entry.LeafCount; i++)
            {
                var slot = slots[i];
                var leafWorldPos = worldPos + slot.Position;
                var angleDeg = slot.BaseAngle * Mathf.Rad2Deg - 90f;
                entry.LeafMatrices[i] = Matrix4x4.TRS(
                    new Vector3(leafWorldPos.x, leafWorldPos.y, 0f),
                    Quaternion.Euler(0f, 0f, angleDeg),
                    Vector3.one * (slot.Scale * scaleCompensation));

                var tint = (Color)slot.Tint;
                tints[i] = new Vector4(tint.r, tint.g, tint.b, tint.a);

                var spriteIndex = Mathf.Clamp(slot.SpriteVariant, 0, sprites.Length - 1);
                var rect = sprites[spriteIndex].textureRect;
                var tex = sprites[spriteIndex].texture;
                uvRects[i] = new Vector4(
                    rect.x / tex.width,
                    rect.y / tex.height,
                    rect.width / tex.width,
                    rect.height / tex.height);
            }

            entry.LeafProps = new MaterialPropertyBlock();
            entry.LeafProps.SetVectorArray(LeafTintId, tints);
            entry.LeafProps.SetVectorArray(UVRectId, uvRects);
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

        private struct SlotRenderData
        {
            internal Material BranchMaterial;
            internal Matrix4x4 BranchMatrix;
            internal Material LeafMaterial;
            internal MaterialPropertyBlock LeafProps;
            internal Matrix4x4[] LeafMatrices;
            internal int LeafCount;
        }
    }
}
