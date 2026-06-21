using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace BalloonParty.Item.Bomb
{
    internal class BombItemHandler : IBalloonItem
    {
        private readonly ItemEffectPlayer _effectPlayer;
        private readonly BalloonOverlapQuery _overlap;
        private readonly IPublisher<ActorHitMessage> _hitPublisher;
        private readonly IPublisher<NudgeMessage> _nudgePublisher;
        private readonly IItemConfiguration _itemConfig;
        private readonly List<Collider2D> _overlapResults = new(8);
        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[6];
        private readonly DisturbanceFieldService _disturbanceField;

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Bomb;

        [Inject]
        public BombItemHandler(
            IItemConfiguration itemConfig,
            IPublisher<ActorHitMessage> hitPublisher,
            IPublisher<NudgeMessage> nudgePublisher,
            ItemEffectPlayer effectPlayer,
            BalloonOverlapQuery overlap,
            DisturbanceFieldService disturbanceField)
        {
            _itemConfig = itemConfig;
            _hitPublisher = hitPublisher;
            _nudgePublisher = nudgePublisher;
            _effectPlayer = effectPlayer;
            _overlap = overlap;
            _disturbanceField = disturbanceField;
        }

        public void Setup(IBalloonModel balloon, Vector3 worldPosition)
        {
            _balloon = balloon;
            _worldPosition = worldPosition;
        }

        public UniTask Activate()
        {
            var settings = _itemConfig[ItemType.Bomb];

            // Shockwave first — nudges all balloons before any get popped or
            // marked unstable by the blast's neighbor nudges.
            _nudgePublisher.Publish(new NudgeMessage(
                null,
                _worldPosition,
                NudgeType.Shockwave,
                settings.Bomb.NudgeOverrides));

            var sourceColorId = _balloon.GetColorId();
            BlastBalloons(settings.Bomb.Radius, new DamageContext(settings.Damage, settings.Flags, sourceColorId));
            _effectPlayer.Play(settings, _worldPosition, sourceColorId);

            _disturbanceField.Stamp(StampSource.Bomb, _worldPosition, Vector2.zero);

            return UniTask.CompletedTask;
        }

        private void BlastBalloons(float radius, DamageContext context)
        {
            var bombSlot = _balloon.SlotIndex.Value;
            HexCoordinates.HexNeighborIndices(bombSlot.x, bombSlot.y, _neighborBuffer);

            // Direct hex neighbors always receive piercing damage — the blast core
            // guarantees a kill regardless of HitsRemaining or Deflect logic.
            var piercingContext = new DamageContext(context.Damage, DamageFlags.Piercing, context.SourceColorId);

            var count = Physics2D.OverlapCircle(_worldPosition, radius, _overlap.Filter, _overlapResults);

            for (var i = 0; i < count; i++)
            {
                if (!_overlap.TryResolveBalloon(_overlapResults[i], _balloon, out var balloonView, out var model))
                {
                    continue;
                }

                var modelSlot = model.SlotIndex.Value;
                var isNeighbor = false;
                for (var n = 0; n < 6; n++)
                {
                    if (_neighborBuffer[n] == modelSlot)
                    {
                        isNeighbor = true;
                        break;
                    }
                }

                var hitContext = isNeighbor ? piercingContext : context;

                _hitPublisher.Publish(ActorHitMessage.From(model,
                    balloonView.transform.position,
                    Vector3.zero,
                    hitContext));
            }
        }
    }
}
