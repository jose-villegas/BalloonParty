#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace BalloonParty.Prediction
{
    /// <summary>
    ///     Pure-logic calculator that produces a list of world-space points representing
    ///     the projectile's predicted trajectory, including wall bounces.
    /// </summary>
    public class PredictionTraceCalculator
    {
        private readonly IGameConfiguration _config;

        public PredictionTraceCalculator(IGameConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        ///     Calculates the prediction trace from <paramref name="origin" /> in the given
        ///     <paramref name="direction" />, bouncing off the left/right/top walls defined
        ///     by <see cref="IGameConfiguration.LimitsClockwise" />.
        /// </summary>
        /// <param name="origin">Starting world position.</param>
        /// <param name="direction">Normalized direction vector.</param>
        /// <param name="results">Reusable list that will be cleared and filled with trace points.</param>
        public void Calculate(Vector3 origin, Vector3 direction, List<Vector3> results)
        {
            results.Clear();
            results.Add(origin);

            var limits = _config.LimitsClockwise;
            var stepsLeft = _config.PredictionTraceMaxSteps;
            var maxBounces = _config.PredictionTraceMaxBounces;

            while (stepsLeft > 0 && maxBounces > 0)
            {
                var shift = _config.PredictionTraceStep;
                var extended = origin + (direction * shift);
                var reflect = Vector3.zero;

                if (extended.x > limits.y)
                {
                    reflect += Vector3.left;
                    shift = (limits.y - origin.x) / direction.x;
                    extended = origin + (direction * shift);
                }

                if (extended.x < limits.w)
                {
                    reflect += Vector3.right;
                    shift = (limits.w - origin.x) / direction.x;
                    extended = origin + (direction * shift);
                }

                if (extended.y > limits.x)
                {
                    reflect += Vector3.down;
                    shift = (limits.x - origin.y) / direction.y;
                    extended = origin + (direction * shift);
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
        }
    }
}
