using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Shared.Animation;
using BalloonParty.Shared.Pool;
using Cysharp.Threading.Tasks;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item.Lightning
{
    /// <summary>
    ///     Poolable chain-lightning effect. Extends <see cref="EffectView" /> so it
    ///     participates in the standard effect-pool pipeline via <see cref="SimplePoolChannel{TItem}" />.
    ///     Visual parameters are serialized on the prefab. Call
    ///     <see cref="PrepareDisplay" /> with target data before <see cref="Play" />.
    /// </summary>
    public class ChainLightningView : EffectView, IChainEffect
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
            _segmentsMultiplier = settings.Lightning.SegmentsMultiplier;
            _randomness = settings.Lightning.Randomness;
            _jumpTime = settings.Lightning.JumpTime;
            _glowSubdivisions = settings.Lightning.GlowSubdivisions;
            _fractalDecay = settings.Lightning.FractalDecay;
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

            var (lineBuffers, cumOffsets) = ChainLightningGeometry.BuildBoltBuffers(
                _targetPositions,
                rendererCount,
                _segmentsMultiplier,
                _randomness,
                _fractalDecay);

            var (glowPath, glowDia) = ChainLightningGeometry.BuildGlowPath(_targetPositions, _glowSubdivisions);

            var playback = new BoltPlayback
            {
                Ct = ct,
                LineBuffers = lineBuffers,
                CumOffsets = cumOffsets,
                RendererCount = rendererCount,
                HasGlow = _glowRenderer != null && glowPath.Length > 0,
                GlowPath = glowPath,
                GlowDia = glowDia,
                GlowSubdivisions = _glowSubdivisions
            };

            // Forward: reveal jumps 0 → jumpCount-1
            for (var i = 0; i < jumpCount; i++)
            {
                if (await StepJump(playback, i + 1, i > 0 ? i - 1 : -1, i, true))
                {
                    return;
                }
            }

            // Retraction: remove jumps jumpCount-1 → 0
            for (var i = jumpCount - 1; i >= 0; i--)
            {
                if (await StepJump(playback, i, i >= 1 ? i : -1, i >= 1 ? i - 1 : -1, false))
                {
                    return;
                }
            }

            InvokeComplete();
        }

        // Reveals (or retracts) one jump: updates the line renderers, fires the hit callback on
        // the way out, then animates the glow. Returns true if cancelled mid-step.
        private async UniTask<bool> StepJump(BoltPlayback p, int lineStage, int glowFrom, int glowTo, bool forward)
        {
            if (p.Ct.IsCancellationRequested)
            {
                return true;
            }

            ApplyLineRenderers(p.LineBuffers, p.CumOffsets[lineStage], p.RendererCount);

            if (forward)
            {
                _onTargetHit?.Invoke(lineStage - 1);
            }

            return await StepGlow(p, glowFrom, glowTo, forward);
        }

        private async UniTask<bool> StepGlow(BoltPlayback p, int glowFrom, int glowTo, bool forward)
        {
            if (p.HasGlow && glowFrom >= 0 && glowTo >= 0)
            {
                return await AnimateGlowSegment(p.GlowPath, p.GlowDia, GlowIdx(p, glowFrom), GlowIdx(p, glowTo), p.Ct);
            }

            if (p.HasGlow && forward)
            {
                SetGlowFromPath(p.GlowPath, p.GlowDia, 0f);
            }
            else if (p.HasGlow)
            {
                DisableGlow();
            }

            return await WaitJump(p.Ct);
        }

        private static float GlowIdx(BoltPlayback p, int stage)
        {
            return Mathf.Min(stage * p.GlowSubdivisions, p.GlowPath.Length - 1);
        }

        // Snapshot of the per-play buffers + glow state, so the jump steps read it without a
        // tangle of captured locals.
        private struct BoltPlayback
        {
            public CancellationToken Ct;
            public Vector3[][] LineBuffers;
            public int[] CumOffsets;
            public int RendererCount;
            public bool HasGlow;
            public Vector3[] GlowPath;
            public float[] GlowDia;
            public int GlowSubdivisions;
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
