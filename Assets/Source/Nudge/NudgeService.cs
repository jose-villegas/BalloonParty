using System.Collections.Generic;
using System.Linq;
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
    public class NudgeService : IStartable
    {
        private readonly BalloonsConfiguration _config;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly ISubscriber<BalloonNudgeMessage> _nudgeSubscriber;

        // Balloons currently mid-nudge tween. Distinguishes nudge-unstable from spawn-unstable.
        private readonly HashSet<IBalloonModel> _nudging = new();

        [Inject]
        public NudgeService(
            SlotGrid grid,
            BalloonsConfiguration config,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            ISubscriber<BalloonNudgeMessage> nudgeSubscriber)
        {
            _grid = grid;
            _config = config;
            _hitSubscriber = hitSubscriber;
            _nudgeSubscriber = nudgeSubscriber;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnBalloonHit);
            _nudgeSubscriber.Subscribe(OnNudge);
        }

        private void OnBalloonHit(BalloonHitMessage msg)
        {
            var hitSlot = msg.Balloon.SlotIndex.Value;
            var hitSlotPos = _grid.IndexToWorldPosition(hitSlot);
            var neighbors = _grid.GetNeighbors(hitSlot.x, hitSlot.y);

            foreach (var neighbor in neighbors)
            {
                var slot = neighbor.SlotIndex.Value;
                var distance = ResolveDistance(neighbor.NudgeOverrides, null, NudgeType.Neighbor);
                var duration = ResolveDuration(neighbor.NudgeOverrides, null, NudgeType.Neighbor);
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
            var distance = ResolveDistance(msg.Balloon.NudgeOverrides, msg.Overrides, msg.Source);
            var duration = ResolveDuration(msg.Balloon.NudgeOverrides, msg.Overrides, msg.Source);
            NudgeBalloon(slot, msg.Origin, distance, duration);
        }

        private void HandleShockwave(BalloonNudgeMessage msg)
        {
            var baseDistance = ResolveDistance(null, msg.Overrides, NudgeType.Shockwave);
            var baseDuration = ResolveDuration(null, msg.Overrides, NudgeType.Shockwave);
            var falloff = ResolveFalloff(msg.Overrides, NudgeType.Shockwave);

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
                    var balloonOverride = FindOverride(model.NudgeOverrides, NudgeType.Shockwave);
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

        private void NudgeBalloon(Vector2Int slot, Vector3 origin, float distance, float duration)
        {
            var model = _grid.At(slot);
            if (model == null)
            {
                return;
            }

            // Allow interrupting a mid-nudge balloon, but not one that is
            // spawning or rebalancing (genuinely not at its grid position yet).
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


        private float ResolveDistance(
            NudgeOverride[] balloonOverrides,
            NudgeOverride[] publisherOverrides,
            NudgeType source)
        {
            var entry = FindOverride(balloonOverrides, source);
            if (entry != null)
            {
                return entry.Distance;
            }

            var pubEntry = FindOverride(publisherOverrides, source);
            if (pubEntry != null)
            {
                return pubEntry.Distance;
            }

            return _config.NudgeDistance;
        }

        private float ResolveDuration(
            NudgeOverride[] balloonOverrides,
            NudgeOverride[] publisherOverrides,
            NudgeType source)
        {
            var entry = FindOverride(balloonOverrides, source);
            if (entry != null)
            {
                return entry.Duration;
            }

            var pubEntry = FindOverride(publisherOverrides, source);
            if (pubEntry != null)
            {
                return pubEntry.Duration;
            }

            return _config.NudgeDuration;
        }

        private float ResolveFalloff(NudgeOverride[] overrides, NudgeType source)
        {
            var entry = FindOverride(overrides, source);
            return entry != null ? entry.Falloff : _config.NudgeFalloff;
        }

        private static NudgeOverride FindOverride(NudgeOverride[] overrides, NudgeType source)
        {
            return overrides?.FirstOrDefault(o => o.AppliesTo.HasFlag(source));
        }
    }
}
