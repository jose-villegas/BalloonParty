using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>Prediction trace settings: how the line is segmented, how far it reaches,
    /// how many reflections it gives away, and its colour.</summary>
    public interface IPredictionTraceConfig
    {
        /// <summary>World length of one polyline segment — smoothness, not accuracy.</summary>
        float SegmentLength { get; }

        /// <summary>
        ///     Reflections the trace will draw before it stops — walls and balloon deflections
        ///     together, because to the player they are the same event: the line turned. The leg
        ///     leaving the last allowed reflection is still drawn; the one after it is not.
        /// </summary>
        /// <remarks>
        ///     Deliberately one budget rather than one per kind. The telegraph is an aid, not a
        ///     solution — showing the whole ricochet answers the shot for the player. Expected to
        ///     rise with progression.
        /// </remarks>
        int MaxReflections { get; }

        /// <summary>Segments before the trace gives up; times SegmentLength, its reach.</summary>
        int MaxSegments { get; }

        Color LineColor { get; }

        /// <summary>
        ///     Whether the trace also casts capsule scene-lights (one per leg, mirroring the drawn line)
        ///     while aiming. Off by default: an earlier attempt at this relit the actors the line crosses
        ///     and read as noise rather than a telegraph. Kept as a per-config toggle for experimentation
        ///     rather than deleted, since the underlying capsule-light machinery (<see
        ///     cref="BalloonParty.Shared.SceneLight.Light.Segment" />) is otherwise unused by this system.
        /// </summary>
        bool LightingEnabled { get; }

        /// <summary>Peak half-width of the capsule lights (one per trace leg), before <see cref="LightWidthCurve" /> tapers it.</summary>
        float LightHalfWidth { get; }

        /// <summary>Peak intensity of the capsule lights, before <see cref="LightFadeCurve" /> tapers it.</summary>
        float LightIntensity { get; }

        /// <summary>Radial falloff exponent for the capsule lights: (1 - dist/radius)^power.</summary>
        float LightFalloffPower { get; }

        /// <summary>
        ///     Intensity multiplier along the line from launch (t=0) to tip (t=1), sampled at each trace
        ///     leg's arc-length midpoint. Linear by default — see <see cref="LightWidthCurve" /> for why
        ///     an eased curve reads badly here.
        /// </summary>
        AnimationCurve LightFadeCurve { get; }

        /// <summary>
        ///     Half-width multiplier along the line from launch (t=0) to tip (t=1) — sampled by the
        ///     trace's total arc-length fraction, the same way as <see cref="LightFadeCurve" />, not
        ///     per-leg. A flat curve gives uniform width; the default is a straight linear decrease, not
        ///     an eased S-curve — with legs of very unequal length (a short post-bounce leg next to a
        ///     long straight one), an eased curve's flat plateaus at each end squeeze the whole visible
        ///     transition into whichever leg happens to straddle the curve's steep middle, reading as a
        ///     hard step between two near-uniform widths (or, for <see cref="LightFadeCurve" />, a strong
        ///     leg next to a uniformly weak one) rather than a smooth change. Linear spreads it evenly
        ///     across distance travelled regardless of how the legs are split.
        /// </summary>
        AnimationCurve LightWidthCurve { get; }
    }
}
