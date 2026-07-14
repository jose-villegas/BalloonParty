using System.Linq;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Replaces the inherited single-colour preview (meaningless for a rainbow — it has no
    ///     concrete colour) with an allowed-colours MASK preview: pick a set of palette colours
    ///     and push them into the banded shader exactly as a level's AllowedColorsMask would.
    /// </summary>
    [CustomEditor(typeof(RainbowBalloonVariant))]
    internal sealed class RainbowBalloonVariantEditor : UnityEditor.Editor
    {
        private readonly ConfigAssetCache<GamePalette> _paletteCache = new();

        // The four progress colours, matching a typical late-level allowed set.
        private int _previewMask = 0b1111;
        private string[] _names;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Bands Preview", EditorStyles.boldLabel);

            var palette = _paletteCache.Value;
            if (palette == null || palette.Colors.Count == 0)
            {
                EditorGUILayout.HelpBox("No GamePalette asset found.", MessageType.Warning);
                return;
            }

            // Bit i = palette index i — the same encoding as a level's AllowedColorsMask.
            _names ??= palette.Colors.Select(c => c.Name).ToArray();
            _previewMask = EditorGUILayout.MaskField("Allowed Colors", _previewMask, _names);

            if (GUILayout.Button("Apply Bands Preview"))
            {
                ((RainbowBalloonVariant)target).PushBands(palette, _previewMask);
                SceneView.RepaintAll();
            }
        }
    }
}
