using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;

[Event(EventTarget.Self)]
public sealed class TriggerStay2DComponent : IComponent
{
    public Collider2D Value;
}