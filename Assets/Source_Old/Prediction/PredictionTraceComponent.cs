using System.Collections.Generic;
using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;

[Game, Unique, Event(EventTarget.Self)]
public class PredictionTraceComponent : IComponent
{
    public List<Vector3> Values;
}

