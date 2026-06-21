using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    internal static class SetMobileTextureSize
    {
        private static readonly string[] MobilePlatforms = { "iPhone", "Android" };

        [MenuItem("Assets/Texture/Set Mobile Max Size/64", false, 2000)]
        private static void Set64()
        {
            Apply(64);
        }

        [MenuItem("Assets/Texture/Set Mobile Max Size/128", false, 2001)]
        private static void Set128()
        {
            Apply(128);
        }

        [MenuItem("Assets/Texture/Set Mobile Max Size/256", false, 2002)]
        private static void Set256()
        {
            Apply(256);
        }

        [MenuItem("Assets/Texture/Set Mobile Max Size/512", false, 2003)]
        private static void Set512()
        {
            Apply(512);
        }

        [MenuItem("Assets/Texture/Set Mobile Max Size/1024", false, 2004)]
        private static void Set1024()
        {
            Apply(1024);
        }

        [MenuItem("Assets/Texture/Set Mobile Max Size/2048", false, 2005)]
        private static void Set2048()
        {
            Apply(2048);
        }

        [MenuItem("Assets/Texture/Set Mobile Max Size/Reset to Default", false, 2100)]
        private static void ResetToDefault()
        {
            Apply(-1);
        }

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
                if (ApplyToTexture(guid, maxSize))
                {
                    count++;
                }
            }

            var action = maxSize < 0 ? "Reset to default" : $"Set mobile max size to {maxSize}";
            Debug.Log($"[SetMobileTextureSize] {action} on {count} texture(s).");
        }

        // Applies the max-size override to every mobile platform of one texture; returns whether
        // anything changed (so it gets re-imported and counted).
        private static bool ApplyToTexture(string guid, int maxSize)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return false;
            }

            var modified = false;
            foreach (var platform in MobilePlatforms)
            {
                modified |= ApplyPlatformSetting(importer, platform, maxSize);
            }

            if (modified)
            {
                importer.SaveAndReimport();
            }

            return modified;
        }

        // A negative maxSize clears the override (back to default); otherwise it sets the cap.
        // Returns whether the platform's settings changed.
        private static bool ApplyPlatformSetting(TextureImporter importer, string platform, int maxSize)
        {
            var settings = importer.GetPlatformTextureSettings(platform);

            if (maxSize < 0)
            {
                if (!settings.overridden)
                {
                    return false;
                }

                settings.overridden = false;
                importer.SetPlatformTextureSettings(settings);
                return true;
            }

            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            importer.SetPlatformTextureSettings(settings);
            return true;
        }
    }
}
