using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Balloon
{
    /// <summary>
    ///     Per-type contact radius, read once off the authored prefabs.
    /// </summary>
    /// <remarks>
    ///     The live deflector field takes radii from the spawned views, which is right for a shot in
    ///     flight. Planning happens before the views exist — or against balloons still animating in —
    ///     so it needs the authored value instead. Reading it here rather than approximating from the
    ///     slot pitch matters because the types genuinely differ: the ordinary balloon, the toughs and
    ///     a soap cluster are three different circles, and a planner that assumed one would place
    ///     shields a shot grazes past.
    /// </remarks>
    internal sealed class BalloonContactRadii
    {
        private readonly Dictionary<BalloonType, float> _byType = new();
        private readonly float _fallback;

        internal BalloonContactRadii(IBalloonsConfiguration configuration, float fallback)
        {
            _fallback = fallback;
            if (configuration?.Entries == null)
            {
                return;
            }

            foreach (var entry in configuration.Entries)
            {
                if (entry?.Prefab == null)
                {
                    continue;
                }

                var radius = entry.Prefab.ContactRadius;
                if (radius > 0f)
                {
                    _byType[entry.BalloonType] = radius;
                }
            }
        }

        /// <summary>The authored radius, or the fallback for a type with no usable collider.</summary>
        internal float For(BalloonType type)
        {
            return _byType.TryGetValue(type, out var radius) ? radius : _fallback;
        }
    }
}
