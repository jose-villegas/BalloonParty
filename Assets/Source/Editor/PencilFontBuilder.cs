using BalloonParty.UI.Tooltip;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Makes the heads-ups' pencil font in one step: the Patrick Hand TMP font asset (created by TMP's own
    ///     "Font Asset > SDF" command, so it matches one made by hand) on the pencil text shader, then sets it on
    ///     the tooltip's label. Running it again reuses the existing font asset and only re-assigns it.
    /// </summary>
    internal static class PencilFontBuilder
    {
        private const string MenuPath = "Tools/BalloonParty/HeadsUp/Build Pencil Font";
        private const string DialogTitle = "Pencil font";
        private const string SourceFontPath = "Assets/Fonts/PatrickHand-Regular.ttf";

        // Where TMP's SDF command saves an asset made from SourceFontPath.
        private const string FontAssetPath = "Assets/Fonts/PatrickHand-Regular SDF.asset";
        private const string CreateSdfMenu = "Assets/Create/TextMeshPro/Font Asset/SDF";
        private const string PencilShaderPath = "Assets/Shaders/BalloonParty/Text/PencilText.shader";
        private const string NoiseTexturePath = "Assets/Textures/Text/pencil-noise.png";

        [MenuItem(MenuPath)]
        private static void Build()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) ?? CreateFontAsset();
            if (fontAsset == null)
            {
                return;
            }

            ApplyPencilShader(fontAsset);
            var prefabPath = PrefabRebuild.FindPrefabWithChild<TooltipBanner>("Tooltip");
            if (prefabPath == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "The tooltip prefab wasn't found.", "OK");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var banner = root.GetComponentInChildren<TooltipBanner>(true);
                var label = new SerializedObject(banner).FindProperty("_label").objectReferenceValue as TMP_Text;
                if (label == null)
                {
                    EditorUtility.DisplayDialog(DialogTitle, "The tooltip has no label assigned.", "OK");
                    return;
                }

                label.font = fontAsset;
                label.fontSharedMaterial = fontAsset.material;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[{DialogTitle}] Set {fontAsset.name} on the label of {prefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static TMP_FontAsset CreateFontAsset()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, $"{SourceFontPath} wasn't found.", "OK");
                return null;
            }

            var previousSelection = Selection.objects;
            Selection.objects = new Object[] { sourceFont };
            try
            {
                EditorApplication.ExecuteMenuItem(CreateSdfMenu);
            }
            finally
            {
                Selection.objects = previousSelection;
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    $"TMP didn't create {FontAssetPath}. Make it by hand: right-click {SourceFontPath} > Create > " +
                    "TextMeshPro > Font Asset > SDF, then run this again.", "OK");
                return null;
            }

            Debug.Log($"[{DialogTitle}] Created {FontAssetPath}.");
            return fontAsset;
        }

        // The font's own material is what every Patrick Hand text draws with, so the pencil goes there. Only
        // the shader and missing textures are set: an existing material keeps its tuned values.
        private static void ApplyPencilShader(TMP_FontAsset fontAsset)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(PencilShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[{DialogTitle}] No pencil shader at {PencilShaderPath}; the font keeps {fontAsset.material.shader.name}.");
                return;
            }

            var material = fontAsset.material;
            material.shader = shader;
            SetTextureIfMissing(material, "_PencilNoiseTex", NoiseTexturePath);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssetIfDirty(fontAsset);
        }

        private static void SetTextureIfMissing(Material material, string property, string path)
        {
            if (material.GetTexture(property) != null)
            {
                return;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning($"[{DialogTitle}] No texture at {path}; {property} is left empty.");
                return;
            }

            material.SetTexture(property, texture);
        }
    }
}
