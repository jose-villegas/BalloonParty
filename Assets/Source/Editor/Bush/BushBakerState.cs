using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    [FilePath("BushBaker/Settings.asset", FilePathAttribute.Location.PreferencesFolder)]
    internal class BushBakerState : ScriptableSingleton<BushBakerState>
    {
        [SerializeField] internal BushLeafBakeSettings LeafSettings = new();
        [SerializeField] internal BushBranchBakeSettings BranchSettings = new();
        [SerializeField] internal string OutputFolder = "Assets/Art/Bush/Baked";
        [SerializeField] internal bool AutoPreview = true;
        [SerializeField] internal uint PreviewSeed = 42;

        [SerializeField] internal bool LeafFoldout = true;
        [SerializeField] internal bool LeafShapeFoldout = true;
        [SerializeField] internal bool LeafSurfaceFoldout = true;
        [SerializeField] internal bool LeafMidribFoldout = true;
        [SerializeField] internal bool LeafPetioleFoldout = true;

        [SerializeField] internal bool BranchFoldout = true;
        [SerializeField] internal bool BranchShapeFoldout = true;
        [SerializeField] internal bool BranchVisualFoldout = true;
        [SerializeField] internal bool BranchLeafFoldout = true;

        internal void Save()
        {
            Save(true);
        }
    }
}
