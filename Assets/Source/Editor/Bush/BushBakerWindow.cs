using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    internal sealed class BushBakerWindow : EditorWindow
    {
        private const float PreviewCellSize = 80f;
        private const float PreviewPadding = 4f;
        private const float PreviewBoxMinSize = 120f;
        private const float PreviewBoxPadding = 6f;
        private const float PropertiesMinWidth = 280f;
        private const float PropertiesMaxWidth = 420f;

        private Texture2D[] _leafPreviews;
        private Texture2D _leafLivePreview;
        private bool _livePreviewOwned = true;
        private int _selectedVariant = -1;
        private Vector2 _scrollPosition;
        private int _lastLeafHash;

        private BushBakerState State => BushBakerState.instance;

        [MenuItem("Tools/Bush Baker")]
        private static void Open()
        {
            GetWindow<BushBakerWindow>("Bush Baker");
        }

        private void OnDisable()
        {
            DestroyLeafPreviews();
            DestroyLeafLivePreview();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUI.BeginChangeCheck();

            DrawSharedSettings();
            EditorGUILayout.Space(16);
            DrawLeafSection();

            if (EditorGUI.EndChangeCheck())
            {
                State.Save();
            }

            EditorGUILayout.EndScrollView();

            if (State.AutoPreview && Event.current.type == EventType.Repaint)
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

            DrawPropertiesAndPreview();

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

            State.LeafSurfaceFoldout = EditorGUILayout.Foldout(State.LeafSurfaceFoldout, "Surface", true);
            if (State.LeafSurfaceFoldout)
            {
                EditorGUI.indentLevel++;
                State.LeafSettings.BaseColor = EditorGUILayout.ColorField("Base Color", State.LeafSettings.BaseColor);
                State.LeafSettings.EdgeShade = EditorGUILayout.Slider("Edge Shade", State.LeafSettings.EdgeShade, 0.4f, 1f);
                State.LeafSettings.HueJitter = EditorGUILayout.Slider("Hue Jitter (°)", State.LeafSettings.HueJitter, 0f, 180f);
                EditorGUI.indentLevel--;
            }

            State.LeafMidribFoldout = EditorGUILayout.Foldout(State.LeafMidribFoldout, "Midrib", true);
            if (State.LeafMidribFoldout)
            {
                EditorGUI.indentLevel++;
                State.LeafSettings.MidribEnabled = EditorGUILayout.Toggle("Enabled", State.LeafSettings.MidribEnabled);

                using (new EditorGUI.DisabledScope(!State.LeafSettings.MidribEnabled))
                {
                    State.LeafSettings.MidribWidth = EditorGUILayout.Slider("Width", State.LeafSettings.MidribWidth, 0.001f, 0.2f);
                    State.LeafSettings.MidribGradient = EditorGUILayout.GradientField("Gradient", State.LeafSettings.MidribGradient);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Lateral Veins", EditorStyles.miniLabel);
                    State.LeafSettings.LateralCount = EditorGUILayout.IntSlider("Pairs", State.LeafSettings.LateralCount, 0, 8);
                    PropertyDrawerHelper.DrawMinMaxSliderLayout("Angle (°)", ref State.LeafSettings.LateralAngle, 10f, 80f);
                    State.LeafSettings.LateralWidthRatio = EditorGUILayout.Slider("Width Ratio", State.LeafSettings.LateralWidthRatio, 0.1f, 1f);
                    PropertyDrawerHelper.DrawMinMaxSliderLayout("Length", ref State.LeafSettings.LateralLength, 0.1f, 1.5f);
                    State.LeafSettings.LateralStart = EditorGUILayout.Slider("Start", State.LeafSettings.LateralStart, -1f, 0.5f);
                    State.LeafSettings.LateralCurvature = EditorGUILayout.Slider("Curvature", State.LeafSettings.LateralCurvature, 0f, 1f);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Venules", EditorStyles.miniLabel);
                    State.LeafSettings.LateralSubCount = EditorGUILayout.IntSlider("Per Lateral", State.LeafSettings.LateralSubCount, 0, 4);
                    State.LeafSettings.LateralSubChance = EditorGUILayout.Slider("Survival Chance", State.LeafSettings.LateralSubChance, 0f, 1f);
                    PropertyDrawerHelper.DrawMinMaxSliderLayout("Length", ref State.LeafSettings.LateralSubLength, 0.05f, 1f);
                    State.LeafSettings.SubVeinCurvature = EditorGUILayout.Slider("Curvature", State.LeafSettings.SubVeinCurvature, 0f, 1f);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Reticulate", EditorStyles.miniLabel);
                    State.LeafSettings.ReticulateEnabled = EditorGUILayout.Toggle("Enabled", State.LeafSettings.ReticulateEnabled);
                    using (new EditorGUI.DisabledScope(!State.LeafSettings.ReticulateEnabled))
                    {
                        State.LeafSettings.ReticulateDensity = EditorGUILayout.Slider("Density", State.LeafSettings.ReticulateDensity, 5f, 60f);
                        State.LeafSettings.ReticulateWidth = EditorGUILayout.Slider("Width", State.LeafSettings.ReticulateWidth, 0.01f, 0.5f);
                        State.LeafSettings.ReticulateOpacity = EditorGUILayout.Slider("Opacity", State.LeafSettings.ReticulateOpacity, 0f, 1f);
                        State.LeafSettings.ReticulateAngle = EditorGUILayout.Slider("Angle (°)", State.LeafSettings.ReticulateAngle, 10f, 80f);
                    }
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawPropertiesAndPreview()
        {
            // Measure the properties column height with a throw-away layout pass
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(PropertiesMinWidth), GUILayout.MaxWidth(PropertiesMaxWidth));
            DrawLeafProperties();
            EditorGUILayout.EndVertical();

            // Capture the rect the properties column just occupied
            var propsRect = GUILayoutUtility.GetLastRect();

            // Preview fills everything to the right of the properties column
            var previewX = propsRect.xMax + PreviewBoxPadding;
            var viewWidth = EditorGUIUtility.currentViewWidth - 20f;
            var previewWidth = viewWidth - previewX;

            if (previewWidth < PreviewBoxMinSize)
            {
                return;
            }

            var previewHeight = Mathf.Max(propsRect.height, PreviewBoxMinSize);
            var boxRect = new Rect(previewX, propsRect.y, previewWidth, previewHeight);

            GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

            var labelRect = new Rect(boxRect.x, boxRect.y + 2f, boxRect.width - 28f, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, "Preview", EditorStyles.centeredGreyMiniLabel);

            var diceRect = new Rect(boxRect.xMax - 26f, boxRect.y + 2f, 24f, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(diceRect, "🎲", EditorStyles.miniButton))
            {
                State.PreviewSeed = (uint)Random.Range(1, int.MaxValue);
                State.Save();
                _lastLeafHash = 0;
                Repaint();
            }

            if (_leafLivePreview != null)
            {
                var inner = PadRect(boxRect, PreviewBoxPadding);
                inner.y += EditorGUIUtility.singleLineHeight;
                inner.height -= EditorGUIUtility.singleLineHeight;

                var size = Mathf.Min(inner.width, inner.height);
                var centred = new Rect(
                    inner.x + (inner.width - size) * 0.5f,
                    inner.y + (inner.height - size) * 0.5f,
                    size, size);

                EditorGUI.DrawTextureTransparent(centred, _leafLivePreview, ScaleMode.ScaleToFit);
            }
        }


        private static Rect PadRect(Rect rect, float padding)
        {
            return new Rect(
                rect.x + padding,
                rect.y + padding,
                rect.width - padding * 2f,
                rect.height - padding * 2f);
        }

        private void CheckAutoPreview()
        {
            var leafHash = ComputeLeafSettingsHash();
            if (leafHash != _lastLeafHash)
            {
                _lastLeafHash = leafHash;
                _selectedVariant = -1;
                DestroyLeafLivePreview();
                _leafLivePreview = BushLeafBaker.BakeLeaf(State.LeafSettings, 0, State.PreviewSeed);
                _livePreviewOwned = true;
                Repaint();
            }
        }

        private int ComputeLeafSettingsHash()
        {
            unchecked
            {
                var h = 17;
                h = h * 31 + State.PreviewSeed.GetHashCode();
                h = h * 31 + State.LeafSettings.Resolution.GetHashCode();
                h = h * 31 + State.LeafSettings.LeafRadius.GetHashCode();
                h = h * 31 + State.LeafSettings.GielisM.GetHashCode();
                h = h * 31 + State.LeafSettings.GielisN1.GetHashCode();
                h = h * 31 + State.LeafSettings.GielisN2.GetHashCode();
                h = h * 31 + State.LeafSettings.GielisN3.GetHashCode();
                h = h * 31 + State.LeafSettings.BaseColor.GetHashCode();
                h = h * 31 + State.LeafSettings.EdgeShade.GetHashCode();
                h = h * 31 + State.LeafSettings.HueJitter.GetHashCode();
                h = h * 31 + State.LeafSettings.MidribEnabled.GetHashCode();
                h = h * 31 + State.LeafSettings.MidribWidth.GetHashCode();
                h = h * 31 + GradientHash(State.LeafSettings.MidribGradient);
                h = h * 31 + State.LeafSettings.LateralCount.GetHashCode();
                h = h * 31 + State.LeafSettings.LateralAngle.GetHashCode();
                h = h * 31 + State.LeafSettings.LateralWidthRatio.GetHashCode();
                h = h * 31 + State.LeafSettings.LateralStart.GetHashCode();
                h = h * 31 + State.LeafSettings.LateralLength.GetHashCode();
                h = h * 31 + State.LeafSettings.LateralSubCount.GetHashCode();
                h = h * 31 + State.LeafSettings.LateralSubChance.GetHashCode();
                h = h * 31 + State.LeafSettings.LateralSubLength.GetHashCode();
                h = h * 31 + State.LeafSettings.LateralCurvature.GetHashCode();
                h = h * 31 + State.LeafSettings.SubVeinCurvature.GetHashCode();
                h = h * 31 + State.LeafSettings.ReticulateEnabled.GetHashCode();
                h = h * 31 + State.LeafSettings.ReticulateDensity.GetHashCode();
                h = h * 31 + State.LeafSettings.ReticulateWidth.GetHashCode();
                h = h * 31 + State.LeafSettings.ReticulateOpacity.GetHashCode();
                h = h * 31 + State.LeafSettings.ReticulateAngle.GetHashCode();
                return h;
            }
        }

        private static int GradientHash(Gradient gradient)
        {
            if (gradient == null)
            {
                return 0;
            }

            unchecked
            {
                var h = 17;
                foreach (var key in gradient.colorKeys)
                {
                    h = h * 31 + key.color.GetHashCode();
                    h = h * 31 + key.time.GetHashCode();
                }

                foreach (var key in gradient.alphaKeys)
                {
                    h = h * 31 + key.alpha.GetHashCode();
                    h = h * 31 + key.time.GetHashCode();
                }

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

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("All Variants", EditorStyles.miniLabel);

            var totalWidth = EditorGUIUtility.currentViewWidth - 36f;
            var cols = Mathf.Max(1, Mathf.FloorToInt(totalWidth / (PreviewCellSize + PreviewPadding)));
            var rows = Mathf.CeilToInt((float)_leafPreviews.Length / cols);

            var startRect = GUILayoutUtility.GetRect(
                totalWidth,
                rows * (PreviewCellSize + PreviewPadding));

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

                var labelText = _selectedVariant == i ? $"► #{i}" : $"#{i}";
                EditorGUI.DropShadowLabel(
                    new Rect(rect.x, rect.yMax - 16, rect.width, 16),
                    labelText,
                    EditorStyles.miniLabel);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    _selectedVariant = i;
                    DestroyLeafLivePreview();
                    _leafLivePreview = _leafPreviews[i];
                    _livePreviewOwned = false;
                    Repaint();
                }
            }

            EditorGUILayout.EndVertical();
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

        private void DestroyLeafPreviews()
        {
            if (_leafPreviews == null)
            {
                return;
            }

            if (!_livePreviewOwned)
            {
                _leafLivePreview = null;
                _livePreviewOwned = true;
            }

            _selectedVariant = -1;

            foreach (var tex in _leafPreviews)
            {
                if (tex != null)
                {
                    DestroyImmediate(tex);
                }
            }

            _leafPreviews = null;
        }

        private void DestroyLeafLivePreview()
        {
            if (_leafLivePreview != null && _livePreviewOwned)
            {
                DestroyImmediate(_leafLivePreview);
            }

            _leafLivePreview = null;
            _livePreviewOwned = true;
        }
    }
}
