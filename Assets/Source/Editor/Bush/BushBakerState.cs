using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    [FilePath("BushBaker/Settings.asset", FilePathAttribute.Location.PreferencesFolder)]
    internal class BushBakerState : ScriptableSingleton<BushBakerState>
    {
        [SerializeField] internal BushLeafBakeSettings LeafSettings = new();
        [SerializeField] internal string OutputFolder = "Assets/Art/Bush/Baked";
        [SerializeField] internal bool AutoPreview = true;

        [SerializeField] internal bool LeafFoldout = true;
        [SerializeField] internal bool LeafShapeFoldout = true;
        [SerializeField] internal bool LeafSurfaceFoldout = true;
        [SerializeField] internal bool LeafMidribFoldout = true;

        internal void Save()
        {
            Save(true);
        }
    }
}
