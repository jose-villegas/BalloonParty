namespace BalloonParty.Game.Health
{
    internal sealed class LossForecast : ILossForecast
    {
        private readonly IPlayerHealth _health;
        private readonly IPendingHealthCharges _pending;

        // Also true once HP is already 0 (0 pending >= 0 remaining) — a dead run stays doomed.
        public bool LossImminent => _pending.PendingCharges >= _health.Current.Value;

        public LossForecast(IPlayerHealth health, IPendingHealthCharges pending)
        {
            _health = health;
            _pending = pending;
        }
    }
}
