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
                        }
                        else
                        {
                            freeProjectile.ReplaceBalloonColor(color);
                            freeProjectile.ReplaceBalloonLastColorPopCount(1);
                        }
                    }

                    balloonEntity.isBalloonHit = true;
                }
            }
        }
    }


}