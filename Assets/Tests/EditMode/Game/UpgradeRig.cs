using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Game.Upgrades;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using UniRx;

namespace BalloonParty.Tests.Game
{
    /// <summary>
    ///     A <see cref="RunUpgrades" /> wired to a hand-driven level-up phase, so a test can present a
    ///     level-up, pick, and advance or abort it the way <c>LevelController</c> would.
    /// </summary>
    internal sealed class UpgradeRig
    {
        internal readonly ReactiveProperty<LevelUpPhase> Phase = new(LevelUpPhase.Playing);
        internal readonly ReactiveProperty<int> Level = new(1);
        internal readonly List<IUpgradeCardSettings> Cards = new();
        internal readonly IUpgradeSettings Settings = Substitute.For<IUpgradeSettings>();
        internal readonly IRetryState RetryState = Substitute.For<IRetryState>();
        internal readonly IPublisher<BonusCardDealtMessage> BonusDealt =
            Substitute.For<IPublisher<BonusCardDealtMessage>>();
        internal readonly RunUpgrades Upgrades;

        internal UpgradeRig(IProjectileFlightConfig flight)
        {
            Settings.CardEveryLevels.Returns(2);
            Settings.CardsOffered.Returns(3);
            Settings.Cards.Returns(Cards);

            var pacing = Substitute.For<ILevelPacingConfiguration>();
            pacing.SeedForLevel(Arg.Any<int>()).Returns(call => call.Arg<int>() * 7919);

            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Phase.Returns(Phase);
            levelProgress.Level.Returns(Level);

            // A fresh run from a cheat start level takes the cards on the way there. Play mode can leave that
            // static set when domain reload is off; it is read again from EditorPrefs on the next play start.
            BalloonParty.Cheats.CheatState.StartLevel = 1;

            Upgrades = new RunUpgrades(flight, Settings, pacing, RetryState, levelProgress, BonusDealt);
            Upgrades.Start();
        }

        /// <summary>Presents the level-up into <paramref name="newLevel" /> and returns the hand it deals.</summary>
        internal IReadOnlyList<IUpgradeCardSettings> Present(int newLevel)
        {
            Level.Value = newLevel - 1;
            Phase.Value = LevelUpPhase.Pending;
            return Upgrades.Offer;
        }

        /// <summary>The pending level-up is dismissed: the level advances, then the Ascent settles.</summary>
        internal void Advance()
        {
            Level.Value++;
            Phase.Value = LevelUpPhase.Transitioning;
            Phase.Value = LevelUpPhase.Playing;
        }

        /// <summary>The pending level-up is aborted: back to play, the level unchanged.</summary>
        internal void Abort()
        {
            Phase.Value = LevelUpPhase.Playing;
        }

        /// <summary>
        ///     The reset of a retry at <paramref name="level" />, bought with a rewarded ad when
        ///     <paramref name="withBonusCard" />. The staged retry is consumed afterwards, as RetryTracker would.
        /// </summary>
        internal void Retry(int level, bool withBonusCard)
        {
            RetryState.RetryLevel.Returns(level);
            RetryState.RetryWithBonusCard.Returns(withBonusCard);
            Level.Value = level;
            Upgrades.ResetRun(1);
            RetryState.RetryLevel.Returns(0);
            RetryState.RetryWithBonusCard.Returns(false);
        }

        /// <summary>Holds <paramref name="card" /> through a real deal, pick and advance.</summary>
        internal void Hold(IUpgradeCardSettings card)
        {
            var deck = new List<IUpgradeCardSettings>(Cards);
            Cards.Clear();
            Cards.Add(card);
            Settings.CardEveryLevels.Returns(1);

            Present(Level.Value + 1);
            Upgrades.Pick(0);
            Advance();

            Cards.Clear();
            Cards.AddRange(deck);
            Settings.CardEveryLevels.Returns(2);
        }

        internal static IUpgradeCardSettings Card(UpgradeKind kind, float amount, int maxStacks = 3)
        {
            var card = Substitute.For<IUpgradeCardSettings>();
            card.Kind.Returns(kind);
            card.Amount.Returns(amount);
            card.MaxStacks.Returns(maxStacks);
            card.Weight.Returns(1f);
            card.Title.Returns(kind.ToString());
            return card;
        }
    }
}
