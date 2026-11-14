namespace BalloonParty.Game.Health
{
    /// <summary>Gives hearts back (the reward balloons' payout). Neither brings back a player with none left.</summary>
    internal interface IPlayerHealing
    {
        /// <summary>Up to <see cref="IPlayerHealth.Max" />, keeping any extra hearts the player already has.</summary>
        void Heal(int amount);

        /// <summary>Past <see cref="IPlayerHealth.Max" />: extra hearts, a purple level's heart balloons' pay.</summary>
        void HealPastMax(int amount);
    }
}
