using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Core.PathCore;
using DG.Tweening.Plugins.Options;
using Entitas;
using UnityEngine;

public class NewBalloonLinesInstanceSystem : ReactiveSystem<GameEntity>
{
    private readonly Contexts _contexts;
    private readonly IGameConfiguration _configuration;
    private readonly IEntity[,] _slots;
    private IGroup<GameEntity> _freeProjectiles;

    public NewBalloonLinesInstanceSystem(Contexts contexts) : base(contexts.game)
    {
        _contexts = contexts;
        _configuration = _contexts.configuration.gameConfiguration.value;
        _slots = _contexts.game.slotsIndexer.Value;

        _freeProjectiles =
            _contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.FreeProjectile));
    }

    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(GameMatcher.GameTurnCounter);
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.hasGameTurnCounter && entity.gameTurnCounter.Value > 1;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        // mark all previous balloons as old
        var balloons = _contexts.game.GetGroup(GameMatcher.Balloon);

        foreach (var balloon in balloons)
        {
            balloon.isNewBalloon = false;
        }
        
        // create new lines
        var coroutineRunner = _contexts.game.coroutineRunner.Value;
        coroutineRunner.StartCoroutine(InstanceBalloonLines());
    }
    
    private IEnumerator InstanceBalloonLines()
    {
        for (int i = 0; i < _configuration.NewProjectileBalloonLines; i++)
        {
            var e = _contexts.game.CreateEntity();
            e.isBalloonLineInstanceEvent = true;
            yield return new WaitForSeconds(_configuration.NewBalloonLinesTimeInterval);
        }
        
        yield return new WaitForEndOfFrame();
        
        // check if new balloons have a power up
        var p = _contexts.game.CreateEntity();
        p.isBalloonsPowerUpCheckEvent = true;
        
        yield return new WaitForEndOfFrame();
        
        // check if balloons can be moved to re-balance
        var b = _contexts.game.CreateEntity();
        b.isBalloonsBalanceEvent = true;
    }
}