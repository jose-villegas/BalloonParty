using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Nudge
{
    internal class NudgeOverrideResolver
    {
        private readonly IBalloonsConfiguration _config;

        public NudgeOverrideResolver(IBalloonsConfiguration config)
        {
            _config = config;
        }

        // Resolves both values in one pass to avoid walking the lists twice.
        public void Resolve(
            IReadOnlyList<NudgeOverride> balloonOverrides,
            IReadOnlyList<NudgeOverride> publisherOverrides,
            NudgeType source,
            out float distance,
            out float duration)
        {
            var entry = FindOverride(balloonOverrides, source) ?? FindOverride(publisherOverrides, source);
            distance = entry?.Distance ?? _config.NudgeDistance;
            duration = entry?.Duration ?? _config.NudgeDuration;
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

        // Avoids FirstOrDefault's per-call allocations; runs several times per hit.
        internal static NudgeOverride FindOverride(IReadOnlyList<NudgeOverride> overrides, NudgeType source)
        {
            if (overrides == null)
            {
                return null;
            }

            for (var i = 0; i < overrides.Count; i++)
            {
                if ((overrides[i].AppliesTo & source) == source)
                {
                    return overrides[i];
                }
            }

            return null;
        }
    }
}
