using Entitas;
using Entitas.CodeGeneration.Attributes;

[Game, Event(EventTarget.Self)]
public sealed class BalloonPowerUpComponent : IComponent
{
    public BalloonPowerUp Value;
}

public enum BalloonPowerUp
{
    None,
    Shield,
    Bomb,
    Laser
}