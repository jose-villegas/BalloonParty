using UnityEngine;

namespace BalloonParty.Game.Coins
{
    /// <summary>
    ///     Keeps the balance in <see cref="PlayerPrefs" />, beside the best run (see <c>RunMeta</c>). Coins are
    ///     progression, so they stay on the device; telemetry reports them separately.
    /// </summary>
    internal sealed class PlayerPrefsCoinStore : ICoinStore
    {
        private const string BalanceKey = "Coins";

        public int Load()
        {
            return PlayerPrefs.GetInt(BalanceKey, 0);
        }

        // PlayerPrefs.Save is slow on mobile, which is why the wallet saves only at its save points.
        public void Save(int balance)
        {
            PlayerPrefs.SetInt(BalanceKey, balance);
            PlayerPrefs.Save();
        }
    }
}
