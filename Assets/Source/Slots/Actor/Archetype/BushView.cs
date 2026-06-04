using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Actor.Cluster;
using UnityEngine;
using MathUtils = BalloonParty.Shared.MathUtils;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Cluster renderer for bush obstacles. Spawns baked canopy sprites and
    /// pooled leaf sprites at phyllotaxis positions per slot.
    /// </summary>
    internal class BushView : ClusterView
    {
        private const int LeafCount = 16;

        [SerializeField] private LeafSpriteView _leafPrefab;

        private readonly List<SpriteRenderer> _canopyRenderers = new();
        private readonly List<LeafSpriteView> _leafSprites = new();
        private readonly List<float> _canopyGapScales = new();

        private PoolChannel<LeafSpriteView> _leafPool;
        private IBushSettings _settings;

        internal IReadOnlyList<LeafSpriteView> LeafSprites => _leafSprites;
        internal LeafSpriteView LeafPrefab => _leafPrefab;

        internal void SetSettings(IBushSettings settings)
        {
            _settings = settings;
        }

        internal void SetLeafPool(PoolChannel<LeafSpriteView> pool)
        {
            _leafPool = pool;
        }

        protected override void OnConfigured(MaterialPropertyBlock block)
        {
            transform.localScale = Vector3.one;

            // Fully disable the base SpriteRenderer — the sprite-based view
            // uses child objects, not the legacy SDF quad.
            if (Renderer != null)
            {
                Renderer.sharedMaterial = null;
                Renderer.sprite = null;
                Renderer.enabled = false;
            }

            if (_settings == null)
            {
                return;
            }

            ConfigureSprites(SlotCentersBuffer, SlotCount, _settings);
        }

        private void LateUpdate()
        {
            if (_settings == null || SlotCount == 0)
            {
                return;
            }

            UpdateCanopyScales();
        }

        internal void ClearSprites()
        {
            foreach (var leaf in _leafSprites)
            {
                if (leaf != null && _leafPool != null)
                {
                    _leafPool.Return(leaf);
                }
            }

            _leafSprites.Clear();

            foreach (var canopy in _canopyRenderers)
            {
                if (canopy != null)
                {
                    canopy.enabled = false;
                }
            }
        }

        private void ConfigureSprites(
            IReadOnlyList<Vector4> slots, int slotCount, IBushSettings settings)
        {
            ClearSprites();
            _canopyGapScales.Clear();

            var canopyVariants = settings.CanopyVariants;
            var leafAtlas = settings.LeafAtlasSprites;
            var ruffleLeafCount = settings.RuffleLeafCount;
            var hasCanopyVariants = canopyVariants != null && canopyVariants.Length > 0;
            var hasLeafAtlas = leafAtlas != null && leafAtlas.Length > 0;

            var canopyIdx = 0;
            for (var i = 0; i < slotCount; i++)
            {
                var slot = slots[i];
                var center = new Vector2(slot.x, slot.y);
                var radiusScale = slot.w > 0.001f ? slot.w : 1f;
                var isGap = radiusScale > 0.001f && radiusScale < 0.99f;
                var hash = MathUtils.Frac(
                    Mathf.Sin(center.x * 127.1f + center.y * 311.7f) * 43758.5453f);

                if (hasCanopyVariants)
                {
                    var variantIdx = Mathf.FloorToInt(hash * canopyVariants.Length) % canopyVariants.Length;
                    SpawnCanopySprite(canopyIdx, center, canopyVariants[variantIdx], i);
                    _canopyGapScales.Add(isGap ? radiusScale : 1f);
                    canopyIdx++;
                }

                if (isGap || !hasLeafAtlas || _leafPool == null)
                {
                    continue;
                }

                for (var d = 0; d < ruffleLeafCount; d++)
                {
                    SpawnLeafSprite(hash, d, leafAtlas, i);
                }
            }

            UpdateCanopyScales();
            ApplyLeafTransforms();
        }

        private void SpawnCanopySprite(
            int index, Vector2 center, Sprite sprite, int sortBase)
        {
            SpriteRenderer sr;
            if (index < _canopyRenderers.Count)
            {
                sr = _canopyRenderers[index];
            }
            else
            {
                var go = new GameObject($"Canopy_{index}");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                _canopyRenderers.Add(sr);
            }

            sr.sprite = sprite;
            sr.enabled = true;
            sr.transform.position = new Vector3(center.x, center.y, 0f);

            if (_settings != null)
            {
                sr.sortingLayerID = _settings.SortingLayerId;
                sr.sortingOrder = _settings.SortingOrderOffset + sortBase * 10;
            }
        }

        private void SpawnLeafSprite(
            float hash, int depth, Sprite[] atlas, int sortBase)
        {
            var leaf = _leafPool.Get();
            leaf.transform.SetParent(transform, false);

            var n = LeafCount - 1 - depth;
            var angle = n * MathUtils.GoldenAngle + hash * MathUtils.TwoPi;
            var angleDeg = angle * Mathf.Rad2Deg;

            leaf.PhyllotaxisIndex = depth;
            leaf.DepthFactor = 1f - depth / Mathf.Max(1f, _settings.RuffleLeafCount - 1f);
            leaf.LeafDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            leaf.BaseRotation = angleDeg;
            leaf.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);

            var spriteIdx = Mathf.FloorToInt(
                MathUtils.Frac(hash * (depth + 1) * 23.1f) * atlas.Length) % atlas.Length;
            var sprite = atlas[spriteIdx];

            leaf.Configure(sprite, _settings.SortingLayerId,
                _settings.SortingOrderOffset + sortBase * 10 + depth + 1);

            _leafSprites.Add(leaf);
        }

        private void UpdateCanopyScales()
        {
            for (var i = 0; i < _canopyRenderers.Count; i++)
            {
                var sr = _canopyRenderers[i];
                if (sr == null || sr.sprite == null)
                {
                    continue;
                }

                var spriteWorldWidth = sr.sprite.bounds.size.x;
                var fitScale = _settings.CanopyDiameter / Mathf.Max(spriteWorldWidth, 0.01f);
                var gapScale = i < _canopyGapScales.Count ? _canopyGapScales[i] : 1f;
                sr.transform.localScale = Vector3.one * (fitScale * gapScale);
            }
        }

        private void ApplyLeafTransforms()
        {
            var slotRadius = _settings.SlotRadius;
            var branchSpread = _settings.BranchSpread;
            var leafSize = _settings.LeafSpriteSize;

            for (var i = 0; i < _leafSprites.Count; i++)
            {
                var leaf = _leafSprites[i];
                if (leaf == null)
                {
                    continue;
                }

                // Recompute position from the slot center stored on the parent
                var slotIdx = i / Mathf.Max(1, _settings.RuffleLeafCount);
                if (slotIdx >= SlotCount)
                {
                    continue;
                }

                var slot = SlotCentersBuffer[slotIdx];
                var center = new Vector2(slot.x, slot.y);
                var radiusScale = slot.w > 0.001f ? slot.w : 1f;
                var baseRadius = slotRadius * radiusScale;

                var leafPos = PhyllotaxisCenter(center, baseRadius,
                    MathUtils.Frac(Mathf.Sin(center.x * 127.1f + center.y * 311.7f) * 43758.5453f),
                    leaf.PhyllotaxisIndex, branchSpread);
                leaf.transform.position = new Vector3(leafPos.x, leafPos.y, 0f);

                // Recompute scale
                if (leaf.TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
                {
                    var spriteWorldWidth = sr.sprite.bounds.size.x;
                    var depthT = leaf.PhyllotaxisIndex / Mathf.Max(1f, _settings.RuffleLeafCount - 1f);
                    var sizeVariation = Mathf.Lerp(1f, 0.7f, depthT);
                    var fitScale = leafSize / Mathf.Max(spriteWorldWidth, 0.01f) * sizeVariation;
                    leaf.transform.localScale = Vector3.one * fitScale;
                }
            }
        }

        internal static Vector2 PhyllotaxisCenter(
            Vector2 slotCenter, float baseRadius, float hash,
            int depth, float branchSpread)
        {
            var n = LeafCount - 1 - depth;
            var fn = (float)n;

            var angle = fn * MathUtils.GoldenAngle + hash * MathUtils.TwoPi;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var maxR = Mathf.Sqrt(LeafCount - 1 + 0.5f);
            var dist = baseRadius * branchSpread * Mathf.Sqrt(fn + 0.5f) / maxR;

            return slotCenter + dir * dist;
        }
    }
}
