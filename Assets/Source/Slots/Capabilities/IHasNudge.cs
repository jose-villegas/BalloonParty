using System.Collections.Generic;
using BalloonParty.Nudge;

namespace BalloonParty.Slots.Capabilities
{
    public interface IHasNudge
    {
        /// <summary>Per-type nudge overrides. Empty/null = use global config defaults.</summary>
        IReadOnlyList<NudgeOverride> NudgeOverrides { get; }
    }
}
