namespace BalloonParty.Game.Health
{
    /// <summary>
    ///     Under the line-based damage model, HP drains immediately at wave resolution — there are no
    ///     pending charges. Loss is imminent when HP is already at zero.
    /// </summary>
    internal sealed class LossForecast : ILossForecast
    {
        private readonly IPlayerHealth _health;

        public bool LossImminent => _health.Current.Value <= 0;

        public LossForecast(IPlayerHealth health)
        {
            _health = health;
        }
    }
}
