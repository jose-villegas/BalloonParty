using System.Linq;
using BalloonParty.Configuration;

namespace BalloonParty.Nudge
{
    public class NudgeOverrideResolver
    {
        private readonly BalloonsConfiguration _config;

        public NudgeOverrideResolver(BalloonsConfiguration config)
        {
            _config = config;
        }

        public float ResolveDistance(
            NudgeOverride[] balloonOverrides,
            NudgeOverride[] publisherOverrides,
            NudgeType source)
        {
            var entry = FindOverride(balloonOverrides, source);
            if (entry != null)
            {
                return entry.Distance;
            }

            var pubEntry = FindOverride(publisherOverrides, source);
            if (pubEntry != null)
            {
                return pubEntry.Distance;
            }

            return _config.NudgeDistance;
        }

        public float ResolveDuration(
            NudgeOverride[] balloonOverrides,
            NudgeOverride[] publisherOverrides,
            NudgeType source)
        {
            var entry = FindOverride(balloonOverrides, source);
            if (entry != null)
            {
                return entry.Duration;
            }

            var pubEntry = FindOverride(publisherOverrides, source);
            if (pubEntry != null)
            {
                return pubEntry.Duration;
            }

            return _config.NudgeDuration;
        }

        public float ResolveFalloff(NudgeOverride[] overrides, NudgeType source)
        {
            var entry = FindOverride(overrides, source);
            return entry != null ? entry.Falloff : _config.NudgeFalloff;
        }

        internal static NudgeOverride FindOverride(NudgeOverride[] overrides, NudgeType source)
        {
            return overrides?.FirstOrDefault(o => o.AppliesTo.HasFlag(source));
        }
    }
}
