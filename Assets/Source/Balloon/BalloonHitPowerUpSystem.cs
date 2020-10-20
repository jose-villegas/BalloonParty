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

                    // play particle fx
                    var gain = _contexts.game.CreateEntity();
                    gain.AddPosition(gameEntity.position.Value);
                    gain.AddParticleFXStartColor(_configuration.BalloonColor(gameEntity.balloonColor.Value));
                    gain.AddPlayParticleFX("PSVFX_ShieldGainPU");

                    break;
                case BalloonPowerUp.Bomb:
                    var e = _contexts.game.CreateEntity();

                    e.AddAsset("BombRange");
                    e.isBalloonCollider = true;
                    // initial position
                    e.AddPosition(gameEntity.position.Value);
                    
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            gameEntity.RemoveBalloonPowerUp();
        }
    }
}