using System;
using System.Collections.Generic;
using System.Threading;
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
        [SerializeField] private LineRenderer _glowLineRenderer;

        private CancellationTokenSource _cts;
        private float _jumpTime;
        private Action<int> _onTargetHit;
        private float _randomness;
        private float _segmentsMultiplier;
        private List<Vector3> _targetPositions;

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
            List<Vector3> targetPositions,
            float segmentsMultiplier,
            float randomness,
            float jumpTime,
            Action<int> onTargetHit)
        {
            _targetPositions = targetPositions;
            _segmentsMultiplier = segmentsMultiplier;
            _randomness = randomness;
            _jumpTime = jumpTime;
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

            if (_glowLineRenderer != null)
            {
                _glowLineRenderer.positionCount = 0;
            }
        }

        // No allocation — buffer is pre-allocated per renderer and reused across all jumps.
        private static void FillSegment(
            Vector3 origin,
            Vector3 target,
            int segments,
            float randomness,
            Vector3[] buffer,
            int offset)
        {
            buffer[offset] = origin;

            for (var k = 1; k < segments - 1; k++)
            {
                var lerped = Vector3.Lerp(origin, target, k / (float)segments);
                buffer[offset + k] = new Vector3(
                    lerped.x + UnityEngine.Random.Range(-randomness, randomness),
                    lerped.y + UnityEngine.Random.Range(-randomness, randomness),
                    0f);
            }

            buffer[offset + segments - 1] = target;
        }

        private async UniTaskVoid PlayAsync()
        {
            var ct = _cts?.Token ?? CancellationToken.None;
            var jumpCount = _targetPositions.Count - 1;
            var rendererCount = _lineRenderers != null ? _lineRenderers.Length : 0;
            var delayMs = Mathf.RoundToInt(_jumpTime * 1000f);

            var segmentSizes = new int[jumpCount];
            for (var i = 0; i < jumpCount; i++)
            {
                var d = Vector3.Distance(_targetPositions[i], _targetPositions[i + 1]);
                segmentSizes[i] = Mathf.Max(Mathf.FloorToInt(d * _segmentsMultiplier), 2);
            }

            var cumOffsets = new int[jumpCount + 1];
            for (var i = 0; i < jumpCount; i++)
            {
                cumOffsets[i + 1] = cumOffsets[i] + segmentSizes[i];
            }

            var totalPoints = cumOffsets[jumpCount];

            // Each renderer gets independent random jitter, so each has its own buffer.
            // During animation only positionCount changes — no per-frame allocation.
            // LineRenderer.SetPositions reads min(array.Length, positionCount) items,
            // so passing the full buffer with a smaller positionCount is safe.
            var lineBuffers = new Vector3[rendererCount][];
            for (var j = 0; j < rendererCount; j++)
            {
                lineBuffers[j] = new Vector3[totalPoints];
                for (var i = 0; i < jumpCount; i++)
                {
                    FillSegment(
                        _targetPositions[i],
                        _targetPositions[i + 1],
                        segmentSizes[i],
                        _randomness,
                        lineBuffers[j],
                        cumOffsets[i]);
                }
            }

            for (var i = 0; i < jumpCount; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                var count = cumOffsets[i + 1];
                for (var j = 0; j < rendererCount; j++)
                {
                    if (_lineRenderers[j] == null)
                    {
                        continue;
                    }

                    _lineRenderers[j].positionCount = count;
                    _lineRenderers[j].SetPositions(lineBuffers[j]);
                }

                SyncGlow(lineBuffers, count, rendererCount);
                _onTargetHit?.Invoke(i);

                await UniTask.Delay(delayMs, cancellationToken: ct).SuppressCancellationThrow();

                if (ct.IsCancellationRequested)
                {
                    return;
                }
            }

            for (var i = jumpCount - 1; i >= 0; i--)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                var count = cumOffsets[i];
                for (var j = 0; j < rendererCount; j++)
                {
                    if (_lineRenderers[j] == null)
                    {
                        continue;
                    }

                    _lineRenderers[j].positionCount = count;
                    if (count > 0)
                    {
                        _lineRenderers[j].SetPositions(lineBuffers[j]);
                    }
                }

                SyncGlow(lineBuffers, count, rendererCount);

                await UniTask.Delay(delayMs, cancellationToken: ct).SuppressCancellationThrow();

                if (ct.IsCancellationRequested)
                {
                    return;
                }
            }

            InvokeComplete();
        }

        private void SyncGlow(Vector3[][] lineBuffers, int count, int rendererCount)
        {
            if (_glowLineRenderer == null || rendererCount == 0)
            {
                return;
            }

            _glowLineRenderer.positionCount = count;
            if (count > 0)
            {
                _glowLineRenderer.SetPositions(lineBuffers[0]);
            }
        }
    }
}
