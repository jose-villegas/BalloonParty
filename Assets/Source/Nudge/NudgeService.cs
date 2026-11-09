using System.Collections.Generic;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Nudge
{
    internal class NudgeService : IStartable
    {
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ISubscriber<NudgeMessage> _nudgeSubscriber;
        private readonly IPublisher<NudgeAppliedMessage> _appliedPublisher;
        private readonly NudgeOverrideResolver _resolver;
        private readonly SlotGrid _grid;
        private readonly List<IWriteableSlotActor> _neighborBuffer = new();
        private readonly List<NudgedActor> _nudged = new();

        [Inject]
        internal NudgeService(
            SlotGrid grid,
            NudgeOverrideResolver resolver,
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<NudgeMessage> nudgeSubscriber,
            IPublisher<NudgeAppliedMessage> appliedPublisher)
        {
            _grid = grid;
            _resolver = resolver;
            _hitSubscriber = hitSubscriber;
            _nudgeSubscriber = nudgeSubscriber;
            _appliedPublisher = appliedPublisher;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnActorHit);
            _nudgeSubscriber.Subscribe(OnNudge);
        }

        private void NudgeActor(Vector2Int slot, Vector3 origin, float distance, float duration)
        {
            if (_grid.ViewAt(slot) is not INudgeable nudgeableView)
            {
                return;
            }

            var direction = _grid.IndexToWorldPosition(slot) - origin;
            nudgeableView.Nudge(direction, distance, duration);
            if (direction.sqrMagnitude >= 1e-8f)
            {
                _nudged.Add(new NudgedActor(slot, (Vector2)direction.normalized * distance));
            }
        }

        private void OnActorHit(ActorHitMessage msg)
        {
            if (msg.Actor is not IHasNudge)
            {
                return;
            }

            _nudged.Clear();
            var hitSlot = msg.Actor.SlotIndex;
            var hitSlotPos = _grid.IndexToWorldPosition(hitSlot);
            _grid.GetNeighbors(hitSlot.x, hitSlot.y, _neighborBuffer);

            foreach (var neighbor in _neighborBuffer)
            {
                if (neighbor is not IHasNudge nudgeable)
                {
                    continue;
                }

                var slot = neighbor.SlotIndex;
                _resolver.Resolve(nudgeable.NudgeOverrides, null, NudgeType.Neighbor, out var distance, out var duration);
                NudgeActor(slot, hitSlotPos, distance, duration);
            }

            PublishApplied();
        }

        private void OnNudge(NudgeMessage msg)
        {
            _nudged.Clear();
            if (msg.Source == NudgeType.Shockwave)
            {
                HandleShockwave(msg);
            }
            else
            {
                HandleSingleActor(msg);
            }

            PublishApplied();
        }

        private void HandleSingleActor(NudgeMessage msg)
        {
            if (msg.Actor == null)
            {
                return;
            }

            if (msg.Actor is not ISlotActor slotActor)
            {
                return;
            }

            var slot = slotActor.SlotIndex;
            _resolver.Resolve(msg.Actor.NudgeOverrides, msg.Overrides, msg.Source, out var distance, out var duration);
            NudgeActor(slot, msg.Origin, distance, duration);
        }

        private void HandleShockwave(NudgeMessage msg)
        {
            _resolver.Resolve(null, msg.Overrides, NudgeType.Shockwave, out var baseDistance, out var baseDuration);
            var falloff = _resolver.ResolveFalloff(msg.Overrides, NudgeType.Shockwave);

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    ApplyShockwaveToSlot(new Vector2Int(col, row), msg, baseDistance, baseDuration, falloff);
                }
            }
        }

        private void ApplyShockwaveToSlot(
            Vector2Int slot, NudgeMessage msg, float baseDistance, float baseDuration, float falloff)
        {
            if (_grid.IsEmpty(slot.x, slot.y))
            {
                return;
            }

            var model = _grid.At(slot);
            if (model == null)
            {
                return;
            }

            IReadOnlyList<NudgeOverride> actorOverrides = null;
            if (model is IHasNudge nudgeable)
            {
                actorOverrides = nudgeable.NudgeOverrides;
            }

            // Per-actor override takes priority over publisher attenuation.
            var actorOverride = NudgeOverrideResolver.FindOverride(actorOverrides, NudgeType.Shockwave);
            float distance;
            float duration;

            if (actorOverride != null)
            {
                distance = actorOverride.Distance;
                duration = actorOverride.Duration;
            }
            else
            {
                var balloonPos = _grid.IndexToWorldPosition(slot);
                var d = Vector3.Distance(msg.Origin, balloonPos);

                // Exponential falloff — closer balloons push harder.
                distance = baseDistance * Mathf.Exp(-falloff * d);
                duration = baseDuration;
            }

            if (distance < 0.001f)
            {
                return;
            }

            NudgeActor(slot, msg.Origin, distance, duration);
        }

        // After the views have their impulses: whoever reads the pushes (BubbleMergeService) sees this nudge whole.
        private void PublishApplied()
        {
            if (_nudged.Count > 0)
            {
                _appliedPublisher.Publish(new NudgeAppliedMessage(_nudged));
            }
        }
    }
}
