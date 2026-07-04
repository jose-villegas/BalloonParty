using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BalloonParty.Shared.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BalloonParty.Editor.SpriteCombine
{
    /// <summary>
    ///     Bake logic for <see cref="SpriteLayerCombiner"/>: flattens the assigned sprite
    ///     layers, in sorting order and with their relative transforms, into one straight-alpha
    ///     PNG sprite whose pivot lands on the combiner's transform — assign it to a renderer
    ///     there and the composition is pixel-identical. Rendering machinery mirrors
    ///     <c>SpriteShadowBakerEditor</c> (CommandBuffer.DrawRenderer through per-texture plain
    ///     sprite materials, platform row flip on readback).
    /// </summary>
    [CustomEditor(typeof(SpriteLayerCombiner))]
    internal sealed class SpriteLayerCombinerEditor : UnityEditor.Editor
    {
        private const string OutputRoot = "Assets/Sprites/Baked/Combined";
        private const int PadPixels = 2;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Bakes the assigned layers into one sprite whose pivot lands on this " +
                "transform — assign it to a renderer here and the layout is unchanged. " +
                "Wiring (sprite swap, disabling the source layers) is manual.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Bake Combined Sprite"))
                {
                    Bake((SpriteLayerCombiner)target);
                }
            }
        }

        private static void Bake(SpriteLayerCombiner clicked)
        {
            var assetPath = ResolvePrefabPath(clicked.gameObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("[SpriteLayerCombiner] Not part of a prefab — the output path " +
                               "mirrors the prefab's location.");
                return;
            }

            // A prefab can host several combiners (e.g. Body + Shine groups) — find the
            // clicked one inside the isolated prefab copy by index.
            var combinerIndex = Array.IndexOf(
                clicked.transform.root.GetComponentsInChildren<SpriteLayerCombiner>(true), clicked);

            var contents = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var combiner = contents.GetComponentsInChildren<SpriteLayerCombiner>(true)[combinerIndex];
                var layers = CollectLayers(combiner);
                if (layers == null)
                {
                    return;
                }

                WarnOnResolutionMismatch(layers);

                if (combiner.NeutralizeTint)
                {
                    // The isolated copy is discarded on unload, so no restore is needed.
                    foreach (var layer in layers)
                    {
                        layer.color = new Color(1f, 1f, 1f, layer.color.a);
                    }
                }

                var sprite = BakeCombinedSprite(combiner, layers, assetPath);
                EditorGUIUtility.PingObject(sprite);
                Debug.Log($"[SpriteLayerCombiner] Flattened {layers.Count} layers of {assetPath} → " +
                          $"{AssetDatabase.GetAssetPath(sprite)}", sprite);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static List<SpriteRenderer> CollectLayers(SpriteLayerCombiner combiner)
        {
            if (combiner.Layers == null || combiner.Layers.Count == 0)
            {
                Debug.LogError("[SpriteLayerCombiner] No layers assigned.");
                return null;
            }

            var layers = combiner.Layers
                .Where(layer => layer != null && layer.sprite != null)
                .OrderBy(layer => layer.sortingOrder)
                .ToList();

            if (layers.Count == 0)
            {
                Debug.LogError("[SpriteLayerCombiner] Assigned layers have no sprites.");
                return null;
            }

            return layers;
        }

        private static Sprite BakeCombinedSprite(
            SpriteLayerCombiner combiner, IReadOnlyList<SpriteRenderer> layers, string prefabPath)
        {
            var pixelsPerUnit = layers[0].sprite.pixelsPerUnit * combiner.ResolutionMultiplier;
            var bounds = CombinedBounds(layers);

            var width = Mathf.CeilToInt(bounds.size.x * pixelsPerUnit) + 2 * PadPixels;
            var height = Mathf.CeilToInt(bounds.size.y * pixelsPerUnit) + 2 * PadPixels;
            var padWorld = PadPixels / pixelsPerUnit;

            var pixels = RenderLayers(layers, bounds, padWorld, width, height);

            var pngPath = OutputPathFor(prefabPath, combiner.OutputSuffix);
            var pivot = new Vector2(
                (combiner.transform.position.x - (bounds.min.x - padWorld)) * pixelsPerUnit / width,
                (combiner.transform.position.y - (bounds.min.y - padWorld)) * pixelsPerUnit / height);

            WritePng(pngPath, pixels, width, height);
            ImportAsSprite(pngPath, pixelsPerUnit, pivot);
            return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        }

        private static Bounds CombinedBounds(IReadOnlyList<SpriteRenderer> layers)
        {
            var bounds = layers[0].bounds;
            for (var i = 1; i < layers.Count; i++)
            {
                bounds.Encapsulate(layers[i].bounds);
            }

            return bounds;
        }

        // Draws each layer's own mesh (vertex colors carry the renderer tint) through a plain
        // sprite material with the sprite texture bound explicitly — DrawRenderer's material
        // override does not carry the SpriteRenderer's implicit texture binding.
        private static Color[] RenderLayers(
            IReadOnlyList<SpriteRenderer> layers, Bounds bounds, float padWorld, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var materials = new Dictionary<Texture, Material>();
            var plainShader = Shader.Find("Sprites/Default");

            var projection = Matrix4x4.Ortho(
                bounds.min.x - padWorld, bounds.max.x + padWorld,
                bounds.min.y - padWorld, bounds.max.y + padWorld,
                -100f, 100f);

            var cmd = new CommandBuffer { name = "SpriteLayerCombine" };
            cmd.SetRenderTarget(rt);
            cmd.ClearRenderTarget(true, true, Color.clear);
            cmd.SetViewProjectionMatrices(
                Matrix4x4.Scale(new Vector3(1f, 1f, -1f)),
                GL.GetGPUProjectionMatrix(projection, true));

            foreach (var layer in layers)
            {
                var texture = layer.sprite.texture;
                if (!materials.TryGetValue(texture, out var material))
                {
                    material = new Material(plainShader) { mainTexture = texture };
                    materials[texture] = material;
                }

                cmd.DrawRenderer(layer, material);
            }

            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            foreach (var material in materials.Values)
            {
                Object.DestroyImmediate(material);
            }

            var raw = readback.GetPixels();
            Object.DestroyImmediate(readback);

            // Metal/DX render top-down into RTs while ReadPixels assumes bottom-up rows —
            // flip during extraction so the bake matches world orientation. Alpha-blending
            // over a clear target leaves premultiplied color — divide it back out, since
            // sprites want straight alpha.
            var flip = SystemInfo.graphicsUVStartsAtTop;
            var pixels = new Color[raw.Length];
            for (var y = 0; y < height; y++)
            {
                var source = y * width;
                var destination = (flip ? height - 1 - y : y) * width;
                for (var x = 0; x < width; x++)
                {
                    var c = raw[source + x];
                    if (c.a > 0.0001f)
                    {
                        c.r = Mathf.Clamp01(c.r / c.a);
                        c.g = Mathf.Clamp01(c.g / c.a);
                        c.b = Mathf.Clamp01(c.b / c.a);
                    }

                    pixels[destination + x] = c;
                }
            }

            return pixels;
        }

        private static void WarnOnResolutionMismatch(IReadOnlyList<SpriteRenderer> layers)
        {
            var reference = layers[0].sprite.pixelsPerUnit;
            foreach (var layer in layers)
            {
                if (!Mathf.Approximately(layer.sprite.pixelsPerUnit, reference))
                {
                    Debug.LogWarning($"[SpriteLayerCombiner] '{layer.name}' has PPU " +
                                     $"{layer.sprite.pixelsPerUnit} vs {reference} — the bake uses " +
                                     "the first layer's PPU; lower-res layers soften.", layer);
                }
            }
        }

        private static string ResolvePrefabPath(GameObject gameObject)
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

        private static string OutputPathFor(string prefabPath, string suffix)
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

            var cleanSuffix = string.IsNullOrWhiteSpace(suffix) ? "Combined" : suffix.Trim();
            return $"{directory}/{Path.GetFileNameWithoutExtension(prefabPath)}_{cleanSuffix}.png";
        }

        private static void WritePng(string path, Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        private static void ImportAsSprite(string path, float pixelsPerUnit, Vector2 pivot)
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
    }
}
