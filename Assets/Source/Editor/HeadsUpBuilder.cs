using System;
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
    ///     Builds the heads-ups in world space, on the <c>HeadsUps</c> prefab the Game scene holds, each line a
    ///     <c>TextMeshPro</c> in the tooltip's pencil font with its <see cref="WriteOnText" /> settings and a
    ///     <see cref="HandwrittenHeadsUp" />: the cannon's "+1" (<see cref="ReloadHeadsUp" />) and the ball's
    ///     "+n bounce" / "-1 bounce" (<see cref="BounceHeadsUp" />). Not on the game canvas, which is bound to the
    ///     camera: in world space a line stays where it happened through the camera rig's zoom and shake. A
    ///     starting look to restyle by hand; running one again rebuilds it, and removes the canvas lines an
    ///     earlier build left on <c>GameCanvas</c>.
    /// </summary>
    internal static class HeadsUpBuilder
    {
        private const string MenuRoot = "Tools/BalloonParty/HeadsUp/";
        private const string PrefabPath = "Assets/Prefabs/Game/HeadsUps.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string RootName = "HeadsUps";
        private const string ReloadName = "ReloadHeadsUp";
        private const string BounceName = "BounceHeadsUp";
        private const string TipName = "PencilTip";
        private const string TipSpritePath = "Assets/Sprites/UI/pencil-tip.png";

        // Above the balloons, the ball and the score trails, like the canvas lines were.
        private const string SortingLayer = "UI";

        // The pencil is held over the paper: above the line it's writing.
        private const int TipSortingOrder = 1;

        // Enough for every ball in the air to bounce at once without one line replacing another.
        private const int BounceLineCount = 6;

        // The canvas build's sizes, in world units: the camera shows the reference 9.6 world units over the
        // canvas's 400, so a canvas unit is 0.024. A TextMeshPro (3D) draws a tenth of its font size per world
        // unit where the canvas text drew one per canvas unit, hence the 10.
        private const float WorldPerCanvasUnit = 0.024f;
        private const float FontSize = 8f * WorldPerCanvasUnit * 10f;
        private static readonly Vector2 ReloadSize = new Vector2(40f, 14f) * WorldPerCanvasUnit;
        private static readonly Vector2 BounceSize = new Vector2(60f, 14f) * WorldPerCanvasUnit;

        [MenuItem(MenuRoot + "Build Reload Heads-Up")]
        private static void BuildReload()
        {
            Build("Reload heads-up", ReloadName, BuildReloadHeadsUp);
        }

        [MenuItem(MenuRoot + "Build Bounce Heads-Up")]
        private static void BuildBounce()
        {
            Build("Bounce heads-up", BounceName, BuildBounceHeadsUp);
        }

        private static void Build(string title, string childName, Func<GameObject, string> build)
        {
            EnsurePrefab();
            // Only once the world line is saved: a cancelled or failed build keeps the canvas line it would replace.
            if (!PrefabRebuild.Run(title, PrefabPath, childName, build))
            {
                return;
            }

            RemoveFromGameCanvas(childName);
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

        private static string BuildReloadHeadsUp(GameObject root)
        {
            var label = FindTooltipLabel();
            if (label == null)
            {
                return MissingLabelError();
            }

            var line = CreateLine(label, root.transform, ReloadName, ReloadSize);
            var reload = line.gameObject.AddComponent<ReloadHeadsUp>();
            var fields = new SerializedObject(reload);
            fields.FindProperty("_headsUp").objectReferenceValue = line;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }

        private static string BuildBounceHeadsUp(GameObject root)
        {
            var label = FindTooltipLabel();
            if (label == null)
            {
                return MissingLabelError();
            }

            var holder = new GameObject(BounceName) { layer = root.layer };
            holder.transform.SetParent(root.transform, false);

            var bounce = holder.AddComponent<BounceHeadsUp>();
            var fields = new SerializedObject(bounce);
            var lines = fields.FindProperty("_lines");
            lines.arraySize = BounceLineCount;
            for (var i = 0; i < BounceLineCount; i++)
            {
                lines.GetArrayElementAtIndex(i).objectReferenceValue =
                    CreateLine(label, holder.transform, $"Line {i}", BounceSize);
            }

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
