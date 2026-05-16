using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    internal static class SetMobileTextureSize
    {
        private static readonly string[] MobilePlatforms = { "iPhone", "Android" };

        [MenuItem("Assets/Texture/Set Mobile Max Size/64", false, 2000)]
        private static void Set64() => Apply(64);

        [MenuItem("Assets/Texture/Set Mobile Max Size/128", false, 2001)]
        private static void Set128() => Apply(128);

        [MenuItem("Assets/Texture/Set Mobile Max Size/256", false, 2002)]
        private static void Set256() => Apply(256);

        [MenuItem("Assets/Texture/Set Mobile Max Size/512", false, 2003)]
        private static void Set512() => Apply(512);

        [MenuItem("Assets/Texture/Set Mobile Max Size/1024", false, 2004)]
        private static void Set1024() => Apply(1024);

        [MenuItem("Assets/Texture/Set Mobile Max Size/2048", false, 2005)]
        private static void Set2048() => Apply(2048);

        [MenuItem("Assets/Texture/Set Mobile Max Size/Reset to Default", false, 2100)]
        private static void ResetToDefault() => Apply(-1);

        [MenuItem("Assets/Texture/Set Mobile Max Size/64", true)]
        [MenuItem("Assets/Texture/Set Mobile Max Size/128", true)]
        [MenuItem("Assets/Texture/Set Mobile Max Size/256", true)]
        [MenuItem("Assets/Texture/Set Mobile Max Size/512", true)]
        [MenuItem("Assets/Texture/Set Mobile Max Size/1024", true)]
        [MenuItem("Assets/Texture/Set Mobile Max Size/2048", true)]
        [MenuItem("Assets/Texture/Set Mobile Max Size/Reset to Default", true)]
        private static bool Validate()
        {
            return Selection.objects.Any(o => o is Texture2D);
        }

        private static void Apply(int maxSize)
        {
            var guids = Selection.assetGUIDs;

            if (guids == null || guids.Length == 0)
            {
                return;
            }

            var count = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer == null)
                {
                    continue;
                }

                var modified = false;

                foreach (var platform in MobilePlatforms)
                {
                    var settings = importer.GetPlatformTextureSettings(platform);

                    if (maxSize < 0)
                    {
                        // Reset: disable the override so it falls back to default
                        if (settings.overridden)
                        {
                            settings.overridden = false;
                            importer.SetPlatformTextureSettings(settings);
                            modified = true;
                        }
                    }
                    else
                    {
                        settings.overridden = true;
                        settings.maxTextureSize = maxSize;
                        importer.SetPlatformTextureSettings(settings);
                        modified = true;
                    }
                }

                if (modified)
                {
                    importer.SaveAndReimport();
                    count++;
                }
            }

            var action = maxSize < 0 ? "Reset to default" : $"Set mobile max size to {maxSize}";
            Debug.Log($"[SetMobileTextureSize] {action} on {count} texture(s).");
        }
    }
}

