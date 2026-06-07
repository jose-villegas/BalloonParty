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
        private Texture2D _leafLivePreview;
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

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.MinWidth(PropertiesMinWidth), GUILayout.MaxWidth(PropertiesMaxWidth));
            DrawLeafProperties();
            EditorGUILayout.EndVertical();

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

            State.LeafSurfaceFoldout = EditorGUILayout.Foldout(State.LeafSurfaceFoldout, "Surface", true);
            if (State.LeafSurfaceFoldout)
            {
                EditorGUI.indentLevel++;
                State.LeafSettings.BaseColor = EditorGUILayout.ColorField("Base Color", State.LeafSettings.BaseColor);
                State.LeafSettings.EdgeShade = EditorGUILayout.Slider("Edge Shade", State.LeafSettings.EdgeShade, 0.4f, 1f);
                State.LeafSettings.HueJitter = EditorGUILayout.Slider("Hue Jitter (°)", State.LeafSettings.HueJitter, 0f, 180f);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawLeafLivePreview()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LivePreviewSize + 8f), GUILayout.ExpandWidth(false));

            if (_leafLivePreview != null)
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.centeredGreyMiniLabel);
                var rect = GUILayoutUtility.GetRect(LivePreviewSize, LivePreviewSize);
                rect.width = LivePreviewSize;
                EditorGUI.DrawTextureTransparent(rect, _leafLivePreview, ScaleMode.ScaleToFit);
            }

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
                h = h * 31 + State.LeafSettings.HueJitter.GetHashCode();
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
            if (_leafLivePreview != null)
            {
                DestroyImmediate(_leafLivePreview);
                _leafLivePreview = null;
            }
        }
    }
}
