using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration.CardReveal;
using BalloonParty.Game;
using BalloonParty.UI.CardReveal;
using BalloonParty.UI.HeadsUp;
using BalloonParty.UI.Tooltip;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Builds the card reveal: imports the baked pop sheets under <c>Assets/Sprites/UI/CardReveal/</c> as linear
    ///     data, lists them on the <see cref="CardRevealSettings" /> asset (made if missing, and assigned to the
    ///     game scope), makes the rubber's material, and builds the <see cref="CardRevealView" /> on
    ///     <c>GameCanvas</c>: the balloon's back layer, the slip, its front layer. The slip is a stand-in until the
    ///     baked one (<c>Plans/PLAN-CardReveal.md</c> phase 3): a paper strip with the tooltip's pencil font. A
    ///     starting look to restyle by hand; running it again rebuilds it.
    /// </summary>
    internal static class CardRevealBuilder
    {
        private const string MenuRoot = "Tools/BalloonParty/CardReveal/";
        private const string SheetFolder = "Assets/Sprites/UI/CardReveal";
        private const string SettingsPath = "Assets/Configuration/CardRevealSettings.asset";
        private const string ScopePrefabPath = "Assets/Prefabs/Game/GameLifetimeScope.prefab";
        private const string MaterialPath = "Assets/Materials/UI/CardRevealRubber.mat";
        private const string ShaderName = "BalloonParty/Sprite/LitFlipbook";
        private const string SlipSpritePath = "Assets/Sprites/UI/rounded_panel.png";
        private const string RootName = "CardReveal";

        // In canvas units (the canvas is 225 wide): a frame shows the balloon about 80 across, a third of the
        // screen, in the top third.
        private const float FrameSize = 124f;

        // The bake's camera sits a little below the balloon's centre (bake_pops.py): its centre is this share
        // of a frame above the frame's.
        private const float CentreAboveFrame = 0.039f;
        private const float SlipCornerMultiplier = 16f;

        private static readonly Vector2 Place = new(0f, 70f);

        // Paper, a little warmer than the board's white; a narrow strip that fits inside the balloon.
        private static readonly Color SlipColor = new(0.98f, 0.965f, 0.925f, 1f);
        private static readonly Vector2 SlipSize = new(66f, 17f);

        [MenuItem(MenuRoot + "Build Card Reveal")]
        private static void BuildFromMenu()
        {
            Build(true);
        }

        /// <summary>Everything the menu item does; <paramref name="askBeforeRebuild" /> off for scripted runs.</summary>
        internal static void Build(bool askBeforeRebuild)
        {
            var sheets = ImportSheets();
            if (sheets.Count == 0)
            {
                Debug.LogError($"[CardReveal] No pop sheets in {SheetFolder}; bake one with Tools/art/balloon_pop.");
                return;
            }

            var settings = EnsureSettings(sheets);
            AssignToScope(settings);
            var material = EnsureMaterial();
            if (material == null)
            {
                Debug.LogError($"[CardReveal] No {ShaderName} shader.");
                return;
            }

            BuildOnCanvas(material, askBeforeRebuild);
        }

        // Linear (the pixels are normals and occlusion, not colour), no mipmaps (the frames sit side by side,
        // and a mip would bleed one into the next), clamped.
        private static List<Texture2D> ImportSheets()
        {
            var sheets = new List<Texture2D>();
            var paths = AssetDatabase.FindAssets("pop_ t:Texture2D", new[] { SheetFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => System.IO.Path.GetFileName(path).StartsWith("pop_"))
                .OrderBy(path => path);
            foreach (var path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (!ImportedAsData(importer))
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.alphaIsTransparency = false;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.maxTextureSize = 2048;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }

                sheets.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }

            return sheets;
        }

        // Already imported as ImportSheets sets it up, so a rebuild doesn't reimport every sheet.
        private static bool ImportedAsData(TextureImporter importer)
        {
            return importer.textureType == TextureImporterType.Default && !importer.sRGBTexture &&
                   importer.alphaSource == TextureImporterAlphaSource.FromInput && !importer.alphaIsTransparency &&
                   !importer.mipmapEnabled && importer.wrapMode == TextureWrapMode.Clamp &&
                   importer.filterMode == FilterMode.Bilinear && importer.npotScale == TextureImporterNPOTScale.None &&
                   importer.maxTextureSize == 2048 &&
                   importer.textureCompression == TextureImporterCompression.CompressedHQ;
        }

        private static CardRevealSettings EnsureSettings(IReadOnlyList<Texture2D> sheets)
        {
            var settings = AssetDatabase.LoadAssetAtPath<CardRevealSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CardRevealSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            var fields = new SerializedObject(settings);
            var list = fields.FindProperty("_popSheets");
            list.arraySize = sheets.Count;
            for (var i = 0; i < sheets.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = sheets[i];
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void AssignToScope(CardRevealSettings settings)
        {
            var root = PrefabUtility.LoadPrefabContents(ScopePrefabPath);
            try
            {
                var scope = root.GetComponent<GameLifetimeScope>();
                var fields = new SerializedObject(scope);
                var field = fields.FindProperty("_cardRevealSettings");
                if (field.objectReferenceValue == settings)
                {
                    return;
                }

                field.objectReferenceValue = settings;
                fields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ScopePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // An existing material keeps its tuned values; only a missing one is made.
        private static Material EnsureMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void BuildOnCanvas(Material material, bool askBeforeRebuild)
        {
            var path = PrefabRebuild.FindPrefabWithChild<TooltipBanner>("GameCanvas");
            if (path == null)
            {
                Debug.LogError("[CardReveal] GameCanvas (the prefab with the tooltip) wasn't found.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var existing = root.transform.Find(RootName);
                if (existing != null)
                {
                    if (askBeforeRebuild && !EditorUtility.DisplayDialog("Card reveal",
                            $"{path} already has {RootName}. Rebuild it? Hand edits to it are lost.", "Rebuild",
                            "Cancel"))
                    {
                        return;
                    }

                    Object.DestroyImmediate(existing.gameObject);
                }

                var label = TooltipLabel(root);
                if (label == null)
                {
                    Debug.LogError("[CardReveal] The tooltip's label isn't assigned or has no WriteOnText; the slip takes its pencil.");
                    return;
                }

                BuildReveal(root.transform, label, material);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[CardReveal] Built {RootName} on {path}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static TMP_Text TooltipLabel(GameObject canvas)
        {
            var banner = canvas.GetComponentInChildren<TooltipBanner>(true);
            if (banner == null)
            {
                return null;
            }

            var label = new SerializedObject(banner).FindProperty("_label").objectReferenceValue as TMP_Text;
            return label != null && label.GetComponent<WriteOnText>() != null ? label : null;
        }

        private static void BuildReveal(Transform canvas, TMP_Text label, Material material)
        {
            var root = CreateRect(RootName, canvas);
            Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var balloon = CreateRect("Balloon", root);
            balloon.sizeDelta = new Vector2(FrameSize, FrameSize);
            balloon.anchoredPosition = Place;

            var back = CreateLayer("Back", balloon, material);
            var slip = CreateSlip(balloon);
            var writeOn = CreateTitle(slip, label);
            var front = CreateLayer("Front", balloon, material);

            var view = root.gameObject.AddComponent<CardRevealView>();
            var fields = new SerializedObject(view);
            fields.FindProperty("_group").objectReferenceValue = group;
            fields.FindProperty("_balloon").objectReferenceValue = balloon;
            fields.FindProperty("_back").objectReferenceValue = back;
            fields.FindProperty("_front").objectReferenceValue = front;
            fields.FindProperty("_writeOn").objectReferenceValue = writeOn;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RawImage CreateLayer(string name, RectTransform balloon, Material material)
        {
            var rect = CreateRect(name, balloon);
            Stretch(rect);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.material = material;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateSlip(RectTransform balloon)
        {
            var rect = CreateRect("Slip", balloon);
            rect.sizeDelta = SlipSize;
            rect.anchoredPosition = new Vector2(0f, FrameSize * CentreAboveFrame);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlipSpritePath);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = SlipCornerMultiplier;
            image.color = SlipColor;
            image.raycastTarget = false;
            return rect;
        }

        // The tooltip's label, cloned: its pencil font, colour and write-on, sized to the slip.
        private static WriteOnText CreateTitle(RectTransform slip, TMP_Text label)
        {
            var clone = Object.Instantiate(label.gameObject, slip, false);
            clone.name = "Title";
            var rect = (RectTransform)clone.transform;
            Stretch(rect);
            rect.offsetMin = new Vector2(4f, 1f);
            rect.offsetMax = new Vector2(-4f, -1f);

            var text = clone.GetComponent<TMP_Text>();
            text.text = string.Empty;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 6f;
            text.fontSizeMax = label.fontSize;
            text.raycastTarget = false;
            return clone.GetComponent<WriteOnText>();
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
