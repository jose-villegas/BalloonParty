using Entitas;
using Entitas.CodeGeneration.Attributes;

[Event(EventTarget.Any)]
public class GamePersistentScoreComponent : IComponent
{
    public string Name;
    public int Score;
}