using System.Collections.Generic;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Bomb;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Solver
{
    /// <summary>One bounce off a mushroom in a simulated flight, as <see cref="ShotBumperLayer.BounceLog" /> keeps
    /// it: what <c>BumperBounce</c> is to the live flight.</summary>
    internal readonly struct ShotBumperBounce
    {
        /// <summary>The bumper's id: which cap of the level was hit.</summary>
        public readonly int BumperId;

        /// <summary>The ball's centre at the touch: the exact point its path entered the bumper's circle.</summary>
        public readonly Vector2 Point;

        /// <summary>The ball's unit direction coming in, and the one it left along.</summary>
        public readonly Vector2 Incoming;

        public readonly Vector2 Outgoing;

        /// <summary>The flight's chain count after this bounce, and what the chain rule answered for it.</summary>
        public readonly int Count;

        public readonly MushroomReward Reward;

        /// <summary>Whether the ball is a lance as it leaves: armed here, or one already.</summary>
        public readonly bool LeavesPiercing;

        public ShotBumperBounce(
            in BumperContact contact, Vector2 outgoing, int count, MushroomReward reward, bool leavesPiercing)
        {
            BumperId = contact.Bumper.Id;
            Point = contact.Point;
            Incoming = contact.Incoming;
            Outgoing = outgoing;
            Count = count;
            Reward = reward;
            LeavesPiercing = leavesPiercing;
        }
    }

    /// <summary>
    ///     The meadow's mushrooms for the shot solver (@ref plan_meadow_mushrooms): the bumper circles as they
    ///     stood when the board was gathered, and the same bounce rule, chain rules and reward the live flight
    ///     has. A peer of <see cref="ShotBoardDynamics" /> and <see cref="ShotItemLayer" />:
    ///     <see cref="ShotSimulator.Simulate" /> takes it nullable, and with none the flight is the one it was
    ///     before the meadow had mushrooms.
    /// </summary>
    /// <remarks>
    ///     It holds no geometry of its own. <see cref="ShotSimulator" /> sweeps the circles with
    ///     <see cref="BumperSweep" />, counts with <see cref="MushroomChainRule" /> and turns the ball with
    ///     <see cref="Rule" />, in the order <c>ProjectileMotionResolver</c> does, so the two cannot bounce a
    ///     ball differently. The circles are copied at gather: the field's own list is refilled when the board
    ///     changes, and a sweep of thousands of flights has to see one board.
    ///     <para>
    ///         <b>Cut caps.</b> The field lists whole caps only, so a cap a lance cut before the gather is not
    ///         here, as it is not there for the game. A simulated lance cuts caps too (<see cref="Cut" />): they
    ///         leave <see cref="Circles" /> for the rest of that flight, and the next flight starts from the board
    ///         as gathered. A bomb the simulated flight sets off blows caps off the same way
    ///         (<see cref="BlowOff" />). Nothing grows back inside a simulated flight: the game's caps return on
    ///         the board's beat, seconds later, which a flight does not model.
    ///     </para>
    /// </remarks>
    internal sealed class ShotBumperLayer
    {
        private readonly List<BumperCircle> _gathered;
        private readonly List<BumperCircle> _circles;

        // The slot each cap stands in, by its place in _gathered and in _circles: a bomb takes caps off by slot.
        // Null when the layer was built without them, and then no bomb takes any.
        private readonly List<Vector2Int> _gatheredSlots;
        private readonly List<Vector2Int> _slots;

        private bool _cutThisFlight;

        /// <summary>
        ///     Every whole cap, in the order of their ids: the board as gathered, less what the flight being
        ///     simulated has cut. A board without whole caps has no layer at all (<see cref="Gather" />), so this
        ///     is empty only once a flight has cut every one.
        /// </summary>
        internal IReadOnlyList<BumperCircle> Circles => _circles;

        /// <summary>Which way the ball leaves a mushroom: the game's own rule.</summary>
        internal IBumperBounceRule Rule { get; }

        /// <summary>The chain's numbers, as <see cref="MushroomChainRule" /> takes them.</summary>
        internal MushroomChainRules Rules { get; }

        /// <summary>
        ///     The speed buff of the lance a chain pays: the Snipe's (<c>Snipe.SpeedBuffMultiplier</c>), which
        ///     <c>ISnipeArmer.ArmAt</c> arms with. 0 for none.
        /// </summary>
        internal float LanceSpeedBuffMultiplier { get; }

        /// <summary>
        ///     When set, every bounce of the flight being simulated, in order, cleared as a flight starts. For
        ///     tests and for drawing; a sweep leaves it null and pays nothing for it.
        /// </summary>
        internal List<ShotBumperBounce> BounceLog { get; set; }

        /// <summary>When set, the id of every cap the flight being simulated cut, in order, cleared as a flight
        /// starts. For tests; a sweep leaves it null.</summary>
        internal List<int> CutLog { get; set; }

        /// <summary>When set, the id of every cap a bomb of the flight being simulated blew off, in order,
        /// cleared as a flight starts. For tests.</summary>
        internal List<int> BlownLog { get; set; }

        /// <param name="lanceSpeedBuffMultiplier">The Snipe's speed buff (<c>Snipe.SpeedBuffMultiplier</c>), which
        /// a lance earned from mushrooms flies with; 0 or less for none.</param>
        /// <param name="slots">The grid slot of each of <paramref name="bumpers" />, in their order
        /// (<c>IBumperField.SlotAt</c>): what a simulated bomb takes caps off by. Without them a bomb takes
        /// none.</param>
        internal ShotBumperLayer(
            IReadOnlyList<BumperCircle> bumpers, IBumperBounceRule rule, in MushroomChainRules rules,
            float lanceSpeedBuffMultiplier, IReadOnlyList<Vector2Int> slots = null)
        {
            _gathered = new List<BumperCircle>(bumpers.Count);
            _circles = new List<BumperCircle>(bumpers.Count);
            for (var i = 0; i < bumpers.Count; i++)
            {
                _gathered.Add(bumpers[i]);
                _circles.Add(bumpers[i]);
            }

            if (slots != null && slots.Count == bumpers.Count)
            {
                _gatheredSlots = new List<Vector2Int>(slots);
                _slots = new List<Vector2Int>(slots);
            }

            Rule = rule;
            Rules = rules;
            LanceSpeedBuffMultiplier = Mathf.Max(0f, lanceSpeedBuffMultiplier);
        }

        /// <summary>
        ///     The layer for the live board, or null when it has no mushrooms (every level but a meadow's), so
        ///     the simulator takes the path it always had. The lance's speed is read where the game's
        ///     <c>SnipeItemHandler</c> reads it.
        /// </summary>
        internal static ShotBumperLayer Gather(
            IBumperField field, IBumperBounceRule rule, IMushroomSettings settings, IItemConfiguration items)
        {
            if (field == null || rule == null || settings == null)
            {
                return null;
            }

            var bumpers = field.Bumpers;
            if (bumpers.Count == 0)
            {
                return null;
            }

            var slots = new List<Vector2Int>(bumpers.Count);
            for (var i = 0; i < bumpers.Count; i++)
            {
                slots.Add(field.SlotAt(i));
            }

            var snipe = items?[ItemType.Snipe];
            return new ShotBumperLayer(
                bumpers, rule, settings.Rules, snipe != null ? snipe.Snipe.SpeedBuffMultiplier : 0f, slots);
        }

        internal void ResetForNewFlight()
        {
            BounceLog?.Clear();
            CutLog?.Clear();
            BlownLog?.Clear();
            if (!_cutThisFlight)
            {
                return;
            }

            _cutThisFlight = false;
            _circles.Clear();
            _circles.AddRange(_gathered);
            if (_slots != null)
            {
                _slots.Clear();
                _slots.AddRange(_gatheredSlots);
            }
        }

        /// <summary>
        ///     The simulated lance cut the cap at <paramref name="index" /> of <see cref="Circles" />: it is gone
        ///     from them for the rest of the flight, as <c>IBumperField.Cut</c> takes it from the game's. False,
        ///     with nothing changed, for an index that is no cap: the caller must then not count a cut, nor look
        ///     for that cap again.
        /// </summary>
        internal bool Cut(int index)
        {
            if (index < 0 || index >= _circles.Count)
            {
                return false;
            }

            CutLog?.Add(_circles[index].Id);
            Remove(index);
            return true;
        }

        /// <summary>
        ///     A bomb went off in the flight being simulated, the one that stood in <paramref name="bombSlot" />:
        ///     the caps the game's <c>MushroomBlast.Bomb</c> would blow off (<see cref="BombBlast.Pierces" />, the
        ///     bomb's own rule, asked the same way) are gone from <see cref="Circles" /> for the rest of the
        ///     flight. Answers how many. The sway moves no ball and is not modelled.
        /// </summary>
        internal int BlowOff(Vector2 centre, Vector2Int bombSlot, bool isRainbow, in BombBlastParams blast)
        {
            if (_slots == null)
            {
                return 0;
            }

            var blown = 0;
            for (var i = _circles.Count - 1; i >= 0; i--)
            {
                if (!BombBlast.Pierces(centre, bombSlot, isRainbow, in blast, _slots[i], _circles[i].Center, 0f))
                {
                    continue;
                }

                BlownLog?.Add(_circles[i].Id);
                Remove(i);
                blown++;
            }

            return blown;
        }

        private void Remove(int index)
        {
            _circles.RemoveAt(index);
            _slots?.RemoveAt(index);
            _cutThisFlight = true;
        }
    }
}
