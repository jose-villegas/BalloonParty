using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Shared;
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
        private List<Vector3> _targetPositions;
        private float _segmentsMultiplier;
        private float _randomness;
        private float _jumpTime;
        private Action<int> _onTargetHit;

        // ── EffectView ────────────────────────────────────────────────────────────

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

        // ── Public setup ──────────────────────────────────────────────────────────

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

        // ── Animation ─────────────────────────────────────────────────────────────

        private async UniTaskVoid PlayAsync()
        {
            var ct = _cts?.Token ?? CancellationToken.None;

            var jumpCount = _targetPositions.Count - 1;
            var rendererCount = _lineRenderers != null ? _lineRenderers.Length : 0;

            // ── Build segments ───────────────────────────────────────────────────
            var segmentQueues = new Queue<Vector3[]>[rendererCount];
            var segmentStacks = new Stack<Vector3[]>[rendererCount];

            for (var j = 0; j < rendererCount; j++)
            {
                segmentQueues[j] = new Queue<Vector3[]>();
                segmentStacks[j] = new Stack<Vector3[]>();
            }

            for (var i = 0; i < jumpCount; i++)
            {
                var origin = _targetPositions[i];
                var target = _targetPositions[i + 1];
                var segments = Mathf.Max(
                    Mathf.FloorToInt(Vector3.Distance(origin, target) * _segmentsMultiplier), 2);

                for (var j = 0; j < rendererCount; j++)
                {
                    var points = BuildSegment(origin, target, segments, _randomness);
                    segmentQueues[j].Enqueue(points);
                    segmentStacks[j].Push(points);
                }
            }

            var linePositions = new List<Vector3>[rendererCount];
            for (var j = 0; j < rendererCount; j++)
            {
                linePositions[j] = new List<Vector3>();
            }

            var delayMs = Mathf.RoundToInt(_jumpTime * 1000f);

            // ── Forward pass ─────────────────────────────────────────────────────
            for (var i = 0; i < jumpCount; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                for (var j = 0; j < rendererCount; j++)
                {
                    if (segmentQueues[j].Count > 0)
                    {
                        linePositions[j].AddRange(segmentQueues[j].Dequeue());
                        _lineRenderers[j].positionCount = linePositions[j].Count;
                        _lineRenderers[j].SetPositions(linePositions[j].ToArray());
                    }
                }

                SyncGlow(linePositions, rendererCount);
                _onTargetHit?.Invoke(i);

                await UniTask.Delay(delayMs, cancellationToken: ct).SuppressCancellationThrow();

                if (ct.IsCancellationRequested)
                {
                    return;
                }
            }

            // ── Reverse pass (retraction) ────────────────────────────────────────
            for (var i = 0; i < jumpCount; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                for (var j = 0; j < rendererCount; j++)
                {
                    if (segmentStacks[j].Count > 0)
                    {
                        var popped = segmentStacks[j].Pop();
                        var newCount = linePositions[j].Count - popped.Length;
                        if (newCount >= 0)
                        {
                            linePositions[j].RemoveRange(newCount, popped.Length);
                        }

                        _lineRenderers[j].positionCount = linePositions[j].Count;
                        _lineRenderers[j].SetPositions(linePositions[j].ToArray());
                    }
                }

                SyncGlow(linePositions, rendererCount);

                await UniTask.Delay(delayMs, cancellationToken: ct).SuppressCancellationThrow();

                if (ct.IsCancellationRequested)
                {
                    return;
                }
            }

            InvokeComplete();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void SyncGlow(List<Vector3>[] linePositions, int rendererCount)
        {
            if (_glowLineRenderer == null || rendererCount == 0)
            {
                return;
            }

            _glowLineRenderer.positionCount = linePositions[0].Count;
            _glowLineRenderer.SetPositions(linePositions[0].ToArray());
        }

        private static Vector3[] BuildSegment(
            Vector3 origin, Vector3 target, int segments, float randomness)
        {
            var points = new Vector3[segments];
            points[0] = origin;

            for (var k = 1; k < segments - 1; k++)
            {
                var lerped = Vector3.Lerp(origin, target, k / (float)segments);
                points[k] = new Vector3(
                    lerped.x + UnityEngine.Random.Range(-randomness, randomness),
                    lerped.y + UnityEngine.Random.Range(-randomness, randomness),
                    0f);
            }

            points[segments - 1] = target;
            return points;
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
    }
}

