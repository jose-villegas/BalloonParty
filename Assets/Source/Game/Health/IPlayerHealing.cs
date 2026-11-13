namespace BalloonParty.Game.Health
{
    /// <summary>Gives hearts back, never past <see cref="IPlayerHealth.Max" /> (the reward balloons' payout).</summary>
    internal interface IPlayerHealing
    {
        void Heal(int amount);
    }
}
