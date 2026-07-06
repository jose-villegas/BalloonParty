namespace BalloonParty.Shared.Messages
{
    /// <summary>Publish <see cref="ActorHitMessage"/> only through this — publishing to the broker directly skips scoring and the owning actor's reaction.</summary>
    public interface IHitDispatcher
    {
        void Dispatch(ActorHitMessage msg);
    }
}
