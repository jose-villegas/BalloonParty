using BalloonParty.Shared;
using BalloonParty.Slots.Actor;

namespace BalloonParty.Configuration.GridActors
{
    /// <summary>
    ///     Tuning of the meadow's mushrooms (@ref plan_meadow_mushrooms): the chain that pays the lance, the
    ///     bounce, where mushrooms may stand, and how a cap answers a hit.
    /// </summary>
    internal interface IMushroomSettings
    {
        /// <summary>The chain's numbers and switches, as <see cref="MushroomChainRule" /> takes them.</summary>
        MushroomChainRules Rules { get; }

        /// <summary>
        ///     Whether a bounce is bent toward a cap of another wall (<see cref="ForgivingKickRule" />). Off, every
        ///     bounce is the mirror reflection.
        /// </summary>
        bool ForgivingKick { get; }

        /// <summary>
        ///     The kick's cone and the two offsets it may land a ball at. The cone is the dial for how much skill a
        ///     rally asks; the baked layouts (<see cref="MushroomWallCatalogue" />) hold for the cone and offsets
        ///     they were baked with.
        /// </summary>
        BumperKickTuning Kick { get; }

        /// <summary>A cap's own radius in world units, without the ball's.</summary>
        float Radius { get; }

        /// <summary>The limits a level's walls of caps keep to, and what a layout must measure to be dealt.</summary>
        MushroomPlacementLimits Placement { get; }

        /// <summary>The most slots a level's permanent things take together: the mushrooms and the bushes share
        /// it, so the bushes get what the mushrooms leave.</summary>
        int PermanentSlotLimit { get; }

        /// <summary>How a cap leans away from a hit and wobbles back, as <c>MushroomCapSpring</c> takes it.</summary>
        MushroomLeanTuning Lean { get; }

        /// <summary>The spores a mushroom lets go: a puff of smoke and a few specks on a hit, more on a cut, a
        /// burst when a blast blows the cap off (<c>MushroomSporePlanner</c>).</summary>
        MushroomSporeTuning Spores { get; }

        /// <summary>How far a bomb's blast sways the mushrooms whose caps it leaves on, as a share of the bomb's
        /// kill radius (<see cref="MushroomBlastRule" />). Which caps it takes off is the bomb's own rule and no
        /// setting: the slots next to it.</summary>
        MushroomBlastTuning Blast { get; }

        /// <summary>How a cap a lance cut falls in two and how it pops back, as <c>MushroomCutTimeline</c> takes
        /// it. The look only: when a cap is cut and when it grows back is the rule's.</summary>
        MushroomCutTuning Cut { get; }

        /// <summary>
        ///     How many of the board's beats (<c>IBoardBeat</c>) a cap a lance cut stays gone: it grows back on
        ///     the beat that brings the count to this, the first being the next beat to fall after the cut. Always
        ///     at least 1.
        /// </summary>
        int RegrowBeats { get; }
    }
}
