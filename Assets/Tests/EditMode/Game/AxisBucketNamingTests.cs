using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Telemetry;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    // AxisBucketNaming was extracted from TelemetryEnvelopeSerializer for W4's inverse (name -> index)
    // need. TelemetryEnvelopeSerializerTests already pins the forward direction (index -> name) through
    // the serializer's JSON output; these tests exercise the class directly rather than only through a
    // consumer, and cover the inverse (TryColorBucketIndexOf) that the serializer never needed.
    [TestFixture]
    public class AxisBucketNamingTests
    {
        private string[] _colorNames;

        [SetUp]
        public void SetUp()
        {
                        _colorNames = new[] { "Red", "Blue" };
        }

        [Test]
        public void BucketName_Color_WithinPalette_ReturnsThePaletteName()
        {
            Assert.AreEqual("Blue", AxisBucketNaming.BucketName(_colorNames, MetricAxis.Color, 1));
        }

        [Test]
        public void BucketName_Color_TrailingIndex_ReturnsOther()
        {
            Assert.AreEqual(AxisBucketNaming.OtherColorBucketName,
                AxisBucketNaming.BucketName(_colorNames, MetricAxis.Color, 2));
        }

        [Test]
        public void BucketName_BalloonType_ValidOrdinal_ReturnsTheEnumName()
        {
            Assert.AreEqual(BalloonType.Tough.ToString(),
                AxisBucketNaming.BucketName(_colorNames, MetricAxis.BalloonType, (int)BalloonType.Tough));
        }

        // Enum.ToString() on an undefined ordinal returns the integer as a string ("99") rather than
        // throwing — exactly the dense-index leak this whole naming scheme exists to keep off the wire.
        // Enum.IsDefined is what stands between a stale/out-of-range bucket and that leak.
        [Test]
        public void BucketName_BalloonType_UndefinedOrdinal_ReturnsUnknown_NotTheRawNumber()
        {
            Assert.AreEqual(AxisBucketNaming.UnknownBucketName,
                AxisBucketNaming.BucketName(_colorNames, MetricAxis.BalloonType, 9999));
        }

        [Test]
        public void BucketName_ItemType_ValidOrdinal_ReturnsTheEnumName()
        {
            Assert.AreEqual(ItemType.Bomb.ToString(),
                AxisBucketNaming.BucketName(_colorNames, MetricAxis.ItemType, (int)ItemType.Bomb));
        }

        [Test]
        public void BucketName_ItemType_UndefinedOrdinal_ReturnsUnknown()
        {
            Assert.AreEqual(AxisBucketNaming.UnknownBucketName,
                AxisBucketNaming.BucketName(_colorNames, MetricAxis.ItemType, 9999));
        }

        // MetricAxis has exactly three members and all three are handled — the default branch has no
        // live caller. Unlike MetricValueResolver's Kind switch (which degrades to a placeholder),
        // BucketName's default THROWS. Pinning the asymmetry: a corrupted MetricAxis is a louder failure
        // here than a corrupted MetricValueKind is in the resolver.
        [Test]
        public void BucketName_UndefinedAxis_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AxisBucketNaming.BucketName(_colorNames, (MetricAxis)99, 0));
        }

        [Test]
        public void TryColorBucketIndexOf_KnownName_ReturnsItsPaletteIndex()
        {
            var found = AxisBucketNaming.TryColorBucketIndexOf(_colorNames, "Blue", out var index);

            Assert.IsTrue(found);
            Assert.AreEqual(1, index);
        }

        [Test]
        public void TryColorBucketIndexOf_OtherSentinel_ReturnsTheTrailingIndex()
        {
            var found = AxisBucketNaming.TryColorBucketIndexOf(_colorNames, AxisBucketNaming.OtherColorBucketName,
                out var index);

            Assert.IsTrue(found);
            Assert.AreEqual(_colorNames.Length, index);
        }

        [Test]
        public void TryColorBucketIndexOf_UnknownName_ReturnsFalse()
        {
            var found = AxisBucketNaming.TryColorBucketIndexOf(_colorNames, "Chartreuse", out var index);

            Assert.IsFalse(found);
            Assert.AreEqual(-1, index);
        }

        [Test]
        public void TryColorBucketIndexOf_EmptyPalette_OtherResolvesToIndexZero()
        {
            var empty = Array.Empty<string>();

            var found = AxisBucketNaming.TryColorBucketIndexOf(empty, AxisBucketNaming.OtherColorBucketName,
                out var index);

            Assert.IsTrue(found);
            Assert.AreEqual(0, index);
        }

        // DESIGN GAP: the "other" sentinel is checked before the palette is searched by name, so a
        // palette that authors an actual progress color literally named "other" is unreachable by its
        // own name — the lookup always resolves to the trailing sentinel index instead of the color's
        // real position. The forward direction has the same ambiguity (BucketName renders both the real
        // "other" entry and the sentinel as the identical string "other"), and the editor drawer's own
        // popup would list two identically-labelled entries for the same reason. Pinned here as the
        // current, not-yet-fixed contract.
        [Test]
        public void TryColorBucketIndexOf_PaletteHasAColorLiterallyNamedOther_ResolvesToTheSentinelNotItsRealIndex()
        {
            var palette = new[] { "Red", "other" };

            var found = AxisBucketNaming.TryColorBucketIndexOf(palette, "other", out var index);

            Assert.IsTrue(found);
            Assert.AreEqual(2, index, "the real 'other' color lives at index 1; the sentinel is index 2 " +
                "(the palette length) — the lookup resolves to the sentinel, not the color's own index");
        }
    }
}
