using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Item.Lightning;
using BalloonParty.Shared.Animation;
using BalloonParty.Shared.Extensions;
using UnityEditor;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Preview module for <see cref="ChainLightningView" />.
    /// </summary>
    internal sealed class ChainLightningPreviewModule : IEffectPreviewModule
    {
        private static readonly FieldInfo LineRenderersField =
            typeof(ChainLightningView).GetField("_lineRenderers", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo GlowRendererField =
            typeof(ChainLightningView).GetField("_glowRenderer", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo GlowColorIntensityField =
            typeof(ChainLightningView).GetField("_glowColorIntensity", BindingFlags.NonPublic | BindingFlags.Instance);

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

        public bool UsesColorPicker => true;

        private float GlowMaxIdx => _glowPath != null && _glowPath.Length > 0 ? _glowPath.Length - 1 : 0f;

        internal ChainLightningPreviewModule(ChainLightningView view)
        {
            _view = view;
        }

        public void DrawGUI()
        {
            _targetCount = EditorGUILayout.IntSlider("Target Count", _targetCount, 1, 20);

            var settings = PreStartSettings;
            EditorGUILayout.LabelField("Jump Time",
                $"{settings?.Lightning.JumpTime ?? 0.15f:F2}s  (from ItemConfiguration)");
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

            if (_glowRenderer != null)
            {
                // Mirror ChainLightningView.Play: glow = chain colour * intensity, sprite alpha kept.
                var intensity = (float)GlowColorIntensityField.GetValue(_view);
                _glowRenderer.color = (context.Tint * intensity).WithAlpha(_glowRenderer.color.a);
            }

            var settings = context.Settings;
            var segMul = settings?.Lightning.SegmentsMultiplier ?? 3f;
            var randomness = settings?.Lightning.Randomness ?? 0.2f;
            _jumpTime = settings?.Lightning.JumpTime ?? 0.15f;
            _glowSubdivisions = settings?.Lightning.GlowSubdivisions ?? 4;
            var fractalDecay = settings?.Lightning.FractalDecay ?? 0.55f;

            var origin = _view.transform.position;

            var targets = new List<Vector3> { origin };

            if (context.GameConfig != null)
            {
                var gridPositions = EditorGridHelper.RandomSlotPositions(
                    _targetCount,
                    context.GameConfig,
                    origin);
                targets.AddRange(gridPositions);
            }
            else
            {
                for (var i = 0; i < _targetCount; i++)
                {
                    var angle = 360f / _targetCount * i * Mathf.Deg2Rad;
                    Vector3 direction = VectorMathExtensions.DirectionFromAngle(angle);
                    targets.Add(origin + direction * 2f);
                }
            }

            _jumpCount = targets.Count - 1;
            if (_jumpCount <= 0)
            {
                _finished = true;
                return;
            }

            var rendererCount = _lineRenderers != null ? _lineRenderers.Length : 0;

            (_lineBuffers, _cumOffsets) = ChainLightningGeometry.BuildBoltBuffers(
                targets,
                rendererCount,
                segMul,
                randomness,
                fractalDecay);

            (_glowPath, _glowDiameters) = ChainLightningGeometry.BuildGlowPath(targets, _glowSubdivisions);

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

            return _retracting ? AdvanceRetract() : AdvanceForward();
        }

        // Flips into retraction after the last jump.
        private bool AdvanceForward()
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
            return true;
        }

        // Finishes once fully retracted.
        private bool AdvanceRetract()
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

        // No static region to outline — the arcs are the preview.
        public void DrawSceneGizmos()
        {
        }

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
