namespace BalloonParty.Slots.Capabilities
{
    public interface IHitable
    {
        HitOutcome EvaluateHit(int damage);
    }
}
