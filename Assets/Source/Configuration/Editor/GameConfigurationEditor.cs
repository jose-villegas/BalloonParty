using UnityEditor;

namespace BalloonParty.Configuration.Editor
{
    [CustomEditor(typeof(GameConfiguration))]
    internal class GameConfigurationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}
