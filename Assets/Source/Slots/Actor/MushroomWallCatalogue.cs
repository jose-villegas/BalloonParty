using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     The layouts a meadow level's mushrooms are dealt from, by how many walls they have. Every one was flown
    ///     from every angle the thrower can aim at and kept because of what it measured
    ///     (<see cref="MushroomLayoutBand" />), so a level picks a layout and flies nothing.
    /// </summary>
    /// <remarks>
    ///     The entries are in <c>MushroomWallCatalogue.Generated.cs</c>, written by the editor's baker
    ///     (<c>Tools/BalloonParty/Bake Mushroom Wall Catalogue</c>) and never by hand. They hold for the board, the
    ///     cannon, the aim grid and the mushroom settings they were baked with (<see cref="BakedWith" />); a test
    ///     flies every entry again and fails when one of those has moved.
    /// </remarks>
    internal static partial class MushroomWallCatalogue
    {
        // An entry is its arming angles, its widest window in angles, then its caps.
        private const int CapsStart = 2;

        private static readonly MushroomWallLayout[] None = Array.Empty<MushroomWallLayout>();

        private static MushroomWallLayout[][] _byWalls;

        /// <summary>The fewest walls any layout of the catalogue has.</summary>
        internal static int FewestWalls => FirstWalls;

        /// <summary>The most walls any layout of the catalogue has.</summary>
        internal static int MostWalls => FirstWalls + Entries.Length - 1;

        /// <summary>The layouts of <paramref name="walls" /> walls, in the order they were baked. Empty when the
        /// catalogue has none of that many.</summary>
        internal static IReadOnlyList<MushroomWallLayout> For(int walls)
        {
            _byWalls ??= Decode();
            var index = walls - FirstWalls;
            return index >= 0 && index < _byWalls.Length ? _byWalls[index] : None;
        }

        /// <summary>A cap as an entry holds it: its wall, its column and its row in one number.</summary>
        internal static int Pack(in MushroomWallCap cap)
        {
            return (cap.Wall << 8) | (cap.Slot.x << 4) | cap.Slot.y;
        }

        private static MushroomWallLayout[][] Decode()
        {
            var byWalls = new MushroomWallLayout[Entries.Length][];
            for (var w = 0; w < Entries.Length; w++)
            {
                var layouts = new MushroomWallLayout[Entries[w].Length];
                for (var i = 0; i < layouts.Length; i++)
                {
                    var entry = Entries[w][i];
                    var caps = new MushroomWallCap[entry.Length - CapsStart];
                    for (var c = 0; c < caps.Length; c++)
                    {
                        var packed = entry[CapsStart + c];
                        caps[c] = new MushroomWallCap(new Vector2Int((packed >> 4) & 0xF, packed & 0xF), packed >> 8);
                    }

                    layouts[i] = new MushroomWallLayout(caps, entry[0], entry[1]);
                }

                byWalls[w] = layouts;
            }

            return byWalls;
        }
    }
}
