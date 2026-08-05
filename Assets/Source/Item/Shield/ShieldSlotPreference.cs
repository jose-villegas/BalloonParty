using BalloonParty.Configuration.Items;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Item.Shield
{
    /// <summary>
    ///     Steers shield-carrying balloons toward slots a shot can still reach, so a planned chain
    ///     degrades gracefully as the board settles instead of quietly becoming uncollectable.
    /// </summary>
    /// <remarks>
    ///     A nudge, not a rule. It is added to the actor's own balance bias rather than replacing it,
    ///     and it is small enough that support and pressure still decide the move — a shield should
    ///     not make a balloon hover in defiance of the board.
    /// </remarks>
    internal sealed class ShieldSlotPreference : IShieldSlotPreference
    {
        private readonly ShieldReachabilityField _field;
        private readonly IShieldChainSettings _settings;

        internal ShieldSlotPreference(ShieldReachabilityField field, IShieldChainSettings settings)
        {
            _field = field;
            _settings = settings;
        }

        public int WeightFor(ISlotActor actor, Vector2Int candidate)
        {
            if (actor is not IHasItemSlot host || host.Item.Value != ItemType.Shield)
            {
                return 0;
            }

            var reflections = _field.ReflectionsToReach(candidate);
            if (reflections == ShieldReachabilityField.Unreachable)
            {
                return 0;
            }

            // Straight-shot slots score highest and each reflection costs, so a shield drifts toward
            // the shots a player can actually see — the same reason the planner prefers walls.
            return Mathf.Max(
                0, _settings.ReachableSlotBonus - (reflections * _settings.PerReflectionPenalty));
        }
    }
}
