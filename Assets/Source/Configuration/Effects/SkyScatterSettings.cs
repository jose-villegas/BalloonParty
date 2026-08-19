using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Authored tuning for the sky-scatter backdrop; assign the display material here and
    /// wire the asset into AppLifetimeScope.</summary>
    [CreateAssetMenu(menuName = "Configuration/Sky Scatter Settings", fileName = "SkyScatterSettings")]
    internal sealed class SkyScatterSettings : ScriptableObject, ISkyScatterSettings
    {
        [Tooltip("Display material using BalloonParty/Display/SkyScatter — ring/colour/wave tuning lives here.")]
        [SerializeField] private Material _material;

        [Tooltip("Quad size over the reference world size — >1 so a camera shake or cinematic pan never reveals an edge.")]
        [SerializeField] private float _overscanMultiplier = 1.6f;

        [Tooltip("Sorting layer for the sky-scatter quad.")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Default";

        [Tooltip("Sorting order — must stay below every other backdrop layer (BackgroundCloud sits at -10) so the sky renders first.")]
        [SerializeField] private int _sortingOrder = -20;

        public Material Material => _material;
        public float OverscanMultiplier => _overscanMultiplier;
        public string SortingLayerName => _sortingLayerName;
        public int SortingOrder => _sortingOrder;
    }
}
