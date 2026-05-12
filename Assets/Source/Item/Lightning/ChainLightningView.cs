using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Shared;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Item.Lightning
{
    /// <summary>
    ///     Drives the chain-lightning line-renderer animation. Poolable — cleared and
    ///     reused across activations. Attach to the ChainLightning prefab.
    /// </summary>
    public class ChainLightningView : MonoBehaviour, IPoolable
    {
        [SerializeField] private LineRenderer[] _lineRenderers;
        [SerializeField] private LineRenderer _glowLineRenderer;

        private CancellationTokenSource _cts;

        // ── IPoolable ────────────────────────────────────────────────────────────

        public void OnSpawned()
        {
            _cts = new CancellationTokenSource();
            ClearRenderers();
        }

        public void OnDespawned()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            ClearRenderers();
        }

        // ── Animation ────────────────────────────────────────────────────────────

        /// <summary>
        ///     Plays the forward-then-reverse lightning animation and returns when finished
        ///     (or when cancelled). <paramref name="targetPositions"/> must include the
        ///     item-balloon world position at index 0; target balloon positions follow.
        ///     <paramref name="onTargetHit"/> is invoked once per jump (index 0 = first
        ///     same-color balloon, 1 = second, …).
        /// </summary>
        public async UniTask Display(
            List<Vector3> targetPositions,
            float segmentsMultiplier,
            float randomness,
            float jumpTime,
            Action<int> onTargetHit,
            CancellationToken externalToken)
        {
            if (targetPositions == null || targetPositions.Count < 2)
            {
                return;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cts?.Token ?? CancellationToken.None,
                externalToken);
            var ct = linkedCts.Token;

            var jumpCount = targetPositions.Count - 1; // one jump per consecutive pair

            // ── Build segments ───────────────────────────────────────────────────
            var rendererCount = _lineRenderers != null ? _lineRenderers.Length : 0;
            var segmentQueues = new Queue<Vector3[]>[rendererCount];
            var segmentStacks = new Stack<Vector3[]>[rendererCount];

            for (var j = 0; j < rendererCount; j++)
            {
                segmentQueues[j] = new Queue<Vector3[]>();
                segmentStacks[j] = new Stack<Vector3[]>();
            }

            for (var i = 0; i < jumpCount; i++)
            {
                var origin = targetPositions[i];
                var target = targetPositions[i + 1];
                var segments = Mathf.Max(
                    Mathf.FloorToInt(Vector3.Distance(origin, target) * segmentsMultiplier), 2);

                for (var j = 0; j < rendererCount; j++)
                {
                    var points = BuildSegment(origin, target, segments, randomness);
                    segmentQueues[j].Enqueue(points);
                    segmentStacks[j].Push(points);
                }
            }

            // Running position lists (appended on forward, trimmed on reverse)
            var linePositions = new List<Vector3>[rendererCount];
            for (var j = 0; j < rendererCount; j++)
            {
                linePositions[j] = new List<Vector3>();
            }

            var delayMs = Mathf.RoundToInt(jumpTime * 1000f);

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

                onTargetHit?.Invoke(i);

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

