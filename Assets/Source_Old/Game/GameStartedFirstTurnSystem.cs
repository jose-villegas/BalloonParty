using System.Collections.Generic;
using Entitas;

public class GameStartedFirstTurnSystem : ReactiveSystem<GameEntity>
{
    private Contexts _contexts;

    public GameStartedFirstTurnSystem(Contexts contexts) : base(contexts.game)
    {
        _contexts = contexts;
    }
    
    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(GameMatcher.AllOf(GameMatcher.GameEvent, GameMatcher.GameStarted));
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.isGameEvent && entity.isGameStarted;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        _contexts.game.ReplaceGameTurnCounter(1);
    }
}