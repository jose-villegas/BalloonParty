using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the ground backdrop (see <c>GroundView</c>).</summary>
    internal interface IGroundSettings
    {
        /// <summary>Floor material using BalloonParty/Scenario/Ground — the dark ground under the blades.</summary>
        Material Material { get; }

        /// <summary>Quad size over the reference world size — &gt;1 so a camera shake or cinematic pan never
        /// reveals an edge.</summary>
        float OverscanMultiplier { get; }

        /// <summary>Sorting layer for the ground quad.</summary>
        string SortingLayerName { get; }

        /// <summary>Sorting order — above the sky scatter (-20), below the cloud backdrop (-10).</summary>
        int SortingOrder { get; }

        /// <summary>World size of the floor's patches, shared with the blades so their drier tips sit over the
        /// floor's lighter patches.</summary>
        float PatchSize { get; }

        /// <summary>World size of the bare-dirt patches the floor shows and the blades thin out over.</summary>
        float DirtPatchSize { get; }

        /// <summary>Noise threshold for dirt: higher means less dirt.</summary>
        float DirtThreshold { get; }

        /// <summary>Width of the soft grass-to-dirt edge, in noise units.</summary>
        float DirtSoftness { get; }

        /// <summary>Blade material using BalloonParty/Scenario/GrassBlades — blade shape, colours and the reaction
        /// to the disturbance field. Without one the ground draws its floor only.</summary>
        Material BladeMaterial { get; }

        /// <summary>World units between neighbouring blade roots.</summary>
        float BladeSpacing { get; }

        /// <summary>World units the blade tile reaches past each edge of the camera view; the tile wraps as the
        /// meadow scrolls, so this must be more than a laid-flat blade.</summary>
        float BladeTileMargin { get; }

        /// <summary>Sorting order for the blades — above the floor, below the smoke and the clouds.</summary>
        int BladeSortingOrder { get; }

        /// <summary>Seed for the blade scatter.</summary>
        int BladeSeed { get; }

        /// <summary>The meadow's average unlit colour, which the GI measures bounce against on ground levels.</summary>
        Color BounceReference { get; }

        /// <summary>Material using Hidden/BalloonParty/Scenario/GrassWake: the grass's own short memory of the
        /// disturbance field, which the blades bend to. Without one the blades stay at rest.</summary>
        Material WakeMaterial { get; }

        /// <summary>Time constant, in seconds, for a pushed blade to stand back up.</summary>
        float BladeRecoverySeconds { get; }
    }
}
