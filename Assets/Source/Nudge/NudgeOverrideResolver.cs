using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration;

namespace BalloonParty.Nudge
{
    internal class NudgeOverrideResolver
    {
        private readonly BalloonsConfiguration _config;

        public NudgeOverrideResolver(BalloonsConfiguration config)
        {
            _config = config;
        }

        public float ResolveDistance(
            IReadOnlyList<NudgeOverride> balloonOverrides,
            IReadOnlyList<NudgeOverride> publisherOverrides,
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
            IReadOnlyList<NudgeOverride> balloonOverrides,
            IReadOnlyList<NudgeOverride> publisherOverrides,
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

        public float ResolveFalloff(IReadOnlyList<NudgeOverride> overrides, NudgeType source)
        {
            var entry = FindOverride(overrides, source);
            return entry != null ? entry.Falloff : _config.NudgeFalloff;
        }

        internal static NudgeOverride FindOverride(IReadOnlyList<NudgeOverride> overrides, NudgeType source)
        {
            return overrides?.FirstOrDefault(o => o.AppliesTo.HasFlag(source));
        }
    }
}
