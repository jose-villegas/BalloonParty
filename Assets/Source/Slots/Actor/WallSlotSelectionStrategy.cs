using System.Collections.Generic;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     The slots of a meadow level's mushrooms: walls of caps, from the baked catalogue
    ///     (<see cref="MushroomWallPicker" />). <c>StaticActorSpawner</c> uses it for the Mushroom entry, whatever
    ///     placement mode that entry has, and asks it for as many walls as the level's gate says; it answers with
    ///     the caps, wall by wall, and the spawner asks which wall each belongs to (<see cref="WallOf" />).
    /// </summary>
    /// <remarks>
    ///     It reads the board at the moment it is asked: the slots nothing stands in are the ones handed to it.
    ///     The spawner asks it before it places anything else, so the pick depends on the level's seed alone and
    ///     a retry stands the walls where they stood. What is placed after the mushrooms (the clouds, the bushes)
    ///     keeps off the slots next to a cap (<see cref="IsBesideACap" />), so a wall reads as a wall.
    /// </remarks>
    internal sealed class WallSlotSelectionStrategy : ISlotSelectionStrategy
    {
        // A slot's neighbours stand one slot's width away, to a rounding error; the next nearest are 1.7 of it.
        private const float NeighbourReach = 1.2f;

        private readonly SlotGrid _grid;
        private readonly IMushroomSettings _settings;
        private readonly IMushroomPlacementSource _source;
        private readonly HashSet<Vector2Int> _free = new();
        private readonly List<MushroomWallCap> _placed = new();

        /// <summary>The caps of the layout picked last, wall by wall. Empty when none was.</summary>
        internal IReadOnlyList<MushroomWallCap> Placed => _placed;

        internal WallSlotSelectionStrategy(SlotGrid grid, IMushroomSettings settings, IMushroomPlacementSource source)
        {
            _grid = grid;
            _settings = settings;
            _source = source;
        }

        /// <summary>
        ///     The caps of a layout of <paramref name="count" /> walls, wall by wall, or of fewer walls when no
        ///     layout of that many fits the board; none when no layout fits at all.
        ///     <paramref name="maxPerCluster" /> is not used: how long a wall is belongs to the layout.
        /// </summary>
        public List<Vector2Int> SelectSlots(IReadOnlyList<Vector2Int> emptySlots, int count, int maxPerCluster = 0)
        {
            _placed.Clear();
            var slots = new List<Vector2Int>();
            if (count <= 0 || emptySlots.Count == 0)
            {
                return slots;
            }

            _free.Clear();
            for (var i = 0; i < emptySlots.Count; i++)
            {
                _free.Add(emptySlots[i]);
            }

            var placement = _settings.Placement;
            var layout = MushroomWallPicker.Pick(new MushroomWallPick
            {
                FreeSlots = _free,
                GridSize = new Vector2Int(_grid.Columns, _grid.Rows),
                Walls = count,
                Seed = _source.LayoutSeed,
                Limits = placement.WithBottomRows(Mathf.Max(placement.BottomRowsKeptClear, _source.BottomRowsReserved)),
            });
            if (layout == null)
            {
                return slots;
            }

            for (var i = 0; i < layout.Caps.Count; i++)
            {
                _placed.Add(layout.Caps[i]);
                slots.Add(layout.Caps[i].Slot);
            }

            return slots;
        }

        /// <summary>The wall of the cap at <paramref name="placeInSelection" /> of the slots answered last.</summary>
        internal int WallOf(int placeInSelection)
        {
            return placeInSelection >= 0 && placeInSelection < _placed.Count
                ? _placed[placeInSelection].Wall
                : placeInSelection;
        }

        /// <summary>
        ///     Whether <paramref name="slot" /> is a neighbour of a cap placed last. A bush there would read as one
        ///     more piece of the wall. Further off a bush is in nobody's way: the ball flies over bushes, so the
        ///     space between two walls stays what the layout measured whatever stands in it.
        /// </summary>
        internal bool IsBesideACap(Vector2Int slot)
        {
            if (_placed.Count == 0 || _grid.Columns < 2)
            {
                return false;
            }

            var first = _grid.IndexToWorldPosition(Vector2Int.zero);
            var second = _grid.IndexToWorldPosition(new Vector2Int(1, 0));
            var reach = NeighbourReach * Mathf.Abs(second.x - first.x);
            var center = (Vector2)_grid.IndexToWorldPosition(slot);
            for (var i = 0; i < _placed.Count; i++)
            {
                if (Vector2.Distance(center, _grid.IndexToWorldPosition(_placed[i].Slot)) <= reach)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
