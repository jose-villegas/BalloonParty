using System.Collections.Generic;

namespace BalloonParty.Configuration.Level
{
    internal static class LevelSteps
    {
        /// <summary>
        ///     Index of the step in force at <paramref name="level" />: the one with the highest
        ///     <see cref="ILevelStep.FromLevel" /> at or below it, whatever the authored order. Before every
        ///     step's start, the earliest step applies. -1 when there are no steps.
        /// </summary>
        internal static int IndexAt<T>(IReadOnlyList<T> steps, int level) where T : ILevelStep
        {
            if (steps == null || steps.Count == 0)
            {
                return -1;
            }

            var inForce = -1;
            var earliest = 0;
            for (var i = 0; i < steps.Count; i++)
            {
                var from = steps[i].FromLevel;
                if (from <= level && (inForce < 0 || from >= steps[inForce].FromLevel))
                {
                    inForce = i;
                }

                if (from < steps[earliest].FromLevel)
                {
                    earliest = i;
                }
            }

            return inForce >= 0 ? inForce : earliest;
        }
    }
}
