using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Core.PathCore;
using DG.Tweening.Plugins.Options;
using Entitas;
using UnityEngine;

public class BalanceBalloonsSystem : ReactiveSystem<GameEntity>
{
    private readonly Contexts _contexts;
    private readonly IGameConfiguration _configuration;
    private readonly IEntity[,] _slots;
    private IGroup<GameEntity> _freeProjectiles;

    public BalanceBalloonsSystem(Contexts contexts) : base(contexts.game)
    {
        _contexts = contexts;
        _configuration = _contexts.configuration.gameConfiguration.value;
        _slots = _contexts.game.slotsIndexer.Value;

        _freeProjectiles =
            _contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.FreeProjectile));
    }

    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(GameMatcher.BalloonsBalanceEvent);
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.isBalloonsBalanceEvent;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        BalanceBalloons();
    }

    private void BalanceBalloons()
    {
        var hasUnbalanced = true;
        var paths = new Dictionary<GameEntity, List<Vector3>>();

        while (hasUnbalanced)
        {
            hasUnbalanced = false;

            for (int i = 0; i < _slots.GetLength(0); i++)
            {
                for (int j = _slots.GetLength(1) - 1; j >= 0; j--)
                {
                    if (_slots.IsEmpty(i, j)) continue;
                    if (!(_slots[i, j] is GameEntity balloonEntity)) continue;
                    if (!_slots.IsUnbalanced(i, j)) continue;

                    var nextSlot = _slots.OptimalNextEmptySlot(i, j);

                    if (!nextSlot.HasValue) continue;

                    hasUnbalanced = true;

                    // swap index values
                    _slots[i, j] = null;
                    _slots[nextSlot.Value.x, nextSlot.Value.y] = balloonEntity;
                    balloonEntity.ReplaceSlotIndex(nextSlot.Value);

                    // is unstable until it reaches its position
                    balloonEntity.isStableBalloon = false;

                    // save to movement path animation
                    if (paths.TryGetValue(balloonEntity, out var path))
                    {
                        path.Add(nextSlot.Value.IndexToPosition(_configuration));
                    }
                    else
                    {
                        paths.Add(balloonEntity, new List<Vector3>()
                        {
                            nextSlot.Value.IndexToPosition(_configuration)
                        });
                    }
                }
            }
        }

        HandlePathTween(paths);
    }

    private void HandlePathTween(Dictionary<GameEntity, List<Vector3>> paths)
    {
        foreach (var path in paths)
        {
            var mono = path.Key.linkedView.Value as MonoBehaviour;

            if (mono != null)
            {
                var entity = path.Key;
                var pathList = path.Value;

                if (entity.hasTweenSequence)
                {
                    if (!entity.tweenSequence.Value.IsComplete())
                    {
                        entity.tweenSequence.Value.Append(DeclareTween(mono, pathList, entity));
                    }
                    else
                    {
                        entity.RemoveTweenSequence();
                        DeclareTween(mono, pathList, entity);
                    }
                }
                else
                {
                    DeclareTween(mono, pathList, entity);
                }
            }
        }
    }

    private TweenerCore<Vector3, Path, PathOptions> DeclareTween(MonoBehaviour mono, List<Vector3> path, GameEntity entity)
    {
        TweenerCore<Vector3, Path, PathOptions> tween;
        tween = mono.transform.DOPath(path.ToArray(), _configuration.TimeForBalloonsBalance,
            PathType.CatmullRom);

        tween.onUpdate += () => { entity.ReplacePosition(mono.transform.position); };
        tween.onComplete += () => { entity.isStableBalloon = true; };

        return tween;
    }
}