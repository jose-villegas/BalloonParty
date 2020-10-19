using Entitas;
using Entitas.CodeGeneration.Attributes;

[Unique, Event(EventTarget.Any)]
public class GameTurnCounterComponent : IComponent
{
    public int Value;
}