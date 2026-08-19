using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the sky-scatter backdrop (see <c>SkyScatterService</c>).</summary>
    internal interface ISkyScatterSettings
    {
        /// <summary>Display material using BalloonParty/Display/SkyScatter — ring count, colours, wave,
        /// and cloud-distortion tuning all live on it.</summary>
        Material Material { get; }

        /// <summary>Quad size over the reference world size — &gt;1 so a camera shake or cinematic pan
        /// never reveals an edge.</summary>
        float OverscanMultiplier { get; }

        /// <summary>Sorting layer for the sky-scatter quad.</summary>
        string SortingLayerName { get; }

        /// <summary>Sorting order — must stay below every other backdrop layer (BackgroundCloud sits at
        /// -10) so the sky renders first, right after the camera's flat clear colour.</summary>
        int SortingOrder { get; }
    }
}
