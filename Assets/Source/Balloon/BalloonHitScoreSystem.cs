using System.Collections.Generic;
using DG.Tweening;
using Entitas;
using UnityEngine;

public class BalloonHitScoreSystem : ReactiveSystem<GameEntity>
{
    private readonly Contexts _contexts;
    private readonly IGameConfiguration _configuration;
    private IEntity[,] _slots;

    public BalloonHitScoreSystem(Contexts contexts) : base(contexts.game)
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
            // create balloon pop effect
            var e = _contexts.game.CreateEntity();
            e.AddPosition(balloonEntity.position.Value);
            e.AddParticleFXStartColor(_configuration.BalloonColor(balloonEntity.balloonColor.Value));
            e.AddPlayParticleFX("PSVFX_BalloonPop");

            // add score
            _contexts.game.AddScore(balloonEntity.balloonColor.Value, out var progress);
            // save position to know where this point comes from
            progress.ReplacePosition(balloonEntity.position.Value);
        }
    }
}