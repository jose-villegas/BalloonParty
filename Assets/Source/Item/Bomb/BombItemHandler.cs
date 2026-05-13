using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace BalloonParty.Item.Bomb
{
    public class BombItemHandler : IBalloonItem
    {
        private static readonly int BalloonsLayer = LayerMask.GetMask("Balloons");

        private readonly ContactFilter2D _balloonFilter;
        private readonly GamePalette _palette;
        private readonly IPublisher<BalloonHitMessage> _hitPublisher;
        private readonly ItemConfiguration _itemConfig;
        private readonly IPublisher<BalloonNudgeMessage> _nudgePublisher;
        private readonly List<Collider2D> _overlapResults = new(8);
        private readonly PoolManager _poolManager;

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Bomb;

        [Inject]
        public BombItemHandler(
            GamePalette palette,
            ItemConfiguration itemConfig,
            IPublisher<BalloonHitMessage> hitPublisher,
            IPublisher<BalloonNudgeMessage> nudgePublisher,
            PoolManager poolManager)
        {
            _palette = palette;
            _itemConfig = itemConfig;
            _hitPublisher = hitPublisher;
            _nudgePublisher = nudgePublisher;
            _poolManager = poolManager;

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

            BlastBalloons(settings.BombRadius);

            // Publish a single shockwave — NudgeService handles grid iteration and falloff
            _nudgePublisher.Publish(new BalloonNudgeMessage(
                null,
                _worldPosition,
                NudgeType.Shockwave,
                settings.NudgeOverrides));

            SpawnVisual(settings);

            return UniTask.CompletedTask;
        }

        private void BlastBalloons(float radius)
        {
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

                _hitPublisher.Publish(new BalloonHitMessage(balloonView.Model, balloonView.transform.position, Vector3.zero));
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

            var balloonColor = _palette.GetColor(_balloon.Color.Value);
            effect.Play(_worldPosition, balloonColor, () => _poolManager.Return(key, effect));
        }
    }
}
