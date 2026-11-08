using BalloonParty.UI.Notices;
using BalloonParty.UI.Tooltip;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Builds the cannon's "+1" ready notice on the game canvas: a copy of the tooltip's pencil label (its font
    ///     and <see cref="WriteOnText" /> included), made small, with a <see cref="HandwrittenNotice" /> and a
    ///     <see cref="ReloadNotice" /> wired to it. A starting look to restyle by hand; running it again
    ///     rebuilds the notice.
    /// </summary>
    internal static class ReloadNoticeBuilder
    {
        private const string MenuPath = "Tools/BalloonParty/Notices/Build Reload Notice";
        private const string DialogTitle = "Reload notice";
        private const string NoticeName = "ReloadNotice";

        // Canvas units at the 225x400 reference resolution.
        private const float FontSize = 8f;
        private static readonly Vector2 Size = new(40f, 14f);

        [MenuItem(MenuPath)]
        private static void Build()
        {
            PrefabRebuild.Run(DialogTitle, PrefabRebuild.FindPrefabWithChild<TooltipBanner>("GameCanvas"),
                NoticeName, BuildNotice);
        }

        private static string BuildNotice(GameObject root)
        {
            var banner = root.GetComponentInChildren<TooltipBanner>(true);
            var label = new SerializedObject(banner).FindProperty("_label").objectReferenceValue as TMP_Text;
            if (label == null || label.GetComponent<WriteOnText>() == null)
            {
                return "The tooltip's label isn't assigned or has no WriteOnText; the notice copies it.";
            }

            var copy = Object.Instantiate(label.gameObject, root.transform, false);
            copy.name = NoticeName;

            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Size;

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

            var notice = copy.AddComponent<HandwrittenNotice>();
            var noticeFields = new SerializedObject(notice);
            noticeFields.FindProperty("_group").objectReferenceValue = group;
            noticeFields.FindProperty("_writeOn").objectReferenceValue = copy.GetComponent<WriteOnText>();
            noticeFields.ApplyModifiedPropertiesWithoutUndo();

            var reload = copy.AddComponent<ReloadNotice>();
            var reloadFields = new SerializedObject(reload);
            reloadFields.FindProperty("_notice").objectReferenceValue = notice;
            reloadFields.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }
    }
}
