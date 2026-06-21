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
        private readonly NudgeOverrideResolver _resolver;
        private readonly SlotGrid _grid;
        private readonly List<IWriteableSlotActor> _neighborBuffer = new();

        [Inject]
        internal NudgeService(
            SlotGrid grid,
            NudgeOverrideResolver resolver,
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<NudgeMessage> nudgeSubscriber)
        {
            _grid = grid;
            _resolver = resolver;
            _hitSubscriber = hitSubscriber;
            _nudgeSubscriber = nudgeSubscriber;
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

            var slotPos = _grid.IndexToWorldPosition(slot);
            nudgeableView.Nudge(slotPos, slotPos - origin, distance, duration, null);
        }

        private void OnActorHit(ActorHitMessage msg)
        {
            if (msg.Actor is not IHasNudge)
            {
                return;
            }

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
                var distance = _resolver.ResolveDistance(nudgeable.NudgeOverrides, null, NudgeType.Neighbor);
                var duration = _resolver.ResolveDuration(nudgeable.NudgeOverrides, null, NudgeType.Neighbor);
                NudgeActor(slot, hitSlotPos, distance, duration);
            }
        }

        private void OnNudge(NudgeMessage msg)
        {
            if (msg.Source == NudgeType.Shockwave)
            {
                HandleShockwave(msg);
            }
            else
            {
                HandleSingleActor(msg);
            }
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
            var distance = _resolver.ResolveDistance(msg.Actor.NudgeOverrides, msg.Overrides, msg.Source);
            var duration = _resolver.ResolveDuration(msg.Actor.NudgeOverrides, msg.Overrides, msg.Source);
            NudgeActor(slot, msg.Origin, distance, duration);
        }

        private void HandleShockwave(NudgeMessage msg)
        {
            var baseDistance = _resolver.ResolveDistance(null, msg.Overrides, NudgeType.Shockwave);
            var baseDuration = _resolver.ResolveDuration(null, msg.Overrides, NudgeType.Shockwave);
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

            // Per-actor shockwave override takes priority over publisher attenuation
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

                // Exponential falloff: closer balloons get a stronger push
                distance = baseDistance * Mathf.Exp(-falloff * d);
                duration = baseDuration;
            }

            if (distance < 0.001f)
            {
                return;
            }

            NudgeActor(slot, msg.Origin, distance, duration);
        }
    }
}
