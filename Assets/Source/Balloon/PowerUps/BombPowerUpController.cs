﻿public class BombPowerUpController : BalloonPowerUpController
{
    public override void Activate()
    {
        var e = _contexts.game.CreateEntity();
        e.AddAsset("BombRange");
        e.AddPosition(_gameEntity.position.Value);
        e.isBalloonCollider = transform;
        
        // mark power up as consumed
        _gameEntity.isBalloonPowerUpActivated = true;
    }
}