using System;
using BalloonParty.Configuration;
using BalloonParty.Editor;
using BalloonParty.Shared;
using NUnit.Framework;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class UpgradeCardTextTests
    {
        [Test]
        public void Title_PrefixesTheCardsIconByItsKind_TintedWithTheText()
        {
            var card = new UpgradeCardSettings(UpgradeKind.ItemOdds, "Lucky Find", "More items", 0.1f, 4);

            Assert.AreEqual("<sprite=\"CardIcons\" name=\"ItemOdds\" tint=1> Lucky Find", UpgradeCardText.Title(card));
        }

        [Test]
        public void IconAtlas_HasAnIconForEveryKind()
        {
            foreach (var kind in Enum.GetNames(typeof(UpgradeKind)))
            {
                CollectionAssert.Contains(CardIconSpriteAssetBuilder.IconNames, kind, $"No card icon named {kind}");
            }
        }
    }
}
