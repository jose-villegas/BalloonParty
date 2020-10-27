using System.Collections.Generic;
using Entitas;

public class ProjectileShieldSystem : ReactiveSystem<GameEntity>
{
    private readonly Contexts _contexts;
    private readonly IGameConfiguration _configuration;

    public ProjectileShieldSystem(Contexts contexts) : base(contexts.game)
    {
        _contexts = contexts;
        _configuration = contexts.configuration.gameConfiguration.value;
    }

    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(GameMatcher.AllOf(GameMatcher.BalloonLastColorPopCount,
            GameMatcher.FreeProjectile, GameMatcher.BalloonColor));
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.hasBalloonLastColorPopCount && entity.isFreeProjectile && entity.hasBalloonColor;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        foreach (var projectileEntity in entities)
        {
            if (projectileEntity.balloonLastColorPopCount.Value >= 3)
            {
                var shields = projectileEntity.projectileBounceShield.Value;
                projectileEntity.ReplaceProjectileBounceShield(shields + 1);
            }
        }
    }
}