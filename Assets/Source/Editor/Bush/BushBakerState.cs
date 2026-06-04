using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    [FilePath("BushBaker/Settings.asset", FilePathAttribute.Location.PreferencesFolder)]
    internal class BushBakerState : ScriptableSingleton<BushBakerState>
    {
        [SerializeField] internal BushLeafBakeSettings LeafSettings = new();
        [SerializeField] internal BushCanopyBakeSettings CanopySettings = new();
        [SerializeField] internal string OutputFolder = "Assets/Art/Bush/Baked";
        [SerializeField] internal bool AutoPreview = true;

        [SerializeField] internal bool LeafFoldout = true;
        [SerializeField] internal bool LeafShapeFoldout = true;
        [SerializeField] internal bool LeafSurfaceFoldout = true;
        [SerializeField] internal bool LeafVeinsFoldout;
        [SerializeField] internal bool LeafSSSFoldout;
        [SerializeField] internal bool LeafVariationFoldout;
        [SerializeField] internal bool CanopyFoldout = true;
        [SerializeField] internal bool CanopyShapeFoldout = true;
        [SerializeField] internal bool CanopyGielisFoldout;
        [SerializeField] internal bool CanopySurfaceFoldout = true;
        [SerializeField] internal bool CanopyVeinsFoldout;
        [SerializeField] internal bool CanopySSSFoldout;

        internal void Save()
        {
            Save(true);
        }
    }
}
