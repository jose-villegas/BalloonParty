using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
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
    ///     in another unbreakable (<see cref="IFormationReinforcements" />).
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
        private readonly FormationPlanner _planner = new();
        private readonly List<IWriteableFormationMember> _candidates = new();
        private readonly List<IWriteableFormationMember> _members = new();
        private readonly List<StandingFormation> _formations = new();
        private readonly List<StandingFormation> _spareFormations = new();
        private readonly Dictionary<IWriteableFormationMember, StandingFormation> _formationOf = new();
        private readonly List<FormationAssignment> _assignments = new();
        private readonly List<FormationShape> _shapePool = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Func<int, int> _pickIndex;

        private bool _arranging;
        private int _reinforcementCalls;
        private int _snakePulses;

        public int ResetOrder => RunResetOrder.Quiesce;

        // Exposed for tests.
        internal IReadOnlyList<IWriteableFormationMember> Members => _members;
        internal IReadOnlyList<StandingFormation> Formations => _formations;
        internal FormationShape? CurrentShape => _formations.Count > 0 ? _formations[0].Shape : null;
        internal Func<float> Random01 { get; set; } = () => Random.value;

        [Inject]
        internal UnbreakableFormationService(SlotGrid grid, IBalloonsConfiguration balloonsConfig)
        {
            _grid = grid;
            _settings = balloonsConfig.UnbreakableFormation;
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
        ///     while there are enough of them. <paramref name="relocate" /> moves each member and owns the grid
        ///     change and the glide.
        /// </summary>
        internal void ArrangeAtWaveStart(Action<IWriteableDynamicSlotActor, Vector2Int> relocate)
        {
            if (_settings == null || !_settings.Enabled)
            {
                ReleaseAll();
                return;
            }

            RollReinforcements();

            // The members' own moves remove and place them; that is not a member being lost.
            _arranging = true;
            try
            {
                ChangeFormationsDoneHolding(relocate);
                FormFromFreeUnbreakables(relocate);
            }
            finally
            {
                _arranging = false;
            }

            RebuildMemberList();
        }

        /// <summary>
        ///     Counts one balancer pulse; true when a snake on the board is due its step
        ///     (<see cref="IUnbreakableFormationSettings.SnakeStepPulses" /> pulses apart). With no snake the count
        ///     starts over, so a new snake's first step comes a full beat after it forms.
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
        ///     Every snake steps once: its head into the next open slot (<see cref="SnakeWalk" />), each member after
        ///     it into the slot the one ahead just left. A snake boxed in turns round and its tail leads. Like
        ///     <see cref="ArrangeAtWaveStart" />, <paramref name="relocate" /> moves each member.
        /// </summary>
        internal void StepSnakes(Action<IWriteableDynamicSlotActor, Vector2Int> relocate)
        {
            var lastRow = _grid.Rows - 1 - Mathf.Max(_settings.HorizontalBottomClearance, _settings.BottomClearance);
            _arranging = true;
            try
            {
                for (var i = 0; i < _formations.Count; i++)
                {
                    if (_formations[i].Shape == FormationShape.Snake && _formations[i].MemberList.Count > 0)
                    {
                        Walk(_formations[i], lastRow, relocate);
                    }
                }
            }
            finally
            {
                _arranging = false;
            }
        }

        private bool HasSnake()
        {
            for (var i = 0; i < _formations.Count; i++)
            {
                if (_formations[i].Shape == FormationShape.Snake)
                {
                    return true;
                }
            }

            return false;
        }

        private void Walk(StandingFormation snake, int lastRow, Action<IWriteableDynamicSlotActor, Vector2Int> relocate)
        {
            var members = snake.MemberList;
            if (!SnakeWalk.TryStep(_grid, members[0].SlotIndex.Value, snake.Heading, lastRow, out var direction))
            {
                members.Reverse();
                var back = members.Count > 1
                    ? HexCoordinates.DirectionBetween(members[1].SlotIndex.Value, members[0].SlotIndex.Value)
                    : -1;
                snake.Heading = back >= 0 ? back : HexCoordinates.Turn(snake.Heading, 3);
                if (!SnakeWalk.TryStep(_grid, members[0].SlotIndex.Value, snake.Heading, lastRow, out direction))
                {
                    return;
                }
            }

            snake.Heading = direction;
            var heading = DirectionHeading(direction);
            var target = HexCoordinates.Step(members[0].SlotIndex.Value, direction);

            // Head first: each member's slot is free by the time the one behind steps into it, even across a gap
            // a popped member left.
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                var from = member.SlotIndex.Value;
                member.SetWalking(true);
                member.SetFormationHeading(heading);
                relocate(member, target);
                target = from;
            }
        }

        private void RollReinforcements()
        {
            var chance = _settings.ReinforcementChance;
            if (chance <= 0f)
            {
                return;
            }

            for (var i = 0; i < _formations.Count; i++)
            {
                if (!_formations[i].Touched && Random01() < chance)
                {
                    _reinforcementCalls++;
                }
            }
        }

        private void ChangeFormationsDoneHolding(Action<IWriteableDynamicSlotActor, Vector2Int> relocate)
        {
            for (var i = 0; i < _formations.Count; i++)
            {
                var formation = _formations[i];
                if (formation.WavesLeft > 0)
                {
                    formation.WavesLeft--;
                    continue;
                }

                CollectCandidates(formation);
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
        private void FormFromFreeUnbreakables(Action<IWriteableDynamicSlotActor, Vector2Int> relocate)
        {
            while (true)
            {
                CollectCandidates(null);
                if (_candidates.Count < _settings.MinMembers || !TryPlanNextShape(null, out var shape))
                {
                    return;
                }

                var formation = TakeSpareFormation();
                _formations.Add(formation);
                Apply(formation, shape, relocate);
            }
        }

        private void Apply(
            StandingFormation formation, FormationShape shape, Action<IWriteableDynamicSlotActor, Vector2Int> relocate)
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
                members.Add(member);
                _formationOf[member] = formation;
                if (member.SlotIndex.Value != assignment.Target)
                {
                    relocate(member, assignment.Target);
                }
            }

            formation.Shape = shape;
            formation.WavesLeft = _settings.HoldWaves - 1;
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

        // The unbreakables a formation may plan with: its own members and every free unbreakable. Members of
        // other formations are left out, so the planner treats their slots as taken. Null: free ones only.
        private void CollectCandidates(StandingFormation own)
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
                FormationShape.Snake => _settings.SnakeWeight,
                _ => 0f,
            };
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
            if (_arranging || change.ChangeType != SlotGridChangeType.Removed || _formations.Count == 0)
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

                if (members.Count < _settings.MinMembers)
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
