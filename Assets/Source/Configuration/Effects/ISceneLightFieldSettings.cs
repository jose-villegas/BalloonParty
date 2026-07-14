namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the scene-light field (see @ref plan_lighting "Milestone 3").</summary>
    internal interface ISceneLightFieldSettings
    {
        /// <summary>Field RT density. Higher = smoother colour/light regions at a larger (but still tiny,
        /// dirty-gated) RT. Far finer than the disturbance field's, which ticks every frame.</summary>
        int TexelsPerUnit { get; }

        /// <summary>Max simultaneous lights composited in one accumulate batch. Capped by the accumulate
        /// shader's compile-time stamp array, so raising it past that needs a shader edit too.</summary>
        int MaxLights { get; }

        /// <summary>Ceiling on the summed magnitude boost overlapping lights can add above the rest
        /// (soft-clamped in the accumulate shader) so overlaps never blow R out.</summary>
        float AccumulationCeiling { get; }

        /// <summary>Radial falloff exponent for a light's magnitude: <c>(1 - dist/radius)^power</c>. 1 = a
        /// linear cone; higher concentrates the light near its centre with a longer dim tail. Shapes both
        /// the magnitude AND (via its gradient) how sharply the direction points toward the light.</summary>
        float FalloffPower { get; }

        /// <summary>|grad R| at/below which the direction stays the global light, and at/above which it's
        /// fully the local gradient — the band over which a light "captures" the direction around it.</summary>
        float DirectionOnset { get; }
        float DirectionFull { get; }
    }
}
