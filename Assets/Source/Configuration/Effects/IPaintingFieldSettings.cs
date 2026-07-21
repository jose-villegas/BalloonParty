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
    }
}
