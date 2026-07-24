using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the smoke field (see <c>SmokeFieldService</c>).</summary>
    internal interface ISmokeFieldSettings
    {
        /// <summary>Blit shader for batched color stamps into the smoke RT.</summary>
        Shader StampShader { get; }

        /// <summary>Blit shader for per-tick decay (fades opacity, clears dead tags).</summary>
        Shader DecayShader { get; }

        /// <summary>Smoke-RT resolution per world unit.</summary>
        float TexelsPerUnit { get; }

        /// <summary>Opacity units lost per second (linear decay).</summary>
        float DecayRate { get; }

        /// <summary>Seconds between decay blit ticks (0 = every frame).</summary>
        float DecayTickInterval { get; }

        /// <summary>Base wind speed for smoke advection (world units/second).</summary>
        float WindSpeed { get; }

        /// <summary>0–1 base wind influence at normal projectile speed. Controls how much wind affects the trail overall.</summary>
        float WindInfluence { get; }

        /// <summary>Power curve controlling how quickly decaying paint becomes wind-susceptible. Higher = stays put longer.</summary>
        float WindAgeBias { get; }

        /// <summary>Normalized wind direction for smoke advection.</summary>
        Vector2 WindDirection { get; }

        /// <summary>Degrees the wind swings left/right from the base direction (0 = fixed).</summary>
        float WindSwingAngle { get; }

        /// <summary>How fast the wind swings back and forth (cycles per second).</summary>
        float WindSwingSpeed { get; }

        PaintProfile GetProfile(PaintSource source);
    }
}
