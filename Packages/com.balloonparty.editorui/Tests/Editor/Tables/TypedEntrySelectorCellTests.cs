using BalloonParty.EditorUI.Tables;
using NUnit.Framework;

namespace BalloonParty.EditorUI.Tests.Tables
{
    [TestFixture]
    public class TypedEntrySelectorCellTests
    {
        [Test]
        public void BuildDropdownNames_AllPresent_KeepsSelectedPrefixOnlyOnSelectedEntry()
        {
            var names = TypedEntrySelectorCell<TestEnum>.BuildDropdownNames(
                new[] { "Alpha", "Beta", "Gamma" },
                new[] { true, true, true },
                1);

            Assert.That(names, Is.EqualTo(new[] { "✓ Alpha", "► Beta", "✓ Gamma" }));
        }

        [Test]
        public void BuildDropdownNames_NonePresent_UsesBlankPrefixForUnselectedEntries()
        {
            var names = TypedEntrySelectorCell<TestEnum>.BuildDropdownNames(
                new[] { "Alpha", "Beta", "Gamma" },
                new[] { false, false, false },
                1);

            Assert.That(names, Is.EqualTo(new[] { "  Alpha", "► Beta", "  Gamma" }));
        }

        [Test]
        public void BuildDropdownNames_MixedPresence_UsesCheckmarkOnlyForPresentEntries()
        {
            var names = TypedEntrySelectorCell<TestEnum>.BuildDropdownNames(
                new[] { "Alpha", "Beta", "Gamma", "Delta" },
                new[] { true, false, true, false },
                3);

            Assert.That(names, Is.EqualTo(new[] { "✓ Alpha", "  Beta", "✓ Gamma", "► Delta" }));
        }

        [Test]
        public void BuildDropdownNames_SelectedIndexBoundary_HandlesFirstAndLastEntries()
        {
            var firstSelected = TypedEntrySelectorCell<TestEnum>.BuildDropdownNames(
                new[] { "Alpha", "Beta", "Gamma" },
                new[] { false, true, false },
                0);
            var lastSelected = TypedEntrySelectorCell<TestEnum>.BuildDropdownNames(
                new[] { "Alpha", "Beta", "Gamma" },
                new[] { false, true, true },
                2);

            Assert.That(firstSelected, Is.EqualTo(new[] { "► Alpha", "✓ Beta", "  Gamma" }));
            Assert.That(lastSelected, Is.EqualTo(new[] { "  Alpha", "✓ Beta", "► Gamma" }));
        }

        private enum TestEnum
        {
            Alpha,
            Beta,
            Gamma,
            Delta
        }
    }
}
