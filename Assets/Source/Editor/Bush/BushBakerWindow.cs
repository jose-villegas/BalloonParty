using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    internal sealed class BushBakerWindow : EditorWindow
    {
        private const float PreviewCellSize = 80f;
        private const float PreviewPadding = 4f;
        private const float LivePreviewSize = 220f;
        private const float PropertiesMinWidth = 280f;
        private const float PropertiesMaxWidth = 420f;

        private Texture2D[] _leafPreviews;
        private Texture2D[] _canopyPreviews;
        private Texture2D _leafLivePreview;
        private Texture2D _canopyLivePreview;
        private Vector2 _scrollPosition;
        private int _lastLeafHash;
        private int _lastCanopyHash;

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

            DrawSharedSettings();
            EditorGUILayout.Space(16);
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

        private void DrawSharedSettings()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            State.AutoPreview = EditorGUILayout.Toggle("Live Preview", State.AutoPreview);
            EditorGUI.indentLevel--;
        }

        private void DrawLeafSection()
        {
            State.LeafFoldout = EditorGUILayout.Foldout(State.LeafFoldout, "Leaf Atlas", true, EditorStyles.foldoutHeader);

            if (!State.LeafFoldout)
            {
                return;
            }

            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();

            // Left column — properties
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(PropertiesMinWidth), GUILayout.MaxWidth(PropertiesMaxWidth));
            DrawLeafProperties();
            EditorGUILayout.EndVertical();

            // Right column — live preview
            DrawLeafLivePreview();

            EditorGUILayout.EndHorizontal();

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

            EditorGUI.indentLevel--;
        }

        private void DrawLeafProperties()
        {
            State.LeafSettings.Resolution = EditorGUILayout.IntPopup(
                "Resolution", State.LeafSettings.Resolution,
                new[] { "32", "64", "128", "256" },
                new[] { 32, 64, 128, 256 });
            State.LeafSettings.LeafRadius = EditorGUILayout.Slider("Leaf Radius", State.LeafSettings.LeafRadius, 0.1f, 1f);
            State.LeafSettings.LeafVariants = EditorGUILayout.IntSlider("Variants", State.LeafSettings.LeafVariants, 1, 16);

            EditorGUILayout.Space(4);

            State.LeafShapeFoldout = EditorGUILayout.Foldout(State.LeafShapeFoldout, "Gielis Superformula", true);
            if (State.LeafShapeFoldout)
            {
                EditorGUI.indentLevel++;
                State.LeafSettings.GielisM = EditorGUILayout.Slider("Lobe Count (m)", State.LeafSettings.GielisM, 0f, 6f);
                State.LeafSettings.GielisN1 = EditorGUILayout.Slider("Curvature (n1)", State.LeafSettings.GielisN1, 0.1f, 4f);
                State.LeafSettings.GielisN2 = EditorGUILayout.Slider("Lateral (n2)", State.LeafSettings.GielisN2, 0.1f, 4f);
                State.LeafSettings.GielisN3 = EditorGUILayout.Slider("Lateral (n3)", State.LeafSettings.GielisN3, 0.1f, 4f);
                EditorGUI.indentLevel--;
            }

            State.LeafSurfaceFoldout = EditorGUILayout.Foldout(State.LeafSurfaceFoldout, "Surface & Shading", true);
            if (State.LeafSurfaceFoldout)
            {
                EditorGUI.indentLevel++;
                State.LeafSettings.BaseColor = EditorGUILayout.ColorField("Base Color", State.LeafSettings.BaseColor);
                State.LeafSettings.EdgeShade = EditorGUILayout.Slider("Edge Shade", State.LeafSettings.EdgeShade, 0.4f, 1f);
                State.LeafSettings.HighlightColor = EditorGUILayout.ColorField("Highlight Color", State.LeafSettings.HighlightColor);
                State.LeafSettings.HighlightSize = EditorGUILayout.Slider("Highlight Size", State.LeafSettings.HighlightSize, 0.05f, 0.7f);
                State.LeafSettings.HighlightOffset = EditorGUILayout.Slider("Highlight Offset", State.LeafSettings.HighlightOffset, -0.5f, 0.5f);
                EditorGUI.indentLevel--;
            }

            State.LeafVeinsFoldout = EditorGUILayout.Foldout(State.LeafVeinsFoldout, "Veins", true);
            if (State.LeafVeinsFoldout)
            {
                EditorGUI.indentLevel++;
                State.LeafSettings.VeinWidth = EditorGUILayout.Slider("Vein Width", State.LeafSettings.VeinWidth, 0.01f, 0.15f);
                State.LeafSettings.VeinDarken = EditorGUILayout.Slider("Vein Darken", State.LeafSettings.VeinDarken, 0.5f, 1f);
                State.LeafSettings.VeinSources = EditorGUILayout.IntSlider("Runions Sources", State.LeafSettings.VeinSources, 0, 300);
                State.LeafSettings.VeinTexStrength = EditorGUILayout.Slider("Runions Strength", State.LeafSettings.VeinTexStrength, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            State.LeafSSSFoldout = EditorGUILayout.Foldout(State.LeafSSSFoldout, "SSS", true);
            if (State.LeafSSSFoldout)
            {
                EditorGUI.indentLevel++;
                State.LeafSettings.SSSStrength = EditorGUILayout.Slider("SSS Strength", State.LeafSettings.SSSStrength, 0f, 1f);
                State.LeafSettings.SSSAbsorption = EditorGUILayout.Slider("SSS Absorption", State.LeafSettings.SSSAbsorption, 0.5f, 10f);
                State.LeafSettings.SSSColor = EditorGUILayout.ColorField("SSS Color", State.LeafSettings.SSSColor);
                EditorGUI.indentLevel--;
            }

            State.LeafVariationFoldout = EditorGUILayout.Foldout(State.LeafVariationFoldout, "Colour Variation", true);
            if (State.LeafVariationFoldout)
            {
                EditorGUI.indentLevel++;
                State.LeafSettings.HueJitter = EditorGUILayout.Slider("Hue Jitter (°)", State.LeafSettings.HueJitter, 0f, 30f);
                State.LeafSettings.EdgeBrowningWidth = EditorGUILayout.Slider("Edge Browning", State.LeafSettings.EdgeBrowningWidth, 0.01f, 0.5f);
                State.LeafSettings.BrowningColor = EditorGUILayout.ColorField("Browning Color", State.LeafSettings.BrowningColor);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawLeafLivePreview()
        {
            if (_leafLivePreview == null)
            {
                return;
            }

            EditorGUILayout.BeginVertical(GUILayout.Width(LivePreviewSize + 8f), GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField("Preview", EditorStyles.centeredGreyMiniLabel);
            var rect = GUILayoutUtility.GetRect(LivePreviewSize, LivePreviewSize);
            rect.width = LivePreviewSize;
            EditorGUI.DrawTextureTransparent(rect, _leafLivePreview, ScaleMode.ScaleToFit);
            EditorGUILayout.EndVertical();
        }

        private void DrawCanopySection()
        {
            State.CanopyFoldout = EditorGUILayout.Foldout(State.CanopyFoldout, "Canopy", true, EditorStyles.foldoutHeader);

            if (!State.CanopyFoldout)
            {
                return;
            }

            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();

            // Left column — properties
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(PropertiesMinWidth), GUILayout.MaxWidth(PropertiesMaxWidth));
            DrawCanopyProperties();
            EditorGUILayout.EndVertical();

            // Right column — live preview
            DrawCanopyLivePreview();

            EditorGUILayout.EndHorizontal();

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

            EditorGUI.indentLevel--;
        }

        private void DrawCanopyProperties()
        {
            State.CanopySettings.Resolution = EditorGUILayout.IntPopup(
                "Resolution", State.CanopySettings.Resolution,
                new[] { "128", "256", "512" },
                new[] { 128, 256, 512 });
            State.CanopySettings.SlotCount = EditorGUILayout.IntSlider("Slot Count", State.CanopySettings.SlotCount, 1, 5);
            State.CanopySettings.CanopyVariants = EditorGUILayout.IntSlider("Variants", State.CanopySettings.CanopyVariants, 1, 8);

            EditorGUILayout.Space(4);

            State.CanopyShapeFoldout = EditorGUILayout.Foldout(State.CanopyShapeFoldout, "Shape", true);
            if (State.CanopyShapeFoldout)
            {
                EditorGUI.indentLevel++;
                State.CanopySettings.SlotRadius = EditorGUILayout.Slider("Slot Radius", State.CanopySettings.SlotRadius, 0.1f, 1f);
                State.CanopySettings.RadiusJitter = EditorGUILayout.Slider("Radius Jitter", State.CanopySettings.RadiusJitter, 0f, 0.15f);
                State.CanopySettings.BranchSpread = EditorGUILayout.Slider("Branch Spread", State.CanopySettings.BranchSpread, 0.1f, 0.8f);
                State.CanopySettings.SubCircleSize = EditorGUILayout.Slider("Leaf Size", State.CanopySettings.SubCircleSize, 0.1f, 0.7f);
                State.CanopySettings.SubCircleSizeVar = EditorGUILayout.Slider("Size Variation", State.CanopySettings.SubCircleSizeVar, 0f, 0.5f);
                EditorGUI.indentLevel--;
            }

            State.CanopyGielisFoldout = EditorGUILayout.Foldout(State.CanopyGielisFoldout, "Gielis Superformula", true);
            if (State.CanopyGielisFoldout)
            {
                EditorGUI.indentLevel++;
                State.CanopySettings.GielisM = EditorGUILayout.Slider("Lobe Count (m)", State.CanopySettings.GielisM, 0f, 6f);
                State.CanopySettings.GielisN1 = EditorGUILayout.Slider("Curvature (n1)", State.CanopySettings.GielisN1, 0.1f, 4f);
                State.CanopySettings.GielisN2 = EditorGUILayout.Slider("Lateral (n2)", State.CanopySettings.GielisN2, 0.1f, 4f);
                State.CanopySettings.GielisN3 = EditorGUILayout.Slider("Lateral (n3)", State.CanopySettings.GielisN3, 0.1f, 4f);
                EditorGUI.indentLevel--;
            }

            State.CanopySurfaceFoldout = EditorGUILayout.Foldout(State.CanopySurfaceFoldout, "Surface & Color", true);
            if (State.CanopySurfaceFoldout)
            {
                EditorGUI.indentLevel++;
                State.CanopySettings.BaseColor = EditorGUILayout.ColorField("Base Color (deep)", State.CanopySettings.BaseColor);
                State.CanopySettings.TopColor = EditorGUILayout.ColorField("Top Color (bright)", State.CanopySettings.TopColor);
                State.CanopySettings.EdgeShade = EditorGUILayout.Slider("Edge Shade", State.CanopySettings.EdgeShade, 0.4f, 1f);
                State.CanopySettings.HueJitter = EditorGUILayout.Slider("Hue Jitter (°)", State.CanopySettings.HueJitter, 0f, 30f);
                State.CanopySettings.EdgeBrowningWidth = EditorGUILayout.Slider("Edge Browning", State.CanopySettings.EdgeBrowningWidth, 0.01f, 0.5f);
                EditorGUI.indentLevel--;
            }

            State.CanopyVeinsFoldout = EditorGUILayout.Foldout(State.CanopyVeinsFoldout, "Veins", true);
            if (State.CanopyVeinsFoldout)
            {
                EditorGUI.indentLevel++;
                State.CanopySettings.VeinWidth = EditorGUILayout.Slider("Vein Width", State.CanopySettings.VeinWidth, 0.01f, 0.15f);
                State.CanopySettings.VeinDarken = EditorGUILayout.Slider("Vein Darken", State.CanopySettings.VeinDarken, 0.5f, 1f);
                EditorGUI.indentLevel--;
            }

            State.CanopySSSFoldout = EditorGUILayout.Foldout(State.CanopySSSFoldout, "SSS & Shadows", true);
            if (State.CanopySSSFoldout)
            {
                EditorGUI.indentLevel++;
                State.CanopySettings.SSSStrength = EditorGUILayout.Slider("SSS Strength", State.CanopySettings.SSSStrength, 0f, 1f);
                State.CanopySettings.SSSAbsorption = EditorGUILayout.Slider("SSS Absorption", State.CanopySettings.SSSAbsorption, 0.5f, 10f);
                State.CanopySettings.SSSColor = EditorGUILayout.ColorField("SSS Color", State.CanopySettings.SSSColor);
                State.CanopySettings.LeafShadowStrength = EditorGUILayout.Slider("Shadow Strength", State.CanopySettings.LeafShadowStrength, 0f, 0.6f);
                State.CanopySettings.ShadowSamples = EditorGUILayout.IntSlider("Shadow Samples", State.CanopySettings.ShadowSamples, 1, 8);
                State.CanopySettings.AOMul = EditorGUILayout.Slider("AO Strength", State.CanopySettings.AOMul, 0f, 1f);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawCanopyLivePreview()
        {
            if (_canopyLivePreview == null)
            {
                return;
            }

            EditorGUILayout.BeginVertical(GUILayout.Width(LivePreviewSize + 8f), GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField("Preview", EditorStyles.centeredGreyMiniLabel);
            var rect = GUILayoutUtility.GetRect(LivePreviewSize, LivePreviewSize);
            rect.width = LivePreviewSize;
            EditorGUI.DrawTextureTransparent(rect, _canopyLivePreview, ScaleMode.ScaleToFit);
            EditorGUILayout.EndVertical();
        }

        private void CheckAutoPreview()
        {
            var leafHash = ComputeLeafSettingsHash();
            if (leafHash != _lastLeafHash)
            {
                _lastLeafHash = leafHash;
                DestroyLeafLivePreview();
                _leafLivePreview = BushLeafBaker.BakeLeaf(State.LeafSettings, 0, 42);
                Repaint();
            }

            var canopyHash = ComputeCanopySettingsHash();
            if (canopyHash != _lastCanopyHash)
            {
                _lastCanopyHash = canopyHash;
                DestroyCanopyLivePreview();
                _canopyLivePreview = BushCanopyBaker.BakeCanopy(State.CanopySettings, 42);
                Repaint();
            }
        }

        private int ComputeLeafSettingsHash()
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
                h = h * 31 + State.LeafSettings.VeinSources.GetHashCode();
                h = h * 31 + State.LeafSettings.VeinTexStrength.GetHashCode();
                h = h * 31 + State.LeafSettings.SSSStrength.GetHashCode();
                h = h * 31 + State.LeafSettings.SSSAbsorption.GetHashCode();
                h = h * 31 + State.LeafSettings.HueJitter.GetHashCode();
                h = h * 31 + State.LeafSettings.EdgeBrowningWidth.GetHashCode();
                return h;
            }
        }

        private int ComputeCanopySettingsHash()
        {
            unchecked
            {
                var h = 17;
                h = h * 31 + State.CanopySettings.Resolution.GetHashCode();
                h = h * 31 + State.CanopySettings.SlotCount.GetHashCode();
                h = h * 31 + State.CanopySettings.SlotRadius.GetHashCode();
                h = h * 31 + State.CanopySettings.RadiusJitter.GetHashCode();
                h = h * 31 + State.CanopySettings.BranchSpread.GetHashCode();
                h = h * 31 + State.CanopySettings.SubCircleSize.GetHashCode();
                h = h * 31 + State.CanopySettings.SubCircleSizeVar.GetHashCode();
                h = h * 31 + State.CanopySettings.GielisM.GetHashCode();
                h = h * 31 + State.CanopySettings.GielisN1.GetHashCode();
                h = h * 31 + State.CanopySettings.GielisN2.GetHashCode();
                h = h * 31 + State.CanopySettings.GielisN3.GetHashCode();
                h = h * 31 + State.CanopySettings.BaseColor.GetHashCode();
                h = h * 31 + State.CanopySettings.TopColor.GetHashCode();
                h = h * 31 + State.CanopySettings.EdgeShade.GetHashCode();
                h = h * 31 + State.CanopySettings.HueJitter.GetHashCode();
                h = h * 31 + State.CanopySettings.SSSStrength.GetHashCode();
                h = h * 31 + State.CanopySettings.LeafShadowStrength.GetHashCode();
                h = h * 31 + State.CanopySettings.AOMul.GetHashCode();
                return h;
            }
        }

        private void DrawLeafPreviewGrid()
        {
            if (_leafPreviews == null || _leafPreviews.Length == 0)
            {
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("All Variants", EditorStyles.miniLabel);

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
            EditorGUILayout.LabelField("All Variants", EditorStyles.miniLabel);

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
            DestroyLeafLivePreview();
            DestroyCanopyLivePreview();
        }

        private void DestroyLeafLivePreview()
        {
            if (_leafLivePreview != null)
            {
                DestroyImmediate(_leafLivePreview);
                _leafLivePreview = null;
            }
        }

        private void DestroyCanopyLivePreview()
        {
            if (_canopyLivePreview != null)
            {
                DestroyImmediate(_canopyLivePreview);
                _canopyLivePreview = null;
            }
        }
    }
}
