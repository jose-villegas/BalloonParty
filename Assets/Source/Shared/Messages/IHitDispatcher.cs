namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Entry point for actor hits. <see cref="Dispatch"/> runs the order-dependent stages
    ///     (streak/score recording first) synchronously before broadcasting the message to
    ///     order-independent observers — callers may rely on those stages having completed when
    ///     it returns. Publish <see cref="ActorHitMessage"/> only through this; publishing to the
    ///     broker directly skips scoring and the owning actor's reaction.
    /// </summary>
    public interface IHitDispatcher
    {
        void Dispatch(ActorHitMessage msg);
    }
}
