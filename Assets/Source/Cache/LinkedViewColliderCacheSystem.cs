using System.Collections.Generic;
using Entitas;
using UnityEngine;


public class BallonsLinkedViewColliderCacheSystem : ReactiveSystem<GameEntity>
{
    private readonly Contexts _contexts;
    private LinkedViewColliderCacheComponent _cache;

    public BallonsLinkedViewColliderCacheSystem(Contexts contexts) : base(contexts.game)
    {
        _contexts = contexts;

        // create cache entity
        var entity = _contexts.game.CreateEntity();
        entity.isLinkedViewColliderCache = true;
        _cache = entity.GetComponent(GameComponentsLookup.LinkedViewColliderCache) as LinkedViewColliderCacheComponent;
    }

    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(GameMatcher.AllOf(GameMatcher.LinkedView, GameMatcher.NewBalloon));
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.isNewBalloon && entity.hasLinkedView;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        foreach (var e in entities)
        {
            var linkedView = e.linkedView.Value as LinkedViewController;

            if (linkedView != null) {
                // extract collider
                var collider = linkedView.GetComponent<Collider2D>();

                if (collider != null) {
                    // cache the collider
                    _cache.Cache(linkedView, collider);
                }
            }
        }
    }
}       