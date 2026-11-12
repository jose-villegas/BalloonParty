using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration.CardReveal;
using BalloonParty.Game;
using BalloonParty.UI;
using BalloonParty.UI.CardReveal;
using BalloonParty.UI.HeadsUp;
using BalloonParty.UI.Tooltip;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Builds the card reveal: imports the baked pop sheets under <c>Assets/Sprites/UI/CardReveal/</c> as linear
    ///     data, lists them on the <see cref="CardRevealSettings" /> asset (made if missing, and assigned to the
    ///     game scope), makes the materials, and builds the <see cref="CardRevealView" /> on a canvas of its own,
    ///     the <c>CardRevealCanvas</c> prefab, placed in the Game scene: the balloon's shadow and back layer, the two
    ///     ribbons (the card's title and what it does), its front layer, and the pushpin that pricks it (its baked
    ///     sheet, <c>pin.png</c>, lit as the rubber is). The canvas is World Space, fitted to the
    ///     camera (<see cref="CameraFittedCanvas" />), on the Balloons layer and sorted just under the screen-space
    ///     light, so the reveal takes part in the game's lighting as the board's balloons do. The ribbons are cream
    ///     satin written in the tooltip's pencil font. A starting look to restyle by hand; running it again
    ///     rebuilds it.
    /// </summary>
    internal static class CardRevealBuilder
    {
        private const string MenuRoot = "Tools/BalloonParty/CardReveal/";
        private const string SheetFolder = "Assets/Sprites/UI/CardReveal";
        private const string SettingsPath = "Assets/Configuration/CardRevealSettings.asset";
        private const string ScopePrefabPath = "Assets/Prefabs/Game/GameLifetimeScope.prefab";
        private const string MaterialPath = "Assets/Materials/UI/CardRevealRubber.mat";
        private const string ShadowMaterialPath = "Assets/Materials/UI/CardRevealRubberShadow.mat";
        private const string PinMaterialPath = "Assets/Materials/UI/CardRevealPin.mat";
        private const string PinSheetPath = "Assets/Sprites/UI/CardReveal/pin.png";
        private const string ShaderName = "BalloonParty/Sprite/LitFlipbook";
        private const string RibbonMaterialPath = "Assets/Materials/UI/CardRevealRibbon.mat";
        private const string RibbonShaderName = "BalloonParty/Sprite/CelRibbon";
        private const string RootName = "CardReveal";
        private const string CanvasPrefabPath = "Assets/Prefabs/Game/CardRevealCanvas.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        // A sheet is 2048 x 3072 (48 frames of 256 a layer, two layers): imported whole, never scaled down.
        private const int MaxSheetSize = 4096;

        // The pin's sheet holds its head and its needle side by side in its top half
        // (Tools/art/balloon_pop/bake_pin.py).
        private const int PinSheetColumns = 2;

        // A layer the main camera draws and the screen-space light's scene capture records (SceneCaptureService's
        // captured layers), so the reveal casts and bounces light as the board's balloons do.
        private const string RevealLayer = "Balloons";

        // Above every gameplay sprite, and just under the screen-space light's overlay (ScreenSpaceLightService:
        // Sky, 32000), which then tints the reveal like the board.
        private const string RevealSortingLayer = "Sky";
        private const int RevealSortingOrder = 31999;

        // In canvas units (the canvas is 225 wide): a frame shows the balloon about 93 across, two fifths of the
        // screen.
        private const float FrameSize = 144f;

        // The bake's camera sits a little below the balloon's centre (bake_pops.py): its centre is this share
        // of a frame above the frame's.
        private const float CentreAboveFrame = 0.039f;

        // The description is written smaller than the title (the tooltip's size), on a narrower ribbon.
        private const float DescriptionScale = 0.72f;
        private const float TitleRibbonHeight = 16f;
        private const float DescriptionRibbonHeight = 12.5f;

        // A little above the screen's middle.
        private static readonly Vector2 Place = new(0f, 12f);

        // Cream, a little warmer than the board's white, so the pencil reads on it over any scenery.
        private static readonly Color RibbonColor = new(0.98f, 0.965f, 0.925f, 1f);

        private static readonly Rect PinHeadCell = new(0f, 0.5f, 0.5f, 0.5f);
        private static readonly Rect PinNeedleCell = new(0.5f, 0.5f, 0.5f, 0.5f);
        private static readonly int SheetGridId = Shader.PropertyToID("_SheetGrid");
        private static readonly int ScatterId = Shader.PropertyToID("_Scatter");

        // Rolled up inside the balloon, then out: the title above the balloon's place, to the left, and what the
        // card does under it, to the right, each flying off its own way at the end.
        private static readonly Flight TitleFlight = new(new Vector2(0f, 6f), -8f, new Vector2(-8f, 42f), -4f,
            new Vector2(-1f, 5f), 0f, new Vector2(-60f, 130f), -14f);

        private static readonly Flight DescriptionFlight = new(new Vector2(0f, -6f), 6f, new Vector2(10f, 18f), 3f,
            new Vector2(1f, 4f), 2.1f, new Vector2(70f, 120f), 12f);

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
            CardRevealShredsBuilder.Build(settings);
            var materials = new Materials(EnsureMaterial(MaterialPath, ShaderName),
                EnsureMaterial(ShadowMaterialPath, ShaderName, "_DropShadow"),
                EnsureMaterial(RibbonMaterialPath, RibbonShaderName), EnsurePinMaterial());
            if (materials.Rubber == null || materials.Shadow == null || materials.Ribbon == null)
            {
                Debug.LogError($"[CardReveal] No {ShaderName} or {RibbonShaderName} shader.");
                return;
            }

            BuildOnCanvas(materials, askBeforeRebuild);
        }

        private static List<Texture2D> ImportSheets()
        {
            return AssetDatabase.FindAssets("pop_ t:Texture2D", new[] { SheetFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => System.IO.Path.GetFileName(path).StartsWith("pop_"))
                .OrderBy(path => path)
                .Select(ImportAsData)
                .ToList();
        }

        // Linear (the pixels are normals and occlusion, not colour), no mipmaps (the frames sit side by side,
        // and a mip would bleed one into the next), clamped. Null if there is no texture at the path.
        private static Texture2D ImportAsData(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return null;
            }

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
                importer.maxTextureSize = MaxSheetSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Already imported as ImportSheets sets it up, so a rebuild doesn't reimport every sheet.
        private static bool ImportedAsData(TextureImporter importer)
        {
            return importer.textureType == TextureImporterType.Default && !importer.sRGBTexture &&
                   importer.alphaSource == TextureImporterAlphaSource.FromInput && !importer.alphaIsTransparency &&
                   !importer.mipmapEnabled && importer.wrapMode == TextureWrapMode.Clamp &&
                   importer.filterMode == FilterMode.Bilinear && importer.npotScale == TextureImporterNPOTScale.None &&
                   importer.maxTextureSize == MaxSheetSize &&
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

        // An existing material keeps its tuned values; only a missing one is made, with switchedOn (a toggle
        // property) turned on.
        private static Material EnsureMaterial(string path, string shaderName, string switchedOn = null)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            if (switchedOn != null)
            {
                material.SetFloat(switchedOn, 1f);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // The rubber's shader on the pin's two cells, without the rubber's glow: plastic and steel don't let the
        // light through. Made only if missing, so its tuning is kept.
        private static Material EnsurePinMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(PinMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            var material = EnsureMaterial(PinMaterialPath, ShaderName);
            if (material == null)
            {
                return null;
            }

            material.SetVector(SheetGridId, new Vector4(PinSheetColumns, 1f));
            material.SetFloat(ScatterId, 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void BuildOnCanvas(Materials materials, bool askBeforeRebuild)
        {
            var gameCanvasPath = PrefabRebuild.FindPrefabWithChild<TooltipBanner>("GameCanvas");
            if (gameCanvasPath == null)
            {
                Debug.LogError("[CardReveal] GameCanvas (the prefab with the tooltip) wasn't found.");
                return;
            }

            if (askBeforeRebuild && AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath) != null &&
                !EditorUtility.DisplayDialog("Card reveal",
                    $"{CanvasPrefabPath} already exists. Rebuild it? Hand edits to it are lost.", "Rebuild", "Cancel"))
            {
                return;
            }

            var gameCanvas = PrefabUtility.LoadPrefabContents(gameCanvasPath);
            try
            {
                var label = TooltipLabel(gameCanvas);
                if (label == null)
                {
                    Debug.LogError("[CardReveal] The tooltip's label isn't assigned or has no WriteOnText; the ribbons take its pencil.");
                    return;
                }

                var scaler = gameCanvas.GetComponent<CanvasScaler>();
                if (scaler == null)
                {
                    Debug.LogError("[CardReveal] GameCanvas has no CanvasScaler; the reveal's canvas fits by its layout.");
                    return;
                }

                BuildCanvasPrefab(label, scaler, materials);

                // Built on the game canvas before it had a canvas of its own: drawn above the screen-space light.
                var old = gameCanvas.transform.Find(RootName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(gameCanvas, gameCanvasPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(gameCanvas);
            }

            PlaceInGameScene();
            Debug.Log($"[CardReveal] Built {CanvasPrefabPath}.");
        }

        // The game canvas's scaler, copied onto the fit, so the reveal lays out on the same canvas size.
        private static void BuildCanvasPrefab(TMP_Text label, CanvasScaler scaler, Materials materials)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(CanvasPrefabPath),
                typeof(RectTransform)) { layer = LayerMask.NameToLayer(RevealLayer) };
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingLayerName = RevealSortingLayer;
                canvas.sortingOrder = RevealSortingOrder;
                root.AddComponent<CameraFittedCanvas>()
                    .MatchScaler(scaler.referenceResolution, scaler.matchWidthOrHeight);
                BuildReveal(root.transform, label, materials);

                // The ribbons' text is cloned from the game canvas, on the UI layer, which the main camera skips.
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = root.layer;
                }

                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // An instance in the Game scene, if it has none. A Game scene open with unsaved changes isn't saved for it.
        private static void PlaceInGameScene()
        {
            var scene = SceneManager.GetSceneByPath(GameScenePath);
            var opened = !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }

            try
            {
                if (scene.GetRootGameObjects().Any(IsRevealCanvas))
                {
                    return;
                }

                if (scene.isDirty)
                {
                    Debug.LogWarning($"[CardReveal] Save {GameScenePath} and build again to place {CanvasPrefabPath} in it.");
                    return;
                }

                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath), scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static bool IsRevealCanvas(GameObject go)
        {
            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == CanvasPrefabPath;
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

        private static void BuildReveal(Transform canvas, TMP_Text label, Materials materials)
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

            var shadow = CreateLayer("Shadow", balloon, materials.Shadow);
            var back = CreateLayer("Back", balloon, materials.Rubber);
            var centre = new Vector2(0f, FrameSize * CentreAboveFrame);
            var title = CreateRibbon("TitleRibbon", balloon, label, materials.Ribbon, centre, TitleRibbonHeight,
                label.fontSize, TitleFlight);
            var description = CreateRibbon("DescriptionRibbon", balloon, label, materials.Ribbon, centre,
                DescriptionRibbonHeight, label.fontSize * DescriptionScale, DescriptionFlight);
            var front = CreateLayer("Front", balloon, materials.Rubber);
            var pin = CreatePin(root, materials.Pin);

            var view = root.gameObject.AddComponent<CardRevealView>();
            var fields = new SerializedObject(view);
            fields.FindProperty("_group").objectReferenceValue = group;
            fields.FindProperty("_balloon").objectReferenceValue = balloon;
            fields.FindProperty("_shadow").objectReferenceValue = shadow;
            fields.FindProperty("_back").objectReferenceValue = back;
            fields.FindProperty("_front").objectReferenceValue = front;
            fields.FindProperty("_titleRibbon").objectReferenceValue = title;
            fields.FindProperty("_descriptionRibbon").objectReferenceValue = description;
            if (pin.HasValue)
            {
                fields.FindProperty("_pin").objectReferenceValue = pin.Value.Root;
                fields.FindProperty("_pinHead").objectReferenceValue = pin.Value.Head;
                fields.FindProperty("_pinNeedle").objectReferenceValue = pin.Value.Needle;
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        // Over the balloon and its ribbons, beside it rather than in it: the view lays it out from the settings
        // (its size, its tip) and moves it to the puncture. The needle under the head.
        private static PinImages? CreatePin(RectTransform root, Material material)
        {
            var sheet = ImportAsData(PinSheetPath);
            if (sheet == null || material == null)
            {
                Debug.LogWarning($"[CardReveal] No {PinSheetPath} (bake it with Tools/art/balloon_pop/bake_pin.py); " +
                                 "the balloon pops with no pin.");
                return null;
            }

            var pin = CreateRect("Pin", root);
            var needle = CreatePinPart("Needle", pin, sheet, material, PinNeedleCell);
            var head = CreatePinPart("Head", pin, sheet, material, PinHeadCell);
            return new PinImages(pin, head, needle);
        }

        private static RawImage CreatePinPart(string name, RectTransform pin, Texture2D sheet, Material material,
            Rect cell)
        {
            var image = CreateLayer(name, pin, material);
            image.texture = sheet;
            image.uvRect = cell;
            image.enabled = false;
            return image;
        }

        private static RawImage CreateLayer(string name, RectTransform parent, Material material)
        {
            var rect = CreateRect(name, parent);
            Stretch(rect);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.material = material;
            image.raycastTarget = false;
            return image;
        }

        // Its flight is relative to the balloon's centre in the frame (centre). The text is the tooltip's label,
        // cloned for its pencil font and colour, without its write-on: the line is on the ribbon from the start.
        private static RibbonPiece CreateRibbon(string name, RectTransform balloon, TMP_Text label, Material material,
            Vector2 centre, float height, float fontSize, Flight flight)
        {
            var rect = CreateRect(name, balloon);
            rect.sizeDelta = new Vector2(60f, height);
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var ribbon = rect.gameObject.AddComponent<RibbonGraphic>();
            ribbon.color = RibbonColor;
            ribbon.material = material;
            ribbon.raycastTarget = false;

            var clone = Object.Instantiate(label.gameObject, rect, false);
            clone.name = "Text";
            Object.DestroyImmediate(clone.GetComponent<WriteOnText>());
            var textRect = (RectTransform)clone.transform;
            Stretch(textRect);

            // Centred, so a glyph's x is measured from the ribbon's centre, as the ribbon's wave is.
            textRect.pivot = new Vector2(0.5f, 0.5f);
            var text = clone.GetComponent<TMP_Text>();
            text.text = string.Empty;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.raycastTarget = false;

            var piece = rect.gameObject.AddComponent<RibbonPiece>();
            var fields = new SerializedObject(piece);
            fields.FindProperty("_ribbon").objectReferenceValue = ribbon;
            fields.FindProperty("_text").objectReferenceValue = text;
            fields.FindProperty("_group").objectReferenceValue = group;
            fields.FindProperty("_restPosition").vector2Value = centre + flight.Rest;
            fields.FindProperty("_restTilt").floatValue = flight.RestTilt;
            fields.FindProperty("_burstTo").vector2Value = centre + flight.BurstTo;
            fields.FindProperty("_tilt").floatValue = flight.Tilt;
            fields.FindProperty("_drift").vector2Value = flight.Drift;
            fields.FindProperty("_swayPhase").floatValue = flight.SwayPhase;
            fields.FindProperty("_exitBy").vector2Value = flight.ExitBy;
            fields.FindProperty("_exitSpin").floatValue = flight.ExitSpin;
            fields.ApplyModifiedPropertiesWithoutUndo();
            rect.anchoredPosition = centre + flight.Rest;
            return piece;
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

        // The reveal's materials: the rubber, its drop shadow (the same shader with Drop Shadow on), the ribbons, and
        // the pin (the same shader, without the glow; none if the shader is missing).
        private readonly struct Materials
        {
            public readonly Material Rubber;
            public readonly Material Shadow;
            public readonly Material Ribbon;
            public readonly Material Pin;

            public Materials(Material rubber, Material shadow, Material ribbon, Material pin)
            {
                Rubber = rubber;
                Shadow = shadow;
                Ribbon = ribbon;
                Pin = pin;
            }
        }

        private readonly struct PinImages
        {
            public readonly RectTransform Root;
            public readonly RawImage Head;
            public readonly RawImage Needle;

            public PinImages(RectTransform root, RawImage head, RawImage needle)
            {
                Root = root;
                Head = head;
                Needle = needle;
            }
        }

        // A ribbon's flight, in canvas units and degrees: rolled up inside, where the burst takes it, its drift and
        // sway, and its way off.
        private readonly struct Flight
        {
            internal readonly Vector2 Rest;
            internal readonly float RestTilt;
            internal readonly Vector2 BurstTo;
            internal readonly float Tilt;
            internal readonly Vector2 Drift;
            internal readonly float SwayPhase;
            internal readonly Vector2 ExitBy;
            internal readonly float ExitSpin;

            internal Flight(Vector2 rest, float restTilt, Vector2 burstTo, float tilt, Vector2 drift, float swayPhase,
                Vector2 exitBy, float exitSpin)
            {
                Rest = rest;
                RestTilt = restTilt;
                BurstTo = burstTo;
                Tilt = tilt;
                Drift = drift;
                SwayPhase = swayPhase;
                ExitBy = exitBy;
                ExitSpin = exitSpin;
            }
        }
    }
}
