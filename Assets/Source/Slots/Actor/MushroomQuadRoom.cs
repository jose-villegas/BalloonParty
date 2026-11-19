using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     How large the quad a mushroom is drawn on has to be. The shader draws the cap, its pieces and its
    ///     shadow from the quad's own position and never past the mesh's corner, so the mesh must hold the most
    ///     the mushroom can ever need at once: the cap at its largest, leaned and rocked its furthest, and its
    ///     shadow beside it. <c>MushroomPrefabBuilder</c> sizes the quad by this, and a test holds the shipped
    ///     quad to it, so a number raised without the prefab rebuilt is caught before the cap's edge is cut off
    ///     square.
    /// </summary>
    internal static class MushroomQuadRoom
    {
        /// <summary>The shadow's reach down-light of a cap on its stem, with its soft edge, in world units: the
        /// material's Down-Light Offset and Softness, with room to spare.</summary>
        internal const float ShadowReach = 0.2f;

        /// <summary>
        ///     Half the quad's side, in world units, for a cap of <paramref name="radius" /> whose material
        ///     swells it by <paramref name="pulseSwell" /> of its radius at a full pulse (the shader's Pulse Swell).
        /// </summary>
        internal static float Half(
            float radius, float pulseSwell, in MushroomLeanTuning lean, in MushroomIdleTuning idle,
            in MushroomCutTuning cut)
        {
            // The larger of a whole cap swollen, by a paying hit's pulse at the top of a breath, and a cut cap's
            // pieces flown, by the bound the shader boxes them with. A cap cut mid lean sends its pieces off from
            // where it leaned to, so the lean adds to either; the field's rocking adds to the hit's lean.
            var swollen = radius * (1f + Mathf.Max(0f, pulseSwell) + idle.Breath);
            var body = Mathf.Max(swollen, radius + cut.RoomBeyond(radius));

            // A cap a blast has lifted throws its shadow further off than one on its stem, and so does a cap at
            // the top of a breath. Never both: a cap in the air has left the stem, and a bare stem does not breathe.
            var shadow = ShadowReach * Mathf.Max(cut.ShadowLift, 1f + idle.ShadowLift);
            return body + lean.MaxLean + idle.RockMost + shadow;
        }
    }
}
