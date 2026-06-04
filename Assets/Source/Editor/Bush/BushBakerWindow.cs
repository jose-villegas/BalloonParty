using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    internal sealed class BushBakerWindow : EditorWindow
    {
        private const float PreviewCellSize = 80f;
        private const float PreviewPadding = 4f;

        private Texture2D[] _leafPreviews;
        private Texture2D[] _canopyPreviews;
        private Texture2D _livePreview;
        private Vector2 _scrollPosition;
        private int _lastSettingsHash;

        private BushBakerState State => BushBakerState.instance;

        [MenuItem("Tools/Bush Baker")]
        private static void Open()
        {
            GetWindow<BushBakerWindow>("Bush Baker");
        }

        private void OnDisable()
        {
            DestroyLeafPreviews();
            DestroyCanopyPreviews();
            DestroyLivePreview();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawLeafSection();
            EditorGUILayout.Space(16);
            DrawCanopySection();

            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
            {
                State.Save();
            }

            if (State.AutoPreview)
            {
                CheckAutoPreview();
            }
        }

        private void DrawLeafSection()
        {
            EditorGUILayout.LabelField("Leaf Atlas", EditorStyles.boldLabel);

            State.LeafSettings.Resolution = EditorGUILayout.IntPopup(
                "Resolution", State.LeafSettings.Resolution,
                new[] { "32", "64", "128", "256" },
                new[] { 32, 64, 128, 256 });
            State.LeafSettings.LeafRadius = EditorGUILayout.Slider("Leaf Radius", State.LeafSettings.LeafRadius, 0.1f, 1f);
            State.LeafSettings.LeafVariants = EditorGUILayout.IntSlider("Variants", State.LeafSettings.LeafVariants, 1, 16);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Gielis Superformula", EditorStyles.miniLabel);
            State.LeafSettings.GielisM = EditorGUILayout.Slider("Lobe Count (m)", State.LeafSettings.GielisM, 0f, 6f);
            State.LeafSettings.GielisN1 = EditorGUILayout.Slider("Curvature (n1)", State.LeafSettings.GielisN1, 0.1f, 4f);
            State.LeafSettings.GielisN2 = EditorGUILayout.Slider("Lateral (n2)", State.LeafSettings.GielisN2, 0.1f, 4f);
            State.LeafSettings.GielisN3 = EditorGUILayout.Slider("Lateral (n3)", State.LeafSettings.GielisN3, 0.1f, 4f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Surface & Shading", EditorStyles.miniLabel);
            State.LeafSettings.BaseColor = EditorGUILayout.ColorField("Base Color", State.LeafSettings.BaseColor);
            State.LeafSettings.EdgeShade = EditorGUILayout.Slider("Edge Shade", State.LeafSettings.EdgeShade, 0.4f, 1f);
            State.LeafSettings.HighlightColor = EditorGUILayout.ColorField("Highlight Color", State.LeafSettings.HighlightColor);
            State.LeafSettings.HighlightSize = EditorGUILayout.Slider("Highlight Size", State.LeafSettings.HighlightSize, 0.05f, 0.7f);
            State.LeafSettings.HighlightOffset = EditorGUILayout.Slider("Highlight Offset", State.LeafSettings.HighlightOffset, -0.5f, 0.5f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Veins", EditorStyles.miniLabel);
            State.LeafSettings.VeinWidth = EditorGUILayout.Slider("Vein Width", State.LeafSettings.VeinWidth, 0.01f, 0.15f);
            State.LeafSettings.VeinDarken = EditorGUILayout.Slider("Vein Darken", State.LeafSettings.VeinDarken, 0.5f, 1f);
            State.LeafSettings.LateralVeinCount = EditorGUILayout.IntSlider("Lateral Count", State.LeafSettings.LateralVeinCount, 3, 12);
            State.LeafSettings.LateralVeinAngle = EditorGUILayout.Slider("Lateral Angle", State.LeafSettings.LateralVeinAngle, 0.3f, 3f);
            State.LeafSettings.VeinSources = EditorGUILayout.IntSlider("Runions Sources", State.LeafSettings.VeinSources, 0, 300);
            State.LeafSettings.VeinTexStrength = EditorGUILayout.Slider("Runions Strength", State.LeafSettings.VeinTexStrength, 0f, 1f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("SSS", EditorStyles.miniLabel);
            State.LeafSettings.SSSStrength = EditorGUILayout.Slider("SSS Strength", State.LeafSettings.SSSStrength, 0f, 1f);
            State.LeafSettings.SSSAbsorption = EditorGUILayout.Slider("SSS Absorption", State.LeafSettings.SSSAbsorption, 0.5f, 10f);
            State.LeafSettings.SSSColor = EditorGUILayout.ColorField("SSS Color", State.LeafSettings.SSSColor);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Colour Variation", EditorStyles.miniLabel);
            State.LeafSettings.HueJitter = EditorGUILayout.Slider("Hue Jitter (°)", State.LeafSettings.HueJitter, 0f, 30f);
            State.LeafSettings.EdgeBrowningWidth = EditorGUILayout.Slider("Edge Browning", State.LeafSettings.EdgeBrowningWidth, 0.01f, 0.5f);
            State.LeafSettings.BrowningColor = EditorGUILayout.ColorField("Browning Color", State.LeafSettings.BrowningColor);

            EditorGUILayout.Space(8);

            State.AutoPreview = EditorGUILayout.Toggle("Live Preview", State.AutoPreview);
            DrawLivePreview();

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Preview All Variants", GUILayout.Height(28)))
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

        private void DrawLivePreview()
        {
            if (_livePreview == null)
            {
                return;
            }

            var previewSize = Mathf.Min(160f, EditorGUIUtility.currentViewWidth - 40f);
            var rect = GUILayoutUtility.GetRect(previewSize, previewSize);
            rect.width = previewSize;
            EditorGUI.DrawTextureTransparent(rect, _livePreview, ScaleMode.ScaleToFit);
        }

        private void CheckAutoPreview()
        {
            var hash = ComputeSettingsHash();
            if (hash == _lastSettingsHash)
            {
                return;
            }

            _lastSettingsHash = hash;
            DestroyLivePreview();
            _livePreview = BushLeafBaker.BakeLeaf(State.LeafSettings, 0, 42);
            Repaint();
        }

        private int ComputeSettingsHash()
        {
            unchecked
            {
                var h = 17;
                h = h * 31 + State.LeafSettings.Resolution.GetHashCode();
                h = h * 31 + State.LeafSettings.LeafRadius.GetHashCode();
                h = h * 31 + State.LeafSettings.GielisM.GetHashCode();
                h = h * 31 + State.LeafSettings.GielisN1.GetHashCode();
                h = h * 31 + State.LeafSettings.GielisN2.GetHashCode();
                h = h * 31 + State.LeafSettings.GielisN3.GetHashCode();
                h = h * 31 + State.LeafSettings.BaseColor.GetHashCode();
                h = h * 31 + State.LeafSettings.EdgeShade.GetHashCode();
                h = h * 31 + State.LeafSettings.HighlightSize.GetHashCode();
                h = h * 31 + State.LeafSettings.VeinWidth.GetHashCode();
                h = h * 31 + State.LeafSettings.VeinDarken.GetHashCode();
                h = h * 31 + State.LeafSettings.LateralVeinCount.GetHashCode();
                h = h * 31 + State.LeafSettings.LateralVeinAngle.GetHashCode();
                h = h * 31 + State.LeafSettings.VeinSources.GetHashCode();
                h = h * 31 + State.LeafSettings.VeinTexStrength.GetHashCode();
                h = h * 31 + State.LeafSettings.SSSStrength.GetHashCode();
                h = h * 31 + State.LeafSettings.SSSAbsorption.GetHashCode();
                h = h * 31 + State.LeafSettings.HueJitter.GetHashCode();
                h = h * 31 + State.LeafSettings.EdgeBrowningWidth.GetHashCode();
                return h;
            }
        }

        private void DrawCanopySection()
        {
            EditorGUILayout.LabelField("Canopy", EditorStyles.boldLabel);

            State.CanopySettings.Resolution = EditorGUILayout.IntPopup(
                "Resolution", State.CanopySettings.Resolution,
                new[] { "128", "256", "512" },
                new[] { 128, 256, 512 });
            State.CanopySettings.SlotCount = EditorGUILayout.IntSlider("Slot Count", State.CanopySettings.SlotCount, 1, 5);
            State.CanopySettings.CanopyVariants = EditorGUILayout.IntSlider("Variants", State.CanopySettings.CanopyVariants, 1, 8);

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Preview Canopies", GUILayout.Height(28)))
            {
                GenerateCanopyPreviews();
            }

            if (GUILayout.Button("Export Canopy Variants", GUILayout.Height(28)))
            {
                ExportCanopyVariants();
            }

            EditorGUILayout.EndHorizontal();

            DrawCanopyPreviewGrid();
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

        private void DrawCanopyPreviewGrid()
        {
            if (_canopyPreviews == null || _canopyPreviews.Length == 0)
            {
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Canopy Preview", EditorStyles.miniLabel);

            var totalWidth = EditorGUIUtility.currentViewWidth - 20f;
            var cellSize = PreviewCellSize * 1.5f;
            var cols = Mathf.Max(1, Mathf.FloorToInt(totalWidth / (cellSize + PreviewPadding)));

            var startRect = GUILayoutUtility.GetRect(
                totalWidth,
                Mathf.CeilToInt((float)_canopyPreviews.Length / cols) * (cellSize + PreviewPadding));

            for (var i = 0; i < _canopyPreviews.Length; i++)
            {
                if (_canopyPreviews[i] == null)
                {
                    continue;
                }

                var col = i % cols;
                var row = i / cols;
                var rect = new Rect(
                    startRect.x + col * (cellSize + PreviewPadding),
                    startRect.y + row * (cellSize + PreviewPadding),
                    cellSize,
                    cellSize);

                EditorGUI.DrawPreviewTexture(rect, _canopyPreviews[i], null, ScaleMode.ScaleToFit);
                EditorGUI.DropShadowLabel(
                    new Rect(rect.x, rect.yMax - 16, rect.width, 16),
                    $"Canopy #{i}",
                    EditorStyles.miniLabel);
            }
        }

        private void GenerateLeafPreviews()
        {
            DestroyLeafPreviews();

            var count = Mathf.Max(1, State.LeafSettings.LeafVariants);
            _leafPreviews = new Texture2D[count];

            for (var i = 0; i < count; i++)
            {
                var seed = (uint)(i * 7919 + 31);
                _leafPreviews[i] = BushLeafBaker.BakeLeaf(State.LeafSettings, i, seed);
            }

            Repaint();
        }

        private void ExportLeafAtlas()
        {
            var path = $"{State.OutputFolder}/LeafAtlas.png";
            var result = LeafAtlasPacker.Pack(State.LeafSettings, path);

            if (result.Atlas != null)
            {
                EditorGUIUtility.PingObject(result.Atlas);
            }
        }

        private void GenerateCanopyPreviews()
        {
            DestroyCanopyPreviews();

            var count = Mathf.Max(1, State.CanopySettings.CanopyVariants);
            _canopyPreviews = BushCanopyBaker.BakeVariants(State.CanopySettings, count);

            Repaint();
        }

        private void ExportCanopyVariants()
        {
            DestroyCanopyPreviews();

            var count = Mathf.Max(1, State.CanopySettings.CanopyVariants);
            var variants = BushCanopyBaker.BakeVariants(State.CanopySettings, count);
            var paths = BushCanopyBaker.ExportVariants(variants, State.OutputFolder);

            foreach (var tex in variants)
            {
                if (tex != null)
                {
                    DestroyImmediate(tex);
                }
            }

            if (paths.Length > 0 && paths[0] != null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[0]);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                }
            }
        }

        private void DestroyLeafPreviews()
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

        private void DestroyCanopyPreviews()
        {
            if (_canopyPreviews == null)
            {
                return;
            }

            foreach (var tex in _canopyPreviews)
            {
                if (tex != null)
                {
                    DestroyImmediate(tex);
                }
            }

            _canopyPreviews = null;
        }

        private void DestroyLivePreview()
        {
            if (_livePreview != null)
            {
                DestroyImmediate(_livePreview);
                _livePreview = null;
            }
        }
    }
}
