using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Blasts reaching the meadow's mushrooms (@ref plan_meadow_mushrooms): what the bomb and the nuke tell
    ///     when they go off, so that neither has to know what a mushroom is. Implemented by
    ///     <c>Item/Bomb/MushroomBlast</c>.
    /// </summary>
    internal interface IMushroomBlast
    {
        /// <summary>
        ///     A bomb went off: the one that stood in <paramref name="bombSlot" />, at
        ///     <paramref name="centre" />, with <paramref name="killRadius" />. The whole caps it would break an
        ///     unbreakable in the place of are blown off (the slots next to it; for a rainbow bomb, the centres
        ///     within its kill radius), and the other whole caps its blast reaches sway. Nothing on a board
        ///     without mushrooms.
        /// </summary>
        void Bomb(Vector2 centre, Vector2Int bombSlot, bool isRainbow, float killRadius);

        /// <summary>
        ///     A blast centred at <paramref name="centre" /> that spares no cap (the nuke's) has reached the
        ///     mushroom in <paramref name="slot" />: its cap, if it is whole, is blown off. Nothing for a slot
        ///     that holds no whole cap.
        /// </summary>
        void BlowOff(Vector2Int slot, Vector2 centre);
    }
}
