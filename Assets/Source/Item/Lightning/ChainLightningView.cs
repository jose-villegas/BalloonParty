using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Shared.Animation;
using BalloonParty.Shared.Pool;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Item.Lightning
{
    /// <summary>
    ///     Poolable chain-lightning effect. Extends <see cref="EffectView" /> so it
    ///     participates in the standard effect-pool pipeline via <see cref="EffectPoolChannel" />.
    ///     Visual parameters are serialized on the prefab. Call
    ///     <see cref="PrepareDisplay" /> with target data before <see cref="Play" />.
    /// </summary>
    public class ChainLightningView : EffectView
    {
        [SerializeField] private LineRenderer[] _lineRenderers;
        [SerializeField] private SpriteRenderer _glowRenderer;

        private CancellationTokenSource _cts;
        private float _fractalDecay;
        private int _glowSubdivisions;
        private float _jumpTime;
        private Action<int> _onTargetHit;
        private float _randomness;
        private float _segmentsMultiplier;
        private IReadOnlyList<Vector3> _targetPositions;

        public override void OnSpawned()
        {
            base.OnSpawned();
            _cts = new CancellationTokenSource();
            ClearRenderers();
        }

        public override void OnDespawned()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            ClearRenderers();
            base.OnDespawned();
        }

        /// <summary>
        ///     Starts the chain-lightning animation fire-and-forget.
        ///     Call <see cref="PrepareDisplay" /> first. <paramref name="position" /> and
        ///     <paramref name="tint" /> are unused — positions are set via
        ///     <see cref="PrepareDisplay" /> and the line material is not tinted.
        ///     <paramref name="onComplete" /> is invoked after the full retraction.
        /// </summary>
        public override void Play(Vector3 position, Color tint, Action onComplete = null)
        {
            OnComplete = onComplete;

            if (_targetPositions == null || _targetPositions.Count < 2)
            {
                InvokeComplete();
                return;
            }

            PlayAsync().Forget();
        }

        /// <summary>
        ///     Sets the target chain before calling <see cref="Play" />.
        ///     Index 0 must be the item-balloon world position; subsequent entries are
        ///     same-color balloon positions sorted nearest-first.
        ///     <paramref name="onTargetHit" /> is invoked per jump (index 0 = first target).
        /// </summary>
        public void PrepareDisplay(
            IReadOnlyList<Vector3> targetPositions,
            ItemSettings settings,
            Action<int> onTargetHit)
        {
            _targetPositions = targetPositions;
            _segmentsMultiplier = settings.LightningSegmentsMultiplier;
            _randomness = settings.LightningRandomness;
            _jumpTime = settings.LightningJumpTime;
            _glowSubdivisions = settings.LightningGlowSubdivisions;
            _fractalDecay = settings.LightningFractalDecay;
            _onTargetHit = onTargetHit;
        }

        private void ClearRenderers()
        {
            if (_lineRenderers != null)
            {
                foreach (var lr in _lineRenderers)
                {
                    if (lr != null)
                    {
                        lr.positionCount = 0;
                    }
                }
            }

            if (_glowRenderer != null)
            {
                _glowRenderer.enabled = false;
                _glowRenderer.transform.localScale = Vector3.zero;
            }
        }

        internal static void FillSegment(
            Vector3 start,
            Vector3 end,
            int segments,
            float displacement,
            float fractalDecay,
            Vector3[] buffer,
            int offset)
        {
            PathHelper.MidpointDisplacement(start, end, displacement, fractalDecay, buffer, offset, segments);
        }

        /// <summary>
        ///     Pre-computes jagged bolt segments for all jumps and renderers.
        ///     Returns per-renderer position buffers and cumulative offset array.
        /// </summary>
        internal static (Vector3[][] lineBuffers, int[] cumOffsets) BuildBoltBuffers(
            IReadOnlyList<Vector3> positions,
            int rendererCount,
            float segmentsMultiplier,
            float randomness,
            float fractalDecay)
        {
            var jumpCount = positions.Count - 1;

            var segmentSizes = new int[jumpCount];
            for (var i = 0; i < jumpCount; i++)
            {
                var d = Vector3.Distance(positions[i], positions[i + 1]);
                segmentSizes[i] = Mathf.Max(Mathf.FloorToInt(d * segmentsMultiplier), 2);
            }

            var cumOffsets = PathHelper.PrefixSum(segmentSizes);
            var totalPoints = cumOffsets[jumpCount];

            var lineBuffers = new Vector3[rendererCount][];
            for (var j = 0; j < rendererCount; j++)
            {
                lineBuffers[j] = new Vector3[totalPoints];
                for (var i = 0; i < jumpCount; i++)
                {
                    FillSegment(
                        positions[i],
                        positions[i + 1],
                        segmentSizes[i],
                        randomness,
                        fractalDecay,
                        lineBuffers[j],
                        cumOffsets[i]);
                }
            }

            return (lineBuffers, cumOffsets);
        }

        /// <summary>
        ///     Builds a smooth Catmull-Rom path through the per-jump centroids so the
        ///     glow sprite can slide instead of snapping between discrete positions.
        ///     Also returns interpolated diameters that match each path sample.
        /// </summary>
        internal static (Vector3[] positions, float[] diameters) BuildGlowPath(
            IReadOnlyList<Vector3> targetPositions,
            int subdivisions)
        {
            var (centroids, rawDiameters) = ComputeStageCentroids(targetPositions);

            if (centroids.Count <= 1)
            {
                return (centroids.ToArray(), rawDiameters);
            }

            var smoothPositions = PathHelper.CatmullRomPath(centroids, centroids.Count, subdivisions);
            var smoothDiameters = PathHelper.ResampleLinear(rawDiameters, smoothPositions.Length);

            return (smoothPositions, smoothDiameters);
        }

        /// <summary>
        ///     Computes the centroid and bounding diameter for each visible glow stage.
        ///     Stage <c>s</c> (1-indexed) covers <c>targetPositions[0..s]</c>.
        /// </summary>
        private static (List<Vector3> centroids, float[] diameters) ComputeStageCentroids(
            IReadOnlyList<Vector3> targetPositions)
        {
            var stageCount = targetPositions.Count - 1;
            var centroids = new List<Vector3>(stageCount);
            var diameters = new float[stageCount];

            for (var stage = 1; stage <= stageCount; stage++)
            {
                var count = stage + 1;
                var centroid = VectorMathHelper.Centroid(targetPositions, count);
                centroids.Add(centroid);

                var radius = VectorMathHelper.BoundingRadius(targetPositions, count, centroid);
                diameters[stage - 1] = (radius + 1f) * 2f;
            }

            return (centroids, diameters);
        }

        private void SetGlowFromPath(Vector3[] path, float[] diameters, float pathIndex)
        {
            if (_glowRenderer == null || path.Length == 0)
            {
                return;
            }

            var pos = PathHelper.SampleAt(path, pathIndex);
            var dia = PathHelper.SampleAt(diameters, pathIndex);

            _glowRenderer.enabled = true;
            _glowRenderer.transform.position = pos;
            _glowRenderer.transform.localScale = new Vector3(dia, dia, 1f);
        }

        private async UniTaskVoid PlayAsync()
        {
            var ct = _cts?.Token ?? CancellationToken.None;
            var jumpCount = _targetPositions.Count - 1;
            var rendererCount = _lineRenderers != null ? _lineRenderers.Length : 0;

            var (lineBuffers, cumOffsets) = BuildBoltBuffers(
                _targetPositions,
                rendererCount,
                _segmentsMultiplier,
                _randomness,
                _fractalDecay);

            var (glowPath, glowDia) = BuildGlowPath(_targetPositions, _glowSubdivisions);
            var hasGlow = _glowRenderer != null && glowPath.Length > 0;
            var maxPathIdx = (float)(glowPath.Length - 1);

            float GlowIdx(int stage)
            {
                return Mathf.Min(stage * _glowSubdivisions, maxPathIdx);
            }

            // Forward: reveal jumps 0 → jumpCount-1
            for (var i = 0; i < jumpCount; i++)
            {
                if (await StepJump(i + 1, i > 0 ? i - 1 : -1, i, true))
                {
                    return;
                }
            }

            // Retraction: remove jumps jumpCount-1 → 0
            for (var i = jumpCount - 1; i >= 0; i--)
            {
                if (await StepJump(i, i >= 1 ? i : -1, i >= 1 ? i - 1 : -1, false))
                {
                    return;
                }
            }

            InvokeComplete();
            return;

            async UniTask<bool> StepJump(int lineStage, int glowFrom, int glowTo, bool forward)
            {
                if (ct.IsCancellationRequested)
                {
                    return true;
                }

                ApplyLineRenderers(lineBuffers, cumOffsets[lineStage], rendererCount);

                if (forward)
                {
                    _onTargetHit?.Invoke(lineStage - 1);
                }

                if (hasGlow && glowFrom >= 0 && glowTo >= 0)
                {
                    return await AnimateGlowSegment(
                        glowPath,
                        glowDia,
                        GlowIdx(glowFrom),
                        GlowIdx(glowTo),
                        ct);
                }

                if (hasGlow && forward)
                {
                    SetGlowFromPath(glowPath, glowDia, 0f);
                }
                else if (hasGlow)
                {
                    DisableGlow();
                }

                return await WaitJump(ct);
            }
        }

        private void ApplyLineRenderers(Vector3[][] lineBuffers, int pointCount, int rendererCount)
        {
            for (var j = 0; j < rendererCount; j++)
            {
                if (_lineRenderers[j] == null)
                {
                    continue;
                }

                _lineRenderers[j].positionCount = pointCount;
                if (pointCount > 0)
                {
                    _lineRenderers[j].SetPositions(lineBuffers[j]);
                }
            }
        }

        private void DisableGlow()
        {
            if (_glowRenderer == null)
            {
                return;
            }

            _glowRenderer.enabled = false;
            _glowRenderer.transform.localScale = Vector3.zero;
        }

        /// <summary>
        ///     Slides the glow sprite from one path index to another over <see cref="_jumpTime" />.
        ///     Returns <c>true</c> when cancelled.
        /// </summary>
        private async UniTask<bool> AnimateGlowSegment(
            Vector3[] path,
            float[] diameters,
            float fromIdx,
            float toIdx,
            CancellationToken ct)
        {
            var elapsed = 0f;
            while (elapsed < _jumpTime)
            {
                if (ct.IsCancellationRequested)
                {
                    return true;
                }

                var t = Mathf.Clamp01(elapsed / _jumpTime);
                SetGlowFromPath(path, diameters, Mathf.Lerp(fromIdx, toIdx, t));
                elapsed += Time.deltaTime;

                await UniTask.Yield(ct).SuppressCancellationThrow();

                if (ct.IsCancellationRequested)
                {
                    return true;
                }
            }

            SetGlowFromPath(path, diameters, toIdx);
            return false;
        }

        /// <summary>
        ///     Waits one jump duration. Returns <c>true</c> when cancelled.
        /// </summary>
        private async UniTask<bool> WaitJump(CancellationToken ct)
        {
            var delayMs = Mathf.RoundToInt(_jumpTime * 1000f);

            await UniTask.Delay(delayMs, cancellationToken: ct).SuppressCancellationThrow();

            return ct.IsCancellationRequested;
        }
    }
}
