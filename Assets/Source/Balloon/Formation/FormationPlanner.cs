using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     Finds where a formation of one shape can stand and which candidate goes to which of its slots.
    ///     Reads the grid only; the caller moves the members. A formation slot must be empty, already hold one
    ///     of the candidates, or hold a balloon that can trade places (<see cref="SnakeWalk.CanPassThrough" />),
    ///     which the caller sends to a slot a member left; so a formation never plans over another formation, and
    ///     a full board doesn't keep the unbreakables apart. Each balloon it moves costs
    ///     <see cref="DisplacedBalloonCost" /> on top of the travel, so a spot with room wins. It never sits in
    ///     the bottom <see cref="IUnbreakableFormationSettings.BottomClearance" /> rows (a horizontal line, the
    ///     bottom <see cref="IUnbreakableFormationSettings.HorizontalBottomClearance" />), and never where it would
    ///     link the board's unbreakables side to side (<see cref="ShotWall" />). Among the spots that fit, the
    ///     planner picks at random among the few with the least total travel. A cover stands round a balloon
    ///     carrying a bomb, a snipe or a lightning, or round a gift balloon, whose spot counts as cheaper
    ///     (<see cref="IUnbreakableFormationSettings.GiftCoverPreference" />).
    /// </summary>
    internal sealed class FormationPlanner
    {
        // A cover is a shield between the cannon and the item: the two slots below it first, then its sides, and
        // only once those are all taken the two above. Six surround it.
        internal const int CoverMembers = 6;
        internal const int CoverMinMembers = 3;

        // A balloon a formation moves out of its way counts as this much travel, so where there is room the
        // formation takes it rather than shuffling the board.
        internal const int DisplacedBalloonCost = 3;

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

        // Slots of travel a gift's spot counts as cheaper, for each member of the cover.
        private float _giftPreference;

        // The deepest row a line across the board may lie on: a spinner's centre keeps above it.
        private int _lastFlatRow;

        /// <summary>
        ///     The slot of the balloon the last plan's cover stands round; null after any other shape, or a plan that
        ///     found no spot.
        /// </summary>
        internal Vector2Int? CoveredHost { get; private set; }

        /// <summary>The slot the last plan's ring or spinner turns about; null after any other shape, or a plan that found no spot.</summary>
        internal Vector2Int? TurnCentre { get; private set; }

        /// <summary>
        ///     Plans <paramref name="shape" /> for up to <see cref="IUnbreakableFormationSettings.MaxMembers" />
        ///     (an item cover, three to six)
        ///     of <paramref name="candidates" />, filling <paramref name="assignmentsOut" /> with one entry per
        ///     formation slot. False when no spot fits. <paramref name="pickIndex" /> maps a count n to an
        ///     index in [0, n). A spinner takes five, or three when fewer are free or its settings allow no more.
        ///     A ring takes <see cref="IUnbreakableFormationSettings.RingMinMembers" /> to
        ///     <see cref="IUnbreakableFormationSettings.RingMaxMembers" />, and with <paramref name="keep" /> (a
        ///     snake's head, coiling into the ring) only the spots that hold that slot, while any does.
        /// </summary>
        internal bool TryPlan(
            SlotGrid grid, FormationShape shape, IReadOnlyList<IWriteableFormationMember> candidates,
            IUnbreakableFormationSettings settings, Func<int, int> pickIndex,
            List<FormationAssignment> assignmentsOut, Vector2Int? keep = null)
        {
            assignmentsOut.Clear();
            CoveredHost = null;
            TurnCentre = null;
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
            _lastFlatRow = grid.Rows - 1 - Mathf.Max(settings.HorizontalBottomClearance, settings.BottomClearance);
            _giftPreference = Mathf.Max(0f, settings.GiftCoverPreference);
            _wall.Load(grid);
            _walledBefore = _wall.Spans();
            CollectSpots(grid, shape, candidates, count, lastRow);
            if (shape == FormationShape.Ring && keep.HasValue)
            {
                KeepSpotsHolding(grid, keep.Value, count, lastRow);
            }

            if (_spots.Count == 0)
            {
                return false;
            }

            _spots.Sort(SpotByCost.Instance);
            var choices = Mathf.Min(_spots.Count, Mathf.Max(1, settings.SpotChoices));

            // The pick among the few cheapest is there so a formation doesn't always stand in the same place. Where
            // a gift's spot leads, it would hand the cover to an item carrier as often as not, whatever the
            // preference: the pick stays among the gifts' spots.
            if (_giftPreference > 0f && _spots[0].Gift)
            {
                choices = LeadingGiftSpots(choices);
            }

            var chosen = _spots[Mathf.Clamp(pickIndex(choices), 0, choices - 1)];

            BuildSlots(grid, shape, chosen.Anchor, count, lastRow, chosen.Variant);
            Assign(candidates, count, assignmentsOut);
            if (shape == FormationShape.ItemCover)
            {
                CoveredHost = chosen.Anchor;
            }
            else if (shape is FormationShape.Ring or FormationShape.Spinner)
            {
                TurnCentre = chosen.Anchor;
            }

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
                    if (shape == FormationShape.Ring)
                    {
                        CollectRingSpots(grid, candidates, count, lastRow, anchor);
                    }
                    else if (shape == FormationShape.Spinner)
                    {
                        CollectSpinnerSpots(grid, candidates, count, lastRow, anchor);
                    }
                    else if (shape != FormationShape.ItemCover)
                    {
                        if (BuildSlots(grid, shape, anchor, count, lastRow, 0))
                        {
                            TryAddSpot(grid, candidates, count, anchor, 0);
                        }
                    }
                    else if (IsCoverHost(grid, anchor, out var gift))
                    {
                        CollectCoverSpots(grid, candidates, count, lastRow, anchor, gift);
                    }
                }
            }
        }

        // One spot for each of the six arcs round a centre: the arc's first slot is the centre's neighbour in the
        // variant's direction, and the rest follow it clockwise.
        private void CollectRingSpots(
            SlotGrid grid, IReadOnlyList<IWriteableFormationMember> candidates, int count, int lastRow,
            Vector2Int centre)
        {
            if (!FormationRing.IsCentre(grid, centre, lastRow))
            {
                return;
            }

            for (var variant = 0; variant < HexCoordinates.Directions; variant++)
            {
                if (BuildSlots(grid, FormationShape.Ring, centre, count, lastRow, variant))
                {
                    TryAddSpot(grid, candidates, count, centre, variant);
                }
            }
        }

        // One spot for a centre the spinner can turn all the way round: the cheapest of the three lines through
        // it. One for each line would fill the few cheapest spots with one centre's three, and the pick among
        // them would nearly always stand the spinner in the same place.
        private void CollectSpinnerSpots(
            SlotGrid grid, IReadOnlyList<IWriteableFormationMember> candidates, int count, int lastRow,
            Vector2Int centre)
        {
            if (!FormationSpinner.IsCentre(grid, centre, count / 2, lastRow, _lastFlatRow))
            {
                return;
            }

            var first = _spots.Count;
            for (var axis = 0; axis < FormationSpinner.Axes; axis++)
            {
                if (BuildSlots(grid, FormationShape.Spinner, centre, count, lastRow, axis))
                {
                    TryAddSpot(grid, candidates, count, centre, axis);
                }
            }

            for (var i = _spots.Count - 1; i > first; i--)
            {
                if (_spots[i].Cost < _spots[first].Cost)
                {
                    _spots[first] = _spots[i];
                }

                _spots.RemoveAt(i);
            }
        }

        // A snake coils into its ring from the head. While any arc has the head's slot at one of its ends, only
        // those spots stay: the head keeps its slot (a member on a formation slot always does) with the open slot
        // ahead of it, and the arc follows it round. Failing that, the arcs that hold the slot anywhere.
        private void KeepSpotsHolding(SlotGrid grid, Vector2Int keep, int count, int lastRow)
        {
            if (!KeepSpots(grid, keep, count, lastRow, atAnEnd: true))
            {
                KeepSpots(grid, keep, count, lastRow, atAnEnd: false);
            }
        }

        private bool KeepSpots(SlotGrid grid, Vector2Int keep, int count, int lastRow, bool atAnEnd)
        {
            var holding = 0;
            for (var i = 0; i < _spots.Count; i++)
            {
                BuildSlots(grid, FormationShape.Ring, _spots[i].Anchor, count, lastRow, _spots[i].Variant);
                if (atAnEnd ? _slots[0] == keep || _slots[^1] == keep : Holds(_slots, keep))
                {
                    _spots[holding++] = _spots[i];
                }
            }

            if (holding == 0)
            {
                return false;
            }

            _spots.RemoveRange(holding, _spots.Count - holding);
            return true;
        }

        private int LeadingGiftSpots(int among)
        {
            var gifts = 0;
            while (gifts < among && _spots[gifts].Gift)
            {
                gifts++;
            }

            return gifts;
        }

        // One spot per cover order, unless both orders come out the same (an even cover, or one an edge trims).
        private void CollectCoverSpots(
            SlotGrid grid, IReadOnlyList<IWriteableFormationMember> candidates, int count, int lastRow,
            Vector2Int host, bool gift)
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

                if (TryAddSpot(grid, candidates, count, host, variant) && gift)
                {
                    PreferLastSpot();
                }
            }
        }

        // The spot just added stands round a gift: it counts as cheaper by the preference for each member it takes
        // (an edge may have trimmed it), so a bigger cover is no easier to draw away to an item carrier.
        private void PreferLastSpot()
        {
            var last = _spots.Count - 1;
            var spot = _spots[last];
            spot.Gift = true;
            spot.Cost -= Mathf.RoundToInt(_giftPreference * _scratch.Count);
            _spots[last] = spot;
        }

        private bool TryAddSpot(
            SlotGrid grid, IReadOnlyList<IWriteableFormationMember> candidates, int count, Vector2Int anchor,
            int variant)
        {
            if (!AllSlotsOpen(grid, candidates, out var displaced))
            {
                return false;
            }

            var cost = Assign(candidates, count, _scratch) + displaced * DisplacedBalloonCost;
            if (ClosesWall(_scratch))
            {
                return false;
            }

            _spots.Add(new Spot
            {
                Anchor = anchor,
                Variant = variant,
                Cost = cost,
                Order = _spots.Count,
            });
            return true;
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

                case FormationShape.Ring:
                    for (var i = 0; i < count; i++)
                    {
                        _slots.Add(HexCoordinates.Step(anchor, variant + i));
                    }

                    break;

                case FormationShape.Spinner:
                    // The line through the anchor along the variant's axis: as many on one side as on the other.
                    for (var i = -(count / 2); i <= count / 2; i++)
                    {
                        _slots.Add(i < 0
                            ? FormationSpinner.Out(anchor, variant + FormationSpinner.Axes, -i)
                            : FormationSpinner.Out(anchor, variant, i));
                    }

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

        // A cover takes three to six, whatever MaxMembers says: six surround it. A horizontal line stops at
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

            // A ring is only a ring with most of its slots taken, and never closes: one slot at least stays open,
            // whatever the settings say.
            if (shape == FormationShape.Ring)
            {
                var fewest = Mathf.Max(settings.RingMinMembers, 2);
                var ringMost = Mathf.Min(settings.RingMaxMembers, FormationRing.MostMembers);
                return candidates >= fewest && fewest <= ringMost ? Mathf.Min(candidates, ringMost) : 0;
            }

            // A spinner is a line with one member in the middle and as many on each side: five, or three.
            if (shape == FormationShape.Spinner)
            {
                var longest = Mathf.Min(settings.SpinnerMaxMembers, FormationSpinner.MostMembers);
                if (candidates >= FormationSpinner.MostMembers && longest >= FormationSpinner.MostMembers)
                {
                    return FormationSpinner.MostMembers;
                }

                return candidates >= FormationSpinner.LeastMembers && longest >= FormationSpinner.LeastMembers
                    ? FormationSpinner.LeastMembers
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

        // displaced: the balloons on the slots, which trade places with the members.
        private bool AllSlotsOpen(SlotGrid grid, IReadOnlyList<IWriteableFormationMember> candidates, out int displaced)
        {
            displaced = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (grid.IsEmpty(slot.x, slot.y))
                {
                    continue;
                }

                var occupant = grid.At(slot);
                if (IsCandidate(occupant, candidates))
                {
                    continue;
                }

                if (!SnakeWalk.CanPassThrough(occupant))
                {
                    return false;
                }

                displaced++;
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

        // What a cover stands round. Of the items only those that hit hard are worth shielding: a bomb, a snipe
        // and a lightning. A gift is any reward balloon, the nuke too: each is something the player wants to reach.
        // Only a balloon the cover can hold in its slot, and one no other cover holds already: the members stand
        // right beside it, and that is a cover only while it stays between them.
        private static bool IsCoverHost(SlotGrid grid, Vector2Int slot, out bool gift)
        {
            gift = false;
            if (grid.IsEmpty(slot.x, slot.y))
            {
                return false;
            }

            var actor = grid.At(slot);
            if (actor is not IWriteableCoverHost { HeldByCover: false })
            {
                return false;
            }

            if (actor is IBalloonModel { TypeName: var type } && type.IsReward())
            {
                gift = true;
                return true;
            }

            return actor is IHasItemSlot host
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

            // A cover's spot round a gift balloon, its cost already less the preference.
            public bool Gift;
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
