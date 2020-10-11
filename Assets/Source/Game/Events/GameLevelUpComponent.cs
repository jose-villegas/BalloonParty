using Entitas;
using Entitas.CodeGeneration.Attributes;

/// <summary>
/// This component indicates the game started
/// </summary>
[Event(EventTarget.Any)]
public sealed class GameLevelUpComponent : IComponent
{
}