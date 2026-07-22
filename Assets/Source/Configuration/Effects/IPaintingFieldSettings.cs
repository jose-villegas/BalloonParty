using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the painting field (see <c>PaintingFieldService</c>).</summary>
    internal interface IPaintingFieldSettings
    {
        /// <summary>Blit shader for batched color stamps into the painting RT.</summary>
        Shader StampShader { get; }

        /// <summary>Blit shader for per-tick decay (fades opacity, clears dead tags).</summary>
        Shader DecayShader { get; }

        /// <summary>Painting-RT resolution per world unit.</summary>
        float TexelsPerUnit { get; }

        /// <summary>Opacity units lost per second (linear decay).</summary>
        float DecayRate { get; }

        /// <summary>Seconds between decay blit ticks (0 = every frame).</summary>
        float DecayTickInterval { get; }

        /// <summary>World-space radius of each paint stamp from the projectile trail.</summary>
        float StampRadius { get; }

        /// <summary>Base wind speed for smoke advection (world units/second).</summary>
        float WindSpeed { get; }

        /// <summary>0–1 base wind influence at normal projectile speed. Controls how much wind affects the trail overall.</summary>
        float WindInfluence { get; }

        /// <summary>Power curve controlling how quickly decaying paint becomes wind-susceptible. Higher = stays put longer.</summary>
        float WindAgeBias { get; }

        /// <summary>Normalized wind direction for smoke advection.</summary>
        Vector2 WindDirection { get; }
    }
}
