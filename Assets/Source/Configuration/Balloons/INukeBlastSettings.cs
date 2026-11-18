using BalloonParty.Nudge;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     The nuke's blast (PLAN-NukeBalloon): a disc that grows from the nuke across the board and pops every balloon
    ///     it reaches but the other reward balloons, behind a blinding light, with the strongest push and a mushroom
    ///     cloud.
    /// </summary>
    public interface INukeBlastSettings
    {
        /// <summary>Seconds the disc takes to reach the farthest slot, easing out.</summary>
        float Seconds { get; }

        /// <summary>
        ///     The outward nudge each balloon and grid actor gets just before the edge reaches it
        ///     (<see cref="NudgeType.Wave" />). An array, as <see cref="NudgeMessage" /> takes; never written to.
        /// </summary>
        NudgeOverride[] NudgeOverrides { get; }

        /// <summary>World units ahead of the edge at which the outward nudge comes.</summary>
        float NudgeLead { get; }

        /// <summary>The disc's material, which draws the white flood, the core and the shock ring. Required: with none the disc isn't drawn.</summary>
        Material BlastMaterial { get; }

        string SortingLayerName { get; }

        int SortingOrder { get; }

        /// <summary>The scene light the blast carries: its peak strength as the disc starts.</summary>
        float LightIntensity { get; }

        /// <summary>World units the light reaches past the disc's edge.</summary>
        float LightReach { get; }

        /// <summary>Seconds the light takes to fade once the disc covers the board.</summary>
        float LightLinger { get; }

        /// <summary>The share of the light's intensity left when the disc reaches the board's far edge.</summary>
        float LightAtEdge { get; }

        /// <summary>World radius of the disturbance field's push at the blast.</summary>
        float PushRadius { get; }

        /// <summary>The push's strength: the strongest on the field (the bomb's profile is 5).</summary>
        float PushStrength { get; }

        /// <summary>Seconds the push ramps over, its radius growing with it.</summary>
        float PushSeconds { get; }

        /// <summary>
        ///     World radius the blast throws aside what it lands in, reported with the push's impact: on water, its
        ///     crater's edge.
        /// </summary>
        float ImpactRadius { get; }

        /// <summary>How hard it throws, a bomb being 1 (the water's crater is as deep); 0 reports no blast.</summary>
        float ImpactStrength { get; }

        /// <summary>Seconds it goes on throwing it aside, hardest at the start: how long the water's crater is held open.</summary>
        float ImpactSeconds { get; }

        /// <summary>The mushroom cloud's material. Required: with none no cloud rises.</summary>
        Material CloudMaterial { get; }

        /// <summary>Seconds the cloud takes to rise, cool and erode away.</summary>
        float CloudSeconds { get; }

        /// <summary>Half the side of the cloud's square quad around the blast, in world units.</summary>
        float CloudReach { get; }

        /// <summary>The cloud's sorting order on <see cref="SortingLayerName" />: above the balloons.</summary>
        int CloudSortingOrder { get; }

        /// <summary>
        ///     The physics layer the cloud draws on: the effects layer the glass over a ball clears, so a ball flying
        ///     through the cloud stays in sight.
        /// </summary>
        string CloudLayerName { get; }
    }
}
