using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Assist
{
    /// <summary>
    ///     Need from armour left standing: each tough (or tougher) or unbreakable balloon on the board counts from 0 when it lands
    ///     to 1 once it has stood <see cref="IAssistSettings.ArmourSecondsForFull" />, and
    ///     <see cref="IAssistSettings.ArmourCountForFull" /> of them at full age are full armour. The need is that
    ///     share times <see cref="IAssistSettings.ArmourNeedWeight" />, read off the board when a roll asks.
    /// </summary>
    internal sealed class ArmourNeed : IArmourNeed, IStartable, IRunResettable, IDisposable
    {
        private readonly SlotGrid _grid;
        private readonly IAssistSettings _settings;
        // When each armoured balloon on the board landed. A balloon moved between slots keeps its time, since it
        // is still the same armour the player has left standing.
        private readonly Dictionary<IWriteableSlotActor, float> _landedAt = new();
        private readonly HashSet<IWriteableSlotActor> _standing = new();
        private readonly List<IWriteableSlotActor> _gone = new();

        private IDisposable _subscription;

        // Tests step the clock; game time, as the Snipe's cooldown, so a pause ages nothing.
        internal Func<float> Now { get; set; } = () => Time.time;
        public int ResetOrder => RunResetOrder.Counters;

        public float Need
        {
            get
            {
                var weight = Mathf.Clamp01(_settings.ArmourNeedWeight);
                var countForFull = _settings.ArmourCountForFull;
                if (weight <= 0f || countForFull <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(StandingArmour() / countForFull) * weight;
            }
        }

        internal ArmourNeed(SlotGrid grid, IAssistSettings settings)
        {
            _grid = grid;
            _settings = settings;
        }

        public void Start()
        {
            _subscription = _grid.OnChanged.Subscribe(OnGridChanged);
        }

        public void ResetRun(int generation)
        {
            _landedAt.Clear();
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private static bool IsArmoured(IWriteableSlotActor actor)
        {
            return actor is ToughBalloonModel or UnbreakableBalloonModel;
        }

        private void OnGridChanged(SlotGridChangedEvent change)
        {
            if (change.ChangeType != SlotGridChangeType.Placed)
            {
                return;
            }

            var actor = _grid.At(change.Index);
            if (IsArmoured(actor) && !_landedAt.ContainsKey(actor))
            {
                _landedAt[actor] = Now();
            }
        }

        // Each armoured balloon's age as a share of the full age, summed. Forgets the ones no longer on the board.
        private float StandingArmour()
        {
            var now = Now();
            var fullAge = Mathf.Max(0.01f, _settings.ArmourSecondsForFull);
            var armour = 0f;
            _standing.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var actor = _grid.At(new Vector2Int(col, row));
                    if (!IsArmoured(actor))
                    {
                        continue;
                    }

                    _standing.Add(actor);
                    if (!_landedAt.TryGetValue(actor, out var landedAt))
                    {
                        landedAt = now;
                        _landedAt[actor] = now;
                    }

                    armour += Mathf.Clamp01((now - landedAt) / fullAge);
                }
            }

            _gone.Clear();
            foreach (var actor in _landedAt.Keys)
            {
                if (!_standing.Contains(actor))
                {
                    _gone.Add(actor);
                }
            }

            foreach (var actor in _gone)
            {
                _landedAt.Remove(actor);
            }

            return armour;
        }
    }
}
