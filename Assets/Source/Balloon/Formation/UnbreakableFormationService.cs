using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game.Assist;
using BalloonParty.Game.Run;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     Gathers the board's unbreakables into formations, as many as there are members for. At each wave
    ///     start the balancer asks it to arrange: each formation holds for
    ///     <see cref="IUnbreakableFormationSettings.HoldWaves" /> waves, then plans a different shape, and the
    ///     unbreakables left free form new ones. It hands the balancer the moves and anchors the members. A
    ///     formation that loses members below the minimum breaks at once. One that stands untouched may call
    ///     in another unbreakable (<see cref="IFormationReinforcements" />). The board's toughs are one snake of
    ///     their own (<see cref="IUnbreakableFormationSettings.ToughSnakes" />), ordered by armour: it starts from
    ///     the toughest with every landed tough lined up behind it at once, each tough that lands later falls in
    ///     behind its tail as soon as it lands, and it walks through the balloons in its way until its last member
    ///     pops.
    /// </summary>
    internal sealed class UnbreakableFormationService : IStartable, IRunResettable, IDisposable,
        IFormationReinforcements
    {
        private static readonly FormationShape[] Shapes =
        {
            FormationShape.Horizontal,
            FormationShape.DiagonalDownRight,
            FormationShape.DiagonalDownLeft,
            FormationShape.ItemCover,
            FormationShape.Snake,
        };

        // West to east, so a snake heading east reverses to lead with its eastmost member.
        private static readonly Comparison<IWriteableFormationMember> ByColumn = (a, b) =>
            a.SlotIndex.Value.x - b.SlotIndex.Value.x;

        private readonly SlotGrid _grid;
        private readonly IUnbreakableFormationSettings _settings;
        private readonly ThreatEase _threatEase;
        private readonly FormationPlanner _planner = new();
        private readonly ShotWall _wall = new();
        private readonly List<IWriteableFormationMember> _candidates = new();
        private readonly List<IWriteableFormationMember> _members = new();
        private readonly List<StandingFormation> _formations = new();
        private readonly List<StandingFormation> _spareFormations = new();
        private readonly Dictionary<IWriteableFormationMember, StandingFormation> _formationOf = new();
        private readonly List<FormationAssignment> _assignments = new();
        private readonly List<SlotMove> _moves = new();
        private readonly List<Vector2Int> _trail = new();
        private readonly List<Vector2Int> _line = new();
        private readonly List<Vector2Int> _leftSlots = new();
        private readonly List<IWriteableFormationMember> _freeToughs = new();
        private readonly List<IWriteableFormationMember> _gathered = new();
        private readonly List<FormationShape> _shapePool = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Func<int, int> _pickIndex;

        private bool _arranging;
        // Counted at the start of each shape plan, for the snake's weight in that plan's picks.
        private int _unbreakablesOnBoard;
        private int _reinforcementCalls;
        private int _snakePulses;

        // Set when free toughs had nowhere to fall in; cleared by the next change on the board.
        private bool _gatherBlocked;

        // Counts wave-start arrangements, so a step in the same balance can skip the formations arranged in it.
        private int _arrangePass;

        public int ResetOrder => RunResetOrder.Quiesce;

        // Exposed for tests.
        internal IReadOnlyList<IWriteableFormationMember> Members => _members;
        internal IReadOnlyList<StandingFormation> Formations => _formations;
        internal FormationShape? CurrentShape => _formations.Count > 0 ? _formations[0].Shape : null;
        internal Func<float> Random01 { get; set; } = () => Random.value;
        internal bool HasToughSnake => ToughSnake() != null;
        internal float ToughSnakeStepSeconds => _settings?.ToughSnakeStepSeconds ?? 0f;

        [Inject]
        internal UnbreakableFormationService(SlotGrid grid, IBalloonsConfiguration balloonsConfig, ThreatEase threatEase)
        {
            _grid = grid;
            _settings = balloonsConfig.UnbreakableFormation;
            _threatEase = threatEase;
            _pickIndex = PickIndex;
        }

        public void Start()
        {
            _grid.OnChanged.Subscribe(OnGridChanged).AddTo(_subscriptions);
        }

        public void ResetRun(int generation)
        {
            ReleaseAll();
            _reinforcementCalls = 0;
            _snakePulses = 0;
            _gatherBlocked = false;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        public int TakeReinforcementCalls()
        {
            var calls = _reinforcementCalls;
            _reinforcementCalls = 0;
            return calls;
        }

        /// <summary>
        ///     Runs once per wave start, before the roam pre-pass. Every formation that stood untouched through
        ///     the last wave rolls for a reinforcement. A formation still holding counts down; one done holding
        ///     plans its next shape, or breaks if none fits. Then the free unbreakables form new formations
        ///     while there are enough of them, and the toughs start their snake if none walks.
        ///     <paramref name="relocate" /> carries out a batch of moves at once and owns the grid change and the
        ///     glides.
        /// </summary>
        internal void ArrangeAtWaveStart(Action<IReadOnlyList<SlotMove>> relocate)
        {
            if (_settings == null || !_settings.Enabled)
            {
                ReleaseAll();
                return;
            }

            _arrangePass++;
            RollReinforcements();

            // The members' own moves remove and place them; that is not a member being lost.
            _arranging = true;
            try
            {
                ChangeFormationsDoneHolding(relocate);
                FormFromFreeUnbreakables(relocate);
                if (_settings.ToughSnakes)
                {
                    GatherToughs(relocate);
                }
            }
            finally
            {
                _arranging = false;
            }

            RebuildMemberList();
        }

        /// <summary>
        ///     True when the toughs can form up: none is still gliding in, and either
        ///     <see cref="IUnbreakableFormationSettings.MinMembers" /> toughs are on the board with no snake yet, or a
        ///     snake walks and a tough has landed free of it. The balancer asks every frame, so the toughs never wait
        ///     for a wave start or a beat. Read-only.
        /// </summary>
        internal bool ToughsDueToGather()
        {
            if (_settings == null || !_settings.Enabled || !_settings.ToughSnakes || IsAFreeToughLanding())
            {
                return false;
            }

            return ToughSnake() == null ? FindToughHead() != null : !_gatherBlocked && HasFreeLandedTough();
        }

        /// <summary>
        ///     Forms the toughs into one body: with no snake, the toughest landed tough starts one where it stands;
        ///     then every free tough that has landed falls in behind the tail at once, along a line of slots on from
        ///     it (<see cref="SnakeBody" />). A balloon on that line moves out to a slot a tough left.
        ///     <paramref name="relocate" /> carries out the moves as one batch.
        /// </summary>
        internal void GatherToughSnake(Action<IReadOnlyList<SlotMove>> relocate)
        {
            if (_settings == null || !_settings.Enabled || !_settings.ToughSnakes)
            {
                return;
            }

            _arranging = true;
            try
            {
                GatherToughs(relocate);
            }
            finally
            {
                _arranging = false;
            }
        }

        /// <summary>
        ///     Counts one snake beat; true when an unbreakable snake on the board is due its step
        ///     (<see cref="IUnbreakableFormationSettings.SnakeStepPulses" /> beats apart). With no snake the count
        ///     starts over, so a new snake takes its first step a full beat after it forms. The tough snake walks
        ///     on a clock of its own (<see cref="ToughSnakeStepSeconds" />).
        /// </summary>
        internal bool TakeSnakeBeat()
        {
            if (!HasSnake())
            {
                _snakePulses = 0;
                return false;
            }

            _snakePulses++;
            if (_snakePulses < _settings.SnakeStepPulses)
            {
                return false;
            }

            _snakePulses = 0;
            return true;
        }

        /// <summary>
        ///     Every unbreakable snake steps once: its head into the next open slot (<see cref="SnakeWalk" />), each member after
        ///     it into the slot the one ahead just left. A snake boxed in turns round and its tail leads. Like
        ///     <see cref="ArrangeAtWaveStart" />, <paramref name="relocate" /> carries out each step's moves at once.
        ///     <paramref name="afterArranging" />: the same balance just arranged at wave start, and a snake formed or
        ///     re-planned there waits for the next beat rather than stepping off its slots the moment it lands.
        /// </summary>
        internal void StepSnakes(Action<IReadOnlyList<SlotMove>> relocate, bool afterArranging = false)
        {
            var lastRow = LastSnakeRow();
            _arranging = true;
            try
            {
                for (var i = 0; i < _formations.Count; i++)
                {
                    var formation = _formations[i];
                    if (formation.Shape == FormationShape.Snake && !formation.Armoured
                        && formation.MemberList.Count > 0
                        && !(afterArranging && formation.ArrangedOnPass == _arrangePass))
                    {
                        Walk(formation, lastRow, relocate);
                    }
                }
            }
            finally
            {
                _arranging = false;
            }
        }

        /// <summary>
        ///     The tough snake steps once, as <see cref="StepSnakes" /> steps an unbreakable one, and the toughs
        ///     that landed meanwhile fall in behind its new tail. <paramref name="afterArranging" />: as for
        ///     <see cref="StepSnakes" />.
        /// </summary>
        internal void StepToughSnake(Action<IReadOnlyList<SlotMove>> relocate, bool afterArranging = false)
        {
            var snake = ToughSnake();
            if (snake == null)
            {
                return;
            }

            _arranging = true;
            try
            {
                if (snake.MemberList.Count > 0 && !(afterArranging && snake.ArrangedOnPass == _arrangePass))
                {
                    Walk(snake, LastSnakeRow(), relocate);
                }

                GatherToughs(relocate);
            }
            finally
            {
                _arranging = false;
            }
        }

        // A snake keeps a horizontal line's rows: never in the bottom HorizontalBottomClearance rows.
        private int LastSnakeRow()
        {
            return SnakeWalk.LastRow(_grid.Rows, _settings);
        }

        private bool HasSnake()
        {
            for (var i = 0; i < _formations.Count; i++)
            {
                if (_formations[i].Shape == FormationShape.Snake && !_formations[i].Armoured)
                {
                    return true;
                }
            }

            return false;
        }

        private void Walk(StandingFormation snake, int lastRow, Action<IReadOnlyList<SlotMove>> relocate)
        {
            var members = snake.MemberList;

            // A tough never walls, so moving one can't close a wall. The toughs walk through the board's balloons
            // rather than wait for an empty slot, which a full board seldom has.
            var armoured = snake.Armoured;
            var wall = armoured ? null : LoadWall();
            if (!SnakeWalk.TryStep(_grid, members[0].SlotIndex.Value, members[^1].SlotIndex.Value, snake.Heading,
                    lastRow, wall, out var direction, armoured))
            {
                members.Reverse();
                var back = members.Count > 1
                    ? HexCoordinates.DirectionBetween(members[1].SlotIndex.Value, members[0].SlotIndex.Value)
                    : -1;
                snake.Heading = back >= 0 ? back : HexCoordinates.Turn(snake.Heading, 3);
                if (!SnakeWalk.TryStep(_grid, members[0].SlotIndex.Value, members[^1].SlotIndex.Value, snake.Heading,
                        lastRow, wall, out direction, armoured))
                {
                    // Boxed in at both ends: it stays turned round, and next beat tries the other end again.
                    return;
                }
            }

            snake.Heading = direction;
            var heading = DirectionHeading(direction);

            // The trail: the slot ahead, then each member's slot head to tail. Even across a gap a popped member
            // left, the member behind it takes the slot the one ahead of it just left.
            _trail.Clear();
            _trail.Add(HexCoordinates.Step(members[0].SlotIndex.Value, direction));
            for (var i = 0; i < members.Count - 1; i++)
            {
                _trail.Add(members[i].SlotIndex.Value);
            }

            var tailSlot = members[^1].SlotIndex.Value;

            // A tough snake moves one place per step toward armour order: a tough hit since the last step drops
            // back past one with more armour left, and one that joined at the tail works its way forward.
            if (armoured)
            {
                ArmourOrder.StepTowardOrder(members);
            }

            // One passed in the re-order holds its slot this beat, so it has no move.
            _moves.Clear();
            for (var i = 0; i < members.Count; i++)
            {
                if (members[i].SlotIndex.Value != _trail[i])
                {
                    _moves.Add(new SlotMove(members[i], _trail[i]));
                }
            }

            // The balloon the head walks into goes round the snake to the slot its tail leaves.
            if (!_grid.IsEmpty(_trail[0].x, _trail[0].y))
            {
                _moves.Add(new SlotMove((IWriteableDynamicSlotActor)_grid.At(_trail[0]), tailSlot));
            }

            for (var i = 0; i < members.Count; i++)
            {
                members[i].SetWalking(true);
                members[i].SetFormationHeading(heading);
            }

            relocate(_moves);
        }

        /// <summary>
        ///     Drops from <paramref name="slots" /> every slot an unbreakable roaming from <paramref name="from" />
        ///     would link the board's unbreakables side to side by landing on (<see cref="ShotWall" />).
        /// </summary>
        internal void DropWallClosingSlots(Vector2Int from, List<Vector2Int> slots)
        {
            var wall = LoadWall();
            if (wall == null)
            {
                return;
            }

            wall.Set(from, false);
            for (var i = slots.Count - 1; i >= 0; i--)
            {
                var slot = slots[i];
                wall.Set(slot, true);
                var closes = wall.Spans();
                wall.Set(slot, false);
                if (closes)
                {
                    slots.RemoveAt(i);
                }
            }
        }

        // The board's wall, loaded; null when it is walled already, by a path nothing here controls, so a move
        // that keeps it is no worse than standing still.
        private ShotWall LoadWall()
        {
            _wall.Load(_grid);
            return _wall.Spans() ? null : _wall;
        }

        private void RollReinforcements()
        {
            // Eased in need, so a player down to the last heart faces fewer calls.
            var chance = _threatEase.Eased(_settings.ReinforcementChance);
            if (chance <= 0f)
            {
                return;
            }

            // Only unbreakables call unbreakables in; a tough snake calls no one.
            for (var i = 0; i < _formations.Count; i++)
            {
                if (!_formations[i].Armoured && !_formations[i].Touched && Random01() < chance)
                {
                    _reinforcementCalls++;
                }
            }
        }

        private void ChangeFormationsDoneHolding(Action<IReadOnlyList<SlotMove>> relocate)
        {
            for (var i = 0; i < _formations.Count; i++)
            {
                var formation = _formations[i];

                // A tough snake has no other shape to change to: it walks until it breaks.
                if (formation.Armoured)
                {
                    continue;
                }

                if (formation.WavesLeft > 0)
                {
                    formation.WavesLeft--;
                    continue;
                }

                CollectCandidates(formation, armoured: false);
                if (_candidates.Count < _settings.MinMembers || !TryPlanNextShape(formation.Shape, out var shape))
                {
                    // The balancer runs the race right after this, so the released members settle there.
                    Release(formation);
                    _formations.RemoveAt(i);
                    i--;
                    continue;
                }

                Apply(formation, shape, relocate);
            }
        }

        // Each pass takes at least MinMembers free unbreakables, so the loop ends.
        private void FormFromFreeUnbreakables(Action<IReadOnlyList<SlotMove>> relocate)
        {
            while (true)
            {
                CollectCandidates(null, armoured: false);
                if (_candidates.Count < _settings.MinMembers || !TryPlanNextShape(null, out var shape))
                {
                    return;
                }

                var formation = TakeSpareFormation();
                _formations.Add(formation);
                Apply(formation, shape, relocate);
            }
        }

        // The board's toughs are one body. They form up once none is still gliding in, so toughs that rise together
        // land as one body and join it as they stand: with no snake and MinMembers toughs on the board, the
        // toughest within the rows a snake walks starts it where it stands, and every free tough falls in behind
        // the tail at once.
        private void GatherToughs(Action<IReadOnlyList<SlotMove>> relocate)
        {
            if (IsAFreeToughLanding())
            {
                return;
            }

            var snake = ToughSnake() ?? StartToughSnake();
            if (snake == null)
            {
                return;
            }

            _freeToughs.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!_grid.IsEmpty(col, row) && SnakeBody.IsFreeLandedTough(_grid.At(new Vector2Int(col, row))))
                    {
                        _freeToughs.Add((IWriteableFormationMember)_grid.At(new Vector2Int(col, row)));
                    }
                }
            }

            if (_freeToughs.Count == 0)
            {
                return;
            }

            var members = snake.MemberList;
            var tail = members[^1].SlotIndex.Value;
            var away = members.Count > 1 ? HexCoordinates.DirectionBetween(members[^2].SlotIndex.Value, tail) : -1;
            if (away < 0)
            {
                away = HexCoordinates.Turn(snake.Heading, 3);
            }

            SnakeBody.Extend(_grid, tail, away, LastSnakeRow(), _freeToughs.Count, _line);
            _gatherBlocked = _line.Count < _freeToughs.Count;
            if (_line.Count == 0)
            {
                return;
            }

            PickGathered(tail);
            _moves.Clear();
            _leftSlots.Clear();
            for (var i = 0; i < _gathered.Count; i++)
            {
                var from = _gathered[i].SlotIndex.Value;
                if (!_line.Contains(from))
                {
                    _leftSlots.Add(from);
                }

                if (from != _line[i])
                {
                    _moves.Add(new SlotMove(_gathered[i], _line[i]));
                }
            }

            // The balloons on the line move out to the slots the toughs left; there are at least as many of those.
            var left = 0;
            for (var i = 0; i < _line.Count; i++)
            {
                var slot = _line[i];
                if (!_grid.IsEmpty(slot.x, slot.y) && !SnakeBody.IsFreeLandedTough(_grid.At(slot)))
                {
                    _moves.Add(new SlotMove((IWriteableDynamicSlotActor)_grid.At(slot), _leftSlots[left++]));
                }
            }

            var walking = members[0].Walking;
            var heading = DirectionHeading(snake.Heading);
            for (var i = 0; i < _gathered.Count; i++)
            {
                var tough = _gathered[i];
                tough.SetInFormation(true);
                tough.SetWalking(walking);
                tough.SetFormationHeading(heading);
                members.Add(tough);
                _formationOf[tough] = snake;
            }

            RebuildMemberList();
            relocate(_moves);
        }

        // Who falls in, in line order: the free toughs on the line, then the nearest of the rest to the tail, as
        // many as the line has slots, with the most armour nearest the body.
        private void PickGathered(Vector2Int tail)
        {
            _gathered.Clear();
            for (var i = 0; i < _line.Count; i++)
            {
                var slot = _line[i];
                if (!_grid.IsEmpty(slot.x, slot.y) && SnakeBody.IsFreeLandedTough(_grid.At(slot)))
                {
                    _gathered.Add((IWriteableFormationMember)_grid.At(slot));
                }
            }

            while (_gathered.Count < _line.Count)
            {
                IWriteableFormationMember nearest = null;
                var best = int.MaxValue;
                for (var i = 0; i < _freeToughs.Count; i++)
                {
                    var tough = _freeToughs[i];
                    var distance = HexCoordinates.Distance(tail, tough.SlotIndex.Value);
                    if (distance < best && !_gathered.Contains(tough))
                    {
                        best = distance;
                        nearest = tough;
                    }
                }

                _gathered.Add(nearest);
            }

            // An insertion sort keeps equal armour in the order picked.
            for (var i = 1; i < _gathered.Count; i++)
            {
                var tough = _gathered[i];
                var j = i - 1;
                while (j >= 0 && ArmourOrder.Of(_gathered[j]) < ArmourOrder.Of(tough))
                {
                    _gathered[j + 1] = _gathered[j];
                    j--;
                }

                _gathered[j + 1] = tough;
            }
        }

        private StandingFormation StartToughSnake()
        {
            var head = FindToughHead();
            if (head == null)
            {
                return null;
            }

            var snake = TakeSpareFormation();
            snake.Armoured = true;
            snake.Shape = FormationShape.Snake;
            snake.ArrangedOnPass = _arrangePass;
            snake.Heading = StartHeading(head.SlotIndex.Value);
            snake.MemberList.Add(head);
            _formationOf[head] = snake;
            head.SetInFormation(true);
            head.SetWalking(false);
            head.SetFormationHeading(DirectionHeading(snake.Heading));
            _formations.Add(snake);
            RebuildMemberList();
            return snake;
        }

        // The body forms on the side away from the heading: toward a free tough beside the head, so toughs that
        // landed side by side are the body as they stand, else toward the farther edge, where it has room.
        private int StartHeading(Vector2Int head)
        {
            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var slot = HexCoordinates.Step(head, direction);
                if (_grid.InBounds(slot) && !_grid.IsEmpty(slot.x, slot.y)
                    && SnakeBody.IsFreeLandedTough(_grid.At(slot)))
                {
                    return HexCoordinates.Turn(direction, 3);
                }
            }

            var roomWest = head.x;
            var roomEast = _grid.Columns - 1 - head.x;
            var east = roomWest != roomEast ? roomWest > roomEast : Random01() < 0.5f;
            return east ? 0 : 3;
        }

        // The tough to start a snake, or null: the toughest landed one within the rows a snake walks, once
        // MinMembers toughs are on the board.
        private IWriteableFormationMember FindToughHead()
        {
            var lastRow = LastSnakeRow();
            IWriteableFormationMember head = null;
            var toughs = 0;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row) || _grid.At(new Vector2Int(col, row)) is not IArmouredFormationMember tough)
                    {
                        continue;
                    }

                    toughs++;
                    if (tough.IsStable.Value && row <= lastRow && (head == null || LeadsBetter(tough, head)))
                    {
                        head = tough;
                    }
                }
            }

            return toughs >= _settings.MinMembers ? head : null;
        }

        private bool IsAFreeToughLanding()
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!_grid.IsEmpty(col, row) && _grid.At(new Vector2Int(col, row)) is var actor
                        && SnakeBody.IsFreeTough(actor) && !SnakeBody.IsFreeLandedTough(actor))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasFreeLandedTough()
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!_grid.IsEmpty(col, row) && SnakeBody.IsFreeLandedTough(_grid.At(new Vector2Int(col, row))))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // More armour, then the deeper row, then farther west: the west end of the initial fill's tough run, which
        // bends onto the line above at its east end.
        private static bool LeadsBetter(IWriteableFormationMember tough, IWriteableFormationMember than)
        {
            var armour = ArmourOrder.Of(tough);
            var thanArmour = ArmourOrder.Of(than);
            if (armour != thanArmour)
            {
                return armour > thanArmour;
            }

            var slot = tough.SlotIndex.Value;
            var thanSlot = than.SlotIndex.Value;
            return slot.y != thanSlot.y ? slot.y > thanSlot.y : slot.x < thanSlot.x;
        }

        private StandingFormation ToughSnake()
        {
            for (var i = 0; i < _formations.Count; i++)
            {
                if (_formations[i].Armoured)
                {
                    return _formations[i];
                }
            }

            return null;
        }

        private void Apply(
            StandingFormation formation, FormationShape shape, Action<IReadOnlyList<SlotMove>> relocate)
        {
            var heading = HeadingFor(shape);

            // Only members the new shape drops go back to red; a member that stays never dips out of it.
            var members = formation.MemberList;
            for (var i = 0; i < members.Count; i++)
            {
                if (!IsAssigned(members[i]))
                {
                    members[i].SetInFormation(false);
                    _formationOf.Remove(members[i]);
                }
            }

            members.Clear();
            for (var i = 0; i < _assignments.Count; i++)
            {
                var assignment = _assignments[i];
                var member = assignment.Member;
                member.SetFormationHeading(heading);
                member.SetInFormation(true);

                // A snake sets it again at its first step; any other shape stands, so a glide into it reads red.
                member.SetWalking(false);
                members.Add(member);
                _formationOf[member] = formation;
            }

            _moves.Clear();
            for (var i = 0; i < _assignments.Count; i++)
            {
                if (_assignments[i].Member.SlotIndex.Value != _assignments[i].Target)
                {
                    _moves.Add(new SlotMove(_assignments[i].Member, _assignments[i].Target));
                }
            }

            relocate(_moves);
            formation.Shape = shape;
            formation.WavesLeft = _settings.HoldWaves - 1;
            formation.ArrangedOnPass = _arrangePass;
            if (shape == FormationShape.Snake)
            {
                LineUpSnake(formation);
            }
        }

        // A snake forms as a row and sets off along it, east or west at random, led by the member at that end.
        private void LineUpSnake(StandingFormation snake)
        {
            var members = snake.MemberList;
            members.Sort(ByColumn);
            var east = Random01() < 0.5f;
            if (east)
            {
                members.Reverse();
            }

            snake.Heading = east ? 0 : 3;
            var heading = DirectionHeading(snake.Heading);
            for (var i = 0; i < members.Count; i++)
            {
                members[i].SetFormationHeading(heading);
            }
        }

        // The members a formation may plan with: its own and every free one of its kind (toughs when armoured,
        // else unbreakables). Members of other formations are left out, so the planner treats their slots as
        // taken. Null: free ones only.
        private void CollectCandidates(StandingFormation own, bool armoured)
        {
            _candidates.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row) || _grid.At(new Vector2Int(col, row)) is not IWriteableFormationMember member)
                    {
                        continue;
                    }

                    if ((member is IArmouredFormationMember) != armoured)
                    {
                        continue;
                    }

                    if (!_formationOf.TryGetValue(member, out var formation) || formation == own)
                    {
                        _candidates.Add(member);
                    }
                }
            }
        }

        // A weighted pick without the shape just held; a shape with no spot drops out and the pick goes
        // again. The held shape is the last resort, when nothing else fits.
        private bool TryPlanNextShape(FormationShape? lastShape, out FormationShape shape)
        {
            _unbreakablesOnBoard = CountUnbreakables();
            _shapePool.Clear();
            foreach (var candidate in Shapes)
            {
                if (candidate != lastShape && WeightOf(candidate) > 0f)
                {
                    _shapePool.Add(candidate);
                }
            }

            while (_shapePool.Count > 0)
            {
                var index = PickWeighted();
                shape = _shapePool[index];
                _shapePool.RemoveAt(index);
                if (_planner.TryPlan(_grid, shape, _candidates, _settings, _pickIndex, _assignments))
                {
                    return true;
                }
            }

            if (lastShape.HasValue && WeightOf(lastShape.Value) > 0f
                && _planner.TryPlan(_grid, lastShape.Value, _candidates, _settings, _pickIndex, _assignments))
            {
                shape = lastShape.Value;
                return true;
            }

            shape = default;
            return false;
        }

        private int PickWeighted()
        {
            var total = 0f;
            for (var i = 0; i < _shapePool.Count; i++)
            {
                total += WeightOf(_shapePool[i]);
            }

            var roll = Random01() * total;
            for (var i = 0; i < _shapePool.Count; i++)
            {
                roll -= WeightOf(_shapePool[i]);
                if (roll < 0f)
                {
                    return i;
                }
            }

            return _shapePool.Count - 1;
        }

        private float WeightOf(FormationShape shape)
        {
            return shape switch
            {
                FormationShape.Horizontal => _settings.HorizontalWeight,
                FormationShape.DiagonalDownRight => _settings.DiagonalWeight,
                FormationShape.DiagonalDownLeft => _settings.DiagonalWeight,
                FormationShape.ItemCover => _settings.ItemCoverWeight,
                FormationShape.Snake => _settings.SnakeWeight * (1f + _settings.SnakeWeightPerExtraUnbreakable
                    * Mathf.Max(0, _unbreakablesOnBoard - _settings.MinMembers)),
                _ => 0f,
            };
        }

        // Every unbreakable on the board, in a formation or not: the snake's weight grows with them. Toughs don't
        // count.
        private int CountUnbreakables()
        {
            var count = 0;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!_grid.IsEmpty(col, row)
                        && _grid.At(new Vector2Int(col, row)) is IWriteableFormationMember and not IArmouredFormationMember)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        // A line runs along its step between neighbouring slots, measured in world space so the diagonals
        // follow the board's real slot spacing. An item cover points up at the item it shields.
        private Vector2 HeadingFor(FormationShape shape)
        {
            Vector2Int to;
            switch (shape)
            {
                case FormationShape.Horizontal:
                    to = new Vector2Int(1, 0);
                    break;
                case FormationShape.DiagonalDownRight:
                    to = HexCoordinates.DiagonalDown(Vector2Int.zero, true);
                    break;
                case FormationShape.DiagonalDownLeft:
                    to = HexCoordinates.DiagonalDown(Vector2Int.zero, false);
                    break;
                default:
                    return Vector2.up;
            }

            Vector2 step = _grid.IndexToWorldPosition(to) - _grid.IndexToWorldPosition(Vector2Int.zero);
            return step.sqrMagnitude > 0f ? step.normalized : Vector2.right;
        }

        // A step in direction, in world space, so the glitter drifts the way the snake walks.
        private Vector2 DirectionHeading(int direction)
        {
            // The grid's spacing is the same everywhere, so any slot measures it; this one has all six neighbours
            // on a board of 3 by 3 or more, though the world maths doesn't need them on the board.
            var from = new Vector2Int(2, 2);
            Vector2 step = _grid.IndexToWorldPosition(HexCoordinates.Step(from, direction)) - _grid.IndexToWorldPosition(from);
            return step.sqrMagnitude > 0f ? step.normalized : Vector2.right;
        }

        private int PickIndex(int count)
        {
            return Mathf.Min((int)(Random01() * count), count - 1);
        }

        // A member popped (or otherwise taken off the board) leaves its formation and marks it touched; below
        // the minimum the rest of that formation are released at once. Other formations stand. The next
        // line's balance, or the timed pulse, floats the released ones up.
        private void OnGridChanged(SlotGridChangedEvent change)
        {
            if (_arranging)
            {
                return;
            }

            // Something moved, so a tough that had nowhere to fall in may have somewhere now.
            _gatherBlocked = false;
            if (change.ChangeType != SlotGridChangeType.Removed || _formations.Count == 0)
            {
                return;
            }

            var lost = false;
            for (var f = _formations.Count - 1; f >= 0; f--)
            {
                var formation = _formations[f];
                var members = formation.MemberList;
                for (var i = members.Count - 1; i >= 0; i--)
                {
                    var member = members[i];
                    var slot = member.SlotIndex.Value;
                    if (!_grid.InBounds(slot) || _grid.At(slot) != member)
                    {
                        member.SetInFormation(false);
                        _formationOf.Remove(member);
                        members.RemoveAt(i);
                        formation.Touched = true;
                        lost = true;
                    }
                }

                // The tough snake is the toughs' one body: it walks on with whoever is left.
                if (members.Count < (formation.Armoured ? 1 : _settings.MinMembers))
                {
                    Release(formation);
                    _formations.RemoveAt(f);
                }
            }

            if (lost)
            {
                RebuildMemberList();
            }
        }

        private bool IsAssigned(IWriteableFormationMember member)
        {
            for (var i = 0; i < _assignments.Count; i++)
            {
                if (_assignments[i].Member == member)
                {
                    return true;
                }
            }

            return false;
        }

        private StandingFormation TakeSpareFormation()
        {
            if (_spareFormations.Count == 0)
            {
                return new StandingFormation();
            }

            var last = _spareFormations.Count - 1;
            var formation = _spareFormations[last];
            _spareFormations.RemoveAt(last);
            return formation;
        }

        // Frees the formation's members and keeps the formation object for the next one; the caller takes it
        // out of the list.
        private void Release(StandingFormation formation)
        {
            var members = formation.MemberList;
            for (var i = 0; i < members.Count; i++)
            {
                members[i].SetInFormation(false);
                _formationOf.Remove(members[i]);
            }

            formation.Reset();
            _spareFormations.Add(formation);
        }

        private void ReleaseAll()
        {
            if (_formations.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _formations.Count; i++)
            {
                Release(_formations[i]);
            }

            _formations.Clear();
            RebuildMemberList();
        }

        private void RebuildMemberList()
        {
            _members.Clear();
            for (var f = 0; f < _formations.Count; f++)
            {
                _members.AddRange(_formations[f].MemberList);
            }
        }
    }
}
