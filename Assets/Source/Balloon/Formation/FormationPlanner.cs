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
    ///     bottom <see cref="IUnbreakableFormationSettings.HorizontalBottomClearance" />), and never where it would
    ///     link the board's unbreakables side to side (<see cref="ShotWall" />). Among the spots that fit, the
    ///     planner picks at random among the few with the least total travel.
    /// </summary>
    internal sealed class FormationPlanner
    {
        // A cover is a shield between the cannon and the item: the two slots below it first, then its sides, and
        // only once those are all taken the two above. Six closes the ring.
        internal const int CoverMembers = 6;
        internal const int CoverMinMembers = 3;

        // Hex directions (HexCoordinates.Step) in the order a cover takes them: below, the sides, above. The two
        // orders differ only in which of a pair comes first, so a cover with an odd count tries both.
        private static readonly int[][] CoverOrders =
        {
            new[] { 1, 2, 0, 3, 5, 4 },
            new[] { 2, 1, 3, 0, 4, 5 },
        };

        private readonly List<Vector2Int> _slots = new();
        private readonly List<Vector2Int> _firstCover = new();
        private readonly List<Spot> _spots = new();
        private readonly List<Pair> _pairs = new();
        private readonly List<bool> _candidateUsed = new();
        private readonly List<bool> _slotUsed = new();
        private readonly List<FormationAssignment> _scratch = new();
        private readonly ShotWall _wall = new();

        // Whether the board was walled before this plan; a wall something else made is no reason to stand still.
        private bool _walledBefore;

        // The fewest a cover may be, an edge's trim included: below MinMembers it would break up at once.
        private int _coverFloor;

        /// <summary>
        ///     Plans <paramref name="shape" /> for up to <see cref="IUnbreakableFormationSettings.MaxMembers" />
        ///     (an item cover, three to six)
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

            _coverFloor = CoverFloor(settings);
            _wall.Load(grid);
            _walledBefore = _wall.Spans();
            CollectSpots(grid, shape, candidates, count, lastRow);
            if (_spots.Count == 0)
            {
                return false;
            }

            _spots.Sort(SpotByCost.Instance);
            var choices = Mathf.Min(_spots.Count, Mathf.Max(1, settings.SpotChoices));
            var chosen = _spots[Mathf.Clamp(pickIndex(choices), 0, choices - 1)];

            BuildSlots(grid, shape, chosen.Anchor, count, lastRow, chosen.Variant);
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
                    if (shape != FormationShape.ItemCover)
                    {
                        if (BuildSlots(grid, shape, anchor, count, lastRow, 0))
                        {
                            TryAddSpot(grid, candidates, count, anchor, 0);
                        }
                    }
                    else if (HostsCoveredItem(grid, anchor))
                    {
                        CollectCoverSpots(grid, candidates, count, lastRow, anchor);
                    }
                }
            }
        }

        // One spot per cover order, unless both orders come out the same (an even cover, or one an edge trims).
        private void CollectCoverSpots(
            SlotGrid grid, IReadOnlyList<IWriteableFormationMember> candidates, int count, int lastRow,
            Vector2Int host)
        {
            _firstCover.Clear();
            for (var variant = 0; variant < CoverOrders.Length; variant++)
            {
                if (!BuildSlots(grid, FormationShape.ItemCover, host, count, lastRow, variant))
                {
                    continue;
                }

                if (variant == 0)
                {
                    _firstCover.AddRange(_slots);
                }
                else if (SameSlots(_firstCover, _slots))
                {
                    continue;
                }

                TryAddSpot(grid, candidates, count, host, variant);
            }
        }

        private void TryAddSpot(
            SlotGrid grid, IReadOnlyList<IWriteableFormationMember> candidates, int count, Vector2Int anchor,
            int variant)
        {
            if (!AllSlotsFree(grid, candidates))
            {
                return;
            }

            var cost = Assign(candidates, count, _scratch);
            if (ClosesWall(_scratch))
            {
                return;
            }

            _spots.Add(new Spot
            {
                Anchor = anchor,
                Variant = variant,
                Cost = cost,
                Order = _spots.Count,
            });
        }

        private static bool SameSlots(IReadOnlyList<Vector2Int> a, IReadOnlyList<Vector2Int> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < b.Count; i++)
            {
                if (!Holds(a, b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Holds(IReadOnlyList<Vector2Int> slots, Vector2Int slot)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] == slot)
                {
                    return true;
                }
            }

            return false;
        }

        // Fills _slots with the shape's slots from anchor (a line's first slot, or the covered item's host).
        // False when a slot falls off the board or into the bottom clearance. A cover skips a neighbour off the
        // board, which the edge already shields, and takes the next in its order (variant picks which).
        private bool BuildSlots(
            SlotGrid grid, FormationShape shape, Vector2Int anchor, int count, int lastRow, int variant)
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
                    AddCoverSlots(grid, anchor, count, variant);
                    break;
            }

            // A cover may come out short at an edge, but a corner leaves fewer neighbours than its smallest.
            if (_slots.Count < (shape == FormationShape.ItemCover ? _coverFloor : count))
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

        private void AddCoverSlots(SlotGrid grid, Vector2Int host, int count, int variant)
        {
            var order = CoverOrders[variant];
            for (var i = 0; i < order.Length && _slots.Count < count; i++)
            {
                var neighbour = HexCoordinates.Step(host, order[i]);
                if (grid.InBounds(neighbour))
                {
                    _slots.Add(neighbour);
                }
            }
        }

        // A cover takes three to six, whatever MaxMembers says: six closes the ring. A horizontal line stops at
        // HorizontalMaxMembers so it never spans the board, a snake at SnakeMaxMembers; a diagonal leaves lanes
        // open and takes up to MaxMembers.
        private static int MemberCount(
            FormationShape shape, int candidates, IUnbreakableFormationSettings settings)
        {
            if (shape == FormationShape.ItemCover)
            {
                return candidates >= CoverFloor(settings)
                    ? Mathf.Min(candidates, CoverMembers)
                    : 0;
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

        // Tries the plan on the wall: every member off its slot, onto its target, then back.
        private bool ClosesWall(IReadOnlyList<FormationAssignment> assignments)
        {
            if (_walledBefore)
            {
                return false;
            }

            Move(assignments, toTargets: true);
            var closes = _wall.Spans();
            Move(assignments, toTargets: false);
            return closes;
        }

        private void Move(IReadOnlyList<FormationAssignment> assignments, bool toTargets)
        {
            for (var i = 0; i < assignments.Count; i++)
            {
                var a = assignments[i];
                _wall.Set(toTargets ? a.Member.SlotIndex.Value : a.Target, false);
            }

            for (var i = 0; i < assignments.Count; i++)
            {
                var a = assignments[i];
                _wall.Set(toTargets ? a.Target : a.Member.SlotIndex.Value, true);
            }
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

        private static int CoverFloor(IUnbreakableFormationSettings settings)
        {
            return Mathf.Max(CoverMinMembers, settings.MinMembers);
        }

        // Only the items that hit hard are worth shielding: a bomb, a snipe and a lightning.
        private static bool HostsCoveredItem(SlotGrid grid, Vector2Int slot)
        {
            return !grid.IsEmpty(slot.x, slot.y)
                   && grid.At(slot) is IHasItemSlot host
                   && host.Item.Value is ItemType.Bomb or ItemType.Snipe or ItemType.Lightning;
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
            public int Variant;
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
