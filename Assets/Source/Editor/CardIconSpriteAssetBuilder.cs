using BalloonParty.Shared;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Makes the <c>CardIcons</c> TextMeshPro sprite asset the card titles draw their icons from
    ///     (<see cref="UpgradeCardText" />): one sprite per square cell of the atlas that
    ///     <c>Tools/art/make_card_icons.py</c> draws, named by its <see cref="UpgradeKind" />. Running it again
    ///     rebuilds the tables in the same asset, so the asset keeps its GUID.
    /// </summary>
    internal static class CardIconSpriteAssetBuilder
    {
        private const string MenuPath = "Tools/BalloonParty/Upgrades/Build Card Icons";
        private const string AtlasPath = "Assets/Sprites/UI/CardIcons/card_icons.png";

        // TMP finds a sprite asset named in a <sprite="..."> tag under Resources/<its sprite asset path>.
        private const string AssetFolder = "Assets/Sprites/UI/CardIcons/Resources/Sprite Assets";
        private const string ShaderName = "TextMeshPro/Sprite";
        private const string Version = "1.1.0";

        // How far the icon's top rises above the baseline, of its height: the cell's lower edge sits a little
        // under the baseline, as a letter's descender does, so the drawing centres on the words beside it.
        private const float AboveBaseline = 0.88f;

        // The atlas's cells, left to right, as make_card_icons.py lays them out. SnipeChance is drawn ahead of
        // its card.
        internal static readonly string[] IconNames =
        {
            "BallCap", "FasterReload", "Shield", "ItemOdds", "RaiseLimits", "SnipeChance"
        };

        [MenuItem(MenuPath)]
        internal static void Build()
        {
            var atlas = ImportAtlas();
            if (atlas == null)
            {
                Debug.LogWarning($"[CardIcons] No {AtlasPath}; run Tools/art/make_card_icons.py.");
                return;
            }

            var cell = atlas.height;
            if (atlas.width != cell * IconNames.Length)
            {
                Debug.LogWarning($"[CardIcons] {AtlasPath} is {atlas.width}x{cell}, not {IconNames.Length} square cells.");
                return;
            }

            var assetPath = $"{AssetFolder}/{UpgradeCardText.IconAssetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
            if (asset == null)
            {
                EnsureFolder(AssetFolder);
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            // Set before the material: an asset with a material and no version is taken for a legacy one and
            // "upgraded", which rebuilds its tables from the old sprite list.
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_Version").stringValue = Version;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(asset.name);
            asset.spriteSheet = atlas;
            FillTables(asset, cell);
            asset.material = Material(asset, atlas);
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardIcons] {assetPath}: {IconNames.Length} icons from {AtlasPath}.");
        }

        // Pointsize left at 0, so TMP sizes each icon to the font's ascent: it stands as tall as a capital.
        private static void FillTables(TMP_SpriteAsset asset, int cell)
        {
            var glyphs = asset.spriteGlyphTable;
            var characters = asset.spriteCharacterTable;
            glyphs.Clear();
            characters.Clear();
            for (var i = 0; i < IconNames.Length; i++)
            {
                var metrics = new GlyphMetrics(cell, cell, 0f, cell * AboveBaseline, cell);
                var glyph = new TMP_SpriteGlyph((uint)i, metrics, new GlyphRect(i * cell, 0, cell, cell), 1f, 0);
                glyphs.Add(glyph);
                characters.Add(new TMP_SpriteCharacter(0xFFFE, asset, glyph) { name = IconNames[i] });
            }
        }

        private static Material Material(TMP_SpriteAsset asset, Texture2D atlas)
        {
            var material = asset.material;
            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName)) { name = $"{asset.name} Material" };
                AssetDatabase.AddObjectToAsset(material, asset);
            }

            material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
            return material;
        }

        private static Texture2D ImportAtlas()
        {
            if (AssetImporter.GetAtPath(AtlasPath) is not TextureImporter importer)
            {
                return null;
            }

            // A plain texture, sampled whole by the sprite material: no mips to blur the strokes at small sizes.
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            if (EditorUtility.IsDirty(importer))
            {
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var slash = path.LastIndexOf('/');
            var parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
