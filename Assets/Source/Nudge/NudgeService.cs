using System.Collections.Generic;
using BalloonParty.Balloon.View;
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
        private readonly HashSet<IWriteableSlotActor> _nudging = new();
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ISubscriber<BalloonNudgeMessage> _nudgeSubscriber;
        private readonly NudgeOverrideResolver _resolver;
        private readonly SlotGrid _grid;

        [Inject]
        internal NudgeService(
            SlotGrid grid,
            NudgeOverrideResolver resolver,
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<BalloonNudgeMessage> nudgeSubscriber)
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

        private void NudgeBalloon(Vector2Int slot, Vector3 origin, float distance, float duration)
        {
            var model = _grid.At(slot);
            if (model == null)
            {
                return;
            }

            if (model is not IWriteableDynamicSlotActor dynamicModel)
            {
                return;
            }

            if (!dynamicModel.IsStable.Value && !_nudging.Contains(model))
            {
                return;
            }

            if (_grid.ViewAt(slot) is not BalloonView balloonView)
            {
                return;
            }

            var slotPos = _grid.IndexToWorldPosition(slot);
            var direction = slotPos - origin;

            _nudging.Add(model);
            dynamicModel.IsStable.Value = false;
            balloonView.Nudge(slotPos,
                direction,
                distance,
                duration,
                () =>
                {
                    dynamicModel.IsStable.Value = true;
                    _nudging.Remove(model);
                });
        }

        private void OnActorHit(ActorHitMessage msg)
        {
            if (msg.Actor is not IHasNudge)
            {
                return;
            }

            var hitSlot = msg.Actor.SlotIndex;
            var hitSlotPos = _grid.IndexToWorldPosition(hitSlot);
            var neighbors = _grid.GetNeighbors(hitSlot.x, hitSlot.y);

            foreach (var neighbor in neighbors)
            {
                if (neighbor is not IHasNudge nudgeable)
                {
                    continue;
                }

                var slot = neighbor.SlotIndex;
                var distance = _resolver.ResolveDistance(nudgeable.NudgeOverrides, null, NudgeType.Neighbor);
                var duration = _resolver.ResolveDuration(nudgeable.NudgeOverrides, null, NudgeType.Neighbor);
                NudgeBalloon(slot, hitSlotPos, distance, duration);
            }
        }

        private void OnNudge(BalloonNudgeMessage msg)
        {
            if (msg.Source == NudgeType.Shockwave)
            {
                HandleShockwave(msg);
            }
            else
            {
                HandleSingleBalloon(msg);
            }
        }

        private void HandleSingleBalloon(BalloonNudgeMessage msg)
        {
            if (msg.Balloon == null)
            {
                return;
            }

            var slot = msg.Balloon.SlotIndex.Value;
            var distance = _resolver.ResolveDistance(msg.Balloon.NudgeOverrides, msg.Overrides, msg.Source);
            var duration = _resolver.ResolveDuration(msg.Balloon.NudgeOverrides, msg.Overrides, msg.Source);
            NudgeBalloon(slot, msg.Origin, distance, duration);
        }

        private void HandleShockwave(BalloonNudgeMessage msg)
        {
            var baseDistance = _resolver.ResolveDistance(null, msg.Overrides, NudgeType.Shockwave);
            var baseDuration = _resolver.ResolveDuration(null, msg.Overrides, NudgeType.Shockwave);
            var falloff = _resolver.ResolveFalloff(msg.Overrides, NudgeType.Shockwave);

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var slot = new Vector2Int(col, row);
                    var model = _grid.At(slot);

                    if (model == null)
                    {
                        continue;
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
                        continue;
                    }

                    NudgeBalloon(slot, msg.Origin, distance, duration);
                }
            }
        }
    }
}
