using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Editor.EditorUI;
using BalloonParty.Item.Lightning;
using BalloonParty.Shared.Animation;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Preview module for <see cref="ChainLightningView" />. Generates random
    ///     grid positions as targets, fills jagged bolt segments into the view's
    ///     <see cref="LineRenderer" />s, and animates forward growth + retraction
    ///     synchronously via delta-time ticks. The glow sprite slides along a smooth
    ///     Catmull-Rom path through per-jump centroids.
    /// </summary>
    internal sealed class ChainLightningPreviewModule : IEffectPreviewModule
    {

        private static readonly FieldInfo LineRenderersField =
            typeof(ChainLightningView).GetField("_lineRenderers", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo GlowRendererField =
            typeof(ChainLightningView).GetField("_glowRenderer", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly ChainLightningView _view;
        private readonly ConfigAssetCache<ItemConfiguration> _itemConfigCache = new();

        private int _targetCount = 5;

        private LineRenderer[] _lineRenderers;
        private SpriteRenderer _glowRenderer;
        private Vector3[][] _lineBuffers;
        private int[] _cumOffsets;
        private int _jumpCount;
        private float _jumpTime;
        private float _elapsed;
        private int _currentJump;
        private bool _retracting;
        private bool _finished;

        private Vector3[] _glowPath;
        private float[] _glowDiameters;
        private float _glowFromIdx;
        private float _glowToIdx;
        private int _glowSubdivisions;

        internal ChainLightningPreviewModule(ChainLightningView view)
        {
            _view = view;
        }

        public bool UsesColorPicker => false;

        public void DrawGUI()
        {
            _targetCount = EditorGUILayout.IntSlider("Target Count", _targetCount, 1, 20);

            var settings = PreStartSettings;
            EditorGUILayout.LabelField("Jump Time",
                $"{(settings?.LightningJumpTime ?? 0.15f):F2}s  (from ItemConfiguration)");
        }

        private ItemSettings PreStartSettings
        {
            get
            {
                var config = _itemConfigCache.Value;
                return config != null ? config[ItemType.Lightning] : null;
            }
        }

        public void Start(EffectPreviewContext context)
        {
            _lineRenderers = (LineRenderer[])LineRenderersField.GetValue(_view);
            _glowRenderer = (SpriteRenderer)GlowRendererField.GetValue(_view);

            var settings = context.Settings;
            var segMul = settings?.LightningSegmentsMultiplier ?? 3f;
            var randomness = settings?.LightningRandomness ?? 0.2f;
            _jumpTime = settings?.LightningJumpTime ?? 0.15f;
            _glowSubdivisions = settings?.LightningGlowSubdivisions ?? 4;
            var fractalDecay = settings?.LightningFractalDecay ?? 0.55f;

            var origin = _view.transform.position;

            var targets = new List<Vector3> { origin };

            if (context.GameConfig != null)
            {
                var gridPositions = EditorGridHelper.RandomSlotPositions(
                    _targetCount, context.GameConfig, origin);
                targets.AddRange(gridPositions);
            }
            else
            {
                for (var i = 0; i < _targetCount; i++)
                {
                    var angle = 360f / _targetCount * i * Mathf.Deg2Rad;
                    targets.Add(origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 2f);
                }
            }

            _jumpCount = targets.Count - 1;
            if (_jumpCount <= 0)
            {
                _finished = true;
                return;
            }

            var rendererCount = _lineRenderers != null ? _lineRenderers.Length : 0;

            (_lineBuffers, _cumOffsets) = ChainLightningView.BuildBoltBuffers(
                targets, rendererCount, segMul, randomness, fractalDecay);

            (_glowPath, _glowDiameters) = ChainLightningView.BuildGlowPath(targets, _glowSubdivisions);

            _elapsed = 0f;
            _currentJump = 0;
            _retracting = false;
            _finished = false;
            _glowFromIdx = 0f;
            _glowToIdx = 0f;

            ClearRenderers();

            ApplyRenderers(_cumOffsets[1]);
            SetGlowFromPath(0f);
        }

        public bool Tick(float delta)
        {
            if (_finished)
            {
                return false;
            }

            _elapsed += delta;

            if (_elapsed < _jumpTime)
            {
                InterpolateGlow();
                return true;
            }

            _elapsed -= _jumpTime;

            if (!_retracting)
            {
                _currentJump++;

                if (_currentJump >= _jumpCount)
                {
                    _retracting = true;
                    _currentJump = _jumpCount - 1;
                    ApplyRenderers(_cumOffsets[_currentJump]);

                    var stage = _currentJump - 1;
                    _glowFromIdx = Mathf.Min((_jumpCount - 1) * _glowSubdivisions, GlowMaxIdx);
                    _glowToIdx = stage >= 0 ? Mathf.Min(stage * _glowSubdivisions, GlowMaxIdx) : 0f;
                    return true;
                }

                ApplyRenderers(_cumOffsets[_currentJump + 1]);

                _glowFromIdx = Mathf.Min((_currentJump - 1) * _glowSubdivisions, GlowMaxIdx);
                _glowToIdx = Mathf.Min(_currentJump * _glowSubdivisions, GlowMaxIdx);
            }
            else
            {
                _currentJump--;

                if (_currentJump < 0)
                {
                    ClearRenderers();
                    _finished = true;
                    return false;
                }

                var count = _cumOffsets[_currentJump];
                if (count > 0)
                {
                    ApplyRenderers(count);

                    var stage = _currentJump - 1;
                    _glowFromIdx = _glowToIdx;
                    _glowToIdx = stage >= 0 ? Mathf.Min(stage * _glowSubdivisions, GlowMaxIdx) : 0f;
                }
                else
                {
                    ClearRenderers();
                }
            }

            return true;
        }

        public void CleanUp()
        {
            ClearRenderers();
            _lineBuffers = null;
            _cumOffsets = null;
            _glowPath = null;
            _glowDiameters = null;
        }

        private float GlowMaxIdx => _glowPath != null && _glowPath.Length > 0 ? _glowPath.Length - 1 : 0f;

        private void InterpolateGlow()
        {
            if (_glowRenderer == null || _glowPath == null || _glowPath.Length == 0)
            {
                return;
            }

            if (!_retracting && _currentJump == 0 && _glowFromIdx == 0f && _glowToIdx == 0f)
            {
                SetGlowFromPath(0f);
                return;
            }

            var t = Mathf.Clamp01(_elapsed / _jumpTime);
            var pathIdx = Mathf.Lerp(_glowFromIdx, _glowToIdx, t);
            SetGlowFromPath(pathIdx);
        }

        private void SetGlowFromPath(float pathIndex)
        {
            if (_glowRenderer == null || _glowPath == null || _glowPath.Length == 0)
            {
                return;
            }

            var pos = PathHelper.SampleAt(_glowPath, pathIndex);
            var dia = PathHelper.SampleAt(_glowDiameters, pathIndex);

            _glowRenderer.enabled = true;
            _glowRenderer.transform.position = pos;
            _glowRenderer.transform.localScale = new Vector3(dia, dia, 1f);
        }

        private void ApplyRenderers(int pointCount)
        {
            var rendererCount = _lineRenderers != null ? _lineRenderers.Length : 0;

            for (var j = 0; j < rendererCount; j++)
            {
                if (_lineRenderers[j] == null)
                {
                    continue;
                }

                _lineRenderers[j].positionCount = pointCount;
                _lineRenderers[j].SetPositions(_lineBuffers[j]);
            }
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
    }
}
