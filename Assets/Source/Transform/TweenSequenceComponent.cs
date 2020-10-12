using DG.Tweening;
using Entitas;
using Entitas.CodeGeneration.Attributes;

[Event(EventTarget.Self)]
public sealed class TweenSequenceComponent : IComponent
{
    public Sequence Value;
}