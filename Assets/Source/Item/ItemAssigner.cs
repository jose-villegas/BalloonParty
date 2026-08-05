using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Balloon;
using BalloonParty.Balloon.Type;
using BalloonParty.Shared;
using BalloonParty.Thrower;
using BalloonParty.Item.Shield;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Items;
using Random = UnityEngine.Random;

namespace BalloonParty.Item
{
    internal class ItemAssigner : IStartable, IDisposable
    {
        // The upper semicircle, trimmed of the near-horizontal shots that never climb.
        private const int FanSamples = 25;
        private const float FanMinDegrees = 25f;
        private const float FanMaxDegrees = 155f;

        private readonly ISubscriber<ItemCheckMessage> _checkSubscriber;
        private readonly IActiveLevelParameters _levelParams;
        private readonly SlotGrid _grid;
        private readonly ISlotGridConfig _gridConfig;
        private readonly BalloonContactRadii _radii;
        private readonly IProjectileFlightConfig _flightConfig;
        private readonly IRunConfig _runConfig;
        private readonly ThrowerOriginProvider _origin;
        private readonly IDeflectorField _deflectorField;

        private readonly List<IHasWriteableItemSlot> _eligibleBuffer = new();
        private readonly Dictionary<string, int> _activeCountsBuffer = new();

        private readonly List<ItemSettings> _guaranteedBuffer = new();

        // Hosts the shield chain picked, in flight order; drained as shields are handed out.
        private readonly List<ShieldPlacement> _chainBuffer = new();
        private readonly List<ShieldHostCandidate> _candidateBuffer = new();
        private readonly List<DeflectorCircle> _deflectorBuffer = new();
        private readonly List<Vector2> _fanBuffer = new();
        private int _chainCursor;

        private IDisposable _subscription;

        [Inject]
        internal ItemAssigner(
            IActiveLevelParameters levelParams,
            SlotGrid grid,
            ISlotGridConfig gridConfig,
            BalloonContactRadii radii,
            IProjectileFlightConfig flightConfig,
            IRunConfig runConfig,
            ThrowerOriginProvider origin,
            IDeflectorField deflectorField,
            ISubscriber<ItemCheckMessage> checkSubscriber)
        {
            _levelParams = levelParams;
            _grid = grid;
            _gridConfig = gridConfig;
            _radii = radii;
            _flightConfig = flightConfig;
            _runConfig = runConfig;
            _origin = origin;
            _deflectorField = deflectorField;
            _checkSubscriber = checkSubscriber;
        }

        public void Start()
        {
            _subscription = _checkSubscriber.Subscribe(OnItemCheck);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void OnItemCheck(ItemCheckMessage msg)
        {
            if (msg.NewBalloons == null || msg.NewBalloons.Count == 0)
            {
                return;
            }

            // Initial board fills skip the cadence gate; later spawns roll only on a cadence turn.
            AnimationCurve weights;
            if (msg.IsInitialSpawn)
            {
                weights = _levelParams.Current.InitialItemCountWeights;
            }
            else if (IsCadenceTurn(msg.TurnCount))
            {
                weights = _levelParams.Current.ItemCountWeights;
            }
            else
            {
                return;
            }

            var count = SampleCount(weights, Random.value);
            AssignItems(msg.NewBalloons, count, msg.IsInitialSpawn);
        }

        // Tracks running per-type counts so caps hold within the batch, not just the pre-existing board.
        private void AssignItems(IReadOnlyList<IBalloonModel> newBalloons, int count, bool isInitial)
        {
            var candidates = _levelParams.Current.Items;
            if (candidates.Count == 0)
            {
                return;
            }

            CollectEligibleSlots(newBalloons);
            if (_eligibleBuffer.Count == 0)
            {
                return;
            }

            PlanShieldChain();

            _activeCountsBuffer.Clear();
            foreach (var c in candidates)
            {
                _activeCountsBuffer[c.Type.ToString()] = CountBalloonsWithItem(c.Type);
            }

            if (isInitial)
            {
                count = Mathf.Max(0, count - PlaceGuaranteedItems());
            }

            PlaceWeightedItems(Mathf.Min(count, _eligibleBuffer.Count));
        }

        // Deterministic, not weighted — but they still take hosts from the same pool, so they go
        // through ConsumeHost and keep the chain's indices honest.
        private int PlaceGuaranteedItems()
        {
            _guaranteedBuffer.Clear();
            var placed = _levelParams.Current.FillGuaranteedItems(_guaranteedBuffer, _activeCountsBuffer);

            foreach (var item in _guaranteedBuffer)
            {
                if (_eligibleBuffer.Count == 0 || !TryTakeHost(item.Type, out var indexOf))
                {
                    break;
                }

                ConsumeHost(indexOf, item.Type);
            }

            return placed;
        }

        private void PlaceWeightedItems(int grants)
        {
            for (var n = 0; n < grants; n++)
            {
                var picked = _levelParams.Current.PickItemEntry(_activeCountsBuffer);
                if (picked == null || picked.Type == ItemType.None)
                {
                    // Every candidate is at its cap.
                    break;
                }

                if (!TryTakeHost(picked.Type, out var indexOf))
                {
                    break;
                }

                ConsumeHost(indexOf, picked.Type);

                var key = picked.Type.ToString();
                _activeCountsBuffer[key] = _activeCountsBuffer[key] + 1;
            }
        }

        // Shields follow the planned chain when there is one; everything else takes the weighted draw.
        private bool TryTakeHost(ItemType type, out int indexOf)
        {
            if (type == ItemType.Shield && TryTakeChainHost(out indexOf))
            {
                return true;
            }

            indexOf = PickWeightedIndex(_eligibleBuffer, Random.value);

            // Defensive only — CollectEligibleSlots already excludes zero-weight hosts.
            return indexOf >= 0;
        }

        private void ConsumeHost(int indexOf, ItemType type)
        {
            _eligibleBuffer[indexOf].Item.Value = type;
            _eligibleBuffer.RemoveAt(indexOf);
            DropChainHostsAfterRemoval(indexOf);
        }

        // Builds a chain over this batch's eligible hosts before any item is handed out, so shields
        // land on balloons a shot can actually collect in sequence rather than wherever the weighted
        // draw happened to point. Off, or with no thrower yet, the draw is untouched.
        private void PlanShieldChain()
        {
            _chainBuffer.Clear();
            _chainCursor = 0;
            if (!_runConfig.PlanShieldChains || !_origin.IsAvailable || _eligibleBuffer.Count == 0)
            {
                return;
            }

            // Slot positions, not view positions: the board is mid-spawn here, and the lattice is
            // where these balloons are heading.
            //
            // Radii are per type plus the shot's own, because that is what contact actually is — the
            // types differ (an ordinary balloon, a tough and a soap cluster are three circles), and a
            // single averaged radius plans shields the shot grazes past on one type and clips early
            // on another.
            var projectileRadius = _origin.ProjectileContactRadius;
            _candidateBuffer.Clear();
            for (var i = 0; i < _eligibleBuffer.Count; i++)
            {
                var host = _eligibleBuffer[i] as ISlotActor;
                var world = host != null ? _grid.IndexToWorldPosition(host.SlotIndex) : Vector3.zero;
                var type = _eligibleBuffer[i] is IBalloonModel balloon ? balloon.TypeName : BalloonType.Simple;
                _candidateBuffer.Add(
                    new ShieldHostCandidate(world, _radii.For(type) + projectileRadius));
            }

            _deflectorBuffer.Clear();
            _deflectorField?.CollectDeflectors(_deflectorBuffer);
            BuildFan();

            var planner = new ShieldChainPlanner(new WallLimits(_flightConfig.LimitsClockwise), _deflectorBuffer);
            planner.PlanChain(
                _origin.Origin, _fanBuffer, _flightConfig.ProjectileStartingShields,
                _eligibleBuffer.Count, _candidateBuffer, _chainBuffer);
        }

        // The upper semicircle, which is what the thrower's position at the bottom limits aiming to
        // in practice — nothing in code clamps it. Near-horizontal is skipped: with no gravity a flat
        // shot bounces between the side walls forever without ever reaching the board.
        private void BuildFan()
        {
            _fanBuffer.Clear();
            for (var i = 0; i < FanSamples; i++)
            {
                var t = (float)i / (FanSamples - 1);
                var degrees = Mathf.Lerp(FanMinDegrees, FanMaxDegrees, t);
                _fanBuffer.Add(new Vector2(
                    Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)));
            }
        }

