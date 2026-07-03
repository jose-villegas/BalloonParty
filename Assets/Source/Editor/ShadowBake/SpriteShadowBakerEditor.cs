using System.Collections.Generic;
using System.IO;
using BalloonParty.Shared.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalloonParty.Editor.ShadowBake
{
    /// <summary>
    ///     Inspector for <see cref="SpriteShadowBaker"/>: the Bake button renders the union
    ///     silhouette of every child sprite (via CommandBuffer.DrawRenderer — no camera or
    ///     scene needed, so it works on loaded prefab contents), blurs it offline, writes the
    ///     sprite to Assets/Sprites/Baked/Shadows mirroring the prefab path, then edits the
    ///     prefab: optional material swap (+ _SpriteScale size compensation) and a shadow child
    ///     displaying the bake. All edits go through LoadPrefabContents/SaveAsPrefabAsset so
    ///     the bake works from a scene instance, the prefab stage, or the asset itself.
    /// </summary>
    [CustomEditor(typeof(SpriteShadowBaker))]
    internal sealed class SpriteShadowBakerEditor : UnityEditor.Editor
    {
        private const string OutputRoot = "Assets/Sprites/Baked/Shadows";
        private const string ShadowChildName = "BakedShadow";
        private const string FamilyShaderPrefix = "BalloonParty/Sprite/";

        private static readonly int SpriteScaleId = Shader.PropertyToID("_SpriteScale");

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Bake"))
            {
                Bake((SpriteShadowBaker)target);
            }
        }

        private static void Bake(SpriteShadowBaker clicked)
        {
            var assetPath = ResolvePrefabPath(clicked.gameObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("[SpriteShadowBaker] Not part of a prefab — bake edits the prefab asset.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var baker = contents.GetComponentInChildren<SpriteShadowBaker>(true);
                var renderers = CollectSourceRenderers(baker);
                if (renderers.Count == 0)
                {
                    Debug.LogError($"[SpriteShadowBaker] {assetPath}: no child sprites to bake.");
                    return;
                }

                var sprite = BakeShadowSprite(baker, renderers, assetPath);
                if (sprite == null)
                {
                    return;
                }

                if (baker.ReplaceShadowMaterials)
                {
                    ReplaceFamilyMaterials(renderers, baker.ReplacementMaterial);
                }

                WireShadowChild(baker, renderers, sprite);

                PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                EditorGUIUtility.PingObject(sprite);
                Debug.Log($"[SpriteShadowBaker] Baked shadow for {assetPath} → {AssetDatabase.GetAssetPath(sprite)}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
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

        private static List<SpriteRenderer> CollectSourceRenderers(SpriteShadowBaker baker)
        {
            var result = new List<SpriteRenderer>();
            foreach (var renderer in baker.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (renderer.enabled && renderer.sprite != null && renderer != baker.ShadowChild)
                {
                    result.Add(renderer);
                }
            }

            return result;
        }

        private static Sprite BakeShadowSprite(
            SpriteShadowBaker baker, IReadOnlyList<SpriteRenderer> renderers, string prefabPath)
        {
            var pixelsPerUnit = renderers[0].sprite.pixelsPerUnit * baker.ResolutionMultiplier;
            var bounds = CombinedBounds(renderers);

            // Box blur applied N times spreads N × radius — pad so the penumbra never clips.
            var blurPixels = Mathf.CeilToInt(baker.BlurRadius * pixelsPerUnit);
            var padPixels = blurPixels * baker.BlurPasses + 2;

            var width = Mathf.CeilToInt(bounds.size.x * pixelsPerUnit) + 2 * padPixels;
            var height = Mathf.CeilToInt(bounds.size.y * pixelsPerUnit) + 2 * padPixels;
            var padWorld = padPixels / pixelsPerUnit;

            var alpha = RenderSilhouetteAlpha(renderers, bounds, padWorld, width, height);
            for (var pass = 0; pass < baker.BlurPasses; pass++)
            {
                BoxBlur(alpha, width, height, blurPixels);
            }

            var pngPath = OutputPathFor(prefabPath);
            var pivot = new Vector2(
                (baker.transform.position.x - (bounds.min.x - padWorld)) * pixelsPerUnit / width,
                (baker.transform.position.y - (bounds.min.y - padWorld)) * pixelsPerUnit / height);

            WriteShadowPng(pngPath, alpha, width, height, baker.ShadowColor);
            ImportAsSprite(pngPath, pixelsPerUnit, pivot);
            return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        }

        private static Bounds CombinedBounds(IReadOnlyList<SpriteRenderer> renderers)
        {
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Count; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        // Draws each renderer's own mesh (handles sliced/tiled draw modes) through a plain
        // sprite material with the sprite texture bound explicitly — DrawRenderer's material
        // override does not carry the SpriteRenderer's implicit texture binding.
        private static float[] RenderSilhouetteAlpha(
            IReadOnlyList<SpriteRenderer> renderers, Bounds bounds, float padWorld, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var materials = new Dictionary<Texture, Material>();
            var plainShader = Shader.Find("Sprites/Default");

            var projection = Matrix4x4.Ortho(
                bounds.min.x - padWorld, bounds.max.x + padWorld,
                bounds.min.y - padWorld, bounds.max.y + padWorld,
                -100f, 100f);

            var cmd = new CommandBuffer { name = "SpriteShadowBake" };
            cmd.SetRenderTarget(rt);
            cmd.ClearRenderTarget(true, true, Color.clear);
            cmd.SetViewProjectionMatrices(
                Matrix4x4.Scale(new Vector3(1f, 1f, -1f)),
                GL.GetGPUProjectionMatrix(projection, true));

            foreach (var renderer in renderers)
            {
                var texture = renderer.sprite.texture;
                if (!materials.TryGetValue(texture, out var material))
                {
                    material = new Material(plainShader) { mainTexture = texture };
                    materials[texture] = material;
                }

                cmd.DrawRenderer(renderer, material);
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

            var pixels = readback.GetPixels();
            Object.DestroyImmediate(readback);

            var alpha = new float[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                alpha[i] = pixels[i].a;
            }

            return alpha;
        }

        // Separable box blur with a running-sum window — O(pixels) per pass regardless of
        // radius, so generous radii stay instant at bake time.
        private static void BoxBlur(float[] alpha, int width, int height, int radius)
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

        private static float SampleClamped(float[] data, int rowOffset, int x, int width)
        {
            return data[rowOffset + Mathf.Clamp(x, 0, width - 1)];
        }

        private static float SampleColumnClamped(float[] data, int x, int y, int width, int height)
        {
            return data[Mathf.Clamp(y, 0, height - 1) * width + x];
        }

        private static string OutputPathFor(string prefabPath)
        {
            // Mirror the prefab's folder under the output root so names can't collide:
            // Assets/Prefabs/Balloon/Balloon.prefab → .../Shadows/Prefabs/Balloon/Balloon_Shadow.png
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

        private static void WriteShadowPng(string path, float[] alpha, int width, int height, Color shadowColor)
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

        private static void ReplaceFamilyMaterials(IReadOnlyList<SpriteRenderer> renderers, Material replacement)
        {
            var plain = replacement != null
                ? replacement
                : AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            foreach (var renderer in renderers)
            {
                var material = renderer.sharedMaterial;
                if (material == null || !material.shader.name.StartsWith(FamilyShaderPrefix))
                {
                    continue;
                }

                // The shadow shaders shrank the sprite inside its quad by _SpriteScale; a plain
                // material renders the full quad, so shrink the renderer to keep the visible size.
                var spriteScale = material.HasProperty(SpriteScaleId) ? material.GetFloat(SpriteScaleId) : 1f;
                renderer.sharedMaterial = plain;

                if (Mathf.Approximately(spriteScale, 1f))
                {
                    continue;
                }

                if (renderer.drawMode != SpriteDrawMode.Simple)
                {
                    renderer.size *= spriteScale;
                }
                else
                {
                    renderer.transform.localScale *= spriteScale;
                    Debug.LogWarning($"[SpriteShadowBaker] '{renderer.name}': simple draw mode — compensated " +
                                     $"_SpriteScale {spriteScale:0.###} via localScale; check any children.");
                }
            }
        }

        private static void WireShadowChild(
            SpriteShadowBaker baker, IReadOnlyList<SpriteRenderer> renderers, Sprite sprite)
        {
            var shadow = baker.ShadowChild;
            if (shadow == null)
            {
                var go = new GameObject(ShadowChildName);
                go.transform.SetParent(baker.transform, false);
                shadow = go.AddComponent<SpriteRenderer>();
                baker.ShadowChild = shadow;
            }

            var lowest = renderers[0];
            foreach (var renderer in renderers)
            {
                if (renderer.sortingOrder < lowest.sortingOrder)
                {
                    lowest = renderer;
                }
            }

            shadow.sprite = sprite;
            shadow.sharedMaterial = baker.ReplacementMaterial != null
                ? baker.ReplacementMaterial
                : AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            shadow.sortingLayerID = lowest.sortingLayerID;
            shadow.sortingOrder = lowest.sortingOrder - 1;
            shadow.transform.position = baker.transform.position + (Vector3)baker.ShadowOffset;
        }
    }
}
