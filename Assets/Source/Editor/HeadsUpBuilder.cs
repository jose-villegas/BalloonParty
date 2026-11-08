using BalloonParty.UI.HeadsUp;
using BalloonParty.UI.Tooltip;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Builds the heads-ups on the game canvas, each line a copy of the tooltip's pencil label (its font and
    ///     <see cref="WriteOnText" /> included), made small, with a <see cref="HandwrittenHeadsUp" />: the
    ///     cannon's "+1" (<see cref="ReloadHeadsUp" />) and the ball's "+n bounce" / "-1 bounce"
    ///     (<see cref="BounceHeadsUp" />). A starting look to restyle by hand; running one again rebuilds it.
    /// </summary>
    internal static class HeadsUpBuilder
    {
        private const string MenuRoot = "Tools/BalloonParty/HeadsUp/";
        private const string ReloadName = "ReloadHeadsUp";
        private const string BounceName = "BounceHeadsUp";

        // Enough for every ball in the air to bounce at once without one line replacing another.
        private const int BounceLineCount = 6;

        // Canvas units at the 225x400 reference resolution.
        private const float FontSize = 8f;
        private static readonly Vector2 ReloadSize = new(40f, 14f);
        private static readonly Vector2 BounceSize = new(60f, 14f);

        [MenuItem(MenuRoot + "Build Reload Heads-Up")]
        private static void BuildReload()
        {
            PrefabRebuild.Run("Reload heads-up", FindGameCanvas(), ReloadName, BuildReloadHeadsUp);
        }

        [MenuItem(MenuRoot + "Build Bounce Heads-Up")]
        private static void BuildBounce()
        {
            PrefabRebuild.Run("Bounce heads-up", FindGameCanvas(), BounceName, BuildBounceHeadsUp);
        }

        private static string FindGameCanvas()
        {
            return PrefabRebuild.FindPrefabWithChild<TooltipBanner>("GameCanvas");
        }

        private static string BuildReloadHeadsUp(GameObject root)
        {
            var label = FindTooltipLabel(root);
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
            var label = FindTooltipLabel(root);
            if (label == null)
            {
                return MissingLabelError();
            }

            // Fills the canvas, so its lines can go anywhere a ball does.
            var holder = new GameObject(BounceName, typeof(RectTransform));
            var rect = (RectTransform)holder.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var bounce = holder.AddComponent<BounceHeadsUp>();
            var fields = new SerializedObject(bounce);
            var lines = fields.FindProperty("_lines");
            lines.arraySize = BounceLineCount;
            for (var i = 0; i < BounceLineCount; i++)
            {
                lines.GetArrayElementAtIndex(i).objectReferenceValue =
                    CreateLine(label, rect, $"Line {i}", BounceSize);
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }

        private static TMP_Text FindTooltipLabel(GameObject root)
        {
            var banner = root.GetComponentInChildren<TooltipBanner>(true);
            var label = new SerializedObject(banner).FindProperty("_label").objectReferenceValue as TMP_Text;
            return label != null && label.GetComponent<WriteOnText>() != null ? label : null;
        }

        private static string MissingLabelError()
        {
            return "The tooltip's label isn't assigned or has no WriteOnText; the heads-up copies it.";
        }

        private static HandwrittenHeadsUp CreateLine(TMP_Text label, Transform parent, string name, Vector2 size)
        {
            var copy = Object.Instantiate(label.gameObject, parent, false);
            copy.name = name;

            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var text = copy.GetComponent<TMP_Text>();
            text.text = string.Empty;
            text.enableAutoSizing = false;
            text.fontSize = FontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            var group = copy.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var line = copy.AddComponent<HandwrittenHeadsUp>();
            var fields = new SerializedObject(line);
            fields.FindProperty("_group").objectReferenceValue = group;
            fields.FindProperty("_writeOn").objectReferenceValue = copy.GetComponent<WriteOnText>();
            fields.ApplyModifiedPropertiesWithoutUndo();
            return line;
        }
    }
}
