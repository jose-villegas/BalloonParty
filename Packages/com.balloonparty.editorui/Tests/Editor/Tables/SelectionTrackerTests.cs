using System.Collections.Generic;
using BalloonParty.EditorUI.Tables;
using NUnit.Framework;

namespace BalloonParty.EditorUI.Tests.Tables
{
    [TestFixture]
    public class SelectionTrackerTests
    {
        [Test]
        public void GetSelected_EmptyList_ReturnsEmptyList()
        {
            var selected = SelectionTracker.GetSelected(new List<SelectableItem>());

            Assert.That(selected, Is.Empty);
        }

        [Test]
        public void GetSelected_AllSelected_ReturnsAllItems()
        {
            var items = new List<SelectableItem>
            {
                new SelectableItem(true),
                new SelectableItem(true)
            };

            var selected = SelectionTracker.GetSelected(items);

            Assert.That(selected, Is.EqualTo(items));
        }

        [Test]
        public void GetSelected_NoneSelected_ReturnsEmptyList()
        {
            var items = new List<SelectableItem>
            {
                new SelectableItem(false),
                new SelectableItem(false)
            };

            var selected = SelectionTracker.GetSelected(items);

            Assert.That(selected, Is.Empty);
        }

        [Test]
        public void GetSelected_MixedSelection_ReturnsOnlySelectedItemsInSourceOrder()
        {
            var first = new SelectableItem(true);
            var second = new SelectableItem(false);
            var third = new SelectableItem(true);
            var items = new List<SelectableItem> { first, second, third };

            var selected = SelectionTracker.GetSelected(items);

            Assert.That(selected, Is.EqualTo(new[] { first, third }));
        }

        private sealed class SelectableItem : ISelectable
        {
            public SelectableItem(bool selected)
            {
                Selected = selected;
            }

            public bool Selected { get; set; }
        }
    }
}
