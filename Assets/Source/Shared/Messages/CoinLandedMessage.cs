using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A dropped coin reached the counter (or was credited at once, with no counter to fly to). The wallet
    ///     counts coins here, not at the drop, so a coin only counts once the player has seen it arrive.
    /// </summary>
    public readonly struct CoinLandedMessage
    {
        public readonly int Amount;
        public readonly Vector3 WorldPosition;

        public CoinLandedMessage(int amount, Vector3 worldPosition)
        {
            Amount = amount;
            WorldPosition = worldPosition;
        }
    }
}
