using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Utilities
{
    /// <summary>Draws a clickable label that pings and selects an asset in the Project view.</summary>
    public static class AssetLinkLabel
    {
        public static void Draw(string label, string assetPath, float width = 160f)
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
