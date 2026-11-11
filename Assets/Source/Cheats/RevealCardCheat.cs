#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE

using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Plays the card reveal for a random card, as if a level-up had just been won, without holding the card:
    ///     to look at the reveal without playing to a card level-up.
    /// </summary>
    internal class RevealCardCheat : ICheat
    {
        private readonly IUpgradeSettings _upgrades;
        private readonly IPublisher<UpgradeCardHeldMessage> _cardHeld;

        public string Name => "Reveal A Card";
        public string Section => "Run";
        public IReadOnlyList<string> Tags => new[] { "card", "reveal", "upgrade" };

        public RevealCardCheat(IUpgradeSettings upgrades, IPublisher<UpgradeCardHeldMessage> cardHeld)
        {
            _upgrades = upgrades;
            _cardHeld = cardHeld;
        }

        public void Execute()
        {
            var cards = _upgrades.Cards;
            if (cards.Count == 0)
            {
                return;
            }

            _cardHeld.Publish(new UpgradeCardHeldMessage(cards[Random.Range(0, cards.Count)], 1));
        }
    }
}
#endif
