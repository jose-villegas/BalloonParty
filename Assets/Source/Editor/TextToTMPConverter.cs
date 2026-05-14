using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.Editor
{
    public static class TextToTMPConverter
    {
        [MenuItem("Tools/BalloonParty/Convert Text → TMP (Selection + Children)")]
        private static void ConvertSelection()
        {
            var found = CollectFromSelection();

            if (found.Count == 0)
            {
                EditorUtility.DisplayDialog("Convert Text → TMP",
                    "No legacy Text components found in the current selection.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Convert Text → TMP");
            var group = Undo.GetCurrentGroup();

            var converted = 0;
            foreach (var text in found)
            {
                ConvertSingle(text);
                converted++;
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[TextToTMP] Converted {converted} component(s). " +
                      "Font assets must be re-assigned manually — TMP uses TMP_FontAsset, not Unity Font.");
        }

        [MenuItem("Tools/BalloonParty/Convert Text → TMP (Selection + Children)", validate = true)]
        private static bool ValidateConvertSelection() => Selection.gameObjects.Length > 0;

        private static List<Text> CollectFromSelection()
        {
            var result = new List<Text>();
            foreach (var go in Selection.gameObjects)
            {
                result.AddRange(go.GetComponentsInChildren<Text>(true));
            }

            return result;
        }

        private static void ConvertSingle(Text source)
        {
            var go = source.gameObject;

            var content    = source.text;
            var fontSize   = source.fontSize;
            var color      = source.color;
            var alignment  = MapAlignment(source.alignment);
            var fontStyle  = MapFontStyle(source.fontStyle);
            var raycast    = source.raycastTarget;
            var autoSize   = source.resizeTextForBestFit;
            var minSize    = source.resizeTextMinSize;
            var maxSize    = source.resizeTextMaxSize;
            var wrapping   = source.horizontalOverflow == HorizontalWrapMode.Wrap;
            var wasEnabled = source.enabled;

            Undo.DestroyObjectImmediate(source);

            var tmp = Undo.AddComponent<TextMeshProUGUI>(go);
            tmp.text             = content;
            tmp.fontSize         = fontSize;
            tmp.color            = color;
            tmp.alignment        = alignment;
            tmp.fontStyle        = fontStyle;
            tmp.raycastTarget    = raycast;
            tmp.enableAutoSizing = autoSize;
            tmp.fontSizeMin      = minSize;
            tmp.fontSizeMax      = maxSize;
            tmp.enableWordWrapping = wrapping;
            tmp.enabled          = wasEnabled;

            EditorUtility.SetDirty(go);
        }

        private static TextAlignmentOptions MapAlignment(TextAnchor anchor) => anchor switch
        {
            TextAnchor.UpperLeft    => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter  => TextAlignmentOptions.Top,
            TextAnchor.UpperRight   => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft   => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight  => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft    => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter  => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight   => TextAlignmentOptions.BottomRight,
            _                       => TextAlignmentOptions.Center,
        };

        private static FontStyles MapFontStyle(FontStyle style) => style switch
        {
            FontStyle.Bold          => FontStyles.Bold,
            FontStyle.Italic        => FontStyles.Italic,
            FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
            _                       => FontStyles.Normal,
        };
    }
}

