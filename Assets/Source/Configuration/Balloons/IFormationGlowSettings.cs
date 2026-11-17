using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     The blue light ring around unbreakables in formation (<c>Balloon/View/FormationGlowView</c>): a ring
    ///     round each member that merges with its neighbours' like metaballs, lit only as members draw close.
    /// </summary>
    public interface IFormationGlowSettings
    {
        bool Enabled { get; }

        /// <summary>World radius of a lone member's ring.</summary>
        float RingRadius { get; }

        /// <summary>World reach of a member's field: past it, it adds nothing. Two rings join closer than about 2.5 ring radii apart at a reach of 2.</summary>
        float Reach { get; }

        /// <summary>World half-width of the ring's line of light.</summary>
        float RingWidth { get; }

        /// <summary>How strongly the inside of a ring is lit, against its line.</summary>
        float Fill { get; }

        /// <summary>Brightness over the palette blue; above 1 the ring blooms.</summary>
        float HdrBoost { get; }

        /// <summary>A member this close to another (world units) glows fully.</summary>
        float FullDistance { get; }

        /// <summary>A member this far from every other glows not at all.</summary>
        float FadeDistance { get; }

        /// <summary>
        ///     Seconds a ring takes to dissolve once its member is disconnected (out of formation, alone, popped or
        ///     cleared), and to dissolve in once it's connected.
        /// </summary>
        float DissolveSeconds { get; }

        /// <summary>Grains per world unit of the noise a dissolving ring erodes along.</summary>
        float DissolveNoiseScale { get; }

        string SortingLayerName { get; }

        int SortingOrder { get; }

        /// <summary><c>BalloonParty/Sprite/FormationGlow</c>, referenced so builds keep it.</summary>
        Shader Shader { get; }
    }
}
