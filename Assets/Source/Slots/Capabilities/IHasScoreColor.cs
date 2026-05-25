using System.Collections.Generic;

namespace BalloonParty.Slots.Capabilities
{
    public interface IHasScoreColor
    {
        void ResolveScoreAttribution(in DamageContext context, IList<ScoreAttribution> results);
    }
}

