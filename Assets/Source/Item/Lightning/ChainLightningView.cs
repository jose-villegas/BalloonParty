using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Shared.Animation;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Rendering;
using Cysharp.Threading.Tasks;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item.Lightning
{
    /// <summary>
    ///     Poolable chain-lightning effect. Call <see cref="PrepareDisplay" /> before <see cref="Play" />.
    /// </summary>
    public class ChainLightningView : EffectView, IChainEffect
    {
        [SerializeField] private LineRenderer[] _lineRenderers;
        [SerializeField] private SpriteRenderer _glowRenderer;
        [SerializeField] [Range(0f, 4f)] private float _glowColorIntensity = 1f;

        private CancellationTokenSource _cts;
        private float _fractalDecay;
        private int _glowSubdivisions;
        private float _jumpTime;
        private Action<int> _onTargetHit;
        private float _randomness;
        private float _segmentsMultiplier;
        private IReadOnlyList<Vector3> _targetPositions;

        private ColorCycleState _glowCycleState;
        private Color _glowFallbackColor = Color.white;
        private float _glowStartTime;
        private float _glowTotalDuration;

        public override void OnSpawned()
        {
            base.OnSpawned();
            _cts = new CancellationTokenSource();
            ClearRenderers();
        }

        public override void OnDespawned()
        {
            LifecycleHelper.CancelAndDispose(ref _cts);
            _glowCycleState.Clear();
            ClearRenderers();
            base.OnDespawned();
        }

        /// <summary>
        ///     Starts the chain-lightning animation; <paramref name="position" /> is unused. <paramref name="tint" />
        ///     is the glow's fallback colour when no cycle colours are set (see <see cref="SetGlowColors" />).
        /// </summary>
        public override void Play(Vector3 position, Color tint, Action onComplete = null)
        {
            OnComplete = onComplete;
            _glowFallbackColor = tint;
            ApplyGlowColor();

            if (_targetPositions == null || _targetPositions.Count < 2)
            {
                InvokeComplete();
                return;
            }

            PlayAsync().Forget();
        }

        public void SetGlowColors(IReadOnlyList<Color> colors, float cycles)
        {
            _glowCycleState.Set(colors, cycles);
        }

        /// <summary>
        ///     Sets the target chain before calling <see cref="Play" />; index 0 is the item balloon.
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
            ApplyGlowColor();
        }

        // Lerps the glow through its colour set, _glowCycles full loops over the anim's duration (a single
        // colour is static). Scaled by _glowColorIntensity; keeps the sprite's designed alpha.
        private void ApplyGlowColor()
        {
            if (_glowRenderer == null)
            {
                return;
            }

            var color = _glowFallbackColor;
            if (_glowCycleState.HasColors)
            {
                var progress = _glowTotalDuration > 0f
                    ? Mathf.Clamp01((Time.time - _glowStartTime) / _glowTotalDuration)
                    : 0f;
                color = _glowCycleState.Sample(progress, _glowFallbackColor);
            }

            _glowRenderer.color = (color * _glowColorIntensity).WithAlpha(_glowRenderer.color.a);
        }

        private async UniTaskVoid PlayAsync()
        {
            var ct = _cts?.Token ?? CancellationToken.None;
            var jumpCount = _targetPositions.Count - 1;
            var rendererCount = _lineRenderers != null ? _lineRenderers.Length : 0;

            // Forward then retract, one jump-time each — the window the glow colour cycles over.
            _glowStartTime = Time.time;
            _glowTotalDuration = Mathf.Max(0.0001f, 2 * jumpCount * _jumpTime);

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

            // Reveal jumps forward.
            for (var i = 0; i < jumpCount; i++)
            {
                if (await StepJump(playback, i + 1, i > 0 ? i - 1 : -1, i, true))
                {
                    return;
                }
            }

            // Retract jumps in reverse.
            for (var i = jumpCount - 1; i >= 0; i--)
            {
                if (await StepJump(playback, i, i >= 1 ? i : -1, i >= 1 ? i - 1 : -1, false))
                {
                    return;
                }
            }

            InvokeComplete();
        }

        // Returns true if cancelled mid-step.
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

        // Snapshot of per-play buffers and glow state, read by the jump steps.
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
        ///     Waits one jump duration.
        /// </summary>
        private async UniTask<bool> WaitJump(CancellationToken ct)
        {
            var delayMs = Mathf.RoundToInt(_jumpTime * 1000f);

            await UniTask.Delay(delayMs, cancellationToken: ct).SuppressCancellationThrow();

            return ct.IsCancellationRequested;
        }
    }
}
