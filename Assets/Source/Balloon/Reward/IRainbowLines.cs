using System.Collections.Generic;
using BalloonParty.Balloon.Model;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     The clock lines a disco ball's pop owes rainbow (PLAN-DiscoBalloon). The spawner hands each clock line's new
    ///     balloons over once they are placed.
    /// </summary>
    internal interface IRainbowLines
    {
        /// <summary>
        ///     Paints the balloons of <paramref name="spawned" /> from <paramref name="from" /> on rainbow while lines are
        ///     owed, and counts the line paid if it brought any balloon.
        /// </summary>
        void PaintLine(IReadOnlyList<IBalloonModel> spawned, int from);
    }
}
