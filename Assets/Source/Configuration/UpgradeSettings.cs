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
            "taken off per stack (0.15 = 15% faster). Item Odds: chance of one more item per item roll, times each item's share. Raise Limits: " +
            "how much higher every other card's limit goes per stack. Snipe Chance: the chance per stack that a " +
            "loaded ball starts Snipe-armed (the total stops at 1).")]
        [SerializeField] [Min(0f)] private float _amount = 1f;

        [Tooltip("Most times a run can hold this card, before Raise Limits cards raise it; at its limit it is no " +
            "longer dealt.")]
        [SerializeField] [Min(1)] private int _maxStacks = 3;

        [Tooltip("Relative odds of this card being dealt. 0 keeps it out of the deck.")]
        [SerializeField] [Min(0f)] private float _weight = 1f;

        [Tooltip("Extra odds of this card being the bonus card a bought retry deals: its weight times (1 + this), " +
            "so 1 = twice as likely. A retry follows a lost run, so the cards that keep the player alive should " +
            "come likelier. 0 = no extra.")]
        [SerializeField] [Min(0f)] private float _retryDealBoost;

        [Tooltip("The run's first card is this one, whatever deals it (the first level-up, a golden balloon, an " +
            "ad's bonus card), so every run starts the same way. The first card in the list with this set wins.")]
        [SerializeField] private bool _dealtFirst;

        public UpgradeKind Kind => _kind;
        public string Title => _title;
        public string Description => _description;
        public float Amount => _amount;
        public int MaxStacks => _maxStacks;
        public float Weight => _weight;
        public float RetryDealBoost => _retryDealBoost;
        public bool DealtFirst => _dealtFirst;

        // Unity's serializer and the inspector's "add element" construct through this one.
        internal UpgradeCardSettings()
        {
        }

        internal UpgradeCardSettings(UpgradeKind kind, string title, string description, float amount,
            int maxStacks, float weight = 1f, float retryDealBoost = 0f, bool dealtFirst = false)
        {
            _dealtFirst = dealtFirst;
            _retryDealBoost = retryDealBoost;
            _kind = kind;
            _title = title;
            _description = description;
            _amount = amount;
            _maxStacks = maxStacks;
            _weight = weight;
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
            new(UpgradeKind.Shield, "Thick Skin", "+1 shield on every pen", 1f, 3, retryDealBoost: 1f),
            new(UpgradeKind.ItemOdds, "Lucky Find", "More items on the board", 0.1f, 4),
            new(UpgradeKind.RaiseLimits, "Raise the Bar", "+1 to every card's limit", 1f, 2, 0.5f),
            new(UpgradeKind.SnipeChance, "Sharpshooter", "Increased Snipe chance", 0.01f, 3, dealtFirst: true),
            new(UpgradeKind.RewardOdds, "Party Favors", "Increased gift balloon chance", 0.25f, 3)
        };

        [Tooltip("Seconds of game time after a pen loads as a Snipe (the Sharpshooter card) before another can, so " +
            "rapid fire can't chain them. 0 = every load rolls.")]
        [SerializeField] [Min(0f)] private float _snipeCooldownSeconds = 8f;

        [Tooltip("On a purple (extra-colour) level, the Sharpshooter card's Snipe chance times this, for the armoured " +
            "golden balloons there. 3 = a 1% card rolls 3% a pen. 1 = no change.")]
        [SerializeField] [Min(1f)] private float _purpleLevelSnipeChanceScale = 3f;

        public int CardEveryLevels => _cardEveryLevels;
        public int CardsOffered => _cardsOffered;
        public IReadOnlyList<IUpgradeCardSettings> Cards => _cards;
        public float SnipeCooldownSeconds => _snipeCooldownSeconds;
        public float PurpleLevelSnipeChanceScale => _purpleLevelSnipeChanceScale;
    }
}
