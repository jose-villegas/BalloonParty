using System.IO;
using BalloonParty.Configuration;
using UnityEditor;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Editor.Bush
{
    /// <summary>
    /// Exports baked branch maps and leaf slot data as <see cref="BushVariantData"/>
    /// ScriptableObjects. Saves textures as PNG with correct import settings.
    /// </summary>
    internal static class BushVariantExporter
    {
        internal static BushVariantData Export(
            int variantIndex,
            int seed,
            BushBranchBakeSettings branchSettings,
            int leafVariantCount,
            float bushWorldSize,
            string outputFolder)
        {
            EnsureFolder(outputFolder);

            var branchMap = BushBranchBaker.Bake(seed, branchSettings);
            if (branchMap == null)
            {
                return null;
            }

            var texturePath = $"{outputFolder}/BranchMap_V{variantIndex:D2}.png";
            SaveTexturePng(branchMap, texturePath);
            ConfigureTextureImporter(texturePath);

            var importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            var leafSlots = BushLeafExtractor.Extract(branchMap, seed, branchSettings, leafVariantCount);
            Object.DestroyImmediate(branchMap);

            var slotData = BuildSlotData(leafSlots, bushWorldSize, seed);
            var boundsSize = new Vector2(bushWorldSize, bushWorldSize);

            // Store raw generator segments for debug gizmo overlay
            var segments = BushBranchGenerator.Generate(seed, branchSettings);
            var debugSegments = new Vector4[segments.Count];
            for (var s = 0; s < segments.Count; s++)
            {
                var seg = segments[s];
                debugSegments[s] = new Vector4(seg.Start.x, seg.Start.y, seg.End.x, seg.End.y);
            }

            var soPath = $"{outputFolder}/BushVariant_V{variantIndex:D2}.asset";
            var variantSo = LoadOrCreateAsset(soPath);
            variantSo.SetBakeData(importedTexture, slotData, boundsSize);
            variantSo.SetDebugData(debugSegments, bushWorldSize);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BushVariantExporter] Exported variant {variantIndex} → {soPath}");
            return variantSo;
        }

        private static LeafSlotData[] BuildSlotData(
            System.Collections.Generic.IReadOnlyList<BushLeafExtractor.LeafSlot> slots,
            float bushWorldSize,
            int seed)
        {
            var rng = new System.Random(seed + 5471);
            var result = new LeafSlotData[slots.Count];

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];

                result[i] = new LeafSlotData
                {
                    UVPosition = slot.UVPosition,
                    BaseAngle = slot.Angle,
                    Depth = slot.Depth,
                    PhaseOffset = (float)rng.NextDouble() * Mathf.PI * 2f,
                    Scale = slot.Scale,
                    SpriteVariant = slot.SpriteVariant,
                    Tint = new Color32(255, 255, 255, 255)
                };
            }

            return result;
        }

        private static void SaveTexturePng(Texture2D texture, string path)
        {
            var bytes = texture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureTextureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static BushVariantData LoadOrCreateAsset(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BushVariantData>(path);
            if (existing != null)
            {
                return existing;
            }

            var so = ScriptableObject.CreateInstance<BushVariantData>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parts = folder.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
