using Entitas;
using Entitas.CodeGeneration.Attributes;

[Event(EventTarget.Any)]
public class GameLevelProgressComponent : IComponent
{
    public string Name;
    public int Current;
}