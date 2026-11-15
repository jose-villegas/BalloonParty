namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the clear circle every ball in flight carries (see <c>Projectile/Clearance</c>).</summary>
    internal interface IClearanceSettings
    {
        /// <summary>World radius of a full-strength circle.</summary>
        float Radius { get; }

        /// <summary>Width of the circle's smooth edge, as a share of its radius.</summary>
        float RimSoftness { get; }

        /// <summary>Seconds a launched ball's circle takes to reach full strength.</summary>
        float FadeIn { get; }

        /// <summary>Seconds a landed ball's circle lingers where it was last seen while it fades out.</summary>
        float FadeOut { get; }

        /// <summary>Alpha left of an effect at the centre of a full-strength circle.</summary>
        float EffectFloorAlpha { get; }

        /// <summary>World units per second a score formation slides out of a circle.</summary>
        float FormationPushSpeed { get; }

        bool PushFormations { get; }

        bool BendTrails { get; }

        bool FadeEffects { get; }
    }
}
