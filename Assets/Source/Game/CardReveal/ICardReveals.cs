using UniRx;

namespace BalloonParty.Game.CardReveal
{
    /// <summary>
    ///     The reveals a stage plays, one at a time. A stage (the reveal's view) attaches while it's on screen; with
    ///     none attached nothing is queued, and the tooltip names the cards instead.
    /// </summary>
    internal interface ICardReveals
    {
        /// <summary>The reveal to play now, or null between reveals.</summary>
        IReadOnlyReactiveProperty<RevealedCard> Current { get; }

        /// <summary>Whether a stage is attached, so cards are announced by the reveal rather than the tooltip.</summary>
        bool HasStage { get; }

        /// <summary>
        ///     The pop the next reveal will play, chosen a reveal ahead so the stage can load its sheet while the
        ///     game plays. Chosen when a stage first attaches, and again as each reveal starts with the last one;
        ///     -1 before a stage attaches.
        /// </summary>
        int NextPop { get; }

        void AttachStage();

        void DetachStage();

        /// <summary>
        ///     The stage is done with <paramref name="done" />: if it's still <see cref="Current" />, the next queued
        ///     reveal, if any, starts. A reveal a reset or detach already dropped finishes nothing, so a stage still
        ///     animating it can't cut off the reveal that replaced it.
        /// </summary>
        void Finish(RevealedCard done);
    }
}
