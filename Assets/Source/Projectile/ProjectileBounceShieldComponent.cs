using Entitas;
using Entitas.CodeGeneration.Attributes;

[Game, Event(EventTarget.Any), Event(EventTarget.Self)]
public sealed class ProjectileBounceShieldComponent : IComponent
{
    public int Value;
}