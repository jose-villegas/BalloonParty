using System.Collections.Generic;
using Entitas;
using UnityEngine;

public class ProjectilePredictionTraceSystem : IExecuteSystem
{
    private readonly Contexts _contexts;
    private readonly IGameConfiguration _configuration;
    private readonly IGroup<GameEntity> _projectiles;

    public ProjectilePredictionTraceSystem(Contexts contexts)
    {
        _contexts = contexts;
        _configuration = _contexts.configuration.gameConfiguration.value;

        _projectiles =
            _contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.Projectile, GameMatcher.Movable,
                GameMatcher.Direction));
    }

    public void Execute()
    {
        if (_projectiles.count <= 0 || _projectiles.count > 1) return;

        var projectile = _projectiles.GetEntities()[0];

        // free projectiles don't get a trace
        if (projectile.isFreeProjectile || !Input.GetMouseButton(0))
        {
            _contexts.game.ReplacePredictionTrace(null);
            return;
        }

        var direction = projectile.direction.Value;
        var origin = projectile.position.Value;
        var limits = _configuration.LimitsClockwise;

        var stepsLeft = _configuration.PredictionTraceMaxSteps;
        var results = new List<Vector3>() { origin };
        int maxBounces = _configuration.PredictionTraceMaxBounces;
        
        while (stepsLeft > 0 && maxBounces > 0)
        {
            var shift = _configuration.PredictionTraceStep;
            var extended = origin + direction * shift;
            var reflect = Vector3.zero;

            // collided with right limit
            if (extended.x > limits.y)
            {
                reflect += Vector3.left;
                shift = (_configuration.LimitsClockwise.y - origin.x) / direction.x;
                extended = CalculateExtended(shift, origin, direction);
            }
            
            // collided with left limit
            if (extended.x < limits.w)
            {
                reflect += Vector3.right;
                shift = (_configuration.LimitsClockwise.w - origin.x) / direction.x;
                extended = CalculateExtended(shift, origin, direction);
            }
            
            // collided with top limit
            if (extended.y > limits.x)
            {
                reflect += Vector3.down;
                shift = (_configuration.LimitsClockwise.x - origin.y) / direction.y;
                extended = CalculateExtended(shift, origin, direction);
                
                // no bounce trace for top limit
                maxBounces = 0;
            }
            
            origin = extended;
            stepsLeft--;

            if (reflect != Vector3.zero)
            {
                results.Add(extended);
                direction = Vector2.Reflect(direction, reflect.normalized);
                maxBounces--;
            }
        }

        _contexts.game.ReplacePredictionTrace(results);
    }

    private static Vector3 CalculateExtended(float shift, Vector3 origin, Vector3 direction)
    {
        return origin + direction * shift;;
    }
}