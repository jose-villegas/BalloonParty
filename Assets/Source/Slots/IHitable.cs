namespace BalloonParty.Slots
{
    public interface IHitable
    {
        HitOutcome EvaluateHit(int damage);
    }
}
