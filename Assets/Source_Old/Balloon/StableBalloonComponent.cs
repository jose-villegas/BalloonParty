using Entitas;
using Entitas.CodeGeneration.Attributes;

[Game, Event(EventTarget.Self, EventType.Added), Event(EventTarget.Self, EventType.Removed)]
public sealed class StableBalloonComponent : IComponent
{
}