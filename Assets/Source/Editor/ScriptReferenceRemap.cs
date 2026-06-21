using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    internal static class ScriptReferenceRemap
    {
        [MenuItem("CONTEXT/MonoBehaviour/Remap Script References")]
        private static void Execute(MenuCommand command)
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(go);
            if (string.IsNullOrEmpty(assetPath) && go.scene.IsValid())
            {
                assetPath = go.scene.path;
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("ScriptReferenceRemap: could not determine asset path.");
                return;
            }

            var brokenGuids = FindBrokenGuids(assetPath);
            if (brokenGuids.Count == 0)
            {
                Debug.LogWarning("ScriptReferenceRemap: no broken script GUIDs found in asset.");
                return;
            }

            var capturedPath = assetPath;
            var capturedGuids = brokenGuids;

            ScriptSearchPopup.Show(new Rect(Vector2.zero, new Vector2(320, 0)), OnScriptSelected);
            return;

            void OnScriptSelected(MonoScript newScript)
            {
                var newGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(newScript));
                var newName = newScript.GetClass()?.Name ?? newScript.name;

                if (capturedGuids.Count == 1)
                {
                    ReplaceGuid(capturedPath, capturedGuids[0], newGuid, newName);
                }
                else
                {
                    // Multiple broken GUIDs — show a menu to pick which one
                    var menu = new GenericMenu();
                    foreach (var oldGuid in capturedGuids)
                    {
                        var g = oldGuid;
                        menu.AddItem(new GUIContent(g), false, () => ReplaceGuid(capturedPath, g, newGuid, newName));
                    }

                    menu.ShowAsContext();
                }
            }
        }

        private static List<string> FindBrokenGuids(string assetPath)
        {
            var broken = new List<string>();

            try
            {
                var text = File.ReadAllText(assetPath);
                var token = "m_Script: {fileID: 11500000, guid: ";
                var idx = 0;

                while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
                {
                    var guidStart = idx + token.Length;
                    AddIfBrokenGuid(text, guidStart, broken);
                    idx = guidStart;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ScriptReferenceRemap: could not read {assetPath} — {e.Message}");
            }

            return broken;
        }

        // Parses the guid starting at <paramref name="guidStart"/> and adds it to
        // <paramref name="broken"/> if it resolves to no loadable MonoScript.
        private static void AddIfBrokenGuid(string text, int guidStart, List<string> broken)
        {
            var guidEnd = text.IndexOf(',', guidStart);
            if (guidEnd <= guidStart)
            {
                return;
            }

            var guid = text.Substring(guidStart, guidEnd - guidStart).Trim();
            var scriptPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(scriptPath)
                && AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath) != null)
            {
                return;
            }

            if (!broken.Contains(guid))
            {
                broken.Add(guid);
            }
        }

        private static void ReplaceGuid(string assetPath, string oldGuid, string newGuid, string newName)
        {
            try
            {
                var text = File.ReadAllText(assetPath);
                var replaced = text.Replace(oldGuid, newGuid);

                if (replaced == text)
                {
                    Debug.LogWarning("ScriptReferenceRemap: GUID not found in file, nothing changed.");
                    return;
                }

                File.WriteAllText(assetPath, replaced);
                AssetDatabase.Refresh();
                Debug.Log($"ScriptReferenceRemap: remapped → {newName} in {Path.GetFileName(assetPath)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ScriptReferenceRemap: failed — {e.Message}");
            }
        }
    }
}
