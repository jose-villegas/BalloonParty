namespace BalloonParty.Shared.Pool
{
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }
}
