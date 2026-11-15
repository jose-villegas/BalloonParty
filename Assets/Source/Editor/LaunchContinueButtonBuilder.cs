using BalloonParty.Game;
using BalloonParty.Shared.GameState;
using BalloonParty.UI;
using BalloonParty.UI.ClothRibbon;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Builds the launch screen's Continue in the Launcher scene, replacing any built before: a root stretched over
    ///     the launch canvas holding <see cref="LaunchContinueView" />, the spring line, the cloth ribbon with
    ///     "continue" on both faces, and a small white copy of the Play balloon. The copy keeps Play's press actions
    ///     (hide, title out, scene activation), with Play's <c>LaunchPlayTrigger.Play</c> swapped for
    ///     <see cref="LaunchContinueView.Continue" />, which plays the ascent itself once the resume is requested; its
    ///     hide hides the whole Continue, and its scene activation is Play's own. Starts inactive: the controller shows
    ///     it only while a run is saved.
    /// </summary>
    internal static class LaunchContinueButtonBuilder
    {
        private const string ScenePath = "Assets/Scenes/Launcher.unity";
        private const string RibbonMaterialPath = "Assets/Materials/UI/CardRevealRibbon.mat";
        private const string FontPath = "Assets/Fonts/PatrickHand-Regular SDF.asset";
        private const string Label = "continue";
        private const float BalloonScale = 0.36f;
        private const float FontSize = 9.5f;

        private static readonly Color White = new(0.97f, 0.97f, 0.98f, 1f);

        // The body art is a flat silhouette; the balloon's depth is all in its specular glows. A white body under
        // them would hide them, so it's a pale cool grey and the glows pure white, standing out as Play's cream
        // glows do on its blue.
        private static readonly Color BodyColor = new(0.8f, 0.82f, 0.87f, 1f);
        private static readonly Color RibbonColor = new(0.98f, 0.965f, 0.925f, 1f);
        private static readonly Color InkColor = new(0.2f, 0.2f, 0.2f, 1f);

        [MenuItem("Tools/BalloonParty/Launch/Build Continue Balloon")]
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

            var playButton = FindPlayButton(scene, play);
            if (playButton == null)
            {
                Debug.LogError("[LaunchContinueButtonBuilder] No button in the Launcher scene calls LaunchPlayTrigger.Play.");
                return false;
            }

            var built = Find<LaunchContinueView>(scene);
            if (built != null)
            {
                Undo.DestroyObjectImmediate(built.gameObject);
            }

            var source = playButton.gameObject;
            var root = NewChild("Continue", source.transform.parent);
            root.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(root, "Build Continue Balloon");
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var line = NewChild("Line", root.transform).AddComponent<SpringLineGraphic>();
            line.color = White;
            line.raycastTarget = false;

            var ribbon = NewChild("Ribbon", root.transform).AddComponent<ClothRibbonGraphic>();
            ribbon.material = AssetDatabase.LoadAssetAtPath<Material>(RibbonMaterialPath);
            ribbon.color = RibbonColor;
            ribbon.raycastTarget = false;
            var writing = ribbon.gameObject.AddComponent<ClothRibbonWriting>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var front = NewText("Front", ribbon.transform, font);
            var back = NewText("Back", ribbon.transform, font);
            var writingObject = new SerializedObject(writing);
            writingObject.FindProperty("_front").objectReferenceValue = front;
            writingObject.FindProperty("_back").objectReferenceValue = back;
            writingObject.ApplyModifiedPropertiesWithoutUndo();

            var balloon = Object.Instantiate(source, root.transform);
            balloon.name = "Balloon";
            var balloonRect = (RectTransform)balloon.transform;
            balloonRect.anchorMin = balloonRect.anchorMax = new Vector2(0.5f, 0.5f);
            balloonRect.anchoredPosition = Vector2.zero;
            balloonRect.localScale = new Vector3(BalloonScale, BalloonScale, 1f);
            Recolor(balloon);

            var view = root.AddComponent<LaunchContinueView>();
            var viewObject = new SerializedObject(view);
            viewObject.FindProperty("_balloon").objectReferenceValue = balloonRect;
            viewObject.FindProperty("_play").objectReferenceValue = source.transform;
            viewObject.FindProperty("_line").objectReferenceValue = line;
            viewObject.FindProperty("_ribbon").objectReferenceValue = ribbon;
            viewObject.FindProperty("_writing").objectReferenceValue = writing;
            viewObject.ApplyModifiedPropertiesWithoutUndo();

            RewirePress(balloon.GetComponent<ScaleDownButton>(), play, view, source.GetComponent<SceneTransition>());

            var scopeObject = new SerializedObject(scope);
            scopeObject.FindProperty("_continueView").objectReferenceValue = view;
            scopeObject.ApplyModifiedProperties();

            root.SetActive(false);
            Debug.Log("[LaunchContinueButtonBuilder] Built the Continue balloon; tune it on LaunchContinueView.");
            return true;
        }

        private static GameObject NewChild(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return child;
        }

        private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font)
        {
            var text = NewChild(name, parent).AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.text = Label;
            text.fontSize = FontSize;
            text.color = InkColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        // The writing is on the ribbon now; the balloon is white, shaded by its glows.
        private static void Recolor(GameObject balloon)
        {
            var label = balloon.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                Object.DestroyImmediate(label.gameObject);
            }

            foreach (var image in balloon.GetComponentsInChildren<Image>(true))
            {
                if (image.name is "Icon" or "BakedShadow")
                {
                    image.color = BodyColor;
                }
                else if (image.name.StartsWith("Specular"))
                {
                    image.color = Color.white;
                }
            }
        }

        // The view requests the resume and then plays the ascent, so Play's own calls come off the copy. The copy's
        // hide is retargeted to the whole Continue, and its scene activation to Play's SceneTransition, the copied
        // one removed: it would preload a second Game scene the moment the balloon first shows.
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
                else if (target == button.gameObject && press.GetPersistentMethodName(i) == nameof(GameObject.SetActive))
                {
                    UnityEventTools.RegisterBoolPersistentListener(press, i, view.gameObject.SetActive, false);
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
