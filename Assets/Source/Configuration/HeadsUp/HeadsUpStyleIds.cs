using System;

namespace BalloonParty.Configuration.HeadsUp
{
    /// <summary>The size of a table indexed by <see cref="HeadsUpStyleId" />.</summary>
    internal static class HeadsUpStyleIds
    {
        // The highest id plus one, not the number of ids: a retired id leaves a gap, never a renumbering.
        internal static readonly int Count = HighestId() + 1;

        private static int HighestId()
        {
            var highest = 0;
            foreach (HeadsUpStyleId id in Enum.GetValues(typeof(HeadsUpStyleId)))
            {
                highest = Math.Max(highest, (int)id);
            }

            return highest;
        }
    }
}