        private bool TryTakeChainHost(out int index)
        {
            index = -1;
            while (_chainCursor < _chainBuffer.Count)
            {
                var candidate = _chainBuffer[_chainCursor++].CandidateIndex;
                if (candidate >= 0 && candidate < _eligibleBuffer.Count)
                {
                    index = candidate;
                    return true;
                }
            }

            return false;
        }

        // _eligibleBuffer is compacted as hosts are consumed, so every planned index above the removed
        // one shifts down. Left unadjusted the chain would walk onto the wrong balloons — silently,
        // since every index stays in range.
        private void DropChainHostsAfterRemoval(int removedIndex)
        {
            for (var i = 0; i < _chainBuffer.Count; i++)
            {
                var placement = _chainBuffer[i];
                if (placement.CandidateIndex > removedIndex)
                {
                    _chainBuffer[i] = new ShieldPlacement(placement.CandidateIndex - 1, placement.EntryAngles);
                }
                else if (placement.CandidateIndex == removedIndex)
                {
                    _chainBuffer[i] = new ShieldPlacement(-1, placement.EntryAngles);
                }
            }
        }

        // Kept as the historical entry point; the draw lives in AnimationCurveExtensions so balloon
        // wave quotas share it.
        internal static int SampleCount(AnimationCurve weights, float roll01)
        {
            return weights.SampleWeightedCount(roll01);
        }

        // Weighted-random index into candidates, biased by ItemActivationWeight; -1 if every weight is 0.
        internal static int PickWeightedIndex(IReadOnlyList<IHasWriteableItemSlot> candidates, float roll01)
        {
            var total = 0f;
            for (var i = 0; i < candidates.Count; i++)
            {
                total += candidates[i].ItemActivationWeight;
            }

            if (total <= 0f)
            {
                return -1;
            }

            var target = Mathf.Clamp01(roll01) * total;
            var accumulated = 0f;
            for (var i = 0; i < candidates.Count; i++)
            {
                accumulated += candidates[i].ItemActivationWeight;
                if (target < accumulated)
                {
                    return i;
                }
            }

            return candidates.Count - 1;
        }

        private bool IsCadenceTurn(int turns)
        {
            var cadence = _levelParams.Current.ItemCadence;
            return cadence > 0 && turns % cadence == 0;
        }

        private void CollectEligibleSlots(IReadOnlyList<IBalloonModel> newBalloons)
        {
            _eligibleBuffer.Clear();
            for (var i = 0; i < newBalloons.Count; i++)
            {
                if (newBalloons[i] is IHasWriteableItemSlot slot && slot.Item.Value == ItemType.None &&
                    slot.ItemActivationWeight > 0f)
                {
                    _eligibleBuffer.Add(slot);
                }
            }
        }

        // Counts via the capability interface, not a concrete model type, or caps silently break.
        private int CountBalloonsWithItem(ItemType type)
        {
            var count = 0;

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    if (_grid.At(new Vector2Int(col, row)) is IHasItemSlot host && host.Item.Value == type)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
