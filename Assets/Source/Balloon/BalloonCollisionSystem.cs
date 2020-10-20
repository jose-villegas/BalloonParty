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
        return context.CreateCollector(GameMatcher.AnyOf(GameMatcher.TriggerEnter2D, GameMatcher.TriggerExit2D, GameMatcher.TriggerStay2D));
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.isBalloonCollider && (entity.hasTriggerEnter2D || entity.hasTriggerStay2D);
    }

    protected override void Execute(List<GameEntity> entities)
    {
        foreach (var collider in entities)
        {
            var collided = collider.hasTriggerEnter2D ? collider.triggerEnter2D.Value :
                collider.hasTriggerStay2D ? collider.triggerStay2D.Value : null;

            if (collided == null) continue;

            // we are colliding with balloons
            if ((collided.gameObject.layer & _layer) > 0)
            {
                var linkedView = collided.GetComponent<ILinkedView>();
                
                if (linkedView.LinkedEntity is GameEntity balloonEntity && balloonEntity.isBalloon)
                {
                    if (collider.isFreeProjectile)
                    {
                        HandleProjectileCollider(balloonEntity.balloonColor.Value, collider);
                    }
                    
                    balloonEntity.isBalloonHit = true;
                }
            }
        }
    }

    private static void HandleProjectileCollider(string color, GameEntity projectile)
    {
        if (!projectile.hasBalloonColor)
        {
            projectile.AddBalloonColor(color);
            projectile.AddBalloonLastColorPopCount(1);
        }
        else
        {
            if (color == projectile.balloonColor.Value)
            {
                var colorCount = projectile.balloonLastColorPopCount.Value;
                projectile.ReplaceBalloonLastColorPopCount(colorCount + 1);
            }
            else
            {
                projectile.ReplaceBalloonColor(color);
                projectile.ReplaceBalloonLastColorPopCount(1);
            }
        }
    }
}