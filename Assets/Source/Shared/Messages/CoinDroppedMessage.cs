using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>A pop dropped <see cref="Amount" /> coins at <see cref="WorldPosition" />, on the board.</summary>
    public readonly struct CoinDroppedMessage
    {
        public readonly int Amount;
        public readonly Vector3 WorldPosition;

        public CoinDroppedMessage(int amount, Vector3 worldPosition)
        {
            Amount = amount;
            WorldPosition = worldPosition;
        }
    }
}
