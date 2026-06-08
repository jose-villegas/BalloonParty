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

        private Texture2D _branchPreview;
        private Texture2D _branchRawMap;
        private int _lastBranchHash;
        private bool _showRuntimePreview = true;

        private readonly TexturePreviewBox _branchPreviewBox = new("Branch Preview");
        private readonly TexturePreviewBox _leafPreviewBox = new("Leaf Preview");

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
            DestroyBranchPreview();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUI.BeginChangeCheck();

            DrawSharedSettings();
            EditorGUILayout.Space(16);
            DrawBranchSection();
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
                CheckBranchAutoPreview();
            }
        }

        private void DrawSharedSettings()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            State.AutoPreview = EditorGUILayout.Toggle("Live Preview", State.AutoPreview);
            EditorGUI.indentLevel--;
        }

        private void DrawBranchSection()
        {
            State.BranchFoldout = EditorGUILayout.Foldout(
                State.BranchFoldout, "Branch Map", true, EditorStyles.foldoutHeader);

            if (!State.BranchFoldout)
            {
                return;
            }

            EditorGUI.indentLevel++;

            DrawBranchPropertiesAndPreview();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Preview Branch Map", GUILayout.Height(28)))
            {
                GenerateBranchPreview();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        private void DrawBranchProperties()
        {
            var s = State.BranchSettings;

            s.Resolution = EditorGUILayout.IntPopup(
                "Resolution", s.Resolution,
                new[] { "64", "128", "256", "512" },
                new[] { 64, 128, 256, 512 });
            s.Variants = EditorGUILayout.IntSlider("Variants", s.Variants, 1, 16);

            EditorGUILayout.Space(4);

            State.BranchShapeFoldout = EditorGUILayout.Foldout(
                State.BranchShapeFoldout, "Fractal Shape", true);
            if (State.BranchShapeFoldout)
            {
                EditorGUI.indentLevel++;
                s.MaxDepth = EditorGUILayout.IntSlider("Max Depth", s.MaxDepth, 1, 6);
                s.BranchesPerNode = EditorGUILayout.IntSlider("Branches Per Node", s.BranchesPerNode, 2, 5);
                PropertyDrawerHelper.DrawMinMaxSliderLayout("Angle Spread (°)", ref s.AngleSpread, 5f, 90f);
                PropertyDrawerHelper.DrawMinMaxSliderLayout("Length", ref s.LengthRange, 0.05f, 0.6f);
                s.LengthDecay = EditorGUILayout.Slider("Length Decay", s.LengthDecay, 0.3f, 0.95f);
                s.TrunkLength = EditorGUILayout.Slider("Trunk Length", s.TrunkLength, 0.02f, 0.3f);
                s.BranchWidth = EditorGUILayout.Slider("Width", s.BranchWidth, 0.005f, 0.06f);
                s.WidthDecay = EditorGUILayout.Slider("Width Decay", s.WidthDecay, 0.3f, 0.9f);
                s.TipTaper = EditorGUILayout.Slider("Tip Taper", s.TipTaper, 0.1f, 0.9f);
                EditorGUI.indentLevel--;
            }

            State.BranchVisualFoldout = EditorGUILayout.Foldout(
                State.BranchVisualFoldout, "Visual", true);
            if (State.BranchVisualFoldout)
            {
                EditorGUI.indentLevel++;
                s.BranchColor = EditorGUILayout.ColorField("Color", s.BranchColor);
                s.ColorVariation = EditorGUILayout.Slider("Color Variation", s.ColorVariation, 0f, 0.3f);
                EditorGUI.indentLevel--;
            }

            State.BranchLeafFoldout = EditorGUILayout.Foldout(
                State.BranchLeafFoldout, "Leaf Placement", true);
            if (State.BranchLeafFoldout)
            {
                EditorGUI.indentLevel++;
                s.LeafDepthThreshold = EditorGUILayout.Slider("Depth Threshold", s.LeafDepthThreshold, 0.1f, 1f);
                s.MaxLeavesPerVariant = EditorGUILayout.IntSlider("Max Leaves", s.MaxLeavesPerVariant, 4, 32);
                s.LeafScale = EditorGUILayout.Slider("Leaf Scale", s.LeafScale, 0.02f, 0.2f);
                s.LeafScaleVariation = EditorGUILayout.Slider("Scale Variation", s.LeafScaleVariation, 0f, 0.8f);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawBranchPropertiesAndPreview()
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(PropertiesMinWidth), GUILayout.MaxWidth(PropertiesMaxWidth));
            DrawBranchProperties();
            EditorGUILayout.EndVertical();

            var propsRect = GUILayoutUtility.GetLastRect();

            var previewX = propsRect.xMax + PreviewBoxPadding;
            var viewWidth = EditorGUIUtility.currentViewWidth - 20f;
            var previewWidth = viewWidth - previewX;

            if (previewWidth < PreviewBoxMinSize)
            {
                return;
            }

            var previewHeight = Mathf.Max(propsRect.height, PreviewBoxMinSize);
            var boxRect = new Rect(previewX, propsRect.y, previewWidth, previewHeight);

            _branchPreviewBox.Draw(boxRect, _branchPreview, DrawBranchToolbarExtras);
        }

        private float DrawBranchToolbarExtras(Rect boxRect, float rightEdge)
        {
            var y = boxRect.y + 2f;

            // Dice: randomise seed
            rightEdge = TexturePreviewBox.DrawToolbarButton(rightEdge, y, "🎲", 26f, () =>
            {
                State.PreviewSeed = (uint)Random.Range(1, int.MaxValue);
                State.Save();
                _lastBranchHash = 0;
                _lastLeafHash = 0;
                Repaint();
            });

            // Map/Visual toggle
            var toggleLabel = _showRuntimePreview ? "🌿" : "🗺";
            rightEdge = TexturePreviewBox.DrawToolbarButton(rightEdge, y, toggleLabel, 28f, () =>
            {
                _showRuntimePreview = !_showRuntimePreview;
                RebuildBranchDisplayTexture();
                Repaint();
            });

            return rightEdge;
        }

        private void GenerateBranchPreview()
        {
            DestroyBranchPreview();
            _branchRawMap = BushBranchBaker.Bake((int)State.PreviewSeed, State.BranchSettings);
            RebuildBranchDisplayTexture();
            _lastBranchHash = ComputeBranchSettingsHash();
            Repaint();
        }

        private void CheckBranchAutoPreview()
        {
            if (!State.BranchFoldout)
            {
                return;
            }

            var hash = ComputeBranchSettingsHash();
            if (hash != _lastBranchHash)
            {
                _lastBranchHash = hash;
                DestroyBranchPreview();
                _branchRawMap = BushBranchBaker.Bake((int)State.PreviewSeed, State.BranchSettings);
                RebuildBranchDisplayTexture();
                Repaint();
            }
        }

        private void RebuildBranchDisplayTexture()
        {
            if (_branchPreview != null && _branchPreview != _branchRawMap)
            {
                DestroyImmediate(_branchPreview);
            }

            _branchPreview = null;

            if (_branchRawMap == null)
            {
                return;
            }

            if (!_showRuntimePreview)
            {
                _branchPreview = _branchRawMap;
                return;
            }

            // Build a runtime-style preview: apply branch color × depth shading
            var pixels = _branchRawMap.GetPixels32();
            var branchColor = State.BranchSettings.BranchColor;
            var result = new Color32[pixels.Length];

            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                var alpha = p.a / 255f;
                if (alpha < 0.01f)
                {
                    result[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // Same formula as BushBranch.shader: color * (0.6 + 0.4 * depth)
                var shade = 0.6f + 0.4f * alpha;
                var r = (byte)Mathf.Clamp(branchColor.r * shade * 255f, 0f, 255f);
                var g = (byte)Mathf.Clamp(branchColor.g * shade * 255f, 0f, 255f);
                var b = (byte)Mathf.Clamp(branchColor.b * shade * 255f, 0f, 255f);
                var a = (byte)Mathf.Clamp(alpha * 255f, 0f, 255f);
                result[i] = new Color32(r, g, b, a);
            }

            var res = _branchRawMap.width;
            _branchPreview = new Texture2D(res, res, TextureFormat.RGBA32, false);
            _branchPreview.SetPixels32(result);
            _branchPreview.Apply();
        }

        private int ComputeBranchSettingsHash()
        {
            unchecked
            {
                var h = 17;
                h = h * 31 + State.PreviewSeed.GetHashCode();
                var s = State.BranchSettings;
                h = h * 31 + s.Resolution.GetHashCode();
                h = h * 31 + s.MaxDepth.GetHashCode();
                h = h * 31 + s.BranchesPerNode.GetHashCode();
                h = h * 31 + s.AngleSpread.GetHashCode();
                h = h * 31 + s.LengthRange.GetHashCode();
                h = h * 31 + s.LengthDecay.GetHashCode();
                h = h * 31 + s.TrunkLength.GetHashCode();
                h = h * 31 + s.BranchWidth.GetHashCode();
                h = h * 31 + s.WidthDecay.GetHashCode();
                h = h * 31 + s.TipTaper.GetHashCode();
                h = h * 31 + s.BranchColor.GetHashCode();
                h = h * 31 + s.ColorVariation.GetHashCode();
                return h;
            }
        }

        private void DestroyBranchPreview()
        {
            if (_branchPreview != null && _branchPreview != _branchRawMap)
            {
                DestroyImmediate(_branchPreview);
            }

            _branchPreview = null;

            if (_branchRawMap != null)
            {
                DestroyImmediate(_branchRawMap);
                _branchRawMap = null;
            }
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
            State.LeafSettings.LeafVariants = EditorGUILayout.IntSlider("Variants", State.LeafSettings.LeafVariants, 1, 64);

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

            State.LeafPetioleFoldout = EditorGUILayout.Foldout(State.LeafPetioleFoldout, "Petiole", true);
            if (State.LeafPetioleFoldout)
            {
                EditorGUI.indentLevel++;
                State.LeafSettings.PetioleEnabled = EditorGUILayout.Toggle("Enabled", State.LeafSettings.PetioleEnabled);

                using (new EditorGUI.DisabledScope(!State.LeafSettings.PetioleEnabled))
                {
                    State.LeafSettings.PetioleLength = EditorGUILayout.Slider("Length", State.LeafSettings.PetioleLength, 0.01f, 0.5f);
                    State.LeafSettings.PetioleWidth = EditorGUILayout.Slider("Width", State.LeafSettings.PetioleWidth, 0.002f, 0.05f);
                    State.LeafSettings.PetioleTaper = EditorGUILayout.Slider("Taper", State.LeafSettings.PetioleTaper, -1f, 1f);
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

            _leafPreviewBox.Draw(boxRect, _leafLivePreview, DrawLeafToolbarExtras);
        }

        private float DrawLeafToolbarExtras(Rect boxRect, float rightEdge)
        {
            var y = boxRect.y + 2f;

            // Dice: randomise seed
            rightEdge = TexturePreviewBox.DrawToolbarButton(rightEdge, y, "🎲", 26f, () =>
            {
                State.PreviewSeed = (uint)Random.Range(1, int.MaxValue);
                State.Save();
                _lastLeafHash = 0;
                Repaint();
            });

            return rightEdge;
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
                h = h * 31 + State.LeafSettings.PetioleEnabled.GetHashCode();
                h = h * 31 + State.LeafSettings.PetioleLength.GetHashCode();
                h = h * 31 + State.LeafSettings.PetioleWidth.GetHashCode();
                h = h * 31 + State.LeafSettings.PetioleTaper.GetHashCode();
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
