using System.Collections.Generic;
namespace BalloonParty.Game.Level
{
    internal static class LevelCompletion
    {
        /// <summary>
        ///     How much of the level is done, 0 to 1: each colour's confirmed points, capped at the goal, over all the
        ///     goals. A colour past its goal can't make up for one behind, so 1 is exactly when every goal is met.
        /// </summary>
        internal static float Of(ILevelProgress progress, IReadOnlyList<string> colors)
        {
            var required = progress.GetRequiredPoints();
            if (required <= 0 || colors == null || colors.Count == 0)
            {
                return 0f;
            }

            long done = 0;
            for (var i = 0; i < colors.Count; i++)
            {
                var points = progress.GetProgress(colors[i]);
                done += points < 0 ? 0 : points > required ? required : points;
            }

            return done / ((float)required * colors.Count);
        }
    }
}
