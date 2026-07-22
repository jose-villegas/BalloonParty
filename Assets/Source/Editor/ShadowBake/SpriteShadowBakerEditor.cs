using System.Collections.Generic;
using BalloonParty.Shared.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalloonParty.Editor.ShadowBake
{
    /// <summary>Inspector for <see cref="SpriteShadowBaker"/>: bakes a blurred silhouette sprite and wires it into the prefab.</summary>
    [CustomEditor(typeof(SpriteShadowBaker))]
    internal sealed class SpriteShadowBakerEditor : UnityEditor.Editor
    {
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
            var assetPath = ShadowBakeUtility.ResolvePrefabPath(clicked.gameObject);
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

                WarnOnResolutionMismatch(renderers);

                // Shrink family-material quads to visible size before the silhouette pass, or they bake oversized.
                var compensations = CompensateSpriteScale(renderers);

                var sprite = BakeShadowSprite(baker, renderers, assetPath);
                if (sprite == null)
                {
                    return;
                }

                if (baker.ReplaceShadowMaterials)
                {
                    SwapFamilyMaterials(renderers, baker.ReplacementMaterial);
                }
                else
                {
                    RestoreScaleCompensation(compensations);
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

            // N blur passes spread N × radius — pad so the penumbra never clips.
            var blurPixels = Mathf.CeilToInt(baker.BlurRadius * pixelsPerUnit);
            var padPixels = blurPixels * baker.BlurPasses + 2;

            var width = Mathf.CeilToInt(bounds.size.x * pixelsPerUnit) + 2 * padPixels;
            var height = Mathf.CeilToInt(bounds.size.y * pixelsPerUnit) + 2 * padPixels;
            var padWorld = padPixels / pixelsPerUnit;

            var alpha = RenderSilhouetteAlpha(renderers, bounds, padWorld, width, height);
            for (var pass = 0; pass < baker.BlurPasses; pass++)
            {
                ShadowBakeUtility.BoxBlur(alpha, width, height, blurPixels);
            }

            var pngPath = ShadowBakeUtility.OutputPathFor(prefabPath);
            var pivot = new Vector2(
                (baker.transform.position.x - (bounds.min.x - padWorld)) * pixelsPerUnit / width,
                (baker.transform.position.y - (bounds.min.y - padWorld)) * pixelsPerUnit / height);

            ShadowBakeUtility.WriteShadowPng(pngPath, alpha, width, height, baker.ShadowColor);
            ShadowBakeUtility.ImportAsSprite(pngPath, pixelsPerUnit, pivot);
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

        // DrawRenderer's material override drops the SpriteRenderer's implicit texture, so bind it explicitly.
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

            var alpha = ShadowBakeUtility.ReadbackAlpha(rt, width, height);
            RenderTexture.ReleaseTemporary(rt);

            foreach (var material in materials.Values)
            {
                Object.DestroyImmediate(material);
            }

            return alpha;
        }

        private sealed class ScaleCompensation
        {
            public SpriteRenderer Renderer;
            public bool UsedRendererSize;
            public Vector2 PreviousSize;
            public Vector3 PreviousScale;
        }

        // Bake resolution follows the first sprite's PPU; warn if others differ.
        private static void WarnOnResolutionMismatch(IReadOnlyList<SpriteRenderer> renderers)
        {
            var reference = renderers[0].sprite.pixelsPerUnit;
            for (var i = 1; i < renderers.Count; i++)
            {
                var pixelsPerUnit = renderers[i].sprite.pixelsPerUnit;
                if (!Mathf.Approximately(pixelsPerUnit, reference))
                {
                    Debug.LogWarning($"[SpriteShadowBaker] Resolution mismatch: '{renderers[i].name}' " +
                                     $"(sprite '{renderers[i].sprite.name}', {pixelsPerUnit} PPU) differs from " +
                                     $"'{renderers[0].name}' ({reference} PPU) — the bake resolves at {reference}; " +
                                     "align the sprites' import settings for a uniform penumbra.");
                }
            }
        }

        // Shrinks the renderer to its _SpriteScale-visible size; persisted if materials are swapped, reverted otherwise.
        private static List<ScaleCompensation> CompensateSpriteScale(IReadOnlyList<SpriteRenderer> renderers)
        {
            var compensations = new List<ScaleCompensation>();

            foreach (var renderer in renderers)
            {
                var material = renderer.sharedMaterial;
                if (material == null || !material.shader.name.StartsWith(FamilyShaderPrefix) ||
                    !material.HasProperty(SpriteScaleId))
                {
                    continue;
                }

                var spriteScale = material.GetFloat(SpriteScaleId);
                if (Mathf.Approximately(spriteScale, 1f))
                {
                    continue;
                }

                var compensation = new ScaleCompensation
                {
                    Renderer = renderer,
                    UsedRendererSize = renderer.drawMode != SpriteDrawMode.Simple,
                    PreviousSize = renderer.drawMode != SpriteDrawMode.Simple ? renderer.size : Vector2.zero,
                    PreviousScale = renderer.transform.localScale
                };

                if (compensation.UsedRendererSize)
                {
                    renderer.size *= spriteScale;
                }
                else
                {
                    renderer.transform.localScale *= spriteScale;
                    Debug.LogWarning($"[SpriteShadowBaker] '{renderer.name}': simple draw mode — compensated " +
                                     $"_SpriteScale {spriteScale:0.###} via localScale; check any children.");
                }

                compensations.Add(compensation);
            }

            return compensations;
        }

        private static void RestoreScaleCompensation(IReadOnlyList<ScaleCompensation> compensations)
        {
            foreach (var compensation in compensations)
            {
                if (compensation.UsedRendererSize)
                {
                    compensation.Renderer.size = compensation.PreviousSize;
                }
                else
                {
                    compensation.Renderer.transform.localScale = compensation.PreviousScale;
                }
            }
        }

        private static void SwapFamilyMaterials(IReadOnlyList<SpriteRenderer> renderers, Material replacement)
        {
            var plain = replacement != null
                ? replacement
                : AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            foreach (var renderer in renderers)
            {
                var material = renderer.sharedMaterial;
                if (material != null && material.shader.name.StartsWith(FamilyShaderPrefix))
                {
                    renderer.sharedMaterial = plain;
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
