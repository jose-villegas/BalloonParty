using System.Collections.Generic;
using Entitas;
using UnityEngine;

public class ProjectileShieldFXSystem : ReactiveSystem<GameEntity>
{
    private readonly Contexts _contexts;
    private readonly IGameConfiguration _configuration;
    private int _counter;

    public ProjectileShieldFXSystem(Contexts contexts) : base(contexts.game)
    {
        _contexts = contexts;
        _configuration = _contexts.configuration.gameConfiguration.value;
        _counter = _configuration.ProjectileStartingShields;
    }
    
    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(
            GameMatcher.AllOf(GameMatcher.FreeProjectile, GameMatcher.ProjectileBounceShield));
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.isFreeProjectile && entity.hasProjectileBounceShield;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        foreach (var gameEntity in entities)
        {
            if (gameEntity.projectileBounceShield.Value > _counter && gameEntity.hasBalloonColor)
            {
                // play particle fx
                var gain = _contexts.game.CreateEntity();
                gain.AddParticleFXParent(gameEntity.linkedView.Value);
                gain.AddPlayParticleFX("PSVFX_ShieldGain");
                gain.AddParticleFXStartColor(
                        _configuration.BalloonColor(gameEntity.balloonColor.Value));
            }
            
            if (gameEntity.projectileBounceShield.Value < _counter)
            {
                // play particle fx
                var e = _contexts.game.CreateEntity();
                e.AddParticleFXParent(gameEntity.linkedView.Value);
                e.AddPlayParticleFX("PSVFX_ShieldLose");

                if (gameEntity.hasBalloonColor)
                {
                    e.AddParticleFXStartColor(_configuration.BalloonColor(gameEntity.balloonColor.Value));
                }
            }

            _counter = gameEntity.projectileBounceShield.Value;
        }
    }
}