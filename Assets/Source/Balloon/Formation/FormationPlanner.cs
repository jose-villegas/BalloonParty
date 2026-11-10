using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     Finds where a formation of one shape can stand and which candidate goes to which of its slots.
    ///     Reads the grid only; the caller moves the members. A formation slot must be empty or already
    ///     hold a formation member, and never sits in the bottom <see cref="IUnbreakableFormationSettings.BottomClearance" />
    ///     rows. Among the spots that fit, the planner picks at random among the few with the least total
    ///     travel.
    /// </summary>
    internal sealed class FormationPlanner
    {
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
            var count = Mathf.Min(candidates.Count, settings.MaxMembers);
            var lastRow = grid.Rows - 1 - settings.BottomClearance;
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

                    if (!BuildSlots(grid, shape, anchor, count, lastRow) || !AllSlotsFree(grid))
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
        // host). False when any slot falls off the board or into the bottom clearance.
        private bool BuildSlots(SlotGrid grid, FormationShape shape, Vector2Int anchor, int count, int lastRow)
        {
            _slots.Clear();
            switch (shape)
            {
                case FormationShape.Horizontal:
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
                    BuildCoverSlots(grid, anchor, count);
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

        // The host's lower neighbours (one of them is off the board at an edge), then the row below the
        // host grows outward, alternating right and left, until it holds count slots. Stops short when
        // the row runs out both ways.
        private void BuildCoverSlots(SlotGrid grid, Vector2Int host, int count)
        {
            var left = HexCoordinates.DiagonalDown(host, false);
            var right = HexCoordinates.DiagonalDown(host, true);
            if (left.x >= 0)
            {
                _slots.Add(left);
            }

            if (right.x < grid.Columns && _slots.Count < count)
            {
                _slots.Add(right);
            }

            var step = 1;
            while (_slots.Count < count)
            {
                var nextRight = new Vector2Int(right.x + step, right.y);
                var nextLeft = new Vector2Int(left.x - step, left.y);
                var rightFits = nextRight.x < grid.Columns;
                var leftFits = nextLeft.x >= 0;
                if (!rightFits && !leftFits)
                {
                    return;
                }

                if (rightFits)
                {
                    _slots.Add(nextRight);
                }

                if (leftFits && _slots.Count < count)
                {
                    _slots.Add(nextLeft);
                }

                step++;
            }
        }

        private bool AllSlotsFree(SlotGrid grid)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!grid.IsEmpty(slot.x, slot.y) && grid.At(slot) is not IWriteableFormationMember)
                {
                    return false;
                }
            }

            return true;
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
