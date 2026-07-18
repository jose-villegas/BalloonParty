using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the screen-space GI approximation (see @ref arch_screen_space_light).</summary>
    internal interface IScreenSpaceLightSettings
    {
        /// <summary>The smear + overlay shaders. Serialized so device builds keep them (Hidden shaders only
        /// reached by <c>Shader.Find</c> are stripped otherwise).</summary>
        Shader SmearShader { get; }
        Shader OverlayShader { get; }

        /// <summary>How far an object's shadow/bleed reaches, in world units.</summary>
        float SmearDistance { get; }

        /// <summary>Divisor applied to the capture resolution for the smear/work targets. The light
        /// buffer is low-frequency (blurred, composited multiplicatively), so it tolerates running well
        /// below capture resolution — pass 1's box soften doubles as the upsample filter and the overlay
        /// samples it bilinearly.</summary>
        int SmearDownscale { get; }

        /// <summary>Per-tap weight decay along the march — lower dies off faster.</summary>
        float TapDecay { get; }

        /// <summary>Taps skipped at the march start so occluders don't fully self-shadow.</summary>
        float TapStart { get; }

        /// <summary>How aggressively the cone march widens — each tap samples a higher mip level
        /// (<c>mip = spread × log₂(1 + tapIndex)</c>). 0 disables (flat march, all mip 0).</summary>
        float MipSpread { get; }

        /// <summary>Shadow-specific mip spread — typically higher than <see cref="MipSpread"/> so shadow
        /// taps soften faster with distance (distance-dependent penumbra). 0 = same as bounce spread.</summary>
        float ShadowMipSpread { get; }

        /// <summary>Weight of the three secondary bounce directions (perpendicular + opposite) relative to
        /// the primary (toward-light) direction. 0 = single-direction bounce (current), 1 = all four
        /// directions contribute equally.</summary>
        float SecondaryBounceWeight { get; }

        /// <summary>Shadow darkening intensity (0 = no shadow, 1 = fully darkened).</summary>
        float ShadowStrength { get; }

        /// <summary>Tint applied to shadowed regions (lerped from white toward this by shadow amount).</summary>
        Color ShadowTint { get; }

        /// <summary>Bounce color bleed strength (scene color deviation from ambient × this).</summary>
        float BounceStrength { get; }
    }
}
