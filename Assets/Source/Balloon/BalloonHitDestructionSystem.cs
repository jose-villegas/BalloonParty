using System.Collections.Generic;
using Entitas;

public class BalloonHitDestructionSystem : ReactiveSystem<GameEntity>
{
    private readonly Contexts _contexts;
    private readonly IGameConfiguration _configuration;
    private IEntity[,] _slots;

    public BalloonHitDestructionSystem(Contexts contexts) : base(contexts.game)
    {
        _contexts = contexts;
        _configuration = contexts.configuration.gameConfiguration.value;
        _slots = _contexts.game.slotsIndexer.Value;
    }

    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(GameMatcher.AllOf(GameMatcher.Balloon, GameMatcher.BalloonHit));
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.isBalloon && entity.isBalloonHit;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        foreach (var balloonEntity in entities)
        {
            // operate on indexing value
            var index = balloonEntity.slotIndex.Value;
            // remove from indexer
            _slots[index.x, index.y] = null;
            balloonEntity.isDestroyed = true;
        }
    }
}