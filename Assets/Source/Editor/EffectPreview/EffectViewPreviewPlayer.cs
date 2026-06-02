using System;
using BalloonParty.Configuration;
using BalloonParty.Editor.EditorUI;
using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Reusable editor preview player for <see cref="Shared.Pool.EffectView" />
    ///     subclasses. Owns the animation loop, palette color picker, and config
    ///     caches. Delegates rendering logic to an <see cref="IEffectPreviewModule" />.
    /// </summary>
    internal sealed class EffectViewPreviewPlayer
    {
        private readonly EditorAnimationLoop _animLoop = new();
        private readonly PaletteColorPicker _colorPicker = new();
        private readonly ConfigAssetCache<GameConfiguration> _gameConfigCache = new();
        private readonly ConfigAssetCache<ItemConfiguration> _itemConfigCache = new();

        private readonly IEffectPreviewModule _module;
        private readonly string _headerLabel;
        private readonly ItemType _itemType;
        private readonly Action _repaint;

        internal bool IsPlaying => _animLoop.IsPlaying;
        internal GameConfiguration GameConfig => _gameConfigCache.Value;

        internal ItemSettings Settings
        {
            get
            {
                var config = _itemConfigCache.Value;
                return config != null ? config[_itemType] : null;
            }
        }

        internal EffectViewPreviewPlayer(
            IEffectPreviewModule module,
            string headerLabel,
            ItemType itemType,
            Action repaint)
        {
            _module = module;
            _headerLabel = headerLabel;
            _itemType = itemType;
            _repaint = repaint;
        }

        internal void DrawInspectorGUI()
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField(_headerLabel, EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_animLoop.IsPlaying))
            {
                if (_module.UsesColorPicker)
                {
                    _colorPicker.DrawLayout();
                }

                _module.DrawGUI();
            }

            _animLoop.DrawSpeedSlider();
            EditorGUILayout.Space(4);
            _animLoop.DrawControls(StartPreview);
        }

        internal void Stop()
        {
            if (_animLoop.IsPlaying)
            {
                _animLoop.Stop();
            }
        }

        private void StartPreview()
        {
            var context = new EffectPreviewContext
            {
                Tint = _module.UsesColorPicker ? _colorPicker.SelectedColor : Color.white,
                Settings = Settings,
                GameConfig = _gameConfigCache.Value
            };

            _module.Start(context);

            _animLoop.Start(
                _module.Tick,
                () =>
                {
                    _module.CleanUp();
                    SceneView.RepaintAll();
                    _repaint?.Invoke();
                },
                () =>
                {
                    SceneView.RepaintAll();
                    _repaint?.Invoke();
                });
        }
    }
}
