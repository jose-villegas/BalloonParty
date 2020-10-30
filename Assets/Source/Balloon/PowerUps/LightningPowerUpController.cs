using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LightningPowerUpController : BalloonPowerUpController
{
    [SerializeField] private ChainLightning _chainLightningPrefab;
    
    public override void Activate()
    {
        var color = _gameEntity.balloonColor.Value;

        var balloons = _contexts.game.GetGroup(GameMatcher.Balloon);
        var targets = balloons.GetEntities().Where(x => x.balloonColor.Value == color).ToList();
        targets.Sort(Comparison);
        
        // forward to chain lightning effect handle
        var chainLightning = Instantiate(_chainLightningPrefab);
        chainLightning.Setup(targets);
        
        // mark power up as consumed
        _gameEntity.isBalloonPowerUpActivated = true;
    }

    private int Comparison(GameEntity x, GameEntity y)
    {
        var origin = _gameEntity.position.Value;
        var d1 = Vector3.Distance(x.position.Value, origin);
        var d2 = Vector3.Distance(x.position.Value, origin);
        return d1.CompareTo(d2);
    }
}