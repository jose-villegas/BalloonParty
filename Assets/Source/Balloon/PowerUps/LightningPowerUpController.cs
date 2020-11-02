using System.Linq;
using UnityEngine;

public class LightningPowerUpController : BalloonPowerUpController
{
    [SerializeField] private ChainLightning _chainLightningPrefab;
    private ChainLightning _chainLightning;

    public override void Activate()
    {
        // setup chain lighting drawer
        var color = _gameEntity.balloonColor.Value;

        var balloons = _contexts.game.GetGroup(GameMatcher.Balloon);
        var targets = balloons.GetEntities().Where(x => x.balloonColor.Value == color).ToList();
        targets.Sort(Comparison);
        
        // forward to chain lightning effect handle
        if (_chainLightning == null)
        {
            _chainLightning = Instantiate(_chainLightningPrefab);
        }

        _chainLightning.Display(targets);
        // mark power up as consumed
        _gameEntity.isBalloonPowerUpActivated = true;
    }

    private int Comparison(GameEntity x, GameEntity y)
    {
        var origin = _gameEntity.position.Value;
        var d1 = Vector3.Distance(x.position.Value, origin);
        var d2 = Vector3.Distance(y.position.Value, origin);
        return d1.CompareTo(d2);
    }
}