using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the surface backdrops (ground and water) (see <c>GroundView</c>).</summary>
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

        /// <summary>Surface material using BalloonParty/Scenario/Water, drawn on the floor quad on water levels.
        /// Without one water levels show the sky.</summary>
        Material WaterMaterial { get; }

        /// <summary>Material using Hidden/BalloonParty/Scenario/WaterCaustics: one tile of the caustic net the water
        /// lays under its surface. Without one the bottom shows no caustics.</summary>
        Material WaterCausticMaterial { get; }

        /// <summary>Texels across the square caustic tile (a power of two).</summary>
        int WaterCausticResolution { get; }

        /// <summary>Material using Hidden/BalloonParty/Scenario/WaterRipple: draws the rings each impact starts on the
        /// surface. Without one the surface only swells.</summary>
        Material WaterRippleMaterial { get; }

        /// <summary>Texels per world unit of the ripple height field.</summary>
        float WaterRippleTexelsPerUnit { get; }

        /// <summary>The water's average unlit colour, which the GI measures bounce against on water levels.</summary>
        Color WaterBounceReference { get; }

        /// <summary>Material using BalloonParty/Scenario/GroundMist — the mist of the sky/ground reveal. Without one
        /// the scenario switches with a cut.</summary>
        Material MistMaterial { get; }

        /// <summary>Sorting order for the mist — over the cloud backdrop, under the paint and the board.</summary>
        int MistSortingOrder { get; }

        /// <summary>Share of a reveal the mist takes to roll in before the hole opens.</summary>
        float MistInShare { get; }

        /// <summary>World width of the ground's soft edge inside the hole.</summary>
        float RevealEdgeSoftness { get; }

        /// <summary>World units the mist reaches inside the hole's edge.</summary>
        float MistReach { get; }

        /// <summary>World units the hole's edge wanders from a circle.</summary>
        float RevealEdgeWander { get; }

        /// <summary>How opaque the mist is at its thickest (0..1).</summary>
        float MistOpacity { get; }

        /// <summary>How many times larger the sky looks than the ground in a reveal (1 = no depth).</summary>
        float RevealDepthZoom { get; }
    }
}
