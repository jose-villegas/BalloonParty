using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>What let a mushroom's spores go, from the least to the most.</summary>
    internal enum MushroomSporeKind : byte
    {
        /// <summary>A ball bounced off the cap.</summary>
        Hit,

        /// <summary>A lance cut the cap.</summary>
        Cut,

        /// <summary>A blast blew the cap off.</summary>
        Blast,
    }

    /// <summary>
    ///     One release of a mushroom's spores (@ref plan_meadow_mushrooms), as worked out by
    ///     <see cref="MushroomSporePlanner" />: a cone of smoke and a count of specks, where they leave from and
    ///     which way. Nothing here draws: the smoke goes to the smoke field and the specks to the mushroom's own
    ///     particles.
    /// </summary>
    internal readonly struct MushroomSporeBurst
    {
        public readonly MushroomSporeKind Kind;

        /// <summary>Where it leaves from, in world units.</summary>
        public readonly Vector2 Origin;

        /// <summary>The way it goes, a unit direction in world space.</summary>
        public readonly Vector2 Direction;

        /// <summary>How long the smoke's cone is, from <see cref="Origin" /> along <see cref="Direction" />.</summary>
        public readonly float Length;

        /// <summary>How wide the smoke's cone is at its far end.</summary>
        public readonly float EndWidth;

        /// <summary>How thick the smoke is, as a share of the smoke profile's own opacity.</summary>
        public readonly float Smoke;

        public readonly int Specks;

        /// <summary>How fast the specks leave, in world units a second.</summary>
        public readonly float SpeckSpeed;

        /// <summary>How wide the specks fan out about <see cref="Direction" />, in degrees.</summary>
        public readonly float FanDegrees;

        /// <summary>Seconds to wait before letting it go.</summary>
        public readonly float Delay;

        internal MushroomSporeBurst(
            MushroomSporeKind kind, Vector2 origin, Vector2 direction, float length, float endWidth, float smoke,
            int specks, float speckSpeed, float fanDegrees, float delay)
        {
            Kind = kind;
            Origin = origin;
            Direction = direction;
            Length = length;
            EndWidth = endWidth;
            Smoke = smoke;
            Specks = specks;
            SpeckSpeed = speckSpeed;
            FanDegrees = fanDegrees;
            Delay = delay;
        }
    }
}
