using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     Finds where a formation of one shape can stand and which candidate goes to which of its slots.
    ///     Reads the grid only; the caller moves the members. A formation slot must be empty or already
    ///     hold one of the candidates, so a formation never plans over another formation. It never sits in
    ///     the bottom <see cref="IUnbreakableFormationSettings.BottomClearance" /> rows (a horizontal line, the
    ///     bottom <see cref="IUnbreakableFormationSettings.HorizontalBottomClearance" />). Among the spots that
    ///     fit, the planner picks at random among the few with the least total travel.
    /// </summary>
    internal sealed class FormationPlanner
    {
        // Every neighbour of the item: a ring with a gap would leave the item a way to be hit, and to move.
        internal const int CoverMembers = 6;

        private readonly Vector2Int[] _neighbours = new Vector2Int[CoverMembers];
        private readonly List<Vector2Int> _slots = new();
        private readonly List<Spot> _spots = new();
        private readonly List<Pair> _pairs = new();
        private readonly List<bool> _candidateUsed = new();
        private readonly List<bool> _slotUsed = new();
        private readonly List<FormationAssignment> _scratch = new();

        /// <summary>
        ///     Plans <paramref name="shape" /> for up to <see cref="IUnbreakableFormationSettings.MaxMembers" />
        ///     of <paramref name="candidates" />, filling <paramref name="assignmentsOut" /> with one entry per
        ///     formation slot. False when no spot fits. <paramref name="pickIndex" /> maps a count n to an
        ///     index in [0, n).
        /// </summary>
        internal bool TryPlan(
            SlotGrid grid, FormationShape shape, IReadOnlyList<IWriteableFormationMember> candidates,
            IUnbreakableFormationSettings settings, Func<int, int> pickIndex,
            List<FormationAssignment> assignmentsOut)
        {
            assignmentsOut.Clear();
            var isRow = shape is FormationShape.Horizontal or FormationShape.Snake;
            var count = MemberCount(shape, candidates.Count, settings);
            // A row across the board this close to the cannon walls the player in; a diagonal leaves lanes open.
            // A snake starts as a row and walks above the same rows.
            var clearance = isRow
                ? Mathf.Max(settings.HorizontalBottomClearance, settings.BottomClearance)
                : settings.BottomClearance;
            var lastRow = grid.Rows - 1 - clearance;
            if (count < 1 || lastRow < 0)
            {
                return false;
            }

            CollectSpots(grid, shape, candidates, count, lastRow);
            if (_spots.Count == 0)
            {
                return false;
            }

            _spots.Sort(SpotByCost.Instance);
            var choices = Mathf.Min(_spots.Count, Mathf.Max(1, settings.SpotChoices));
            var chosen = _spots[Mathf.Clamp(pickIndex(choices), 0, choices - 1)];

            BuildSlots(grid, shape, chosen.Anchor, count, lastRow);
            Assign(candidates, count, assignmentsOut);
            return true;
        }

        private void CollectSpots(
            SlotGrid grid, FormationShape shape, IReadOnlyList<IWriteableFormationMember> candidates, int count,
            int lastRow)
        {
            _spots.Clear();
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    var anchor = new Vector2Int(col, row);
                    if (shape == FormationShape.ItemCover && !HostsItem(grid, anchor))
                    {
                        continue;
                    }

                    if (!BuildSlots(grid, shape, anchor, count, lastRow) || !AllSlotsFree(grid, candidates))
                    {
                        continue;
                    }

                    _spots.Add(new Spot
                    {
                        Anchor = anchor,
                        Cost = Assign(candidates, count, _scratch),
                        Order = _spots.Count,
                    });
                }
            }
        }

        // Fills _slots with the shape's slots from anchor (a line's first slot, or the covered item's
        // host, whose six neighbours make the ring). False when any slot falls off the board or into the
        // bottom clearance, so an item on the board's edge is never covered.
        private bool BuildSlots(SlotGrid grid, FormationShape shape, Vector2Int anchor, int count, int lastRow)
        {
            _slots.Clear();
            switch (shape)
            {
                case FormationShape.Horizontal:
                case FormationShape.Snake:
                    for (var i = 0; i < count; i++)
                    {
                        _slots.Add(new Vector2Int(anchor.x + i, anchor.y));
                    }

                    break;

                case FormationShape.DiagonalDownRight:
                case FormationShape.DiagonalDownLeft:
                    var right = shape == FormationShape.DiagonalDownRight;
                    var slot = anchor;
                    for (var i = 0; i < count; i++)
                    {
                        _slots.Add(slot);
                        slot = HexCoordinates.DiagonalDown(slot, right);
                    }

                    break;

                case FormationShape.ItemCover:
                    HexCoordinates.HexNeighborIndices(anchor.x, anchor.y, _neighbours);
                    _slots.AddRange(_neighbours);
                    break;
            }

            if (_slots.Count < count)
            {
                return false;
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                if (!grid.InBounds(_slots[i]) || _slots[i].y > lastRow)
                {
                    return false;
                }
            }

            return true;
        }

        // A cover is the whole ring or nothing. A horizontal line stops at HorizontalMaxMembers so it never
        // spans the board, a snake at SnakeMaxMembers; a diagonal leaves lanes open and takes up to MaxMembers.
        private static int MemberCount(
            FormationShape shape, int candidates, IUnbreakableFormationSettings settings)
        {
            if (shape == FormationShape.ItemCover)
            {
                return candidates >= CoverMembers ? CoverMembers : 0;
            }

            var most = shape switch
            {
                FormationShape.Horizontal when settings.HorizontalMaxMembers > 0 =>
                    Mathf.Min(settings.HorizontalMaxMembers, settings.MaxMembers),
                FormationShape.Snake when settings.SnakeMaxMembers > 0 =>
                    Mathf.Min(settings.SnakeMaxMembers, settings.MaxMembers),
                _ => settings.MaxMembers,
            };
            return Mathf.Min(candidates, most);
        }

        private bool AllSlotsFree(SlotGrid grid, IReadOnlyList<IWriteableFormationMember> candidates)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!grid.IsEmpty(slot.x, slot.y) && !IsCandidate(grid.At(slot), candidates))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCandidate(IWriteableSlotActor occupant, IReadOnlyList<IWriteableFormationMember> candidates)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                if (ReferenceEquals(candidates[i], occupant))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HostsItem(SlotGrid grid, Vector2Int slot)
        {
            return !grid.IsEmpty(slot.x, slot.y)
                   && grid.At(slot) is IHasItemSlot host
                   && host.Item.Value != ItemType.None;
        }

        // Nearest pair first: a candidate already standing on a formation slot always keeps it (distance
        // 0), so no member ever moves into a slot another member still holds. Returns the total travel.
        private int Assign(
            IReadOnlyList<IWriteableFormationMember> candidates, int count, List<FormationAssignment> assignmentsOut)
        {
            assignmentsOut.Clear();
            _pairs.Clear();
            _candidateUsed.Clear();
            _slotUsed.Clear();

            for (var c = 0; c < candidates.Count; c++)
            {
                _candidateUsed.Add(false);
                var from = candidates[c].SlotIndex.Value;
                for (var s = 0; s < _slots.Count; s++)
                {
                    _pairs.Add(new Pair { Candidate = c, Slot = s, Distance = HexCoordinates.Distance(from, _slots[s]) });
                }
            }

            for (var s = 0; s < _slots.Count; s++)
            {
                _slotUsed.Add(false);
            }

            _pairs.Sort(PairByDistance.Instance);

            var cost = 0;
            for (var i = 0; i < _pairs.Count && assignmentsOut.Count < count; i++)
            {
                var pair = _pairs[i];
                if (_candidateUsed[pair.Candidate] || _slotUsed[pair.Slot])
                {
                    continue;
                }

                _candidateUsed[pair.Candidate] = true;
                _slotUsed[pair.Slot] = true;
                cost += pair.Distance;
                assignmentsOut.Add(new FormationAssignment(candidates[pair.Candidate], _slots[pair.Slot]));
            }

            return cost;
        }

        private struct Spot
        {
            public Vector2Int Anchor;
            public int Cost;
            public int Order;
        }

        private struct Pair
        {
            public int Candidate;
            public int Slot;
            public int Distance;
        }

        // Cached comparers: List.Sort(Comparison) wraps the delegate in a new comparer on every call.
        private sealed class SpotByCost : IComparer<Spot>
        {
            internal static readonly SpotByCost Instance = new();

            public int Compare(Spot a, Spot b)
            {
                return a.Cost != b.Cost ? a.Cost - b.Cost : a.Order - b.Order;
            }
        }

        // Nearest pairs first; ties keep candidate then slot order so a plan is deterministic.
        private sealed class PairByDistance : IComparer<Pair>
        {
            internal static readonly PairByDistance Instance = new();

            public int Compare(Pair a, Pair b)
            {
                return a.Distance != b.Distance ? a.Distance - b.Distance
                    : a.Candidate != b.Candidate ? a.Candidate - b.Candidate
                    : a.Slot - b.Slot;
            }
        }
    }
}
