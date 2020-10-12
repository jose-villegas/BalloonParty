using System.Collections.Generic;
using DG.Tweening;
using Entitas;
using UnityEngine;

public class BalloonCollisionSystem : ReactiveSystem<GameEntity>
{
    private readonly Contexts _contexts;
    private readonly int _layer;
    private readonly IGameConfiguration _configuration;
    private IEntity[,] _slots;

    public BalloonCollisionSystem(Contexts contexts) : base(contexts.game)
    {
        _contexts = contexts;
        _layer = LayerMask.NameToLayer("Balloons");
        _slots = _contexts.game.slotsIndexer.Value;
        _configuration = contexts.configuration.gameConfiguration.value;
    }

    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(GameMatcher.AllOf(GameMatcher.FreeProjectile, GameMatcher.TriggerEnter2D));
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.isFreeProjectile && entity.hasTriggerEnter2D;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        foreach (var freeProjectile in entities)
        {
            var collider = freeProjectile.triggerEnter2D.Value;

            // we are colliding with balloons
            if ((collider.gameObject.layer & _layer) > 0)
            {
                var linkedView = collider.GetComponent<ILinkedView>();

                if (linkedView.LinkedEntity is GameEntity balloonEntity && balloonEntity.isBalloon)
                {
                    var color = balloonEntity.balloonColor.Value;

                    if (!freeProjectile.hasBalloonColor)
                    {
                        freeProjectile.AddBalloonColor(color);
                        freeProjectile.AddBalloonLastColorPopCount(1);
                    }
                    else
                    {
                        if (color == freeProjectile.balloonColor.Value)
                        {
                            var colorCount = freeProjectile.balloonLastColorPopCount.Value;
                            freeProjectile.ReplaceBalloonLastColorPopCount(colorCount + 1);

                            // when 3 of the same color are hit, add an extra bounce shield
                            if (colorCount >= 2)
                            {
                                var shields = freeProjectile.projectileBounceShield.Value;
                                freeProjectile.ReplaceProjectileBounceShield(shields + 1);

                                // play particle fx
                                var gain = _contexts.game.CreateEntity();
                                gain.AddParticleFXParent(freeProjectile.linkedView.Value);
                                gain.AddPlayParticleFX("PSVFX_ShieldGain");
                                gain.AddParticleFXStartColor(
                                    _configuration.BalloonColor(freeProjectile.balloonColor.Value));
                            }
                        }
                        else
                        {
                            freeProjectile.ReplaceBalloonColor(color);
                            freeProjectile.ReplaceBalloonLastColorPopCount(1);
                        }
                    }

                    // create balloon pop effect
                    var e = _contexts.game.CreateEntity();
                    e.AddPosition(balloonEntity.position.Value);
                    e.AddParticleFXStartColor(_configuration.BalloonColor(balloonEntity.balloonColor.Value));
                    e.AddPlayParticleFX("PSVFX_BalloonPop");

                    // operate on indexing value
                    var index = balloonEntity.slotIndex.Value;

                    // start nudge animation from explosion on neighboring slots
                    NudgeNeighbors(index, balloonEntity);

                    // remove from indexer
                    _slots[index.x, index.y] = null;

                    // add score
                    _contexts.game.AddScore(balloonEntity.balloonColor.Value);

                    // destroy
                    balloonEntity.isDestroyed = true;
                }
            }
        }
    }

    private void NudgeNeighbors(Vector2Int index, GameEntity balloonEntity)
    {
        var neighbors = _slots.GetNeighbors(index.x, index.y);

        foreach (var neighbor in neighbors)
        {
            if (neighbor is GameEntity neighborEntity)
            {
                var mono = neighborEntity.linkedView.Value as MonoBehaviour;

                if (mono != null)
                {
                    var position = neighborEntity.position.Value;
                    var direction = neighborEntity.position.Value - balloonEntity.position.Value;
                    var slotIndex = neighborEntity.slotIndex.Value;
                    var sequence = DOTween.Sequence();

                    sequence.Append(mono.transform.DOMove(
                        position + direction.normalized * _configuration.NudgeDistance,
                        _configuration.NudgeDuration / 2f));
                    sequence.Append(mono.transform.DOMove(slotIndex.IndexToPosition(_configuration),
                        _configuration.NudgeDuration / 2f));
                    neighborEntity.isStableBalloon = false;
                    sequence.onComplete += () => { neighborEntity.isStableBalloon = true; };

                    // add tweenSequence to entity to avoid colliding with another tween
                    neighborEntity.ReplaceTweenSequence(sequence);
                }
            }
        }
    }
}