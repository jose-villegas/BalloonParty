using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Authored tuning for the ground backdrop shown on ground levels: the floor quad and the grass blades
    /// over it. Assign both materials here and wire the asset into GameLifetimeScope.</summary>
    [CreateAssetMenu(menuName = "Configuration/Ground Settings", fileName = "GroundSettings")]
    internal sealed class GroundSettings : ScriptableObject, IGroundSettings
    {
        [Tooltip("Floor material using BalloonParty/Scenario/Ground — the dark ground under the blades and its patches.")]
        [SerializeField] private Material _material;

        [Tooltip("Quad size over the reference world size — >1 so a camera shake or cinematic pan never reveals an edge.")]
        [SerializeField] private float _overscanMultiplier = 1.6f;

        [Tooltip("Sorting layer for the ground quad.")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Default";

        [Tooltip("Sorting order — above the sky scatter (-20), below the cloud backdrop (-10).")]
        [SerializeField] private int _sortingOrder = -15;

        [Tooltip("World size of the floor's darker and lighter patches. The blades read the same patches, so their tips turn drier over the lighter ones.")]
        [Min(0.01f)]
        [SerializeField] private float _patchSize = 3.5f;

        [Header("Dirt")]
        [Tooltip("World size of the bare-dirt patches. The floor shows dirt there and the blades thin out toward it.")]
        [Min(0.01f)]
        [SerializeField] private float _dirtPatchSize = 6f;

        [Tooltip("Noise threshold for dirt: higher means less dirt (0.5 is about half the ground, 0.7 a few large clusters).")]
        [Range(0f, 1f)]
        [SerializeField] private float _dirtThreshold = 0.7f;

        [Tooltip("Width of the fade between grass and dirt, in noise units: wider blends the patches into the grass.")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float _dirtSoftness = 0.2f;

        [Header("Grass Blades")]
        [Tooltip("Blade material using BalloonParty/Scenario/GrassBlades — blade shape, colours and the reaction to the disturbance field.")]
        [SerializeField] private Material _bladeMaterial;

        [Tooltip("World units between neighbouring blade roots. Blade count grows with the inverse square: 0.085 is about 9000 blades on a 9:16 phone (45k vertices, one draw), capped at 13107.")]
        [Min(0.02f)]
        [SerializeField] private float _bladeSpacing = 0.085f;

        [Tooltip("World units the blade tile reaches past each edge of the camera view. The tile wraps as the meadow scrolls, so this must be more than a laid-flat blade (the blade material's length) or blades pop at the edges.")]
        [Min(0f)]
        [SerializeField] private float _bladeTileMargin = 0.4f;

        [Tooltip("Sorting order for the blades — above the floor, below the smoke (-11) and the cloud backdrop (-10).")]
        [SerializeField] private int _bladeSortingOrder = -14;

        [Tooltip("Seed for the blade scatter. Any value works; change it to reshuffle the meadow.")]
        [SerializeField] private int _bladeSeed = 1337;

        [Header("Blade Wake")]
        [Tooltip("Material using Hidden/BalloonParty/Scenario/GrassWake — the grass's own short memory of the disturbance field. Without one the blades stay at rest.")]
        [SerializeField] private Material _wakeMaterial;

        [Tooltip("Seconds for a pushed blade to stand most of the way back up (the wake's time constant). The shared field holds a push for seconds; the grass lets it go this fast instead.")]
        [Min(0.02f)]
        [SerializeField] private float _bladeRecoverySeconds = 0.35f;

        public Material Material => _material;
        public float OverscanMultiplier => _overscanMultiplier;
        public string SortingLayerName => _sortingLayerName;
        public int SortingOrder => _sortingOrder;
        public float PatchSize => _patchSize;
        public float DirtPatchSize => _dirtPatchSize;
        public float DirtThreshold => _dirtThreshold;
        public float DirtSoftness => _dirtSoftness;
        public Material BladeMaterial => _bladeMaterial;
        public float BladeSpacing => _bladeSpacing;
        public float BladeTileMargin => _bladeTileMargin;
        public int BladeSortingOrder => _bladeSortingOrder;
        public int BladeSeed => _bladeSeed;
        public Material WakeMaterial => _wakeMaterial;
        public float BladeRecoverySeconds => _bladeRecoverySeconds;
    }
}
