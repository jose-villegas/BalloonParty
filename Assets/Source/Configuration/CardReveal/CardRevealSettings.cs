using UnityEngine;

namespace BalloonParty.Configuration.CardReveal
{
    /// <summary>
    ///     The card reveal's settings. Unassigned on the game scope, a default instance stands in, so the reveal
    ///     needs no asset until its pops are baked (<c>Plans/PLAN-CardReveal.md</c>).
    /// </summary>
    [CreateAssetMenu(menuName = "Configuration/Card Reveal Settings", fileName = "CardRevealSettings")]
    internal sealed class CardRevealSettings : ScriptableObject, ICardRevealSettings
    {
        [Tooltip("How many baked balloon pops there are to choose from.")]
        [SerializeField] [Min(1)] private int _popCount = 16;

        [Tooltip("A new reveal never uses one of this many pops shown last. Capped at one fewer than Pop Count, " +
                 "so there is always a pop to choose.")]
        [SerializeField] [Min(0)] private int _recentPopsAvoided = 2;

        public int PopCount => Mathf.Max(1, _popCount);
        public int RecentPopsAvoided => Mathf.Max(0, _recentPopsAvoided);
    }
}
