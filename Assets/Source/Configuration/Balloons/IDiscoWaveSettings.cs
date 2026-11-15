using BalloonParty.Nudge;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     The disco ball's wave (PLAN-DiscoBalloon): a disc that grows from the pop across the board and paints every
    ///     paintable balloon it reaches rainbow.
    /// </summary>
    public interface IDiscoWaveSettings
    {
        /// <summary>Seconds the disc takes to reach the farthest slot, easing out.</summary>
        float Seconds { get; }

        /// <summary>
        ///     The nudge each reached balloon gets, away from the pop (<see cref="NudgeType.Wave" />). An array, as
        ///     <see cref="NudgeMessage" /> takes; never written to.
        /// </summary>
        NudgeOverride[] NudgeOverrides { get; }

        /// <summary>A painted balloon's burst of its old colour, as a share of a pop's.</summary>
        float BurstScale { get; }

        /// <summary>The disc's material, which holds its edge and wash. Required: with none the disc isn't drawn.</summary>
        Material DiscMaterial { get; }

        string SortingLayerName { get; }

        int SortingOrder { get; }
    }
}
