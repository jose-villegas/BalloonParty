using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    /// <summary>
    /// Renders a full multi-slot bush canopy into a <see cref="Texture2D"/>
    /// using <c>BushBake.shader</c> via a temporary offscreen camera.
    /// Uses the same <c>_SlotCentersWorld</c> / <c>_SlotCount</c> MPB contract
    /// as <c>ClusterView.Configure</c>.
    /// </summary>
    internal static class BushCanopyBaker
    {
        private const string ShaderName = "BalloonParty/Grid/BushBake";
        private const int BakeLayer = 31;
        private const int MaxSlots = 16;
        private const float HexSpacingX = 0.7f;
        private const float HexSpacingY = 0.6f;

        private static readonly int SlotCentersWorldId = Shader.PropertyToID("_SlotCentersWorld");
        private static readonly int SlotCountId = Shader.PropertyToID("_SlotCount");
        private static readonly int SlotRadiusId = Shader.PropertyToID("_SlotRadius");
        private static readonly int RadiusJitterId = Shader.PropertyToID("_RadiusJitter");
        private static readonly int BranchSpreadId = Shader.PropertyToID("_BranchSpread");
        private static readonly int SubCircleSizeId = Shader.PropertyToID("_SubCircleSize");
        private static readonly int SubCircleSizeVarId = Shader.PropertyToID("_SubCircleSizeVar");
        private static readonly int GielisMId = Shader.PropertyToID("_GielisM");
        private static readonly int GielisN1Id = Shader.PropertyToID("_GielisN1");
        private static readonly int GielisN2Id = Shader.PropertyToID("_GielisN2");
        private static readonly int GielisN3Id = Shader.PropertyToID("_GielisN3");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
        private static readonly int EdgeShadeId = Shader.PropertyToID("_EdgeShade");
        private static readonly int VeinWidthId = Shader.PropertyToID("_VeinWidth");
        private static readonly int VeinDarkenId = Shader.PropertyToID("_VeinDarken");
        private static readonly int VeinDepthId = Shader.PropertyToID("_VeinDepth");
        private static readonly int VeinCountId = Shader.PropertyToID("_VeinCount");
        private static readonly int SSSAbsorptionId = Shader.PropertyToID("_SSSAbsorption");
        private static readonly int SSSStrengthId = Shader.PropertyToID("_SSSStrength");
        private static readonly int SSSColorId = Shader.PropertyToID("_SSSColor");
        private static readonly int LeafShadowStrengthId = Shader.PropertyToID("_LeafShadowStrength");
        private static readonly int ShadowSamplesId = Shader.PropertyToID("_ShadowSamples");
        private static readonly int AOMulId = Shader.PropertyToID("_AOMul");
        private static readonly int HueJitterId = Shader.PropertyToID("_HueJitter");
        private static readonly int EdgeBrowningWidthId = Shader.PropertyToID("_EdgeBrowningWidth");

        /// <summary>
        /// Bakes a single canopy variant with a deterministic seed controlling
        /// phyllotaxis rotation, Gielis jitter, and hue variation.
        /// </summary>
        internal static Texture2D BakeCanopy(BushCanopyBakeSettings settings, uint seed)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[BushCanopyBaker] Shader '{ShaderName}' not found.");
                return null;
            }

            var slotPositions = GenerateSlotPositions(settings.SlotCount, seed);
            var bounds = ComputeBounds(slotPositions, settings.SlotCount, settings.SlotRadius);

            var rt = RenderTexture.GetTemporary(
                settings.Resolution, settings.Resolution, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            var material = new Material(shader);
            ConfigureMaterial(material, settings, seed);

            var mpb = new MaterialPropertyBlock();
            mpb.SetVectorArray(SlotCentersWorldId, slotPositions);
            mpb.SetInt(SlotCountId, settings.SlotCount);

            var cameraGo = CreateBakeCamera(bounds, rt);
            var quadGo = CreateBakeQuad(material, mpb, bounds);

            cameraGo.GetComponent<Camera>().Render();

            var result = ReadbackTexture(rt, settings.Resolution);

            Object.DestroyImmediate(quadGo);
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(material);
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        /// <summary>
        /// Bakes N canopy variants with different seeds and returns the array.
        /// </summary>
        internal static Texture2D[] BakeVariants(BushCanopyBakeSettings settings, int variantCount)
        {
            var results = new Texture2D[variantCount];
            for (var i = 0; i < variantCount; i++)
            {
                var seed = (uint)(i * 7919 + 31);
                results[i] = BakeCanopy(settings, seed);
            }

            return results;
        }

        /// <summary>
        /// Exports each canopy variant as a separate PNG and configures sprite
        /// import settings. Returns the asset paths.
        /// </summary>
        internal static string[] ExportVariants(
            IReadOnlyList<Texture2D> variants, string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var paths = new string[variants.Count];

            for (var i = 0; i < variants.Count; i++)
            {
                if (variants[i] == null)
                {
                    continue;
                }

                var path = $"{outputFolder}/Canopy_{i:D2}.png";
                var bytes = variants[i].EncodeToPNG();
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                ConfigureSpriteImporter(path);
                paths[i] = path;
            }

            Debug.Log($"[BushCanopyBaker] Exported {variants.Count} canopy variant(s) to {outputFolder}");
            return paths;
        }

        private static Vector4[] GenerateSlotPositions(int slotCount, uint seed)
        {
            var positions = new Vector4[MaxSlots];
            var hash = (seed % 10007u) / 10007f;

            if (slotCount <= 1)
            {
                positions[0] = new Vector4(0f, 0f, hash, 1f);
                return positions;
            }

            // Hex grid pattern centred at origin
            var placed = 0;
            var ring = 0;
            positions[placed++] = new Vector4(0f, 0f, hash, 1f);

            while (placed < slotCount)
            {
                ring++;
                for (var side = 0; side < 6 && placed < slotCount; side++)
                {
                    for (var step = 0; step < ring && placed < slotCount; step++)
                    {
                        var angle = (side * 60f + step * 60f / ring) * Mathf.Deg2Rad;
                        var x = Mathf.Cos(angle) * ring * HexSpacingX;
                        var y = Mathf.Sin(angle) * ring * HexSpacingY;

                        var slotHash = Mathf.Abs(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f);
                        slotHash -= Mathf.Floor(slotHash);

                        positions[placed++] = new Vector4(x, y, slotHash, 1f);
                    }
                }
            }

            return positions;
        }

        private static Rect ComputeBounds(Vector4[] positions, int count, float slotRadius)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (var i = 0; i < count; i++)
            {
                var p = new Vector2(positions[i].x, positions[i].y);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            var padding = slotRadius * 2f;
            min -= Vector2.one * padding;
            max += Vector2.one * padding;

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void ConfigureMaterial(Material material, BushCanopyBakeSettings settings, uint seed)
        {
            var hash = (seed % 10007u) / 10007f;

            material.SetFloat(SlotRadiusId, settings.SlotRadius);
            material.SetFloat(RadiusJitterId, settings.RadiusJitter);
            material.SetFloat(BranchSpreadId, settings.BranchSpread);
            material.SetFloat(SubCircleSizeId, settings.SubCircleSize);
            material.SetFloat(SubCircleSizeVarId, settings.SubCircleSizeVar);

            material.SetFloat(GielisMId, settings.GielisM + (hash - 0.5f) * 0.4f);
            material.SetFloat(GielisN1Id, settings.GielisN1 + hash * 0.2f);
            material.SetFloat(GielisN2Id, settings.GielisN2 + (hash * 3.7f % 1f) * 0.2f - 0.1f);
            material.SetFloat(GielisN3Id, settings.GielisN3 + (hash * 5.3f % 1f) * 0.2f - 0.1f);

            material.SetColor(BaseColorId, settings.BaseColor);
            material.SetColor(TopColorId, settings.TopColor);
            material.SetFloat(EdgeShadeId, settings.EdgeShade);

            material.SetFloat(VeinWidthId, settings.VeinWidth);
            material.SetFloat(VeinDarkenId, settings.VeinDarken);
            material.SetInt(VeinDepthId, settings.VeinDepth);
            material.SetFloat(VeinCountId, settings.VeinCount);

            material.SetFloat(SSSAbsorptionId, settings.SSSAbsorption);
            material.SetFloat(SSSStrengthId, settings.SSSStrength);
            material.SetColor(SSSColorId, settings.SSSColor);

            material.SetFloat(LeafShadowStrengthId, settings.LeafShadowStrength);
            material.SetInt(ShadowSamplesId, settings.ShadowSamples);
            material.SetFloat(AOMulId, settings.AOMul);

            material.SetFloat(HueJitterId, settings.HueJitter);
            material.SetFloat(EdgeBrowningWidthId, settings.EdgeBrowningWidth);
        }

        private static GameObject CreateBakeCamera(Rect bounds, RenderTexture rt)
        {
            var go = new GameObject("_CanopyBakeCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(bounds.height, bounds.width) * 0.5f;
            cam.nearClipPlane = -1f;
            cam.farClipPlane = 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.targetTexture = rt;
            cam.cullingMask = 1 << BakeLayer;
            cam.enabled = false;

            go.transform.position = new Vector3(bounds.center.x, bounds.center.y, -0.5f);

            return go;
        }

        private static GameObject CreateBakeQuad(Material material, MaterialPropertyBlock mpb, Rect bounds)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "_CanopyBakeQuad";
            go.hideFlags = HideFlags.HideAndDontSave;
            go.layer = BakeLayer;

            var scaleX = bounds.width * 1.1f;
            var scaleY = bounds.height * 1.1f;
            go.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            go.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0f);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.SetPropertyBlock(mpb);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            return go;
        }

        private static Texture2D ReadbackTexture(RenderTexture rt, int resolution)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;

            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            tex.Apply();

            RenderTexture.active = previous;
            return tex;
        }

        private static void ConfigureSpriteImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}

