using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the ground backdrop (see <c>GroundView</c>).</summary>
    internal interface IGroundSettings
    {
        /// <summary>Display material using BalloonParty/Scenario/Ground — the meadow's colours, blade shape and
        /// the grass reaction to the disturbance field all live on it.</summary>
        Material Material { get; }

        /// <summary>Quad size over the reference world size — &gt;1 so a camera shake or cinematic pan never
        /// reveals an edge.</summary>
        float OverscanMultiplier { get; }

        /// <summary>Sorting layer for the ground quad.</summary>
        string SortingLayerName { get; }

        /// <summary>Sorting order — above the sky scatter (-20), below the cloud backdrop (-10).</summary>
        int SortingOrder { get; }
    }
}
