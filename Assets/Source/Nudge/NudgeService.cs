using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Nudge
{
    internal class NudgeService : IStartable
    {
        private readonly BalloonsConfiguration _config;
        private readonly HashSet<IBalloonModel> _nudging = new();
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly ISubscriber<BalloonNudgeMessage> _nudgeSubscriber;
        private readonly NudgeOverrideResolver _resolver;
        private readonly SlotGrid _grid;

        [Inject]
        internal NudgeService(
            SlotGrid grid,
            BalloonsConfiguration config,
            NudgeOverrideResolver resolver,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            ISubscriber<BalloonNudgeMessage> nudgeSubscriber)
        {
            _grid = grid;
            _config = config;
            _resolver = resolver;
            _hitSubscriber = hitSubscriber;
            _nudgeSubscriber = nudgeSubscriber;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnBalloonHit);
            _nudgeSubscriber.Subscribe(OnNudge);
        }

        private void NudgeBalloon(Vector2Int slot, Vector3 origin, float distance, float duration)
        {
            var model = _grid.At(slot);
            if (model == null)
            {
                return;
            }

            if (!model.IsStable.Value && !_nudging.Contains(model))
            {
                return;
            }

            var view = _grid.ViewAt(slot);
            if (view == null)
            {
                return;
            }

            var slotPos = _grid.IndexToWorldPosition(slot);
            var direction = slotPos - origin;

            _nudging.Add(model);
            model.IsStable.Value = false;
            view.Nudge(slotPos,
                direction,
                distance,
                duration,
                () =>
                {
                    model.IsStable.Value = true;
                    _nudging.Remove(model);
                });
        }

        private void OnBalloonHit(BalloonHitMessage msg)
        {
            var hitSlot = msg.Balloon.SlotIndex.Value;
            var hitSlotPos = _grid.IndexToWorldPosition(hitSlot);
            var neighbors = _grid.GetNeighbors(hitSlot.x, hitSlot.y);

            foreach (var neighbor in neighbors)
            {
                var slot = neighbor.SlotIndex.Value;
                var distance = _resolver.ResolveDistance(neighbor.NudgeOverrides, null, NudgeType.Neighbor);
                var duration = _resolver.ResolveDuration(neighbor.NudgeOverrides, null, NudgeType.Neighbor);
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

                    // Per-balloon shockwave override takes priority over publisher attenuation
                    var balloonOverride = NudgeOverrideResolver.FindOverride(model.NudgeOverrides, NudgeType.Shockwave);
                    float distance;
                    float duration;

                    if (balloonOverride != null)
                    {
                        distance = balloonOverride.Distance;
                        duration = balloonOverride.Duration;
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
