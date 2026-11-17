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

        /// <summary>Clearing radius in world units for a ball with one shield; one with none left clears as one.</summary>
        float MinClearRadius { get; }

        /// <summary>Clearing radius in world units for a ball with <see cref="ShieldsForMaxRadius" /> shields or
        /// more; between the two it grows evenly with each shield.</summary>
        float MaxClearRadius { get; }

        /// <summary>The shield count at which a clearing reaches <see cref="MaxClearRadius" />.</summary>
        int ShieldsForMaxRadius { get; }

        /// <summary>Whether a clearing leaves a trail along the ball's path that lingers and closes, or is only a dome
        /// where the ball is.</summary>
        bool Trail { get; }

        /// <summary>Seconds a stamp takes to decay from full to nothing.</summary>
        float LingerSeconds { get; }

        /// <summary>Mask value where effects start to fade.</summary>
        float EdgeLow { get; }

        /// <summary>Mask value where effects reach full clearing.</summary>
        float EdgeHigh { get; }

        /// <summary>How much of the effects a full clearing removes: 1 hides them, below 1 leaves a trace.</summary>
        float Strength { get; }

        /// <summary>How far the glass dome over a ball bends the effects under its rim, as a fraction of its radius;
        /// 0 for none.</summary>
        float LensRefraction { get; }

        /// <summary>How far the dome's colours split apart, as a fraction of its bend.</summary>
        float LensDispersion { get; }

        /// <summary>Brightness of the glint and rim on the dome; 0 hides the glass.</summary>
        float LensHighlight { get; }

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
