using System.Collections.Generic;
using BalloonParty.Configuration.GridActors;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The bounce with a forgiving kick: a ball leaving a mushroom close enough to the line of another one is
    ///     bent onto it and landed where its next bounce carries the chain on, so a chain needs a good first hit
    ///     and not a perfect flight. The geometry is <see cref="BumperKick" />; this reads the mushroom settings
    ///     for it on every call, so a retune takes hold at once.
    /// </summary>
    internal sealed class ForgivingKickRule : IBumperBounceRule
    {
        private readonly IMushroomSettings _settings;

        internal ForgivingKickRule(IMushroomSettings settings)
        {
            _settings = settings;
        }

        public Vector2 Resolve(
            in BumperContact contact, IReadOnlyList<BumperCircle> bumpers, in MushroomChainState chain)
        {
            if (!_settings.ForgivingKick)
            {
                return TrueReflectRule.Reflect(contact);
            }

            return BumperKick.Resolve(contact, bumpers, chain, _settings.Rules.ChainTarget, _settings.Kick);
        }
    }
}
