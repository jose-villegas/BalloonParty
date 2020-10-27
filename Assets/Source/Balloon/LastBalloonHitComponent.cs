using Entitas;
using Entitas.CodeGeneration.Attributes;

[Game, Event(EventTarget.Self)]
public sealed class LastBalloonHitComponent : IComponent
{
    public IEntity Value;
}