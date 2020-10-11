using Entitas;
using Entitas.CodeGeneration.Attributes;

[Unique, Event(EventTarget.Any)]
public class GameLevelComponent : IComponent
{
    public int Value;
}