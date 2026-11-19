using BalloonParty.UI.ClothRibbon;
using UnityEngine;

namespace BalloonParty.Game
{
    /// <summary>
    ///     One launch balloon's motion, with nothing of the engine's in it: its body's centre wandering a room
    ///     (<see cref="BalloonWander" />) and the line and ribbon hanging from its knot (<see cref="RibbonCloth" />).
    ///     <see cref="LaunchBalloonView" /> makes one from its settings and draws it.
    /// </summary>
    internal sealed class BalloonDrift
    {
        private readonly BalloonWander _wander;

        /// <summary>The body's centre, bob included.</summary>
        internal Vector2 Centre => _wander.Shown;

        internal RibbonCloth Cloth { get; }

        /// <summary>
        ///     Starts at <paramref name="start" /> in <paramref name="room" />, the line hanging straight down from
        ///     the knot, <paramref name="knotFromCentre" /> from the body's centre, and the ribbon laid out flat.
        /// </summary>
        internal BalloonDrift(
            BalloonWanderSettings wander, RibbonCloth cloth, Rect room, Vector2 start, Vector2 knotFromCentre, int seed)
        {
            _wander = new BalloonWander(wander, room, default, start, seed);
            Cloth = cloth;
            Cloth.Place(Centre + knotFromCentre);
        }

        /// <summary>
        ///     Moves the balloon on and lets what hangs from it follow. The knot's place is asked for each step: it
        ///     comes in toward the centre as the balloon shrinks on a press.
        /// </summary>
        internal void Step(float deltaTime, Vector2 knotFromCentre)
        {
            _wander.Step(deltaTime);
            Cloth.Step(Centre + knotFromCentre, deltaTime, null);
        }
    }
}
