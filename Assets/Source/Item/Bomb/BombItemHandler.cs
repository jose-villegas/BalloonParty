using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Pool;
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
        private static readonly int BalloonsLayer = LayerMask.GetMask("Balloons");

        private readonly ContactFilter2D _balloonFilter;
        private readonly GamePalette _palette;
        private readonly IPublisher<ActorHitMessage> _hitPublisher;
        private readonly IPublisher<NudgeMessage> _nudgePublisher;
        private readonly ItemConfiguration _itemConfig;
        private readonly List<Collider2D> _overlapResults = new(8);
        private readonly PoolManager _poolManager;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly DisturbanceFieldSettings _disturbanceSettings;

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Bomb;

        [Inject]
        public BombItemHandler(
            GamePalette palette,
            ItemConfiguration itemConfig,
            IPublisher<ActorHitMessage> hitPublisher,
            IPublisher<NudgeMessage> nudgePublisher,
            PoolManager poolManager,
            DisturbanceFieldService disturbanceField,
            DisturbanceFieldSettings disturbanceSettings)
        {
            _palette = palette;
            _itemConfig = itemConfig;
            _hitPublisher = hitPublisher;
            _nudgePublisher = nudgePublisher;
            _poolManager = poolManager;
            _disturbanceField = disturbanceField;
            _disturbanceSettings = disturbanceSettings;

            _balloonFilter = new ContactFilter2D();
            _balloonFilter.SetLayerMask(BalloonsLayer);
            _balloonFilter.useTriggers = true;
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
                settings.NudgeOverrides));

            var sourceColorId = (_balloon as IHasColor)?.Color.Value ?? "";
            BlastBalloons(settings.BombRadius, new DamageContext(settings.Damage, settings.Flags, sourceColorId));
            SpawnVisual(settings);

            var stamp = _disturbanceSettings.GetProfile(StampSource.Bomb);
            _disturbanceField.StampOverDuration(_worldPosition, stamp.Radius,
                stamp.Strength, Vector2.zero, stamp.Duration);

            return UniTask.CompletedTask;
        }

        private void BlastBalloons(float radius, DamageContext context)
        {
            var bombSlot = _balloon.SlotIndex.Value;
            var neighborIndices = new HashSet<Vector2Int>(SlotGrid.HexNeighborIndices(bombSlot.x, bombSlot.y));

            // Direct hex neighbors always receive piercing damage — the blast core
            // guarantees a kill regardless of HitsRemaining or Deflect logic.
            var piercingContext = new DamageContext(context.Damage, DamageFlags.Piercing, context.SourceColorId);

            var count = Physics2D.OverlapCircle(_worldPosition, radius, _balloonFilter, _overlapResults);

            for (var i = 0; i < count; i++)
            {
                var balloonView = _overlapResults[i].GetComponentInParent<BalloonView>();
                if (balloonView == null || balloonView.Model == null)
                {
                    continue;
                }

                // Skip the bomb balloon itself — it is already being handled by the
                // normal hit pipeline that triggered this activation.
                if (balloonView.Model == _balloon)
                {
                    continue;
                }

                var hitContext = neighborIndices.Contains(balloonView.Model.SlotIndex.Value)
                    ? piercingContext
                    : context;

                _hitPublisher.Publish(new ActorHitMessage(balloonView.Model,
                    balloonView.transform.position,
                    Vector3.zero,
                    balloonView.Model.EvaluateHit(hitContext),
                    hitContext));
            }
        }

        private void SpawnVisual(ItemSettings settings)
        {
            if (settings.ActivationEffectPrefab == null)
            {
                return;
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key, () => new EffectPoolChannel(settings.ActivationEffectPrefab));

            var balloonColor = _palette.GetColor((_balloon as IHasColor)?.Color.Value);
            effect.Play(_worldPosition, balloonColor, () => _poolManager.Return(key, effect));
        }
    }
}
