using BalloonParty.Balloon.Type;

namespace BalloonParty.Shared.Extensions
{
    internal static class BalloonTypeExtensions
    {
        /// <summary>The plain-balloon skins that behave identically to <see cref="BalloonType.Simple" /> (see <see cref="BalloonType" />'s silver/gold note) — the only types extra pop-spawns may use.</summary>
        internal static bool IsSimpleFamily(this BalloonType type)
        {
            return type is BalloonType.Simple or BalloonType.SimpleSilver or BalloonType.SimpleGold;
        }
    }
}
