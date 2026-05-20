using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EditorUI
{
    /// <summary>
    ///     Draws a clickable label that pings and selects an asset in the Project view.
    /// </summary>
    internal static class AssetLinkLabel
    {
        internal static void Draw(string label, string assetPath, float width = 160f)
        {
            if (GUILayout.Button(label, EditorStyles.linkLabel, GUILayout.Width(width)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            }
        }
    }
}
