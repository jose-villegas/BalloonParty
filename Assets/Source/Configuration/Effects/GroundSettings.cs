using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Authored tuning for the ground backdrop shown on ground levels; assign the display material
    /// here and wire the asset into GameLifetimeScope.</summary>
    [CreateAssetMenu(menuName = "Configuration/Ground Settings", fileName = "GroundSettings")]
    internal sealed class GroundSettings : ScriptableObject, IGroundSettings
    {
        [Tooltip("Display material using BalloonParty/Scenario/Ground — meadow and grass-reaction tuning lives here.")]
        [SerializeField] private Material _material;

        [Tooltip("Quad size over the reference world size — >1 so a camera shake or cinematic pan never reveals an edge.")]
        [SerializeField] private float _overscanMultiplier = 1.6f;

        [Tooltip("Sorting layer for the ground quad.")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Default";

        [Tooltip("Sorting order — above the sky scatter (-20), below the cloud backdrop (-10).")]
        [SerializeField] private int _sortingOrder = -15;

        public Material Material => _material;
        public float OverscanMultiplier => _overscanMultiplier;
        public string SortingLayerName => _sortingLayerName;
        public int SortingOrder => _sortingOrder;
    }
}
