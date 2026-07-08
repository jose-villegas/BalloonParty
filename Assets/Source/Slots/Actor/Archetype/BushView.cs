using BalloonParty.Configuration;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Rendering;
using BalloonParty.Slots.Actor.Cluster;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>Cluster renderer for bush obstacles; falls back to per-leaf <c>DrawMesh</c> when GPU instancing is unavailable.</summary>
    internal class BushView : ClusterView
    {
#if UNITY_EDITOR
        [SerializeField] private bool _debugLeafPivots;
        [SerializeField] private bool _debugBranchSegments;
#endif

        [Inject] private ProjectilePositionProvider _projectileProvider;
        [Inject] private ImpactEventBus _impactBus;
        [Inject] private PoolManager _poolManager;

        private static bool? _supportsInstancing;
        private static Mesh _sharedLeafQuad;
        private static Mesh _sharedBranchQuad;
        private IBushSettings _settings;
        private BushMaterialSet _materials;
        private BushRenderDataBuilder _builder;
        private BushRustleController _rustle;
        private BushRenderData _renderData;
        private MaterialPropertyBlock _fallbackMpb;

        // Baked WORLD matrices ignore this transform, so the Ascent slide is applied as a manual offset.
        private Vector3 _restPosition;
        private Matrix4x4[] _offsetMatrices;
#if UNITY_EDITOR
        private int _lastRevision;
#endif

        private static bool SupportsInstancing
        {
            get
            {
                _supportsInstancing ??= SystemInfo.supportsInstancing;
                return _supportsInstancing.Value;
            }
        }

        private void LateUpdate()
        {
            if (_renderData == null)
            {
                return;
            }

#if UNITY_EDITOR
            // Live tuning: any inspector edit bumps the SO's revision — rebuild so every field (colours,
            // AO, wind, and structural ones like atlas/gradient/world size) previews. Runs in play mode
            // too (that's where the DI controller configures bushes); still editor-only via UNITY_EDITOR.
            if (_settings != null && _settings.LiveTuning && _settings.Revision != _lastRevision)
            {
                _lastRevision = _settings.Revision;
                RebuildRenderResources();
            }
#endif

            var leafMesh = GetLeafQuadMesh();
            var branchMesh = GetBranchQuadMesh();
            var layer = gameObject.layer;

            var offset = transform.position - _restPosition;
            var displaced = offset.sqrMagnitude > 1e-8f;
            var offsetMatrix = displaced ? Matrix4x4.Translate(offset) : Matrix4x4.identity;

            // Render queue (inner 2999 < branch 3000 < outer 3001) drives layering.
            DrawLeafBatches(leafMesh, _renderData.InnerBatches, _materials.InnerLeaf, layer, displaced, offsetMatrix);

            // Indexing avoids the per-frame heap enumerator of IReadOnlyList's foreach.
            var slots = _renderData.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.BranchMaterial != null)
                {
                    var matrix = displaced ? offsetMatrix * slot.BranchMatrix : slot.BranchMatrix;
                    Graphics.DrawMesh(branchMesh, matrix, slot.BranchMaterial, layer);
                }
            }

            DrawLeafBatches(leafMesh, _renderData.OuterBatches, _materials.OuterLeaf, layer, displaced, offsetMatrix);

            _rustle.Tick();
        }

        private void OnDestroy()
        {
            // Edit-mode rebuilds release via OnConfigured instead; DestroyImmediate is illegal here.
            if (Application.isPlaying)
            {
                _materials?.Release();
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

            if (_settings == null)
            {
                return;
            }

            RebuildRenderResources();

            // Statics spawn before the Ascent lifts the scenario root, so this is the settled position.
            _restPosition = transform.position;
#if UNITY_EDITOR
            _lastRevision = _settings.Revision;
#endif
        }

        protected override void OnCleared()
        {
            // LateUpdate draws off _renderData, not the base Renderer, so it must be dropped to stop drawing.
            _renderData = null;
        }

        // Rebuilds materials + render data from the current settings. Reused by the live-tuning rebuild.
        private void RebuildRenderResources()
        {
            EnsureComponents();
            _materials.Release();
            _materials.BuildLeafMaterials(_settings.LeafAtlasSprites);
            _renderData = _builder.Build(SlotCentersBuffer, SlotCount);
            _rustle.SetSlots(CollectSlotPositions());
        }

        private void EnsureComponents()
        {
            _materials ??= new BushMaterialSet(_settings);
            _builder ??= new BushRenderDataBuilder(_settings, _materials);
            _rustle ??= new BushRustleController(_projectileProvider, _impactBus, _poolManager, _settings);
        }

        private Vector2[] CollectSlotPositions()
        {
            var positions = new Vector2[SlotCount];
            for (var i = 0; i < SlotCount; i++)
            {
                var center = SlotCentersBuffer[i];
                positions[i] = new Vector2(center.x, center.y);
            }

            return positions;
        }

        private void DrawLeafBatches(
            Mesh leafMesh, LeafBatch[] batches, Material material, int layer, bool displaced, Matrix4x4 offsetMatrix)
        {
            if (material == null)
            {
                return;
            }

            for (var b = 0; b < batches.Length; b++)
            {
                var batch = batches[b];
                if (batch.Count <= 0)
                {
                    continue;
                }

                // Offset only when displaced, into a reused scratch array.
                var matrices = batch.Matrices;
                if (displaced)
                {
                    matrices = OffsetMatrices(batch.Matrices, batch.Count, offsetMatrix);
                }

                if (SupportsInstancing)
                {
                    // Must pass layer explicitly, or layer-masked cameras render branches without foliage.
                    Graphics.DrawMeshInstanced(leafMesh, 0, material, matrices, batch.Count, batch.Props,
                        UnityEngine.Rendering.ShadowCastingMode.Off, false, layer);
                    continue;
                }

                _fallbackMpb ??= new MaterialPropertyBlock();

                for (var i = 0; i < batch.Count; i++)
                {
                    _fallbackMpb.SetVector(BushShaderProperties.LeafTint, batch.Tints[i]);
                    _fallbackMpb.SetVector(BushShaderProperties.UVRect, batch.UVRects[i]);
                    _fallbackMpb.SetVector(BushShaderProperties.LeafWind, batch.Winds[i]);
                    Graphics.DrawMesh(leafMesh, matrices[i], material, layer, null, 0, _fallbackMpb);
                }
            }
        }

        private Matrix4x4[] OffsetMatrices(Matrix4x4[] source, int count, Matrix4x4 offsetMatrix)
        {
            if (_offsetMatrices == null || _offsetMatrices.Length < count)
            {
                _offsetMatrices = new Matrix4x4[count];
            }

            for (var i = 0; i < count; i++)
            {
                _offsetMatrices[i] = offsetMatrix * source[i];
            }

            return _offsetMatrices;
        }

        private static Mesh GetBranchQuadMesh()
        {
            _sharedBranchQuad ??= MeshHelper.CreateQuad(QuadPivot.Center);
            return _sharedBranchQuad;
        }

        private static Mesh GetLeafQuadMesh()
        {
            _sharedLeafQuad ??= MeshHelper.CreateQuad(QuadPivot.Bottom);
            return _sharedLeafQuad;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_settings == null || _renderData == null || _renderData.Slots.Count == 0)
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

                foreach (var entry in _renderData.Slots)
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

            for (var i = 0; i < _renderData.Slots.Count; i++)
            {
                var entry = _renderData.Slots[i];
                var variant = variants[i % variants.Length];
                var segments = variant.DebugSegments;
                if (segments == null || segments.Count == 0)
                {
                    continue;
                }

                var size = _settings.BushWorldSize;

                foreach (var seg in segments)
                {
                    var startWorld = new Vector3(
                        entry.WorldPos.x + (seg.x - 0.5f) * size,
                        entry.WorldPos.y + (seg.y - 0.5f) * size,
                        0f);
                    var endWorld = new Vector3(
                        entry.WorldPos.x + (seg.z - 0.5f) * size,
                        entry.WorldPos.y + (seg.w - 0.5f) * size,
                        0f);

                    // Green: generator centerline.
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(startWorld, endWorld);

                    // Magenta: segment endpoint (tip candidate).
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

                // Yellow: attachment point (branch tip).
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(attachPos, 0.008f);

                Gizmos.DrawLine(attachPos, attachPos + up * scale * 0.3f);

                // Red: TRS origin.
                var trsPos = attachPos + (Vector3)(rot * new Vector3(0f, -totalPivotOffset * scale, 0f));
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(trsPos, 0.005f);

                // Cyan: sprite center.
                var spriteCenter = trsPos + (Vector3)(rot * new Vector3(0f, 0.5f * scale, 0f));
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(spriteCenter, 0.006f);

                // Line shows quad extent, TRS origin to sprite center.
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                Gizmos.DrawLine(trsPos, trsPos + (Vector3)(rot * new Vector3(0f, scale, 0f)));
            }
        }
#endif
    }
}
