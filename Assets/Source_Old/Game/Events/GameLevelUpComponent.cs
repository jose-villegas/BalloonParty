using Entitas;
using Entitas.CodeGeneration.Attributes;

[Event(EventTarget.Any)]
public sealed class GameLevelUpComponent : IComponent
{
    public int Value;
}