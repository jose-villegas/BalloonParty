using BalloonParty.Game;
using BalloonParty.Shared.GameState;
using BalloonParty.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Adds the launch screen's Continue button to the Launcher scene: a copy of the Play button under it, with
    ///     Play's press actions (hide, title out, scene activation) kept, its <c>LaunchPlayTrigger.Play</c> calls
    ///     swapped for <see cref="LaunchContinueView.Continue" />, which plays the ascent itself once the resume is
    ///     requested, and the view handed to <see cref="LaunchLifetimeScope" />. Starts inactive: the controller
    ///     shows it only while a run is saved. Running it again does nothing once the scene has a Continue button.
    /// </summary>
    internal static class LaunchContinueButtonBuilder
    {
        private const string ScenePath = "Assets/Scenes/Launcher.unity";
        private const string ButtonName = "Continue";
        private const string LabelTemplate = "continue {0}";

        // Below Play, by this much of Play's height.
        private const float VerticalStep = 1.25f;

        [MenuItem("Tools/BalloonParty/Launch/Add Continue Button")]
        private static void Build()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            if (TryBuild(scene))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (openedHere)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool TryBuild(Scene scene)
        {
            var play = Find<LaunchPlayTrigger>(scene);
            var scope = Find<LaunchLifetimeScope>(scene);
            if (play == null || scope == null)
            {
                Debug.LogError("[LaunchContinueButtonBuilder] The Launcher scene has no LaunchPlayTrigger or LaunchLifetimeScope.");
                return false;
            }

            if (Find<LaunchContinueView>(scene) != null)
            {
                Debug.Log("[LaunchContinueButtonBuilder] The Launcher scene already has a Continue button.");
                return false;
            }

            var playButton = FindPlayButton(scene, play);
            if (playButton == null)
            {
                Debug.LogError("[LaunchContinueButtonBuilder] No button in the Launcher scene calls LaunchPlayTrigger.Play.");
                return false;
            }

            var source = playButton.gameObject;
            var copy = Object.Instantiate(source, source.transform.parent);
            Undo.RegisterCreatedObjectUndo(copy, "Add Continue Button");
            copy.name = ButtonName;
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            if (copy.transform is RectTransform rect && source.transform is RectTransform sourceRect)
            {
                rect.anchoredPosition = sourceRect.anchoredPosition
                    - new Vector2(0f, sourceRect.rect.height * VerticalStep);
            }

            var label = copy.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = LabelTemplate;
            }

            var view = copy.AddComponent<LaunchContinueView>();
            var viewObject = new SerializedObject(view);
            viewObject.FindProperty("_levelLabel").objectReferenceValue = label;
            viewObject.ApplyModifiedPropertiesWithoutUndo();

            RewirePress(copy.GetComponent<ScaleDownButton>(), play, view, source.GetComponent<SceneTransition>());

            var scopeObject = new SerializedObject(scope);
            scopeObject.FindProperty("_continueView").objectReferenceValue = view;
            scopeObject.ApplyModifiedProperties();

            copy.SetActive(false);
            Debug.Log("[LaunchContinueButtonBuilder] Added the Continue button under Play; position and style it as you like.");
            return true;
        }

        // The view requests the resume and then plays the ascent, so Play's own calls come off the copy. The copy's
        // scene activation goes to Play's SceneTransition and the copied one is removed: it would preload a second
        // Game scene the moment the button first shows.
        private static void RewirePress(
            ScaleDownButton button, LaunchPlayTrigger play, LaunchContinueView view, SceneTransition playTransition)
        {
            if (button.OnScaleComplete is not UnityEvent press)
            {
                return;
            }

            var copiedTransition = button.GetComponent<SceneTransition>();
            for (var i = press.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                var target = press.GetPersistentTarget(i);
                if (target == play)
                {
                    UnityEventTools.RemovePersistentListener(press, i);
                }
                else if (copiedTransition != null && target == copiedTransition && playTransition != null)
                {
                    UnityEventTools.RegisterVoidPersistentListener(press, i, playTransition.Load);
                }
            }

            UnityEventTools.AddVoidPersistentListener(press, view.Continue);
            if (copiedTransition != null && playTransition != null)
            {
                Object.DestroyImmediate(copiedTransition);
            }

            EditorUtility.SetDirty(button);
        }

        private static ScaleDownButton FindPlayButton(Scene scene, LaunchPlayTrigger play)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var button in root.GetComponentsInChildren<ScaleDownButton>(true))
                {
                    var press = button.OnScaleComplete;
                    for (var i = 0; i < press.GetPersistentEventCount(); i++)
                    {
                        if (press.GetPersistentTarget(i) == play && press.GetPersistentMethodName(i) == "Play")
                        {
                            return button;
                        }
                    }
                }
            }

            return null;
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
