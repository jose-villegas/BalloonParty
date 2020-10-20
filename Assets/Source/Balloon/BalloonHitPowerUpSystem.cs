using System;
using System.Collections.Generic;
using Entitas;

public class BalloonHitPowerUpSystem : ReactiveSystem<GameEntity>
{
    private readonly Contexts _contexts;
    private readonly IGameConfiguration _configuration;
    private readonly IGroup<GameEntity> _freeProjectiles;

    public BalloonHitPowerUpSystem(Contexts contexts) : base(contexts.game)
    {
        _contexts = contexts;
        _configuration = contexts.configuration.gameConfiguration.value;

        _freeProjectiles = _contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.FreeProjectile));
    }

    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(GameMatcher.AllOf(GameMatcher.Balloon, GameMatcher.BalloonHit,
            GameMatcher.BalloonPowerUp));
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.isBalloon && entity.isBalloonHit && entity.hasBalloonPowerUp;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        foreach (var gameEntity in entities)
        {
            var powerUp = gameEntity.balloonPowerUp.Value;

            switch (powerUp)
            {
                case BalloonPowerUp.None:
                    break;
                case BalloonPowerUp.Shield:
                    foreach (var projectile in _freeProjectiles)
                    {
                        var shield = projectile.projectileBounceShield.Value;
                        projectile.ReplaceProjectileBounceShield(shield + 1);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            gameEntity.RemoveBalloonPowerUp();
        }
    }
}