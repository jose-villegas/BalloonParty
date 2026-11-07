using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     The shared half of the UI builder menu items: find the prefab whose root carries a component, then
    ///     rebuild one named child on it, asking first when that child already exists.
    /// </summary>
    internal static class PrefabRebuild
    {
        /// <summary>The first prefab matching <paramref name="search" /> whose root has a <typeparamref name="T" />.</summary>
        internal static string FindPrefabWithRoot<T>(string search) where T : Component
        {
            foreach (var guid in AssetDatabase.FindAssets($"{search} t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null && asset.GetComponent<T>() != null)
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        ///     Opens the prefab at <paramref name="path" />, replaces its root's <paramref name="childName" />
        ///     child with what <paramref name="build" /> makes, and saves. <paramref name="build" /> returns an
        ///     error to show, or null on success; nothing is saved on an error or a declined rebuild.
        /// </summary>
        internal static void Run(string dialogTitle, string path, string childName, Func<GameObject, string> build)
        {
            if (path == null)
            {
                EditorUtility.DisplayDialog(dialogTitle, $"The prefab to build {childName} on wasn't found.", "OK");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var existing = root.transform.Find(childName);
                if (existing != null)
                {
                    if (!EditorUtility.DisplayDialog(dialogTitle,
                            $"{path} already has {childName}. Rebuild it? Hand edits to it are lost.", "Rebuild",
                            "Cancel"))
                    {
                        return;
                    }

                    Object.DestroyImmediate(existing.gameObject);
                }

                var error = build(root);
                if (error != null)
                {
                    EditorUtility.DisplayDialog(dialogTitle, error, "OK");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[{dialogTitle}] Built {childName} on {path}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
