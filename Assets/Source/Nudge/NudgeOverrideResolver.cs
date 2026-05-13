using System.Linq;

namespace BalloonParty.Nudge
{
    public static class NudgeOverrideResolver
    {
        /// <summary>
        /// Finds the first override whose AppliesTo flags include the given source,
        /// then falls through to the per-message override, then the global config default.
        /// </summary>
        public static float ResolveDistance(
            NudgeOverride[] overrides,
            NudgeType source,
            float? messageOverride,
            float configDefault)
        {
            var entry = overrides?.FirstOrDefault(o => o.AppliesTo.HasFlag(source));
            return entry != null ? entry.Distance : messageOverride ?? configDefault;
        }

        public static float ResolveDuration(
            NudgeOverride[] overrides,
            NudgeType source,
            float? messageOverride,
            float configDefault)
        {
            var entry = overrides?.FirstOrDefault(o => o.AppliesTo.HasFlag(source));
            return entry != null ? entry.Duration : messageOverride ?? configDefault;
        }

        public static float ResolveFalloff(
            NudgeOverride[] overrides,
            NudgeType source,
            float configDefault)
        {
            var entry = overrides?.FirstOrDefault(o => o.AppliesTo.HasFlag(source));
            return entry != null && entry.Falloff > 0f ? entry.Falloff : configDefault;
        }
    }
}

