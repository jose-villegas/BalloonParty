using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     How far a bomb's blast shakes the meadow's mushrooms. Authored on the mushroom settings
    ///     (<c>IMushroomSettings.Blast</c>). Which caps a bomb takes off is not a number here: it is the bomb's own
    ///     rule for what it breaks outright (<c>BombBlast.Pierces</c>).
    /// </summary>
    [Serializable]
    internal struct MushroomBlastTuning
    {
        [Tooltip("How far a bomb sways the mushrooms whose caps it leaves on, as a share of the bomb's kill " +
            "radius. 1 is the blast's own reach, the one it has for balloons: a mushroom sways when its cap " +
            "overlaps the kill radius. More widens the sway; 0 turns it off.")]
        [SerializeField] [Min(0f)] private float _swayShare;

        /// <summary>The share of a bomb's kill radius its sway reaches to. Never under 0.</summary>
        public float SwayShare => Mathf.Max(0f, _swayShare);

        internal MushroomBlastTuning(float swayShare)
        {
            _swayShare = swayShare;
        }
    }

    /// <summary>
    ///     The sway a bomb's blast gives the mushrooms it reaches without taking their caps off
    ///     (@ref plan_meadow_mushrooms). Plain arithmetic, so the reach and the strength can be held in tests.
    /// </summary>
    internal static class MushroomBlastRule
    {
        /// <summary>
        ///     How far from a bomb's centre a mushroom's centre may be and still sway: the kill radius times the
        ///     sway's share, and the cap's own radius, since the blast reaches a cap as it reaches a balloon, when
        ///     the two circles overlap. 0 when the share turns the sway off.
        /// </summary>
        internal static float SwayReach(float killRadius, float capRadius, in MushroomBlastTuning tuning)
        {
            var share = tuning.SwayShare;
            return share > 0f ? (killRadius * share) + Mathf.Max(0f, capRadius) : 0f;
        }

        /// <summary>
        ///     How hard a mushroom <paramref name="distance" /> from the blast sways, 0 to 1: 1 at
        ///     <paramref name="nearest" />, the distance of the slots next to the bomb (where a cap is taken off,
        ///     so no swaying cap stands nearer), falling in a straight line to 0 at <paramref name="reach" />. 0
        ///     at the reach and past it.
        /// </summary>
        internal static float SwayStrength(float distance, float nearest, float reach)
        {
            if (distance >= reach)
            {
                return 0f;
            }

            return reach > nearest ? Mathf.Clamp01((reach - distance) / (reach - nearest)) : 1f;
        }

        /// <summary>The way a blast at <paramref name="blast" /> pushes a mushroom at <paramref name="cap" />: the
        /// unit direction from the one to the other, and straight up when the two are the same point.</summary>
        internal static Vector2 Away(Vector2 blast, Vector2 cap)
        {
            var away = cap - blast;
            return away.sqrMagnitude > 1e-10f ? away.normalized : Vector2.up;
        }
    }
}
