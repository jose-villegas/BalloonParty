using System.Collections.Generic;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    /// <summary>
    ///     Whether the balloons no ordinary shot gets through (unbreakables, deflectors) link one side of the board
    ///     to the other along hex neighbours. Such a chain shuts every ordinary shot out of the board above it, and a
    ///     standing formation also holds back the balloons below, so whatever moves an unbreakable must not close
    ///     one. Load the board, try a move with <see cref="Set" />, ask <see cref="Spans" />, then undo the move.
    /// </summary>
    internal sealed class ShotWall
    {
        private readonly Vector2Int[] _neighbours = new Vector2Int[HexCoordinates.Directions];
        private readonly Stack<Vector2Int> _open = new();
        private bool[,] _blocked = new bool[0, 0];
        private bool[,] _seen = new bool[0, 0];
        private int _columns;
        private int _rows;

        /// <summary>Whether <paramref name="actor" /> stops every ordinary shot for good, so it can wall.</summary>
        internal static bool Walls(IWriteableSlotActor actor)
        {
            // A worn deflector (a tough balloon, a gatekeeper) gives way to enough hits; only a lasting one walls.
            return actor is IDeflectsShots { DeflectsOrdinaryHit: true } && actor is not IHasDurability;
        }

        internal void Load(SlotGrid grid)
        {
            _columns = grid.Columns;
            _rows = grid.Rows;
            if (_blocked.GetLength(0) != _columns || _blocked.GetLength(1) != _rows)
            {
                _blocked = new bool[_columns, _rows];
                _seen = new bool[_columns, _rows];
            }

            for (var col = 0; col < _columns; col++)
            {
                for (var row = 0; row < _rows; row++)
                {
                    _blocked[col, row] = !grid.IsEmpty(col, row) && Walls(grid.At(new Vector2Int(col, row)));
                }
            }
        }

        internal void Set(Vector2Int slot, bool walls)
        {
            if (slot.x >= 0 && slot.x < _columns && slot.y >= 0 && slot.y < _rows)
            {
                _blocked[slot.x, slot.y] = walls;
            }
        }

        /// <summary>
        ///     Whether a chain of walling slots runs from the first column to the last. Every row's end slot counts
        ///     as touching its side, odd rows' shifted ones too: a shot squeezing past one would be a gamble.
        /// </summary>
        internal bool Spans()
        {
            System.Array.Clear(_seen, 0, _seen.Length);
            for (var row = 0; row < _rows; row++)
            {
                if (_blocked[0, row] && !_seen[0, row] && ReachesLastColumn(new Vector2Int(0, row)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ReachesLastColumn(Vector2Int start)
        {
            _open.Clear();
            _open.Push(start);
            _seen[start.x, start.y] = true;
            while (_open.Count > 0)
            {
                var slot = _open.Pop();
                if (slot.x == _columns - 1)
                {
                    return true;
                }

                HexCoordinates.HexNeighborIndices(slot.x, slot.y, _neighbours);
                for (var i = 0; i < _neighbours.Length; i++)
                {
                    var next = _neighbours[i];
                    if (next.x < 0 || next.x >= _columns || next.y < 0 || next.y >= _rows
                        || !_blocked[next.x, next.y] || _seen[next.x, next.y])
                    {
                        continue;
                    }

                    _seen[next.x, next.y] = true;
                    _open.Push(next);
                }
            }

            return false;
        }
    }
}
