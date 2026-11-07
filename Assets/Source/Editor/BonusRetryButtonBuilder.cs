using BalloonParty.UI.GameOver;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Builds the game-over screen's bonus retry button: a copy of its Retry button, in the strip under
    ///     the panel, wired to <see cref="GameOverScreen.OnBonusRetryPressed" /> and assigned to the screen.
    ///     A starting layout to restyle by hand; running it again rebuilds the button.
    /// </summary>
    internal static class BonusRetryButtonBuilder
    {
        private const string MenuPath = "Tools/BalloonParty/Game Over/Build Bonus Retry Button";
        private const string DialogTitle = "Bonus retry button";
        private const string ButtonName = "BonusRetry";
        private const string Label = "Retry + bonus card (ad)";

        // Canvas units at the 225x400 reference resolution: the panel ends 80 units above the bottom
        // edge, so the button fits in the strip below it.
        private const float ButtonBottom = 40f;
        private const float ButtonHeight = 30f;
        private const float ButtonSideMargin = 30f;

        [MenuItem(MenuPath)]
        private static void Build()
        {
            PrefabRebuild.Run(DialogTitle, PrefabRebuild.FindPrefabWithRoot<GameOverScreen>("GameOver"), ButtonName,
                BuildButton);
        }

        private static string BuildButton(GameObject root)
        {
            var screen = root.GetComponent<GameOverScreen>();
            var fields = new SerializedObject(screen);
            var retryButton = fields.FindProperty("_retryButton").objectReferenceValue as Button;
            if (retryButton == null)
            {
                return "The screen's Retry button isn't assigned; the bonus button copies its look.";
            }

            var copy = Object.Instantiate(retryButton.gameObject, root.transform, false);
            copy.name = ButtonName;

            var rect = (RectTransform)copy.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.right;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, ButtonBottom);
            rect.sizeDelta = new Vector2(-2f * ButtonSideMargin, ButtonHeight);

            // The copy carries Retry's persistent OnRetryPressed call; this one buys the bonus retry instead.
            var button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(button.onClick, screen.OnBonusRetryPressed);

            var label = copy.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = Label;
                label.enableAutoSizing = true;
            }

            fields.FindProperty("_bonusRetryButton").objectReferenceValue = button;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }
    }
}
