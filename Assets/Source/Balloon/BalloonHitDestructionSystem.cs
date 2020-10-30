using System.Collections.Generic;
using Entitas;

public class BalloonHitDestructionSystem : IExecuteSystem
{
    private readonly Contexts _contexts;
    private readonly IGameConfiguration _configuration;
    private readonly IEntity[,] _slots;
    private readonly IGroup<GameEntity> _hits;

    public BalloonHitDestructionSystem(Contexts contexts)
    {
        _contexts = contexts;
        _configuration = contexts.configuration.gameConfiguration.value;
        _slots = _contexts.game.slotsIndexer.Value;
        _hits = _contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.Balloon, GameMatcher.BalloonHit));
    }

    public void Execute()
    {
        foreach (var hit in _hits.GetEntities())
        {
            // destruction conditions
            if (hit.isBalloonScoreReady)
            {
                if (hit.hasBalloonPowerUp && hit.isBalloonPowerUpActivated)
                {
                    DestroyBalloon(hit);
                }
                else if (!hit.hasBalloonPowerUp)
                {
                    DestroyBalloon(hit);
                }
            }
        }
    }

    private void DestroyBalloon(GameEntity hit)
    {
        // operate on indexing value
        var index = hit.slotIndex.Value;
        // remove from indexer
        _slots[index.x, index.y] = null;
        hit.isDestroyed = true;
    }
}