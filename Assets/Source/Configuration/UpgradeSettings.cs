using System;
using System.Collections.Generic;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>Inspector-authored <see cref="IUpgradeCardSettings" />.</summary>
    [Serializable]
    internal class UpgradeCardSettings : IUpgradeCardSettings
    {
        [SerializeField] private UpgradeKind _kind;
        [SerializeField] private string _title;
        [SerializeField] private string _description;

        [Tooltip("Ball cap and Shield: whole units per stack. Faster Reload: the share of reload time " +
            "taken off per stack (0.15 = 15% faster). Item Odds: chance of one more item per item roll.")]
        [SerializeField] [Min(0f)] private float _amount = 1f;

        [Tooltip("Most times a run can hold this card; once maxed it is no longer dealt.")]
        [SerializeField] [Min(1)] private int _maxStacks = 3;

        [Tooltip("Relative odds of this card being dealt. 0 keeps it out of the deck.")]
        [SerializeField] [Min(0f)] private float _weight = 1f;

        public UpgradeKind Kind => _kind;
        public string Title => _title;
        public string Description => _description;
        public float Amount => _amount;
        public int MaxStacks => _maxStacks;
        public float Weight => _weight;

        // Unity's serializer and the inspector's "add element" construct through this one.
        internal UpgradeCardSettings()
        {
        }

        internal UpgradeCardSettings(UpgradeKind kind, string title, string description, float amount,
            int maxStacks)
        {
            _kind = kind;
            _title = title;
            _description = description;
            _amount = amount;
            _maxStacks = maxStacks;
        }
    }

    /// <summary>
    ///     Inspector-authored <see cref="IUpgradeSettings" />, serialized inside <see cref="RunConfig" />:
    ///     cards are run-scoped, so they live with the other run rules.
    /// </summary>
    /// <remarks>
    ///     Field initialisers carry the first deck, so an asset saved before the block existed still
    ///     deals cards.
    /// </remarks>
    [Serializable]
    internal class UpgradeSettings : IUpgradeSettings
    {
        [Tooltip("A card pick every this many level-ups; every other level-up passes with no screen. " +
            "0 = never deal cards.")]
        [SerializeField] [Min(0)] private int _cardEveryLevels = 2;

        [SerializeField] [Min(1)] private int _cardsOffered = 3;

        [SerializeField] private UpgradeCardSettings[] _cards =
        {
            new(UpgradeKind.BallCap, "Extra Pen", "+1 pen in flight", 1f, 3),
            new(UpgradeKind.FasterReload, "Quick Hands", "Reload 15% faster", 0.15f, 4),
            new(UpgradeKind.Shield, "Thick Skin", "+1 shield on every pen", 1f, 3),
            new(UpgradeKind.ItemOdds, "Lucky Find", "More items on the board", 0.5f, 4)
        };

        public int CardEveryLevels => _cardEveryLevels;
        public int CardsOffered => _cardsOffered;
        public IReadOnlyList<IUpgradeCardSettings> Cards => _cards;
    }
}
