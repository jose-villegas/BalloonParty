using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>
    ///     Read-only tuning for the effects clearing (see @ref plan_effects_layer_clearing): how wide a clearing
    ///     each ball in flight opens in the effects drawn above the board, how long it lingers, and the targets'
    ///     resolutions.
    /// </summary>
    internal interface IEffectsClearingSettings
    {
        bool Enabled { get; }

        /// <summary>World units from the ball's path to the clearing's edge, before it starts to close.</summary>
        float ClearRadius { get; }

        /// <summary>Seconds a stamp takes to decay from full to nothing.</summary>
        float LingerSeconds { get; }

        /// <summary>Mask value where effects start to fade.</summary>
        float EdgeLow { get; }

        /// <summary>Mask value where effects reach full clearing.</summary>
        float EdgeHigh { get; }

        /// <summary>How much of the effects a full clearing removes: 1 hides them, below 1 leaves a trace.</summary>
        float Strength { get; }

        /// <summary>Decay on real time even while a ball flies; once none does, it always is.</summary>
        bool UseUnscaledTime { get; }

        /// <summary>Screen size over the clearing mask's size.</summary>
        int MaskDownscale { get; }

        /// <summary>Screen size over an effects map's size.</summary>
        int MapDownscale { get; }

        Shader MaskShader { get; }

        Shader CompositeShader { get; }
    }
}
