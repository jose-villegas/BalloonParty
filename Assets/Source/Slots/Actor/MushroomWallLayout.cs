using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>One cap of a layout: the slot it stands in and the wall it belongs to.</summary>
    internal readonly struct MushroomWallCap
    {
        public readonly Vector2Int Slot;

        /// <summary>Which wall of the layout, 0 or more. The caps of one wall stand in neighbouring slots.</summary>
        public readonly int Wall;

        public MushroomWallCap(Vector2Int slot, int wall)
        {
            Slot = slot;
            Wall = wall;
        }
    }

    /// <summary>
    ///     One way a level's mushrooms stand: two to four walls of caps, with what the layout measured when it was
    ///     baked. An entry of <see cref="MushroomWallCatalogue" />.
    /// </summary>
    internal sealed class MushroomWallLayout
    {
        /// <summary>The caps, wall by wall. A cap's place in the list is the id its bumper circle carries.</summary>
        public IReadOnlyList<MushroomWallCap> Caps { get; }

        public int Walls { get; }

        /// <summary>
        ///     Of the thrower's aim angles, how many armed the lance when the layout was baked, on a board with
        ///     nothing else on it.
        /// </summary>
        public int ArmingAngles { get; }

        /// <summary>The longest unbroken run of those angles: the widest window a player can aim into.</summary>
        public int WidestWindowAngles { get; }

        internal MushroomWallLayout(IReadOnlyList<MushroomWallCap> caps, int armingAngles, int widestWindowAngles)
        {
            Caps = caps;
            ArmingAngles = armingAngles;
            WidestWindowAngles = widestWindowAngles;
            var walls = 0;
            for (var i = 0; i < caps.Count; i++)
            {
                walls = Mathf.Max(walls, caps[i].Wall + 1);
            }

            Walls = walls;
        }
    }
}
