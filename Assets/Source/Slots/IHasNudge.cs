using BalloonParty.Nudge;

namespace BalloonParty.Slots
{
    public interface IHasNudge
    {
        /// <summary>Per-type nudge overrides. Empty/null = use global config defaults.</summary>
        NudgeOverride[] NudgeOverrides { get; }
    }
}

