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

        internal void Save()
        {
            Save(true);
        }
    }
}
