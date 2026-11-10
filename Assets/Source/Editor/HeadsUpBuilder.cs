using BalloonParty.UI.HeadsUp;
using BalloonParty.UI.Tooltip;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Builds the <see cref="HeadsUpLayer" /> in world space, on the <c>HeadsUps</c> prefab the Game scene
    ///     holds, with the one line it clones every heads-up from: a <c>TextMeshPro</c> in the tooltip's pencil
    ///     font with its <see cref="WriteOnText" /> settings and a <see cref="HandwrittenHeadsUp" />. Not on the
    ///     game canvas, which is bound to the camera: in world space a line stays where it happened through the
    ///     camera rig's zoom and shake. A starting look to restyle by hand; running it again rebuilds it, and
    ///     removes the single-purpose heads-ups (the reload and the bounce ones) that earlier builds left, here
    ///     or on <c>GameCanvas</c>.
    /// </summary>
    internal static class HeadsUpBuilder
    {
        private const string MenuRoot = "Tools/BalloonParty/HeadsUp/";
        private const string PrefabPath = "Assets/Prefabs/Game/HeadsUps.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string RootName = "HeadsUps";
        private const string LayerName = "HeadsUpLayer";
        private const string LineName = "Line";
        private const string TipName = "PencilTip";
        private const string TipSpritePath = "Assets/Sprites/UI/pencil-tip.png";

        // Above the balloons, the ball and the score trails, like the canvas lines were.
        private const string SortingLayer = "UI";

        // The pencil is held over the paper: above the line it's writing.
        private const int TipSortingOrder = 1;

        // The canvas build's sizes, in world units: the camera shows the reference 9.6 world units over the
        // canvas's 400, so a canvas unit is 0.024. A TextMeshPro (3D) draws a tenth of its font size per world
        // unit where the canvas text drew one per canvas unit, hence the 10.
        private const float WorldPerCanvasUnit = 0.024f;
        private const float FontSize = 8f * WorldPerCanvasUnit * 10f;
        private static readonly Vector2 LineSize = new Vector2(60f, 14f) * WorldPerCanvasUnit;

        // What the layer replaced: one view per kind of heads-up, each with its own lines.
        private static readonly string[] Retired = { "ReloadHeadsUp", "BounceHeadsUp" };

        [MenuItem(MenuRoot + "Build Heads-Up Layer")]
        private static void BuildLayer()
        {
            EnsurePrefab();
            if (!PrefabRebuild.Run("Heads-up layer", PrefabPath, LayerName, BuildHeadsUpLayer))
            {
                return;
            }

            foreach (var retired in Retired)
            {
                RemoveFromGameCanvas(retired);
            }

            EnsureSceneInstance();
        }

        private static void EnsurePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                return;
            }

            var root = new GameObject(RootName) { layer = LayerMask.NameToLayer("UI") };
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        // The lines used to sit on the canvas; a rebuild there would leave both writing.
        private static void RemoveFromGameCanvas(string childName)
        {
            var path = FindGameCanvas();
            if (path == null)
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var legacy = root.transform.Find(childName);
                if (legacy == null)
                {
                    return;
                }

                Object.DestroyImmediate(legacy.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[HeadsUpBuilder] Removed the canvas {childName} from {path}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureSceneInstance()
        {
            var scene = SceneManager.GetSceneByPath(GameScenePath);
            if (!scene.isLoaded)
            {
                Debug.LogWarning($"[HeadsUpBuilder] Open {GameScenePath} and build again to place {RootName} in it.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab)
                {
                    return;
                }
            }

            PrefabUtility.InstantiatePrefab(prefab, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[HeadsUpBuilder] Placed {RootName} in {GameScenePath}; save the scene.");
        }

        private static string FindGameCanvas()
        {
            return PrefabRebuild.FindPrefabWithChild<TooltipBanner>("GameCanvas");
        }

        private static string BuildHeadsUpLayer(GameObject root)
        {
            var label = FindTooltipLabel();
            if (label == null)
            {
                return MissingLabelError();
            }

            foreach (var retired in Retired)
            {
                var old = root.transform.Find(retired);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            var holder = new GameObject(LayerName) { layer = root.layer };
            holder.transform.SetParent(root.transform, false);
            var layer = holder.AddComponent<HeadsUpLayer>();

            // The pool's original: cloned, never shown itself.
            var line = CreateLine(label, holder.transform, LineName, LineSize);
            line.gameObject.SetActive(false);

            var fields = new SerializedObject(layer);
            fields.FindProperty("_linePrefab").objectReferenceValue = line;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }

        private static TMP_Text FindTooltipLabel()
        {
            var path = FindGameCanvas();
            var canvas = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            var banner = canvas != null ? canvas.GetComponentInChildren<TooltipBanner>(true) : null;
            if (banner == null)
            {
                return null;
            }

            var label = new SerializedObject(banner).FindProperty("_label").objectReferenceValue as TMP_Text;
            return label != null && label.GetComponent<WriteOnText>() != null ? label : null;
        }

        private static string MissingLabelError()
        {
            return "The tooltip's label isn't assigned or has no WriteOnText; the heads-up takes its font and write-on.";
        }

        private static HandwrittenHeadsUp CreateLine(TMP_Text label, Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            var text = go.AddComponent<TextMeshPro>();
            text.font = label.font;
            text.fontSharedMaterial = label.fontSharedMaterial;
            text.color = label.color;
            text.fontStyle = label.fontStyle;
            text.text = string.Empty;
            text.enableAutoSizing = false;
            text.fontSize = FontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            go.GetComponent<MeshRenderer>().sortingLayerName = SortingLayer;

            // The tooltip's reveal timing, pointed at this label and at its own pencil tip.
            var writeOn = go.AddComponent<WriteOnText>();
            EditorUtility.CopySerialized(label.GetComponent<WriteOnText>(), writeOn);
            var writeOnFields = new SerializedObject(writeOn);
            writeOnFields.FindProperty("_text").objectReferenceValue = text;
            writeOnFields.FindProperty("_tip").objectReferenceValue = CreateTip(go.transform);
            writeOnFields.ApplyModifiedPropertiesWithoutUndo();

            var line = go.AddComponent<HandwrittenHeadsUp>();
            var fields = new SerializedObject(line);
            fields.FindProperty("_writeOn").objectReferenceValue = writeOn;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return line;
        }

        // Its pivot is the graphite point, which WriteOnText puts on the reveal edge. Hidden until a write.
        private static Transform CreateTip(Transform line)
        {
            var go = new GameObject(TipName) { layer = line.gameObject.layer };
            go.transform.SetParent(line, false);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TipSpritePath);
            if (sprite.sprite == null)
            {
                Debug.LogError($"[HeadsUp] No pencil tip sprite at {TipSpritePath}; the heads-ups write without one.");
            }

            sprite.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            sprite.sortingLayerName = SortingLayer;
            sprite.sortingOrder = TipSortingOrder;
            go.SetActive(false);
            return go.transform;
        }
    }
}
