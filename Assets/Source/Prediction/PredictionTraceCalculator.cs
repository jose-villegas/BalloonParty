using System.Collections.Generic;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Prediction
{
    internal class PredictionTraceCalculator
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

            var walls = new WallLimits(_config.LimitsClockwise);
            var stepsLeft = _config.PredictionTraceMaxSteps;
            var maxBounces = _config.PredictionTraceMaxBounces;

            while (stepsLeft > 0 && maxBounces > 0)
            {
                var shift = _config.PredictionTraceStep;
                var extended = origin + (direction * shift);
                var reflect = Vector3.zero;

                if (extended.x > walls.Right)
                {
                    reflect += Vector3.left;
                    shift = (walls.Right - origin.x) / direction.x;
                    extended = origin + (direction * shift);
                }

                if (extended.x < walls.Left)
                {
                    reflect += Vector3.right;
                    shift = (walls.Left - origin.x) / direction.x;
                    extended = origin + (direction * shift);
                }

                if (extended.y > walls.Top)
                {
                    reflect += Vector3.down;
                    shift = (walls.Top - origin.y) / direction.y;
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
