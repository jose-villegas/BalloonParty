using System.Collections.Generic;
using Entitas;
using Entitas.Unity;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class TraceDrawerController : EntityLinkerController, IPredictionTraceListener
{
    private LineRenderer _lineRenderer;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
    }

    protected override void DefineEntity(IEntity e)
    {
        base.DefineEntity(e);

        var gameEntity = e as GameEntity;
        
        gameEntity.AddPredictionTrace(new List<Vector3>());
        // listen to modifications
        gameEntity.AddPredictionTraceListener(this);
    }

    public void OnPredictionTrace(GameEntity entity, List<Vector3> values)
    {
        if (values == null || values.Count == 0)
        {
            _lineRenderer.positionCount = 0;
            return;
        }

        _lineRenderer.positionCount = values.Count;
        _lineRenderer.SetPositions(values.ToArray());
    }
}