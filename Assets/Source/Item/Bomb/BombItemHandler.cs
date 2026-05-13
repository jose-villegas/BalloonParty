using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
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
        private readonly SlotGrid _grid;
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
            SlotGrid grid,
            PoolManager poolManager)
        {
            _palette = palette;
            _itemConfig = itemConfig;
            _hitPublisher = hitPublisher;
            _nudgePublisher = nudgePublisher;
            _grid = grid;
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
            NudgeAllBalloons(settings);
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

        // Nudge distance falls off exponentially with distance from the explosion center:
        //   attenuated = nudgeDistance * e^(-falloff * d)
        // where d is the world-space distance to the balloon.
        private void NudgeAllBalloons(ItemSettings settings)
        {
            var nudgeDistance = settings.BombNudgeDistance;
            var nudgeFalloff = settings.BombNudgeFalloff;

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

                    if (model == null || model == _balloon)
                    {
                        continue;
                    }

                    var balloonPos = _grid.IndexToWorldPosition(slot);
                    var d = Vector3.Distance(_worldPosition, balloonPos);

                    // Exponential falloff: closer balloons get a stronger push
                    var attenuated = nudgeDistance * Mathf.Exp(-nudgeFalloff * d);

                    // Skip negligible nudges
                    if (attenuated < 0.001f)
                    {
                        continue;
                    }

                    _nudgePublisher.Publish(new BalloonNudgeMessage(
                        model,
                        _worldPosition,
                        attenuated));
                }
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
