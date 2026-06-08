using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    /// <summary>
    /// Bakes N leaf variants via <see cref="BushLeafBaker"/>, packs them into
    /// a square grid atlas, and saves the atlas as a PNG with sprite slicing.
    /// </summary>
    internal static class LeafAtlasPacker
    {
        internal struct PackResult
        {
            internal Texture2D Atlas;
            internal string AssetPath;
        }

        /// <summary>
        /// Bakes leaf variants and packs them into a single atlas texture saved
        /// at <paramref name="outputPath"/>. Returns the atlas and its asset path.
        /// </summary>
        internal static PackResult Pack(BushLeafBakeSettings settings, string outputPath)
        {
            var variantCount = Mathf.Max(1, settings.LeafVariants);
            var leafTextures = BakeAllVariants(settings, variantCount);

            var gridSize = ComputeGridSize(variantCount);
            var atlasWidth = gridSize.x * settings.Resolution;
            var atlasHeight = gridSize.y * settings.Resolution;

            var atlas = ComposeAtlas(leafTextures, settings.Resolution, gridSize, atlasWidth, atlasHeight);

            CleanupLeafTextures(leafTextures);

            var assetPath = SaveAtlasPng(atlas, outputPath);
            ConfigureSpriteImporter(assetPath, variantCount, settings.Resolution, gridSize);

            Object.DestroyImmediate(atlas);

            var importedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            return new PackResult
            {
                Atlas = importedAtlas,
                AssetPath = assetPath
            };
        }

        private static Texture2D[] BakeAllVariants(BushLeafBakeSettings settings, int variantCount)
        {
            var textures = new Texture2D[variantCount];
            for (var i = 0; i < variantCount; i++)
            {
                var seed = (uint)(i * 7919 + 31);
                textures[i] = BushLeafBaker.BakeLeaf(settings, i, seed);
            }

            return textures;
        }

        private static Vector2Int ComputeGridSize(int count)
        {
            var cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            var rows = Mathf.CeilToInt((float)count / cols);
            return new Vector2Int(cols, rows);
        }

        private static Texture2D ComposeAtlas(
            Texture2D[] variants, int cellSize,
            Vector2Int gridSize, int atlasWidth, int atlasHeight)
        {
            var atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
            var clearPixels = new Color[atlasWidth * atlasHeight];
            atlas.SetPixels(clearPixels);

            for (var i = 0; i < variants.Length; i++)
            {
                if (variants[i] == null)
                {
                    continue;
                }

                var col = i % gridSize.x;
                // Pack top-to-bottom so sprite index matches variant index
                var row = gridSize.y - 1 - i / gridSize.x;
                var xOffset = col * cellSize;
                var yOffset = row * cellSize;

                var pixels = variants[i].GetPixels();
                atlas.SetPixels(xOffset, yOffset, cellSize, cellSize, pixels);
            }

            atlas.Apply();
            return atlas;
        }

        private static string SaveAtlasPng(Texture2D atlas, string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!outputPath.EndsWith(".png"))
            {
                outputPath += ".png";
            }

            var bytes = atlas.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[LeafAtlasPacker] Saved atlas to {outputPath}");

            return outputPath;
        }

        private static void ConfigureSpriteImporter(
            string assetPath, int variantCount, int cellSize, Vector2Int gridSize)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var spriteRects = new SpriteRect[variantCount];
            for (var i = 0; i < variantCount; i++)
            {
                var col = i % gridSize.x;
                var row = gridSize.y - 1 - i / gridSize.x;

                spriteRects[i] = new SpriteRect
                {
                    name = $"Leaf_{i:D2}",
                    rect = new Rect(col * cellSize, row * cellSize, cellSize, cellSize),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
            }

            provider.SetSpriteRects(spriteRects);
            provider.Apply();

            importer.SaveAndReimport();
        }

        private static void CleanupLeafTextures(Texture2D[] textures)
        {
            foreach (var tex in textures)
            {
                if (tex != null)
                {
                    Object.DestroyImmediate(tex);
                }
            }
        }
    }
}
