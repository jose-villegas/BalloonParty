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
        return context.CreateCollector(GameMatcher.AllOf(GameMatcher.BalloonCollider, GameMatcher.TriggerEnter2D));
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.isBalloonCollider && entity.hasTriggerEnter2D;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        foreach (var collider in entities)
        {
            var collided = collider.triggerEnter2D.Value;

            // we are colliding with balloons
            if ((collided.gameObject.layer & _layer) > 0)
            {
                var linkedView = collided.GetComponent<ILinkedView>();

                if (linkedView.LinkedEntity is GameEntity balloonEntity && balloonEntity.isBalloon)
                {
                    if (collider.isFreeProjectile)
                    {
                        HandleProjectileCollider(balloonEntity, collider);
                    }

                    balloonEntity.isBalloonHit = true;
                }
            }
        }
    }

    private static void HandleProjectileCollider(GameEntity balloonEntity, GameEntity collider)
    {
        var color = balloonEntity.balloonColor.Value;

        if (!collider.hasBalloonColor)
        {
            collider.AddBalloonColor(color);
            collider.AddBalloonLastColorPopCount(1);
        }
        else
        {
            if (color == collider.balloonColor.Value)
            {
                var colorCount = collider.balloonLastColorPopCount.Value;
                collider.ReplaceBalloonLastColorPopCount(colorCount + 1);
            }
            else
            {
                collider.ReplaceBalloonColor(color);
                collider.ReplaceBalloonLastColorPopCount(1);
            }
        }
    }
}