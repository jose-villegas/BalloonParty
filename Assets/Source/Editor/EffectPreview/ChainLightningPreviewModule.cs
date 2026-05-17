using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Editor.EditorUI;
using BalloonParty.Item.Lightning;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Preview module for <see cref="ChainLightningView" />. Generates random
    ///     grid positions as targets, fills jagged bolt segments into the view's
    ///     <see cref="LineRenderer" />s, and animates forward growth + retraction
    ///     synchronously via delta-time ticks.
    /// </summary>
    internal sealed class ChainLightningPreviewModule : IEffectPreviewModule
    {
        private static readonly FieldInfo LineRenderersField =
            typeof(ChainLightningView).GetField("_lineRenderers", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo GlowRendererField =
            typeof(ChainLightningView).GetField("_glowLineRenderer", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly ChainLightningView _view;
        private readonly ConfigAssetCache<ItemConfiguration> _itemConfigCache = new();

        private int _targetCount = 5;

        private LineRenderer[] _lineRenderers;
        private LineRenderer _glowRenderer;
        private Vector3[][] _lineBuffers;
        private int[] _cumOffsets;
        private int _jumpCount;
        private float _jumpTime;
        private float _elapsed;
        private int _currentJump;
        private bool _retracting;
        private bool _finished;

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
            _glowRenderer = (LineRenderer)GlowRendererField.GetValue(_view);

            var settings = context.Settings;
            var segMul = settings?.LightningSegmentsMultiplier ?? 3f;
            var randomness = settings?.LightningRandomness ?? 0.2f;
            _jumpTime = settings?.LightningJumpTime ?? 0.15f;

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
                // Fallback: radial positions around origin
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
                targets, rendererCount, segMul, randomness);

            _elapsed = 0f;
            _currentJump = 0;
            _retracting = false;
            _finished = false;

            ClearRenderers();

            // Show the first jump immediately
            ApplyRenderers(_cumOffsets[1]);
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
                    return true;
                }

                ApplyRenderers(_cumOffsets[_currentJump + 1]);
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

            SyncGlow(pointCount, rendererCount);
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
                _glowRenderer.positionCount = 0;
            }
        }

        private void SyncGlow(int count, int rendererCount)
        {
            if (_glowRenderer == null || rendererCount == 0)
            {
                return;
            }

            _glowRenderer.positionCount = count;
            if (count > 0)
            {
                _glowRenderer.SetPositions(_lineBuffers[0]);
            }
        }
    }
}

