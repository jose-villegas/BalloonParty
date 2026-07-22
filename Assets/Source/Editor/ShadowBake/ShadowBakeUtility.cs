using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BalloonParty.Editor.ShadowBake
{
    /// <summary>
    ///     Shared bake-pipeline utilities for shadow bakers — blur, PNG output, sprite import,
    ///     and RT readback. Stateless; each baker owns its own silhouette rendering and child wiring.
    /// </summary>
    internal static class ShadowBakeUtility
    {
        private const string OutputRoot = "Assets/Sprites/Baked/Shadows";

        internal static string ResolvePrefabPath(GameObject gameObject)
        {
            var stage = PrefabStageUtility.GetPrefabStage(gameObject);
            if (stage != null)
            {
                return stage.assetPath;
            }

            var instancePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
            if (!string.IsNullOrEmpty(instancePath))
            {
                return instancePath;
            }

            return AssetDatabase.GetAssetPath(gameObject);
        }

        internal static string OutputPathFor(string prefabPath)
        {
            var relativeDir = Path.GetDirectoryName(prefabPath);
            if (!string.IsNullOrEmpty(relativeDir))
            {
                relativeDir = relativeDir.Replace('\\', '/');
                if (relativeDir.StartsWith("Assets/"))
                {
                    relativeDir = relativeDir.Substring("Assets/".Length);
                }
            }

            var directory = string.IsNullOrEmpty(relativeDir) ? OutputRoot : $"{OutputRoot}/{relativeDir}";
            Directory.CreateDirectory(directory);
            return $"{directory}/{Path.GetFileNameWithoutExtension(prefabPath)}_Shadow.png";
        }

        /// <summary>
        ///     Reads back a RenderTexture into a float alpha array, handling the platform-dependent
        ///     vertical flip (Metal/DX render top-down but ReadPixels assumes bottom-up).
        /// </summary>
        internal static float[] ReadbackAlpha(RenderTexture rt, int width, int height)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = previous;

            var pixels = readback.GetPixels();
            Object.DestroyImmediate(readback);

            var flip = SystemInfo.graphicsUVStartsAtTop;
            var alpha = new float[pixels.Length];
            for (var y = 0; y < height; y++)
            {
                var source = y * width;
                var destination = (flip ? height - 1 - y : y) * width;
                for (var x = 0; x < width; x++)
                {
                    alpha[destination + x] = pixels[source + x].a;
                }
            }

            return alpha;
        }

        /// <summary>Separable box blur with a running-sum window — O(pixels) per pass regardless of radius.</summary>
        internal static void BoxBlur(float[] alpha, int width, int height, int radius)
        {
            if (radius <= 0)
            {
                return;
            }

            var window = 2 * radius + 1;
            var scratch = new float[alpha.Length];

            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                var sum = 0f;
                for (var x = -radius; x <= radius; x++)
                {
                    sum += SampleClamped(alpha, row, x, width);
                }

                for (var x = 0; x < width; x++)
                {
                    scratch[row + x] = sum / window;
                    sum += SampleClamped(alpha, row, x + radius + 1, width) -
                           SampleClamped(alpha, row, x - radius, width);
                }
            }

            for (var x = 0; x < width; x++)
            {
                var sum = 0f;
                for (var y = -radius; y <= radius; y++)
                {
                    sum += SampleColumnClamped(scratch, x, y, width, height);
                }

                for (var y = 0; y < height; y++)
                {
                    alpha[y * width + x] = sum / window;
                    sum += SampleColumnClamped(scratch, x, y + radius + 1, width, height) -
                           SampleColumnClamped(scratch, x, y - radius, width, height);
                }
            }
        }

        internal static void WriteShadowPng(string path, float[] alpha, int width, int height, Color shadowColor)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[alpha.Length];
            var rgb = (Color32)shadowColor;

            for (var i = 0; i < alpha.Length; i++)
            {
                var a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[i] * shadowColor.a) * 255f);
                pixels[i] = new Color32(rgb.r, rgb.g, rgb.b, a);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        internal static void ImportAsSprite(string path, float pixelsPerUnit, Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static float SampleClamped(float[] data, int rowOffset, int x, int width)
        {
            return data[rowOffset + Mathf.Clamp(x, 0, width - 1)];
        }

        private static float SampleColumnClamped(float[] data, int x, int y, int width, int height)
        {
            return data[Mathf.Clamp(y, 0, height - 1) * width + x];
        }
    }
}
