using System.Collections.Generic;
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
    ///     Builds the launch screen's two drifting balloons in the Launcher scene, replacing any built before.
    ///     <para>
    ///         Play keeps its own balloon, the Play button where the launch canvas has it. Its label comes off the
    ///         button's prefab, and a root stretched over the launch canvas, drawn over the button, holds its
    ///         <see cref="LaunchBalloonView" />, spring line and cloth ribbon with "play" on both faces. A press
    ///         hides that root along with the button.
    ///     </para>
    ///     <para>
    ///         Continue is a root of its own, drawn over Play: a <see cref="LaunchBalloonView" /> and
    ///         <see cref="LaunchContinueView" />, the line, the ribbon saying "continue", and a small white copy of
    ///         the Play balloon. The copy keeps Play's press actions (hide, title out, scene activation), with Play's
    ///         <c>LaunchPlayTrigger.Play</c> swapped for <see cref="LaunchContinueView.Continue" />, which plays the
    ///         ascent itself once the resume is requested; its hide hides the whole Continue, and its scene
    ///         activation is Play's own. Starts inactive: the controller shows it only while a run is saved.
    ///     </para>
    ///     Each balloon is handed the other, to share the screen with while both show: Play its upper part,
    ///     Continue its lower. A balloon's line and ribbon are drawn after the balloon, whose first child is its
    ///     shadow: the shadow shifts with the light and must never darken the ribbon hanging under it.
    /// </summary>
    internal static class LaunchBalloonsBuilder
    {
        private const string ScenePath = "Assets/Scenes/Launcher.unity";
        private const string RibbonMaterialPath = "Assets/Materials/UI/CardRevealRibbon.mat";
        private const string FontPath = "Assets/Fonts/PatrickHand-Regular SDF.asset";
        private const string UndoName = "Build Launch Balloons";
        private const float ContinueScale = 0.36f;

        // Play is the large balloon, so its ribbon is larger and its line shorter, and it drifts more slowly.
        private const float PlayRibbonHeight = 18f;
        private const float PlayLineLength = 20f;
        private const float PlaySpeed = 10f;
        private const float PlayAcceleration = 8f;

        private static readonly Ribbon PlayRibbon = new("play", 14f);
        private static readonly Ribbon ContinueRibbon = new("continue", 9.5f);

        // A little above the middle of the room it drifts in: alone, the ribbon hangs about the middle of the screen.
        private static readonly Vector2 PlayStart = new(0.5f, 0.57f);

        private static readonly Color White = new(0.97f, 0.97f, 0.98f, 1f);

        // The body art is a flat silhouette; the balloon's depth is all in its specular glows. A white body under
        // them would hide them, so it's a cool grey and the glows pure white: the scene light warms both alike, and
        // the glows stand out by being brighter and warmer, as Play's cream glows do on its blue.
        private static readonly Color BodyColor = new(0.7f, 0.74f, 0.84f, 1f);
        private static readonly Color RibbonColor = new(0.98f, 0.965f, 0.925f, 1f);
        private static readonly Color InkColor = new(0.2f, 0.2f, 0.2f, 1f);

        [MenuItem("Tools/BalloonParty/Launch/Build Balloons")]
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
            // Taking the label off the button's prefab can replace the button's objects in the scene, so it goes
            // first and everything is looked up after it.
            RemoveLabel(FindPlayButton(scene, Find<LaunchPlayTrigger>(scene)));

            var play = Find<LaunchPlayTrigger>(scene);
            var scope = Find<LaunchLifetimeScope>(scene);
            if (play == null || scope == null)
            {
                Debug.LogError("[LaunchBalloonsBuilder] The Launcher scene has no LaunchPlayTrigger or LaunchLifetimeScope.");
                return false;
            }

            var playButton = FindPlayButton(scene, play);
            if (playButton == null)
            {
                Debug.LogError("[LaunchBalloonsBuilder] No button in the Launcher scene calls LaunchPlayTrigger.Play.");
                return false;
            }

            RemoveBuilt(scene, playButton);
            var source = playButton.gameObject;
            var playView = BuildPlay(playButton);
            var continueView = BuildContinue(source, play, scope);
            SetOther(playView, continueView);
            SetOther(continueView, playView);

            Debug.Log("[LaunchBalloonsBuilder] Built the Play and Continue balloons; tune each on its LaunchBalloonView.");
            return true;
        }

        private static LaunchBalloonView BuildPlay(ScaleDownButton button)
        {
            var source = button.transform;

            // Right after the button among its siblings: the line and ribbon are drawn over the balloon, and so
            // over its shadow.
            var root = NewRoot("PlayRibbon", source.parent, source.GetSiblingIndex() + 1);
            var view = AddView(root, (RectTransform)source, PlayRibbon);
            var viewObject = new SerializedObject(view);
            viewObject.FindProperty("_above").boolValue = true;
            viewObject.FindProperty("_start").vector2Value = PlayStart;
            viewObject.FindProperty("_wander._speed").floatValue = PlaySpeed;
            viewObject.FindProperty("_wander._acceleration").floatValue = PlayAcceleration;
            viewObject.FindProperty("_cloth._height").floatValue = PlayRibbonHeight;
            viewObject.FindProperty("_cloth._lineLength").floatValue = PlayLineLength;
            viewObject.ApplyModifiedPropertiesWithoutUndo();

            // The button hides itself on a press; its line and ribbon are not its children, so they are hidden too.
            if (button.OnScaleComplete is UnityEvent press)
            {
                UnityEventTools.AddBoolPersistentListener(press, root.SetActive, false);
                EditorUtility.SetDirty(button);
                PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            }

            return view;
        }

        private static LaunchBalloonView BuildContinue(GameObject source, LaunchPlayTrigger play, LaunchLifetimeScope scope)
        {
            var root = NewRoot("Continue", source.transform.parent, source.transform.GetSiblingIndex() + 2);
            var balloon = Object.Instantiate(source, root.transform);
            balloon.name = "Balloon";
            var balloonRect = (RectTransform)balloon.transform;
            balloonRect.anchorMin = balloonRect.anchorMax = new Vector2(0.5f, 0.5f);
            balloonRect.anchoredPosition = Vector2.zero;
            balloonRect.localScale = new Vector3(ContinueScale, ContinueScale, 1f);
            Recolor(balloon);

            var view = AddView(root, balloonRect, ContinueRibbon);
            var press = root.AddComponent<LaunchContinueView>();
            RewirePress(balloon.GetComponent<ScaleDownButton>(), play, press, source.GetComponent<SceneTransition>());

            var scopeObject = new SerializedObject(scope);
            scopeObject.FindProperty("_continueView").objectReferenceValue = press;
            scopeObject.ApplyModifiedProperties();

            root.SetActive(false);
            return view;
        }

        // The view with its line, ribbon and the writing on both of the ribbon's faces, on a root already made.
        private static LaunchBalloonView AddView(GameObject root, RectTransform balloon, Ribbon label)
        {
            var line = NewChild("Line", root.transform).AddComponent<SpringLineGraphic>();
            line.color = White;
            line.raycastTarget = false;

            var ribbon = NewChild("Ribbon", root.transform).AddComponent<ClothRibbonGraphic>();
            ribbon.material = AssetDatabase.LoadAssetAtPath<Material>(RibbonMaterialPath);
            ribbon.color = RibbonColor;
            ribbon.raycastTarget = false;
            var writing = ribbon.gameObject.AddComponent<ClothRibbonWriting>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var writingObject = new SerializedObject(writing);
            writingObject.FindProperty("_front").objectReferenceValue = NewText("Front", ribbon.transform, font, label);
            writingObject.FindProperty("_back").objectReferenceValue = NewText("Back", ribbon.transform, font, label);
            writingObject.ApplyModifiedPropertiesWithoutUndo();

            var view = root.AddComponent<LaunchBalloonView>();
            var viewObject = new SerializedObject(view);
            viewObject.FindProperty("_balloon").objectReferenceValue = balloon;
            viewObject.FindProperty("_line").objectReferenceValue = line;
            viewObject.FindProperty("_ribbon").objectReferenceValue = ribbon;
            viewObject.FindProperty("_writing").objectReferenceValue = writing;
            viewObject.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void SetOther(LaunchBalloonView view, LaunchBalloonView other)
        {
            var viewObject = new SerializedObject(view);
            viewObject.FindProperty("_other").objectReferenceValue = other;
            viewObject.ApplyModifiedPropertiesWithoutUndo();
        }

        // Stretched over the launch canvas, so the balloon's room is the screen and its space the canvas's.
        private static GameObject NewRoot(string name, Transform parent, int siblingIndex)
        {
            var root = NewChild(name, parent);
            root.transform.SetSiblingIndex(siblingIndex);
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root;
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

        private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, Ribbon label)
        {
            var text = NewChild(name, parent).AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.text = label.Text;
            text.fontSize = label.FontSize;
            text.color = InkColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        // What the button does is written on its ribbon. The label is the button prefab's own, so it comes off the
        // prefab; a button that is no prefab's loses it where it stands.
        private static void RemoveLabel(ScaleDownButton button)
        {
            if (button == null)
            {
                return;
            }

            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(button.gameObject);
            if (string.IsNullOrEmpty(path))
            {
                DestroyLabel(button.gameObject);
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(path);
            if (DestroyLabel(contents))
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static bool DestroyLabel(GameObject balloon)
        {
            var label = balloon.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                return false;
            }

            Object.DestroyImmediate(label.gameObject);
            return true;
        }

        // The roots built before, and Play's press action hiding its one, which would be left pointing at nothing.
        private static void RemoveBuilt(Scene scene, ScaleDownButton playButton)
        {
            var built = new HashSet<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var view in root.GetComponentsInChildren<LaunchBalloonView>(true))
                {
                    built.Add(view.gameObject);
                }

                foreach (var view in root.GetComponentsInChildren<LaunchContinueView>(true))
                {
                    built.Add(view.gameObject);
                }
            }

            if (playButton.OnScaleComplete is UnityEvent press)
            {
                for (var i = press.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    var target = press.GetPersistentTarget(i);
                    var hides = press.GetPersistentMethodName(i) == nameof(GameObject.SetActive);
                    if (hides && (target == null || (target is GameObject hidden && built.Contains(hidden))))
                    {
                        UnityEventTools.RemovePersistentListener(press, i);
                    }
                }

                EditorUtility.SetDirty(playButton);
                PrefabUtility.RecordPrefabInstancePropertyModifications(playButton);
            }

            foreach (var root in built)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        // The copy is white, shaded by its glows.
        private static void Recolor(GameObject balloon)
        {
            DestroyLabel(balloon);
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

        // The view requests the resume and then plays the ascent, so Play's own calls come off the copy, along with
        // Play's hiding of its own ribbon. The copy's hide is retargeted to the whole Continue, and its scene
        // activation to Play's SceneTransition, the copied one removed: it would preload a second Game scene the
        // moment the balloon first shows.
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
                var hides = press.GetPersistentMethodName(i) == nameof(GameObject.SetActive);
                if (target == play || (hides && target != button.gameObject))
                {
                    UnityEventTools.RemovePersistentListener(press, i);
                }
                else if (hides)
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
            if (play == null)
            {
                return null;
            }

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

        private readonly struct Ribbon
        {
            internal readonly string Text;
            internal readonly float FontSize;

            internal Ribbon(string text, float fontSize)
            {
                Text = text;
                FontSize = fontSize;
            }
        }
    }
}
