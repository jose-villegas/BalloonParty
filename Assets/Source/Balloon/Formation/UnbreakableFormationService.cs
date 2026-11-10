using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     Gathers the board's unbreakables into formations. At each wave start the balancer asks it to
    ///     arrange: with enough unbreakables it plans a shape and hands the balancer the moves, anchors the
    ///     members, and holds them for <see cref="IUnbreakableFormationSettings.HoldWaves" /> waves before
    ///     picking a different shape. A formation that loses members below the minimum breaks at once.
    /// </summary>
    internal sealed class UnbreakableFormationService : IStartable, IRunResettable, IDisposable
    {
        private static readonly FormationShape[] Shapes =
        {
            FormationShape.Horizontal,
            FormationShape.DiagonalDownRight,
            FormationShape.DiagonalDownLeft,
            FormationShape.ItemCover,
        };

        private readonly SlotGrid _grid;
        private readonly IUnbreakableFormationSettings _settings;
        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;
        private readonly FormationPlanner _planner = new();
        private readonly List<IWriteableFormationMember> _candidates = new();
        private readonly List<IWriteableFormationMember> _members = new();
        private readonly List<FormationAssignment> _assignments = new();
        private readonly List<FormationShape> _shapePool = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Func<int, int> _pickIndex;

        private FormationShape? _lastShape;
        private int _wavesLeft;
        private bool _arranging;

        public int ResetOrder => RunResetOrder.Quiesce;

        // Exposed for tests.
        internal IReadOnlyList<IWriteableFormationMember> Members => _members;
        internal FormationShape? CurrentShape => _members.Count > 0 ? _lastShape : null;
        internal Func<float> Random01 { get; set; } = () => Random.value;

        [Inject]
        internal UnbreakableFormationService(
            SlotGrid grid, IBalloonsConfiguration balloonsConfig,
            IPublisher<BalanceBalloonsMessage> balancePublisher = null)
        {
            _grid = grid;
            _settings = balloonsConfig.UnbreakableFormation;
            _balancePublisher = balancePublisher;
            _pickIndex = PickIndex;
        }

        public void Start()
        {
            _grid.OnChanged.Subscribe(OnGridChanged).AddTo(_subscriptions);
        }

        public void ResetRun(int generation)
        {
            Release(requestBalance: false);
            _lastShape = null;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        /// <summary>
        ///     Runs once per wave start, before the roam pre-pass. Holds the current formation while it has
        ///     waves left; otherwise plans the next one and moves each member with
        ///     <paramref name="relocate" />, which owns the grid change and the glide.
        /// </summary>
        internal void ArrangeAtWaveStart(Action<IWriteableDynamicSlotActor, Vector2Int> relocate)
        {
            if (_settings == null || !_settings.Enabled)
            {
                Release(requestBalance: false);
                return;
            }

            if (_members.Count > 0 && _wavesLeft > 0)
            {
                _wavesLeft--;
                return;
            }

            CollectCandidates();
            if (_candidates.Count < _settings.MinMembers || !TryPlanNextShape(out var shape))
            {
                // The balancer runs the race right after this, so the released members settle there.
                Release(requestBalance: false);
                return;
            }

            Release(requestBalance: false);

            // The members' own moves remove and place them; that is not a member being lost.
            _arranging = true;
            for (var i = 0; i < _assignments.Count; i++)
            {
                var assignment = _assignments[i];
                var member = assignment.Member;
                member.SetInFormation(true);
                _members.Add(member);
                if (member.SlotIndex.Value != assignment.Target)
                {
                    relocate(member, assignment.Target);
                }
            }

            _arranging = false;
            _lastShape = shape;
            _wavesLeft = _settings.HoldWaves - 1;
        }

        private void CollectCandidates()
        {
            _candidates.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!_grid.IsEmpty(col, row) && _grid.At(new Vector2Int(col, row)) is IWriteableFormationMember member)
                    {
                        _candidates.Add(member);
                    }
                }
            }
        }

        // A weighted pick without the shape just held; a shape with no spot drops out and the pick goes
        // again. The held shape is the last resort, when nothing else fits.
        private bool TryPlanNextShape(out FormationShape shape)
        {
            _shapePool.Clear();
            foreach (var candidate in Shapes)
            {
                if (candidate != _lastShape && WeightOf(candidate) > 0f)
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

            if (_lastShape.HasValue && WeightOf(_lastShape.Value) > 0f
                && _planner.TryPlan(_grid, _lastShape.Value, _candidates, _settings, _pickIndex, _assignments))
            {
                shape = _lastShape.Value;
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
                _ => 0f,
            };
        }

        private int PickIndex(int count)
        {
            return Mathf.Min((int)(Random01() * count), count - 1);
        }

        // A member popped (or otherwise taken off the board) leaves the formation; below the minimum the
        // rest are released at once and a balance is asked for so they float up.
        private void OnGridChanged(SlotGridChangedEvent change)
        {
            if (_arranging || change.ChangeType != SlotGridChangeType.Removed || _members.Count == 0)
            {
                return;
            }

            for (var i = _members.Count - 1; i >= 0; i--)
            {
                var member = _members[i];
                var slot = member.SlotIndex.Value;
                if (!_grid.InBounds(slot) || _grid.At(slot) != member)
                {
                    member.SetInFormation(false);
                    _members.RemoveAt(i);
                }
            }

            if (_members.Count > 0 && _members.Count < _settings.MinMembers)
            {
                Release(requestBalance: true);
            }
        }

        private void Release(bool requestBalance)
        {
            if (_members.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _members.Count; i++)
            {
                _members[i].SetInFormation(false);
            }

            _members.Clear();
            _wavesLeft = 0;

            if (requestBalance)
            {
                _balancePublisher?.Publish(new BalanceBalloonsMessage());
            }
        }
    }
}
