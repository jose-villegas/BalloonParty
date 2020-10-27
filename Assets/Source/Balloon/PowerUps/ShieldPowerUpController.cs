public class ShieldPowerUpController : BalloonPowerUpController
{
    public override void Activate()
    {
        foreach (var projectile in _freeProjectiles)
        {
            var shield = projectile.projectileBounceShield.Value;
            projectile.ReplaceProjectileBounceShield(shield + 1);
        }
                    
        // play particle fx
        var gain = _contexts.game.CreateEntity();
        gain.AddPosition(_gameEntity.position.Value);
        gain.AddParticleFXStartColor(_configuration.BalloonColor(_gameEntity.balloonColor.Value));
        gain.AddPlayParticleFX("PSVFX_ShieldGainPU");
    }
}