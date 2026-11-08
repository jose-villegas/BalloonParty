using System.Collections.Generic;
using BalloonParty.UI;
using BalloonParty.UI.GameOver;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Rebuilds the game-over screen in the UI theme ("glossy balloon": layered buttons and panels that
    ///     carry the balloons' specular, see <c>UI/GameOver/README.md</c>). It makes a card sized to its
    ///     content with the title on a plaque across its top edge, the result in a cream well, Retry as the
    ///     one green primary button, the ways to make the retry easier as tiles (each with a ribbon across its
    ///     corner saying what it gives), and Start Over as a red button, smaller than Retry: it throws the run away. The Retry and bonus retry buttons are kept and moved in, so their
    ///     clicks and sounds survive. The prefab is a variant of <c>Popup.prefab</c>, whose own Restart button
    ///     can't move, so the builder replaces it with a copy of its components and the same click, and
    ///     removes the rest of the old panel. Running it again rebuilds the screen the same way. Edges are
    ///     soft (outlines are glows), the speculars run along each shape's rim toward the time-of-day light
    ///     (<see cref="TimeOfDaySpecular"/>) and the faces take a little of its tint.
    /// </summary>
    internal static class GameOverLayoutBuilder
    {
        private const string MenuPath = "Tools/BalloonParty/Game Over/Rebuild Layout";
        private const string DialogTitle = "Game-over layout";
        private const string PrefabPath = "Assets/Prefabs/GameOver/UI/GameOver.prefab";
        private const string HostPrefabPath = "Assets/Prefabs/Game/GameCanvas.prefab";
        private const string SpriteFolder = "Assets/Sprites/UI/";
        private const string FontFolder = "Assets/Fonts";
        private const string MaterialFolderName = "Materials";
        private const string TitleFontPath = FontFolder + "/Rounds Black SDF.asset";
        private const string HandFontPath = FontFolder + "/PatrickHand-Regular SDF.asset";
        private const string PanelName = "Panel";
        private const string LooseBonusName = "BonusRetry";

        // Canvas units, on the 225x400 reference resolution. Corners are an Image's Pixels Per Unit
        // Multiplier on the sprites' shared 64 px corner: 4 gives a 16-unit radius.
        private const float PanelWidth = 186f;
        private const float PanelCorner = 4f;
        private const float WellCorner = 6f;
        private const float ButtonCorner = 5.5f;
        private const float TileCorner = 7f;
        private const float ShadowPad = 8f;
        private const float ShadowDrop = 5f;
        private const float MinTouchHeight = 27f;
        private const float SpriteCornerPixels = 64f;

        // The glow sprite's room for its falloff, in pixels on each side.
        private const float GlowPad = 16f;
        private const float FaceTint = 0.25f;

        // The card's body is mostly see-through, so the loss reads over the board it happened
        // on; what's information or an action (plaque, result, buttons) stays solid.
        private const float BodyAlpha = 0.4f;

        private static readonly Color ShadowColor = Hex(0x0B2A4A, 0.4f);
        private static readonly Color PanelStroke = Hex(0x0B4F8A);
        private static readonly Color PanelBody = Hex(0x3BB6F4, BodyAlpha);
        private static readonly Color Cream = Hex(0xFFF7E8);
        private static readonly Color Graphite = Hex(0x4A4A52);
        private static readonly Color Blue = Hex(0x1A86DA);
        private static readonly Color Dim = Hex(0x10233A, 0.6f);
        private static readonly Palette Green = new(Hex(0x6BD94A), Hex(0x1F8A16), Hex(0x155C10));
        private static readonly Palette Vermilion = new(Hex(0xE2611C), Hex(0x8E3A0A), Hex(0x6E2A00));
        private static readonly Palette Plum = new(Hex(0xB565B5), Hex(0x5E215E), Hex(0x4A1A4A));
        private static readonly Palette Danger = new(Hex(0xE5484D), Hex(0xA3262A), Hex(0x6B1215));

        // The layers that take the time-of-day tint: the coloured faces, not the shadows, glows or text.
        private static readonly HashSet<string> LitLayers = new() { "Face", "Body", "Lip", "Sheen" };

        [MenuItem(MenuPath)]
        private static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog(DialogTitle,
                    $"Rebuild the panel of {PrefabPath}? Hand edits to it are lost; the buttons' clicks are kept.",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            var error = Build();
            if (error != null)
            {
                EditorUtility.DisplayDialog(DialogTitle, error, "OK");
            }
        }

        /// <summary>Rebuilds and saves the prefab; returns an error to show, or null on success.</summary>
        internal static string Build()
        {
            var kit = Kit.Load();
            if (kit == null)
            {
                return "A sprite or font the layout uses wasn't found; see GameOverLayoutBuilder's paths.";
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var error = Rebuild(root, kit);
                if (error != null)
                {
                    return error;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            ClearHostOverrides();
            AssetDatabase.SaveAssets();
            Debug.Log($"[{DialogTitle}] Rebuilt the panel of {PrefabPath}.");
            return null;
        }

        // The canvas holding the screen keeps overrides of its children's rects (layout writes them); left in
        // place, they pin kept labels where the old layout had them.
        private static void ClearHostOverrides()
        {
            var host = PrefabUtility.LoadPrefabContents(HostPrefabPath);
            try
            {
                foreach (var screen in host.GetComponentsInChildren<GameOverScreen>(true))
                {
                    var instance = PrefabUtility.GetNearestPrefabInstanceRoot(screen.gameObject);
                    if (instance == null)
                    {
                        continue;
                    }

                    foreach (var change in PrefabUtility.GetObjectOverrides(instance))
                    {
                        if (change.instanceObject is RectTransform rect && rect != instance.transform)
                        {
                            change.Revert(InteractionMode.AutomatedAction);
                        }
                    }

                    PrefabUtility.RemoveUnusedOverrides(new[] { instance }, InteractionMode.AutomatedAction);
                }

                PrefabUtility.SaveAsPrefabAsset(host, HostPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(host);
            }
        }

        private static string Rebuild(GameObject root, Kit kit)
        {
            var screen = root.GetComponent<GameOverScreen>();
            if (screen == null)
            {
                return $"{PrefabPath} has no GameOverScreen on its root.";
            }

            var fields = new SerializedObject(screen);
            var retry = fields.FindProperty("_retryButton").objectReferenceValue as Button;
            var bonus = fields.FindProperty("_bonusRetryButton").objectReferenceValue as Button;
            var restart = FindRestart(root);
            if (retry == null || bonus == null || restart == null)
            {
                return "The Retry, bonus retry or Restart button wasn't found; the layout keeps their clicks.";
            }

            // The base popup's own button can't be moved by the variant; a copy can.
            if (PrefabUtility.IsPartOfPrefabInstance(restart.gameObject))
            {
                restart = CopyButton(restart, root.transform, "Restart", screen.OnRestartPressed);
            }

            // Lift the kept buttons out before the old panel goes.
            foreach (var kept in new[] { retry, bonus, restart })
            {
                kept.transform.SetParent(root.transform, false);
            }

            DestroyChild(root.transform, PanelName);
            DestroyChild(root.transform, LooseBonusName, bonus.transform);
            Backdrop(root.transform);

            var panel = BuildPanel(root.transform, kit);
            Plaque(panel, kit);
            var well = Well(panel, kit);
            var level = Label(well, "Level", "Level {0}", kit.TitleFont, kit.Number, 25f, Color.white);
            var score = Label(well, "Score", "{0:N0} points", kit.TitleFont, kit.Plain, 12f, Blue);
            var best = Label(well, "Best", "best: level {0}, {1:N0} points", kit.HandFont, null, 12f, Graphite);
            var retryLabel = StyleRetry(retry, panel, kit);
            var offers = BuildOffers(panel, bonus, kit);
            StyleRestart(restart, panel, kit);
            TintFaces(panel);

            fields.FindProperty("_finalLevelLabel").objectReferenceValue = level;
            fields.FindProperty("_finalScoreLabel").objectReferenceValue = score;
            fields.FindProperty("_bestLabel").objectReferenceValue = best;
            fields.FindProperty("_retryLabel").objectReferenceValue = retryLabel;
            fields.FindProperty("_offers").objectReferenceValue = offers;
            fields.FindProperty("_panel").objectReferenceValue = panel;
            fields.FindProperty("_primaryButton").objectReferenceValue = retry.transform;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }

        private static Button FindRestart(GameObject root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentMethodName(i) == nameof(GameOverScreen.OnRestartPressed))
                    {
                        return button;
                    }
                }
            }

            return null;
        }

        // A new button carrying the source's other components (its sound emitter among them), wired to the
        // same click.
        private static Button CopyButton(Button source, Transform parent, string name, UnityAction click)
        {
            var node = Node(name, parent);
            var image = node.gameObject.AddComponent<Image>();
            var button = node.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            foreach (var component in source.GetComponents<Component>())
            {
                if (component is RectTransform || component is CanvasRenderer || component is Image
                    || component is Button)
                {
                    continue;
                }

                if (UnityEditorInternal.ComponentUtility.CopyComponent(component))
                {
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(node.gameObject);
                }
            }

            UnityEventTools.AddPersistentListener(button.onClick, click);
            Node("Text", node).gameObject.AddComponent<TextMeshProUGUI>();
            return button;
        }

        private static void DestroyChild(Transform parent, string name, Transform keep = null)
        {
            var child = parent.Find(name);
            if (child != null && child != keep)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        // The board recedes under a dark dim instead of the base popup's orange wash; the rays are for
        // rewards, not for a loss.
        private static void Backdrop(Transform root)
        {
            var glow = root.Find("Glow");
            if (glow != null && glow.TryGetComponent<Image>(out var dim))
            {
                dim.sprite = null;
                dim.color = Dim;
                var rect = (RectTransform)glow;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                Stretch(rect, -40f);
            }

            var rays = root.Find("Rays");
            if (rays != null)
            {
                rays.gameObject.SetActive(false);
            }
        }

        // The card: a soft shadow, a dark blurred edge that smears into the board, the blue gradient body,
        // its sheen and the specular. It sizes to its content, so nothing it holds can spill out. The body is
        // see-through, so the layers behind it are hollow; the shadow's sprite is drawn for this corner and
        // drop.
        private static Transform BuildPanel(Transform root, Kit kit)
        {
            var panel = Node(PanelName, root);
            var rect = (RectTransform)panel;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 4f);
            rect.sizeDelta = new Vector2(PanelWidth, 0f);

            Layer(panel, "Body", kit.Gradient, PanelBody, PanelCorner, 0f);
            Layer(panel, "Smear", kit.Smear, PanelStroke, PanelCorner, -ShadowPad).fillCenter = false;
            var shadow = Layer(panel, "Shadow", kit.DropShadow, ShadowColor, PanelCorner, -ShadowPad);
            shadow.fillCenter = false;
            ((RectTransform)shadow.transform).offsetMin -= new Vector2(0f, ShadowDrop);
            Sheen(panel, kit, 1.5f, 0.8f, 0.2f, PanelCorner);
            Specular(panel, PanelCorner, 0f, 70f, 3.5f, 0.6f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 25, 16);
            layout.spacing = 8f;
            Column(layout, TextAnchor.UpperCenter);
            layout.childForceExpandWidth = true;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        // "Game Over" in white on a vermilion plaque across the card's top edge.
        private static void Plaque(Transform panel, Kit kit)
        {
            var plaque = Node("Plaque", panel);
            plaque.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)plaque;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 3f);
            rect.sizeDelta = new Vector2(132f, 28f);
            Layers(plaque, kit, Vermilion, ButtonCorner, 2.5f);
            var title = Label(plaque, "Title", "Game Over", kit.TitleFont, kit.Title, 17f, Color.white);
            Stretch((RectTransform)title.transform, 0f);
        }

        // The result, on cream: the white level outlined in the panel's blue, the points in blue and the
        // best in pencil, all readable at a glance (white on the bare blue was 2.7:1).
        private static Transform Well(Transform panel, Kit kit)
        {
            var well = Node("Result", panel);
            Layer(well, "Face", kit.Flat, Cream, WellCorner, 0f, default, 1.5f);
            Layer(well, "Edge", kit.Flat, new Color(PanelStroke.r, PanelStroke.g, PanelStroke.b, 0.45f), WellCorner,
                0f);
            var layout = well.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 9, 8);
            layout.spacing = 0f;
            Column(layout, TextAnchor.MiddleCenter);
            return well;
        }

        private static TMP_Text StyleRetry(Button retry, Transform panel, Kit kit)
        {
            var label = KeepOnlyLabel(retry);
            retry.transform.SetParent(panel, false);
            Size(retry.gameObject, -1f, 36f);
            retry.targetGraphic = Layers(retry.transform, kit, Green, ButtonCorner, 3f);
            label.transform.SetAsLastSibling();
            StyleLabel(label, "Retry level {0}", kit.TitleFont, kit.ButtonGreen, 15f, Color.white);
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 15f;
            label.margin = new Vector4(10f, 0f, 10f, 0f);
            return label;
        }

        // The row of ways to make the retry easier, the tiles sharing its width; the coin shop's tile joins it.
        private static GameObject BuildOffers(Transform panel, Button bonus, Kit kit)
        {
            var offers = Node("Offers", panel);
            var tiles = offers.gameObject.AddComponent<HorizontalLayoutGroup>();
            tiles.padding = new RectOffset(0, 0, 2, 4);
            tiles.spacing = 8f;
            tiles.childAlignment = TextAnchor.MiddleCenter;
            tiles.childControlWidth = true;
            tiles.childControlHeight = true;
            tiles.childForceExpandWidth = true;
            tiles.childForceExpandHeight = false;

            StyleAdTile(bonus, offers, kit);
            return offers.gameObject;
        }

        // The ad's tile, told by pictures: a face-down card with a "?" (a random card) beside a pill with the
        // play mark and "AD".
        private static void StyleAdTile(Button tile, Transform row, Kit kit)
        {
            var label = KeepOnlyLabel(tile);
            tile.transform.SetParent(row, false);
            Size(tile.gameObject, -1f, 40f);
            tile.GetComponent<LayoutElement>().flexibleWidth = 1f;
            tile.targetGraphic = Layers(tile.transform, kit, Plum, TileCorner, 3f);

            var content = Node("Content", tile.transform);
            Stretch((RectTransform)content, 0f);
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 0, 3);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var card = Node("Card", content);
            var face = card.gameObject.AddComponent<Image>();
            Rounded(face, kit.Flat, Color.white, 14f);
            face.raycastTarget = false;
            ((RectTransform)card).sizeDelta = new Vector2(19f, 25f);
            Size(card.gameObject, 19f, 25f);
            card.localRotation = Quaternion.Euler(0f, 0f, -6f);
            var mark = Label(card, "Mark", "?", kit.TitleFont, kit.Plain, 16f, Plum.Lip);
            Stretch((RectTransform)mark.transform, 0f);

            var pill = Node("Pill", content);
            var pillFace = pill.gameObject.AddComponent<Image>();
            Rounded(pillFace, kit.Flat, Color.white, 10f);
            pillFace.raycastTarget = false;
            ((RectTransform)pill).sizeDelta = new Vector2(42f, 16f);
            Size(pill.gameObject, 42f, 16f);
            var play = Icon(pill, "Play", kit.Play, Plum.Lip, 11f);
            var playRect = (RectTransform)play.transform;
            playRect.anchorMin = playRect.anchorMax = new Vector2(0f, 0.5f);
            playRect.anchoredPosition = new Vector2(10f, 0f);

            label.transform.SetParent(pill, false);
            StyleLabel(label, "AD", kit.TitleFont, kit.Plain, 10.5f, Plum.Lip);
            var labelRect = (RectTransform)label.transform;
            Stretch(labelRect, 0f);
            labelRect.offsetMin = new Vector2(14f, 0f);

            Sash(tile.transform, "Boost", kit);
        }

        // A ribbon across the tile's top-right corner, saying what the tile gives. It runs past the tile's
        // edges into a soft-edged clip a little larger than the corner, so it reads as wrapped round it.
        private static void Sash(Transform tile, string text, Kit kit)
        {
            const float clipSize = 34f;
            const float overhang = 3f;
            var clip = Node("Sash", tile);
            clip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var clipRect = (RectTransform)clip;
            clipRect.anchorMin = clipRect.anchorMax = Vector2.one;
            clipRect.pivot = Vector2.one;
            clipRect.anchoredPosition = new Vector2(overhang, overhang);
            clipRect.sizeDelta = new Vector2(clipSize, clipSize);
            clip.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(2, 2);

            var band = Node("Band", clip);
            var bandRect = (RectTransform)band;
            bandRect.anchorMin = bandRect.anchorMax = Vector2.one;
            bandRect.pivot = new Vector2(0.5f, 0.5f);
            bandRect.anchoredPosition = new Vector2(-13f, -13f);
            bandRect.sizeDelta = new Vector2(60f, 11f);
            bandRect.localRotation = Quaternion.Euler(0f, 0f, -45f);
            Layer(band, "Face", kit.Gradient, Vermilion.Face, 40f, 0f);
            Layer(band, "Edge", kit.Glow, Vermilion.Outline, 40f, GlowInset(40f, 0.8f));
            var word = Label(band, "Label", text, kit.TitleFont, kit.Title, 6.5f, Color.white);
            Stretch((RectTransform)word.transform, 0f);
        }

        private static void StyleRestart(Button restart, Transform panel, Kit kit)
        {
            var label = KeepOnlyLabel(restart);
            restart.transform.SetParent(panel, false);
            restart.transform.SetAsLastSibling();

            // Red, for the heavy choice: it throws the run away. Shorter than Retry, which stays the one to
            // press.
            Size(restart.gameObject, -1f, MinTouchHeight);
            restart.targetGraphic = Layers(restart.transform, kit, Danger, ButtonCorner, 2.5f);
            label.transform.SetAsLastSibling();
            StyleLabel(label, "Start over", kit.TitleFont, kit.ButtonRed, 10f, Color.white);
        }

        // Clears what an earlier layout added under a kept button, keeping its one label.
        private static TMP_Text KeepOnlyLabel(Button button)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                label = Node("Text", button.transform).gameObject.AddComponent<TextMeshProUGUI>();
            }

            label.transform.SetParent(button.transform, false);
            var children = new List<GameObject>();
            foreach (Transform child in button.transform)
            {
                if (child != label.transform)
                {
                    children.Add(child.gameObject);
                }
            }

            foreach (var child in children)
            {
                Object.DestroyImmediate(child);
            }

            foreach (var group in button.GetComponents<LayoutGroup>())
            {
                Object.DestroyImmediate(group);
            }

            // Layers draw the button now; its own Image only takes the taps.
            if (button.TryGetComponent<Image>(out var own))
            {
                own.sprite = null;
                own.color = Color.clear;
            }

            Stretch((RectTransform)label.transform, 0f);
            return label;
        }

        // A button's or plaque's layers, back to front: soft shadow, outline (reaching down around the
        // lip), lip, gradient face, sheen and specular. Returns the face, the graphic a button tints.
        private static Image Layers(Transform target, Kit kit, Palette palette, float corner, float lip)
        {
            var face = Layer(target, "Face", kit.Gradient, palette.Face, corner, 0f);
            Layer(target, "Lip", kit.Flat, palette.Lip, corner, 0f, new Vector2(0f, -lip));
            var outline = Layer(target, "Outline", kit.Glow, palette.Outline, corner, GlowInset(corner, 1.2f));
            ((RectTransform)outline.transform).offsetMin += new Vector2(0f, -lip);
            Layer(target, "Shadow", kit.Shadow, ShadowColor, corner, -ShadowPad * 0.6f, new Vector2(0f, -lip - 2f));
            var sheen = Sheen(target, kit, 1.5f, 0.55f, 0.35f, corner);
            sheen.transform.SetSiblingIndex(face.transform.GetSiblingIndex() + 1);
            var specular = Specular(target, corner, 0f, 30f, 2.5f, 0.75f);
            specular.transform.SetSiblingIndex(sheen.transform.GetSiblingIndex() + 1);
            return face;
        }

        // Grows a glow layer past its parent by its blur room, then by the visible width.
        private static float GlowInset(float corner, float width)
        {
            return -(GlowPad / corner) - width;
        }

        // A little of the time-of-day light's colour on the faces, so the card sits in the scene's light.
        private static void TintFaces(Transform panel)
        {
            var lit = new List<Graphic>();
            foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true))
            {
                if (LitLayers.Contains(graphic.name))
                {
                    lit.Add(graphic);
                }
            }

            var tint = new SerializedObject(panel.gameObject.AddComponent<TimeOfDayTint>());
            var graphics = tint.FindProperty("_graphics");
            graphics.arraySize = lit.Count;
            for (var i = 0; i < lit.Count; i++)
            {
                graphics.GetArrayElementAtIndex(i).objectReferenceValue = lit[i];
            }

            tint.FindProperty("_intensity").floatValue = FaceTint;
            tint.ApplyModifiedPropertiesWithoutUndo();
        }

        // A layer behind the content: stretched over its parent, inset (negative grows it), shifted, its top
        // lowered by topInset. Each goes to the back, so call them front to back.
        private static Image Layer(Transform parent, string name, Sprite sprite, Color color, float corner,
            float inset, Vector2 shift = default, float topInset = 0f)
        {
            var node = Node(name, parent);
            node.SetAsFirstSibling();
            var rect = (RectTransform)node;
            Stretch(rect, inset);
            rect.offsetMin += shift;
            rect.offsetMax += shift - new Vector2(0f, topInset);
            node.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var image = node.gameObject.AddComponent<Image>();
            Rounded(image, sprite, color, corner);
            image.raycastTarget = false;
            return image;
        }

        // The face's sheen over its top part, white fading downward, in front of everything so far.
        private static Image Sheen(Transform parent, Kit kit, float inset, float from, float alpha, float corner)
        {
            var image = Layer(parent, "Sheen", kit.Gloss, new Color(1f, 1f, 1f, alpha), corner, inset);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = new Vector2(0f, from);
            rect.SetAsLastSibling();
            return image;
        }

        // The balloons' specular, along the rim of the shape inset by the given amount from the parent, whose
        // corner is the given multiplier on the sprites' corner.
        private static TimeOfDaySpecular Specular(Transform parent, float corner, float inset, float length,
            float width, float alpha)
        {
            var node = Node("Specular", parent);
            Stretch((RectTransform)node, inset);
            node.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // A Graphic added from code doesn't bring its renderer along.
            node.gameObject.AddComponent<CanvasRenderer>();
            var specular = node.gameObject.AddComponent<TimeOfDaySpecular>();
            specular.color = new Color(1f, 1f, 1f, alpha);
            specular.raycastTarget = false;
            var fields = new SerializedObject(specular);
            fields.FindProperty("_cornerRadius").floatValue = SpriteCornerPixels / corner;
            fields.FindProperty("_inset").floatValue = Mathf.Max(2f, width);
            fields.FindProperty("_length").floatValue = length;
            fields.FindProperty("_width").floatValue = width;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return specular;
        }

        private static Image Icon(Transform parent, string name, Sprite sprite, Color color, float size)
        {
            var image = Node(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            ((RectTransform)image.transform).sizeDelta = new Vector2(size, size);
            return image;
        }

        private static void Column(VerticalLayoutGroup layout, TextAnchor alignment)
        {
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static Transform Node(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.layer = parent.gameObject.layer;
            node.transform.SetParent(parent, false);
            return node.transform;
        }

        private static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font,
            Material material, float size, Color color)
        {
            var label = Node(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            StyleLabel(label, text, font, material, size, color);
            return label;
        }

        private static void StyleLabel(TMP_Text label, string text, TMP_FontAsset font, Material material,
            float size, Color color)
        {
            label.text = text;
            label.font = font;
            label.fontSharedMaterial = material != null ? material : font.material;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = false;
            label.fontStyle = FontStyles.Normal;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
        }

        private static void Size(GameObject target, float width, float height)
        {
            if (!target.TryGetComponent<LayoutElement>(out var element))
            {
                element = target.AddComponent<LayoutElement>();
            }

            element.preferredWidth = width;
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void Rounded(Image image, Sprite sprite, Color color, float corner)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = corner;
            image.color = color;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }

        private readonly struct Palette
        {
            public readonly Color Face;
            public readonly Color Lip;
            public readonly Color Outline;

            public Palette(Color face, Color lip, Color outline)
            {
                Face = face;
                Lip = lip;
                Outline = outline;
            }
        }

        // The sprites, fonts and text materials the layout uses. The materials are the theme's text roles,
        // one each so labels batch: white with a dark outline and a soft drop under it, or plain.
        private sealed class Kit
        {
            public Sprite Flat;
            public Sprite Gradient;
            public Sprite Shadow;
            public Sprite Glow;
            public Sprite Smear;
            public Sprite DropShadow;
            public Sprite Gloss;
            public Sprite CardPlus;
            public Sprite Play;
            public TMP_FontAsset TitleFont;
            public TMP_FontAsset HandFont;
            public Material Title;
            public Material Number;
            public Material ButtonGreen;
            public Material ButtonRed;
            public Material Plain;

            public static Kit Load()
            {
                var kit = new Kit
                {
                    Flat = SpriteAt("rounded_panel"),
                    Gradient = SpriteAt("rounded_gradient"),
                    Shadow = SpriteAt("rounded_shadow"),
                    Glow = SpriteAt("rounded_glow"),
                    Smear = SpriteAt("rounded_smear"),
                    DropShadow = SpriteAt("rounded_drop_shadow"),
                    Gloss = SpriteAt("gloss"),
                    CardPlus = SpriteAt("card_plus"),
                    Play = SpriteAt("play"),
                    TitleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath),
                    HandFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HandFontPath),
                };
                if (kit.Flat == null || kit.Gradient == null || kit.Shadow == null || kit.Glow == null
                    || kit.Smear == null || kit.DropShadow == null || kit.Gloss == null || kit.CardPlus == null
                    || kit.Play == null
                    || kit.TitleFont == null || kit.HandFont == null)
                {
                    return null;
                }

                kit.Title = TextMaterial(kit.TitleFont, "Title", Vermilion.Outline, 0.25f, true);
                kit.Number = TextMaterial(kit.TitleFont, "Number", PanelStroke, 0.22f, true);
                kit.ButtonGreen = TextMaterial(kit.TitleFont, "Button Green", Green.Outline, 0.25f, true);
                kit.ButtonRed = TextMaterial(kit.TitleFont, "Button Red", Danger.Outline, 0.25f, true);
                kit.Plain = TextMaterial(kit.TitleFont, "Plain", Color.clear, 0f, false);
                return kit;
            }

            private static Sprite SpriteAt(string name)
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
            }

            // One material per text role, kept as an asset beside the font and updated in place, so labels
            // keep their reference across rebuilds.
            private static Material TextMaterial(TMP_FontAsset font, string role, Color outline, float width,
                bool underlay)
            {
                var folder = $"{FontFolder}/{MaterialFolderName}";
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    AssetDatabase.CreateFolder(FontFolder, MaterialFolderName);
                }

                var path = $"{folder}/{font.name} {role}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(font.material);
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    material.CopyPropertiesFromMaterial(font.material);
                }

                material.shaderKeywords = font.material.shaderKeywords;
                material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
                if (underlay)
                {
                    material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    material.SetColor(ShaderUtilities.ID_UnderlayColor, ShadowColor);
                    material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                    material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.2f);
                }
                else
                {
                    material.DisableKeyword(ShaderUtilities.Keyword_Underlay);
                }

                EditorUtility.SetDirty(material);
                return material;
            }
        }
    }
}
