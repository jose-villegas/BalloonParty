using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    internal sealed class BushBakerWindow : EditorWindow
    {
        private const float PreviewCellSize = 80f;
        private const float PreviewPadding = 4f;

        [SerializeField] private BushLeafBakeSettings _leafSettings = new();
        [SerializeField] private BushCanopyBakeSettings _canopySettings = new();
        [SerializeField] private string _outputFolder = "Assets/Art/Bush/Baked";

        private Texture2D[] _leafPreviews;
        private Vector2 _scrollPosition;

        [MenuItem("Tools/Bush Baker")]
        private static void Open()
        {
            GetWindow<BushBakerWindow>("Bush Baker");
        }

        private void OnDisable()
        {
            DestroyPreviews();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawLeafSection();
            EditorGUILayout.Space(16);
            DrawCanopySection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawLeafSection()
        {
            EditorGUILayout.LabelField("Leaf Atlas", EditorStyles.boldLabel);

            _leafSettings.Resolution = EditorGUILayout.IntPopup(
                "Resolution", _leafSettings.Resolution,
                new[] { "32", "64", "128", "256" },
                new[] { 32, 64, 128, 256 });

            _leafSettings.LeafRadius = EditorGUILayout.Slider("Leaf Radius", _leafSettings.LeafRadius, 0.1f, 1f);
            _leafSettings.LeafVariants = EditorGUILayout.IntSlider("Variants", _leafSettings.LeafVariants, 1, 16);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Gielis Superformula", EditorStyles.miniLabel);
            _leafSettings.GielisM = EditorGUILayout.Slider("Lobe Count (m)", _leafSettings.GielisM, 0f, 6f);
            _leafSettings.GielisN1 = EditorGUILayout.Slider("Curvature (n1)", _leafSettings.GielisN1, 0.1f, 4f);
            _leafSettings.GielisN2 = EditorGUILayout.Slider("Lateral (n2)", _leafSettings.GielisN2, 0.1f, 4f);
            _leafSettings.GielisN3 = EditorGUILayout.Slider("Lateral (n3)", _leafSettings.GielisN3, 0.1f, 4f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Shading", EditorStyles.miniLabel);
            _leafSettings.SSSStrength = EditorGUILayout.Slider("SSS Strength", _leafSettings.SSSStrength, 0f, 1f);
            _leafSettings.SSSAbsorption = EditorGUILayout.Slider("SSS Absorption", _leafSettings.SSSAbsorption, 0.5f, 10f);
            _leafSettings.SSSColor = EditorGUILayout.ColorField("SSS Color", _leafSettings.SSSColor);
            _leafSettings.HueJitter = EditorGUILayout.Slider("Hue Jitter (°)", _leafSettings.HueJitter, 0f, 30f);
            _leafSettings.EdgeBrowningWidth = EditorGUILayout.Slider("Edge Browning", _leafSettings.EdgeBrowningWidth, 0.01f, 0.5f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Venation", EditorStyles.miniLabel);
            _leafSettings.VeinSources = EditorGUILayout.IntSlider("Vein Sources", _leafSettings.VeinSources, 0, 300);

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Preview Leaves", GUILayout.Height(28)))
            {
                GenerateLeafPreviews();
            }

            if (GUILayout.Button("Export Leaf Atlas", GUILayout.Height(28)))
            {
                ExportLeafAtlas();
            }

            EditorGUILayout.EndHorizontal();

            DrawLeafPreviewGrid();
        }

        private void DrawCanopySection()
        {
            EditorGUILayout.LabelField("Canopy (Phase 2)", EditorStyles.boldLabel);
            GUI.enabled = false;
            _canopySettings.Resolution = EditorGUILayout.IntPopup(
                "Resolution", _canopySettings.Resolution,
                new[] { "128", "256", "512" },
                new[] { 128, 256, 512 });
            _canopySettings.SlotCount = EditorGUILayout.IntSlider("Slot Count", _canopySettings.SlotCount, 1, 5);
            _canopySettings.CanopyVariants = EditorGUILayout.IntSlider("Variants", _canopySettings.CanopyVariants, 1, 8);
            GUI.enabled = true;

            EditorGUILayout.HelpBox(
                "Canopy baking will be implemented in Phase 2.",
                MessageType.Info);
        }

        private void DrawLeafPreviewGrid()
        {
            if (_leafPreviews == null || _leafPreviews.Length == 0)
            {
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Preview", EditorStyles.miniLabel);

            var totalWidth = EditorGUIUtility.currentViewWidth - 20f;
            var cols = Mathf.Max(1, Mathf.FloorToInt(totalWidth / (PreviewCellSize + PreviewPadding)));

            var startRect = GUILayoutUtility.GetRect(
                totalWidth,
                Mathf.CeilToInt((float)_leafPreviews.Length / cols) * (PreviewCellSize + PreviewPadding));

            for (var i = 0; i < _leafPreviews.Length; i++)
            {
                if (_leafPreviews[i] == null)
                {
                    continue;
                }

                var col = i % cols;
                var row = i / cols;
                var rect = new Rect(
                    startRect.x + col * (PreviewCellSize + PreviewPadding),
                    startRect.y + row * (PreviewCellSize + PreviewPadding),
                    PreviewCellSize,
                    PreviewCellSize);

                EditorGUI.DrawPreviewTexture(rect, _leafPreviews[i], null, ScaleMode.ScaleToFit);
                EditorGUI.DropShadowLabel(
                    new Rect(rect.x, rect.yMax - 16, rect.width, 16),
                    $"#{i}",
                    EditorStyles.miniLabel);
            }
        }

        private void GenerateLeafPreviews()
        {
            DestroyPreviews();

            var count = Mathf.Max(1, _leafSettings.LeafVariants);
            _leafPreviews = new Texture2D[count];

            for (var i = 0; i < count; i++)
            {
                var seed = (uint)(i * 7919 + 31);
                _leafPreviews[i] = BushLeafBaker.BakeLeaf(_leafSettings, i, seed);
            }

            Repaint();
        }

        private void ExportLeafAtlas()
        {
            var path = $"{_outputFolder}/LeafAtlas.png";
            var result = LeafAtlasPacker.Pack(_leafSettings, path);

            if (result.Atlas != null)
            {
                EditorGUIUtility.PingObject(result.Atlas);
            }
        }

        private void DestroyPreviews()
        {
            if (_leafPreviews == null)
            {
                return;
            }

            foreach (var tex in _leafPreviews)
            {
                if (tex != null)
                {
                    DestroyImmediate(tex);
                }
            }

            _leafPreviews = null;
        }
    }
}

