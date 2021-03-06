﻿using System.Collections.Generic;
using DG.Tweening;
using Entitas;
using UnityEngine;

public class BalloonHitNudgeAnimationSystem : ReactiveSystem<GameEntity>
{
    private readonly Contexts _contexts;
    private readonly IGameConfiguration _configuration;
    private readonly IEntity[,] _slots;

    public BalloonHitNudgeAnimationSystem(Contexts contexts) : base(contexts.game)
    {
        _contexts = contexts;
        _configuration = contexts.configuration.gameConfiguration.value;
        _slots = _contexts.game.slotsIndexer.Value;
    }

    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(GameMatcher.AllOf(GameMatcher.Balloon, GameMatcher.BalloonNudge));
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.isBalloon && entity.isBalloonHit && entity.hasBalloonNudge && entity.hasSlotIndex;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        foreach (var balloonEntity in entities)
        {
            // operate on indexing value
            var index = balloonEntity.slotIndex.Value;
            
            // start nudge animation from explosion on neighboring slots
            var distance = balloonEntity.balloonNudge.Distance;
            var duration = balloonEntity.balloonNudge.Duration;
            
            NudgeNeighbors(index, balloonEntity, distance, duration);
            // remove from indexer
            _slots[index.x, index.y] = null;
        }
    }

    private void NudgeNeighbors(Vector2Int index, GameEntity balloonEntity, float distance, float duration)
    {
        var neighbors = _slots.GetNeighbors(index.x, index.y);

        foreach (var neighbor in neighbors)
        {
            if (neighbor is GameEntity neighborEntity)
            {
                var mono = neighborEntity.linkedView.Value as MonoBehaviour;

                if (mono != null)
                {
                    var position = neighborEntity.position.Value;
                    var direction = neighborEntity.position.Value - balloonEntity.position.Value;
                    var slotIndex = neighborEntity.slotIndex.Value;
                    var sequence = DOTween.Sequence();

                    sequence.Append(mono.transform.DOMove(position + direction.normalized * distance, duration / 2f));
                    sequence.Append(mono.transform.DOMove(slotIndex.IndexToPosition(_configuration), duration / 2f));
                    neighborEntity.isStableBalloon = false;
                    sequence.onComplete += () => { neighborEntity.isStableBalloon = true; };

                    // add tweenSequence to entity to avoid colliding with another tween
                    neighborEntity.ReplaceTweenSequence(sequence);
                }
            }
        }
    }
}