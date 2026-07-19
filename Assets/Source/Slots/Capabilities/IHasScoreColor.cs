using System.Collections.Generic;

namespace BalloonParty.Slots.Capabilities
{
    public interface IHasScoreColor
    {
        /// <summary>
        ///     <paramref name="incompleteColors" /> lists the colours whose progress bar isn't full yet —
        ///     scatter implementers (Tough/BubbleCluster) confine their random split to these so completing
        ///     a colour never wastes points on it. Single-colour attributions ignore it.
        /// </summary>
        void ResolveScoreAttribution(
            in DamageContext context, IReadOnlyList<string> incompleteColors, IList<ScoreAttribution> results);
    }
}
