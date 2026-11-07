using BalloonParty.Audio.View;
using BalloonParty.UI.LevelUp;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Builds the level-up popup's upgrade card slots: a row of cards under the popup's panel, each a
    ///     copy of the Continue button relabelled as a card, assigned to the popup's card views. A starting
    ///     layout to restyle by hand; running it again rebuilds the row.
    /// </summary>
    internal static class UpgradeCardSlotBuilder
    {
        private const string MenuPath = "Tools/BalloonParty/Level Up/Build Upgrade Card Slots";
        private const string DialogTitle = "Upgrade card slots";
        private const string RowName = "UpgradeCards";
        private const int SlotCount = 3;

        // Canvas units at the 225x400 reference resolution: the panel ends 80 units above the bottom
        // edge, so the row fits in the strip below it.
        private const float RowBottom = 8f;
        private const float RowHeight = 66f;
        private const float RowSideMargin = 10f;
        private const float CardSpacing = 6f;
        private const float CardPadding = 3f;

        [MenuItem(MenuPath)]
        private static void Build()
        {
            var path = FindPopupPrefab();
            if (path == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "No prefab with a LevelUpPopUp on its root was found.", "OK");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var existing = root.transform.Find(RowName);
                if (existing != null)
                {
                    if (!EditorUtility.DisplayDialog(DialogTitle,
                            $"{path} already has upgrade card slots. Rebuild them? Hand edits to the row are lost.",
                            "Rebuild", "Cancel"))
                    {
                        return;
                    }

                    Object.DestroyImmediate(existing.gameObject);
                }

                if (!TryBuild(root, out var error))
                {
                    EditorUtility.DisplayDialog(DialogTitle, error, "OK");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[UpgradeCardSlotBuilder] Built {SlotCount} upgrade card slots on {path}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string FindPopupPrefab()
        {
            foreach (var guid in AssetDatabase.FindAssets("LevelUp t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null && asset.GetComponent<LevelUpPopUp>() != null)
                {
                    return path;
                }
            }

            return null;
        }

        private static bool TryBuild(GameObject root, out string error)
        {
            var popup = new SerializedObject(root.GetComponent<LevelUpPopUp>());
            var continueButton = popup.FindProperty("_continueButton").objectReferenceValue as Button;
            if (continueButton == null || continueButton.GetComponentInChildren<TMP_Text>(true) == null)
            {
                error = "The popup's Continue button (with a text label) isn't assigned; the cards copy its look.";
                return false;
            }

            var row = CreateRow(root.transform);
            var cards = popup.FindProperty("_cardViews");
            cards.arraySize = SlotCount;
            for (var slot = 0; slot < SlotCount; slot++)
            {
                cards.GetArrayElementAtIndex(slot).objectReferenceValue = CreateCard(continueButton, row, slot);
            }

            popup.ApplyModifiedPropertiesWithoutUndo();
            error = null;
            return true;
        }

        private static Transform CreateRow(Transform parent)
        {
            var row = new GameObject(RowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.layer = parent.gameObject.layer;

            var rect = (RectTransform)row.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.right;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, RowBottom);
            rect.sizeDelta = new Vector2(-2f * RowSideMargin, RowHeight);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = CardSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return rect;
        }

        private static UpgradeCardView CreateCard(Button continueButton, Transform row, int slot)
        {
            var card = Object.Instantiate(continueButton.gameObject, row, false);
            card.name = $"Card {slot + 1}";

            var button = card.GetComponent<Button>();
            // The copy carries Continue's persistent OnContinue call; a card reports its slot instead.
            button.onClick = new Button.ButtonClickedEvent();

            // The pick's dismissal already plays the confirm sound, so a click sound here would double it.
            if (card.TryGetComponent<UiSfxEmitter>(out var emitter))
            {
                Object.DestroyImmediate(emitter);
            }

            var title = card.GetComponentInChildren<TMP_Text>(true);
            title.name = "Title";
            var description = Object.Instantiate(title, card.transform, false);
            description.name = "Description";
            var stacks = Object.Instantiate(title, card.transform, false);
            stacks.name = "Stacks";

            StyleLabel(title, new Vector2(0f, 0.62f), Vector2.one, 6f, 10f, "Card");
            StyleLabel(description, new Vector2(0f, 0.2f), new Vector2(1f, 0.62f), 4f, 7f, "What the card does");
            StyleLabel(stacks, Vector2.zero, new Vector2(1f, 0.2f), 4f, 6f, "0/3");

            var view = card.AddComponent<UpgradeCardView>();
            var fields = new SerializedObject(view);
            fields.FindProperty("_button").objectReferenceValue = button;
            fields.FindProperty("_titleLabel").objectReferenceValue = title;
            fields.FindProperty("_descriptionLabel").objectReferenceValue = description;
            fields.FindProperty("_stacksLabel").objectReferenceValue = stacks;
            fields.ApplyModifiedPropertiesWithoutUndo();

            // The popup shows a card only once its fill completes.
            card.SetActive(false);
            return view;
        }

        private static void StyleLabel(TMP_Text label, Vector2 anchorMin, Vector2 anchorMax, float minSize,
            float maxSize, string placeholder)
        {
            var rect = label.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(CardPadding, 0f);
            rect.offsetMax = new Vector2(-CardPadding, 0f);

            label.text = placeholder;
            label.enableAutoSizing = true;
            label.fontSizeMin = minSize;
            label.fontSizeMax = maxSize;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.Center;
        }
    }
}
