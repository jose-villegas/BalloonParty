using Entitas;
using Entitas.CodeGeneration.Attributes;

[Event(EventTarget.Any)]
public class GameScoreComponent : IComponent
{
    public string Name;
    public int Score;
}