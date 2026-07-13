using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    [CreateAssetMenu(menuName = "Configuration/Speck Field Settings", fileName = "SpeckFieldSettings")]
    internal class SpeckFieldSettings : ScriptableObject, ISpeckFieldSettings
    {
        [Tooltip("Buffer size — the hard cap on active specks.")]
        [SerializeField] private int _count = 4096;

        [Tooltip("World-space extent the field wraps specks within (toroidal).")]
        [SerializeField] private Vector2 _regionSize = new(30f, 20f);

        [SerializeField] private SpeckMotionSettings _motion = new();
        [SerializeField] private SpeckAppearanceSettings _appearance = new();
        [SerializeField] private SpeckSpawnSettings _spawning = new();

        public int Count => _count;
        public Vector2 RegionSize => _regionSize;
        public ISpeckMotionSettings Motion => _motion;
        public ISpeckAppearanceSettings Appearance => _appearance;
        public ISpeckSpawnSettings Spawning => _spawning;
    }
}
