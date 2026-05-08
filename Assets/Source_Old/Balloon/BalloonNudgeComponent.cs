using Entitas;
using Entitas.CodeGeneration.Attributes;

[Game, Event(EventTarget.Self)]
public class BalloonNudgeComponent : IComponent
{
    public float Duration;
    public float Distance;
}